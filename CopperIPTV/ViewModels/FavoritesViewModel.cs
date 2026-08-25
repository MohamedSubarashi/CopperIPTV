using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperIPTV.Models;
using CopperIPTV.Services;

namespace CopperIPTV.ViewModels;

public partial class FavoritesViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private ObservableCollection<Channel> _allFavorites = [];

    [ObservableProperty]
    private ObservableCollection<Channel> _favoriteChannels = [];

    [ObservableProperty]
    private bool _hasFavorites;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public FavoritesViewModel(MainViewModel mainVm)
    {
        _mainVm = mainVm;
        LoadFavorites();
    }

    private void LoadFavorites()
    {
        var db = DatabaseService.Instance;
        var favorites = db.GetFavorites();
        var favIds = new HashSet<string>(favorites.Select(f => f.ChannelId));

        var allChannels = db.GetAllChannels().Where(c => favIds.Contains(c.Id)).ToList();
        AllFavorites = new ObservableCollection<Channel>(allChannels);
        HasFavorites = allChannels.Count > 0;
        ApplySearch();
    }

    partial void OnSearchQueryChanged(string value) => ApplySearch();

    private void ApplySearch()
    {
        var filtered = string.IsNullOrEmpty(SearchQuery)
            ? AllFavorites.ToList()
            : AllFavorites.Where(c => c.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

        FavoriteChannels = new ObservableCollection<Channel>(filtered);
    }

    [RelayCommand]
    private void OpenChannel(Channel channel)
    {
        _mainVm.NavigateToPlayer(channel, FavoriteChannels.ToList());
    }

    [RelayCommand]
    private void DeleteChannel(Channel channel)
    {
        DatabaseService.Instance.DeleteChannel(channel.Id);
        LoadFavorites();
    }

    [RelayCommand]
    private void ToggleFavorite(Channel channel)
    {
        DatabaseService.Instance.DeleteFavorite(channel.Id);
        LoadFavorites();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadFavorites();
    }
}
