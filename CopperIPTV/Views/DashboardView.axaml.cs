using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
}
