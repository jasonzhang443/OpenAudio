using OpenAudio.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OpenAudio.Audio;

public sealed class AudioMixerService : IDisposable
{
    private const float BoostZoneStart = 0.55f;
    private const float MaxBoostGain = 5f;

    private readonly SessionLogger _logger;
    private VolumeSampleProvider? _musicVolumeProvider;
    private VolumeSampleProvider? _microphoneVolumeProvider;
    private int _musicVolumePercent;
    private int _microphoneVolumePercent;
    private bool _isMicrophoneMuted;

    public AudioMixerService(MMDevice outputDevice, SessionLogger logger)
    {
        _logger = logger;
        OutputFormat = CreateTargetFormat(outputDevice);
    }

    public WaveFormat OutputFormat { get; }

    public ISampleProvider BuildMixer(IAudioSource musicSource, IAudioSource? microphoneSource, int musicVolumePercent, int microphoneVolumePercent)
    {
        _musicVolumePercent = musicVolumePercent;
        _microphoneVolumePercent = microphoneVolumePercent;

        var mixer = new MixingSampleProvider(OutputFormat)
        {
            ReadFully = true
        };

        _musicVolumeProvider = new VolumeSampleProvider(Normalize(musicSource.SampleProvider, OutputFormat))
        {
            Volume = SliderToGain(_musicVolumePercent)
        };

        mixer.AddMixerInput(_musicVolumeProvider);

        if (microphoneSource is not null)
        {
            _microphoneVolumeProvider = new VolumeSampleProvider(Normalize(microphoneSource.SampleProvider, OutputFormat))
            {
                Volume = CalculateMicrophoneGain()
            };

            mixer.AddMixerInput(_microphoneVolumeProvider);
        }
        else
        {
            _microphoneVolumeProvider = null;
        }

        _logger.Log($"Audio mixer configured at {OutputFormat.SampleRate} Hz / {OutputFormat.Channels} channel(s).");
        return new LimiterSampleProvider(mixer, threshold: 0.42f, ceiling: 0.995f, makeupGain: 2.8f);
    }

    public void SetMusicVolume(int volumePercent)
    {
        _musicVolumePercent = volumePercent;
        if (_musicVolumeProvider is not null)
        {
            _musicVolumeProvider.Volume = SliderToGain(_musicVolumePercent);
        }
    }

    public void SetMicrophoneVolume(int volumePercent)
    {
        _microphoneVolumePercent = volumePercent;
        if (_microphoneVolumeProvider is not null)
        {
            _microphoneVolumeProvider.Volume = CalculateMicrophoneGain();
        }
    }

    public void SetMicrophoneMuted(bool isMuted)
    {
        _isMicrophoneMuted = isMuted;
        if (_microphoneVolumeProvider is not null)
        {
            _microphoneVolumeProvider.Volume = CalculateMicrophoneGain();
        }
    }

    public void Dispose()
    {
        _musicVolumeProvider = null;
        _microphoneVolumeProvider = null;
    }

    private float CalculateMicrophoneGain() => _isMicrophoneMuted ? 0f : SliderToGain(_microphoneVolumePercent);

    private static ISampleProvider Normalize(ISampleProvider source, WaveFormat targetFormat)
    {
        ISampleProvider current = source;

        if (current.WaveFormat.Channels != targetFormat.Channels)
        {
            current = new ChannelAligningSampleProvider(current, targetFormat.Channels);
        }

        if (current.WaveFormat.SampleRate != targetFormat.SampleRate)
        {
            current = new WdlResamplingSampleProvider(current, targetFormat.SampleRate);
        }

        if (current.WaveFormat.Channels != targetFormat.Channels)
        {
            current = new ChannelAligningSampleProvider(current, targetFormat.Channels);
        }

        return current;
    }

    private static WaveFormat CreateTargetFormat(MMDevice outputDevice)
    {
        var mixFormat = outputDevice.AudioClient.MixFormat;
        var sampleRate = mixFormat.SampleRate > 0 ? mixFormat.SampleRate : 48000;
        var channels = mixFormat.Channels <= 1 ? 1 : 2;
        return WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    private static float SliderToGain(int volumePercent)
    {
        var normalized = Math.Clamp(volumePercent / 100f, 0f, 1f);
        if (normalized <= BoostZoneStart)
        {
            return normalized / BoostZoneStart;
        }

        var boostProgress = (normalized - BoostZoneStart) / (1f - BoostZoneStart);
        return 1f + (boostProgress * (MaxBoostGain - 1f));
    }
}

