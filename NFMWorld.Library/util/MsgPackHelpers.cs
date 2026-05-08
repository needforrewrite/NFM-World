using System.Runtime.CompilerServices;
using Maxine.Extensions.MessagePack;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.Xna.Framework;
using NFMWorldLibrary;
using NFMWorldLibrary.Files.Demo;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

[assembly: MessagePackAssumedFormattable(typeof(PlayerState))]
[assembly: MessagePackAssumedFormattable(typeof(Vector2))]
[assembly: MessagePackAssumedFormattable(typeof(Vector3))]
[assembly: MessagePackAssumedFormattable(typeof(Vector4))]
[assembly: MessagePackAssumedFormattable(typeof(Quaternion))]
[assembly: MessagePackAssumedFormattable(typeof(Matrix))]
[assembly: MessagePackAssumedFormattable(typeof(Color))]
[assembly: MessagePackAssumedFormattable(typeof(Color3))]
[assembly: MessagePackAssumedFormattable(typeof(AngleSingle))]
[assembly: MessagePackAssumedFormattable(typeof(fix64))]
[assembly: MessagePackAssumedFormattable(typeof(f64Vector3))]
[assembly: MessagePackAssumedFormattable(typeof(DemoEntry))]
[assembly: MessagePackAssumedFormattable(typeof(List<DemoEntry>))]
[assembly: MessagePackAssumedFormattable(typeof(f64AngleSingle))]
[assembly: MessagePackAssumedFormattable(typeof(f64Euler))]
[assembly: MessagePackAssumedFormattable(typeof(InlineArray4<fix64>))]

[assembly: MessagePackKnownFormatter(typeof(UnlimitedArrayFormatter<object>))]
[assembly: MessagePackKnownFormatter(typeof(InlineArray4Formatter<int>))]
[assembly: MessagePackKnownFormatter(typeof(InlineArray5Formatter<int>))]
[assembly: MessagePackKnownFormatter(typeof(UnlimitedArrayFormatter<PiecePlacement>))]
[assembly: MessagePackKnownFormatter(typeof(UnlimitedArrayFormatter<Rad3dBoxDef>))]

namespace NFMWorldLibrary.Util;

public static class MsgPackHelpers
{
    private static readonly IFormatterResolver CompositeResolver = MessagePack.Resolvers.CompositeResolver.Create([
        new UnsafeUnmanagedStructFormatter<PlayerState>(100),
        new UnsafeUnmanagedStructFormatter<Vector2>(101),
        new UnsafeUnmanagedStructFormatter<Vector3>(102),
        new UnsafeUnmanagedStructFormatter<Vector4>(103),
        new UnsafeUnmanagedStructFormatter<Quaternion>(104),
        new UnsafeUnmanagedStructFormatter<Matrix>(105),
        new UnsafeUnmanagedStructFormatter<Color>(106),
        new UnsafeUnmanagedStructFormatter<Color3>(107),
        new UnsafeUnmanagedStructFormatter<AngleSingle>(108),
        new UnsafeUnmanagedStructFormatter<fix64>(109),
        new UnsafeUnmanagedStructFormatter<f64Vector3>(110),
        new UnsafeUnmanagedStructFormatter<DemoEntry>(111),
        new UnsafeUnmanagedStructListFormatter<DemoEntry>(112),
        new UnsafeUnmanagedStructFormatter<f64AngleSingle>(113),
        new UnsafeUnmanagedStructFormatter<f64Euler>(114),
        new UnsafeUnmanagedStructFormatter<InlineArray4<fix64>>(115), // keep for time trial format compatibility
        new UnsafeUnmanagedStructFormatter<Int3>(116),
        InlineArray4Formatter<int>.Instance,
        InlineArray5Formatter<int>.Instance,
        UnlimitedArrayFormatter<PiecePlacement>.Instance,
        UnlimitedArrayFormatter<Rad3dBoxDef>.Instance,
    ], [
        StandardResolver.Instance,
        InlineArrayResolver.Instance,
        UnlimitedArrayResolver.Instance,
        MsgPackResolver.Instance,
    ]);

    public static MessagePackSerializerOptions Options => MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData)
        .WithResolver(DedupingResolver.Create(CompositeResolver));
}