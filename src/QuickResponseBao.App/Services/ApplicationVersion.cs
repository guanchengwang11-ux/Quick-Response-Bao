using System.Reflection;

namespace QuickResponseBao.App.Services;

public static class ApplicationVersion
{
    public static string Current
    {
        get
        {
            var value = typeof(ApplicationVersion).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrWhiteSpace(value)
                ? typeof(ApplicationVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"
                : value.Split('+', 2)[0];
        }
    }
}
