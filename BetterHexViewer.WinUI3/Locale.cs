using System.Globalization;
using System.Resources;

namespace BetterHexViewer.WinUI3;

internal static class Locale
{
    private static readonly ResourceManager Resources = new(
        "BetterHexViewer.WinUI3.Strings.Resources", typeof(Locale).Assembly);

    internal static string Get(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException($"Missing UI resource: {key}");
}
