using System.Diagnostics;
using OpenAudio.Models;
using NAudio.CoreAudioApi;

namespace OpenAudio.Services;

public sealed class ApplicationSourceService
{
    private readonly VbCableDetector _vbCableDetector;
    private readonly SessionLogger _logger;

    public ApplicationSourceService(VbCableDetector vbCableDetector, SessionLogger logger)
    {
        _vbCableDetector = vbCableDetector;
        _logger = logger;
    }

    public IReadOnlyList<AudioApplicationItem> GetApplicationSources()
    {
        var currentProcessId = Environment.ProcessId;
        var items = new Dictionary<int, Candidate>();
        var visibleProcessesByName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentProcessId || process.SessionId != Process.GetCurrentProcess().SessionId || process.HasExited)
                {
                    continue;
                }

                var hasVisibleWindow = process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(process.MainWindowTitle);
                if (!hasVisibleWindow)
                {
                    continue;
                }

                var processName = CleanProcessName(process.ProcessName);
                var candidate = new Candidate(process.Id, processName, process.MainWindowTitle, hasVisibleWindow, hasAudioSession: false);
                items[process.Id] = candidate;

                if (!visibleProcessesByName.TryGetValue(processName, out var ids))
                {
                    ids = new List<int>();
                    visibleProcessesByName[processName] = ids;
                }

                ids.Add(process.Id);
            }
            catch
            {
                // Some processes cannot be inspected safely.
            }
            finally
            {
                process.Dispose();
            }
        }

        foreach (var sessionProcessId in GetAudioSessionProcessIds())
        {
            if (sessionProcessId == 0 || sessionProcessId == currentProcessId)
            {
                continue;
            }

            if (items.TryGetValue((int)sessionProcessId, out var existing))
            {
                existing.HasAudioSession = true;
                continue;
            }

            try
            {
                using var process = Process.GetProcessById((int)sessionProcessId);
                if (process.HasExited)
                {
                    continue;
                }

                var processName = CleanProcessName(process.ProcessName);
                if (visibleProcessesByName.TryGetValue(processName, out var ids) && ids.Count == 1 && items.TryGetValue(ids[0], out var visibleMatch))
                {
                    visibleMatch.HasAudioSession = true;
                    continue;
                }

                items[process.Id] = new Candidate(process.Id, processName, process.MainWindowTitle, hasVisibleWindow: process.MainWindowHandle != IntPtr.Zero, hasAudioSession: true);
            }
            catch
            {
                // Process may have already exited.
            }
        }

        var results = items.Values
            .Where(candidate => !string.Equals(candidate.ProcessName, "System", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.HasAudioSession)
            .ThenByDescending(candidate => candidate.HasVisibleWindow)
            .ThenBy(candidate => candidate.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(candidate => candidate.WindowTitle, StringComparer.CurrentCultureIgnoreCase)
            .Select(candidate => new AudioApplicationItem(
                candidate.ProcessId,
                candidate.ProcessName,
                candidate.WindowTitle,
                candidate.HasAudioSession,
                candidate.HasVisibleWindow))
            .ToList();

        _logger.Log($"Application source refresh complete. App sources: {results.Count}.");
        return results;
    }

    private IEnumerable<uint> GetAudioSessionProcessIds()
    {
        var processIds = new List<uint>();
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        foreach (var device in devices)
        {
            if (_vbCableDetector.IsVbCableDevice(device))
            {
                continue;
            }

            SessionCollection? sessions = null;

            try
            {
                sessions = device.AudioSessionManager.Sessions;
                for (var index = 0; index < sessions.Count; index++)
                {
                    AudioSessionControl? session = null;

                    try
                    {
                        session = sessions[index];
                        if (session.IsSystemSoundsSession)
                        {
                            continue;
                        }

                        var processId = session.GetProcessID;
                        if (processId > 0)
                        {
                            processIds.Add(processId);
                        }
                    }
                    finally
                    {
                        session?.Dispose();
                    }
                }
            }
            catch
            {
                // Session enumeration is best-effort only.
            }
            finally
            {
                device.Dispose();
            }
        }

        return processIds;
    }

    private static string CleanProcessName(string processName) =>
        string.IsNullOrWhiteSpace(processName) ? "Unknown app" : processName.Trim();

    private sealed class Candidate
    {
        public Candidate(int processId, string processName, string? windowTitle, bool hasVisibleWindow, bool hasAudioSession)
        {
            ProcessId = processId;
            ProcessName = processName;
            WindowTitle = string.IsNullOrWhiteSpace(windowTitle) ? null : windowTitle.Trim();
            HasVisibleWindow = hasVisibleWindow;
            HasAudioSession = hasAudioSession;
        }

        public int ProcessId { get; }

        public string ProcessName { get; }

        public string? WindowTitle { get; }

        public bool HasVisibleWindow { get; }

        public bool HasAudioSession { get; set; }
    }
}

