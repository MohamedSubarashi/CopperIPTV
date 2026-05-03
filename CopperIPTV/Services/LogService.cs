using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace CopperIPTV.Services;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");
}

public static class LogService
{
    private static readonly ObservableCollection<LogEntry> _entries = [];
    private static readonly object _lock = new();
    private const int MaxEntries = 1000;
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly string _logFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CopperIPTV", "copper_iptv.log");

    public static IReadOnlyList<LogEntry> Entries => _entries;
    public static event Action<LogEntry>? OnLogAdded;

    static LogService()
    {
        try
        {
            var dir = Path.GetDirectoryName(_logFilePath)!;
            Directory.CreateDirectory(dir);
        }
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
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source
        };

        lock (_lock)
        {
            var consoleColor = level switch
            {
                LogLevel.Debug => ConsoleColor.DarkGray,
                LogLevel.Info => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => ConsoleColor.White
            };

            Console.ForegroundColor = consoleColor;
            Console.WriteLine($"[{entry.FormattedTime}] [{level,-7}] [{source}] {message}");
            Console.ResetColor();

            try
            {
                if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > MaxFileSize)
                    File.Delete(_logFilePath);

                var fileLine = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level,-7}] [{source}] {message}";
                File.AppendAllText(_logFilePath, fileLine + Environment.NewLine);
            }
            catch { }

            if (_entries.Count >= MaxEntries)
                Dispatcher.UIThread.Post(() => _entries.RemoveAt(0));

            Dispatcher.UIThread.Post(() =>
            {
                _entries.Add(entry);
                OnLogAdded?.Invoke(entry);
            });
        }
    }

    public static void Clear()
    {
        lock (_lock)
            Dispatcher.UIThread.Post(() => _entries.Clear());
    }
}
