using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperIPTV.Models;

namespace CopperIPTV.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private Action<Key>? _playerKeyHandler;

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private bool _isPlayerActive;

    public MainViewModel()
    {
        _currentView = new DashboardViewModel(this);
        _isPlayerActive = false;
    }

    partial void OnCurrentViewChanged(ViewModelBase value)
    {
        IsPlayerActive = value is PlayerViewModel;
        if (!IsPlayerActive)
            _playerKeyHandler = null;
    }

    public void RegisterPlayerKeyHandler(Action<Key> handler) => _playerKeyHandler = handler;
    public void UnregisterPlayerKeyHandler() => _playerKeyHandler = null;

    public void HandleKey(Key key)
    {
        if (IsPlayerActive && _playerKeyHandler != null)
        {
            _playerKeyHandler(key);
        }
    }

    [RelayCommand]
    public void NavigateTo(string viewName)
    {
        CurrentView = viewName switch
        {
            "Dashboard" => new DashboardViewModel(this),
            "Favorites" => new FavoritesViewModel(this),
            "RecentChannels" => new RecentChannelsViewModel(this),
            "Settings" => new SettingsViewModel(this),
            "About" => new AboutViewModel(),
            _ => CurrentView
        };
    }

    public void NavigateToPlayer(Channel channel, List<Channel>? channelList = null)
    {
        CurrentView = new PlayerViewModel(this, channel, channelList);
    }
}
