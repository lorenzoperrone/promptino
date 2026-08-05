using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Promptino.Platform;

/// <summary>
/// Provides logging capabilities across the application.
/// </summary>
public interface ILogger
{
    /// <summary>Logs an informational message.</summary>
    void LogInfo(string message);

    /// <summary>Logs a warning message.</summary>
    void LogWarning(string message);

    /// <summary>Logs an error message with an optional exception payload.</summary>
    void LogError(string message, Exception? exception = null);
}

/// <summary>
/// Thread-safe file logger supporting automatic size-based log rotation and privacy path sanitization.
/// </summary>
public sealed class FileLogger : ILogger
{
    private readonly string _logFilePath;
    private readonly object _lock = new();
    private static readonly (string Path, string Placeholder)[]? _sensitivePaths;

    /// <summary>Gets or sets the maximum log file size in bytes before triggering log rotation (default 1 MB).</summary>
    public long MaxLogFileSize { get; set; } = 1 * 1024 * 1024; // Default: 1 MB

    /// <summary>Gets or sets the maximum number of rotated backup log files to retain (default 5).</summary>
    public int MaxBackupFiles { get; set; } = 5; // Default: 5 backups

    /// <summary>
    /// Initializes a new instance of <see cref="FileLogger"/>.
    /// </summary>
    /// <param name="pathProvider">Optional path provider; uses <see cref="WindowsAppDataPathProvider"/> if null.</param>
    public FileLogger(IAppDataPathProvider? pathProvider = null)
    {
        var provider = pathProvider ?? new WindowsAppDataPathProvider();
        _logFilePath = provider.GetLogFilePath();
    }

    static FileLogger()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var paths = new List<(string Path, string Placeholder)>();

            if (!string.IsNullOrEmpty(appData))
                CollectPathVariants(appData, "%APPDATA%", paths);
            if (!string.IsNullOrEmpty(userProfile))
                CollectPathVariants(userProfile, "%USERPROFILE%", paths);

            _sensitivePaths = paths.OrderByDescending(p => p.Path.Length).ToArray();
        }
        catch
        {
            _sensitivePaths = null;
        }
    }

    /// <inheritdoc />
    public void LogInfo(string message) => Log("INFO", message);

    /// <inheritdoc />
    public void LogWarning(string message) => Log("WARNING", message);

    /// <inheritdoc />
    public void LogError(string message, Exception? exception = null)
    {
        var fullMessage = exception != null ? $"{message} - Exception: {exception.Message}\nStack: {exception.StackTrace}" : message;
        Log("ERROR", fullMessage);
    }

    private void Log(string level, string message)
    {
        try
        {
            message = SanitizeMessage(message);

            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (_lock)
            {
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length >= MaxLogFileSize)
                    {
                        RotateLogFile();
                    }
                }

                var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, line);
            }
        }
        catch
        {
            // Resiliency boundary: swallow logging errors to prevent application crashes
        }
    }

    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || _sensitivePaths is null) return message;

        foreach (var (path, placeholder) in _sensitivePaths)
        {
            if (message.Contains(path, StringComparison.OrdinalIgnoreCase))
                message = message.Replace(path, placeholder, StringComparison.OrdinalIgnoreCase);
        }

        return message;
    }

    private static void CollectPathVariants(string original, string placeholder, List<(string Path, string Placeholder)> list)
    {
        try
        {
            list.Add((Path.GetFullPath(original), placeholder));

            var shortPath = GetShortPathName(original);
            if (shortPath is not null && !shortPath.Equals(original, StringComparison.OrdinalIgnoreCase))
                list.Add((shortPath, placeholder));
        }
        catch { }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);

    private static string? GetShortPathName(string path)
    {
        var sb = new StringBuilder(260);
        var len = GetShortPathName(path, sb, sb.Capacity);
        return len > 0 ? sb.ToString() : null;
    }

    private void RotateLogFile()
    {
        try
        {
            string maxBackupPath = $"{_logFilePath}.{MaxBackupFiles}";
            if (File.Exists(maxBackupPath))
            {
                File.Delete(maxBackupPath);
            }

            for (int i = MaxBackupFiles - 1; i >= 1; i--)
            {
                string source = $"{_logFilePath}.{i}";
                string dest = $"{_logFilePath}.{i + 1}";
                if (File.Exists(source))
                {
                    if (File.Exists(dest))
                    {
                        File.Delete(dest);
                    }
                    File.Move(source, dest);
                }
            }

            if (File.Exists(_logFilePath))
            {
                string dest = $"{_logFilePath}.1";
                if (File.Exists(dest))
                {
                    File.Delete(dest);
                }
                File.Move(_logFilePath, dest);
            }
        }
        catch
        {
            // Ignore rotation errors to remain resilient
        }
    }
}
