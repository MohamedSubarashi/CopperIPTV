using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CopperIPTV.Models;

namespace CopperIPTV.Services;

public static class PlaylistService
{
    private static readonly HttpClient _httpClient;
    private static readonly string[] _errorMarkers = {
        "Fatal error", "Parse error", "Warning:", "Exception:",
        "Uncaught Error", "Internal Server Error", "500 Internal",
        "<!DOCTYPE HTML", "<html>", "<h1>500</h1>", "<h1>403</h1>",
        "connection failed", "database error", "bind_param"
    };

    static PlaylistService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
    }

    private static string? DetectServerError(string content)
    {
        var lower = content.ToLowerInvariant();
        foreach (var marker in _errorMarkers)
        {
            if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                var snippet = content.Substring(0, Math.Min(300, content.Length));
                var cleanSnippet = Regex.Replace(snippet, "<[^>]+>", "").Trim();
                return $"Server returned an error page: {cleanSnippet}";
            }
        }
        return null;
    }

    public static async Task<(bool success, string? error, List<Channel>? channels)> FetchAndParseUrl(string url)
    {
        var normalized = url.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            LogService.Warning("FetchAndParseUrl: URL is empty");
            return (false, "URL is empty.", null);
        }

        LogService.Info($"FetchAndParseUrl starting with: {normalized}");
        normalized = NormalizeUrl(normalized);
        LogService.Info($"Normalized URL: {normalized}");

        var lastError = string.Empty;

        var urlsToTry = new List<string> { normalized };
        if (normalized.StartsWith("https://"))
        {
            var httpUrl = normalized.Replace("https://", "http://");
            urlsToTry.Add(httpUrl);
            LogService.Debug($"Will also try HTTP fallback: {httpUrl}");
        }
        else if (normalized.StartsWith("http://"))
        {
            var httpsUrl = normalized.Replace("http://", "https://");
            urlsToTry.Add(httpsUrl);
            LogService.Debug($"Will also try HTTPS alternative: {httpsUrl}");
        }

        foreach (var tryUrl in urlsToTry)
        {
            LogService.Info($"--- Trying URL: {tryUrl} ---");
            try
            {
                LogService.Debug("Sending HTTP GET request...");
                var response = await _httpClient.GetAsync(tryUrl);
                LogService.Info($"Response: {(int)response.StatusCode} {response.StatusCode}");

                var contentType = response.Content.Headers.ContentType?.ToString() ?? "unknown";
                LogService.Debug($"Content-Type: {contentType}");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                LogService.Info($"Received {content.Length} bytes");

                if (content.Length < 10)
                {
                    lastError = $"URL returned empty response ({content.Length} bytes). Content: {content}";
                    LogService.Warning(lastError);
                    continue;
                }

                var preview = content.Substring(0, Math.Min(200, content.Length));
                LogService.Debug($"Content preview: {preview}");

                var serverError = DetectServerError(content);
                if (serverError != null)
                {
                    lastError = $"Playlist server is down. {serverError}";
                    LogService.Error(lastError);
                    break;
                }

                var parseResult = ParseContent(content);
                if (parseResult.success)
                {
                    LogService.Info($"SUCCESS: Parsed {parseResult.channels!.Count} channels from {tryUrl}");
                    return parseResult;
                }

                LogService.Warning($"Standard parse failed: {parseResult.error}");

                var xtreamResult = await TryParseXtreamCodes(content, tryUrl);
                if (xtreamResult.success)
                {
                    LogService.Info($"SUCCESS: Xtream Codes parsed {xtreamResult.channels!.Count} channels from {tryUrl}");
                    return xtreamResult;
                }

                lastError = $"URL returned content but no valid M3U playlist. Content-Type: {contentType}. Preview: {preview}";
                LogService.Warning(lastError);
            }
            catch (HttpRequestException ex)
            {
                lastError = $"HTTP error ({tryUrl}): {ex.Message}";
                LogService.Error(ex, lastError);
            }
            catch (TaskCanceledException ex)
            {
                lastError = $"Timeout ({tryUrl}): Server did not respond within 60 seconds";
                LogService.Error(ex, lastError);
            }
            catch (Exception ex)
            {
                lastError = $"Unexpected error ({tryUrl}): {ex.GetType().Name}: {ex.Message}";
                LogService.Error(ex, lastError);
            }
        }

        var xtreamUrl = TryBuildXtreamUrl(normalized);
        if (!string.IsNullOrEmpty(xtreamUrl))
        {
            LogService.Info($"Trying Xtream Codes auto-build URL: {xtreamUrl}");
            try
            {
                var response = await _httpClient.GetAsync(xtreamUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                LogService.Debug($"Xtream response length: {content.Length}");

                var serverError = DetectServerError(content);
                if (serverError == null)
                {
                    var result = ParseContent(content);
                    if (result.success)
                    {
                        LogService.Info($"SUCCESS: Xtream auto-build parsed {result.channels!.Count} channels");
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Xtream auto-build URL failed");
            }
        }

        LogService.Error($"All fetch attempts failed. Last error: {lastError}");
        return (false, $"Failed to fetch playlist. Last error: {lastError}", null);
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim().TrimEnd('/');

        if (url.StartsWith("http://") || url.StartsWith("https://"))
            return url;

        LogService.Debug($"URL has no scheme, prepending http://");
        var parts = url.Split(new[] { ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return "http://" + url;

        return "http://" + url;
    }

    private static string? TryBuildXtreamUrl(string url)
    {
        try
        {
            var uri = new Uri(url.StartsWith("http") ? url : "http://" + url);
            var pathParts = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (pathParts.Length == 1)
            {
                var usernameOrCode = pathParts[0];
                var baseUrl = $"{uri.Scheme}://{uri.Authority}/get.php?username={Uri.EscapeDataString(usernameOrCode)}&password=&type=m3u_plus&output=ts";
                LogService.Debug($"Built Xtream URL: {baseUrl}");
                return baseUrl;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Could not build Xtream URL");
        }

        return null;
    }

    private static async Task<(bool success, string? error, List<Channel>? channels)> TryParseXtreamCodes(string content, string baseUrl)
    {
        try
        {
            if (content.Contains("user_info") && content.Contains("server_info"))
            {
                LogService.Info("Detected Xtream Codes JSON response");
                var uri = new Uri(baseUrl);
                var pathParts = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string username = string.Empty, password = string.Empty;

                if (pathParts.Length >= 2)
                {
                    username = pathParts[pathParts.Length - 2];
                    password = pathParts[pathParts.Length - 1];
                }

                var usernameMatch = Regex.Match(content, "\"username\":\"([^\"]+)\"");
                var passwordMatch = Regex.Match(content, "\"password\":\"([^\"]+)\"");
                if (usernameMatch.Success) username = usernameMatch.Groups[1].Value;
                if (passwordMatch.Success) password = passwordMatch.Groups[1].Value;

                LogService.Info($"Extracted Xtream credentials - user: {username}");

                var m3uUrl = $"{uri.Scheme}://{uri.Authority}/get.php?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}&type=m3u_plus&output=ts";
                LogService.Info($"Generated M3U URL: {m3uUrl}");

                var m3uContent = await _httpClient.GetStringAsync(m3uUrl);
                return ParseContent(m3uContent);
            }
        }
        catch (Exception ex)
        {
            LogService.Debug($"Xtream Codes parsing failed: {ex.Message}");
        }

        return (false, null, null);
    }

    public static (bool success, string? error, List<Channel>? channels) ParseFile(string filePath)
    {
        LogService.Info($"ParseFile: {filePath}");
        try
        {
            var content = File.ReadAllText(filePath);
            LogService.Info($"File read: {content.Length} bytes");
            return ParseContent(content);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "ParseFile failed");
            return (false, $"Failed to read file: {ex.Message}", null);
        }
    }

    public static (bool success, string? error, List<Channel>? channels) ParseRawContent(string content)
    {
        LogService.Info($"ParseRawContent: {content.Length} bytes");
        return ParseContent(content);
    }

    private static (bool success, string? error, List<Channel>? channels) ParseContent(string content)
    {
        if (!content.Contains("#EXT"))
        {
            LogService.Warning("ParseContent: No #EXT markers found in content");
            return (false, "Invalid M3U content. No #EXT markers found.", null);
        }

        var channels = M3UParser.Parse(content);
        if (channels.Count == 0)
        {
            LogService.Warning("ParseContent: No channels parsed from M3U content");
            return (false, "No channels found in playlist.", null);
        }

        LogService.Info($"ParseContent: Successfully parsed {channels.Count} channels");
        return (true, null, channels);
    }

    public static async Task SavePlaylist(string name, string sourceUrl, List<Channel> channels, bool autoRefresh = false, int refreshInterval = 60)
    {
        LogService.Info($"SavePlaylist: '{name}' with {channels.Count} channels from {sourceUrl}");
        var playlist = new Playlist
        {
            Name = name,
            SourceUrl = sourceUrl,
            ChannelCount = channels.Count,
            AutoRefresh = autoRefresh,
            RefreshIntervalMinutes = refreshInterval
        };

        await Task.Run(() =>
        {
            var db = DatabaseService.Instance;
            db.InsertPlaylist(playlist);
            var playlistId = playlist.Id;
            LogService.Debug($"Playlist saved with ID: {playlistId}");

            foreach (var ch in channels)
            {
                ch.PlaylistId = playlistId.ToString();
            }

            db.BulkInsertChannels(channels);
            LogService.Info($"All {channels.Count} channels saved to database");
        });
    }

    public static async Task<bool> RefreshPlaylist(int playlistId)
    {
        var db = DatabaseService.Instance;
        var playlist = db.GetPlaylist(playlistId);
        if (playlist == null || string.IsNullOrEmpty(playlist.SourceUrl)) return false;

        LogService.Info($"Refreshing playlist: {playlist.Name}");

        (bool success, string? error, List<Channel>? channels) result;
        if (playlist.SourceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            playlist.SourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            result = await FetchAndParseUrl(playlist.SourceUrl);
        }
        else if (File.Exists(playlist.SourceUrl))
        {
            result = ParseFile(playlist.SourceUrl);
        }
        else
        {
            LogService.Warning($"Cannot refresh playlist '{playlist.Name}': Source is not a URL and local file does not exist ({playlist.SourceUrl})");
            return false;
        }

        if (!result.success || result.channels == null) return false;

        await Task.Run(() =>
        {
            db.DeleteChannelsByPlaylistId(playlistId);
            foreach (var ch in result.channels)
            {
                ch.PlaylistId = playlistId.ToString();
            }
            db.BulkInsertChannels(result.channels);

            playlist.ChannelCount = result.channels.Count;
            playlist.LastRefreshed = DateTime.UtcNow;
            db.UpdatePlaylist(playlist);
        });

        LogService.Info($"Refreshed {playlist.Name}: {result.channels.Count} channels");
        return true;
    }

    public static void RemovePlaylist(int playlistId)
    {
        LogService.Info($"RemovePlaylist: ID={playlistId}");
        var db = DatabaseService.Instance;
        db.DeleteChannelsByPlaylistId(playlistId);
        db.DeletePlaylist(playlistId);
    }
}
