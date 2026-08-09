using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Diagnostics;

namespace QuickResponseBao.App.Views.Pages;

public partial class DiagnosticsPage : Page, IRefreshablePage
{
    private readonly Action<string> _feedback; private readonly DispatcherTimer _timer; private App Runtime => (App)System.Windows.Application.Current;
    public DiagnosticsPage(Action<string> feedback) { InitializeComponent(); _feedback = feedback; _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _timer.Tick += (_, _) => Render(); Loaded += (_, _) => { Render(); _timer.Start(); }; Unloaded += (_, _) => _timer.Stop(); }
    public Task RefreshAsync() { Render(); return Task.CompletedTask; }
    private void Render()
    {
        if (Runtime.Listener is null) return; var x = Runtime.GetDiagnosticSnapshot();
        StatusItems.ItemsSource = new[] { Status("GlobalKeyboardHook", x.HookRunning, x.HookRunning ? "Running" : "Stopped"), Text("CurrentApplication", Empty(x.ForegroundProcess), true), Status("AllowedApplication", x.IsWhitelisted), Status("TextInput", x.TextInputDetected), Text("SecurityState", x.PasswordFieldDetected ? LocalizationService.Get("Restricted") : LocalizationService.Get("Safe"), !x.PasswordFieldDetected), Text("CandidatePosition", LocalizationService.Get($"Position{x.CandidatePosition}"), x.CandidatePosition == CandidatePositionMethod.Caret), Nullable("LastPaste", x.LastPasteSucceeded), Nullable("ClipboardRestored", x.LastClipboardRestored) };
        FallbackBar.IsOpen = x.CandidatePosition != CandidatePositionMethod.Caret; ProcessValue.Text = Empty(x.ForegroundProcess); TitleValue.Text = Empty(x.WindowTitle); BufferValue.Text = x.SearchBufferLength.ToString(); FailureValue.Text = Empty(x.LastFailureReason); LogValue.Text = x.LogDirectory;
        PidValue.Text = LocalizationService.Get("Unavailable"); HandleValue.Text = Empty(x.ConfirmationTargetWindow);
    }
    private DiagnosticStatus Status(string key, bool value, string? valueKey = null) => new(LocalizationService.Get(key), LocalizationService.Get(valueKey ?? (value ? "Yes" : "No")), FindBrush(value ? "QrbSuccessBrush" : "QrbErrorBrush"));
    private DiagnosticStatus Nullable(string key, bool? value) => value is null ? Text(key, LocalizationService.Get("NotTested"), false) : Status(key, value.Value, value.Value ? "Successful" : "Failed");
    private DiagnosticStatus Text(string key, string value, bool good) => new(LocalizationService.Get(key), value, FindBrush(good ? "QrbSuccessBrush" : "QrbWarningBrush"));
    private System.Windows.Media.Brush FindBrush(string key) => (System.Windows.Media.Brush)FindResource(key);
    private async void Candidate_Click(object sender, RoutedEventArgs e) { var owner = Window.GetWindow(this); owner.WindowState = WindowState.Minimized; await Task.Delay(800); Runtime.TestCandidateWindow(); owner.WindowState = WindowState.Normal; }
    private async void Paste_Click(object sender, RoutedEventArgs e) { try { await Runtime.TestPasteAsync(); _feedback(LocalizationService.Get("PasteTestSucceeded")); } catch { _feedback(LocalizationService.Get("PasteTestFailed")); } }
    private void Capture_Click(object sender, RoutedEventArgs e) { var x = Runtime.GetDiagnosticSnapshot(); _feedback(string.Format(LocalizationService.Get("ApplicationCaptured"), Empty(x.ForegroundProcess))); }
    private void Logs_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("explorer.exe", Runtime.Paths.Logs) { UseShellExecute = true });
    private async void Export_Click(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = $"QuickResponseBao-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json" }; if (dialog.ShowDialog(Window.GetWindow(this)) == true) { await new SafeDiagnosticReportService().ExportAsync(dialog.FileName, Runtime.GetDiagnosticSnapshot(), ApplicationVersion.Current); _feedback(LocalizationService.Get("DiagnosticsExported")); } }
    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? LocalizationService.Get("Unavailable") : value;
}
public sealed record DiagnosticStatus(string Label, string Value, System.Windows.Media.Brush Brush);
