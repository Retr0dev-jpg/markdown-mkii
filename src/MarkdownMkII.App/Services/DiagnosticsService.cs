using System.Text;
using MarkdownMkII.Core.Models;

namespace MarkdownMkII.Services;

public static class DiagnosticsService
{
    private const long MaxLogBytes = 5 * 1024 * 1024;
    private const int RotationCheckInterval = 200;
    private const string Stage = "Diagnostics";

    private static readonly string LogFilePath = AppPaths.LogFile;

    private static readonly object Gate = new();

    [ThreadStatic]
    private static bool isResolvingLevel;

    private static LogLevel? cachedLevel;
    private static int isSubscribed;
    private static int writesSinceRotationCheck = RotationCheckInterval;

    public static string LogPath => LogFilePath;

    public static string LogDirectory => Path.GetDirectoryName(LogFilePath) ?? string.Empty;

    public static LogLevel Level => ResolveLevel();

    static DiagnosticsService()
    {
        try
        {
            var directory = Path.GetDirectoryName(LogFilePath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch
        {
        }
    }

    public static void Log(string stage, string message)
    {
        if (ResolveLevel() != LogLevel.Verbose)
        {
            return;
        }

        Write($"INFO  [{stage}] {message}");
    }

    public static void LogError(string stage, string message, Exception? exception = null)
    {
        if (ResolveLevel() == LogLevel.Off)
        {
            return;
        }

        var privateArchive = NoteArchive.Database.IsProtectionConfigured;
        var text = exception is null
            ? $"ERROR [{stage}] {message}"
            : $"ERROR [{stage}] {message} :: {exception.GetType().Name}: {(privateArchive ? exception.HResult.ToString("X8") : exception.Message)}";
        Write(text);
    }

    public static Task<long> GetSizeAsync() => Task.Run(() =>
    {
        try
        {
            var info = new FileInfo(LogFilePath);
            return info.Exists ? info.Length : 0L;
        }
        catch
        {
            return 0L;
        }
    });

    public static Task ClearAsync() => Task.Run(() =>
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.WriteAllText(LogFilePath, string.Empty, Encoding.UTF8);
                writesSinceRotationCheck = RotationCheckInterval;
            }
        }
        catch
        {
        }
    });

    private static LogLevel ResolveLevel()
    {
        if (cachedLevel.HasValue)
        {
            return cachedLevel.Value;
        }

        if (isResolvingLevel)
        {
            return LogLevel.ErrorsOnly;
        }

        isResolvingLevel = true;
        try
        {
            var level = SettingsService.Instance.Diagnostics.LogLevel;
            if (Subscribe())
            {
                cachedLevel = level;
            }

            return level;
        }
        catch
        {
            return LogLevel.ErrorsOnly;
        }
        finally
        {
            isResolvingLevel = false;
        }
    }

    private static bool Subscribe()
    {
        if (Interlocked.Exchange(ref isSubscribed, 1) != 0)
        {
            return true;
        }

        try
        {
            SettingsService.Instance.Changed += (_, e) =>
            {
                if (e.Includes(SettingsArea.Diagnostics))
                {
                    cachedLevel = null;
                }
            };
            return true;
        }
        catch
        {
            Interlocked.Exchange(ref isSubscribed, 0);
            return false;
        }
    }

    private static void Write(string line)
    {
        var timestamped = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} | {line}";
        try
        {
            lock (Gate)
            {
                File.AppendAllText(LogFilePath, timestamped + Environment.NewLine, Encoding.UTF8);
                if (++writesSinceRotationCheck >= RotationCheckInterval)
                {
                    writesSinceRotationCheck = 0;
                    RotateIfNeeded();
                }
            }
        }
        catch
        {
        }

        System.Diagnostics.Debug.WriteLine(timestamped);
    }

    private static void RotateIfNeeded()
    {
        var info = new FileInfo(LogFilePath);
        if (!info.Exists || info.Length <= MaxLogBytes)
        {
            return;
        }

        var bytes = File.ReadAllBytes(LogFilePath);
        var start = bytes.Length / 2;
        while (start < bytes.Length && bytes[start] != (byte)'\n')
        {
            start++;
        }

        if (start < bytes.Length)
        {
            start++;
        }

        File.WriteAllBytes(LogFilePath, bytes[start..]);
    }
}
