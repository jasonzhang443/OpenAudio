namespace OpenAudio.Models;

public sealed class AudioDeviceItem
{
    public AudioDeviceItem(string deviceId, string friendlyName, bool isDefault)
    {
        DeviceId = deviceId;
        FriendlyName = friendlyName;
        IsDefault = isDefault;
    }

    public string DeviceId { get; }

    public string FriendlyName { get; }

    public bool IsDefault { get; }

    public string DisplayName => IsDefault ? $"{FriendlyName} (Default)" : FriendlyName;

    public override string ToString() => DisplayName;
}


