using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using CopperIPTV.Models;

namespace CopperIPTV.Services;

public static class EpgService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<List<EpgProgram>> FetchEpg(string url, string channelId)
    {
        var programs = new List<EpgProgram>();
        try
        {
            LogService.Info($"Fetching EPG from: {url}");
            var content = await _httpClient.GetStringAsync(url);
            programs = ParseXmlTv(content, channelId);
            LogService.Info($"Parsed {programs.Count} EPG programs for channel {channelId}");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to fetch EPG");
        }
        return programs;
    }

    public static async Task<List<EpgProgram>> FetchAllEpg(string url, Dictionary<string, string> tvgIdMap)
    {
        var allPrograms = new List<EpgProgram>();
        try
        {
            LogService.Info($"Fetching full EPG guide from: {url}");
            var content = await _httpClient.GetStringAsync(url);

            using var reader = new StringReader(content);
            using var xml = XmlReader.Create(reader, new XmlReaderSettings { Async = true, IgnoreWhitespace = true });

            string? currentChannelId = null;
            string title = "", desc = "", category = "";
            DateTime start = DateTime.MinValue, stop = DateTime.MinValue;
            bool inProgram = false;

            while (await xml.ReadAsync())
            {
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.Name == "programme")
                    {
                        inProgram = true;
                        currentChannelId = xml.GetAttribute("channel");
                        var startStr = xml.GetAttribute("start");
                        var stopStr = xml.GetAttribute("stop");
                        start = ParseXmlTvDate(startStr);
                        stop = ParseXmlTvDate(stopStr);
                        title = ""; desc = ""; category = "";
                    }
                    else if (inProgram && xml.Name == "title")
                    {
                        title = await xml.ReadElementContentAsStringAsync();
                    }
                    else if (inProgram && xml.Name == "desc")
                    {
                        desc = await xml.ReadElementContentAsStringAsync();
                    }
                    else if (inProgram && xml.Name == "category")
                    {
                        category = await xml.ReadElementContentAsStringAsync();
                    }
                }
                else if (xml.NodeType == XmlNodeType.EndElement && xml.Name == "programme" && inProgram)
                {
                    inProgram = false;
                    if (!string.IsNullOrEmpty(currentChannelId) && tvgIdMap.TryGetValue(currentChannelId, out var dbChannelId))
                    {
                        allPrograms.Add(new EpgProgram
                        {
                            ChannelId = dbChannelId,
                            Title = title,
                            Description = desc,
                            Start = start,
                            Stop = stop,
                            Category = category
                        });
                    }
                }
            }

            LogService.Info($"Parsed {allPrograms.Count} total EPG programs");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to fetch full EPG");
        }
        return allPrograms;
    }

    private static List<EpgProgram> ParseXmlTv(string xmlContent, string channelId)
    {
        var programs = new List<EpgProgram>();
        try
        {
            using var reader = new StringReader(xmlContent);
            using var xml = XmlReader.Create(reader, new XmlReaderSettings { Async = true });

            string title = "", desc = "", category = "";
            DateTime start = DateTime.MinValue, stop = DateTime.MinValue;
            bool inProgram = false;

            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.Element && xml.Name == "programme")
                {
                    inProgram = true;
                    start = ParseXmlTvDate(xml.GetAttribute("start"));
                    stop = ParseXmlTvDate(xml.GetAttribute("stop"));
                    title = ""; desc = ""; category = "";
                }
                else if (inProgram && xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.Name == "title") title = xml.ReadElementContentAsString();
                    else if (xml.Name == "desc") desc = xml.ReadElementContentAsString();
                    else if (xml.Name == "category") category = xml.ReadElementContentAsString();
                }
                else if (xml.NodeType == XmlNodeType.EndElement && xml.Name == "programme")
                {
                    inProgram = false;
                    programs.Add(new EpgProgram
                    {
                        ChannelId = channelId,
                        Title = title,
                        Description = desc,
                        Start = start,
                        Stop = stop,
                        Category = category
                    });
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to parse XMLTV");
        }
        return programs;
    }

    private static DateTime ParseXmlTvDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr) || dateStr.Length < 14) return DateTime.MinValue;
        try
        {
            var year = int.Parse(dateStr.Substring(0, 4));
            var month = int.Parse(dateStr.Substring(4, 2));
            var day = int.Parse(dateStr.Substring(6, 2));
            var hour = int.Parse(dateStr.Substring(8, 2));
            var minute = int.Parse(dateStr.Substring(10, 2));
            var second = int.Parse(dateStr.Substring(12, 2));
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    public static async Task<bool> RefreshEpgForAllPlaylists(string epgUrl)
    {
        if (string.IsNullOrEmpty(epgUrl)) return false;

        var channels = DatabaseService.Instance.GetAllChannels();
        var tvgIdMap = channels.Where(c => !string.IsNullOrEmpty(c.TvgId))
            .ToDictionary(c => c.TvgId, c => c.Id);

        var programs = await FetchAllEpg(epgUrl, tvgIdMap);
        if (programs.Count == 0) return false;

        DatabaseService.Instance.SaveEpgPrograms(programs);
        DatabaseService.Instance.ClearOldEpg(DateTime.UtcNow.AddDays(-2));
        return true;
    }
}
