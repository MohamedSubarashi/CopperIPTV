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
        RecentChannels = new ObservableCollection<Channel>(channels);
        HasRecentChannels = channels.Count > 0;
    }

    [RelayCommand]
    private void OpenChannel(Channel channel)
    {
        var allChannels = DatabaseService.Instance.GetAllChannels();
        _mainVm.NavigateToPlayer(channel, allChannels);
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadRecentChannels();
    }
}
