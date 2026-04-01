using NAudio.Wave;

namespace OpenAudio.Audio;

public sealed class ChannelAligningSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly WaveFormat _waveFormat;
    private float[] _sourceBuffer = Array.Empty<float>();

    public ChannelAligningSampleProvider(ISampleProvider source, int targetChannels)
    {
        if (targetChannels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetChannels));
        }

        _source = source;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, targetChannels);
    }

    public WaveFormat WaveFormat => _waveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var inputChannels = _source.WaveFormat.Channels;
        var outputChannels = _waveFormat.Channels;
        var framesRequested = count / outputChannels;

        if (framesRequested == 0)
        {
            return 0;
        }

        EnsureSourceBuffer(framesRequested * inputChannels);

        var samplesRead = _source.Read(_sourceBuffer, 0, framesRequested * inputChannels);
        var framesRead = samplesRead / inputChannels;

        for (var frame = 0; frame < framesRead; frame++)
        {
            var sourceFrameOffset = frame * inputChannels;
            var targetFrameOffset = offset + (frame * outputChannels);

            if (inputChannels == outputChannels)
            {
                Array.Copy(_sourceBuffer, sourceFrameOffset, buffer, targetFrameOffset, outputChannels);
                continue;
            }

            if (outputChannels == 1)
            {
                var sum = 0f;
                for (var channel = 0; channel < inputChannels; channel++)
                {
                    sum += _sourceBuffer[sourceFrameOffset + channel];
                }

                buffer[targetFrameOffset] = sum / inputChannels;
                continue;
            }

            if (inputChannels == 1)
            {
                var monoSample = _sourceBuffer[sourceFrameOffset];
                for (var channel = 0; channel < outputChannels; channel++)
                {
                    buffer[targetFrameOffset + channel] = monoSample;
                }

                continue;
            }

            if (outputChannels == 2)
            {
                buffer[targetFrameOffset] = _sourceBuffer[sourceFrameOffset];
                buffer[targetFrameOffset + 1] = _sourceBuffer[sourceFrameOffset + 1];
                continue;
            }

            for (var channel = 0; channel < outputChannels; channel++)
            {
                var sourceChannel = Math.Min(channel, inputChannels - 1);
                buffer[targetFrameOffset + channel] = _sourceBuffer[sourceFrameOffset + sourceChannel];
            }
        }

        return framesRead * outputChannels;
    }

    private void EnsureSourceBuffer(int requiredSamples)
    {
        if (_sourceBuffer.Length >= requiredSamples)
        {
            return;
        }

        _sourceBuffer = new float[requiredSamples];
    }
}


