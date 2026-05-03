# Copper IPTV Player

<p align="center">
  <strong>A modern, cross-platform IPTV player for Live TV and VOD streaming.</strong>
</p>

<div align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-blue.svg" alt="Version 1.0.0">
  <img src="https://img.shields.io/badge/.NET-8.0-purple.svg" alt=".NET 8.0">
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg" alt="Cross-Platform">
</div>

## Features

- **M3U Playlist Support** — Import playlists from URL, local file, or paste content directly
- **Xtream Codes Login** — Authenticate with server URL, username, and password for automatic Live TV + VOD sync
- **FG Code Support** — Fetch playlists via DramaLive-compatible FG Codes
- **Live TV & VOD** — Stream live channels and on-demand movies
- **Channel Categories** — Organized by groups for easy browsing
- **Favorites** — Heart your favorite channels for quick access
- **Recent Channels** — Track recently watched channels
- **TV Guide (EPG)** — XMLTV integration for program schedules
- **Stream Health Check** — Automatic health monitoring with fallback URLs
- **Auto-Refresh** — Keep playlists updated automatically
- **Video Player** — Full VLC-powered playback with keyboard shortcuts
- **Dark Theme** — Beautiful copper-themed dark UI
- **Debug Logging** — Built-in logging for troubleshooting

## Downloads

| Platform | Architecture | Download |
|----------|-------------|----------|
| Windows | x64 | [CopperIPTV-win-x64](https://github.com/mohya1992/Copper-IPTV-Player/releases/download/v1.0.0/CopperIPTV-win-x64.zip) |
| Windows | ARM64 | [CopperIPTV-win-arm64](https://github.com/mohya1992/Copper-IPTV-Player/releases/download/v1.0.0/CopperIPTV-win-arm64.zip) |
| macOS | Intel (x64) | [CopperIPTV-osx-x64](https://github.com/mohya1992/Copper-IPTV-Player/releases/download/v1.0.0/CopperIPTV-osx-x64.zip) |
| macOS | Apple Silicon | [CopperIPTV-osx-arm64](https://github.com/mohya1992/Copper-IPTV-Player/releases/download/v1.0.0/CopperIPTV-osx-arm64.zip) |
| Linux | x64 | [CopperIPTV-linux-x64](https://github.com/mohya1992/Copper-IPTV-Player/releases/download/v1.0.0/CopperIPTV-linux-x64.zip) |
| Linux | ARM64 | [CopperIPTV-linux-arm64](https://github.com/mohya1992/Copper-IPTV-Player/releases/download/v1.0.0/CopperIPTV-linux-arm64.zip) |

## Installation

### Windows
1. Download the appropriate ZIP for your system
2. Extract to any folder
3. Run `CopperIPTV.exe`

### macOS
1. Download the appropriate ZIP for your chip (Intel or Apple Silicon)
2. Extract the archive
3. Run the application from Terminal: `./CopperIPTV`
4. If blocked by Gatekeeper: `sudo xattr -rd com.apple.quarantine CopperIPTV`

### Linux
1. Download the appropriate ZIP
2. Extract: `unzip CopperIPTV-linux-x64.zip`
3. Make executable: `chmod +x CopperIPTV`
4. Run: `./CopperIPTV`

## Keyboard Shortcuts (Player)

| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `F` / `Enter` | Toggle fullscreen |
| `Escape` | Exit fullscreen |
| `M` | Toggle mute |
| `↑` | Volume up |
| `↓` | Volume down |
| `←` | Previous channel |
| `→` | Next channel |

## Build from Source

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Git

### Clone
```bash
git clone https://github.com/mohya1992/Copper-IPTV-Player.git
cd Copper-IPTV-Player/CopperIPTV
```

### Build
```bash
dotnet restore
dotnet build
```

### Publish for a specific platform
```bash
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# macOS Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

## Screenshots

The application features a sleek dark copper theme with:
- Sidebar navigation for Live TV, Recent, Favorites, and Settings
- Channel grid view with logos and category filtering
- Full-screen video player with hover-based controls
- Settings panel for playlist management and Xtream login

## Technology Stack

- **.NET 8** — Cross-platform runtime
- **Avalonia UI** — Cross-platform desktop UI framework
- **LibVLC** — VLC media engine for playback
- **SQLite** — Local database for playlists, favorites, and settings
- **CommunityToolkit.Mvvm** — MVVM pattern with source generators

## Version 1.0.0 — Changelog

### Features
- Full M3U playlist import (URL, file, paste)
- Xtream Codes authentication with automatic Live TV + VOD channel sync
- FG Code (DramaLive) playlist fetching
- VLC-powered video playback with keyboard shortcuts
- Favorites system with search
- Recent channels tracking
- EPG (TV Guide) integration via XMLTV
- Stream health monitoring with auto-check
- Playlist auto-refresh service
- Cross-platform builds (Windows, macOS, Linux)
- Dark copper theme UI
- Debug log viewer
- Hover-based auto-hide player controls
- Channel category filtering

### Bug Fixes
- Fixed sidebar navigation command binding (`MethodAccessException`)
- Fixed UI freezing on large playlist imports (bulk insert with SQLite transactions)
- Fixed keyboard shortcut handling in player airspace
- Fixed pointer event conflicts on video overlay (flash loop on hover)
- Fixed DateTime consistency (UTC standardization across all services)
- Fixed async deadlock in Xtream Codes JSON parsing (`.Result` → `await`)
- Fixed auto-refresh service `_isRunning` state not resetting on cancellation

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

MIT License — see [LICENSE](LICENSE) for details.

---

<p align="center">Built with ❤️ using .NET 8 and Avalonia UI</p>
