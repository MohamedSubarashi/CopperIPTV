using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CopperIPTV.Models;

namespace CopperIPTV.Services;

public static class FGCodeFetcher
{
    private static readonly HttpClient _httpClient;

    static FGCodeFetcher()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Linux; Android 12; SM-G991B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Mobile Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ar,en-US;q=0.9,en;q=0.8");
    }

    public static async Task<(bool success, string? error, List<Channel>? channels)> FetchByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, "FG Code is empty.", null);

        code = code.Trim();
        LogService.Info($"FGCodeFetcher: Fetching code '{code}'");

        var domains = new[] { "fgcode.org", "fgcode.store" };
        var schemes = new[] { "http", "https" };

        foreach (var domain in domains)
        {
            foreach (var scheme in schemes)
            {
                var baseUrl = $"{scheme}://{domain}";

                var endpoints = new[]
                {
                    $"{baseUrl}/link/api.php?code={code}&type=live",
                    $"{baseUrl}/link/api.php?code={code}",
                    $"{baseUrl}/api/playlist?code={code}",
                    $"{baseUrl}/api/m3u?code={code}",
                    $"{baseUrl}/api/get?code={code}",
                    $"{baseUrl}/playlist/{code}",
                    $"{baseUrl}/m3u/{code}",
                    $"{baseUrl}/get/{code}",
                    $"{baseUrl}/fetch/{code}",
                    $"{baseUrl}/code/{code}",
                    $"{baseUrl}/{code}"
                };

                foreach (var endpoint in endpoints)
                {
                    var result = await TryFetchEndpoint(endpoint, code, domain);
                    if (result.success)
                    {
                        LogService.Info($"FGCodeFetcher: SUCCESS via {endpoint}");
                        return result;
                    }
                }
            }
        }

        LogService.Error("FGCodeFetcher: All endpoints failed");
        return (false, "Failed to fetch FG Code. The server may be down or the code is invalid.", null);
    }

    private static async Task<(bool success, string? error, List<Channel>? channels)> TryFetchEndpoint(string url, string code, string domain)
    {
        try
        {
            LogService.Debug($"FGCodeFetcher: Trying {url}");
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                LogService.Debug($"FGCodeFetcher: {url} returned {(int)response.StatusCode}");
                return (false, null, null);
            }

            var content = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(content) || content.Length < 10)
            {
                LogService.Debug($"FGCodeFetcher: {url} returned empty response");
                return (false, null, null);
            }

            if (content.Contains("Fatal error") || content.Contains("Parse error") || content.Contains("bind_param"))
            {
                LogService.Debug($"FGCodeFetcher: {url} returned PHP error");
                return (false, null, null);
            }

            if (content.Contains("#EXTM3U") || content.Contains("#EXTINF"))
            {
                LogService.Info($"FGCodeFetcher: {url} returned M3U format");
                var parseResult = M3UParser.Parse(content);
                if (parseResult.Count > 0)
                    return (true, null, parseResult);
                return (false, "M3U parsed but no channels found", null);
            }

            var jsonResult = TryParseJsonResponse(content, code, domain);
            if (jsonResult.success)
            {
                LogService.Info($"FGCodeFetcher: {url} returned JSON with {jsonResult.channels!.Count} channels");
                return jsonResult;
            }

            LogService.Debug($"FGCodeFetcher: {url} returned unparseable content (length: {content.Length})");
            return (false, null, null);
        }
        catch (Exception ex)
        {
            LogService.Debug($"FGCodeFetcher: {url} failed: {ex.Message}");
            return (false, null, null);
        }
    }

    private static (bool success, string? error, List<Channel>? channels) TryParseJsonResponse(string content, string code, string domain)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var channels = new List<Channel>();

            if (root.TryGetProperty("categories", out var categories) &&
                root.TryGetProperty("streams", out var streams))
            {
                channels = ParseXtreamStyle(categories, streams, code, domain);
            }
            else if (root.TryGetProperty("data", out var data))
            {
                channels = ParseDataWrapper(data, code, domain);
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                channels = ParseArray(root, code, domain);
            }
            else if (root.TryGetProperty("channels", out var channelsProp))
            {
                channels = ParseArray(channelsProp, code, domain);
            }
            else if (root.TryGetProperty("live", out var liveProp))
            {
                channels = ParseArray(liveProp, code, domain);
            }
            else if (root.TryGetProperty("result", out var resultProp))
            {
                channels = ParseArray(resultProp, code, domain);
            }
            else if (root.TryGetProperty("response", out var responseProp))
            {
                channels = ParseArray(responseProp, code, domain);
            }

            if (channels.Count > 0)
                return (true, null, channels);

            return (false, "JSON parsed but no channels found", null);
        }
        catch (JsonException)
        {
            return (false, null, null);
        }
    }

    private static List<Channel> ParseXtreamStyle(JsonElement categories, JsonElement streams, string code, string domain)
    {
        var catMap = new Dictionary<string, string>();
        foreach (var cat in categories.EnumerateArray())
        {
            var id = GetProp(cat, "category_id") ?? GetProp(cat, "id") ?? "";
            var name = GetProp(cat, "category_name") ?? GetProp(cat, "name") ?? "Uncategorized";
            if (!string.IsNullOrEmpty(id))
                catMap[id] = name.Trim();
        }

        var channels = new List<Channel>();
        var baseUrl = $"http://{domain}";

        foreach (var stream in streams.EnumerateArray())
        {
            var name = GetProp(stream, "name") ?? GetProp(stream, "stream_display_name") ?? "Unknown";
            var streamId = GetProp(stream, "stream_id") ?? GetProp(stream, "id") ?? "";
            var categoryId = GetProp(stream, "category_id") ?? "";
            var logo = GetProp(stream, "stream_icon") ?? GetProp(stream, "logo") ?? GetProp(stream, "tvg-logo") ?? "";
            var streamType = GetProp(stream, "stream_type") ?? "live";
            var container = GetProp(stream, "container_extension") ?? "ts";
            var epgId = GetProp(stream, "epg_channel_id") ?? GetProp(stream, "tvg-id") ?? "";
            var country = GetProp(stream, "country") ?? "";
            var language = GetProp(stream, "language") ?? "";

            var url = GetProp(stream, "url") ?? GetProp(stream, "stream_url") ?? GetProp(stream, "direct_source") ?? "";
            if (string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(streamId))
            {
                url = $"{baseUrl}/live/{code}//{streamId}.{container}";
            }

            if (string.IsNullOrEmpty(url)) continue;

            var group = catMap.TryGetValue(categoryId, out var groupName) ? groupName.Trim() : "Uncategorized";

            channels.Add(new Channel
            {
                Id = GenerateId(url, name),
                Name = name,
                Url = url,
                Logo = logo,
                Group = group,
                TvgId = epgId,
                TvgName = name,
                Language = language,
                Country = country,
                PlaylistId = "fgcode_" + code
            });
        }

        return channels;
    }

    private static List<Channel> ParseDataWrapper(JsonElement data, string code, string domain)
    {
        if (data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty("streams", out var streams))
                return ParseArray(streams, code, domain);
            if (data.TryGetProperty("channels", out var channels))
                return ParseArray(channels, code, domain);
            if (data.TryGetProperty("live", out var live))
                return ParseArray(live, code, domain);
        }
        return ParseArray(data, code, domain);
    }

    private static List<Channel> ParseArray(JsonElement array, string code, string domain)
    {
        var channels = new List<Channel>();
        var baseUrl = $"http://{domain}";

        foreach (var item in array.EnumerateArray())
        {
            var name = GetProp(item, "name") ?? GetProp(item, "title") ?? GetProp(item, "channel_name") ?? "Unknown";
            var url = GetProp(item, "url") ?? GetProp(item, "stream_url") ?? GetProp(item, "link") ?? GetProp(item, "source") ?? "";
            var logo = GetProp(item, "logo") ?? GetProp(item, "icon") ?? GetProp(item, "image") ?? GetProp(item, "tvg-logo") ?? "";
            var groupVal = GetProp(item, "group") ?? GetProp(item, "category") ?? GetProp(item, "group-title");
            var group = !string.IsNullOrWhiteSpace(groupVal) ? groupVal.Trim() : "Uncategorized";
            var tvgId = GetProp(item, "tvg-id") ?? GetProp(item, "epg_id") ?? "";
            var country = GetProp(item, "country") ?? "";
            var language = GetProp(item, "language") ?? "";

            if (string.IsNullOrEmpty(url)) continue;

            if (!url.StartsWith("http") && !string.IsNullOrEmpty(url))
            {
                url = $"{baseUrl}{(url.StartsWith("/") ? "" : "/")}{url}";
            }

            channels.Add(new Channel
            {
                Id = GenerateId(url, name),
                Name = name,
                Url = url,
                Logo = logo,
                Group = group,
                TvgId = tvgId,
                TvgName = name,
                Language = language,
                Country = country,
                PlaylistId = "fgcode_" + code
            });
        }

        return channels;
    }

    private static string? GetProp(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind != JsonValueKind.Null)
        {
            return prop.ToString();
        }
        return null;
    }

    private static string GenerateId(string url, string name)
    {
        var combined = url + name;
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash).Substring(0, 16).Replace("+", "").Replace("/", "").Replace("=", "");
    }
}
