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

    [STAThread]
    public static void Main(string[] args)
    {
        var libVlcPath = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");

        try
        {
            if (Directory.Exists(libVlcPath))
                Core.Initialize(libVlcPath);
            else
                Core.Initialize();

            SharedLibVLC = new LibVLC("--no-video-title-show", "--ignore-config");
        }
        catch
        {
        }

        try
        {
            var appBuilder = BuildAvaloniaApp();

            appBuilder.AfterSetup(ctx =>
            {
                if (ctx.Instance?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var db = DatabaseService.Instance;
                    if (db.GetSetting("auto_playlist_refresh", "false") == "true")
                        PlaylistAutoRefreshService.Start();
                    if (db.GetSetting("auto_health_check", "false") == "true")
                    {
                        var interval = int.TryParse(db.GetSetting("health_check_interval", "30"), out var v) ? v : 30;
                        StreamHealthService.StartAutoCheck(interval);
                    }
                }
            });

            appBuilder.StartWithClassicDesktopLifetime(args);

            PlaylistAutoRefreshService.Stop();
            StreamHealthService.StopAutoCheck();
            SharedLibVLC?.Dispose();
        }
        catch
        {
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
