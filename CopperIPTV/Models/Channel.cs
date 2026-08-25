using System;
using SQLite;

namespace CopperIPTV.Models;

public class Channel
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Unknown Channel";
    public string Url { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public string Group { get; set; } = "Uncategorized";
    public string TvgId { get; set; } = string.Empty;
    public string TvgName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public string FallbackUrl { get; set; } = string.Empty;
    public int HealthScore { get; set; } = 100;
    public DateTime LastChecked { get; set; } = DateTime.MinValue;

    // Not persisted; resolved from the Favorite table when loading lists.
    [Ignore]
    public bool IsFavorite { get; set; }
}
