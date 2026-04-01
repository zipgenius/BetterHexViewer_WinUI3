# BetterHexViewer.WinUI3

![BetterHexViewer icon](https://raw.githubusercontent.com/zipgenius/BetterHexViewer_WinUI3/main/Graphics/icon.png)

[![NuGet](https://img.shields.io/nuget/v/BetterHexViewer.WinUI3?logo=nuget)](https://www.nuget.org/packages/BetterHexViewer.WinUI3)
[![Build](https://github.com/zipgenius/BetterHexViewer_WinUI3/actions/workflows/nuget-publish.yml/badge.svg)](https://github.com/zipgenius/BetterHexViewer_WinUI3/actions/workflows/nuget-publish.yml)
[![MIT License](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/zipgenius/BetterHexViewer_WinUI3/blob/main/LICENSE)

**BetterHexViewer.WinUI3** is a feature-rich, fully customisable hex-viewer control for Windows App SDK / WinUI 3 applications.
All rendering is GPU-accelerated via **Win2D** (`CanvasControl`) — a single `DrawingSession` replaces thousands of XAML elements, delivering smooth scrolling even on 4K displays with files of any size.

---

## Features

Feature | Detail
--- | ---
GPU-accelerated rendering | Win2D `CanvasControl` — one `DrawingSession` per frame, ~2 ms CPU at 4K
Large-file support | `MemoryMappedFile` — no size limit, tested up to 84 GB; scroll works beyond 2 GB
Mouse selection | Click-drag in hex or ASCII panels; both stay in sync
Keyboard navigation | Arrow keys, Home/End, PgUp/PgDn, Shift-extend selection, Ctrl+A
Text and byte search | `SearchAsync` / `SearchNextAsync` / `SearchPreviousAsync` / `FindAllAsync`
Programmatic selection | `SelectRange(offset, length)` and `SelectFromTo(start, end)`
Hover cross-highlight | 1 px border tracks the pointer in both panels simultaneously
Context menu | Right-click to copy selection as hex string or ASCII text
Offset formats | `Hexadecimal` · `Decimal` · `Octal`
Column grouping | 1 · 2 · 4 · 8 · 16 bytes per visual group
Full-width auto-fit | `FullWidth = true` adapts column count to the available width
Configurable spacing | `BytesSpacing` and `ExtraLineGap` in pixels
ASCII encoding | `AsciiEncoding` — Latin-1 (default), IBM CP437, UTF-8, CP1252, any `Encoding`
ASCII panel | Toggleable via `ShowAsciiPanel`
Font control | `FontFamily`, `FontSize`, `FontWeight` — hot-reloadable at runtime
Colour themes | All brushes are bindable — background, foreground, offsets, ruler, selection, dividers
Tooltip | 500 ms hover tooltip showing offset and byte value
SelectionChanged event | `StartOffset`, `Length`, full `byte[]` (no length cap on the property)
ScrollToOffset | Scroll to any byte offset programmatically

---

## Requirements

Component | Minimum version
--- | ---
Windows App SDK | 1.8
Target OS | Windows 10 19041 (20H1)
.NET | 8.0
Win2D | `Microsoft.Graphics.Win2D` 1.3.0

---

## Quick start

### 1 — Install the NuGet package

```shell
dotnet add package BetterHexViewer.WinUI3
```

### 2 — Add the XAML namespace

```xml
xmlns:hex="using:BetterHexViewer.WinUI3"
```

### 3 — Place the control

```xml
<hex:BetterHexViewer
    x:Name="HexViewer"
    OffsetFormat="Hexadecimal"
    ColumnGroupSize="One"
    BytesSpacing="10"
    ExtraLineGap="4"
    FullWidth="False"
    ShowAsciiPanel="True"
    SelectionChanged="HexViewer_SelectionChanged"/>
```

### 4 — Load content

```csharp
// From a file (any size — uses MemoryMappedFile)
await HexViewer.OpenFileAsync(filePath);

// From a byte array
HexViewer.LoadBytes(myByteArray);
```

### 5 — Search

```csharp
// Text search (forward)
var result = await HexViewer.SearchAsync("MZ");

// Byte-sequence search
var result = await HexViewer.SearchAsync(new byte[] { 0x4D, 0x5A });

// Navigate results
await HexViewer.SearchNextAsync();
await HexViewer.SearchPreviousAsync();

// Find all occurrences
IReadOnlyList<long> offsets = await HexViewer.FindAllAsync("MZ");
```

### 6 — Programmatic selection

```csharp
// Select 16 bytes starting at offset 0x100
HexViewer.SelectRange(0x100, 16);

// Select from offset 0x200 to 0x2FF (inclusive)
HexViewer.SelectFromTo(0x200, 0x2FF);
```

### 7 — Change ASCII encoding

```csharp
// Enable extended code pages (call once at app startup)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

HexViewer.AsciiEncoding = Encoding.GetEncoding(437); // IBM CP437
HexViewer.AsciiEncoding = Encoding.UTF8;
```

---

## Properties

### Layout and display

Property | Type | Default | Description
--- | --- | --- | ---
`OffsetFormat` | `OffsetFormat` | `Hexadecimal` | Format of the offset column
`ColumnGroupSize` | `ColumnGroupSize` | `One` | Bytes per visual group
`BytesSpacing` | `double` | `10` | Horizontal gap between hex columns (px)
`ExtraLineGap` | `double` | `4` | Extra vertical gap between rows (px)
`FullWidth` | `bool` | `false` | Auto-fit columns to available width
`ShowAsciiPanel` | `bool` | `true` | Show or hide the ASCII panel
`AsciiEncoding` | `Encoding` | Latin-1 | Encoding used in the ASCII panel
`BytesPerLine` *(read-only)* | `int` | 16 | Current number of columns
`FileSize` *(read-only)* | `long` | 0 | Size of loaded data in bytes

### Font

Property | Type | Default | Description
--- | --- | --- | ---
`FontFamily` | `FontFamily` | `Courier New` | Monospaced font family
`FontSize` | `double` | `13` | Font size in points
`FontWeight` | `FontWeight` | `Normal` | Font weight

### Colours

Property | Type | Default | Description
--- | --- | --- | ---
`Background` | `Brush` | Theme | Control background
`Foreground` | `Brush` | Theme | Hex and ASCII data text
`OffsetForeground` | `Brush` | Dark blue | Offset column text
`OffsetBackground` | `Brush` | Theme | Offset column background
`AsciiBackground` | `Brush` | Theme | ASCII panel background
`RulerForeground` | `Brush?` | auto | Ruler header text
`SelectionBackground` | `Brush` | Blue | Selection highlight background
`SelectionForeground` | `Brush` | White | Selected byte text
`DividerBrush` | `Brush` | Gray | Column divider lines

---

## Methods

Method | Description
--- | ---
`OpenFileAsync(string path)` | Opens a file of any size via `MemoryMappedFile`
`LoadBytes(byte[] data)` | Loads raw bytes directly
`Clear()` | Clears the viewer
`CopySelectionAsHex()` | Copies selection to clipboard as space-separated hex pairs
`CopySelectionAsAscii()` | Copies selection to clipboard as ASCII text
`ScrollToOffset(long offset)` | Scrolls to the row containing the given byte offset
`SelectRange(long offset, long length)` | Programmatically selects `length` bytes from `offset`
`SelectFromTo(long start, long end)` | Selects bytes between two offsets (inclusive, any order)
`SearchAsync(byte[])` | Searches forward for a byte pattern
`SearchAsync(string, Encoding?)` | Searches forward for a text pattern
`SearchNextAsync()` | Finds the next match
`SearchPreviousAsync()` | Finds the previous match
`FindAllAsync(byte[])` | Returns offsets of all non-overlapping matches
`FindAllAsync(string, Encoding?)` | Returns offsets of all text matches
`ClearSearch()` | Clears search state and selection
`ApplyTheme()` | Re-applies light/dark colour defaults
`Dispose()` | Releases the memory-mapped file handle

---

## Events

```csharp
// Fired when the selection changes
HexViewer.SelectionChanged += (sender, e) =>
{
    long   offset = e.StartOffset; // -1 when no selection
    long   length = e.Length;
    byte[] data   = e.Data;        // full copy, no size cap
};

// Fired after each SearchAsync / SearchNextAsync / SearchPreviousAsync
HexViewer.SearchResultFound += (sender, result) =>
{
    bool found   = result.Found;
    long offset  = result.Offset;  // -1 when not found
    long length  = result.Length;
    bool wrapped = result.Wrapped;
};

// Fired when mouse hover enters/moves/exits a byte cell (HEX or ASCII panel)
HexViewer.HoverOffsetChanged += (sender, e) =>
{
    long offset = e.Offset;          // -1 when pointer is outside data cells
    bool isHex  = e.IsHexColumn;
    bool isAsc  = e.IsAsciiColumn;
};
```

---

## Enumerations

```csharp
public enum OffsetFormat    { Hexadecimal, Decimal, Octal }
public enum ColumnGroupSize { One = 1, Two = 2, Four = 4, Eight = 8, Sixteen = 16 }
```

---

## Building from source

```shell
git clone https://github.com/zipgenius/BetterHexViewer_WinUI3.git
cd BetterHexViewer.WinUI3
dotnet restore
dotnet build -c Release -p:Platform=x64
```

Run the demo:

```shell
dotnet run --project BetterHexViewer.Demo -c Debug -p:Platform=x64
```

Create a NuGet package:

```shell
dotnet pack BetterHexViewer.WinUI3/BetterHexViewer.WinUI3.csproj -c Release -p:Platform=x64 -o nupkg
```

---

## Changelog

See [CHANGELOG.md](https://github.com/zipgenius/BetterHexViewer_WinUI3/blob/main/CHANGELOG.md).

---

## License

MIT © zipgenius.it — see [LICENSE](https://github.com/zipgenius/BetterHexViewer_WinUI3/blob/main/LICENSE).
