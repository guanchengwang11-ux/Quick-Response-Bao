using System.Diagnostics;
using System.Net.Http;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Updates;
using QuickResponseBao.Infrastructure.ImportExport;
using Microsoft.Win32;

namespace QuickResponseBao.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private App Runtime => (App)System.Windows.Application.Current;
    public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = _viewModel = viewModel; }
    public async Task InitializeAsync(AppSettings settings)
    {
        _viewModel.Settings = settings; WhitelistText.Text = string.Join(Environment.NewLine, settings.AllowedProcesses);
        await _viewModel.RefreshAsync(); UpdateListenerDisplay();
    }
    public Task RefreshAsync() => _viewModel.RefreshAsync();
    public void OpenSettings() => MainTabs.SelectedIndex = 2;
    public void AddResponse() => Add_Click(this, new RoutedEventArgs());

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ResponseEditorWindow { Owner = this };
        if (editor.ShowDialog() == true) { await Runtime.Repository.UpsertAsync(editor.Response); await ChangedAsync("Response saved."); }
    }
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } selected) return;
        var editor = new ResponseEditorWindow(selected) { Owner = this };
        if (editor.ShowDialog() == true) { await Runtime.Repository.UpsertAsync(editor.Response); await ChangedAsync("Response updated."); }
    }
    private async void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return;
        var copy = new QuickResponse { Summary = $"{x.Summary} (copy)", Content = x.Content, Keywords = [.. x.Keywords], Category = x.Category, Language = x.Language, IsEnabled = x.IsEnabled, SortOrder = x.SortOrder };
        await Runtime.Repository.UpsertAsync(copy); await ChangedAsync("Response duplicated.");
    }
    private async void ToggleResponse_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return; x.IsEnabled = !x.IsEnabled;
        await Runtime.Repository.UpsertAsync(x); await ChangedAsync(x.IsEnabled ? "Response enabled." : "Response disabled.");
    }
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return;
        if (System.Windows.MessageBox.Show($"Delete ‘{x.Summary}’?", "Quick Response Bao", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Runtime.Repository.DeleteAsync(x.Id); await ChangedAsync("Response deleted.");
    }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV or JSON|*.csv;*.json|CSV|*.csv|JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            FeedbackText.Text = "Importing…"; var files = new QuickResponseFileService();
            var items = Path.GetExtension(dialog.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? await files.ImportJsonAsync(dialog.FileName) : await files.ImportCsvAsync(dialog.FileName);
            foreach (var item in items) await Runtime.Repository.UpsertAsync(item);
            await ChangedAsync($"Imported {items.Count} responses.");
        }
        catch (Exception ex) { FeedbackText.Text = $"Import failed: {ex.Message}"; }
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json|CSV|*.csv", FileName = "quick-responses.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var items = await Runtime.Repository.GetAllAsync(); var files = new QuickResponseFileService();
            if (Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)) await files.ExportCsvAsync(dialog.FileName, items);
            else await files.ExportJsonAsync(dialog.FileName, items);
            FeedbackText.Text = $"Exported {items.Count} responses.";
        }
        catch (Exception ex) { FeedbackText.Text = $"Export failed: {ex.Message}"; }
    }
    private async Task ChangedAsync(string feedback) { await Runtime.ReloadCacheAsync(); await _viewModel.RefreshAsync(); FeedbackText.Text = feedback; }

    private void ToggleListener_Click(object sender, RoutedEventArgs e) { if (Runtime.Listener.IsRunning) Runtime.PauseListener(); else Runtime.TryStartListener(); UpdateListenerDisplay(); }
    private void UpdateListenerDisplay()
    {
        if (Runtime.Listener?.IsRunning == true) { ListenerStatus.SetResourceReference(TextBlock.TextProperty, "ListenerEnabled"); ListenerButton.SetResourceReference(ContentControl.ContentProperty, "Pause"); ListenerStatus.Foreground = System.Windows.Media.Brushes.ForestGreen; }
        else { ListenerStatus.SetResourceReference(TextBlock.TextProperty, "ListenerPaused"); ListenerButton.SetResourceReference(ContentControl.ContentProperty, "Resume"); ListenerStatus.Foreground = System.Windows.Media.Brushes.DarkGoldenrod; }
    }
    private async void SwitchLanguage_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.Language = _viewModel.Settings.Language == "en-US" ? "zh-CN" : "en-US";
        LocalizationService.Apply(_viewModel.Settings.Language); await Runtime.SaveSettingsAsync(_viewModel.Settings); FeedbackText.Text = "Language updated.";
    }
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.AllowedProcesses = WhitelistText.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        try { await Runtime.SaveSettingsAsync(_viewModel.Settings); FeedbackText.Text = "Settings saved."; }
        catch (Exception ex) { FeedbackText.Text = $"Settings could not be saved: {ex.Message}"; }
    }
    private void OpenRepository_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/guanchengwang11-ux/Quick-Response-Bao") { UseShellExecute = true });
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        FeedbackText.Text = "Checking for updates…";
        try
        {
            var update = await new GitHubUpdateService(new HttpClient()).CheckAsync("1.0.0");
            FeedbackText.Text = update is null ? (_viewModel.Settings.Language == "en-US" ? "You’re using the latest version." : "当前已经是最新版本。") : $"Version {update.Version} is available.";
        }
        catch (Exception ex) { FeedbackText.Text = $"Update check failed: {ex.Message}"; }
    }
}
