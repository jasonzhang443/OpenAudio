using OpenAudio.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OpenAudio.Audio;

public abstract class WasapiCaptureSourceBase : IAudioSource
{
    private readonly MMDevice _device;
    private readonly SessionLogger _logger;
    private readonly string _sourceType;
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _bufferedProvider;
    private ISampleProvider? _sampleProvider;
    private bool _isStopping;
    private bool _disposed;

    protected WasapiCaptureSourceBase(MMDevice device, SessionLogger logger, string sourceType)
    {
        _device = device;
        _logger = logger;
        _sourceType = sourceType;
        InitializeCapture();
    }

    public string DeviceId => _device.ID;

    public string SourceName => _device.FriendlyName;

    public WaveFormat WaveFormat => _sampleProvider?.WaveFormat
        ?? throw new InvalidOperationException("Capture format is not available.");

    public ISampleProvider SampleProvider => _sampleProvider
        ?? throw new InvalidOperationException("Capture source has not been initialized.");

    public event EventHandler<string>? Faulted;

    public void Start()
    {
        ThrowIfDisposed();

        if (_capture is null)
        {
            InitializeCapture();
        }

        _isStopping = false;
        _capture!.StartRecording();
        _logger.Log($"{_sourceType} started on {_device.FriendlyName}.");
    }

    public void Stop()
    {
        if (_capture is null)
        {
            return;
        }

        _isStopping = true;

        try
        {
            _capture.StopRecording();
        }
        catch (Exception exception)
        {
            _logger.Log($"{_sourceType} stop failed.", exception);
        }

        DisposeCapture();
        _logger.Log($"{_sourceType} stopped on {_device.FriendlyName}.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        DisposeCapture();
        _disposed = true;
    }

    protected abstract WasapiCapture CreateCapture(MMDevice device);

    private void InitializeCapture()
    {
        _capture = CreateCapture(_device);
        _bufferedProvider = new BufferedWaveProvider(_capture.WaveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(750),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        _sampleProvider = _bufferedProvider.ToSampleProvider();
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
    }

    private void DisposeCapture()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _capture = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _bufferedProvider?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_isStopping || e.Exception is null)
        {
            return;
        }

        Faulted?.Invoke(this, $"{_sourceType} stopped unexpectedly: {e.Exception.Message}");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

