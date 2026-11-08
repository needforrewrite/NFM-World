using System.Buffers.Binary;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Audio;

public static class SpatialAudio
{
    public static Vector3 ListenerPosition { get; private set; }
    public static Vector3 ListenerForward { get; private set; } = Vector3.UnitZ;
    public static Vector3 ListenerUp { get; private set; } = -Vector3.UnitY;

    public static void UpdateListener(Vector3 position, Vector3 forward, Vector3 up)
    {
        var forwardLength = forward.LengthSquared();
        if (!Finite(position)
            || !Finite(forward)
            || !Finite(up)
            || !float.IsFinite(forwardLength)
            || forwardLength < 0.0001f) return;
        forward = Vector3.Normalize(forward);
        up -= forward * Vector3.Dot(up, forward);
        var upLength = up.LengthSquared();
        if (!float.IsFinite(upLength) || upLength < 0.0001f) return;
        ListenerPosition = position;
        ListenerForward = forward;
        ListenerUp = Vector3.Normalize(up);
        if (OperatingSystem.IsBrowser())
        {
            WebAudioBackend.SetListener(
                position.X,
                position.Y,
                position.Z,
                forward.X,
                forward.Y,
                forward.Z,
                ListenerUp.X,
                ListenerUp.Y,
                ListenerUp.Z
            );
            WebAudioSoundClip.UpdateSpatialSounds();
        }
        else
            FaudioSoundClip.UpdateSpatialSounds();
    }

    public static float Gain(Vector3 position) => SoundSpatialization.DistanceGain(Vector3.Distance(ListenerPosition, position));

    public static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    internal static byte[] ToMono(ReadOnlySpan<byte> pcm, int channels)
    {
        if (channels <= 0 || pcm.Length % (channels * sizeof(short)) != 0)
            throw new ArgumentException("Invalid interleaved PCM data.");
        var mono = new byte[pcm.Length / channels];
        for (var frame = 0; frame < mono.Length / sizeof(short); frame++)
        {
            var sum = 0L;
            for (var channel = 0; channel < channels; channel++)
                sum += BinaryPrimitives.ReadInt16LittleEndian(
                    pcm.Slice((frame * channels + channel) * sizeof(short), sizeof(short))
                );
            BinaryPrimitives.WriteInt16LittleEndian(mono.AsSpan(frame * sizeof(short), sizeof(short)), (short)(sum / channels));
        }

        return mono;
    }
}
