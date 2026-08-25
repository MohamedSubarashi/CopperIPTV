using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperIPTV.Models;
using CopperIPTV.Services;

namespace CopperIPTV.ViewModels;

public partial class RecentChannelsViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private ObservableCollection<Channel> _recentChannels = [];

    [ObservableProperty]
    private bool _hasRecentChannels;

    public RecentChannelsViewModel(MainViewModel mainVm)
    {
        _mainVm = mainVm;
        LoadRecentChannels();
    }

    private void LoadRecentChannels()
    {
        var db = DatabaseService.Instance;
        var channels = db.GetRecentChannels(20);
        var favIds = db.GetFavoriteIds();
        foreach (var ch in channels)
            ch.IsFavorite = favIds.Contains(ch.Id);

        RecentChannels = new ObservableCollection<Channel>(channels);
        HasRecentChannels = channels.Count > 0;
    }

    [RelayCommand]
    private void OpenChannel(Channel channel)
    {
        _mainVm.NavigateToPlayer(channel, RecentChannels.ToList());
    }

    [RelayCommand]
    private void DeleteChannel(Channel channel)
    {
        DatabaseService.Instance.DeleteChannel(channel.Id);
        LoadRecentChannels();
    }

    [RelayCommand]
    private void ToggleFavorite(Channel channel)
    {
        var db = DatabaseService.Instance;
        if (channel.IsFavorite)
            db.DeleteFavorite(channel.Id);
        else
            db.InsertFavorite(new Favorite { ChannelId = channel.Id });
        LoadRecentChannels();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadRecentChannels();
    }
}
