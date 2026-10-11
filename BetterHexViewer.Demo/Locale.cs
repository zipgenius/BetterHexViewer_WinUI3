using System.Globalization;
using System.Resources;

namespace BetterHexViewer.Demo;

internal static class Locale
{
    private static readonly ResourceManager Resources = new(
        "BetterHexViewer.Demo.Strings.Resources", typeof(Locale).Assembly);

    internal static string Get(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException($"Missing UI resource: {key}");

    internal static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
