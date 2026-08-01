using System.Windows;

namespace QuickResponseBao.App.Services;

public static class LocalizationService
{
    public static void Apply(string language)
    {
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(x => x.Source?.OriginalString.Contains("Strings.") == true);
        var replacement = new ResourceDictionary
        {
            Source = new Uri(language == "en-US" ? "Resources/Strings.en-US.xaml" : "Resources/Strings.zh-CN.xaml", UriKind.Relative)
        };
        if (current is null) dictionaries.Add(replacement);
        else dictionaries[dictionaries.IndexOf(current)] = replacement;
    }
}
