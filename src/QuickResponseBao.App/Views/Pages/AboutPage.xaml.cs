using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.Infrastructure.Updates;

namespace QuickResponseBao.App.Views.Pages;

public partial class AboutPage : Page, IRefreshablePage
{
    private readonly Func<UpdateCheckResult?, bool, Task> _showUpdate; private readonly Action<string> _feedback;
    public AboutPage(Func<UpdateCheckResult?, bool, Task> showUpdate, Action<string> feedback) { InitializeComponent(); _showUpdate = showUpdate; _feedback = feedback; VersionValue.Text = $"{LocalizationService.Get("Version")} {ApplicationVersion.Current}"; }
    public Task RefreshAsync() { VersionValue.Text = $"{LocalizationService.Get("Version")} {ApplicationVersion.Current}"; return Task.CompletedTask; }
    private void Repository_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/guanchengwang11-ux/Quick-Response-Bao") { UseShellExecute = true });
    private async void Update_Click(object sender, RoutedEventArgs e) { try { await _showUpdate(null, false); } catch (Exception ex) { _feedback($"{LocalizationService.Get("UpdateCheckFailed")}: {ex.Message}"); } }
}
