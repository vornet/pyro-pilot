using NAudio.Wave;

namespace PyroPilot.App.Services;

/// <summary>
/// Thin wrapper over NAudio for the show's audio track: load a file, play
/// from/seek to a position, and compute peaks for a waveform display. NAudio's
/// output backend is Windows-only (winmm.dll); this is the seam to swap in a
/// cross-platform backend (e.g. a miniaudio binding) when macOS/Linux support
/// is added.
/// </summary>
public sealed class AudioPlaybackService : IDisposable
{
    private AudioFileReader? _reader;
    private WaveOutEvent? _output;

    public bool IsLoaded => _reader is not null;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public void Load(string filePath)
    {
        Stop();
        _reader?.Dispose();
        _output?.Dispose();

        _reader = new AudioFileReader(filePath);
        _output = new WaveOutEvent();
        _output.Init(_reader);
    }

    public void Unload()
    {
        Stop();
        _reader?.Dispose();
        _output?.Dispose();
        _reader = null;
        _output = null;
    }

    public void PlayFrom(TimeSpan position)
    {
        if (_reader is null || _output is null) return;
        _reader.CurrentTime = Clamp(position);
        _output.Play();
    }

    public void Pause() => _output?.Pause();

    public void Stop()
    {
        _output?.Stop();
        if (_reader is not null) _reader.CurrentTime = TimeSpan.Zero;
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is null) return;
        _reader.CurrentTime = Clamp(position);
    }

    public void SetVolume(float volume)
    {
        if (_reader is not null) _reader.Volume = Math.Clamp(volume, 0f, 1f);
    }

    private TimeSpan Clamp(TimeSpan position)
    {
        if (_reader is null) return TimeSpan.Zero;
        if (position < TimeSpan.Zero) return TimeSpan.Zero;
        return position > _reader.TotalTime ? _reader.TotalTime : position;
    }

    /// <summary>Downsampled absolute-peak samples for a simple waveform display, one value per bucket in 0..1.</summary>
    public static float[] ComputeWaveformPeaks(string filePath, int bucketCount)
    {
        using var reader = new AudioFileReader(filePath);
        long totalFloats = reader.Length / sizeof(float);
        int samplesPerBucket = (int)Math.Max(1, totalFloats / Math.Max(1, bucketCount));

        var peaks = new float[bucketCount];
        var buffer = new float[samplesPerBucket];

        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            int read = reader.Read(buffer, 0, buffer.Length);
            if (read == 0) break;

            float max = 0f;
            for (int i = 0; i < read; i++) max = Math.Max(max, Math.Abs(buffer[i]));
            peaks[bucket] = max;
        }

        return peaks;
    }

    public void Dispose()
    {
        _output?.Dispose();
        _reader?.Dispose();
    }
}
