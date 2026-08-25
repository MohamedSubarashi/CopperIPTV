using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using CopperIPTV.Models;
using CopperIPTV.ViewModels;

namespace CopperIPTV.Views;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? _vm;

    public DashboardView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DashboardViewModel vm)
            _vm = vm;
    }

    private void OnChannelClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Channel channel } && _vm != null)
            _vm.OpenChannelCommand.Execute(channel);
    }

    private void OnCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string category } && _vm != null)
            _vm.SelectedCategory = category;
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
