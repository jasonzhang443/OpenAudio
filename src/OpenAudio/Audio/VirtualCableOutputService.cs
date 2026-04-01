using OpenAudio.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OpenAudio.Audio;

public sealed class VirtualCableOutputService : IDisposable
{
    private readonly MMDevice _outputDevice;
    private readonly SessionLogger _logger;
    private WasapiOut? _output;
    private bool _isStopping;

    public VirtualCableOutputService(MMDevice outputDevice, SessionLogger logger)
    {
        _outputDevice = outputDevice;
        _logger = logger;
    }

    public event EventHandler<string>? Faulted;

    public void Start(ISampleProvider sampleProvider)
    {
        Stop();

        _isStopping = false;
        _output = new WasapiOut(_outputDevice, AudioClientShareMode.Shared, true, 80);
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(new SampleToWaveProvider(sampleProvider));
        _output.Play();

        _logger.Log($"Virtual cable output started on {_outputDevice.FriendlyName}.");
    }

    public void Stop()
    {
        if (_output is null)
        {
            return;
        }

        _isStopping = true;

        try
        {
            _output.Stop();
        }
        catch (Exception exception)
        {
            _logger.Log("Virtual cable output stop failed.", exception);
        }
        finally
        {
            DisposeOutput();
            _logger.Log($"Virtual cable output stopped on {_outputDevice.FriendlyName}.");
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_isStopping || e.Exception is null)
        {
            return;
        }

        Faulted?.Invoke(this, $"VB-Cable output stopped unexpectedly: {e.Exception.Message}");
    }

    private void DisposeOutput()
    {
        if (_output is null)
        {
            return;
        }

        _output.PlaybackStopped -= OnPlaybackStopped;
        _output.Dispose();
        _output = null;
    }
}

