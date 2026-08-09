using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;

namespace QuickResponseBao.App.Views.Pages;

public partial class SettingsPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel; private readonly Action<string> _feedback; private bool _loading;
    private App Runtime => (App)System.Windows.Application.Current;
    public string CurrentVersionLabel => $"{LocalizationService.Get("CurrentVersion")}: {ApplicationVersion.Current}";
    public SettingsPage(MainViewModel viewModel, Action<string> feedback) { InitializeComponent(); DataContext = _viewModel = viewModel; _feedback = feedback; }
    public Task RefreshAsync() { _loading = true; Select(ThemeBox, _viewModel.Settings.Theme); Select(LanguageBox, _viewModel.Settings.Language); _loading = false; return Task.CompletedTask; }
    private static void Select(System.Windows.Controls.ComboBox box, string value) => box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase));
    private void ImmediateSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || System.Windows.Application.Current is not App runtime) return;
        if ((ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() is { } theme) { _viewModel.Settings.Theme = theme; runtime.ThemeService.Apply(theme); }
        if ((LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() is { } language && language != _viewModel.Settings.Language) { _viewModel.Settings.Language = language; LocalizationService.Apply(language); }
        SaveState.SetResourceReference(TextBlock.TextProperty, "UnsavedChanges");
    }
    private async void Save_Click(object sender, RoutedEventArgs e) { _viewModel.Settings.Normalize(); await Runtime.SaveSettingsAsync(_viewModel.Settings); SaveState.SetResourceReference(TextBlock.TextProperty, "Saved"); _feedback(LocalizationService.Get("SettingsSaved")); }
    private async void TestPaste_Click(object sender, RoutedEventArgs e) { try { await Runtime.TestPasteAsync(); _feedback(LocalizationService.Get("PasteTestSucceeded")); } catch { _feedback(LocalizationService.Get("PasteTestFailed")); } }
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await Runtime.MainAppWindow.ShowUpdateWindowAsync();
    private void Data_Click(object sender, RoutedEventArgs e) => Open(Runtime.Paths.Root);
    private void Logs_Click(object sender, RoutedEventArgs e) => Open(Runtime.Paths.Logs);
    private void Backups_Click(object sender, RoutedEventArgs e) => new BackupManagerWindow(Runtime.BackupService) { Owner = Window.GetWindow(this) }.ShowDialog();
    private static void Open(string path) => Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
}
