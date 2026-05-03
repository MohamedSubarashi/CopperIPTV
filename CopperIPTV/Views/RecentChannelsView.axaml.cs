using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
}
