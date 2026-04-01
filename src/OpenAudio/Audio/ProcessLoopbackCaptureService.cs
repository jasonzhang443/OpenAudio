using System.Runtime.InteropServices;
using OpenAudio.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OpenAudio.Audio;

public sealed class ProcessLoopbackCaptureService : IAudioSource
{
    private readonly uint _processId;
    private readonly string _sourceName;
    private readonly SessionLogger _logger;
    private readonly WaveFormat _captureFormat = new(48000, 16, 2);
    private readonly BufferedWaveProvider _bufferedProvider;
    private readonly ISampleProvider _sampleProvider;

    private AutoResetEvent? _sampleReadyEvent;
    private CancellationTokenSource? _captureCancellationTokenSource;
    private Task? _captureTask;
    private byte[] _captureBuffer = Array.Empty<byte>();
    private bool _disposed;

    public ProcessLoopbackCaptureService(uint processId, string sourceName, SessionLogger logger)
    {
        _processId = processId;
        _sourceName = sourceName;
        _logger = logger;

        _bufferedProvider = new BufferedWaveProvider(_captureFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(750),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        _sampleProvider = _bufferedProvider.ToSampleProvider();
    }

    public string DeviceId => _processId.ToString();

    public string SourceName => _sourceName;

    public WaveFormat WaveFormat => _sampleProvider.WaveFormat;

    public ISampleProvider SampleProvider => _sampleProvider;

    public event EventHandler<string>? Faulted;

    public void Start()
    {
        ThrowIfDisposed();

        if (_captureTask is not null)
        {
            return;
        }

        _sampleReadyEvent = new AutoResetEvent(false);
        _captureCancellationTokenSource = new CancellationTokenSource();
        var startupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureTask = Task.Factory.StartNew(
            () => RunCaptureLoop(startupCompletion, _captureCancellationTokenSource.Token),
            _captureCancellationTokenSource.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        startupCompletion.Task.GetAwaiter().GetResult();
    }

    public void Stop()
    {
        if (_captureTask is null)
        {
            return;
        }

        try
        {
            _captureCancellationTokenSource?.Cancel();
            _sampleReadyEvent?.Set();
            _captureTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception exception)
        {
            _logger.Log("Process loopback capture task stop failed.", exception);
        }

        _captureTask = null;
        _captureCancellationTokenSource?.Dispose();
        _captureCancellationTokenSource = null;
        _sampleReadyEvent?.Dispose();
        _sampleReadyEvent = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private void RunCaptureLoop(TaskCompletionSource startupCompletion, CancellationToken cancellationToken)
    {
        AudioClient? audioClient = null;
        AudioCaptureClient? audioCaptureClient = null;

        try
        {
            audioClient = ProcessLoopbackAudioClientFactory.Create(_processId, _logger);
            audioClient.Initialize(
                AudioClientShareMode.Shared,
                AudioClientStreamFlags.Loopback | AudioClientStreamFlags.EventCallback | AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality,
                0,
                0,
                _captureFormat,
                Guid.Empty);

            audioCaptureClient = audioClient.AudioCaptureClient;
            if (_sampleReadyEvent is null)
            {
                throw new InvalidOperationException("Sample-ready event was not created.");
            }

            audioClient.SetEventHandle(_sampleReadyEvent.SafeWaitHandle.DangerousGetHandle());
            audioClient.Start();
            _logger.Log($"Process loopback capture started for {_sourceName} (PID {_processId}).");
            startupCompletion.TrySetResult();

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_sampleReadyEvent is null || !_sampleReadyEvent.WaitOne(TimeSpan.FromMilliseconds(250)))
                {
                    continue;
                }

                DrainAvailablePackets(audioCaptureClient);
            }
        }
        catch (Exception exception)
        {
            startupCompletion.TrySetException(exception);

            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.Log("Process loopback capture faulted.", exception);
                Faulted?.Invoke(this, $"Process capture stopped unexpectedly: {exception.Message}");
            }
        }
        finally
        {
            try
            {
                audioClient?.Stop();
            }
            catch (Exception exception)
            {
                _logger.Log("Process loopback audio client stop failed.", exception);
            }

            audioCaptureClient?.Dispose();
            audioClient?.Dispose();
            _logger.Log($"Process loopback capture stopped for {_sourceName} (PID {_processId}).");
        }
    }

    private void DrainAvailablePackets(AudioCaptureClient audioCaptureClient)
    {
        while (true)
        {
            var framesAvailable = audioCaptureClient.GetNextPacketSize();
            if (framesAvailable <= 0)
            {
                break;
            }

            AudioClientBufferFlags flags;
            long devicePosition;
            long qpcPosition;

            var dataPointer = audioCaptureClient.GetBuffer(out framesAvailable, out flags, out devicePosition, out qpcPosition);
            try
            {
                var bytesToCapture = framesAvailable * _captureFormat.BlockAlign;
                EnsureCaptureBuffer(bytesToCapture);

                if ((flags & AudioClientBufferFlags.Silent) != 0 || dataPointer == IntPtr.Zero)
                {
                    Array.Clear(_captureBuffer, 0, bytesToCapture);
                }
                else
                {
                    Marshal.Copy(dataPointer, _captureBuffer, 0, bytesToCapture);
                }

                _bufferedProvider.AddSamples(_captureBuffer, 0, bytesToCapture);
            }
            finally
            {
                audioCaptureClient.ReleaseBuffer(framesAvailable);
            }
        }
    }

    private void EnsureCaptureBuffer(int requiredBytes)
    {
        if (_captureBuffer.Length >= requiredBytes)
        {
            return;
        }

        _captureBuffer = new byte[requiredBytes];
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

