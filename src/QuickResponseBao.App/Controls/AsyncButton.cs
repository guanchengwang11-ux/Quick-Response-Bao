using System.Windows;
using WpfButton = System.Windows.Controls.Button;

namespace QuickResponseBao.App.Controls;

/// <summary>A button contract that exposes a visual-only loading state for asynchronous commands.</summary>
public sealed class AsyncButton : WpfButton
{
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(AsyncButton), new PropertyMetadata(false, OnLoadingChanged));

    public static readonly DependencyProperty LoadingContentProperty = DependencyProperty.Register(
        nameof(LoadingContent), typeof(object), typeof(AsyncButton), new PropertyMetadata("Loading…"));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public object LoadingContent
    {
        get => GetValue(LoadingContentProperty);
        set => SetValue(LoadingContentProperty, value);
    }

    private static void OnLoadingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is AsyncButton button) button.IsEnabled = !(bool)args.NewValue;
    }
}
