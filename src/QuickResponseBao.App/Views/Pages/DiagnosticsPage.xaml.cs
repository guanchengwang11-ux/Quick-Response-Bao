using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using QuickResponseBao.App.Services;
using QuickResponseBao.Infrastructure.Diagnostics;

namespace QuickResponseBao.App.Views.Pages;

public partial class DiagnosticsPage : Page, IRefreshablePage
{
    private readonly Action<string> _feedback; private readonly DispatcherTimer _timer; private App Runtime => (App)System.Windows.Application.Current;
    public DiagnosticsPage(Action<string> feedback)
    {
        InitializeComponent(); _feedback = feedback; _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Render(); Loaded += (_, _) => { Render(); _timer.Start(); }; Unloaded += (_, _) => _timer.Stop();
    }
    public Task RefreshAsync() { Render(); return Task.CompletedTask; }
    private void Render()
    {
        if (Runtime.Listener is null) return; var x = Runtime.GetDiagnosticSnapshot();
        ProcessValue.Text = Empty(x.ForegroundProcess); TitleValue.Text = Empty(x.WindowTitle); WhitelistValue.Text = YesNo(x.IsWhitelisted);
        HookValue.Text = YesNo(x.HookRunning); InputValue.Text = YesNo(x.TextInputDetected); BufferValue.Text = x.SearchBufferLength.ToString();
        PositionValue.Text = LocalizationService.Get($"Position{x.CandidatePosition}"); PasteValue.Text = x.LastPasteSucceeded is null ? LocalizationService.Get("NotTested") : YesNo(x.LastPasteSucceeded.Value);
        ClipboardValue.Text = x.LastClipboardRestored is null ? LocalizationService.Get("NotTested") : YesNo(x.LastClipboardRestored.Value); FailureValue.Text = Empty(x.LastFailureReason);
    }
    private async void Candidate_Click(object sender, RoutedEventArgs e) { var owner = Window.GetWindow(this); owner.WindowState = WindowState.Minimized; await Task.Delay(1500); Runtime.TestCandidateWindow(); owner.WindowState = WindowState.Normal; }
    private async void Paste_Click(object sender, RoutedEventArgs e) { var owner = Window.GetWindow(this); owner.WindowState = WindowState.Minimized; try { await Task.Delay(1500); await Runtime.TestPasteAsync(); _feedback(LocalizationService.Get("Succeeded")); } catch (Exception ex) { _feedback(ex.Message); } finally { owner.WindowState = WindowState.Normal; } }
    private void Logs_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("explorer.exe", Runtime.Paths.Logs) { UseShellExecute = true });
    private async void Export_Click(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = $"QuickResponseBao-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json" }; if (dialog.ShowDialog(Window.GetWindow(this)) == true) { await new SafeDiagnosticReportService().ExportAsync(dialog.FileName, Runtime.GetDiagnosticSnapshot(), ApplicationVersion.Current); _feedback(LocalizationService.Get("DiagnosticsExported")); } }
    private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? LocalizationService.Get("Unavailable") : value;
    private static string YesNo(bool value) => LocalizationService.Get(value ? "Yes" : "No");
}
