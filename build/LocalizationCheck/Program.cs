using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Run after the Release/x64 solution build and library pack.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
string[] cultures = ["fr", "de", "ru", "ar", "ja", "sv", "es", "sw", "zh-Hans", "sk"];
const string framework = "net10.0-windows10.0.26100.0";
var checks = 0;

void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

Dictionary<string, string> ReadResources(string file)
{
    var data = XDocument.Load(file).Root!.Elements("data").ToArray();
    Check(data.Select(d => (string)d.Attribute("name")!).Distinct().Count() == data.Length,
        $"Duplicate resource keys: {file}");
    return data.ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
}

string Placeholders(string value) => string.Join(",", Regex.Matches(value, @"\{\d+(?:[^}]*)\}")
    .Select(m => m.Value).Order());

foreach (var project in new[] { "BetterHexViewer.WinUI3", "BetterHexViewer.Demo" })
{
    var strings = Path.Combine(root, project, "Strings");
    var neutral = ReadResources(Path.Combine(strings, "Resources.resx"));
    var output = Path.Combine(root, project, "bin", "x64", "Release", framework);
    var assembly = Assembly.LoadFrom(Path.Combine(output, project + ".dll"));
    var manager = new ResourceManager(project + ".Strings.Resources", assembly);
    foreach (var culture in cultures.Prepend("en"))
    {
        var expected = culture == "en" ? neutral : ReadResources(Path.Combine(strings, $"Resources.{culture}.resx"));
        Check(expected.Keys.Order().SequenceEqual(neutral.Keys.Order()), $"Keys differ: {project}/{culture}");
        var ci = CultureInfo.GetCultureInfo(culture);
        Check(manager.GetResourceSet(ci, true, false) is not null, $"Missing compiled resources: {project}/{culture}");
        foreach (var (key, text) in expected)
        {
            Check(!string.IsNullOrWhiteSpace(text), $"Empty resource: {project}/{culture}/{key}");
            Check(Placeholders(text) == Placeholders(neutral[key]), $"Placeholders differ: {project}/{culture}/{key}");
            Check(manager.GetString(key, ci)?.Replace("\r\n", "\n") == text,
                $"Compiled text differs: {project}/{culture}/{key}");
            // Exercise actual composite formatting, including numeric X8 placeholders.
            _ = string.Format(ci, text, 123L, 456L, 789L, "preview");
            checks++;
        }
    }
    foreach (var (requested, resolved) in new[] {
        ("fr-FR", "fr"), ("de-DE", "de"), ("ar-SA", "ar"),
        ("sw-KE", "sw"), ("zh-CN", "zh-Hans"), ("zh-SG", "zh-Hans"),
        ("it-IT", "en"), ("zh-TW", "en") })
    {
        foreach (var key in neutral.Keys)
            Check(manager.GetString(key, CultureInfo.GetCultureInfo(requested)) ==
                manager.GetString(key, CultureInfo.GetCultureInfo(resolved)),
                $"Incorrect fallback: {project}/{requested}/{key}");
    }
    Console.WriteLine($"{project}: {neutral.Count} keys verified in English + 10 translations; regional and English fallback passed.");
}

using var package = ZipFile.OpenRead(Path.Combine(root, "nupkg", "BetterHexViewer.WinUI3.1.2.3.nupkg"));
var libOutput = Path.Combine(root, "BetterHexViewer.WinUI3", "bin", "x64", "Release", framework);
foreach (var culture in cultures)
{
    var name = $"lib/net10.0-windows10.0.26100/{culture}/BetterHexViewer.WinUI3.resources.dll";
    var entry = package.GetEntry(name);
    Check(entry is not null, $"Package missing {name}");
    using var stream = entry!.Open();
    using var bytes = new MemoryStream();
    stream.CopyTo(bytes);
    Check(bytes.ToArray().SequenceEqual(File.ReadAllBytes(Path.Combine(libOutput, culture, "BetterHexViewer.WinUI3.resources.dll"))),
        $"Package resource differs from tested assembly: {culture}");
}
Check(package.GetEntry("README.md") is not null, "Package missing README");
using var metadataStream = package.GetEntry("BetterHexViewer.WinUI3.nuspec")!.Open();
var metadata = XDocument.Load(metadataStream);
Check(metadata.Descendants().Single(e => e.Name.LocalName == "version").Value == "1.2.3", "Incorrect package version");
Check(metadata.Descendants().Single(e => e.Name.LocalName == "releaseNotes").Value.Contains("1.2.3"), "Missing release notes");
Console.WriteLine($"Package 1.2.3: all ten tested satellite assemblies, README and metadata verified. {checks} checks passed.");
