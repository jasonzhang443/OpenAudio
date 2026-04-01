namespace OpenAudio.Models;

public sealed class MusicSourceModeOption
{
    public MusicSourceModeOption(MusicSourceMode mode, string displayName)
    {
        Mode = mode;
        DisplayName = displayName;
    }

    public MusicSourceMode Mode { get; }

    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}


