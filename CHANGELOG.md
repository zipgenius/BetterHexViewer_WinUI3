# Changelog

All notable changes to **BetterHexViewer.WinUI3** are documented here.  
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

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
