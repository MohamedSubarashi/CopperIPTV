using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperIPTV.Services;

namespace CopperIPTV.ViewModels;

public partial class LogViewModel : ViewModelBase
{
    public ObservableCollection<LogEntry> Entries { get; } = [];

    [ObservableProperty]
    private int _entryCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private bool _showErrorsOnly;

    public LogViewModel()
    {
        foreach (var entry in LogService.Entries)
        {
            Entries.Add(entry);
        }

        LogService.OnLogAdded += OnLogAdded;
        UpdateCounts();
    }

    private void OnLogAdded(LogEntry entry)
    {
        Entries.Add(entry);
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        EntryCount = Entries.Count;
        ErrorCount = 0;
        foreach (var e in Entries)
        {
            if (e.Level == LogLevel.Error) ErrorCount++;
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        LogService.Clear();
        Entries.Clear();
        UpdateCounts();
    }
}
