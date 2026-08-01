using System.Windows;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.App;
public partial class CategoryChoiceWindow : Window
{
    public CategoryChoiceWindow(IReadOnlyList<CategoryInfo> categories) { InitializeComponent(); CategoryBox.ItemsSource = categories; CategoryBox.SelectedIndex = categories.Count > 0 ? 0 : -1; }
    public CategoryInfo? SelectedCategory => CategoryBox.SelectedItem as CategoryInfo;
    private void Save_Click(object sender, RoutedEventArgs e) { if (SelectedCategory is not null) DialogResult = true; }
}
