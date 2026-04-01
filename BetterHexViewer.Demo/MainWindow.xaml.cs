// MainWindow.xaml.cs – BetterHexViewer WinUI 3 Demo
// zipgenius.it

using BetterHexViewer.WinUI3;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage.Pickers;
using Windows.UI;

namespace BetterHexViewer.Demo
{
    public sealed partial class MainWindow : Window
    {
        // ─── Monospaced font names ─────────────────────────────────────────
        // Well-known monospaced families that may be present on Windows.
        // We verify each one exists via CanvasTextFormat before showing it.
        private static readonly string[] KnownMonoFonts =
        {
            "Cascadia Code",
            "Cascadia Mono",
            "Consolas",
            "Courier New",
            "Courier",
            "Fixedsys",
            "Lucida Console",
            "Lucida Sans Typewriter",
            "MS Gothic",
            "NSimSun",
            "OCR A Extended",
            "Source Code Pro",
            "Terminal",
            "Ubuntu Mono",
            "DejaVu Sans Mono",
            "Fira Code",
            "JetBrains Mono",
            "Hack",
            "Inconsolata",
            "Anonymous Pro",
        };

        private double _currentFontSize = 13;

        public MainWindow()
        {
            InitializeComponent();
            Title = "BetterHexViewer WinUI 3 – Demo by zipgenius.it";
            HexViewer.HoverOffsetChanged += HexViewer_HoverOffsetChanged;
            PopulateMonoFonts();
            PopulateEncodings();
            UpdateFontSizeLabel();
            UpdateStatusBytesPerLine();
        }

        // ─── Encoding list ────────────────────────────────────────────────

        private record EncodingEntry(string Label, int CodePage)
        {
            public override string ToString() => Label;
        }

        private static readonly EncodingEntry[] KnownEncodings =
        {
            new("Latin-1 / ISO-8859-1 (default)", 28591),
            new("IBM CP437 (DOS/OEM US)",          437),
            new("IBM CP850 (DOS/OEM Multilingual)", 850),
            new("IBM CP858 (DOS/OEM €)",           858),
            new("Windows CP1250 (Central EU)",     1250),
            new("Windows CP1251 (Cyrillic)",       1251),
            new("Windows CP1252 (Western EU)",     1252),
            new("Windows CP1253 (Greek)",          1253),
            new("Windows CP1254 (Turkish)",        1254),
            new("Windows CP1255 (Hebrew)",         1255),
            new("Windows CP1256 (Arabic)",         1256),
            new("ISO-8859-2 (Central EU)",         28592),
            new("ISO-8859-5 (Cyrillic)",           28595),
            new("ISO-8859-7 (Greek)",              28597),
            new("ISO-8859-8 (Hebrew)",             28598),
            new("KOI8-R (Russian)",                20866),
            new("Shift-JIS (Japanese)",            932),
            new("GBK / CP936 (Simplified Chinese)",936),
            new("Big5 (Traditional Chinese)",      950),
            new("EUC-KR (Korean)",                 51949),
            new("UTF-8",                           65001),
        };

        private void PopulateEncodings()
        {
            System.Text.Encoding.RegisterProvider(
                System.Text.CodePagesEncodingProvider.Instance);

            var available = new System.Collections.Generic.List<EncodingEntry>();
            foreach (var entry in KnownEncodings)
            {
                try
                {
                    System.Text.Encoding.GetEncoding(entry.CodePage);
                    available.Add(entry);
                }
                catch { /* not available on this system */ }
            }
            CmbEncoding.ItemsSource   = available;
            CmbEncoding.SelectedIndex = 0;   // Latin-1 default
        }

        private void CmbEncoding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexViewer == null || CmbEncoding.SelectedItem is not EncodingEntry entry) return;
            try
            {
                HexViewer.AsciiEncoding = System.Text.Encoding.GetEncoding(entry.CodePage);
            }
            catch { /* ignore unsupported */ }
        }

        private void PopulateMonoFonts()
        {
            // Use CanvasFontSet.GetSystemFontSet() to enumerate fonts actually
            // present and supported by DirectWrite on this machine.
            // Raster-only fonts (Fixedsys, Terminal…) are absent from the
            // DirectWrite font set and will therefore be excluded automatically.
            var systemFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var fontSet = Microsoft.Graphics.Canvas.Text.CanvasFontSet.GetSystemFontSet();
                for (int i = 0; i < fontSet.Fonts.Count; i++)
                {
                    var props = fontSet.Fonts[i];
                    // FamilyName is in the font's own locale; also try "en-us"
                    if (props.FamilyNames.TryGetValue("en-us", out string? name) && name != null)
                        systemFonts.Add(name);
                    else if (props.FamilyNames.Count > 0)
                        systemFonts.Add(props.FamilyNames.First().Value);
                }
            }
            catch { /* fallback: include everything */ }

            var available = new List<string>();
            foreach (var name in KnownMonoFonts)
            {
                // If we got a valid font set, require the family to be present in it.
                if (systemFonts.Count == 0 || systemFonts.Contains(name))
                    available.Add(name);
            }

            // Always include "Courier New" as absolute fallback
            if (!available.Contains("Courier New"))
                available.Insert(0, "Courier New");

            CmbFont.ItemsSource = available;

            // Select the current font
            string current = HexViewer?.FontFamily?.Source ?? "Courier New";
            string first   = current.Contains(',') ? current.Split(',')[0].Trim() : current;
            int idx = available.IndexOf(first);
            CmbFont.SelectedIndex = idx >= 0 ? idx : available.IndexOf("Courier New");
        }

        /// <summary>
        /// Measures the rendered width of a single "W" glyph for the given font.
        /// Used to detect silent DirectWrite fallback substitutions.
        /// </summary>
        private static double MeasureFontWidth(
            Microsoft.Graphics.Canvas.CanvasDevice device, string fontName, float fontSize)
        {
            using var fmt = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
            {
                FontFamily   = fontName,
                FontSize     = fontSize,
                WordWrapping = Microsoft.Graphics.Canvas.Text.CanvasWordWrapping.NoWrap
            };
            using var layout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(
                device, "W", fmt, 4096f, 4096f);
            return layout.LayoutBounds.Width;
        }

        private void CmbFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexViewer == null || CmbFont.SelectedItem is not string name) return;
            HexViewer.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(name);
        }

        // ─── Font size ────────────────────────────────────────────────────

        private void BtnFontPlus_Click(object sender, RoutedEventArgs e)
        {
            _currentFontSize = Math.Min(32, _currentFontSize + 1);
            ApplyFontSize();
        }

        private void BtnFontMinus_Click(object sender, RoutedEventArgs e)
        {
            _currentFontSize = Math.Max(8, _currentFontSize - 1);
            ApplyFontSize();
        }

        private void ApplyFontSize()
        {
            if (HexViewer == null) return;
            HexViewer.FontSize = _currentFontSize;
            UpdateFontSizeLabel();
            UpdateStatusBytesPerLine();
        }

        private void UpdateFontSizeLabel()
        {
            if (TxtFontSize != null)
                TxtFontSize.Text = ((int)_currentFontSize).ToString();
        }

        // ─── Bold toggle ──────────────────────────────────────────────────

        private void TglBold_Changed(object sender, RoutedEventArgs e)
        {
            if (HexViewer == null) return;
            HexViewer.FontWeight = TglBold.IsChecked == true
                ? FontWeights.Bold
                : FontWeights.Normal;
        }

        // ─── Show selection ───────────────────────────────────────────────

        private async void BtnShowSelection_Click(object sender, RoutedEventArgs e)
        {
            var sel = HexViewer.Selection;

            string message;
            if (!sel.HasSelection)
            {
                message = "No bytes are currently selected.";
            }
            else
            {
                // Build hex preview (first 64 bytes max for display)
                int previewLen = (int)Math.Min(sel.Length, 64);
                var hexParts   = new System.Text.StringBuilder();
                for (int i = 0; i < previewLen; i++)
                {
                    if (i > 0 && i % 16 == 0) hexParts.Append('\n');
                    else if (i > 0)            hexParts.Append(' ');
                    hexParts.Append(sel.Data[i].ToString("X2"));
                }
                if (sel.Length > 64)
                    hexParts.Append($"\n… ({sel.Length - 64} more bytes)");

                message =
                    $"Start offset : 0x{sel.StartOffset:X8} ({sel.StartOffset})\n" +
                    $"Length       : {sel.Length} byte{(sel.Length != 1 ? "s" : "")}\n" +
                    $"Full Data[]  : {sel.Data.Length} bytes (no limit)\n\n" +
                    $"Hex preview:\n{hexParts}";
            }

            var dialog = new ContentDialog
            {
                Title             = "Selection — HexViewer.Selection",
                Content           = new TextBlock
                {
                    Text         = message,
                    FontFamily   = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize     = 12,
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "OK",
                XamlRoot        = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            TxtFileName.Text = $"Loading {file.Name}…";
            try
            {
                await HexViewer.OpenFileAsync(file.Path);
                long sz = HexViewer.FileSize;
                TxtFileName.Text = file.Path;
                TxtFileSize.Text = FormatSize(sz);
            }
            catch (Exception ex)
            {
                TxtFileName.Text = $"Error: {ex.Message}";
            }
            UpdateStatusBytesPerLine();
        }

        // ─── Offset format ────────────────────────────────────────────────

        private void CmbOffset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexViewer == null) return;
            HexViewer.OffsetFormat = CmbOffset.SelectedIndex switch
            {
                1 => OffsetFormat.Decimal,
                2 => OffsetFormat.Octal,
                _ => OffsetFormat.Hexadecimal
            };
        }

        // ─── Column grouping ──────────────────────────────────────────────

        private void CmbGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexViewer == null) return;
            HexViewer.ColumnGroupSize = CmbGroup.SelectedIndex switch
            {
                1 => ColumnGroupSize.Two,
                2 => ColumnGroupSize.Four,
                3 => ColumnGroupSize.Eight,
                4 => ColumnGroupSize.Sixteen,
                _ => ColumnGroupSize.One
            };
            UpdateStatusBytesPerLine();
        }

        // ─── Spacing ──────────────────────────────────────────────────────

        private void SldrSpacing_ValueChanged(object sender,
            Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (HexViewer == null) return;
            HexViewer.BytesSpacing = e.NewValue;
            TxtSpacingVal.Text = ((int)e.NewValue).ToString();
            UpdateStatusBytesPerLine();
        }

        // ─── Row gap ──────────────────────────────────────────────────────

        private void SldrLineGap_ValueChanged(object sender,
            Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (HexViewer == null) return;
            HexViewer.ExtraLineGap = e.NewValue;
            TxtLineGapVal.Text = ((int)e.NewValue).ToString();
        }

        // ─── Full width ───────────────────────────────────────────────────

        private void TglFullWidth_Changed(object sender, RoutedEventArgs e)
        {
            if (HexViewer == null) return;
            HexViewer.FullWidth = TglFullWidth.IsChecked == true;
            UpdateStatusBytesPerLine();
        }

        // ─── ASCII panel ──────────────────────────────────────────────────

        private void TglAscii_Changed(object sender, RoutedEventArgs e)
        {
            if (HexViewer == null) return;
            HexViewer.ShowAsciiPanel = TglAscii.IsChecked == true;
        }

        // ─── Theme preset ─────────────────────────────────────────────────

        private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexViewer == null) return;

            switch (CmbTheme.SelectedIndex)
            {
                case 1: // Dark
                    SetColors(Color.FromArgb(255,30,30,30),
                              Color.FromArgb(255,220,220,220),
                              Color.FromArgb(255,100,180,255),
                              Color.FromArgb(255,80,80,80),
                              Color.FromArgb(255,0,100,200),
                              Colors.White);
                    break;

                case 2: // Matrix
                    SetColors(Colors.Black,
                              Color.FromArgb(255,0,200,0),
                              Color.FromArgb(255,0,255,0),
                              Color.FromArgb(255,0,100,0),
                              Color.FromArgb(255,0,140,0),
                              Colors.Black);
                    break;

                default: // Light
                    SetColors(Color.FromArgb(255,250,249,248),
                              Color.FromArgb(255,27,27,27),
                              Color.FromArgb(255,0,0,139),
                              Colors.Gray,
                              Color.FromArgb(255,0,120,215),
                              Colors.White);
                    break;
            }
        }

        private void SetColors(Color bg, Color fg, Color offset, Color divider,
                                Color selBg, Color selFg)
        {
            HexViewer.Background          = new SolidColorBrush(bg);
            HexViewer.Foreground          = new SolidColorBrush(fg);
            HexViewer.OffsetForeground    = new SolidColorBrush(offset);
            HexViewer.DividerBrush        = new SolidColorBrush(divider);
            HexViewer.SelectionBackground = new SolidColorBrush(selBg);
            HexViewer.SelectionForeground = new SolidColorBrush(selFg);
            // Let Render() auto-derive offset/ascii backgrounds from the new Background
            HexViewer.OffsetBackground    = null;
            HexViewer.AsciiBackground     = null;

            UpdateSwatches(bg, fg, offset, divider, selBg, selFg);
        }

        private void UpdateSwatches(Color bg, Color fg, Color offset, Color divider,
                                    Color selBg, Color selFg)
        {
            if (SwatchBg == null) return;
            SwatchBg.Background     = new SolidColorBrush(bg);
            SwatchFg.Background     = new SolidColorBrush(fg);
            SwatchOffset.Background = new SolidColorBrush(offset);
            SwatchDivider.Background= new SolidColorBrush(divider);
            SwatchSelBg.Background  = new SolidColorBrush(selBg);
            SwatchSelFg.Background  = new SolidColorBrush(selFg);
            // OffsetBg/AsciiBg are null (auto) after a theme reset — show neutral swatch
            SwatchOffsetBg.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200));
            SwatchAsciiBg.Background  = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200));
        }

        // ─── Individual colour pickers ────────────────────────────────────

        private async void BtnColorBg_Click(object sender, RoutedEventArgs e)
        {
            var c = await PickColorAsync(((SolidColorBrush)HexViewer.Background).Color);
            if (c.HasValue) { HexViewer.Background = new SolidColorBrush(c.Value); SwatchBg.Background = HexViewer.Background; }
        }

        private async void BtnColorFg_Click(object sender, RoutedEventArgs e)
        {
            var c = await PickColorAsync(((SolidColorBrush)HexViewer.Foreground).Color);
            if (c.HasValue) { HexViewer.Foreground = new SolidColorBrush(c.Value); SwatchFg.Background = HexViewer.Foreground; }
        }

        private async void BtnColorOffset_Click(object sender, RoutedEventArgs e)
        {
            var c = await PickColorAsync(((SolidColorBrush)HexViewer.OffsetForeground).Color);
            if (c.HasValue) { HexViewer.OffsetForeground = new SolidColorBrush(c.Value); SwatchOffset.Background = HexViewer.OffsetForeground; }
        }

        private async void BtnColorDivider_Click(object sender, RoutedEventArgs e)
        {
            var c = await PickColorAsync(((SolidColorBrush)HexViewer.DividerBrush).Color);
            if (c.HasValue) { HexViewer.DividerBrush = new SolidColorBrush(c.Value); SwatchDivider.Background = HexViewer.DividerBrush; }
        }

        private async void BtnColorSelBg_Click(object sender, RoutedEventArgs e)
        {
            var c = await PickColorAsync(((SolidColorBrush)HexViewer.SelectionBackground).Color);
            if (c.HasValue) { HexViewer.SelectionBackground = new SolidColorBrush(c.Value); SwatchSelBg.Background = HexViewer.SelectionBackground; }
        }

        private async void BtnColorOffsetBg_Click(object sender, RoutedEventArgs e)
        {
            var current = HexViewer.OffsetBackground as SolidColorBrush
                          ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 232, 232));
            var c = await PickColorAsync(current.Color);
            if (c.HasValue) { HexViewer.OffsetBackground = new SolidColorBrush(c.Value); SwatchOffsetBg.Background = HexViewer.OffsetBackground; }
        }

        private async void BtnColorAsciiBg_Click(object sender, RoutedEventArgs e)
        {
            var current = HexViewer.AsciiBackground as SolidColorBrush
                          ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 232, 232));
            var c = await PickColorAsync(current.Color);
            if (c.HasValue) { HexViewer.AsciiBackground = new SolidColorBrush(c.Value); SwatchAsciiBg.Background = HexViewer.AsciiBackground; }
        }

        private void BtnResetColors_Click(object sender, RoutedEventArgs e)
        {
            // Reset theme ComboBox to Light (index 0) which triggers SetColors
            CmbTheme.SelectedIndex = 0;
            // Also reset the new auto-derived properties
            HexViewer.OffsetBackground = null;
            HexViewer.AsciiBackground  = null;
            HexViewer.RulerForeground  = null;
            // Refresh swatches to reflect auto state
            SwatchOffsetBg.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 232, 232));
            SwatchAsciiBg.Background  = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 232, 232));
        }

        private async void BtnColorSelFg_Click(object sender, RoutedEventArgs e)
        {
            var c = await PickColorAsync(((SolidColorBrush)HexViewer.SelectionForeground).Color);
            if (c.HasValue) { HexViewer.SelectionForeground = new SolidColorBrush(c.Value); SwatchSelFg.Background = HexViewer.SelectionForeground; }
        }

        /// <summary>
        /// Shows WinUI 3 ColorPicker inside a ContentDialog.
        /// Returns null if the user cancels.
        /// </summary>
        private async System.Threading.Tasks.Task<Color?> PickColorAsync(Color initial)
        {
            var picker = new ColorPicker
            {
                Color              = initial,
                ColorSpectrumShape = ColorSpectrumShape.Ring,
                IsAlphaEnabled     = false,
                IsHexInputVisible  = true,
                Width              = 280
            };

            var dialog = new ContentDialog
            {
                Title             = "Pick a colour",
                Content           = picker,
                PrimaryButtonText = "OK",
                CloseButtonText   = "Cancel",
                XamlRoot          = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? picker.Color : null;
        }

        // ─── Search ───────────────────────────────────────────────────────

        private void RbSearchMode_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtSearchHint == null) return;
            TxtSearchHint.Visibility = (RbSearchHex?.IsChecked == true)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void TxtSearch_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                await RunSearch(forward: true);
        }

        private async void BtnSearchNext_Click(object sender, RoutedEventArgs e)
            => await RunSearch(forward: true);

        private async void BtnSearchPrev_Click(object sender, RoutedEventArgs e)
            => await RunSearch(forward: false);

        private async void BtnFindAll_Click(object sender, RoutedEventArgs e)
        {
            if (HexViewer.FileSize == 0 && HexViewer.BytesPerLine == 0) return;
            var query = TxtSearch?.Text?.Trim();
            if (string.IsNullOrEmpty(query)) return;

            TxtSearchResult.Text = "Searching…";

            IReadOnlyList<long> offsets;

            if (RbSearchHex?.IsChecked == true)
            {
                var tokens = query
                    .Replace(",", " ").Replace("-", " ")
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var bytes = new System.Collections.Generic.List<byte>();
                foreach (var t in tokens)
                {
                    if (!byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    {
                        TxtSearchResult.Text = $"Invalid hex token: \"{t}\"";
                        return;
                    }
                    bytes.Add(b);
                }
                if (bytes.Count == 0) return;
                offsets = await HexViewer.FindAllAsync(bytes.ToArray());
            }
            else
            {
                offsets = await HexViewer.FindAllAsync(query);
            }

            if (offsets.Count == 0)
            {
                TxtSearchResult.Text = "Not found.";
                return;
            }

            // Show results in a ContentDialog with a scrollable list
            var sb = new System.Text.StringBuilder();
            int shown = Math.Min(offsets.Count, 1000);
            for (int i = 0; i < shown; i++)
                sb.AppendLine($"0x{offsets[i]:X8}  ({offsets[i]})");
            if (offsets.Count > 1000)
                sb.AppendLine($"… and {offsets.Count - 1000} more");

            var scroll = new ScrollViewer
            {
                MaxHeight = 400,
                Content   = new TextBlock
                {
                    Text       = sb.ToString(),
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize   = 12,
                    IsTextSelectionEnabled = true
                }
            };

            var dialog = new ContentDialog
            {
                Title             = $"{offsets.Count} occurrence{(offsets.Count == 1 ? "" : "s")} found",
                Content           = scroll,
                CloseButtonText   = "Close",
                XamlRoot          = Content.XamlRoot
            };

            TxtSearchResult.Text = $"{offsets.Count} occurrence{(offsets.Count == 1 ? "" : "s")} found.";
            await dialog.ShowAsync();
        }

        private async System.Threading.Tasks.Task RunSearch(bool forward)
        {
            if (HexViewer.FileSize == 0 && HexViewer.BytesPerLine == 0) return;
            var query = TxtSearch?.Text?.Trim();
            if (string.IsNullOrEmpty(query)) return;

            TxtSearchResult.Text = "Searching…";

            HexSearchResult result;

            if (RbSearchHex?.IsChecked == true)
            {
                // Parse hex string: accept "4D 5A", "4D5A", "4D,5A"
                var tokens = query
                    .Replace(",", " ").Replace("-", " ")
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var bytes = new System.Collections.Generic.List<byte>();
                foreach (var t in tokens)
                {
                    if (!byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    {
                        TxtSearchResult.Text = $"Invalid hex token: \"{t}\"";
                        return;
                    }
                    bytes.Add(b);
                }
                if (bytes.Count == 0) return;

                result = forward
                    ? await HexViewer.SearchAsync(bytes.ToArray())
                    : await HexViewer.SearchPreviousAsync();
            }
            else
            {
                result = forward
                    ? await HexViewer.SearchAsync(query)
                    : await HexViewer.SearchPreviousAsync();
            }

            if (!result.Found)
                TxtSearchResult.Text = "Not found.";
            else if (result.Wrapped)
                TxtSearchResult.Text = $"Found at 0x{result.Offset:X8}  (wrapped)";
            else
                TxtSearchResult.Text = $"Found at 0x{result.Offset:X8}";
        }

        private void HexViewer_SelectionChanged(object sender, HexSelectionChangedEventArgs e)
        {
            TxtSelection.Text = e.StartOffset < 0
                ? string.Empty
                : $"Sel: 0x{e.StartOffset:X8}  len: {e.Length}";
        }

        private void HexViewer_HoverOffsetChanged(object? sender, HexHoverOffsetChangedEventArgs e)
        {
            if (HexViewer == null)
            {
                TxtHover.Text = string.Empty;
                return;
            }

            TxtHover.Text = e.Offset < 0
                ? string.Empty
                : $"Hover: {FormatOffsetByMode(e.Offset)} ({(e.IsAsciiColumn ? "ASCII" : "HEX")})";
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private void UpdateStatusBytesPerLine()
        {
            if (TxtBytesPerLine == null || HexViewer == null) return;
            TxtBytesPerLine.Text = $"{HexViewer.BytesPerLine} cols";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)            return $"{bytes} B";
            if (bytes < 1024 * 1024)     return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024L) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        private string FormatOffsetByMode(long offset)
            => HexViewer.OffsetFormat switch
            {
                OffsetFormat.Decimal => offset.ToString(),
                OffsetFormat.Octal => Convert.ToString(offset, 8).PadLeft(11, '0'),
                _ => $"0x{offset:X8}"
            };
    }
}
