using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CopperIPTV.ViewModels;
using CopperIPTV.Views;

namespace CopperIPTV;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null) return null;

        var name = param.GetType().Name.Replace("ViewModel", "");
        var type = Type.GetType($"CopperIPTV.Views.{name}View");

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock
        {
            Text = "Not Found: " + param.GetType().Name,
            Foreground = Avalonia.Media.Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize = 14
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
