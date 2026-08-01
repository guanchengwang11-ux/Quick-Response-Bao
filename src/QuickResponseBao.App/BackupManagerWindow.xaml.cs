using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Interfaces;

namespace QuickResponseBao.App;

public partial class BackupManagerWindow : Window
{
    private readonly IDatabaseBackupService _service;
    private readonly ObservableCollection<DatabaseBackupInfo> _backups = [];
    public BackupManagerWindow(IDatabaseBackupService service) { InitializeComponent(); _service = service; BackupsGrid.ItemsSource = _backups; Loaded += async (_, _) => await RefreshAsync(); }
    public bool DatabaseRestored { get; private set; }
    private async Task RefreshAsync() { _backups.Clear(); foreach (var item in await _service.GetBackupsAsync()) _backups.Add(item); }
    private async Task RunAsync(Func<Task<string>> action)
    {
        Actions.IsEnabled = false; Feedback.Text = LocalizationService.Get("Loading");
        try { Feedback.Text = await action(); await RefreshAsync(); }
        catch (Exception ex) { Feedback.Text = $"{LocalizationService.Get("OperationFailed")}: {ex.Message}"; }
        finally { Actions.IsEnabled = true; }
    }
    private async void Create_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var backup = await _service.CreateBackupAsync(); return $"{LocalizationService.Get("BackupCreated")} {backup.Path} ({backup.Size:N0} bytes)";
    });
    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (BackupsGrid.SelectedItem is not DatabaseBackupInfo item) { Feedback.Text = LocalizationService.Get("NoSelection"); return; }
        await RestoreAsync(item.Path);
    }
    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Quick Response Bao database|*.db|All files|*.*" }; if (dialog.ShowDialog(this) == true) await RestoreAsync(dialog.FileName);
    }
    private async Task RestoreAsync(string path)
    {
        if (System.Windows.MessageBox.Show(LocalizationService.Get("ConfirmRestore"), LocalizationService.Get("Backups"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            var result = await _service.RestoreAsync(path); DatabaseRestored |= result.Succeeded;
            if (!result.Succeeded) throw new InvalidOperationException(result.Message);
            return $"{LocalizationService.Get("RestoreSucceeded")} {LocalizationService.Get("Location")}: {result.SafetyBackupPath}";
        });
    }
}
