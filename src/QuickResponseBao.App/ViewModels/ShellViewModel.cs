using QuickResponseBao.App.Services;

namespace QuickResponseBao.App.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private bool _isListening;
    private string _currentRoute = ShellRoutes.Dashboard;

    public bool IsListening { get => _isListening; private set => Set(ref _isListening, value); }
    public string CurrentRoute { get => _currentRoute; private set => Set(ref _currentRoute, value); }

    public void UpdateListener(bool isListening) => IsListening = isListening;
    public void Navigate(string route) => CurrentRoute = ShellRoutes.Normalize(route);
}
