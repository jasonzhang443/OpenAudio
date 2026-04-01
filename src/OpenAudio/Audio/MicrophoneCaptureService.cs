using OpenAudio.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OpenAudio.Audio;

public sealed class MicrophoneCaptureService : WasapiCaptureSourceBase
{
    public MicrophoneCaptureService(MMDevice device, SessionLogger logger)
        : base(device, logger, "Microphone capture")
    {
    }

    protected override WasapiCapture CreateCapture(MMDevice device) => new WasapiCapture(device);
}


