using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace CopperIPTV.Services;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public static class LogService
{
    private static readonly object _lock = new();
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly string _logFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CopperIPTV", "copperiptv.log.txt");

    public static string LogFilePath => _logFilePath;

    static LogService()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!); }
        catch { }
    }

    public static void Info(string message, [CallerMemberName] string source = "") =>
        AddEntry(LogLevel.Info, message, source);

    public static void Debug(string message, [CallerMemberName] string source = "") =>
        AddEntry(LogLevel.Debug, message, source);

    public static void Warning(string message, [CallerMemberName] string source = "") =>
        AddEntry(LogLevel.Warning, message, source);

    public static void Error(string message, [CallerMemberName] string source = "") =>
        AddEntry(LogLevel.Error, message, source);

    public static void Error(Exception ex, string message = "", [CallerMemberName] string source = "")
    {
        var fullMessage = string.IsNullOrEmpty(message) ? ex.Message : $"{message}: {ex.Message}";
        AddEntry(LogLevel.Error, fullMessage, source);
    }

    private static void AddEntry(LogLevel level, string message, string source)
    {
        var ts = DateTime.Now;
        lock (_lock)
        {
            try
            {
                if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > MaxFileSize)
                    File.Delete(_logFilePath);

                var line = $"[{ts:yyyy-MM-dd HH:mm:ss.fff}] [{level,-7}] [{source}] {message}";
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
