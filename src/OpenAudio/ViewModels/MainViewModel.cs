using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using OpenAudio.Audio;
using OpenAudio.Models;
using OpenAudio.Services;
using OpenAudio.Utils;

namespace OpenAudio.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const string VbCableDownloadUrl = "https://vb-audio.com/Cable/";

    private readonly VbCableDetector _vbCableDetector;
    private readonly AudioDeviceService _audioDeviceService;
    private readonly ApplicationSourceService _applicationSourceService;
    private readonly SessionLogger _logger;
    private readonly DispatcherTimer _applicationRefreshTimer;

    private IAudioSource? _musicSource;
    private MicrophoneCaptureService? _microphoneCapture;
    private AudioMixerService? _audioMixer;
    private VirtualCableOutputService? _virtualCableOutput;

    private VbCableStatus _vbCableStatus = new();
    private AudioApplicationItem? _selectedApplicationSource;
    private AudioDeviceItem? _selectedMicrophone;
    private bool _includeMicrophone;
    private bool _isMicrophoneMuted;
    private int _musicVolume = 80;
    private int _microphoneVolume = 80;
    private string _statusText = "Idle";
    private bool _isRunning;
    private bool _isStarting;
    private bool _disposed;

    public MainViewModel(
        VbCableDetector vbCableDetector,
        AudioDeviceService audioDeviceService,
        ApplicationSourceService applicationSourceService,
        SessionLogger logger)
    {
        _vbCableDetector = vbCableDetector;
        _audioDeviceService = audioDeviceService;
        _applicationSourceService = applicationSourceService;
        _logger = logger;

        ApplicationSources = new ObservableCollection<AudioApplicationItem>();
        Microphones = new ObservableCollection<AudioDeviceItem>();

        StartCommand = new RelayCommand(Start, () => CanStart);
        StopCommand = new RelayCommand(Stop, () => CanStop);
        RecheckCommand = new RelayCommand(Recheck);
        OpenDownloadCommand = new RelayCommand(OpenDownloadPage);
        ToggleMicrophoneMuteCommand = new RelayCommand(ToggleMicrophoneMute, () => CanToggleMicrophoneMute);

        _applicationRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _applicationRefreshTimer.Tick += OnApplicationRefreshTimerTick;

        _audioDeviceService.DevicesChanged += OnDevicesChanged;
        RefreshSources();
        _applicationRefreshTimer.Start();
    }

    public ObservableCollection<AudioApplicationItem> ApplicationSources { get; }

    public ObservableCollection<AudioDeviceItem> Microphones { get; }

    public RelayCommand StartCommand { get; }

    public RelayCommand StopCommand { get; }

    public RelayCommand RecheckCommand { get; }

    public RelayCommand OpenDownloadCommand { get; }

    public RelayCommand ToggleMicrophoneMuteCommand { get; }

    public AudioApplicationItem? SelectedApplicationSource
    {
        get => _selectedApplicationSource;
        set
        {
            if (SetProperty(ref _selectedApplicationSource, value))
            {
                OnPropertyChanged(nameof(MusicSourcePreviewText));
                UpdateCommandStates();
            }
        }
    }

    public AudioDeviceItem? SelectedMicrophone
    {
        get => _selectedMicrophone;
        set
        {
            if (SetProperty(ref _selectedMicrophone, value))
            {
                OnPropertyChanged(nameof(MicrophonePreviewText));
                UpdateCommandStates();
            }
        }
    }

    public bool IncludeMicrophone
    {
        get => _includeMicrophone;
        set
        {
            if (!SetProperty(ref _includeMicrophone, value))
            {
                return;
            }

            if (value && SelectedMicrophone is null)
            {
                SelectedMicrophone = PickSelection(Microphones, null);
            }

            if (!value)
            {
                SetMicrophoneMuted(false);
            }

            OnPropertiesChanged(nameof(IsMicrophoneSelectionEnabled), nameof(IsMicrophoneVolumeEnabled), nameof(MicrophoneSectionSummary), nameof(IsMicrophoneMuteEnabled));
            UpdateCommandStates();
        }
    }

    public bool IsMicrophoneMuted
    {
        get => _isMicrophoneMuted;
        set => SetMicrophoneMuted(value);
    }

    public int MusicVolume
    {
        get => _musicVolume;
        set
        {
            if (SetProperty(ref _musicVolume, value))
            {
                _audioMixer?.SetMusicVolume(value);
                OnPropertyChanged(nameof(MusicVolumeText));
            }
        }
    }

    public int MicrophoneVolume
    {
        get => _microphoneVolume;
        set
        {
            if (SetProperty(ref _microphoneVolume, value))
            {
                _audioMixer?.SetMicrophoneVolume(value);
                OnPropertyChanged(nameof(MicrophoneVolumeText));
            }
        }
    }

    public string MusicVolumeText => $"{MusicVolume}%";

    public string MicrophoneVolumeText => $"{MicrophoneVolume}%";

    public string MicrophoneMuteButtonText => IsMicrophoneMuted ? "Unmute mic" : "Mute mic";

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertiesChanged(nameof(StatusBadgeText), nameof(IsRunningStatus), nameof(IsStartingStatus), nameof(HasErrorStatus));
            }
        }
    }

    public bool IsVbCableInstalled => _vbCableStatus.IsInstalled;

    public bool IsApplicationCaptureSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348);

    public string HeaderSummary => IsVbCableInstalled
        ? "Route one app straight into your game's voice chat through VB-Cable, with your mic mixed in only when you want it."
        : "Install VB-Cable first so Windows can expose a virtual microphone for your game.";

    public string VbCableInstalledText => $"VB-Cable installed: {(IsVbCableInstalled ? "Yes" : "No")}";

    public string VbCableSummary => IsVbCableInstalled && !string.IsNullOrWhiteSpace(_vbCableStatus.RenderEndpointName)
        ? $"Routing mixed audio to: {_vbCableStatus.RenderEndpointName}"
        : "Routing target unavailable until VB-Cable is installed.";

    public string GameMicrophoneInstruction => IsVbCableInstalled && !string.IsNullOrWhiteSpace(_vbCableStatus.CaptureEndpointName)
        ? $"In your game's voice chat settings, choose this microphone: {_vbCableStatus.CaptureEndpointName}"
        : "In your game's voice chat settings, choose this microphone: unavailable until VB-Cable is installed.";

    public string MusicSourceHelperText => IsApplicationCaptureSupported
        ? "Pick the app you want to send into voice chat. Browser and Spotify capture work best when the app is already open."
        : "Application-only capture requires Windows 10 build 20348 or later.";

    public string MusicSourcePreviewText => SelectedApplicationSource?.DisplayName ?? "Choose the app that is playing the audio you want to route.";

    public string MicrophonePreviewText => SelectedMicrophone?.DisplayName ?? "Choose a microphone to mix in, or leave microphone disabled.";

    public string MicrophoneSectionSummary => IncludeMicrophone
        ? "Your microphone will be mixed into the routed app audio."
        : "Only the selected app audio will be routed right now.";

    public string SetupBodyText => "VB-Cable is required so Windows can expose a virtual microphone to your game. Install it, then click Recheck.";

    public string InstallStepsText =>
        "1. Download VB-Cable from the official VB-Audio site.\n" +
        "2. Run VBCABLE_Setup_x64.exe as administrator.\n" +
        "3. Reboot if Windows asks.\n" +
        "4. Return here and click Recheck.";

    public Visibility SetupVisibility => IsVbCableInstalled ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MainVisibility => IsVbCableInstalled ? Visibility.Visible : Visibility.Collapsed;

    public bool CanStart =>
        !_isRunning
        && !_isStarting
        && IsVbCableInstalled
        && IsApplicationCaptureSupported
        && SelectedApplicationSource is not null
        && (!IncludeMicrophone || SelectedMicrophone is not null);

    public bool CanStop => _isRunning || _isStarting;

    public bool CanEditDeviceSelection => !_isRunning && !_isStarting;

    public bool IsMicrophoneSelectionEnabled => IncludeMicrophone && CanEditDeviceSelection;

    public bool IsMicrophoneVolumeEnabled => IncludeMicrophone;

    public bool CanToggleMicrophoneMute => IncludeMicrophone;

    public bool IsMicrophoneMuteEnabled => IncludeMicrophone;

    public bool IsRunningStatus => string.Equals(StatusText, "Running", StringComparison.OrdinalIgnoreCase);

    public bool IsStartingStatus => string.Equals(StatusText, "Starting", StringComparison.OrdinalIgnoreCase);

    public bool HasErrorStatus => StatusText.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);

    public string StatusBadgeText => HasErrorStatus
        ? "Needs attention"
        : IsRunningStatus
            ? "Live"
            : IsStartingStatus
                ? "Connecting"
                : string.Equals(StatusText, "Stopped", StringComparison.OrdinalIgnoreCase)
                    ? "Stopped"
                    : "Idle";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _applicationRefreshTimer.Stop();
        _applicationRefreshTimer.Tick -= OnApplicationRefreshTimerTick;
        _audioDeviceService.DevicesChanged -= OnDevicesChanged;
        StopPipeline(updateStatus: false);
        _audioDeviceService.Dispose();
        _logger.Dispose();
    }

    private void Start()
    {
        if (!CanStart)
        {
            return;
        }

        _isStarting = true;
        StatusText = "Starting";
        UpdateStateProperties();

        try
        {
            RefreshApplicationSources(force: true);

            var currentCableStatus = _vbCableDetector.Detect();
            if (!currentCableStatus.IsInstalled || string.IsNullOrWhiteSpace(currentCableStatus.RenderEndpointId))
            {
                throw new InvalidOperationException("VB-Cable was not found. Install it, then click Recheck.");
            }

            var cableRenderDevice = _audioDeviceService.GetRenderDeviceById(currentCableStatus.RenderEndpointId);
            _musicSource = CreateMusicSource();
            _musicSource.Faulted += OnAudioRuntimeFault;

            if (IncludeMicrophone)
            {
                if (SelectedMicrophone is null)
                {
                    throw new InvalidOperationException("Pick a microphone or turn off Include microphone.");
                }

                var microphoneDevice = _audioDeviceService.GetCaptureDeviceById(SelectedMicrophone.DeviceId);
                if (_vbCableDetector.IsVbCableDevice(microphoneDevice))
                {
                    throw new InvalidOperationException("VB-Cable cannot be used as the microphone source.");
                }

                _microphoneCapture = new MicrophoneCaptureService(microphoneDevice, _logger);
                _microphoneCapture.Faulted += OnAudioRuntimeFault;
            }

            _audioMixer = new AudioMixerService(cableRenderDevice, _logger);
            var mixedSampleProvider = _audioMixer.BuildMixer(_musicSource, _microphoneCapture, MusicVolume, MicrophoneVolume);
            _audioMixer.SetMicrophoneMuted(IsMicrophoneMuted);

            _virtualCableOutput = new VirtualCableOutputService(cableRenderDevice, _logger);
            _virtualCableOutput.Faulted += OnAudioRuntimeFault;
            _virtualCableOutput.Start(mixedSampleProvider);

            _musicSource.Start();
            _microphoneCapture?.Start();

            _vbCableStatus = currentCableStatus;
            _isRunning = true;
            StatusText = "Running";
            _logger.Log("Audio session started successfully.");
        }
        catch (Exception exception)
        {
            _logger.Log("Failed to start the audio session.", exception);
            StopPipeline(updateStatus: false);
            StatusText = $"Error: {exception.Message}";
        }
        finally
        {
            _isStarting = false;
            UpdateStateProperties();
        }
    }

    private void Stop()
    {
        StopPipeline(updateStatus: true);
    }

    private void Recheck()
    {
        RefreshSources();

        if (!_isRunning && !_isStarting)
        {
            StatusText = "Idle";
        }
    }

    private void OpenDownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = VbCableDownloadUrl,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            _logger.Log("Failed to open the VB-Cable download page.", exception);
            StatusText = $"Error: {exception.Message}";
        }
    }

    private void RefreshSources()
    {
        RefreshDeviceSources();
        RefreshApplicationSources(force: false);
    }

    private void RefreshDeviceSources()
    {
        var previousMicrophoneDeviceId = SelectedMicrophone?.DeviceId;
        var wasRunning = _isRunning || _isStarting;

        _vbCableStatus = _vbCableDetector.Detect();

        var microphones = _audioDeviceService.GetMicrophones();
        var previousMicrophoneStillExists = previousMicrophoneDeviceId is not null
            && microphones.Any(device => string.Equals(device.DeviceId, previousMicrophoneDeviceId, StringComparison.OrdinalIgnoreCase));

        ReplaceCollection(Microphones, microphones);
        SelectedMicrophone = PickSelection(Microphones, previousMicrophoneDeviceId);

        if (IncludeMicrophone && SelectedMicrophone is null)
        {
            SelectedMicrophone = PickSelection(Microphones, null);
        }

        OnPropertiesChanged(
            nameof(IsVbCableInstalled),
            nameof(HeaderSummary),
            nameof(VbCableInstalledText),
            nameof(VbCableSummary),
            nameof(GameMicrophoneInstruction),
            nameof(SetupVisibility),
            nameof(MainVisibility),
            nameof(IsMicrophoneSelectionEnabled),
            nameof(IsMicrophoneVolumeEnabled),
            nameof(MicrophoneSectionSummary),
            nameof(IsMicrophoneMuteEnabled));

        UpdateCommandStates();

        _logger.Log($"Device refresh complete. Microphones: {Microphones.Count}. VB-Cable installed: {IsVbCableInstalled}.");

        if (!wasRunning)
        {
            return;
        }

        var lostMicrophoneSource = IncludeMicrophone && previousMicrophoneDeviceId is not null && !previousMicrophoneStillExists;

        if (!_vbCableStatus.IsInstalled || lostMicrophoneSource)
        {
            HandleRuntimeFault("A required audio device changed or disappeared. Audio routing was stopped.");
        }
    }

    private void RefreshApplicationSources(bool force)
    {
        if ((_isRunning || _isStarting) && !force)
        {
            return;
        }

        var previousProcessId = SelectedApplicationSource?.ProcessId;
        var applicationSources = _applicationSourceService.GetApplicationSources();
        ReplaceCollection(ApplicationSources, applicationSources);
        SelectedApplicationSource = PickApplicationSelection(ApplicationSources, previousProcessId);
        UpdateCommandStates();
    }

    private void StopPipeline(bool updateStatus)
    {
        var firstErrorMessage = string.Empty;

        DetachRuntimeHandlers();

        try
        {
            _microphoneCapture?.Stop();
        }
        catch (Exception exception)
        {
            firstErrorMessage = string.IsNullOrWhiteSpace(firstErrorMessage) ? exception.Message : firstErrorMessage;
            _logger.Log("Microphone stop failed.", exception);
        }

        try
        {
            _musicSource?.Stop();
        }
        catch (Exception exception)
        {
            firstErrorMessage = string.IsNullOrWhiteSpace(firstErrorMessage) ? exception.Message : firstErrorMessage;
            _logger.Log("Music stop failed.", exception);
        }

        try
        {
            _virtualCableOutput?.Stop();
        }
        catch (Exception exception)
        {
            firstErrorMessage = string.IsNullOrWhiteSpace(firstErrorMessage) ? exception.Message : firstErrorMessage;
            _logger.Log("VB-Cable output stop failed.", exception);
        }

        _microphoneCapture?.Dispose();
        _musicSource?.Dispose();
        _virtualCableOutput?.Dispose();
        _audioMixer?.Dispose();

        _microphoneCapture = null;
        _musicSource = null;
        _virtualCableOutput = null;
        _audioMixer = null;

        _isRunning = false;
        _isStarting = false;
        UpdateStateProperties();

        if (!updateStatus)
        {
            return;
        }

        StatusText = string.IsNullOrWhiteSpace(firstErrorMessage)
            ? "Stopped"
            : $"Error: {firstErrorMessage}";
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshDeviceSources));
    }

    private void OnApplicationRefreshTimerTick(object? sender, EventArgs e)
    {
        if (_disposed || _isRunning || _isStarting)
        {
            return;
        }

        RefreshApplicationSources(force: false);
    }

    private void OnAudioRuntimeFault(object? sender, string message)
    {
        if (_disposed)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => HandleRuntimeFault(message)));
    }

    private void HandleRuntimeFault(string message)
    {
        _logger.Log(message);
        StopPipeline(updateStatus: false);
        StatusText = $"Error: {message}";
    }

    private void DetachRuntimeHandlers()
    {
        if (_musicSource is not null)
        {
            _musicSource.Faulted -= OnAudioRuntimeFault;
        }

        if (_microphoneCapture is not null)
        {
            _microphoneCapture.Faulted -= OnAudioRuntimeFault;
        }

        if (_virtualCableOutput is not null)
        {
            _virtualCableOutput.Faulted -= OnAudioRuntimeFault;
        }
    }

    private void UpdateStateProperties()
    {
        OnPropertiesChanged(nameof(CanEditDeviceSelection), nameof(IsMicrophoneSelectionEnabled), nameof(IsMicrophoneMuteEnabled));
        UpdateCommandStates();
    }

    private void UpdateCommandStates()
    {
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        ToggleMicrophoneMuteCommand.RaiseCanExecuteChanged();
    }

    private void ToggleMicrophoneMute()
    {
        if (!CanToggleMicrophoneMute)
        {
            return;
        }

        SetMicrophoneMuted(!IsMicrophoneMuted);
    }

    private void SetMicrophoneMuted(bool value)
    {
        if (!SetProperty(ref _isMicrophoneMuted, value, nameof(IsMicrophoneMuted)))
        {
            return;
        }

        _audioMixer?.SetMicrophoneMuted(value);
        OnPropertyChanged(nameof(MicrophoneMuteButtonText));
    }

    private IAudioSource CreateMusicSource()
    {
        if (!IsApplicationCaptureSupported)
        {
            throw new InvalidOperationException("Application-only capture requires Windows 10 build 20348 or later.");
        }

        if (SelectedApplicationSource is null)
        {
            throw new InvalidOperationException("Pick an app to capture before starting.");
        }

        using var process = Process.GetProcessById(SelectedApplicationSource.ProcessId);
        if (process.HasExited)
        {
            throw new InvalidOperationException("The selected app has already closed. Pick it again and retry.");
        }

        return new ProcessLoopbackCaptureService((uint)SelectedApplicationSource.ProcessId, SelectedApplicationSource.DisplayName, _logger);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static AudioDeviceItem? PickSelection(IEnumerable<AudioDeviceItem> items, string? preferredDeviceId)
    {
        var list = items.ToList();

        if (!string.IsNullOrWhiteSpace(preferredDeviceId))
        {
            var existing = list.FirstOrDefault(item =>
                string.Equals(item.DeviceId, preferredDeviceId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return existing;
            }
        }

        return list.FirstOrDefault(item => item.IsDefault) ?? list.FirstOrDefault();
    }

    private static AudioApplicationItem? PickApplicationSelection(IEnumerable<AudioApplicationItem> items, int? preferredProcessId)
    {
        var list = items.ToList();

        if (preferredProcessId.HasValue)
        {
            var existing = list.FirstOrDefault(item => item.ProcessId == preferredProcessId.Value);
            if (existing is not null)
            {
                return existing;
            }
        }

        return list.FirstOrDefault(item => item.HasAudioSession)
               ?? list.FirstOrDefault(item => item.HasVisibleWindow)
               ?? list.FirstOrDefault();
    }
}

