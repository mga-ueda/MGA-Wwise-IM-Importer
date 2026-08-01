using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter.Wave;

/// <summary>
/// メトロノームクリック音（High / Low）の読み込み・音量。
/// 再生中の拍クリックは <see cref="WaveAudioPlayer"/> のオンメモリ加算ミックス側で行う。
/// </summary>
internal sealed class MetronomePlayer : IDisposable
{
    private readonly float[] _highSamples;
    private readonly float[] _lowSamples;
    private readonly int _sampleRate;
    private float _volume = DefaultVolume;
    private bool _disposed;

    /// <summary>クリック音量。既定 0.3（30%）。下限 10%、上限 100%。アプリ設定に保存する。</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, MinVolume, MaxVolume);
    }

    public const float MinVolume = 0.1f;
    public const float MaxVolume = 1f;
    public const float DefaultVolume = 0.3f;
    public const float VolumeStep = 0.1f;

    public IReadOnlyList<float> HighSamples => _highSamples;
    public IReadOnlyList<float> LowSamples => _lowSamples;
    public int SampleRate => _sampleRate;

    private MetronomePlayer(float[] highSamples, float[] lowSamples, int sampleRate)
    {
        _highSamples = highSamples;
        _lowSamples = lowSamples;
        _sampleRate = sampleRate;
        _volume = DefaultVolume;
    }

    public static MetronomePlayer? TryCreate()
    {
        if (!TryLoadClick(AppEmbeddedResources.OpenMetronomeHigh(), out var high, out var highRate)
            || !TryLoadClick(AppEmbeddedResources.OpenMetronomeLow(), out var low, out var lowRate))
        {
            return null;
        }

        if (highRate != lowRate || highRate <= 0)
        {
            return null;
        }

        return new MetronomePlayer(high, low, highRate);
    }

    /// <summary>ホイール等で音量を一段変え、変更があれば true。</summary>
    public bool TryAdjustVolume(int wheelDelta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (wheelDelta == 0)
        {
            return false;
        }

        var next = Math.Clamp(
            Volume + (wheelDelta > 0 ? VolumeStep : -VolumeStep),
            MinVolume,
            MaxVolume);
        if (Math.Abs(next - Volume) < 1e-6f)
        {
            return false;
        }

        Volume = next;
        return true;
    }

    /// <summary>クリック音を再生サンプルレートへ線形リサンプルする。</summary>
    public static float[] ResampleMono(IReadOnlyList<float> source, int sourceRate, int targetRate)
    {
        if (source.Count == 0 || sourceRate <= 0 || targetRate <= 0)
        {
            return [];
        }

        if (sourceRate == targetRate)
        {
            if (source is float[] exact)
            {
                return exact;
            }

            var copy = new float[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        var length = Math.Max(1, (int)Math.Round(source.Count * (double)targetRate / sourceRate));
        var dest = new float[length];
        if (length == 1)
        {
            dest[0] = source[0];
            return dest;
        }

        var last = source.Count - 1;
        for (var i = 0; i < length; i++)
        {
            var srcPos = i * (double)last / (length - 1);
            var i0 = (int)srcPos;
            var i1 = Math.Min(last, i0 + 1);
            var t = (float)(srcPos - i0);
            dest[i] = source[i0] * (1f - t) + source[i1] * t;
        }

        return dest;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private static bool TryLoadClick(Stream? stream, out float[] samples, out int sampleRate)
    {
        samples = [];
        sampleRate = 0;
        if (stream is null)
        {
            return false;
        }

        try
        {
            using (stream)
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                memory.Position = 0;
                using var reader = new WaveFileReader(memory);
                sampleRate = reader.WaveFormat.SampleRate;
                ISampleProvider sampleProvider = reader.ToSampleProvider();
                if (sampleProvider.WaveFormat.Channels > 1)
                {
                    sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
                }

                var list = new List<float>(Math.Max(256, (int)(reader.Length / 2)));
                var block = new float[1024];
                int read;
                while ((read = sampleProvider.Read(block, 0, block.Length)) > 0)
                {
                    for (var i = 0; i < read; i++)
                    {
                        list.Add(block[i]);
                    }
                }

                if (list.Count == 0 || sampleRate <= 0)
                {
                    return false;
                }

                samples = list.ToArray();
                return true;
            }
        }
        catch (Exception)
        {
            samples = [];
            sampleRate = 0;
            return false;
        }
    }
}
