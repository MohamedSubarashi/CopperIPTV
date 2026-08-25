using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using CopperIPTV.Models;
using CopperIPTV.ViewModels;

namespace CopperIPTV.Views;

public partial class RecentChannelsView : UserControl
{
    private RecentChannelsViewModel? _vm;

    public RecentChannelsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is RecentChannelsViewModel vm)
            _vm = vm;
    }

    private void OnChannelClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Channel channel } && _vm != null)
            _vm.OpenChannelCommand.Execute(channel);
    }

    private void OnMenuOpenClick(object? sender, RoutedEventArgs e)
    {
        if (_vm != null && ResolveChannel(sender) is { } channel)
            _vm.OpenChannelCommand.Execute(channel);
    }

    private void OnMenuDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_vm != null && ResolveChannel(sender) is { } channel)
            _vm.DeleteChannelCommand.Execute(channel);
    }

    private void OnMenuToggleFavoriteClick(object? sender, RoutedEventArgs e)
    {
        if (_vm != null && ResolveChannel(sender) is { } channel)
            _vm.ToggleFavoriteCommand.Execute(channel);
    }

    // ContextMenu items inherit the placement target's DataContext; fall back
    // to the target itself in case inheritance is unavailable.
    private static Channel? ResolveChannel(object? sender)
    {
        if (sender is not MenuItem mi) return null;
        if (mi.DataContext is Channel ch) return ch;

        var target = mi.GetLogicalParent<ContextMenu>()?.PlacementTarget as Button;
        return target?.DataContext as Channel;
    }
}
