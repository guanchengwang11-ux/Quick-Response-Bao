using System.Windows;
using Microsoft.Win32;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.App.Services;

public sealed class ThemeService : IDisposable
{
    private readonly System.Windows.Application _application;
    private string _preference = ThemeMode.System;
    public ThemeService(System.Windows.Application application)
    {
        _application = application; SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
    }
    public bool IsDark { get; private set; }
    public string Preference => _preference;
    public event EventHandler? ThemeChanged;

    public void Apply(string? preference)
    {
        _preference = ThemeMode.Normalize(preference); IsDark = ThemeMode.ResolveDark(_preference, SystemUsesLightTheme());
        var dictionaries = _application.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(x => x.Source?.OriginalString.Contains("Resources/Themes/", StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary { Source = new Uri(IsDark ? "Resources/Themes/Dark.xaml" : "Resources/Themes/Light.xaml", UriKind.Relative) };
        if (existing is null) dictionaries.Insert(Math.Min(1, dictionaries.Count), replacement);
        else dictionaries[dictionaries.IndexOf(existing)] = replacement;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public static bool SystemUsesLightTheme()
    {
        try
        {
            var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            return Convert.ToInt32(value) != 0;
        }
        catch { return true; }
    }

    private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_preference != ThemeMode.System) return;
        _application.Dispatcher.BeginInvoke(() => Apply(_preference));
    }
    public void Dispose() { SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged; GC.SuppressFinalize(this); }
}
