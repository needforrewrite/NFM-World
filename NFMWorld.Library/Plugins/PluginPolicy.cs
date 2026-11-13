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
/// <see cref="VendorPluginVisibleAttribute"/>.
/// </summary>
/// <remarks>
/// Without it a downloaded plugin runs with the host's privileges - filesystem, network,
/// reflection, native libraries. Under it, a reach for an unmarked member throws
/// <see cref="System.Security.SecurityException"/>.
///
/// The visible surface is read from the attributes rather than listed by hand, so a
/// member cannot be reachable and unmarked at once, and exposing one stays a one-line
/// change next to the code.
///
/// What each marker means:
///
/// - <see cref="Visible.Self"/> on a type exposes the type and the members that carry
///   their own <c>[PluginVisible]</c>. It grants nothing by itself, and in particular not
///   the type's private members.
/// - <c>Visible.IncludingAllMembersOfType</c> exposes the type's public members when they
///   have no marker of their own. It is how a base class a plugin derives from - where a
///   plugin's overrides call protected members - and a record's generated members are
///   reached. It still stops at public, except constructors: the public and protected
///   ones are admitted, since a plugin calls the protected constructor of the abstract
///   base it derives from.
/// - <see cref="VendorPluginVisibleAttribute"/> on the assembly names a type outside the
///   host (FixedMathSharp and friends) that the host's own visible members mention.
///
/// A handful of members the runtime and the plugin's own package make it reach for -
/// not part of the host surface, but unavoidable around it - are listed in
/// <see cref="AllowTightPack"/>.
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

    /// <summary>
    /// The formatters generated code constructs itself, one per member type it resolves through the
    /// provider. Kept in step with <c>TightPack.Generator.TypeParser.BuildFormatterExpression</c>:
    /// a type named there but missing here cannot be used by a plugin.
    /// </summary>
    private static readonly Type[] GeneratedFormatterTypes =
    [
        typeof(TightPack.Formatters.ArrayFormatter<>),
        typeof(TightPack.Formatters.TwoDimensionalArrayFormatter<>),
        typeof(TightPack.Formatters.ThreeDimensionalArrayFormatter<>),
        typeof(TightPack.Formatters.FourDimensionalArrayFormatter<>),
        typeof(TightPack.Formatters.ListFormatter<>),
        typeof(TightPack.Formatters.DictionaryFormatter<,>),
        typeof(TightPack.Formatters.HashSetFormatter<>),
        typeof(TightPack.Formatters.KeyValuePairFormatter<,>),
        typeof(TightPack.Formatters.EnumerableFormatter<>),
        typeof(TightPack.Formatters.InterfaceListFormatter<>),
        typeof(TightPack.Formatters.IReadOnlyListFormatter<>),
        typeof(TightPack.Formatters.InterfaceCollectionFormatter<>),
        typeof(TightPack.Formatters.IReadOnlyCollectionFormatter<>),
        typeof(TightPack.Formatters.InterfaceDictionaryFormatter<,>),
        typeof(TightPack.Formatters.IReadOnlyDictionaryFormatter<,>),
        typeof(TightPack.Formatters.InterfaceSetFormatter<>),
        typeof(TightPack.Formatters.IReadOnlySetFormatter<>),
        typeof(TightPack.Formatters.StackFormatter<>),
        typeof(TightPack.Formatters.QueueFormatter<>),
        typeof(TightPack.Formatters.LinkedListFormatter<>),
        typeof(TightPack.Formatters.SortedSetFormatter<>),
        typeof(TightPack.Formatters.PriorityQueueFormatter<,>),
        typeof(TightPack.Formatters.SortedDictionaryFormatter<,>),
        typeof(TightPack.Formatters.SortedListFormatter<,>),
        typeof(TightPack.Formatters.CollectionFormatter<>),
        typeof(TightPack.Formatters.ObservableCollectionFormatter<>),
        typeof(TightPack.Formatters.ReadOnlyCollectionFormatter<>),
        typeof(TightPack.Formatters.ReadOnlyObservableCollectionFormatter<>),
        typeof(TightPack.Formatters.ConcurrentQueueFormatter<>),
        typeof(TightPack.Formatters.ConcurrentStackFormatter<>),
        typeof(TightPack.Formatters.ConcurrentBagFormatter<>),
        typeof(TightPack.Formatters.ConcurrentDictionaryFormatter<,>),
        typeof(TightPack.Formatters.BlockingCollectionFormatter<>),
        typeof(TightPack.Formatters.ArraySegmentFormatter<>),
        typeof(TightPack.Formatters.ImmutableArrayFormatter<>),
        typeof(TightPack.Formatters.MemoryFormatter<>),
        typeof(TightPack.Formatters.ReadOnlyMemoryFormatter<>),
        typeof(TightPack.Formatters.MemoryByteFormatter),
        typeof(TightPack.Formatters.ReadOnlyMemoryByteFormatter),
        typeof(TightPack.Formatters.InlineArray2Formatter<>),
        typeof(TightPack.Formatters.InlineArray3Formatter<>),
        typeof(TightPack.Formatters.InlineArray4Formatter<>),
        typeof(TightPack.Formatters.InlineArray5Formatter<>),
        typeof(TightPack.Formatters.InlineArray6Formatter<>),
        typeof(TightPack.Formatters.InlineArray7Formatter<>),
        typeof(TightPack.Formatters.InlineArray8Formatter<>),
        typeof(TightPack.Formatters.InlineArray9Formatter<>),
        typeof(TightPack.Formatters.InlineArray10Formatter<>),
        typeof(TightPack.Formatters.InlineArray11Formatter<>),
        typeof(TightPack.Formatters.InlineArray12Formatter<>),
        typeof(TightPack.Formatters.InlineArray13Formatter<>),
        typeof(TightPack.Formatters.InlineArray14Formatter<>),
        typeof(TightPack.Formatters.InlineArray15Formatter<>),
        typeof(TightPack.Formatters.InlineArray16Formatter<>),
        typeof(TightPack.Formatters.NullableFormatter<>),
        typeof(TightPack.Formatters.BitArrayFormatter),
        typeof(TightPack.Formatters.TypeFormatter),
        typeof(TightPack.Formatters.ValueTupleFormatter<>),
        typeof(TightPack.Formatters.ValueTupleFormatter<,>),
        typeof(TightPack.Formatters.ValueTupleFormatter<,,>),
        typeof(TightPack.Formatters.ValueTupleFormatter<,,,>),
        typeof(TightPack.Formatters.ValueTupleFormatter<,,,,>),
        typeof(TightPack.Formatters.ValueTupleFormatter<,,,,,>),
        typeof(TightPack.Formatters.ValueTupleFormatter<,,,,,,>),
        typeof(TightPack.Formatters.ValueTupleFormatter<,,,,,,,>),
    ];

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

        // Nothing in a plugin needs to reinterpret memory: Unsafe.As/SizeOf has no length,
        // so nothing can check a reinterpretation through it. TightPack does its own unsafe
        // work inside its runtime assembly, so the plugin's generated code never needs it.
        //
        // The deny runs last, on the finished policy, and only over what it admits:
        // Unsafe is a type-forwarder, so reflecting over it returns System.Private.CoreLib
        // methods, and removing those by identity takes unrelated members of the same
        // module with them.
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
        // [VendorPluginVisible] names a type outside this assembly outright - a vendor
        // type with no host attributes of its own - along with how much of it is exposed.
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
            // A marked type contributes what it marked, member by member. Its bare marker
            // is not a grant of its whole surface: that is what IncludingAllMembersOfType
            // is for, and this path only runs for types that were not marked with it.
            var typeMarker = type.GetCustomAttributesData().FirstOrDefault(IsPluginVisible);
            if (typeMarker is not null)
            {
                Allow(builder, type, VisibilityOf(typeMarker));

                // Implementing a visible interface is itself a cross-assembly reach, so its
                // members have to be open - this is what lets an object returned as an
                // interface be called through it.
                foreach (var contract in type.GetInterfaces())
                    Allow(builder, Definition(contract), Visible.IncludingAllMembersOfType);
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
        AllowTightPack(builder);
    }

    /// <summary>
    /// The TightPack members a plugin's generated code calls.
    /// </summary>
    /// <remarks>
    /// Generated serialization code is ordinary plugin IL, so it only reaches what is on
    /// the policy like any other code - but a plugin that cannot serialize an event is not
    /// a gamemode, so this set has to be complete enough to run it.
    ///
    /// The members here are named one at a time, and none of them can be given a wrong
    /// length: every read and write goes through <see cref="TightPack.TightPackReader"/> or
    /// <see cref="TightPack.TightPackWriter{TBufferWriter}"/>, whose span accessors check
    /// the requested size against the buffer and throw on overrun. That is what separates
    /// them from <c>System.Runtime.CompilerServices.Unsafe</c>, which is denied below
    /// because it takes no length at all.
    ///
    /// Only blitting is exempt, and only for a struct the generator proved has no padding:
    /// <c>WriteBlittable</c> is called with a compile-time size, and the generated static
    /// field asserts it against the runtime.
    /// </remarks>
    private static void AllowTightPack(CasPolicyBuilder builder)
    {
        // The base every generated formatter derives from, and the registration path its
        // static constructor drives.
        builder.Allow(new TypeBinding(typeof(TightPack.TightPackFormatter<>), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(TightPack.TightPackableFormatter<>), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(TightPack.TightPackSerializer), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(TightPack.TightPackFormatterProvider), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(TightPack.TightPackSerializationException), Accessibility.Public));
        builder.Allow(new TypeBinding(typeof(TightPack.TightPackBlit), Accessibility.Public));

        // Generated code names a member's formatter directly in a static field initializer, so a
        // plugin that declares a collection, a multi-dimensional array or a Type member uses this
        // type itself and not only the reader/writer below. Each one is a thin serializer over
        // those same primitives.
        foreach (Type formatter in GeneratedFormatterTypes)
        {
            builder.Allow(new TypeBinding(formatter, Accessibility.Public));
        }

        // Reader and writer primitives, by signature so the generic instantiations the
        // generated code calls are the only ones reachable. Every one of them goes through
        // GetSpan, which checks the requested size against the buffer.
        foreach (var method in typeof(TightPack.TightPackReader).GetMethods(AllMembers)
            .Where(m => m.Name.StartsWith("Read", StringComparison.Ordinal)
                        || m.Name.StartsWith("TryRead", StringComparison.Ordinal)
            ))
            builder.Allow(method);

        foreach (var method in typeof(TightPack.TightPackWriter<>).GetMethods(AllMembers)
            .Where(m => m.Name.StartsWith("Write", StringComparison.Ordinal)))
            builder.Allow(method);
    }

    /// <summary>
    /// CasCore's default sandbox, minus its System.Text.Json group.
    /// </summary>
    /// <remarks>
    /// <see cref="CasPolicyBuilderExtensions.WithDefaultSandbox"/> throws on this
    /// runtime: its JsonSerializerOptions binding names members the .NET 11 BCL no
    /// longer has, and the binding is eager. Plugins serialize their own events with
    /// TightPack, so JSON is dropped rather than re-listed; restore it here if a plugin
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
    {
        // Self promises a type whose members are visible one by one, each marked
        // [PluginVisible] - it admits no members of its own, only the type, whose marked
        // members AllowMember adds. The IncludingAllMembersOfType variants make the type's
        // own members visible without a marker, and that stops at public: a plugin never
        // needs a host type's internals.
        if (visible == Visible.Self)
            return;

        builder.Allow(new TypeBinding(type, Accessibility.Public));

        // TypeBinding drops protected constructors, but a plugin derives from these bases
        // and calls that constructor, which is protected on an abstract class.
        builder.Allow(Constructors(type));
    }

    private static IEnumerable<ConstructorInfo> Constructors(Type type)
        => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(c => c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly);

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
