namespace QuickResponseBao.Core.Models;

public static class ThemeMode
{
    public const string Light = "Light";
    public const string Dark = "Dark";
    public const string System = "System";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "light" => Light,
        "dark" => Dark,
        _ => System
    };

    public static bool ResolveDark(string? value, bool systemUsesLightTheme) => Normalize(value) switch
    {
        Dark => true,
        Light => false,
        _ => !systemUsesLightTheme
    };
}
