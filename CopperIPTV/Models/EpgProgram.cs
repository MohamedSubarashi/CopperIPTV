using System;
using SQLite;

namespace CopperIPTV.Models;

public class EpgProgram
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime Stop { get; set; }
    public string Category { get; set; } = string.Empty;
}
