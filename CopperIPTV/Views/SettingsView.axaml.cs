using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CopperIPTV.ViewModels;

namespace CopperIPTV.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _vm;

    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsViewModel vm)
            _vm = vm;
    }

    private void OnRemovePlaylist(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PlaylistInfo playlist } && _vm != null)
            _vm.RemovePlaylistCommand.Execute(playlist.Id);
    }

    private void OnRefreshPlaylist(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PlaylistInfo playlist } && _vm != null)
            _vm.RefreshPlaylistCommand.Execute(playlist.Id);
    }

    private void OnRefreshXtream(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: XtreamAccountInfo account } && _vm != null)
            _vm.RefreshXtreamAccountCommand.Execute(account.Id);
    }

    private void OnRemoveXtream(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: XtreamAccountInfo account } && _vm != null)
            _vm.RemoveXtreamAccountCommand.Execute(account.Id);
    }
}
