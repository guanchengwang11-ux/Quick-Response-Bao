using System.Windows;
using System.Windows.Controls;

namespace QuickResponseBao.App.Controls;

public static class ResponsiveGrid
{
    public static readonly DependencyProperty IsSettingRowProperty = DependencyProperty.RegisterAttached(
        "IsSettingRow", typeof(bool), typeof(ResponsiveGrid), new PropertyMetadata(false, Changed));
    public static void SetIsSettingRow(DependencyObject element, bool value) => element.SetValue(IsSettingRowProperty, value);
    public static bool GetIsSettingRow(DependencyObject element) => (bool)element.GetValue(IsSettingRowProperty);
    private static void Changed(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not Grid grid || args.NewValue is not true || grid.ColumnDefinitions.Count > 0) return;
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 280 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    }
}
