using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.App.Views.Pages;

public partial class ApplicationsPage : Page, IRefreshablePage
{
    private readonly Action<string> _feedback; private readonly ObservableCollection<ApplicationEntry> _items = [];
    private App Runtime => (App)System.Windows.Application.Current;
    public ApplicationsPage(Action<string> feedback) { InitializeComponent(); _feedback = feedback; ApplicationsList.ItemsSource = _items; }
    public Task RefreshAsync() { _items.Clear(); foreach (var name in Runtime.Settings.AllowedProcesses) _items.Add(Create(name, true)); UpdateEmpty(); return Task.CompletedTask; }
    private async void Running_Click(object sender, RoutedEventArgs e)
    {
        var running = Process.GetProcesses().Select(p => { try { return p.MainWindowHandle != 0 ? $"{p.ProcessName}.exe" : null; } catch { return null; } finally { p.Dispose(); } })
            .Where(x => x is not null).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var dialog = new Window { Title = LocalizationService.Get("AddRunningApplication"), Owner = Window.GetWindow(this), Width = 480, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var list = new System.Windows.Controls.ListBox { ItemsSource = running, Margin = new Thickness(16) }; list.MouseDoubleClick += (_, _) => dialog.DialogResult = list.SelectedItem is not null;
        dialog.Content = list; if (dialog.ShowDialog() == true && list.SelectedItem is string process) await AddAsync(process);
    }
    private async void Browse_Click(object sender, RoutedEventArgs e) { var d = new Microsoft.Win32.OpenFileDialog { Filter = "Application (*.exe)|*.exe" }; if (d.ShowDialog(Window.GetWindow(this)) == true) await AddAsync(Path.GetFileName(d.FileName)); }
    private async void Remove_Click(object sender, RoutedEventArgs e) { if ((sender as System.Windows.Controls.Button)?.Tag is ApplicationEntry item) { _items.Remove(item); await PersistAsync(); _feedback(LocalizationService.Get("ApplicationRemoved")); } }
    private async void Enabled_Click(object sender, RoutedEventArgs e) => await PersistAsync();
    private async Task AddAsync(string process)
    {
        if (ProcessWhitelist.Contains(_items.Select(x => x.ProcessName), process)) { _feedback(LocalizationService.Get("ApplicationAlreadyAllowed")); return; }
        _items.Add(Create(process, true)); await PersistAsync(); _feedback(string.Format(LocalizationService.Get("ApplicationCaptured"), process));
    }
    private async Task PersistAsync() { Runtime.Settings.AllowedProcesses = _items.Where(x => x.Enabled).Select(x => x.ProcessName).ToList(); await Runtime.SaveSettingsAsync(Runtime.Settings); UpdateEmpty(); }
    private void UpdateEmpty() => EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    private static ApplicationEntry Create(string process, bool enabled) => new(DisplayName(process), process, enabled);
    private static string DisplayName(string process) => process.ToLowerInvariant() switch { "lark.exe" => "Lark", "telegram.exe" => "Telegram", "discord.exe" => "Discord", "chrome.exe" => "Google Chrome", "msedge.exe" => "Microsoft Edge", _ => Path.GetFileNameWithoutExtension(process) };
}

public sealed class ApplicationEntry(string displayName, string processName, bool enabled)
{
    public string DisplayName { get; } = displayName; public string ProcessName { get; } = processName; public bool Enabled { get; set; } = enabled;
}
