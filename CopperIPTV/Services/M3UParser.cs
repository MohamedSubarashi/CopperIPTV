using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CopperIPTV.Services;

public static class M3UParser
{
    public static List<Models.Channel> Parse(string content)
    {
        var channels = new List<Models.Channel>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Models.Channel? currentChannel = null;
        string? fallbackUrl = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("#EXTINF:"))
            {
                if (currentChannel != null && !string.IsNullOrEmpty(currentChannel.Url))
                {
                    if (!string.IsNullOrEmpty(fallbackUrl))
                        currentChannel.FallbackUrl = fallbackUrl;
                    channels.Add(currentChannel);
                    fallbackUrl = null;
                }
                currentChannel = ParseExtInf(trimmed);
            }
            else if (trimmed.StartsWith("#EXTVLCOPT:") || trimmed.StartsWith("#KODIPROP:"))
            {
                continue;
            }
            else if (trimmed.StartsWith("#"))
            {
                continue;
            }
            else if (currentChannel != null && IsStreamUrl(trimmed))
            {
                if (string.IsNullOrEmpty(currentChannel.Url))
                {
                    currentChannel.Url = trimmed;
                    currentChannel.Id = GenerateId(trimmed, currentChannel.Name);
                }
                else if (string.IsNullOrEmpty(fallbackUrl))
                {
                    fallbackUrl = trimmed;
                }
            }
        }

        if (currentChannel != null && !string.IsNullOrEmpty(currentChannel.Url))
        {
            if (!string.IsNullOrEmpty(fallbackUrl))
                currentChannel.FallbackUrl = fallbackUrl;
            channels.Add(currentChannel);
        }

        LogService.Info($"M3UParser: Parsed {channels.Count} channels");
        return channels;
    }

    private static bool IsStreamUrl(string line)
    {
        return line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("udp://", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("rtp://", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("mms://", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("mmsh://", StringComparison.OrdinalIgnoreCase);
    }

    private static Models.Channel ParseExtInf(string line)
    {
        var channel = new Models.Channel();
        var infoPart = line.Substring(8);

        var commaIndex = infoPart.LastIndexOf(',');
        if (commaIndex >= 0)
        {
            channel.Name = infoPart.Substring(commaIndex + 1).Trim();
            var attrs = infoPart.Substring(0, commaIndex);

            var tvgId = ExtractAttr(attrs, "tvg-id");
            if (!string.IsNullOrEmpty(tvgId)) channel.TvgId = tvgId;

            var tvgName = ExtractAttr(attrs, "tvg-name");
            if (!string.IsNullOrEmpty(tvgName)) channel.TvgName = tvgName;

            var logo = ExtractAttr(attrs, "tvg-logo");
            if (!string.IsNullOrEmpty(logo)) channel.Logo = logo;

            var group = ExtractAttr(attrs, "group-title");
            if (!string.IsNullOrWhiteSpace(group)) channel.Group = group.Trim();

            var language = ExtractAttr(attrs, "tvg-language");
            if (!string.IsNullOrEmpty(language)) channel.Language = language;

            var country = ExtractAttr(attrs, "tvg-country");
            if (!string.IsNullOrEmpty(country)) channel.Country = country;

            var url = ExtractAttr(attrs, "url");
            if (!string.IsNullOrEmpty(url)) channel.Url = url;
        }
        else
        {
            channel.Name = infoPart.Trim();
        }

        return channel;
    }

    private static string ExtractAttr(string attrs, string name)
    {
        var pattern = $"{name}=\"([^\"]*)\"";
        var match = Regex.Match(attrs, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string GenerateId(string url, string name)
    {
        var combined = url + name;
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash).Substring(0, 16).Replace("+", "").Replace("/", "").Replace("=", "");
    }
}
