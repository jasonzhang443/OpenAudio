namespace OpenAudio.Models;

public sealed class VbCableStatus
{
    public bool IsInstalled { get; init; }

    public string? RenderEndpointId { get; init; }

    public string? RenderEndpointName { get; init; }

    public string? CaptureEndpointId { get; init; }

    public string? CaptureEndpointName { get; init; }
}

