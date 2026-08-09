namespace QuickResponseBao.App.Services;

public enum UiFeedbackSeverity { Success, Information, Warning, Error }

public sealed record UiFeedbackMessage(string Message, UiFeedbackSeverity Severity, string? Details = null, TimeSpan? Duration = null)
{
    public TimeSpan DisplayDuration => Duration ?? (Severity == UiFeedbackSeverity.Error ? TimeSpan.FromSeconds(6) : TimeSpan.FromSeconds(3));
}

public sealed class UiFeedbackService
{
    public event EventHandler<UiFeedbackMessage>? MessageRequested;
    public void Show(string message, UiFeedbackSeverity severity = UiFeedbackSeverity.Success, string? details = null) =>
        MessageRequested?.Invoke(this, new UiFeedbackMessage(message, severity, details));
    public void Success(string message) => Show(message);
    public void Information(string message) => Show(message, UiFeedbackSeverity.Information);
    public void Warning(string message, string? details = null) => Show(message, UiFeedbackSeverity.Warning, details);
    public void Error(string message, string? details = null) => Show(message, UiFeedbackSeverity.Error, details);
}
