using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperIPTV.Models;
using CopperIPTV.Services;

namespace CopperIPTV.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private ObservableCollection<PlaylistInfo> _playlists = [];

    [ObservableProperty]
    private string _playlistName = string.Empty;

    [ObservableProperty]
    private string _playlistUrl = string.Empty;

    [ObservableProperty]
    private string _rawContent = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _fgCode = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAddForm;

    [ObservableProperty]
    private int _addMode;

    [ObservableProperty]
    private bool _showLog;

    [ObservableProperty]
    private string _epgUrl = "";

    [ObservableProperty]
    private bool _isRefreshingEpg;

    [ObservableProperty]
    private string _epgStatus = "";

    [ObservableProperty]
    private int _defaultVolume = 80;

    [ObservableProperty]
    private int _networkCaching = 3000;

    [ObservableProperty]
    private bool _autoHealthCheck = false;

    [ObservableProperty]
    private int _healthCheckInterval = 30;

    [ObservableProperty]
    private bool _autoPlaylistRefresh = false;

    [ObservableProperty]
    private string _healthStatus = "Not running";

    [ObservableProperty]
    private string _refreshStatus = "Not running";

    [ObservableProperty]
    private int _recentChannelCount = 0;

    [ObservableProperty]
    private ObservableCollection<XtreamAccountInfo> _xtreamAccounts = [];

    [ObservableProperty]
    private string _xtreamServerUrl = string.Empty;

    [ObservableProperty]
    private string _xtreamUsername = string.Empty;

    [ObservableProperty]
    private string _xtreamPassword = string.Empty;

    [ObservableProperty]
    private bool _isXtreamSyncing;

    [ObservableProperty]
    private string _xtreamStatus = string.Empty;

    [ObservableProperty]
    private bool _includeVod = true;

    [ObservableProperty]
    private bool _showXtreamForm;

    public SettingsViewModel(MainViewModel mainVm)
    {
        _mainVm = mainVm;
        LoadPlaylists();
        LoadSettings();
        LoadRecentCount();
        LoadXtreamAccounts();
    }

    private void LoadSettings()
    {
        var db = DatabaseService.Instance;
        EpgUrl = db.GetSetting("epg_url", "");
        DefaultVolume = int.TryParse(db.GetSetting("default_volume", "80"), out var vol) ? vol : 80;
        NetworkCaching = int.TryParse(db.GetSetting("network_caching", "3000"), out var cache) ? cache : 3000;
        AutoHealthCheck = StreamHealthService.IsRunning;
        AutoPlaylistRefresh = PlaylistAutoRefreshService.IsRunning;
        HealthStatus = StreamHealthService.IsRunning ? "Running" : "Stopped";
        RefreshStatus = PlaylistAutoRefreshService.IsRunning ? "Running" : "Stopped";
    }

    private void LoadRecentCount()
    {
        var recent = DatabaseService.Instance.GetRecentChannels(50);
        RecentChannelCount = recent.Count;
    }

    private void LoadPlaylists()
    {
        var db = DatabaseService.Instance;
        var playlists = db.GetAllPlaylists();
        Playlists = new ObservableCollection<PlaylistInfo>(playlists.Select(p => new PlaylistInfo
        {
            Id = p.Id,
            Name = p.Name,
            ChannelCount = p.ChannelCount,
            AddedAt = p.AddedAt,
            AutoRefresh = p.AutoRefresh,
            RefreshIntervalMinutes = p.RefreshIntervalMinutes,
            LastRefreshed = p.LastRefreshed
        }));
    }

    [RelayCommand]
    private async Task AddFromFgCode()
    {
        if (string.IsNullOrWhiteSpace(FgCode))
        {
            StatusMessage = "Please enter an FG Code.";
            return;
        }

        IsLoading = true;
        StatusMessage = null;

        var code = FgCode.Replace("http://", "").Replace("https://", "").Replace("fgcode.org/", "").Replace("fgcode.store/", "").Trim();
        var result = await FGCodeFetcher.FetchByCode(code);
        await ProcessResultAsync(result, string.IsNullOrWhiteSpace(PlaylistName) ? $"FG Code: {code}" : PlaylistName, $"fgcode:{code}");
    }

    [RelayCommand]
    private async Task AddFromUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(PlaylistUrl))
        {
            StatusMessage = "Please enter a URL.";
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        AddMode = 0;

        var result = await PlaylistService.FetchAndParseUrl(PlaylistUrl);
        await ProcessResultAsync(result, PlaylistName, PlaylistUrl);
    }

    [RelayCommand]
    private async Task AddFromFileAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select M3U File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("M3U Files") { Patterns = ["*.m3u", "*.m3u8", "*.txt"] }
            }
        });

        if (files.Count == 0) return;

        IsLoading = true;
        StatusMessage = null;
        AddMode = 1;

        var path = files[0].Path.LocalPath;
        var result = PlaylistService.ParseFile(path);
        await ProcessResultAsync(result, PlaylistName, path);
    }

    [RelayCommand]
    private async Task AddFromPasteAsync()
    {
        if (string.IsNullOrWhiteSpace(RawContent))
        {
            StatusMessage = "Please paste M3U content.";
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        AddMode = 2;

        var result = PlaylistService.ParseRawContent(RawContent);
        await ProcessResultAsync(result, PlaylistName, "pasted");
    }

    private async Task ProcessResultAsync((bool success, string? error, List<Channel>? channels) result, string name, string source)
    {
        if (!result.success)
        {
            StatusMessage = result.error;
            IsLoading = false;
            return;
        }

        var playlistName = string.IsNullOrWhiteSpace(name) ? $"Playlist {Playlists.Count + 1}" : name;
        await PlaylistService.SavePlaylist(playlistName, source, result.channels!, AutoPlaylistRefresh, 60);

        StatusMessage = $"Added {result.channels!.Count} channels from '{playlistName}'";
        LoadPlaylists();
        PlaylistName = string.Empty;
        PlaylistUrl = string.Empty;
        RawContent = string.Empty;
        IsLoading = false;
    }

    [RelayCommand]
    private void RemovePlaylist(int playlistId)
    {
        PlaylistService.RemovePlaylist(playlistId);
        LoadPlaylists();
    }

    [RelayCommand]
    private async Task RefreshPlaylistAsync(int playlistId)
    {
        var success = await PlaylistService.RefreshPlaylist(playlistId);
        StatusMessage = success ? "Playlist refreshed successfully" : "Failed to refresh playlist";
        LoadPlaylists();
    }

    [RelayCommand]
    private void TogglePlaylistAutoRefresh(int playlistId)
    {
        var db = DatabaseService.Instance;
        var playlist = db.GetPlaylist(playlistId);
        if (playlist == null) return;

        playlist.AutoRefresh = !playlist.AutoRefresh;
        db.UpdatePlaylist(playlist);
        LoadPlaylists();
    }

    [RelayCommand]
    private void ShowAddPlaylist()
    {
        ShowAddForm = !ShowAddForm;
    }

    [RelayCommand]
    private void ToggleLog()
    {
        ShowLog = !ShowLog;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var db = DatabaseService.Instance;
        db.SetSetting("epg_url", EpgUrl);
        db.SetSetting("default_volume", DefaultVolume.ToString());
        db.SetSetting("network_caching", NetworkCaching.ToString());
        StatusMessage = "Settings saved!";
    }

    [RelayCommand]
    private async Task RefreshEpgAsync()
    {
        if (string.IsNullOrEmpty(EpgUrl))
        {
            EpgStatus = "Please set an EPG URL first";
            return;
        }

        IsRefreshingEpg = true;
        EpgStatus = "Fetching EPG data...";

        var success = await EpgService.RefreshEpgForAllPlaylists(EpgUrl);
        EpgStatus = success ? "EPG refreshed successfully!" : "Failed to refresh EPG";
        IsRefreshingEpg = false;
    }

    [RelayCommand]
    private void ToggleHealthCheck()
    {
        if (StreamHealthService.IsRunning)
        {
            StreamHealthService.StopAutoCheck();
            HealthStatus = "Stopped";
            AutoHealthCheck = false;
        }
        else
        {
            StreamHealthService.StartAutoCheck(HealthCheckInterval);
            HealthStatus = $"Running (every {HealthCheckInterval}min)";
            AutoHealthCheck = true;
        }
    }

    [RelayCommand]
    private async Task RunHealthCheckNow()
    {
        HealthStatus = "Running check...";
        await StreamHealthService.CheckAllChannels();
        HealthStatus = "Check complete";
    }

    [RelayCommand]
    private void ClearRecentChannels()
    {
        DatabaseService.Instance.ClearRecentChannels();
        LoadRecentCount();
        StatusMessage = "Recent channels cleared";
    }

    [RelayCommand]
    private void TogglePlaylistRefreshService()
    {
        if (PlaylistAutoRefreshService.IsRunning)
        {
            PlaylistAutoRefreshService.Stop();
            RefreshStatus = "Stopped";
            AutoPlaylistRefresh = false;
        }
        else
        {
            PlaylistAutoRefreshService.Start();
            RefreshStatus = "Running";
            AutoPlaylistRefresh = true;
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow as TopLevel;
        return null;
    }

    private void LoadXtreamAccounts()
    {
        var db = DatabaseService.Instance;
        var accounts = db.GetAllXtreamAccounts();
        XtreamAccounts = new ObservableCollection<XtreamAccountInfo>(accounts.Select(a => new XtreamAccountInfo
        {
            Id = a.Id,
            ServerUrl = a.ServerUrl,
            Username = a.Username,
            ServerName = a.ServerName ?? "Xtream Server",
            LiveCount = a.LiveCount,
            VodCount = a.VodCount,
            AddedAt = a.AddedAt,
            LastSynced = a.LastSynced
        }));
    }

    [RelayCommand]
    private async Task LoginToXtreamAsync()
    {
        if (string.IsNullOrWhiteSpace(XtreamServerUrl))
        {
            XtreamStatus = "Please enter a server URL.";
            return;
        }
        if (string.IsNullOrWhiteSpace(XtreamUsername))
        {
            XtreamStatus = "Please enter a username.";
            return;
        }
        if (string.IsNullOrWhiteSpace(XtreamPassword))
        {
            XtreamStatus = "Please enter a password.";
            return;
        }

        IsXtreamSyncing = true;
        XtreamStatus = "Authenticating...";

        var authResult = await XtreamService.Authenticate(XtreamServerUrl, XtreamUsername, XtreamPassword);
        if (!authResult.success)
        {
            XtreamStatus = authResult.error;
            IsXtreamSyncing = false;
            return;
        }

        var authInfo = authResult.authInfo!;
        XtreamStatus = $"Connected to {authInfo.ServerName}. Syncing channels...";

        var account = new XtreamAccount
        {
            ServerUrl = authInfo.ServerUrl,
            Username = authInfo.Username,
            Password = authInfo.Password,
            ServerName = authInfo.ServerName,
            LiveCount = authInfo.LiveCount,
            VodCount = authInfo.VodCount,
            SeriesCount = authInfo.SeriesCount
        };

        var (live, vod) = await XtreamService.SyncAllChannels(account);
        var allChannels = new List<Channel>();
        allChannels.AddRange(live);
        if (IncludeVod) allChannels.AddRange(vod);

        if (allChannels.Count == 0)
        {
            XtreamStatus = "No channels found. Check your subscription.";
            IsXtreamSyncing = false;
            return;
        }

        var db = DatabaseService.Instance;
        db.InsertXtreamAccount(account);
        var accountId = account.Id;

        foreach (var ch in allChannels)
            ch.PlaylistId = $"xtream_{accountId}";

        db.BulkInsertChannels(allChannels);

        var playlist = new Playlist
        {
            Name = authInfo.ServerName ?? "Xtream",
            SourceUrl = $"xtream_{accountId}",
            ChannelCount = allChannels.Count,
            AutoRefresh = AutoPlaylistRefresh,
            RefreshIntervalMinutes = 60,
            LastRefreshed = DateTime.UtcNow
        };
        db.InsertPlaylist(playlist);

        XtreamStatus = $"Added {live.Count} live + {(IncludeVod ? vod.Count : 0)} VOD from {authInfo.ServerName}";
        LoadXtreamAccounts();
        LoadPlaylists();
        XtreamServerUrl = string.Empty;
        XtreamUsername = string.Empty;
        XtreamPassword = string.Empty;
        IsXtreamSyncing = false;
    }

    [RelayCommand]
    private async Task RefreshXtreamAccount(int accountId)
    {
        var db = DatabaseService.Instance;
        var account = db.GetAllXtreamAccounts().FirstOrDefault(a => a.Id == accountId);
        if (account == null) return;

        IsXtreamSyncing = true;
        XtreamStatus = $"Refreshing {account.ServerName}...";

        var (live, vod) = await XtreamService.SyncAllChannels(account);
        var allChannels = new List<Channel>();
        allChannels.AddRange(live);
        if (IncludeVod) allChannels.AddRange(vod);

        db.Execute("DELETE FROM Channel WHERE PlaylistId = ?", $"xtream_{accountId}");
        foreach (var ch in allChannels)
            ch.PlaylistId = $"xtream_{accountId}";
        db.BulkInsertChannels(allChannels);

        db.UpdateXtreamAccount(account);

        var playlist = db.GetAllPlaylists().FirstOrDefault(p => p.SourceUrl == $"xtream_{accountId}");
        if (playlist != null)
        {
            playlist.ChannelCount = allChannels.Count;
            playlist.LastRefreshed = DateTime.UtcNow;
            db.UpdatePlaylist(playlist);
        }

        XtreamStatus = $"Refreshed {account.ServerName}: {live.Count} live + {(IncludeVod ? vod.Count : 0)} VOD";
        LoadXtreamAccounts();
        LoadPlaylists();
        IsXtreamSyncing = false;
    }

    [RelayCommand]
    private void RemoveXtreamAccount(int accountId)
    {
        DatabaseService.Instance.DeleteXtreamAccount(accountId);
        LoadXtreamAccounts();
        LoadPlaylists();
        XtreamStatus = "Account removed";
    }

    [RelayCommand]
    private void ToggleXtreamForm()
    {
        ShowXtreamForm = !ShowXtreamForm;
    }
}

public class PlaylistInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "Untitled";
    public int ChannelCount { get; set; }
    public DateTime AddedAt { get; set; }
    public bool AutoRefresh { get; set; }
    public int RefreshIntervalMinutes { get; set; }
    public DateTime? LastRefreshed { get; set; }
}

public class XtreamAccountInfo
{
    public int Id { get; set; }
    public string ServerUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ServerName { get; set; } = "Xtream Server";
    public int LiveCount { get; set; }
    public int VodCount { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? LastSynced { get; set; }
}
