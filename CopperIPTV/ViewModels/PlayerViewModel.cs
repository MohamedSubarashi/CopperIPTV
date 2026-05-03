using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperIPTV.Models;
using CopperIPTV.Services;

namespace CopperIPTV.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private Channel _channel;

    [ObservableProperty]
    private string _mediaUrl;

    [ObservableProperty]
    private string _channelName;

    [ObservableProperty]
    private string _channelGroup;

    [ObservableProperty]
    private string? _channelLogo;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private int _streamQuality = 0;

    [ObservableProperty]
    private string _streamQualityText = "";

    [ObservableProperty]
    private bool _hasPreviousChannel;

    [ObservableProperty]
    private bool _hasNextChannel;

    [ObservableProperty]
    private string _currentEpgTitle = "";

    private List<Channel> _channelList = [];
    private int _currentIndex = -1;

    public PlayerViewModel(MainViewModel mainVm, Channel channel, List<Channel>? channelList = null)
    {
        _mainVm = mainVm;
        _channel = channel;
        _mediaUrl = channel.Url;
        _channelName = channel.Name;
        _channelGroup = channel.Group;
        _channelLogo = string.IsNullOrEmpty(channel.Logo) ? null : channel.Logo;

        var db = DatabaseService.Instance;
        IsFavorite = db.IsFavorite(channel.Id);

        db.AddRecentChannel(channel.Id);

        if (channelList != null)
        {
            _channelList = channelList;
            _currentIndex = _channelList.FindIndex(c => c.Id == channel.Id);
            UpdateNavButtons();
        }

        LoadEpgInfo();
        UpdateStreamQuality();
    }

    private void LoadEpgInfo()
    {
        var epg = DatabaseService.Instance.GetEpgForChannel(Channel.Id);
        var now = DateTime.UtcNow;
        var currentProgram = epg.Find(p => p.Start <= now && p.Stop >= now);
        CurrentEpgTitle = currentProgram != null ? $"Now Playing: {currentProgram.Title}" : "";
    }

    private void UpdateStreamQuality()
    {
        var health = Channel.HealthScore;
        StreamQuality = health;
        StreamQualityText = health switch
        {
            >= 90 => "Excellent",
            >= 70 => "Good",
            >= 50 => "Fair",
            >= 30 => "Poor",
            _ => "Offline"
        };
    }

    private void UpdateNavButtons()
    {
        HasPreviousChannel = _currentIndex > 0;
        HasNextChannel = _currentIndex < _channelList.Count - 1;
    }

    [RelayCommand]
    private void GoBack()
    {
        _mainVm.NavigateToCommand.Execute("Dashboard");
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        var db = DatabaseService.Instance;

        if (IsFavorite)
        {
            db.DeleteFavorite(Channel.Id);
            IsFavorite = false;
        }
        else
        {
            db.InsertFavorite(new Favorite { ChannelId = Channel.Id });
            IsFavorite = true;
        }
    }

    [RelayCommand]
    private void PreviousChannel()
    {
        if (_currentIndex <= 0) return;
        _currentIndex--;
        NavigateToChannel(_channelList[_currentIndex]);
    }

    [RelayCommand]
    private void NextChannel()
    {
        if (_currentIndex >= _channelList.Count - 1) return;
        _currentIndex++;
        NavigateToChannel(_channelList[_currentIndex]);
    }

    private void NavigateToChannel(Channel channel)
    {
        _mainVm.CurrentView = new PlayerViewModel(_mainVm, channel, _channelList);
    }

    [RelayCommand]
    private void RetryStream()
    {
        var db = DatabaseService.Instance;
        db.UpdateChannelHealth(Channel.Id, -10);

        if (!string.IsNullOrEmpty(Channel.FallbackUrl) && HasError)
        {
            MediaUrl = Channel.FallbackUrl;
            HasError = false;
            UpdateStreamQuality();
        }
    }
}
