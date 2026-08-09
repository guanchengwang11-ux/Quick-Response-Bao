using System.Globalization;
using System.Windows.Data;

namespace QuickResponseBao.App.Converters;

public sealed class KeywordsPreviewConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<string> source) return string.Empty;
        var keywords = source.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (keywords.Length <= 2) return string.Join(" · ", keywords);
        return $"{keywords[0]} · {keywords[1]} · +{keywords.Length - 2}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
