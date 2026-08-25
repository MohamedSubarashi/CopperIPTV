using Avalonia.Controls;
using CopperIPTV.ViewModels;

namespace CopperIPTV.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
        DataContext = new LogViewModel();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        (DataContext as LogViewModel)?.Detach();
        base.OnDetachedFromVisualTree(e);
    }
}
