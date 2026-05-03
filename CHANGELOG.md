# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2025-01-03

### Added
- M3U playlist import from URL, file, or paste content
- Xtream Codes authentication with automatic Live TV + VOD channel sync
- FG Code (DramaLive) playlist fetching
- VLC-powered video player with full playback controls
- Keyboard shortcuts for player control (play/pause, volume, channels, fullscreen)
- Favorites system with search filtering
- Recent channels tracking (last 50)
- EPG (TV Guide) integration via XMLTV format
- Stream health monitoring with automatic checking
- Playlist auto-refresh service
- Channel category filtering and search
- Cross-platform builds (Windows x64/ARM64, macOS x64/ARM64, Linux x64/ARM64)
- Dark copper theme UI with Fluent design
- Built-in debug log viewer with color-coded levels
- Hover-based auto-hide player controls
- Bulk channel database insert with transactions for performance
- Settings persistence (volume, network caching, EPG URL)

### Changed
- Optimized large playlist imports (100-1000x faster with bulk inserts)
- Standardized all DateTime usage to UTC
- Player controls now show/hide on mouse hover instead of timer

### Fixed
- Fixed sidebar navigation command binding (`MethodAccessException`)
- Fixed UI freezing when adding large playlists (per-channel → bulk transaction)
- Fixed keyboard shortcuts not working in VLC player airspace
- Fixed pointer event conflicts on video overlay causing flash loop
- Fixed DateTime inconsistency between services (Now → UtcNow)
- Fixed async deadlock in Xtream Codes parsing (`.Result` → `await`)
- Fixed auto-refresh service `_isRunning` not resetting on cancellation
- Fixed `int.Parse` crashes on invalid settings (→ `int.TryParse`)
- Fixed M3U parser duplicate condition check
