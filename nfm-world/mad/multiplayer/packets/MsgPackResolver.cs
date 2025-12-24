using MessagePack;
using Microsoft.Xna.Framework;
using NFMWorld.Mad;
using SoftFloat;

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

namespace NFMWorld.Mad;

[GeneratedMessagePackResolver]
internal partial class MsgPackResolver;