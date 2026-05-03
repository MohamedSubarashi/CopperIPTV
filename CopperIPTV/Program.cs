using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using System;
using System.IO;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using CopperIPTV.Services;

namespace CopperIPTV;

class Program
{
    public static LibVLC? SharedLibVLC { get; private set; }

    [DllImport("kernel32.dll")]
    static extern bool AllocConsole();

    [STAThread]
    public static void Main(string[] args)
    {
        AllocConsole();
        Console.Title = "Copper IPTV Player - Debug Log";
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Log(ConsoleColor.Green, "=== Copper IPTV Player Starting ===");
        Log(ConsoleColor.Gray, $"App directory: {AppContext.BaseDirectory}");
        Log(ConsoleColor.Gray, $"OS: {Environment.OSVersion}");
        Log(ConsoleColor.Gray, $".NET: {Environment.Version}");
        Log(ConsoleColor.Gray, $"Arch: {RuntimeInformation.OSArchitecture}");

        var libVlcPath = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
        Log(ConsoleColor.Gray, $"VLC path: {libVlcPath}");
        Log(ConsoleColor.Gray, $"VLC dir exists: {Directory.Exists(libVlcPath)}");

        try
        {
            if (Directory.Exists(libVlcPath))
            {
                Log(ConsoleColor.Green, "VLC directory found, initializing with explicit path");
                Core.Initialize(libVlcPath);
            }
            else
            {
                Log(ConsoleColor.Yellow, "VLC directory not found, using default");
                Core.Initialize();
            }

            SharedLibVLC = new LibVLC("--no-video-title-show", "--ignore-config");
            Log(ConsoleColor.Green, "VLC singleton initialized");
        }
        catch (Exception ex)
        {
            Log(ConsoleColor.Red, $"VLC init failed: {ex.Message}");
        }

        try
        {
            Log(ConsoleColor.Green, "Starting Avalonia UI...");
            var appBuilder = BuildAvaloniaApp();

            appBuilder.AfterSetup(ctx =>
            {
                if (ctx.Instance?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var db = Services.DatabaseService.Instance;
                    var autoRefresh = db.GetSetting("auto_playlist_refresh", "false");
                    if (autoRefresh == "true")
                    {
                        Services.PlaylistAutoRefreshService.Start();
                    }

                    var autoHealth = db.GetSetting("auto_health_check", "false");
                    if (autoHealth == "true")
                    {
                        var interval = int.TryParse(db.GetSetting("health_check_interval", "30"), out var parsedInterval) ? parsedInterval : 30;
                        Services.StreamHealthService.StartAutoCheck(interval);
                    }

                    desktop.ShutdownRequested += (s, e) =>
                    {
                        Services.PlaylistAutoRefreshService.Stop();
                        Services.StreamHealthService.StopAutoCheck();
                        SharedLibVLC?.Dispose();
                        Log(ConsoleColor.Green, "VLC disposed");
                    };
                }
            });

            appBuilder.StartWithClassicDesktopLifetime(args);
            Log(ConsoleColor.Green, "=== Application Shutdown ===");
        }
        catch (Exception ex)
        {
            Log(ConsoleColor.Red, $"FATAL ERROR: {ex}");
            Console.WriteLine();
            Console.WriteLine("Press any key to close...");
            Console.ReadKey();
        }
    }

    static void Log(ConsoleColor color, string message)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        Console.ResetColor();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
