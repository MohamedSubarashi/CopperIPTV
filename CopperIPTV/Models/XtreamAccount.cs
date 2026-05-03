using System;
using SQLite;

namespace CopperIPTV.Models;

public class XtreamAccount
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string ServerUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public int LiveCount { get; set; }
    public int VodCount { get; set; }
    public int SeriesCount { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSynced { get; set; }
}
