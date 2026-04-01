using OpenAudio.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace OpenAudio.Services;

public sealed class AudioDeviceService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationClient _notificationClient;
    private readonly VbCableDetector _vbCableDetector;
    private readonly SessionLogger _logger;
    private bool _disposed;

    public AudioDeviceService(VbCableDetector vbCableDetector, SessionLogger logger)
    {
        _vbCableDetector = vbCableDetector;
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();
        _notificationClient = new DeviceNotificationClient(() =>
        {
            _logger.Log("Windows audio device topology changed.");
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        });
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
    }

    public event EventHandler? DevicesChanged;

    public IReadOnlyList<AudioDeviceItem> GetPlaybackDevices() => GetDevices(DataFlow.Render);

    public IReadOnlyList<AudioDeviceItem> GetMicrophones() => GetDevices(DataFlow.Capture);

    public MMDevice GetRenderDeviceById(string deviceId) => _enumerator.GetDevice(deviceId);

    public MMDevice GetCaptureDeviceById(string deviceId) => _enumerator.GetDevice(deviceId);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
        _enumerator.Dispose();
    }

    private IReadOnlyList<AudioDeviceItem> GetDevices(DataFlow flow)
    {
        var defaultDeviceId = TryGetDefaultDeviceId(flow);

        return _enumerator
            .EnumerateAudioEndPoints(flow, DeviceState.Active)
            .Where(device => !_vbCableDetector.IsVbCableDevice(device))
            .Select(device => new AudioDeviceItem(
                device.ID,
                CleanFriendlyName(device.FriendlyName),
                string.Equals(device.ID, defaultDeviceId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(device => device.IsDefault)
            .ThenBy(device => device.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private string? TryGetDefaultDeviceId(DataFlow flow)
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia).ID;
        }
        catch
        {
            return null;
        }
    }

    private static string CleanFriendlyName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Unnamed audio device" : name.Trim();

    private sealed class DeviceNotificationClient : IMMNotificationClient
    {
        private readonly Action _notifyChange;

        public DeviceNotificationClient(Action notifyChange)
        {
            _notifyChange = notifyChange;
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _notifyChange();

        public void OnDeviceAdded(string pwstrDeviceId) => _notifyChange();

        public void OnDeviceRemoved(string deviceId) => _notifyChange();

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => _notifyChange();

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}


