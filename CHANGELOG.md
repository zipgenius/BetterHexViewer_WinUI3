# Changelog

All notable changes to **BetterHexViewer.WinUI3** are documented here.  
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.1.4] – 2026-03-12

### Added
- **Keyboard navigation** — arrow keys (with Ctrl modifier for line start/end
  and page jumps), Home/End, PgUp/PgDn, Shift to extend selection, Ctrl+A to
  select all. Input is received via `XamlRoot.Content.AddHandler` so it works
  regardless of WinUI 3 internal focus routing.
- **`SelectRange(long offset, long length)`** — programmatically selects a range
  of bytes and scrolls to the start of the selection.
- **`SelectFromTo(long start, long end)`** — selects bytes between two offsets
  (both inclusive, any order); complements `SelectRange`.

### Fixed
- **Large-file scroll beyond 2 GB** — scroll fields `_topLine`, `_sbTotalLines`,
  `_sbVisibleLines`, and `_sbDragStartTop` changed from `int` to `long`; tested
  with an 84 GB file (offset `1507F69DF0`).
- **Font rendering** — `MeasureFont()` now measures actual glyph width via
  `CanvasTextLayout` instead of using the fixed `fs * 0.601` formula, so fonts
  with non-standard proportions (e.g. Cascadia Mono, Lucida Console) lay out
  correctly.
- **Demo font picker** — `PopulateMonoFonts()` now uses
  `CanvasFontSet.GetSystemFontSet()` to exclude font families not supported by
  DirectWrite (raster-only fonts such as Fixedsys and Terminal are filtered out).

### Changed
- **App icon** — Demo and NuGet package now use the new icon supplied by the
  author (blue rounded-square with "0x" glyph). The `Win32Resource` / `.res`
  approach has been removed; the Demo csproj uses `ApplicationIcon` pointing
  to `Graphics/icon.png`, which the Windows App SDK build toolchain handles
  natively.

---

## [1.1.3] – 2026-03-12

### Added
- **`FindAllAsync(byte[])`** / **`FindAllAsync(string, Encoding?)`** — returns
  `IReadOnlyList<long>` with the byte offset of every non-overlapping occurrence
  of the pattern in the loaded data. Runs on a background thread via
  Boyer-Moore-Horspool; cancelled automatically when new data is loaded.
  Demo shows results in a scrollable `ContentDialog` (up to 1 000 rows displayed,
  total count always exact).

### Fixed
- Tooltip on hex bytes / ASCII characters now hides immediately when a mouse
  click begins a selection (`HideTooltip()` added to `OnPointerPressed`).

---



### Changed
- **Target framework upgraded to .NET 10** (`net10.0-windows10.0.19041.0`) for
  both the library and the Demo app; CI workflow updated accordingly.
- **App icon replaced** — the Demo now uses the definitive icon supplied by the
  author. All 9 frames (16/20/24/32/40/48/64/128/256 px) are embedded as Win32
  resources in `app_icon.res` so Windows uses the correct pre-rendered frame
  for the taskbar and Explorer without downscaling.

---

## [1.1.1] – 2026-03-11

### Added
- **Auto-scroll during selection drag** — dragging the pointer above or below
  the content area scrolls the view automatically and extends the selection,
  with speed proportional to the distance from the edge.
- **`IDisposable`** — control now implements `IDisposable`; `Dispose()` and the
  `Unloaded` event both release the memory-mapped file handle.

### Changed
- **Large-file support via `MemoryMappedFile`** — `OpenFileAsync` no longer
  limits reading to 1 MB. Files of any size (tested up to 3.84 GB) are opened
  via `System.IO.MemoryMappedFiles`; the OS handles paging transparently.
  `LoadBytes(byte[])` continues to work unchanged.
- **Offset / ASCII background colours** — `OffsetBackground` and
  `AsciiBackground` now have explicit Light (`#F2F2F2`) and Dark (`#282828`)
  defaults set in `PropertyMetadata` and `ApplyTheme()`; the bytes area uses
  `#FEFEFE` (Light) or the blended dark value. Custom themes (Matrix, etc.)
  auto-derive offset/ascii backgrounds from `Background` using perceived
  luminance so they are always distinguishable.
- **Ruler header vertical centering** — `OFFSET`, column numbers, and `ASCII`
  label are now perfectly centred in the full ruler band height.
- **`MeasureFont()` call site** — removed from `UpdateBytesPerLine()`;
  called only in `OnApplyTemplate` and `OnFontPropertyChanged`, eliminating
  residual per-scroll flickering.
- **Demo icon** — `.ico` embedded as Win32 resource (`IDI_ICON1`) so Windows
  uses pre-rendered 16/24/32/48 px frames for taskbar and Explorer instead of
  downscaling the 256 px frame.
- Demo: removed `"(first 1 MB loaded)"` status message (no longer applicable).

### Fixed
- Empty area to the right of the ASCII panel (when `FullWidth = false`) now
  shows the application window background instead of the ASCII panel colour.
- `CS0266` long-to-int implicit conversion errors in `UpdateScrollBar`,
  `Selection` property, and `FireSelectionChanged`.
- Duplicate `#endregion` preprocessor directive.

## [1.1.0] – 2026-03-10

### Added
- **`AsciiEncoding` property** — CLR property (type `System.Text.Encoding`) that
  controls how bytes are decoded for display in the ASCII panel.
  Default is ISO-8859-1 (Latin-1). Supports IBM CP437, CP850, CP1252, UTF-8,
  Shift-JIS, GBK, and any other encoding available via
  `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`.
  The `CopySelectionAsAscii` and hover tooltip also use this encoding.
- **`RulerForeground` DP** (`Brush?`) — explicit colour for the ruler header
  (OFFSET label, column numbers, ASCII label). When `null` (default), the colour
  is auto-derived from `Background` using perceived luminance for maximum contrast.
- **`FontWeight` shadow DP** — bold can be toggled at runtime; `GetFmt()` respects
  both the `bold` render parameter and the `FontWeight` DP.
- **Icon** — new SVG / PNG / ICO icon set in `Graphics/` folder; associated with
  both the NuGet package and the Demo application.

### Changed
- **`cRulerFg` colour** — now derived from `Background` via perceived luminance
  (`0.299R + 0.587G + 0.114B < 128`) instead of `IsDark`, ensuring readable
  header text on any custom background including Matrix green-on-black.
- **`cContent` / `cRulerBg`** — also derived from `Background` via `BlendColor()`;
  no longer hardcoded to `IsDark` dark/light values.
- **`cHover` border colour** — matches `cRulerFg` (semi-transparent) instead of
  a fixed grey, ensuring consistency with header text colour in all themes.
- **`MeasureFont()` no longer called inside `Render()`** — moved to
  `OnFontPropertyChanged` and `OnApplyTemplate` only, eliminating per-frame
  floating-point variance that caused ruler text to flicker on pointer move / scroll.
- Demo toolbar: added **ASCII encoding `ComboBox`** with 21 common encodings,
  verified at runtime.

### Fixed
- Ruler header text flickering by a few pixels on pointer movement or scroll
  (caused by `MeasureFont()` being called every frame).
- `UpdateSwatches()` null-reference exception on startup (guard added).

---

## [1.0.9] – 2026-03-10

### Added
- **Hover cross-highlight** — 1 px border around the hovered byte in both the
  hex and ASCII panels simultaneously.
- **`FontFamily` / `FontSize` / `FontWeight` shadow DPs** — intercept runtime
  changes via `OnFontPropertyChanged`; invalidate text formats and trigger
  re-layout automatically.
- **Tooltip overlay** — replaced `ToolTipService` with a `Border` + `TextBlock`
  floated in a `Canvas` layer above `CanvasControl`; 500 ms delay; correct
  window-relative positioning via `Canvas.SetLeft/Top`.

### Changed (Demo)
- Toolbar moved to a right-side panel (230 px, `ScrollViewer`-wrapped).
- Added font-family `ComboBox` (monospaced fonts enumerated via Win2D).
- Added font-size `+` / `−` buttons and bold `ToggleButton`.
- Added per-colour buttons with WinUI 3 `ColorPicker` dialog and live swatches.
- Demo set as solution startup project.

---

## [1.0.8] – 2026-03-10

### Changed
- **Full Win2D GPU rendering** — replaced the XAML TextBlock/Rectangle/Line
  object pool with a single `CanvasControl`. All drawing is done in one
  `DrawingSession` per frame. CPU time per frame drops from ~50 ms to ~2 ms
  at 4K + FullWidth + tight spacing.
- New dependency: `Microsoft.Graphics.Win2D` 1.3.0.
- `Generic.xaml`: four-canvas XAML stack replaced by a single
  `canvas:CanvasControl` + tooltip overlay grid.
- `CanvasTextFormat` objects cached and reused across frames.

### Fixed
- Scrollbar column pointer coordinate mapping corrected for single-canvas layout.
- `FontFamily` multi-font stack (e.g. `"Cascadia Mono, Consolas, Courier New"`)
  now correctly passes only the first family name to Win2D.

---

## [1.0.4] – 2025-XX-XX

### Fixed
- Header width spans full control width including the scrollbar column.
- Scrollbar thumb colour and track origin corrected.
- Single-byte selection rectangle width fixed.
- Selection gaps in both hex and ASCII areas eliminated.
- Text vertical and horizontal centering corrected.
- Right-click no longer clears the current selection.

---

## [1.0.3] – 2025-XX-XX

### Changed
- Custom canvas-drawn scrollbar replaces native `ScrollBar`.
- Track starts below ruler header at `_cachedDividerY + 1`.

### Fixed
- `HexStep` uses `InnerByteGap` (3 px) within a group, `BytesSpacing` only between groups.
- Ruler column labels centred exactly over their hex slots.
- `FullWidth` column count formula corrected.

---

## [1.0.2] – 2025-XX-XX

### Fixed
- Offset column width always sized for the widest format (Octal 11 digits).
- Theme-aware colours for ruler background, content background, ruler foreground.

---

## [1.0.1] – 2025-XX-XX

### Fixed
- `OFFSET` and `ASCII` header labels centred over their columns.
- System theme support via `ActualThemeChanged` and `ApplyTheme()`.

---

## [1.0.0] – 2025-XX-XX

### Added
- Initial release.
- `OpenFileAsync`, `LoadBytes`, `Clear`, `CopySelectionAsHex`,
  `CopySelectionAsAscii`, `ScrollToOffset`.
- `OffsetFormat`, `ColumnGroupSize`, `BytesSpacing`, `ExtraLineGap`,
  `FullWidth`, `ShowAsciiPanel` dependency properties.
- `SelectionBackground`, `SelectionForeground`, `OffsetForeground`,
  `DividerBrush` brush DPs.
- `BytesPerLine` and `FileSize` read-only DPs.
- `SelectionChanged` event with `HexSelectionChangedEventArgs`.
- Mouse click+drag selection, right-click context menu.
- GitHub Actions CI/CD workflow for NuGet publishing on `v*.*.*` tags.
