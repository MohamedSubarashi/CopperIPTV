using System;
using SQLite;

namespace CopperIPTV.Models;

public class RecentChannel
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public DateTime WatchedAt { get; set; } = DateTime.UtcNow;
}
