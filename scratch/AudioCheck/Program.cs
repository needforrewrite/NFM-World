using System.IO.Compression;
using NFMWorld.Audio;

// Usage: AudioCheck <path-to.zipo-or-module> [tempoMul] [freqMul]
// Defaults are the NFM1 introductory stage's own directives: soundtracktempomul(0.8),
// soundtrackfreqmul(0.95). The remastered MP3 next to it is the same tune at natural tempo, so
// its measured beat rate is the reference the module render has to match.
var path = args.Length > 0 ? args[0] : "nfm-world/bin/Debug/net11.0/data/music/nfm1/stage1.zipo";
var tempoMul = args.Length > 1 ? double.Parse(args[1]) : 0.8;
var freqMul = args.Length > 2 ? double.Parse(args[2]) : 0.95;
var remasteredPath = Path.Combine(Path.GetDirectoryName(path)!, "stage1remastered.mp3");

using var zip = ZipFile.OpenRead(path);
var entry = zip.Entries[0];

using var entryStream = entry.Open();
var decoded = TrackerDecoder.Decode(entryStream);
var bytesPerSecond = decoded.SampleRate * 2 * (int)decoded.Channels;

Console.WriteLine($"module {entry.Name}: {decoded.SampleRate} Hz {decoded.Channels}, {decoded.PcmData.Count / (double)bytesPerSecond:F2} s");
Console.WriteLine($"  beat rate (natural render): {BeatRate(decoded.PcmData, decoded.SampleRate, (int)decoded.Channels):F1} BPM");

foreach (var ratio in new[] { tempoMul })
{
    using var stretched = TempoStretcher.Process(decoded.PcmData, decoded.SampleRate, (int)decoded.Channels, ratio);
    var stretchedSeconds = stretched.Count / (double)bytesPerSecond;
    // freqMul is a voice rate, not part of the PCM, but it scales the heard tempo directly.
    Console.WriteLine($"as the game plays it (stretch {ratio} x freq {freqMul}): {stretchedSeconds / freqMul:F2} s, " +
                      $"beat rate {BeatRate(stretched, decoded.SampleRate, (int)decoded.Channels) * freqMul:F1} BPM");
}

decoded.Dispose();

if (File.Exists(remasteredPath))
{
    using var mp3 = File.OpenRead(remasteredPath);
    var reference = AudioDecoder.Decode(mp3, ".mp3");
    var referenceBytesPerSecond = reference.SampleRate * 2 * (int)reference.Channels;
    Console.WriteLine($"remastered {Path.GetFileName(remasteredPath)}: {reference.SampleRate} Hz {reference.Channels}, " +
                      $"{reference.PcmData.Count / (double)referenceBytesPerSecond:F2} s");
    Console.WriteLine($"  beat rate: {BeatRate(reference.PcmData, reference.SampleRate, (int)reference.Channels):F1} BPM");
    reference.Dispose();
}
else
{
    Console.WriteLine($"(no reference at {remasteredPath})");
}

// Onset-strength autocorrelation: frame RMS envelope at 200 Hz, half-wave-rectified difference,
// then the lag in the 60..200 BPM range with the highest correlation. Reports the strongest peak
// and whether a half/double-tempo neighbour is nearly as strong (the usual ambiguity).
static double BeatRate(ReadOnlySpan<byte> pcm16, int sampleRate, int channels)
{
    const int envRate = 200;
    var frameSamples = sampleRate / envRate;
    var frameCount = pcm16.Length / 2 / channels / frameSamples;
    if (frameCount < envRate * 4) return 0;

    var onset = new double[frameCount];
    double previous = 0;
    for (var f = 0; f < frameCount; f++)
    {
        double sum = 0;
        var baseIndex = f * frameSamples;
        for (var i = 0; i < frameSamples; i++)
        {
            var byteIndex = (baseIndex + i) * channels * 2;
            var s = BitConverter.ToInt16(pcm16.Slice(byteIndex, 2)) / 32768.0;
            sum += s * s;
        }
        var rms = Math.Sqrt(sum / frameSamples);
        onset[f] = Math.Max(0, rms - previous);
        previous = rms;
    }

    var mean = 0.0;
    foreach (var v in onset) mean += v;
    mean /= onset.Length;
    for (var i = 0; i < onset.Length; i++) onset[i] -= mean;

    var minLag = envRate * 60 / 200; // 200 BPM
    var maxLag = envRate * 60 / 60;  // 60 BPM
    var best = (lag: 0, score: double.MinValue);
    for (var lag = minLag; lag <= maxLag; lag++)
    {
        double score = 0;
        for (var i = lag; i < onset.Length; i++) score += onset[i] * onset[i - lag];
        score /= onset.Length - lag;
        if (score > best.score) best = (lag, score);
    }

    return best.lag == 0 ? 0 : 60.0 * envRate / best.lag;
}
