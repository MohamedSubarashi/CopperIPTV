using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;
using CopperIPTV.Models;

namespace CopperIPTV.Services;

public class DatabaseService
{
    private static DatabaseService? _instance;
    private readonly SQLiteConnection _db;
    private readonly object _gate = new();

    public static DatabaseService Instance => _instance ??= new DatabaseService();

    private DatabaseService()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CopperIPTV", "copper_iptv.db");

        LogService.Info($"DatabaseService: Initializing database at {dbPath}");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _db = new SQLiteConnection(dbPath);

        _db.CreateTable<Playlist>();
        _db.CreateTable<Channel>();
        _db.CreateTable<Favorite>();
        _db.CreateTable<RecentChannel>();
        _db.CreateTable<AppSettings>();
        _db.CreateTable<EpgProgram>();
        _db.CreateTable<XtreamAccount>();

        LogService.Info("DatabaseService: Tables created/verified");

        RunMigrations();
        SeedTestChannels();
    }

    private void RunMigrations()
    {
        try
        {
            var cols = _db.GetTableInfo("Channel");
            var colNames = cols.Select(c => c.Name).ToList();
            if (!colNames.Contains("FallbackUrl"))
                _db.Execute("ALTER TABLE Channel ADD COLUMN FallbackUrl TEXT DEFAULT ''");
            if (!colNames.Contains("HealthScore"))
                _db.Execute("ALTER TABLE Channel ADD COLUMN HealthScore INTEGER DEFAULT 100");
            if (!colNames.Contains("LastChecked"))
                _db.Execute("ALTER TABLE Channel ADD COLUMN LastChecked TEXT DEFAULT '0001-01-01T00:00:00'");
        }
        catch { }

        try
        {
            var playlistCols = _db.GetTableInfo("Playlist");
            var playlistColNames = playlistCols.Select(c => c.Name).ToList();
            if (!playlistColNames.Contains("AutoRefresh"))
                _db.Execute("ALTER TABLE Playlist ADD COLUMN AutoRefresh INTEGER DEFAULT 0");
            if (!playlistColNames.Contains("RefreshIntervalMinutes"))
                _db.Execute("ALTER TABLE Playlist ADD COLUMN RefreshIntervalMinutes INTEGER DEFAULT 60");
            if (!playlistColNames.Contains("LastRefreshed"))
                _db.Execute("ALTER TABLE Playlist ADD COLUMN LastRefreshed TEXT");
        }
        catch { }
    }

    private void SeedTestChannels()
    {
        var existingCount = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM Channel");
        if (existingCount > 0) return;

        var testChannels = new[]
        {
            new Channel
            {
                Id = "test-apple-basic",
                Name = "Apple Basic Stream (HLS)",
                Url = "https://devstreaming-cdn.apple.com/videos/stream/basic/index.m3u8",
                Logo = "",
                Group = "Test Streams",
                TvgId = "",
                TvgName = "Apple Basic",
                Language = "en",
                Country = "",
                PlaylistId = "0"
            },
            new Channel
            {
                Id = "test-apple-bipbop",
                Name = "Apple BipBop All (HLS)",
                Url = "https://devstreaming-cdn.apple.com/videos/stream/for001/stream.m3u8",
                Logo = "",
                Group = "Test Streams",
                TvgId = "",
                TvgName = "Apple BipBop",
                Language = "en",
                Country = "",
                PlaylistId = "0"
            },
            new Channel
            {
                Id = "test-aljazeera-doc",
                Name = "Al Jazeera Documentary",
                Url = "https://live-hls-web-ajd.getaj.net/AJD/index.m3u8",
                Logo = "",
                Group = "Test Streams",
                TvgId = "",
                TvgName = "Al Jazeera Documentary",
                Language = "ar",
                Country = "QA",
                PlaylistId = "0"
            }
        };

        var testPlaylist = new Playlist { Name = "Test Streams", SourceUrl = "seeded", ChannelCount = testChannels.Length };
        _db.Insert(testPlaylist);
        var playlistId = testPlaylist.Id;

        foreach (var ch in testChannels)
        {
            ch.PlaylistId = playlistId.ToString();
            _db.Insert(ch);
        }

        LogService.Info($"DatabaseService: Seeded {testChannels.Length} test channels");
    }

    public List<Playlist> GetAllPlaylists() { lock (_gate) return _db.Table<Playlist>().OrderByDescending(p => p.AddedAt).ToList(); }
    public int InsertPlaylist(Playlist playlist) { lock (_gate) return _db.Insert(playlist); }
    public int UpdatePlaylist(Playlist playlist) { lock (_gate) return _db.Update(playlist); }
    public int DeletePlaylist(int id) { lock (_gate) return _db.Delete<Playlist>(id); }
    public Playlist? GetPlaylist(int id) { lock (_gate) return _db.Table<Playlist>().FirstOrDefault(p => p.Id == id); }

    public List<Channel> GetAllChannels() { lock (_gate) return _db.Table<Channel>().ToList(); }
    public List<Channel> GetChannelsByPlaylistId(int playlistId) { lock (_gate) return _db.Table<Channel>().Where(c => c.PlaylistId == playlistId.ToString()).ToList(); }
    public int InsertChannel(Channel channel) { lock (_gate) return _db.InsertOrReplace(channel); }
    public int UpdateChannel(Channel channel) { lock (_gate) return _db.Update(channel); }
    public int DeleteChannelsByPlaylistId(int playlistId) { lock (_gate) return _db.Execute("DELETE FROM Channel WHERE PlaylistId = ?", playlistId.ToString()); }

    public bool DeleteChannel(string channelId)
    {
        lock (_gate)
        {
            var channel = GetChannelUnsafe(channelId);
            if (channel == null) return false;

            var deleted = _db.Delete<Channel>(channelId);
            _db.Execute("DELETE FROM Favorite WHERE ChannelId = ?", channelId);
            _db.Execute("DELETE FROM RecentChannel WHERE ChannelId = ?", channelId);

            // Keep playlist channel counts accurate for regular playlists.
            if (int.TryParse(channel.PlaylistId, out var playlistId))
            {
                var playlist = _db.Table<Playlist>().FirstOrDefault(p => p.Id == playlistId);
                if (playlist != null)
                {
                    playlist.ChannelCount = _db.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM Channel WHERE PlaylistId = ?", channel.PlaylistId);
                    _db.Update(playlist);
                }
            }

            LogService.Info($"DatabaseService: Deleted channel '{channel.Name}' ({channelId})");
            return deleted > 0;
        }
    }

    public int InsertOrUpdateChannel(Channel channel)
    {
        lock (_gate)
        {
            var existing = GetChannelUnsafe(channel.Id);
            if (existing != null)
            {
                return _db.Update(channel);
            }
            return _db.Insert(channel);
        }
    }

    public void BulkInsertChannels(List<Channel> channels)
    {
        lock (_gate)
        {
            _db.RunInTransaction(() =>
            {
                foreach (var ch in channels)
                {
                    _db.InsertOrReplace(ch);
                }
            });
        }
    }

    public List<Favorite> GetFavorites() { lock (_gate) return _db.Table<Favorite>().ToList(); }
    public HashSet<string> GetFavoriteIds() { lock (_gate) return _db.Table<Favorite>().Select(f => f.ChannelId).ToHashSet(); }
    public bool IsFavorite(string channelId) { lock (_gate) return _db.Table<Favorite>().Any(f => f.ChannelId == channelId); }
    public int InsertFavorite(Favorite fav) { lock (_gate) return _db.Insert(fav); }
    public int DeleteFavorite(string channelId) { lock (_gate) return _db.Execute("DELETE FROM Favorite WHERE ChannelId = ?", channelId); }

    public Channel? GetChannel(string id) { lock (_gate) return GetChannelUnsafe(id); }

    private Channel? GetChannelUnsafe(string id) => _db.Table<Channel>().FirstOrDefault(c => c.Id == id);

    public List<Channel> GetRecentChannels(int limit = 20)
    {
        lock (_gate)
        {
            var recentIds = _db.Query<RecentChannel>(
                "SELECT ChannelId FROM RecentChannel ORDER BY WatchedAt DESC LIMIT ?", limit)
                .Select(r => r.ChannelId).ToList();

            var channels = new List<Channel>();
            foreach (var id in recentIds)
            {
                var ch = GetChannelUnsafe(id);
                if (ch != null) channels.Add(ch);
            }
            return channels;
        }
    }

    public void AddRecentChannel(string channelId)
    {
        lock (_gate)
        {
            _db.Execute("DELETE FROM RecentChannel WHERE ChannelId = ?", channelId);
            _db.Insert(new RecentChannel { ChannelId = channelId });
            _db.Execute("DELETE FROM RecentChannel WHERE Id NOT IN (SELECT Id FROM RecentChannel ORDER BY WatchedAt DESC LIMIT 50)");
        }
    }

    public void ClearRecentChannels()
    {
        lock (_gate)
            _db.Execute("DELETE FROM RecentChannel");
    }

    public string GetSetting(string key, string defaultValue = "")
    {
        lock (_gate)
        {
            var setting = _db.Table<AppSettings>().FirstOrDefault(s => s.Key == key);
            return setting?.Value ?? defaultValue;
        }
    }

    public void SetSetting(string key, string value)
    {
        lock (_gate)
        {
            var existing = _db.Table<AppSettings>().FirstOrDefault(s => s.Key == key);
            if (existing != null)
            {
                existing.Value = value;
                _db.Update(existing);
            }
            else
            {
                _db.Insert(new AppSettings { Key = key, Value = value });
            }
        }
    }

    public void UpdateChannelHealth(string channelId, int scoreDelta)
    {
        lock (_gate)
        {
            var ch = GetChannelUnsafe(channelId);
            if (ch == null) return;
            ch.HealthScore = Math.Max(0, Math.Min(100, ch.HealthScore + scoreDelta));
            ch.LastChecked = DateTime.UtcNow;
            _db.Update(ch);
        }
    }

    public List<EpgProgram> GetEpgForChannel(string channelId, DateTime? date = null)
    {
        var targetDate = date ?? DateTime.Today;
        // EPG times are stored as UTC ticks; convert the local day window to UTC.
        var start = DateTime.SpecifyKind(targetDate.Date, DateTimeKind.Local).ToUniversalTime();
        var end = start.AddDays(1);
        lock (_gate)
        {
            return _db.Table<EpgProgram>()
                .Where(ep => ep.ChannelId == channelId && ep.Start >= start && ep.Start < end)
                .OrderBy(ep => ep.Start)
                .ToList();
        }
    }

    public void SaveEpgPrograms(List<EpgProgram> programs)
    {
        if (programs.Count == 0) return;

        lock (_gate)
        {
            _db.RunInTransaction(() =>
            {
                foreach (var p in programs)
                {
                    var existing = _db.Table<EpgProgram>().FirstOrDefault(ep =>
                        ep.ChannelId == p.ChannelId && ep.Start == p.Start);
                    if (existing != null)
                    {
                        existing.Title = p.Title;
                        existing.Description = p.Description;
                        existing.Stop = p.Stop;
                        existing.Category = p.Category;
                        _db.Update(existing);
                    }
                    else
                    {
                        _db.Insert(p);
                    }
                }
            });
        }
    }

    public void ClearOldEpg(DateTime before)
    {
        lock (_gate)
            _db.Execute("DELETE FROM EpgProgram WHERE Stop < ?", before);
    }

    public List<XtreamAccount> GetAllXtreamAccounts() { lock (_gate) return _db.Table<XtreamAccount>().OrderByDescending(a => a.AddedAt).ToList(); }
    public int InsertXtreamAccount(XtreamAccount account) { lock (_gate) return _db.Insert(account); }
    public int UpdateXtreamAccount(XtreamAccount account) { lock (_gate) return _db.Update(account); }
    public int DeleteXtreamAccount(int id)
    {
        lock (_gate)
        {
            var playlistIdStr = $"xtream_{id}";
            _db.Execute("DELETE FROM Channel WHERE PlaylistId = ?", playlistIdStr);
            _db.Execute("DELETE FROM Playlist WHERE SourceUrl = ?", playlistIdStr);
            return _db.Delete<XtreamAccount>(id);
        }
    }

    public int Execute(string command, params object[] args) { lock (_gate) return _db.Execute(command, args); }
}
