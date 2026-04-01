using OpenAudio.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OpenAudio.Audio;

public sealed class LoopbackCaptureService : WasapiCaptureSourceBase
{
    public LoopbackCaptureService(MMDevice device, SessionLogger logger)
        : base(device, logger, "Loopback capture")
    {
    }

    protected override WasapiCapture CreateCapture(MMDevice device) => new WasapiLoopbackCapture(device);
}


