using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CopperIPTV.Models;

namespace CopperIPTV.Services;

public static class XtreamService
{
    private static readonly HttpClient _httpClient;

    // Credentials can contain reserved characters (&, +, /, spaces) that break
    // query strings and path segments if inserted raw.
    private static string Esc(string value) => Uri.EscapeDataString(value);

    static XtreamService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public static async Task<(bool success, string error, XtreamAuthInfo? authInfo)> Authenticate(string serverUrl, string username, string password)
    {
        var baseUrl = NormalizeServerUrl(serverUrl);
        var apiUrl = $"{baseUrl}/player_api.php?username={Esc(username)}&password={Esc(password)}";

        LogService.Info($"Xtream: Authenticating to {baseUrl}");

        try
        {
            var response = await _httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var userExists = root.TryGetProperty("user_info", out var userInfo);
            var serverExists = root.TryGetProperty("server_info", out var serverInfo);

            if (!userExists || !serverExists)
            {
                return (false, "Invalid server response. Not an Xtream Codes server.", null);
            }

            var authStatus = GetProp(userInfo, "auth") ?? "";
            if (authStatus != "1")
            {
                return (false, "Authentication failed. Check your username and password.", null);
            }

            var status = GetProp(userInfo, "status") ?? "";
            if (status == "Active" || status == "active" || status == "1")
            {
                var authInfo = new XtreamAuthInfo
                {
                    ServerUrl = baseUrl,
                    Username = username,
                    Password = password,
                    ServerName = GetProp(serverInfo, "server_name") ?? "Xtream Server",
                    LiveCount = ParseInt(GetProp(userInfo, "live_cons"), 0),
                    VodCount = ParseInt(GetProp(userInfo, "vod_cons"), 0),
                    SeriesCount = ParseInt(GetProp(userInfo, "series_cons"), 0),
                    MaxConnections = ParseInt(GetProp(userInfo, "max_connections"), 1),
                    ExpDate = GetProp(userInfo, "exp_date"),
                    AllowedOutputFormats = GetProp(serverInfo, "allowed_output_formats") ?? ""
                };

                var activeCons = ParseInt(GetProp(userInfo, "active_cons"), 0);
                authInfo.ActiveConnections = activeCons;

                LogService.Info($"Xtream: Auth success - {authInfo.ServerName}");
                return (true, string.Empty, authInfo);
            }

            return (false, "Account is not active or has expired.", null);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Xtream: Authentication failed");
            return (false, $"Authentication failed: {ex.Message}", null);
        }
    }

    public static async Task<List<Channel>> GetLiveChannels(XtreamAccount account)
    {
        var channels = new List<Channel>();
        var apiUrl = $"{account.ServerUrl}/player_api.php?username={Esc(account.Username)}&password={Esc(account.Password)}&action=get_live_streams";

        LogService.Info($"Xtream: Fetching live streams from {account.ServerUrl}");

        try
        {
            var response = await _httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var name = GetProp(item, "name") ?? GetProp(item, "stream_display_name") ?? "Unknown";
                var streamId = GetProp(item, "stream_id") ?? "";
                var categoryId = GetProp(item, "category_id") ?? "";
                var logo = GetProp(item, "stream_icon") ?? GetProp(item, "logo") ?? "";
                var epgId = GetProp(item, "epg_channel_id") ?? "";
                var country = GetProp(item, "country") ?? "";
                var language = GetProp(item, "language") ?? "";
                var rating = GetProp(item, "rating") ?? "";

                var url = BuildStreamUrl(account, streamId, "live", "ts");

                if (string.IsNullOrEmpty(streamId)) continue;

                channels.Add(new Channel
                {
                    Id = $"xtream_{account.Id}_live_{streamId}",
                    Name = name,
                    Url = url,
                    Logo = logo,
                    Group = "Live TV",
                    TvgId = epgId,
                    TvgName = name,
                    Language = language,
                    Country = country,
                    PlaylistId = $"xtream_{account.Id}"
                });
            }

            LogService.Info($"Xtream: Fetched {channels.Count} live channels");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Xtream: Failed to fetch live channels");
        }

        return channels;
    }

    public static async Task<List<Channel>> GetLiveChannelsWithCategories(XtreamAccount account)
    {
        var channels = new List<Channel>();
        var catUrl = $"{account.ServerUrl}/player_api.php?username={Esc(account.Username)}&password={Esc(account.Password)}&action=get_live_categories";

        var catMap = new Dictionary<string, string>();
        try
        {
            var catResponse = await _httpClient.GetStringAsync(catUrl);
            using var catDoc = JsonDocument.Parse(catResponse);
            foreach (var cat in catDoc.RootElement.EnumerateArray())
            {
                var catId = GetProp(cat, "category_id") ?? "";
                var catName = GetProp(cat, "category_name") ?? "Uncategorized";
                if (!string.IsNullOrEmpty(catId))
                    catMap[catId] = catName;
            }
        }
        catch { }

        var apiUrl = $"{account.ServerUrl}/player_api.php?username={Esc(account.Username)}&password={Esc(account.Password)}&action=get_live_streams";

        LogService.Info($"Xtream: Fetching live streams with categories");

        try
        {
            var response = await _httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var name = GetProp(item, "name") ?? GetProp(item, "stream_display_name") ?? "Unknown";
                var streamId = GetProp(item, "stream_id") ?? "";
                var categoryId = GetProp(item, "category_id") ?? "";
                var logo = GetProp(item, "stream_icon") ?? GetProp(item, "logo") ?? "";
                var epgId = GetProp(item, "epg_channel_id") ?? "";
                var country = GetProp(item, "country") ?? "";
                var language = GetProp(item, "language") ?? "";

                var group = catMap.TryGetValue(categoryId, out var groupName) ? groupName : "Live TV";
                var url = BuildStreamUrl(account, streamId, "live", "ts");

                if (string.IsNullOrEmpty(streamId)) continue;

                channels.Add(new Channel
                {
                    Id = $"xtream_{account.Id}_live_{streamId}",
                    Name = name,
                    Url = url,
                    Logo = logo,
                    Group = group,
                    TvgId = epgId,
                    TvgName = name,
                    Language = language,
                    Country = country,
                    PlaylistId = $"xtream_{account.Id}"
                });
            }

            LogService.Info($"Xtream: Fetched {channels.Count} live channels with categories");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Xtream: Failed to fetch live channels");
        }

        return channels;
    }

    public static async Task<List<Channel>> GetVodChannels(XtreamAccount account)
    {
        var channels = new List<Channel>();
        var catUrl = $"{account.ServerUrl}/player_api.php?username={Esc(account.Username)}&password={Esc(account.Password)}&action=get_vod_categories";

        var catMap = new Dictionary<string, string>();
        try
        {
            var catResponse = await _httpClient.GetStringAsync(catUrl);
            using var catDoc = JsonDocument.Parse(catResponse);
            foreach (var cat in catDoc.RootElement.EnumerateArray())
            {
                var catId = GetProp(cat, "category_id") ?? "";
                var catName = GetProp(cat, "category_name") ?? "VOD";
                if (!string.IsNullOrEmpty(catId))
                    catMap[catId] = catName;
            }
        }
        catch { }

        var apiUrl = $"{account.ServerUrl}/player_api.php?username={Esc(account.Username)}&password={Esc(account.Password)}&action=get_vod_streams";

        LogService.Info($"Xtream: Fetching VOD streams");

        try
        {
            var response = await _httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var name = GetProp(item, "name") ?? "Unknown";
                var streamId = GetProp(item, "stream_id") ?? "";
                var categoryId = GetProp(item, "category_id") ?? "";
                var logo = GetProp(item, "cover") ?? GetProp(item, "logo") ?? "";
                var rating = GetProp(item, "rating") ?? "";

                var container = GetProp(item, "container_extension") ?? "mp4";
                var group = catMap.TryGetValue(categoryId, out var groupName) ? groupName : "VOD";
                var url = BuildStreamUrl(account, streamId, "movie", container);

                if (string.IsNullOrEmpty(streamId)) continue;

                channels.Add(new Channel
                {
                    Id = $"xtream_{account.Id}_vod_{streamId}",
                    Name = name,
                    Url = url,
                    Logo = logo,
                    Group = group,
                    TvgName = name,
                    PlaylistId = $"xtream_{account.Id}"
                });
            }

            LogService.Info($"Xtream: Fetched {channels.Count} VOD streams");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Xtream: Failed to fetch VOD streams");
        }

        return channels;
    }

    public static async Task<(List<Channel> live, List<Channel> vod)> SyncAllChannels(XtreamAccount account)
    {
        var live = await GetLiveChannelsWithCategories(account);
        var vod = await GetVodChannels(account);

        account.LastSynced = DateTime.UtcNow;
        account.LiveCount = live.Count;
        account.VodCount = vod.Count;

        LogService.Info($"Xtream: Sync complete - {live.Count} live, {vod.Count} VOD");
        return (live, vod);
    }

    public static string BuildStreamUrl(XtreamAccount account, string streamId, string type, string extension)
    {
        return $"{account.ServerUrl}/{type}/{Esc(account.Username)}/{Esc(account.Password)}/{streamId}.{extension}";
    }

    private static string NormalizeServerUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "http://" + url;
        return url;
    }

    private static string? GetProp(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind != JsonValueKind.Null)
            return prop.ToString();
        return null;
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}

public class XtreamAuthInfo
{
    public string ServerUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public int LiveCount { get; set; }
    public int VodCount { get; set; }
    public int SeriesCount { get; set; }
    public int MaxConnections { get; set; }
    public int ActiveConnections { get; set; }
    public string? ExpDate { get; set; }
    public string AllowedOutputFormats { get; set; } = string.Empty;
}
