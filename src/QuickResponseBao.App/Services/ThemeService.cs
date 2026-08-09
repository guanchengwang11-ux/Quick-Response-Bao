using System.Windows;
using Microsoft.Win32;
using QuickResponseBao.Core.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

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
        _preference = ThemeMode.Normalize(preference);
        IsDark = ThemeMode.ResolveDark(_preference, SystemUsesLightTheme());

        var highContrast = _preference == ThemeMode.System && ApplicationThemeManager.IsSystemHighContrast();
        var applicationTheme = highContrast ? ApplicationTheme.HighContrast : IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(applicationTheme, WindowBackdropType.None, updateAccent: false);

        var dictionaries = _application.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(IsQrbThemeDictionary);
        var resource = highContrast ? "Resources/HighContrastTheme.xaml" : IsDark ? "Resources/DarkTheme.xaml" : "Resources/LightTheme.xaml";
        var replacement = new ResourceDictionary { Source = new Uri(resource, UriKind.Relative) };
        if (existing is null) dictionaries.Insert(Math.Min(2, dictionaries.Count), replacement);
        else dictionaries[dictionaries.IndexOf(existing)] = replacement;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsQrbThemeDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        return source?.Contains("Resources/LightTheme.xaml", StringComparison.OrdinalIgnoreCase) == true
            || source?.Contains("Resources/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) == true
            || source?.Contains("Resources/HighContrastTheme.xaml", StringComparison.OrdinalIgnoreCase) == true;
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
