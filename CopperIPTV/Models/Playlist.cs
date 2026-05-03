using System;
using SQLite;

namespace CopperIPTV.Models;

public class Playlist
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = "Untitled";
    public string SourceUrl { get; set; } = string.Empty;
    public int ChannelCount { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public bool AutoRefresh { get; set; } = false;
    public int RefreshIntervalMinutes { get; set; } = 60;
    public DateTime? LastRefreshed { get; set; }
}
