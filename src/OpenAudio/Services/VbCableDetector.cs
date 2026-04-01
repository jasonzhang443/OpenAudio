using OpenAudio.Models;
using NAudio.CoreAudioApi;

namespace OpenAudio.Services;

public sealed class VbCableDetector
{
    private readonly SessionLogger _logger;

    public VbCableDetector(SessionLogger logger)
    {
        _logger = logger;
    }

    public VbCableStatus Detect()
    {
        using var enumerator = new MMDeviceEnumerator();

        var renderEndpoint = enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(IsRenderEndpointMatch);

        var captureEndpoint = enumerator
            .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(IsCaptureEndpointMatch);

        var status = new VbCableStatus
        {
            IsInstalled = renderEndpoint is not null && captureEndpoint is not null,
            RenderEndpointId = renderEndpoint?.ID,
            RenderEndpointName = CleanFriendlyName(renderEndpoint?.FriendlyName),
            CaptureEndpointId = captureEndpoint?.ID,
            CaptureEndpointName = CleanFriendlyName(captureEndpoint?.FriendlyName)
        };

        _logger.Log(
            $"VB-Cable detection completed. Installed: {status.IsInstalled}. Render: {status.RenderEndpointName ?? "missing"}. Capture: {status.CaptureEndpointName ?? "missing"}.");

        return status;
    }

    public bool IsVbCableDevice(MMDevice device) => IsAnyVbCableName(device.FriendlyName);

    private static bool IsRenderEndpointMatch(MMDevice device)
    {
        var name = device.FriendlyName;
        return Contains(name, "CABLE Input")
            || (IsAnyVbCableName(name) && Contains(name, "Input"));
    }

    private static bool IsCaptureEndpointMatch(MMDevice device)
    {
        var name = device.FriendlyName;
        return Contains(name, "CABLE Output")
            || (IsAnyVbCableName(name) && Contains(name, "Output"));
    }

    private static bool IsAnyVbCableName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return Contains(name, "VB-Audio Virtual Cable")
            || Contains(name, "VB Audio Virtual Cable")
            || Contains(name, "CABLE Input")
            || Contains(name, "CABLE Output");
    }

    private static bool Contains(string? value, string token) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static string? CleanFriendlyName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();
}


