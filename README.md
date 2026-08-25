# Copper IPTV Player

A modern, cross-platform IPTV player built with Avalonia UI and LibVLC. Supports M3U playlists, Xtream Codes, XMLTV EPG, favorites, recents, stream health monitoring and auto-refresh.

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)

## Features

- M3U / M3U8 playlist support (URL, file, or paste)
- Xtream Codes login with automatic live + VOD sync
- XMLTV EPG guide
- Favorites and recently watched
- Right-click channel menu (open, favorite, delete)
- Stream health checks with automatic fallback URL switching
- Playlist auto-refresh
- Search and category filtering

## Downloads

Prebuilt binaries for every platform are produced by GitHub Actions on every push:

1. Open the **Actions** tab on the repository
2. Select the latest successful **Build** run
3. Download your platform's artifact from the **Artifacts** section:
   - `CopperIPTV-win-x64` - Windows 10/11 (64-bit), fully self-contained
   - `CopperIPTV-linux-x64` - Linux x64, self-contained (.NET included)
   - `CopperIPTV-osx-x64` - macOS Intel
   - `CopperIPTV-osx-arm64` - macOS Apple Silicon

## Runtime requirements

| Platform | Requirement |
|----------|-------------|
| Windows  | None - native libVLC is bundled |
| Linux    | `libvlc` from your distro: `sudo apt install vlc` or `sudo dnf install vlc` |
| macOS    | Install [VLC](https://www.videolan.org/) (`/Applications/VLC.app`) or `brew install libvlc` |

The app itself is self-contained on all platforms (no .NET installation required).

## Building from source

Requires the .NET 8 SDK.

```bash
# Run locally
dotnet run --project CopperIPTV

# Publish for a specific OS
dotnet publish CopperIPTV/CopperIPTV.csproj -c Release -r win-x64 --self-contained true
dotnet publish CopperIPTV/CopperIPTV.csproj -c Release -r linux-x64 --self-contained true
dotnet publish CopperIPTV/CopperIPTV.csproj -c Release -r osx-x64 --self-contained true
dotnet publish CopperIPTV/CopperIPTV.csproj -c Release -r osx-arm64 --self-contained true
```

On Linux/macOS the build does not bundle libVLC; it is resolved at runtime from the system install.

## Tech stack

- [.NET 8](https://dotnet.microsoft.com/) + [Avalonia UI 11](https://avaloniaui.net/)
- [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp) for playback
- SQLite via sqlite-net-pcl for playlists, channels, EPG and settings

## License

All rights reserved.
