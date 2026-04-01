namespace OpenAudio.Models;

public sealed class AudioApplicationItem
{
    public AudioApplicationItem(int processId, string processName, string? windowTitle, bool hasAudioSession, bool hasVisibleWindow)
    {
        ProcessId = processId;
        ProcessName = processName;
        WindowTitle = windowTitle;
        HasAudioSession = hasAudioSession;
        HasVisibleWindow = hasVisibleWindow;
    }

    public int ProcessId { get; }

    public string ProcessName { get; }

    public string? WindowTitle { get; }

    public bool HasAudioSession { get; }

    public bool HasVisibleWindow { get; }

    public string DisplayName
    {
        get
        {
            var baseName = string.IsNullOrWhiteSpace(WindowTitle)
                ? ProcessName
                : $"{ProcessName} - {WindowTitle}";

            var audioHint = HasAudioSession ? " [audio detected]" : string.Empty;
            return $"{baseName}{audioHint} (PID {ProcessId})";
        }
    }

    public override string ToString() => DisplayName;
}


