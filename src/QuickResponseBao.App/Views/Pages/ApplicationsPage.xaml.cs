using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.Infrastructure.Windows;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.App.Views.Pages;

public partial class ApplicationsPage : Page, IRefreshablePage
{
    private readonly Action<string> _feedback; private App Runtime => (App)System.Windows.Application.Current;
    public ApplicationsPage(Action<string> feedback) { InitializeComponent(); _feedback = feedback; }
    public Task RefreshAsync() { WhitelistText.Text = string.Join(Environment.NewLine, Runtime.Settings.AllowedProcesses); return Task.CompletedTask; }
    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        CaptureButton.IsEnabled = false; var owner = Window.GetWindow(this); owner.WindowState = WindowState.Minimized;
        try { await Task.Delay(1800); var process = Runtime.Listener.InspectEnvironment().ProcessName; if (string.IsNullOrWhiteSpace(process)) _feedback(LocalizationService.Get("CaptureApplicationFailed")); else await AddProcessAsync(process); }
        catch (Exception ex) { _feedback($"{LocalizationService.Get("OperationFailed")}: {ex.Message}"); }
        finally { owner.WindowState = WindowState.Normal; owner.Activate(); CaptureButton.IsEnabled = true; }
    }
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses()) { try { if (process.MainWindowHandle != 0) names.Add($"{process.ProcessName}.exe"); } catch { } finally { process.Dispose(); } }
        ProcessesBox.ItemsSource = names.OrderBy(x => x).ToList(); if (ProcessesBox.Items.Count > 0) ProcessesBox.SelectedIndex = 0;
    }
    private async void Add_Click(object sender, RoutedEventArgs e) { if (ProcessesBox.SelectedItem is string process) await AddProcessAsync(process); }
    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync();
    private async Task AddProcessAsync(string process)
    {
        var values = Values(); if (ProcessWhitelist.Contains(values, process)) { _feedback(LocalizationService.Get("ApplicationAlreadyAllowed")); return; }
        values.Add(process); WhitelistText.Text = string.Join(Environment.NewLine, values); await SaveAsync(); _feedback(string.Format(LocalizationService.Get("ApplicationCaptured"), process));
    }
    private List<string> Values() => WhitelistText.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    private async Task SaveAsync() { Runtime.Settings.AllowedProcesses = Values(); await Runtime.SaveSettingsAsync(Runtime.Settings); _feedback(LocalizationService.Get("WhitelistSaved")); }
}
