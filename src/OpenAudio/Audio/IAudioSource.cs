using NAudio.Wave;

namespace OpenAudio.Audio;

public interface IAudioSource : IDisposable
{
    string DeviceId { get; }

    string SourceName { get; }

    WaveFormat WaveFormat { get; }

    ISampleProvider SampleProvider { get; }

    event EventHandler<string>? Faulted;

    void Start();

    void Stop();
}


