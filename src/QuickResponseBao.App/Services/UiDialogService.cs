using System.Windows;
using System.Windows.Controls;

namespace QuickResponseBao.App.Services;

public static class UiDialogService
{
    public static bool Confirm(Window? owner, string title, string message, bool dangerous = true)
    {
        var dialog = new Window { Title = title, Owner = owner, Width = 460, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var root = new Grid { Margin = new Thickness(24) }; root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var content = new StackPanel(); var heading = new TextBlock { Text = title }; heading.SetResourceReference(FrameworkElement.StyleProperty, "QrbSectionTitleTextStyle");
        var body = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 22) }; body.SetResourceReference(FrameworkElement.StyleProperty, "QrbBodyTextStyle"); content.Children.Add(heading); content.Children.Add(body); root.Children.Add(content);
        var actions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right }; var cancel = new System.Windows.Controls.Button { Content = LocalizationService.Get("Cancel"), IsCancel = true }; var confirm = new System.Windows.Controls.Button { Content = LocalizationService.Get("Confirm"), IsDefault = true }; confirm.SetResourceReference(FrameworkElement.StyleProperty, dangerous ? "QrbDangerButtonStyle" : "QrbPrimaryButtonStyle"); confirm.Click += (_, _) => dialog.DialogResult = true; actions.Children.Add(cancel); actions.Children.Add(confirm); Grid.SetRow(actions, 1); root.Children.Add(actions); dialog.Content = root;
        return dialog.ShowDialog() == true;
    }

    public static void ShowFatal(Window? owner, string title, string message, string? details = null)
    {
        var full = string.IsNullOrWhiteSpace(details) ? message : $"{message}\n\n{LocalizationService.Get("TechnicalDetails")}:\n{details}";
        Confirm(owner, title, full, false);
    }
}
