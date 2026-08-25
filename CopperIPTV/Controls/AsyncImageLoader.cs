using System;
using System.Collections.Concurrent;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace CopperIPTV.Controls;

// Avalonia cannot download remote images via a plain string binding to
// Image.Source, so channel logos from http(s) URLs never rendered. This
// attached property downloads the image asynchronously and assigns it to
// the target Image on the UI thread, with an in-memory cache.
public static class AsyncImageLoader
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
    private static readonly AttachedProperty<object?> _loadTokenProperty =
        AvaloniaProperty.RegisterAttached<Image, object?>("LoadToken", typeof(AsyncImageLoader));

    public static readonly AttachedProperty<string?> SourceProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>(
            "Source", typeof(AsyncImageLoader));

    public static string? GetSource(Image image) => image.GetValue(SourceProperty);
    public static void SetSource(Image image, string? value) => image.SetValue(SourceProperty, value);

    private static void OnSourceChanged(Image image, AvaloniaPropertyChangedEventArgs e)
    {
        var url = e.NewValue as string;

        // Invalidate any in-flight load for this image instance.
        image.SetValue(_loadTokenProperty, new object());

        if (string.IsNullOrWhiteSpace(url))
        {
            image.Source = null;
            return;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // Local/resource path - let Avalonia resolve it directly.
            try { image.Source = new Bitmap(url); } catch { image.Source = null; }
            return;
        }

        var token = image.GetValue(_loadTokenProperty);
        _ = LoadAsync(image, url, token);
    }

    private static async System.Threading.Tasks.Task LoadAsync(Image image, string url, object? token)
    {
        Bitmap? bitmap;
        try
        {
            bitmap = _cache.GetOrAdd(url, static u => Download(u));
        }
        catch
        {
            bitmap = null;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Ignore stale results (image reused/recycled for another channel).
            if (!ReferenceEquals(image.GetValue(_loadTokenProperty), token)) return;
            image.Source = bitmap;
        });
    }

    private static Bitmap? Download(string url)
    {
        try
        {
            using var stream = _client.GetStreamAsync(url).GetAwaiter().GetResult();
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
