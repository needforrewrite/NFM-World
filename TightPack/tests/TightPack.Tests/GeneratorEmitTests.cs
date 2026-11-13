using System.Collections.Immutable;

namespace TightPack.Tests;

/// <summary>
/// Asserts on what the generator writes, for behaviour that cannot be observed at runtime.
/// </summary>
[TestClass]
public sealed class GeneratorEmitTests
{
    /// <summary>
    /// A packable struct reached only through <c>Nullable&lt;T&gt;</c> has no generated member of its
    /// own to register it, so the wrapping type must. Without that registration the provider falls
    /// back to building the formatter by reflection: correct under JIT but invisible to the AOT
    /// compiler, so only a published build would fail.
    /// </summary>
    [TestMethod]
    public void WrapperType_RegistersItsPackableArgument()
    {
        ImmutableDictionary<string, string> sources = GeneratorDiagnosticsTests.RunAndGetSources(
            """
            [TightPackable]
            public partial struct Inner
            {
                public int Value { get; set; }
            }

            [TightPackable]
            public partial struct Outer
            {
                public Inner? Maybe { get; set; }
            }
            """
        );

        // Outer's own output: Inner's generated file names its own formatter too.
        string outer = sources["Outer.TightPack.g.cs"];

        Assert.IsTrue(
            outer.Contains("TightPackableFormatter<global::Probe.Inner>"),
            "Outer does not register Inner's formatter, so it can only be found by reflection:\n" + outer
        );
    }

    /// <summary>
    /// A tuple member is written element by element. Routing it through ValueTupleFormatter instead
    /// resolves an element formatter per element, which measured ~2.7ns of the ~6.4ns each element
    /// cost. The wire bytes and the runtime behaviour are identical either way, so only the emitted
    /// code shows whether this is still true.
    /// </summary>
    [TestMethod]
    public void TupleMember_IsWrittenElementByElement()
    {
        ImmutableDictionary<string, string> sources = GeneratorDiagnosticsTests.RunAndGetSources(
            """
            [TightPackable]
            public partial struct Holder
            {
                public (int A, float B) Pair { get; set; }
            }
            """
        );

        string holder = sources["Holder.TightPack.g.cs"];

        Assert.IsFalse(
            holder.Contains("ValueTupleFormatter"),
            "Holder routes its tuple through a formatter instead of writing the elements."
        );
        Assert.IsTrue(
            holder.Contains("WriteInt32(__w_Pair.Item1)") &&
            holder.Contains("WriteSingle(__w_Pair.Item2)"),
            "Holder does not write the tuple elements directly."
        );
    }

    /// <summary>
    /// The counterpart: a plain packable member writes its own registration, so the wrapper must not
    /// duplicate it.
    /// </summary>
    [TestMethod]
    public void DirectPackableMember_IsNotRegisteredByTheWrapper()
    {
        ImmutableDictionary<string, string> sources = GeneratorDiagnosticsTests.RunAndGetSources(
            """
            [TightPackable]
            public partial struct OnlyInner
            {
                public int Value { get; set; }
            }

            [TightPackable]
            public partial struct Holder
            {
                public OnlyInner Direct { get; set; }
            }
            """
        );

        string holder = sources["Holder.TightPack.g.cs"];

        Assert.IsFalse(
            holder.Contains("TightPackableFormatter<global::Probe.OnlyInner>"),
            "Holder duplicates a registration OnlyInner already makes for itself:\n" + holder
        );
    }

    /// <summary>
    /// A readonly private field cannot be assigned by the deserializer, but a blitted value is
    /// rebuilt from its whole image, so no assignment is needed and the constructor gate stops
    /// applying. Types shaped like <c>CompactIpAddress</c> depend on this.
    /// </summary>
    [TestMethod]
    public void IncludedReadonlyFields_AreBlitted()
    {
        ImmutableDictionary<string, string> sources = GeneratorDiagnosticsTests.RunAndGetSources(
            """
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
            [TightPackable]
            public partial struct ReadonlyFields
            {
                [TightPackInclude] private readonly int _a;
                [TightPackInclude] private readonly short _b;

                public ReadonlyFields(int a, short b) => (_a, _b) = (a, b);
                public int Sum => _a + _b;
            }
            """
        );

        string generated = sources["ReadonlyFields.TightPack.g.cs"];

        Assert.IsTrue(
            generated.Contains("WriteBlittable"),
            "Readonly included fields are not blitted, so the type loses its layout optimization:\n" + generated
        );
    }

    /// <summary>
    /// A private field is invisible to the payload unless it is included, and a blit writes the whole
    /// image, so an excluded field would be written uninitialised. It must stay on the member path.
    /// </summary>
    [TestMethod]
    public void ExcludedPrivateFields_AreNotBlitted()
    {
        ImmutableDictionary<string, string> sources = GeneratorDiagnosticsTests.RunAndGetSources(
            """
            [TightPackable]
            public partial struct PrivateFields
            {
                public int Visible;
                private int _hidden;
            }
            """
        );

        string generated = sources["PrivateFields.TightPack.g.cs"];

        Assert.IsFalse(
            generated.Contains("WriteBlittable"),
            "A struct with an excluded field was blitted, which puts it on the wire:\n" + generated
        );
    }

    /// <summary>
    /// The BCL declares inline arrays up to 16, and every one the format accepts must name a
    /// formatter the provider can build without reflection.
    /// </summary>
    [TestMethod]
    public void InlineArrayMembers_AboveFourAreSupported()
    {
        ImmutableDictionary<string, string> sources = GeneratorDiagnosticsTests.RunAndGetSources(
            """
            [TightPackable]
            public partial struct WideArrays
            {
                public System.Runtime.CompilerServices.InlineArray8<ushort> Eight;
                public System.Runtime.CompilerServices.InlineArray16<byte> Sixteen;
            }
            """
        );

        string generated = sources["WideArrays.TightPack.g.cs"];

        Assert.IsTrue(
            generated.Contains("InlineArray8Formatter"),
            "InlineArray8 is not resolved to a formatter:\n" + generated
        );
        Assert.IsTrue(
            generated.Contains("InlineArray16Formatter"),
            "InlineArray16 is not resolved to a formatter:\n" + generated
        );
    }
}
