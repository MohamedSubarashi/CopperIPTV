using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CopperIPTV.Models;

namespace CopperIPTV.Services;

public static class PlaylistAutoRefreshService
{
    private static CancellationTokenSource? _cts;
    private static bool _isRunning;

    public static bool IsRunning => _isRunning;

    public static void Start()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _isRunning = true;

        Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    await CheckAndRefreshPlaylists();
                    await Task.Delay(TimeSpan.FromMinutes(5), _cts.Token);
                }
            }
            finally
            {
                _isRunning = false;
            }
        }, _cts.Token);

        LogService.Info("Playlist auto-refresh service started");
    }

    public static void Stop()
    {
        _cts?.Cancel();
        _isRunning = false;
        LogService.Info("Playlist auto-refresh service stopped");
    }

    private static async Task CheckAndRefreshPlaylists()
    {
        var db = DatabaseService.Instance;
        var playlists = db.GetAllPlaylists().Where(p => p.AutoRefresh).ToList();

        foreach (var playlist in playlists)
        {
            if (playlist.LastRefreshed.HasValue &&
                DateTime.UtcNow - playlist.LastRefreshed.Value < TimeSpan.FromMinutes(playlist.RefreshIntervalMinutes))
                continue;

            LogService.Info($"Auto-refreshing playlist: {playlist.Name}");

            var success = await PlaylistService.RefreshPlaylist(playlist.Id);
            if (success)
            {
                LogService.Info($"Auto-refresh succeeded for {playlist.Name}");
            }
            else
            {
                LogService.Warning($"Auto-refresh failed for {playlist.Name}");
            }
        }
    }
}
