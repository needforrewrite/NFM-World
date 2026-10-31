using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using DouglasDwyer.CasCore;
using NFMWorld.PluginSystem;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;
using NFMWorld.DriverInterface;

namespace NFMWorldLibrary.Plugins;

/// <summary>
/// The CasCore policy every plugin assembly is loaded under: CasCore's default sandbox
/// plus exactly what the host marked <see cref="PluginVisibleAttribute"/> or
/// <see cref="PublicVisibleAttribute{T}"/>.
/// </summary>
/// <remarks>
/// Without it a downloaded plugin runs with the host's privileges - filesystem, network,
/// reflection, native libraries. Under it, a reach for an unmarked member throws
/// <see cref="System.Security.SecurityException"/>.
///
/// The visible surface is read from the attributes rather than listed by hand, so a
/// member cannot be reachable and unmarked at once, and exposing one stays a one-line
/// change next to the code. <see cref="Visible.Self"/> exposes a type's public members;
/// the <c>IncludingAllMembersOfType</c> variants expose all of them.
///
/// A handful of members the runtime and the plugin's own package make it reach for -
/// not part of the host surface, but unavoidable around it - are listed in
/// <see cref="RuntimeReachable"/>.
///
/// Only built once a plugin is actually loaded. Not supported where the runtime cannot
/// JIT the instrumented IL.
/// </remarks>
internal static class PluginPolicy
{
    private static readonly Assembly[] Hosts =
    [
        typeof(PluginGamemode).Assembly,
        typeof(Keys).Assembly,
        typeof(f64Euler).Assembly,
        typeof(NFMWorld.Reactor.View).Assembly
    ];

    private static readonly Lazy<CasPolicy> Policy = new(Build);

    /// <summary>Whether the sandbox can run here: CasCore rewrites IL and needs a JIT.</summary>
    public static bool Supported => RuntimeFeature.IsDynamicCodeSupported;

    public static CasPolicy Instance => Policy.Value;

    [RequiresUnreferencedCode("Walks the host assemblies' attributes and types.")]
    private static CasPolicy Build()
    {
        var builder = DefaultSandbox();

        // Generated union formatters read value.GetType() to pick a tag.
        builder.Allow(typeof(object).GetMethod("GetType", Type.EmptyTypes)!);

        foreach (var assembly in Hosts)
            AllowVisible(builder, assembly);

        AllowRuntimeReachable(builder);

        // Nothing in a plugin needs to reinterpret memory. The one place the generator
        // emits it - Unsafe.As/SizeOf for unions and fixed sizes - is served by the fixed
        // wrappers in AllowMemoryPack, which carry their lengths.
        //
        // Deny only what is actually on the policy: Unsafe is a type-forwarder, so
        // reflecting over it returns System.Private.CoreLib methods, and removing those
        // by identity takes unrelated members of the same module with them.
        var policy = builder.Build();
        var unsafeType = typeof(Unsafe);
        foreach (var method in unsafeType.GetMethods(AllMembers).Where(m => m.DeclaringType == unsafeType))
        {
            if (policy.CanAccess(method))
                builder.Deny(method);
        }

        return builder.Build();
    }

    [RequiresUnreferencedCode("Walks the host assemblies' attributes and types.")]
    private static void AllowVisible(CasPolicyBuilder builder, Assembly assembly)
    {
        // [PublicVisible<T>] names a type outright - a vendor type with no host attributes
        // of its own - along with how much of it is exposed.
        foreach (var marker in assembly.GetCustomAttributesData())
        {
            if (marker.AttributeType is { IsGenericType: true } attribute
                && attribute.GetGenericTypeDefinition() == typeof(VendorPluginVisibleAttribute<>))
            {
                Allow(builder, attribute.GetGenericArguments()[0], VisibilityOf(marker));
            }
            
            if (marker.AttributeType == typeof(VendorPluginVisibleAttribute))
            {
                var type = (Type)marker.ConstructorArguments[0].Value!;
                Allow(builder, type, VisibilityOf(marker));
            }
        }

        foreach (var type in Types(assembly))
        {
            // A marked type is open through its whole surface: a plugin's subclass calls
            // protected members of it and is called back through it.
            if (type.GetCustomAttributesData().Any(IsPluginVisible))
            {
                Allow(builder, type, Visible.IncludingAllMembersOfType);

                // Its bases too, member by member: a plugin's override of Begin() reaches
                // the base implementation through the plugin's own type, which the policy
                // resolves back to the base's member.
                for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
                    AllowMembers(builder, baseType);

                // Implementing a visible interface is itself a cross-assembly reach, so its
                // members have to be open too - this is what lets an object returned as an
                // interface be called through it.
                foreach (var contract in type.GetInterfaces())
                    Allow(builder, Definition(contract), Visible.Self);
            }

            foreach (var member in type.GetMembers(AllMembers))
            {
                if (member.DeclaringType != type || member.MemberType == MemberTypes.NestedType)
                    continue;

                if (member.GetCustomAttributesData().Any(IsPluginVisible))
                    AllowMember(builder, member);
            }

        }
    }

    /// <summary>
    /// The host types a visible type unavoidably reaches at run time: they appear in
    /// member signatures, so the runtime reads their metadata whether or not the plugin
    /// touches them.
    /// </summary>
    /// <remarks>
    /// Only what the visible members' signatures name; nothing here is a permission a
    /// plugin needs for its own logic.
    /// </remarks>
    private static void AllowRuntimeReachable(CasPolicyBuilder builder)
    {
        AllowMemoryPack(builder);
    }

    /// <summary>
    /// The MemoryPack members a plugin's generated formatters call.
    /// </summary>
    /// <remarks>
    /// Generated formatters are ordinary plugin IL, so they only reach what is on the
    /// policy like any other code - but a plugin that cannot serialize an event is not a
    /// gamemode, so this set has to be complete enough to run them.
    ///
    /// The members here are named one at a time, and none of them can be given a wrong
    /// length: every read and write goes through <see cref="MemoryPack.MemoryPackReader"/>
    /// or <see cref="MemoryPack.MemoryPackWriter{TBufferWriter}"/>, whose span accessors
    /// check the requested size against the buffer and throw on overrun. That is what
    /// separates them from <c>System.Runtime.CompilerServices.Unsafe</c>, which is denied
    /// below: the generator also emits <c>Unsafe.As</c>/<c>Unsafe.SizeOf</c>, and those
    /// take no length at all.
    /// </remarks>
    private static void AllowMemoryPack(CasPolicyBuilder builder)
    {
        // The base every generated formatter derives from, and the registration path its
        // static constructor drives.
        builder.Allow(new TypeBinding(typeof(MemoryPack.MemoryPackFormatter<>), Accessibility.Protected));
        builder.Allow(new TypeBinding(typeof(MemoryPack.Formatters.MemoryPackableFormatter<>), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(MemoryPack.Formatters.ArrayFormatter<>), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(MemoryPack.MemoryPackSerializer), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(MemoryPack.MemoryPackFormatterProvider), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(MemoryPack.MemoryPackSerializationException), Accessibility.Public));

        // Reader and writer primitives, by signature so the generic instantiations the
        // generated code calls are the only ones reachable.
        foreach (var method in typeof(MemoryPack.MemoryPackReader).GetMethods(AllMembers)
                     .Where(m => m.Name is nameof(MemoryPack.MemoryPackReader.ReadPackable)
                         or nameof(MemoryPack.MemoryPackReader.ReadValue)
                         or nameof(MemoryPack.MemoryPackReader.TryReadObjectHeader)
                         or nameof(MemoryPack.MemoryPackReader.TryReadUnionHeader)
                         || m.Name.StartsWith("ReadUnmanaged", StringComparison.Ordinal)))
            builder.Allow(method);

        foreach (var method in typeof(MemoryPack.MemoryPackWriter<>).GetMethods(AllMembers)
                     .Where(m => m.Name is nameof(MemoryPack.MemoryPackWriter<>.WriteNullObjectHeader)
                         or nameof(MemoryPack.MemoryPackWriter<>.WriteNullUnionHeader)
                         or nameof(MemoryPack.MemoryPackWriter<>.WriteObjectHeader)
                         or nameof(MemoryPack.MemoryPackWriter<>.WriteUnionHeader)
                         or nameof(MemoryPack.MemoryPackWriter<>.WritePackable)
                         or nameof(MemoryPack.MemoryPackWriter<>.WriteValue)
                         // Unmanaged blobs: the generator emits these for plain fields, and
                         // they take the size from Unsafe.SizeOf internally, then hand it to
                         // GetSpanReference, which checks it against the buffer.
                         || m.Name.StartsWith("WriteUnmanaged", StringComparison.Ordinal)))
            builder.Allow(method);

        // The Unsafe.SizeOf replacement generated code uses.
        builder.Allow(new TypeBinding(typeof(MemoryPack.MemoryPackUnmanaged), Accessibility.Public));
    }

    /// <summary>
    /// CasCore's default sandbox, minus its System.Text.Json group.
    /// </summary>
    /// <remarks>
    /// <see cref="CasPolicyBuilderExtensions.WithDefaultSandbox"/> throws on this
    /// runtime: its JsonSerializerOptions binding names members the .NET 11 BCL no
    /// longer has, and the binding is eager. Plugins serialize their own events with
    /// MemoryPack, so JSON is dropped rather than re-listed; restore it here if a plugin
    /// ever needs System.Text.Json.
    /// </remarks>
    private static CasPolicyBuilder DefaultSandbox()
    {
        var builder = new CasPolicyBuilder();
        builder.WithSandboxedSystemCollectionsConcurrent();
        builder.WithSandboxedSystemCollections();
        builder.WithSandboxedSystemCollectionsImmutable();
        builder.WithSandboxedSystemCollectionsNonGeneric();
        builder.WithSandboxedSystemCollectionsSpecialized();
        builder.WithSandboxedSystemComponentModelTypeConverter();
        builder.WithSandboxedSystemConsole();
        builder.WithSandboxedSystemDiagnosticsDiagnosticSource();
        builder.WithSandboxedSystemLinq();
        builder.WithSandboxedSystemLinqParallel();
        builder.WithSandboxedSystemLinqQueryable();
        builder.WithSandboxedSystemObjectModel();
        builder.WithSandboxedSystem();
        builder.WithSandboxedSystemReflectionTypeExtensions();
        builder.WithSandboxedSystemRuntimeNumerics();
        builder.WithSandboxedSystemRuntimeSerializationFormatters();
        builder.WithSandboxedSystemTextRegularExpressions();
        builder.WithSandboxedSystemThreadingChannels();
        builder.WithSandboxedSystemThreading();
        builder.WithSandboxedSystemThreadingTasksDataflow();
        builder.WithSandboxedSystemThreadingTasksParallel();
        builder.WithSandboxedSystemLinqExpressions();
        builder.WithSandboxedRuntimeReflection();
        return builder;
    }

    private static void Allow(CasPolicyBuilder builder, Type type, Visible visible)
        => builder.Allow(new TypeBinding(type, visible switch
        {
            // Accessibility is inclusive: Private admits every member, so a plugin's
            // subclass sees protected members it overrides.
            Visible.IncludingAllMembersOfType or Visible.IncludingAnyGenericInstantiationAndAllMembersOfType
                => Accessibility.Private,
            _ => Accessibility.Protected
        }));

    /// <summary>
    /// Allows the callable members behind a marked member: a property's or event's
    /// accessors, a field, a method. Anything else (a nested type, a property's name
    /// object) carries no policy identity and is skipped rather than admitted.
    /// </summary>
    private static void AllowMember(CasPolicyBuilder builder, MemberInfo member)
    {
        switch (member)
        {
            case PropertyInfo property:
                foreach (var accessor in property.GetAccessors(nonPublic: true))
                    builder.Allow(accessor);
                break;
            case EventInfo @event:
                if (@event.AddMethod is { } add)
                    builder.Allow(add);
                if (@event.RemoveMethod is { } remove)
                    builder.Allow(remove);
                break;
            case MethodBase or FieldInfo:
                builder.Allow(member);
                break;
        }
    }

    /// <summary>
    /// Allows every member a derived override can reach through its base - the whole
    /// contract, not just the public slice.
    /// </summary>
    private static void AllowMembers(CasPolicyBuilder builder, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        foreach (var member in type.GetMembers(AllMembers))
        {
            if (member.MemberType != MemberTypes.NestedType)
                AllowMember(builder, member);
        }
    }

    private static Type Definition(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    private static Visible VisibilityOf(CustomAttributeData marker)
        => marker.ConstructorArguments.Count == 0 ? Visible.Self : (Visible)(int)marker.ConstructorArguments[^1].Value!;

    private static bool IsPluginVisible(CustomAttributeData attribute)
        => attribute.AttributeType == typeof(PluginVisibleAttribute);

    /// <summary>Every type the assembly defines, including ones that fail to load.</summary>
    [RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetTypes()")]
    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
}
