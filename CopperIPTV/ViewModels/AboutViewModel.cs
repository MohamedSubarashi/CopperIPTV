using System;
using CommunityToolkit.Mvvm.Input;

namespace CopperIPTV.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    public string AppName { get; } = "Copper IPTV Player";
    public string Version { get; } =
        typeof(AboutViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    public string Description { get; } =
        "A modern, cross-platform IPTV player supporting M3U playlists and Xtream Codes. " +
        "Built with Avalonia UI and LibVLC - runs on Windows, Linux and macOS.";
    public string Copyright { get; } = $"© {DateTime.Now.Year} Mohamed Subarashi";

    public const string DonationUrl = "https://ko-fi.com/mohamedsubarashi";
}
