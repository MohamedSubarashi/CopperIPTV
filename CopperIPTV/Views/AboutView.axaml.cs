using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CopperIPTV.ViewModels;

namespace CopperIPTV.Views;

public partial class AboutView : UserControl
{
    private const string GitHubUrl = "https://github.com/MohamedSubarashi/CopperIPTV";

    public AboutView()
    {
        InitializeComponent();
    }

    private void OnDonateClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl(AboutViewModel.DonationUrl);
    }

    private void OnGitHubClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl(GitHubUrl);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
