using NAudio.Wave;

namespace OpenAudio.Audio;

public sealed class LimiterSampleProvider : ISampleProvider
{
    private const float Epsilon = 0.0001f;

    private readonly ISampleProvider _source;
    private readonly float _threshold;
    private readonly float _ceiling;
    private readonly float _makeupGain;

    public LimiterSampleProvider(ISampleProvider source, float threshold = 0.82f, float ceiling = 0.98f, float makeupGain = 1f)
    {
        _source = source;
        _threshold = Math.Clamp(threshold, 0.1f, 0.99f);
        _ceiling = Math.Clamp(ceiling, 0.1f, 1f);
        _makeupGain = Math.Max(0.1f, makeupGain);

        if (_threshold >= _ceiling)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Limiter threshold must be below the ceiling.");
        }
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);

        for (var index = 0; index < samplesRead; index++)
        {
            var sampleIndex = offset + index;
            var sample = buffer[sampleIndex] * _makeupGain;
            var magnitude = MathF.Abs(sample);

            if (magnitude > _threshold)
            {
                var limitingRange = Math.Max(Epsilon, _ceiling - _threshold);
                var overshoot = magnitude - _threshold;
                var compressedMagnitude = _threshold + (overshoot / (1f + (overshoot / limitingRange)));
                sample = MathF.CopySign(MathF.Min(compressedMagnitude, _ceiling), sample);
            }

            buffer[sampleIndex] = Math.Clamp(sample, -_ceiling, _ceiling);
        }

        return samplesRead;
    }
}

