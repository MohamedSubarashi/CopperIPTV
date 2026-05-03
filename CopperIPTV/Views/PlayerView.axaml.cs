using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CopperIPTV.ViewModels;
using CopperIPTV.Services;
using LibVLCSharp.Shared;

namespace CopperIPTV.Views;

public partial class PlayerView : UserControl, IDisposable
{
    private MediaPlayer? _mediaPlayer;
    private PlayerViewModel? _vm;
    private TextBlock? _favIcon;
    private TextBlock? _favText;
    private Border? _loadingOverlay;
    private Border? _errorOverlay;
    private TextBlock? _statusText;
    private TextBlock? _errorText;
    private Button? _retryButton;
    private Button? _playPauseButton;
    private Button? _muteButton;
    private Slider? _volumeSlider;
    private TextBlock? _volumeLabel;
    private TextBlock? _qualityDot;
    private Button? _maximizeBtn;
    private Button? _minimizeBtn;
    private Border? _uiControlsContainer;
    private Border? _controlsBar;
    private StackPanel? _headerPanel;
    private Slider? _seekSlider;
    private TextBlock? _currentTimeText;
    private TextBlock? _totalTimeText;
    private Button? _centerPlayPauseBtn;
    private DispatcherTimer? _seekUpdateTimer;
    private DispatcherTimer? _hidePlayPauseTimer;
    private bool _isFirstPlay = true;
    private bool _hasError;
    private bool _hasLoggedBuffering;
    private DateTime _errorTime;
    private bool _isDisposed;
    private bool _isMaximized;
    private bool _isUserSeeking;
    private bool _isVideoViewDetached;

    public PlayerView()
    {
        InitializeComponent();

        _favIcon = this.FindControl<TextBlock>("FavIcon");
        _favText = this.FindControl<TextBlock>("FavText");
        _loadingOverlay = this.FindControl<Border>("LoadingOverlay");
        _errorOverlay = this.FindControl<Border>("ErrorOverlay");
        _statusText = this.FindControl<TextBlock>("StatusText");
        _errorText = this.FindControl<TextBlock>("ErrorText");
        _retryButton = this.FindControl<Button>("RetryButton");
        _playPauseButton = this.FindControl<Button>("PlayPauseButton");
        _muteButton = this.FindControl<Button>("MuteButton");
        _volumeSlider = this.FindControl<Slider>("VolumeSlider");
        _volumeLabel = this.FindControl<TextBlock>("VolumeLabel");
        _qualityDot = this.FindControl<TextBlock>("QualityDot");
        _maximizeBtn = this.FindControl<Button>("MaximizeBtn");
        _minimizeBtn = this.FindControl<Button>("MinimizeBtn");
        _uiControlsContainer = this.FindControl<Border>("UIControlsContainer");
        _controlsBar = this.FindControl<Border>("ControlsBar");
        _headerPanel = this.FindControl<StackPanel>("HeaderPanel");
        _seekSlider = this.FindControl<Slider>("SeekSlider");
        _currentTimeText = this.FindControl<TextBlock>("CurrentTimeText");
        _totalTimeText = this.FindControl<TextBlock>("TotalTimeText");
        _centerPlayPauseBtn = this.FindControl<Button>("CenterPlayPauseBtn");

        if (_retryButton != null)
            _retryButton.Click += OnRetryClicked;

        if (_volumeSlider != null)
            _volumeSlider.ValueChanged += OnVolumeChanged;

        if (_seekSlider != null)
        {
            _seekSlider.PointerPressed += OnSeekPointerPressed;
            _seekSlider.PointerReleased += OnSeekPointerReleased;
            _seekSlider.ValueChanged += OnSeekValueChanged;
        }

        _seekUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _seekUpdateTimer.Tick += OnSeekUpdateTick;

        _hidePlayPauseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _hidePlayPauseTimer.Tick += (s, e) =>
        {
            if (_centerPlayPauseBtn != null)
                _centerPlayPauseBtn.IsVisible = false;
            _hidePlayPauseTimer?.Stop();
        };

        if (DataContext is ViewModels.PlayerViewModel)
            RegisterGlobalKeyHandler();

        LogService.Debug("PlayerView constructor called");
        InitializeMediaPlayer();
    }

    private void RegisterGlobalKeyHandler()
    {
        if (VisualRoot is Window { DataContext: ViewModels.MainViewModel mainVm })
        {
            mainVm.RegisterPlayerKeyHandler(OnGlobalKey);
        }
    }

    private void UnregisterGlobalKeyHandler()
    {
        if (VisualRoot is Window { DataContext: ViewModels.MainViewModel mainVm })
        {
            mainVm.UnregisterPlayerKeyHandler();
        }
    }

    private void OnGlobalKey(Key key)
    {
        switch (key)
        {
            case Key.Space:
                TogglePlayPause();
                break;
            case Key.Enter:
                ToggleMaximize();
                break;
            case Key.F:
                ToggleMaximize();
                break;
            case Key.Escape:
                ExitMaximize();
                break;
            case Key.M:
                ToggleMute();
                break;
            case Key.Up:
                AdjustVolume(5);
                break;
            case Key.Down:
                AdjustVolume(-5);
                break;
            case Key.Left:
                _vm?.PreviousChannelCommand.Execute(null);
                break;
            case Key.Right:
                _vm?.NextChannelCommand.Execute(null);
                break;
            case Key.Home:
                try { if (_mediaPlayer != null) _mediaPlayer.Position = 0; } catch { }
                break;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is PlayerViewModel)
            OnGlobalKey(e.Key);
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        ShowControls();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        ShowControls();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        HideControls();
    }

    private void ShowCenterPlayPause(bool isPaused)
    {
        if (_centerPlayPauseBtn != null)
        {
            _centerPlayPauseBtn.Content = isPaused ? "▶" : "⏸";
            _centerPlayPauseBtn.IsVisible = true;
            _hidePlayPauseTimer?.Stop();
            _hidePlayPauseTimer?.Start();
        }
    }

    private void ShowControls()
    {
        if (_uiControlsContainer != null) _uiControlsContainer.IsVisible = true;
    }

    private void HideControls()
    {
        if (_uiControlsContainer != null && _mediaPlayer?.IsPlaying == true) _uiControlsContainer.IsVisible = false;
    }

    private void TogglePlayPause()
    {
        if (_mediaPlayer == null) return;
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
            LogService.Info("Playback paused (keyboard)");
            UpdatePlayPauseIcon(true);
            ShowCenterPlayPause(true);
        }
        else
        {
            _mediaPlayer.Play();
            LogService.Info("Playback resumed (keyboard)");
            UpdatePlayPauseIcon(false);
            ShowCenterPlayPause(false);
        }
        ShowControls();
    }

    private void ToggleMute()
    {
        if (_mediaPlayer == null) return;
        _mediaPlayer.Mute = !_mediaPlayer.Mute;
        UpdateMuteIcon(_mediaPlayer.Mute);
        ShowControls();
    }

    private void AdjustVolume(int delta)
    {
        if (_mediaPlayer == null || _volumeSlider == null || _volumeLabel == null) return;
        var newValue = Math.Max(0, Math.Min(100, _volumeSlider.Value + delta));
        _volumeSlider.Value = newValue;
        _mediaPlayer.Volume = (int)Math.Round(newValue * 2.0);
        _volumeLabel.Text = $"{(int)Math.Round(newValue)}%";
        ShowControls();
    }

    private void ToggleMaximize()
    {
        if (VisualRoot is Window window)
        {
            _isMaximized = !_isMaximized;
            window.WindowState = _isMaximized ? WindowState.Maximized : WindowState.Normal;
            window.ExtendClientAreaToDecorationsHint = _isMaximized;
            ShowControls();
        }
    }

    private void ExitMaximize()
    {
        if (_isMaximized && VisualRoot is Window window)
        {
            _isMaximized = false;
            window.WindowState = WindowState.Normal;
            window.ExtendClientAreaToDecorationsHint = false;
            ShowControls();
        }
    }

    private void OnMaximizeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnMinimizeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (VisualRoot is Window window)
        {
            if (_isMaximized)
            {
                window.WindowState = WindowState.Normal;
                Dispatcher.UIThread.Post(() =>
                {
                    window.WindowState = WindowState.Minimized;
                }, DispatcherPriority.Background);
            }
            else
            {
                window.WindowState = WindowState.Minimized;
            }
        }
    }

    private void InitializeMediaPlayer()
    {
        try
        {
            if (Program.SharedLibVLC == null)
            {
                LogService.Error("SharedLibVLC is null - VLC failed to initialize at startup");
                ShowError("Video engine failed to start. Check logs for details.");
                return;
            }

            ResetVideoView();

            LogService.Info("Creating MediaPlayer from shared LibVLC...");

            _mediaPlayer = new MediaPlayer(Program.SharedLibVLC);

            _mediaPlayer.Playing += OnPlaying;
            _mediaPlayer.EncounteredError += OnError;
            _mediaPlayer.EndReached += OnEndReached;
            _mediaPlayer.Buffering += OnBuffering;
            _mediaPlayer.Paused += OnPaused;
            _mediaPlayer.Stopped += OnStopped;

            var db = DatabaseService.Instance;
            var defaultVolume = int.TryParse(db.GetSetting("default_volume", "80"), out var vol) ? vol : 80;

            _mediaPlayer.Volume = defaultVolume * 2;
            UpdatePlayPauseIcon(false);
            UpdateMuteIcon(false);
            if (_volumeSlider != null) _volumeSlider.Value = defaultVolume;

            if (VideoView != null)
            {
                VideoView.MediaPlayer = _mediaPlayer;
                LogService.Info("VideoView.MediaPlayer assigned");
            }
            else
            {
                LogService.Error("VideoView control is null! Cannot display video.");
            }

            LogService.Info("MediaPlayer initialization complete");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "MediaPlayer initialization failed");
            ShowError($"Video engine failed: {ex.Message}");
        }
    }

    private void DestroyMediaPlayer()
    {
        if (_mediaPlayer == null) return;

        _seekUpdateTimer?.Stop();

        try { _mediaPlayer.Playing -= OnPlaying; } catch { }
        try { _mediaPlayer.EncounteredError -= OnError; } catch { }
        try { _mediaPlayer.EndReached -= OnEndReached; } catch { }
        try { _mediaPlayer.Buffering -= OnBuffering; } catch { }
        try { _mediaPlayer.Paused -= OnPaused; } catch { }
        try { _mediaPlayer.Stopped -= OnStopped; } catch { }

        try { _mediaPlayer.Stop(); } catch { }
        try { _mediaPlayer.Dispose(); } catch { }
        _mediaPlayer = null;

        LogService.Debug("MediaPlayer destroyed");
    }

    private void ClearVideoView()
    {
        try
        {
            if (VideoView != null && !_isVideoViewDetached)
            {
                VideoView.IsVisible = false;
                VideoView.MediaPlayer = null;
                _isVideoViewDetached = true;
                LogService.Debug("VideoView cleared and hidden");
            }
        }
        catch { }
    }

    private void ResetVideoView()
    {
        try
        {
            _isVideoViewDetached = false;
            if (VideoView != null)
            {
                VideoView.IsVisible = true;
                LogService.Debug("VideoView reset to visible");
            }
        }
        catch { }
    }

    private void StopPlayback()
    {
        if (_isDisposed) return;

        _seekUpdateTimer?.Stop();
        _hidePlayPauseTimer?.Stop();

        ClearVideoView();
        DestroyMediaPlayer();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is PlayerViewModel vm)
        {
            _vm = vm;
            _isFirstPlay = true;
            _hasError = false;
            UpdateFavoriteUI(vm.IsFavorite);
            UpdateQualityIndicator(vm.StreamQuality);

            LogService.Info($"DataContext changed - Channel: {vm.ChannelName}");

            if (VisualRoot is Window { DataContext: ViewModels.MainViewModel mainVm })
            {
                mainVm.RegisterPlayerKeyHandler(OnGlobalKey);
            }

            if (_mediaPlayer != null && Program.SharedLibVLC != null && !_hasError)
            {
                PlayMedia(vm.MediaUrl);
            }
            else if (_hasError)
            {
                LogService.Warning("Cannot play - error state active");
            }
            else
            {
                LogService.Warning("Cannot play - MediaPlayer not ready");
                ShowError("Video engine not ready. Check logs for details.");
            }
        }
        else
        {
            LogService.Debug("PlayerView DataContext cleared - cleaning up");
            UnregisterGlobalKeyHandler();
            StopPlayback();
        }
    }

    private void PlayMedia(string url)
    {
        if (_mediaPlayer == null || string.IsNullOrEmpty(url) || Program.SharedLibVLC == null)
        {
            LogService.Warning($"Cannot play - ready={(_mediaPlayer != null && Program.SharedLibVLC != null)}, URL empty={string.IsNullOrEmpty(url)}");
            ShowError("No stream URL available");
            return;
        }

        _hasError = false;
        _isFirstPlay = true;
        _hasLoggedBuffering = false;
        _errorTime = DateTime.UtcNow;
        ShowLoading("Loading stream...");

        LogService.Info($"Attempting to play: {url}");

        try
        {
            var db = DatabaseService.Instance;
            var networkCaching = int.TryParse(db.GetSetting("network_caching", "3000"), out var nc) ? nc : 3000;

            var media = new Media(Program.SharedLibVLC, url, FromType.FromLocation);

            media.AddOption($":network-caching={networkCaching}");
            media.AddOption($":live-caching={networkCaching}");
            media.AddOption($":file-caching={networkCaching}");
            media.AddOption(":http-reconnect");
            media.AddOption(":http-user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            media.AddOption(":http-referrer=");
            media.AddOption(":http-extra-headers=Accept: */*\r\nAccept-Encoding: gzip, deflate, br\r\nAccept-Language: en-US,en;q=0.9");

            string? domain = ExtractDomain(url);
            if (!string.IsNullOrEmpty(domain))
            {
                media.AddOption($":http-referrer=https://{domain}/");
                LogService.Debug($"Set HTTP referrer to: https://{domain}/");
            }

            media.AddOption(":network-caching=3000");
            media.AddOption(":live-caching=3000");
            media.AddOption(":file-caching=3000");
            media.AddOption(":no-video-title-show");

            LogService.Debug("Media options set, calling Play()...");

            bool playResult = _mediaPlayer.Play(media);
            LogService.Info($"MediaPlayer.Play() returned: {playResult}");
            LogService.Debug($"MediaPlayer.State: {_mediaPlayer.State}");

            media.Dispose();

            Dispatcher.UIThread.Post(() =>
            {
                if (_isFirstPlay && _loadingOverlay != null)
                {
                    ShowLoading("Buffering...");
                }
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "PlayMedia exception");
            ShowError($"Playback error: {ex.Message}");
        }
    }

    private void OnRetryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Retry();
    }

    public void Retry()
    {
        if (_vm != null && Program.SharedLibVLC != null)
        {
            LogService.Info("Retrying playback...");
            _hasError = false;
            ClearVideoView();
            DestroyMediaPlayer();
            InitializeMediaPlayer();
            PlayMedia(_vm.MediaUrl);
        }
    }

    private void OnPlaying(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        _isFirstPlay = false;
        LogService.Info("VLC Event: Playing");
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDisposed) return;
                if (_loadingOverlay != null)
                    _loadingOverlay.IsVisible = false;
                if (_errorOverlay != null)
                    _errorOverlay.IsVisible = false;
                UpdatePlayPauseIcon(false);
                UpdateSeekInfo();
                _seekUpdateTimer?.Start();
            });
        }
        catch { }
    }

    private void OnSeekUpdateTick(object? sender, EventArgs e)
    {
        if (_mediaPlayer != null && !_isUserSeeking)
        {
            UpdateSeekInfo();
        }
    }

    private void UpdateSeekInfo()
    {
        if (_mediaPlayer == null) return;

        try
        {
            var time = _mediaPlayer.Time;
            var length = _mediaPlayer.Length;

            if (_currentTimeText != null)
                _currentTimeText.Text = FormatTime(time);

            if (_totalTimeText != null)
            {
                if (length > 0)
                {
                    _totalTimeText.Text = FormatTime(length);
                    if (_seekSlider != null && !_isUserSeeking)
                        _seekSlider.Value = (time / (double)length) * 100;
                }
                else
                {
                    _totalTimeText.Text = "LIVE";
                }
            }
        }
        catch { }
    }

    private string FormatTime(long ms)
    {
        if (ms <= 0) return "00:00";
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.Hours > 0 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
    }

    private void OnSeekPointerPressed(object? sender, PointerEventArgs e)
    {
        _isUserSeeking = true;
    }

    private void OnSeekPointerReleased(object? sender, PointerEventArgs e)
    {
        _isUserSeeking = false;
        SeekToPosition();
    }

    private void OnSeekValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUserSeeking && _currentTimeText != null)
        {
            var length = _mediaPlayer?.Length ?? 0;
            var seekTime = (long)(e.NewValue / 100.0 * length);
            _currentTimeText.Text = FormatTime(seekTime);
        }
    }

    private void SeekToPosition()
    {
        if (_mediaPlayer != null && _seekSlider != null)
        {
            try
            {
                _mediaPlayer.Position = (float)(_seekSlider.Value / 100.0);
                LogService.Debug($"Seeked to position: {_seekSlider.Value:F1}%");
            }
            catch { }
        }
    }

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (_isDisposed) return;
        if (!_hasLoggedBuffering)
        {
            _hasLoggedBuffering = true;
            LogService.Debug("VLC Event: Buffering started");
        }
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDisposed) return;
                if (_statusText != null && _loadingOverlay != null && _loadingOverlay.IsVisible)
                    _statusText.Text = "Buffering...";
            });
        }
        catch { }
    }

    private void OnError(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        _hasError = true;
        var state = _mediaPlayer?.State.ToString() ?? "Unknown";
        var elapsed = DateTime.UtcNow - _errorTime;
        string message;

        if (elapsed.TotalSeconds < 1)
        {
            message = state switch
            {
                "Error" => "Stream failed to load. The URL may be invalid.",
                "Ended" => "Stream ended unexpectedly.",
                "NothingSpecial" => "Cannot connect to stream server. Check your connection.",
                _ => "Failed to open stream. The server may be unreachable."
            };
        }
        else if (elapsed.TotalSeconds < 10)
        {
            message = state switch
            {
                "Error" => "Stream connection failed after a few seconds. The server may have rejected the request.",
                "Ended" => "Stream ended prematurely.",
                "NothingSpecial" => "Connection dropped. The stream server may be overloaded.",
                _ => "Stream failed. The server stopped responding."
            };
        }
        else
        {
            message = state switch
            {
                "Error" => "Stream timed out. The server is slow or the stream is geo-blocked.",
                "Ended" => "Stream ended after playing for a while.",
                "NothingSpecial" => "Connection lost after extended playback.",
                _ => "Stream error after extended playback."
            };
        }

        LogService.Error($"VLC Error - State: {state}, Elapsed: {elapsed.TotalSeconds:F1}s");
        _seekUpdateTimer?.Stop();
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_isDisposed)
                    ShowError(message);
            });
        }
        catch { }
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        _hasError = true;
        LogService.Warning("VLC Event: EndReached");
        _seekUpdateTimer?.Stop();
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_isDisposed)
                    ShowError("Stream ended.");
            });
        }
        catch { }
    }

    private void OnPaused(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        LogService.Debug("VLC Event: Paused");
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_isDisposed)
                {
                    UpdatePlayPauseIcon(true);
                    ShowControls();
                }
            });
        }
        catch { }
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        LogService.Debug("VLC Event: Stopped");
        _seekUpdateTimer?.Stop();
    }

    private void OnPlayPauseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void OnCenterPlayPauseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void OnMuteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ToggleMute();
    }

    private void OnVolumeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_mediaPlayer == null || _volumeLabel == null) return;

        int volume = (int)Math.Round(e.NewValue * 2.0);
        _mediaPlayer.Volume = volume;
        _volumeLabel.Text = $"{(int)Math.Round(e.NewValue)}%";
        UpdateMuteIcon(false);
    }

    private void UpdatePlayPauseIcon(bool isPaused)
    {
        if (_playPauseButton != null)
            _playPauseButton.Content = isPaused ? "▶" : "⏸";
        if (_centerPlayPauseBtn != null)
            _centerPlayPauseBtn.Content = isPaused ? "▶" : "⏸";
    }

    private void UpdateMuteIcon(bool isMuted)
    {
        if (_muteButton != null)
            _muteButton.Content = isMuted ? "🔇" : "🔊";
    }

    private void UpdateQualityIndicator(int quality)
    {
        if (_qualityDot == null) return;
        var color = quality switch
        {
            >= 90 => "#4caf50",
            >= 70 => "#8bc34a",
            >= 50 => "#ffc107",
            >= 30 => "#ff9800",
            _ => "#f44336"
        };
        _qualityDot.Text = "●";
        _qualityDot.Foreground = Avalonia.Media.Brush.Parse(color);
    }

    private void ShowLoading(string message)
    {
        if (_loadingOverlay != null)
            _loadingOverlay.IsVisible = true;
        if (_errorOverlay != null)
            _errorOverlay.IsVisible = false;
        if (_statusText != null)
            _statusText.Text = message;
    }

    private void ShowError(string message)
    {
        _isFirstPlay = false;
        if (_loadingOverlay != null)
            _loadingOverlay.IsVisible = false;
        if (_errorOverlay != null)
            _errorOverlay.IsVisible = true;
        if (_errorText != null)
            _errorText.Text = message;
    }

    private void UpdateFavoriteUI(bool isFavorite)
    {
        if (_favIcon != null)
            _favIcon.Text = isFavorite ? "❤️" : "♡";
        if (_favText != null)
            _favText.Text = isFavorite ? "Favorited" : "Favorite";
    }

    private string? ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        LogService.Debug("Disposing PlayerView...");

        UnregisterGlobalKeyHandler();

        _seekUpdateTimer?.Stop();
        _hidePlayPauseTimer?.Stop();

        if (_retryButton != null)
            _retryButton.Click -= OnRetryClicked;

        if (_volumeSlider != null)
            _volumeSlider.ValueChanged -= OnVolumeChanged;

        if (_seekSlider != null)
        {
            _seekSlider.PointerPressed -= OnSeekPointerPressed;
            _seekSlider.PointerReleased -= OnSeekPointerReleased;
            _seekSlider.ValueChanged -= OnSeekValueChanged;
        }

        ClearVideoView();
        DestroyMediaPlayer();

        GC.SuppressFinalize(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is PlayerViewModel)
            RegisterGlobalKeyHandler();

        if (VisualRoot is Window window)
            window.AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var state = _mediaPlayer?.State.ToString() ?? "N/A";
        var elapsed = DateTime.UtcNow - _errorTime;
        LogService.Debug($"PlayerView detached - State: {state}, Playback time: {elapsed.TotalSeconds:F1}s");

        _seekUpdateTimer?.Stop();
        _hidePlayPauseTimer?.Stop();

        try
        {
            if (VideoView != null)
            {
                VideoView.IsVisible = false;
                VideoView.MediaPlayer = null;
                _isVideoViewDetached = true;
            }
        }
        catch { }

        try
        {
            if (_mediaPlayer != null)
            {
                try { _mediaPlayer.Stop(); } catch { }
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }
        }
        catch { }

        UnregisterGlobalKeyHandler();

        if (VisualRoot is Window window)
            window.RemoveHandler(InputElement.KeyDownEvent, OnWindowKeyDown);

        base.OnDetachedFromVisualTree(e);
    }
}
