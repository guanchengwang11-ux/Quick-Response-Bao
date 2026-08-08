using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;

namespace QuickResponseBao.App.Views.Pages;

public partial class SettingsPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel; private readonly Action<string> _feedback; private App Runtime => (App)System.Windows.Application.Current;
    public SettingsPage(MainViewModel viewModel, Action<string> feedback) { InitializeComponent(); DataContext = _viewModel = viewModel; _feedback = feedback; }
    public Task RefreshAsync() { SelectTheme(); return Task.CompletedTask; }
    private void SelectTheme() => ThemeBox.SelectedItem = ThemeBox.Items.Cast<ComboBoxItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), _viewModel.Settings.Theme, StringComparison.OrdinalIgnoreCase));
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if ((ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() is { } theme) _viewModel.Settings.Theme = theme;
        await Runtime.SaveSettingsAsync(_viewModel.Settings); _feedback(LocalizationService.Get("SettingsSaved"));
    }
    private async void Language_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.Language = _viewModel.Settings.Language == "en-US" ? "zh-CN" : "en-US";
        LocalizationService.Apply(_viewModel.Settings.Language); await Runtime.SaveSettingsAsync(_viewModel.Settings); _feedback(LocalizationService.Get("LanguageUpdated"));
    }
    private void Backups_Click(object sender, RoutedEventArgs e) => new BackupManagerWindow(Runtime.BackupService) { Owner = Window.GetWindow(this) }.ShowDialog();
}
