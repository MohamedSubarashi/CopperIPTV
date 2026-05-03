using System;
using SQLite;

namespace CopperIPTV.Models;

public class Favorite
{
    [PrimaryKey]
    public string ChannelId { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
