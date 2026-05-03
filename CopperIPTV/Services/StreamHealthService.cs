using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CopperIPTV.Models;

namespace CopperIPTV.Services;

public static class StreamHealthService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static CancellationTokenSource? _cts;
    private static bool _isRunning;

    public static bool IsRunning => _isRunning;

    public static async Task<int> CheckStreamHealth(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (response.IsSuccessStatusCode)
                return 100;
            return (int)(response.StatusCode switch
            {
                System.Net.HttpStatusCode.NotFound => 0,
                System.Net.HttpStatusCode.Forbidden => 20,
                System.Net.HttpStatusCode.ServiceUnavailable => 30,
                System.Net.HttpStatusCode.GatewayTimeout => 40,
                _ => 50
            });
        }
        catch
        {
            return 0;
        }
    }

    public static async Task CheckAllChannels()
    {
        _isRunning = true;
        var channels = DatabaseService.Instance.GetAllChannels();
        var db = DatabaseService.Instance;

        LogService.Info($"Health check started for {channels.Count} channels");

        foreach (var ch in channels)
        {
            var score = await CheckStreamHealth(ch.Url);
            db.UpdateChannelHealth(ch.Id, score - ch.HealthScore);
            LogService.Debug($"Health check: {ch.Name} = {score}%");

            if (!string.IsNullOrEmpty(ch.FallbackUrl) && score < 50)
            {
                var fallbackScore = await CheckStreamHealth(ch.FallbackUrl);
                if (fallbackScore > score)
                {
                    ch.Url = ch.FallbackUrl;
                    db.UpdateChannel(ch);
                    LogService.Info($"Switched {ch.Name} to fallback URL");
                }
            }
        }

        _isRunning = false;
        LogService.Info("Health check complete");
    }

    public static void StartAutoCheck(int intervalMinutes = 30)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await CheckAllChannels();
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), _cts.Token);
            }
        }, _cts.Token);

        LogService.Info($"Auto health check started (every {intervalMinutes}min)");
    }

    public static void StopAutoCheck()
    {
        _cts?.Cancel();
        _isRunning = false;
        LogService.Info("Auto health check stopped");
    }
}
