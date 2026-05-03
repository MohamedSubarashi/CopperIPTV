using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CopperIPTV.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.HandleKey(e.Key);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.HandleKey(e.Key);
        }
    }
}
