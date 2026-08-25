using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperIPTV.Models;
using CopperIPTV.Services;

namespace CopperIPTV.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    [ObservableProperty]
    private ObservableCollection<Channel> _allChannels = [];

    [ObservableProperty]
    private ObservableCollection<Channel> _filteredChannels = [];

    [ObservableProperty]
    private bool _hasChannels;

    public DashboardViewModel(MainViewModel mainVm)
    {
        _mainVm = mainVm;
        LoadChannels();
    }

    private void LoadChannels()
    {
        var db = DatabaseService.Instance;
        var channels = db.GetAllChannels();
        var favIds = db.GetFavoriteIds();
        foreach (var ch in channels)
            ch.IsFavorite = favIds.Contains(ch.Id);

        AllChannels = new ObservableCollection<Channel>(channels);
        HasChannels = channels.Count > 0;

        var cats = channels.Select(c => c.Group.Trim())
                           .Where(g => !string.IsNullOrWhiteSpace(g))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .OrderBy(g => g)
                           .ToList();
        Categories = new ObservableCollection<string>(["All", .. cats]);
        SelectedCategory = "All";
        ApplyFilters();
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filtered = AllChannels
            .Where(c => (SelectedCategory == "All" || c.Group.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
                     && (string.IsNullOrEmpty(SearchQuery) || c.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        FilteredChannels = new ObservableCollection<Channel>(filtered);
    }

    [RelayCommand]
    private void OpenChannel(Channel channel)
    {
        _mainVm.NavigateToPlayer(channel, FilteredChannels.ToList());
    }

    [RelayCommand]
    private void DeleteChannel(Channel channel)
    {
        DatabaseService.Instance.DeleteChannel(channel.Id);
        LoadChannels();
    }

    [RelayCommand]
    private void ToggleFavorite(Channel channel)
    {
        var db = DatabaseService.Instance;
        if (channel.IsFavorite)
            db.DeleteFavorite(channel.Id);
        else
            db.InsertFavorite(new Favorite { ChannelId = channel.Id });
        LoadChannels();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadChannels();
    }
}
