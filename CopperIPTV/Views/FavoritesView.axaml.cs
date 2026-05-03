using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CopperIPTV.Models;
using CopperIPTV.ViewModels;

namespace CopperIPTV.Views;

public partial class FavoritesView : UserControl
{
    private FavoritesViewModel? _vm;

    public FavoritesView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is FavoritesViewModel vm)
            _vm = vm;
    }

    private void OnChannelClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Channel channel } && _vm != null)
            _vm.OpenChannelCommand.Execute(channel);
    }
}
