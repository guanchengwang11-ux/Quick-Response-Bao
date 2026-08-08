using System.Windows;
using QuickResponseBao.App.Services;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace QuickResponseBao.App;

public partial class CategoryDeleteWindow : FluentWindow
{
    public CategoryDeleteWindow(string categoryName, int responseCount)
    {
        InitializeComponent();
        CategoryNameText.Text = categoryName;
        MessageText.Text = responseCount > 0
            ? string.Format(LocalizationService.Get("CategoryContainsResponses"), responseCount)
            : LocalizationService.Get("CategoryEmptyDeleteHint");
        DestinationBox.ItemsSource = new[] { LocalizationService.Get("UncategorizedCategory") };
        DestinationBox.SelectedIndex = 0;
        MovePanel.Visibility = responseCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
