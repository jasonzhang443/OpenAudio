using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace OpenAudio.Services;

public sealed class SessionLogger : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly List<string> _entries = new();
    private readonly string? _logFilePath;
    private bool _disposed;

    public SessionLogger()
    {
        try
        {
            var logsDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(logsDirectory);
            _logFilePath = Path.Combine(logsDirectory, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(_logFilePath, "OpenAudio session log" + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            _logFilePath = null;
        }
    }

    public ReadOnlyCollection<string> Entries
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.AsReadOnly();
            }
        }
    }

    public string? LogFilePath => _logFilePath;

    public void Log(string message, Exception? exception = null)
    {
        if (_disposed)
        {
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (exception is not null)
        {
            line = $"{line} | {exception.GetType().Name}: {exception.Message}";
        }

        lock (_syncRoot)
        {
            _entries.Add(line);
        }

        if (!string.IsNullOrWhiteSpace(_logFilePath))
        {
            try
            {
                var fileLine = exception is null
                    ? line
                    : $"{line}{Environment.NewLine}{exception}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, fileLine + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Optional file logging should never break the app.
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

