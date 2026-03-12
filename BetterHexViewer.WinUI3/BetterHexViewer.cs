// BetterHexViewer.cs
// WinUI 3 Hex Viewer Control
// Part of BetterHexViewer.WinUI3 – by zipgenius.it
// Version: 1.1.2

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace BetterHexViewer.WinUI3
{
    public enum OffsetFormat    { Hexadecimal, Decimal, Octal }
    public enum ColumnGroupSize { One = 1, Two = 2, Four = 4, Eight = 8, Sixteen = 16 }

    /// <summary>Result of a search operation.</summary>
    public sealed class HexSearchResult : EventArgs
    {
        /// <summary>True when a match was found.</summary>
        public bool Found        { get; }
        /// <summary>Byte offset of the match start, or -1 when not found.</summary>
        public long Offset       { get; }
        /// <summary>Length of the match in bytes.</summary>
        public long Length       { get; }
        /// <summary>True when the search wrapped around the end/start of the data.</summary>
        public bool Wrapped      { get; }

        internal HexSearchResult(bool found, long offset, long length, bool wrapped)
        { Found = found; Offset = offset; Length = length; Wrapped = wrapped; }

        internal static readonly HexSearchResult NotFound =
            new HexSearchResult(false, -1, 0, false);
    }

    /// <summary>
    /// Describes the current selection state of a <see cref="BetterHexViewer"/>.
    /// Unlike <see cref="HexSelectionChangedEventArgs"/>, <see cref="Data"/> has
    /// no length cap — it always contains the full selected content.
    /// </summary>
    public sealed class HexSelection
    {
        /// <summary>Byte offset of the first selected byte, or -1 when there is no selection.</summary>
        public long StartOffset { get; }

        /// <summary>Number of selected bytes (0 when there is no selection).</summary>
        public long Length { get; }

        /// <summary>
        /// Full copy of the selected bytes — no length limit.
        /// Empty array when there is no selection.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>Whether any bytes are currently selected.</summary>
        public bool HasSelection => StartOffset >= 0 && Length > 0;

        internal HexSelection(long start, long length, byte[] data)
        { StartOffset = start; Length = length; Data = data; }

        internal static readonly HexSelection Empty =
            new HexSelection(-1, 0, Array.Empty<byte>());
    }

    public sealed class HexSelectionChangedEventArgs : EventArgs
    {
        public long   StartOffset { get; }
        public long   Length      { get; }
        public byte[] Data        { get; }
        internal HexSelectionChangedEventArgs(long start, long length, byte[] data)
        { StartOffset = start; Length = length; Data = data; }
    }

    /// <summary>
    /// WinUI 3 GPU-accelerated hex viewer (Win2D CanvasControl) – zipgenius.it
    /// </summary>
    public sealed partial class BetterHexViewer : Control, IDisposable
    {
        // ─── Layout constants ─────────────────────────────────────────────
        private const double RulerExtraHeight = 8;
        private const double HexExtraGap      = 12;
        private const double AsciiRightPad    = 12;
        private const double ScrollBarWidth   = 16;
        private const double InnerByteGap     = 3;
        private const double SelPad           = 2;
        private const int    MinBytesPerLine  = 1;

        // ─── Win2D canvas ─────────────────────────────────────────────────
        private CanvasControl _canvas = null!;

        // ─── Tooltip overlay (Border + TextBlock in XAML template) ───────
        private Border    _tooltipBorder = null!;
        private TextBlock _tooltipText   = null!;
        private Microsoft.UI.Xaml.DispatcherTimer? _tooltipTimer;
        private long   _tooltipByteIdx = -1;
        private Point  _tooltipPt;

        // ─── Scrollbar state ──────────────────────────────────────────────
        private int    _sbTotalLines   = 0;
        private int    _sbVisibleLines = 1;
        private bool   _sbDragging     = false;
        private double _sbDragStartY   = 0;
        private int    _sbDragStartTop = 0;
        private double _wheelAccum     = 0;
        private Microsoft.UI.Xaml.DispatcherTimer? _sbRepeatTimer;
        private int    _sbRepeatDir   = 0;
        private bool   _sbRepeatFirst = true;
        private double _sbBtnUpBottom = 0;
        private double _sbBtnDnTop    = 0;
        private double _sbThumbTop    = 0;
        private double _sbThumbBot    = 0;

        // ─── UI helpers ───────────────────────────────────────────────────
        private MenuFlyout _contextMenu = null!;

        // ─── Data ─────────────────────────────────────────────────────────
        // Supports both byte[] (LoadBytes) and MemoryMappedFile (OpenFileAsync).
        // All rendering and selection code accesses bytes via ByteAt(i) and DataLength.
        private byte[]?                       _bytes;          // set by LoadBytes()
        private MemoryMappedFile?             _mmf;            // set by OpenFileAsync()
        private MemoryMappedViewAccessor?     _mmva;           // view over _mmf
        private long   _fileSize;

        /// <summary>Number of bytes currently loaded (file size or array length).</summary>
        private long DataLength => _fileSize;

        /// <summary>Returns byte at position <paramref name="i"/> from either source.</summary>
        private byte ByteAt(long i)
        {
            if (_bytes != null) return _bytes[i];
            if (_mmva  != null) return _mmva.ReadByte(i);
            return 0;
        }

        /// <summary>Disposes any open memory-mapped file and clears all data fields.</summary>
        private void ReleaseData()
        {
            _mmva?.Dispose(); _mmva = null;
            _mmf?.Dispose();  _mmf  = null;
            _bytes    = null;
            _fileSize = 0;
        }
        private int    _topLine;

        // ─── Selection & hover ────────────────────────────────────────────
        private long _selStart   = -1;
        private long _selEnd     = -1;
        private long _caretByte  = -1;
        private long _hoverByte  = -1;   // byte under pointer (for hover highlight)
        private bool _mouseDown;

        // ─── Font metrics ─────────────────────────────────────────────────
        private double _charWidth    = 8;
        private double _lineHeight   = 16;
        private int    _bytesPerLine = 16;

        // ─── Cached layout ────────────────────────────────────────────────
        private double _cachedHexStartX;
        private double _cachedAsciiStartX;
        private double _cachedDividerY;
        private double _cachedAsciiColWidth;

        // ─── Render throttle ──────────────────────────────────────────────
        private bool _renderPending;

        // ─── Win2D text formats ───────────────────────────────────────────
        private CanvasTextFormat? _txFmt;
        private CanvasTextFormat? _txFmtBold;

        // ═══════════════════════════════════════════════════════════════════
        //  DEPENDENCY PROPERTIES
        // ═══════════════════════════════════════════════════════════════════

        #region FontFamily (shadow – intercepts runtime changes)
        public static new readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily),
                typeof(BetterHexViewer), new PropertyMetadata(
                    new FontFamily("Courier New"), OnFontPropertyChanged));
        public new FontFamily FontFamily
        {
            get => (FontFamily)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }
        #endregion

        #region FontSize (shadow – intercepts runtime changes)
        public static new readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double),
                typeof(BetterHexViewer), new PropertyMetadata(13.0, OnFontPropertyChanged));
        public new double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, Math.Max(6, value));
        }
        #endregion

        #region FontWeight (shadow – intercepts runtime changes)
        public static new readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight),
                typeof(BetterHexViewer), new PropertyMetadata(
                    FontWeights.Normal, OnFontPropertyChanged));
        public new FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }
        #endregion

        #region ColumnGroupSize
        public static readonly DependencyProperty ColumnGroupSizeProperty =
            DependencyProperty.Register(nameof(ColumnGroupSize), typeof(ColumnGroupSize),
                typeof(BetterHexViewer), new PropertyMetadata(ColumnGroupSize.One, OnLayoutPropertyChanged));
        public ColumnGroupSize ColumnGroupSize
        {
            get => (ColumnGroupSize)GetValue(ColumnGroupSizeProperty);
            set => SetValue(ColumnGroupSizeProperty, value);
        }
        #endregion

        #region OffsetFormat
        public static readonly DependencyProperty OffsetFormatProperty =
            DependencyProperty.Register(nameof(OffsetFormat), typeof(OffsetFormat),
                typeof(BetterHexViewer), new PropertyMetadata(OffsetFormat.Hexadecimal, OnLayoutPropertyChanged));
        public OffsetFormat OffsetFormat
        {
            get => (OffsetFormat)GetValue(OffsetFormatProperty);
            set => SetValue(OffsetFormatProperty, value);
        }
        #endregion

        #region BytesSpacing
        public static readonly DependencyProperty BytesSpacingProperty =
            DependencyProperty.Register(nameof(BytesSpacing), typeof(double),
                typeof(BetterHexViewer), new PropertyMetadata(10.0, OnLayoutPropertyChanged));
        public double BytesSpacing
        {
            get => (double)GetValue(BytesSpacingProperty);
            set => SetValue(BytesSpacingProperty, Math.Max(0, value));
        }
        #endregion

        #region ExtraLineGap
        public static readonly DependencyProperty ExtraLineGapProperty =
            DependencyProperty.Register(nameof(ExtraLineGap), typeof(double),
                typeof(BetterHexViewer), new PropertyMetadata(4.0, OnRenderPropertyChanged));
        public double ExtraLineGap
        {
            get => (double)GetValue(ExtraLineGapProperty);
            set => SetValue(ExtraLineGapProperty, Math.Clamp(value, 0, 12));
        }
        #endregion

        #region FullWidth
        public static readonly DependencyProperty FullWidthProperty =
            DependencyProperty.Register(nameof(FullWidth), typeof(bool),
                typeof(BetterHexViewer), new PropertyMetadata(false, OnLayoutPropertyChanged));
        public bool FullWidth
        {
            get => (bool)GetValue(FullWidthProperty);
            set => SetValue(FullWidthProperty, value);
        }
        #endregion

        #region SelectionBackground
        public static readonly DependencyProperty SelectionBackgroundProperty =
            DependencyProperty.Register(nameof(SelectionBackground), typeof(Brush),
                typeof(BetterHexViewer), new PropertyMetadata(
                    new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)), OnRenderPropertyChanged));
        public Brush SelectionBackground
        {
            get => (Brush)GetValue(SelectionBackgroundProperty);
            set => SetValue(SelectionBackgroundProperty, value);
        }
        #endregion

        #region SelectionForeground
        public static readonly DependencyProperty SelectionForegroundProperty =
            DependencyProperty.Register(nameof(SelectionForeground), typeof(Brush),
                typeof(BetterHexViewer), new PropertyMetadata(
                    new SolidColorBrush(Colors.White), OnRenderPropertyChanged));
        public Brush SelectionForeground
        {
            get => (Brush)GetValue(SelectionForegroundProperty);
            set => SetValue(SelectionForegroundProperty, value);
        }
        #endregion

        #region DividerBrush
        public static readonly DependencyProperty DividerBrushProperty =
            DependencyProperty.Register(nameof(DividerBrush), typeof(Brush),
                typeof(BetterHexViewer), new PropertyMetadata(
                    new SolidColorBrush(Colors.Gray), OnRenderPropertyChanged));
        public Brush DividerBrush
        {
            get => (Brush)GetValue(DividerBrushProperty);
            set => SetValue(DividerBrushProperty, value);
        }
        #endregion

        #region OffsetBackground
        /// <summary>
        /// Background colour of the offset column area.
        /// When null, it is auto-derived as a darker shade of <see cref="Control.Background"/>.
        /// </summary>
        public static readonly DependencyProperty OffsetBackgroundProperty =
            DependencyProperty.Register(nameof(OffsetBackground), typeof(Brush),
                typeof(BetterHexViewer), new PropertyMetadata(
                    new SolidColorBrush(Color.FromArgb(255, 242, 242, 242)), OnRenderPropertyChanged));
        public Brush? OffsetBackground
        {
            get => (Brush?)GetValue(OffsetBackgroundProperty);
            set => SetValue(OffsetBackgroundProperty, value);
        }
        #endregion

        #region AsciiBackground
        /// <summary>
        /// Background colour of the ASCII panel area.
        /// When null, defaults to the same value as <see cref="OffsetBackground"/>.
        /// </summary>
        public static readonly DependencyProperty AsciiBackgroundProperty =
            DependencyProperty.Register(nameof(AsciiBackground), typeof(Brush),
                typeof(BetterHexViewer), new PropertyMetadata(
                    new SolidColorBrush(Color.FromArgb(255, 242, 242, 242)), OnRenderPropertyChanged));
        public Brush? AsciiBackground
        {
            get => (Brush?)GetValue(AsciiBackgroundProperty);
            set => SetValue(AsciiBackgroundProperty, value);
        }
        #endregion

        #region OffsetForeground
        public static readonly DependencyProperty OffsetForegroundProperty =
            DependencyProperty.Register(nameof(OffsetForeground), typeof(Brush),
                typeof(BetterHexViewer), new PropertyMetadata(
                    new SolidColorBrush(Color.FromArgb(255, 0, 0, 139)), OnRenderPropertyChanged));
        public Brush OffsetForeground
        {
            get => (Brush)GetValue(OffsetForegroundProperty);
            set => SetValue(OffsetForegroundProperty, value);
        }
        #endregion

        #region RulerForeground
        public static readonly DependencyProperty RulerForegroundProperty =
            DependencyProperty.Register(nameof(RulerForeground), typeof(Brush),
                typeof(BetterHexViewer), new PropertyMetadata(null, OnRenderPropertyChanged));
        /// <summary>
        /// Colour of the ruler header labels (OFFSET, column numbers, ASCII).
        /// When null the colour is derived automatically from the background.
        /// </summary>
        public Brush? RulerForeground
        {
            get => (Brush?)GetValue(RulerForegroundProperty);
            set => SetValue(RulerForegroundProperty, value);
        }
        #endregion

        #region ShowAsciiPanel
        public static readonly DependencyProperty ShowAsciiPanelProperty =
            DependencyProperty.Register(nameof(ShowAsciiPanel), typeof(bool),
                typeof(BetterHexViewer), new PropertyMetadata(true, OnLayoutPropertyChanged));
        public bool ShowAsciiPanel
        {
            get => (bool)GetValue(ShowAsciiPanelProperty);
            set => SetValue(ShowAsciiPanelProperty, value);
        }
        #endregion

        #region AsciiEncoding
        // Not a DependencyProperty because Encoding is not a WinRT type.
        // Use a plain CLR property + manual invalidate.
        private Encoding _asciiEncoding = Encoding.Latin1;

        /// <summary>
        /// Encoding used to decode bytes for the ASCII panel.
        /// Default is ISO-8859-1 (Latin-1), which maps every byte 0x00–0xFF
        /// to its Unicode code point – a safe universal fallback.
        /// Set to e.g. <c>Encoding.GetEncoding(437)</c> for IBM CP437,
        /// <c>Encoding.UTF8</c> for UTF-8, etc.
        /// Call <see cref="System.Text.Encoding.RegisterProvider"/> with
        /// <see cref="System.Text.CodePagesEncodingProvider.Instance"/> before
        /// using code-page encodings like CP437 or CP1252.
        /// </summary>
        public Encoding AsciiEncoding
        {
            get => _asciiEncoding;
            set
            {
                if (value == null || ReferenceEquals(value, _asciiEncoding)) return;
                _asciiEncoding = value;
                _canvas?.Invalidate();
            }
        }
        #endregion

        #region BytesPerLine (read-only)
        public static readonly DependencyProperty BytesPerLineProperty =
            DependencyProperty.Register(nameof(BytesPerLine), typeof(int),
                typeof(BetterHexViewer), new PropertyMetadata(16));
        public int BytesPerLine => (int)GetValue(BytesPerLineProperty);
        private void SetBytesPerLine(int v) => SetValue(BytesPerLineProperty, v);
        #endregion

        #region FileSize (read-only)
        public static readonly DependencyProperty FileSizeProperty =
            DependencyProperty.Register(nameof(FileSize), typeof(long),
                typeof(BetterHexViewer), new PropertyMetadata(0L));
        public long FileSize => (long)GetValue(FileSizeProperty);
        private void SetFileSize(long v) => SetValue(FileSizeProperty, v);
        #endregion

        #region Selection (read-only, no length limit)
        /// <summary>
        /// Returns the current selection as a <see cref="HexSelection"/> object.
        /// <see cref="HexSelection.Data"/> contains the full selected bytes with
        /// no length cap. Returns <see cref="HexSelection.Empty"/> when nothing
        /// is selected or no data is loaded.
        /// </summary>
        public HexSelection Selection
        {
            get
            {
                if (_selStart < 0 || _selEnd < 0 || DataLength == 0)
                    return HexSelection.Empty;
                long s   = Math.Min(_selStart, _selEnd);
                long e   = Math.Max(_selStart, _selEnd);
                long len = e - s + 1;
                var  data = new byte[(int)len];
                // Copy from whichever source is active
                if (_bytes != null)
                    Array.Copy(_bytes, s, data, 0, len);
                else if (_mmva != null)
                    _mmva.ReadArray(s, data, 0, (int)len);
                return new HexSelection(s, len, data);
            }
        }
        #endregion

        public event EventHandler<HexSelectionChangedEventArgs>? SelectionChanged;

        // ── Search state ─────────────────────────────────────────────────
        private byte[]?            _lastPattern;       // last searched byte pattern
        private long               _lastMatchOffset = -1;
        private CancellationTokenSource? _searchCts;

        /// <summary>
        /// Raised when a search finds a match. Contains the match offset and length.
        /// When no match is found the event is raised with <see cref="HexSearchResult.Found"/> = false.
        /// </summary>
        public event EventHandler<HexSearchResult>? SearchResultFound;

        // ═══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════

        public BetterHexViewer()
        {
            DefaultStyleKey = typeof(BetterHexViewer);
            // Unlock all system code pages (IBM437, CP1252, etc.)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // Light-theme defaults — overridden by ApplyTheme() once OnApplyTemplate runs
            Background = new SolidColorBrush(Color.FromArgb(255, 254, 254, 254));
            Foreground = new SolidColorBrush(Color.FromArgb(255,  27,  27,  27));
            // Release memory-mapped file when the control is unloaded from the visual tree
            Unloaded += (_, _) => ReleaseData();
        }

        /// <summary>
        /// Releases the memory-mapped file handle (if any) opened by
        /// <see cref="OpenFileAsync"/>. Call this when the control is no longer needed.
        /// </summary>
        public void Dispose() => ReleaseData();

        // ═══════════════════════════════════════════════════════════════════
        //  TEMPLATE
        // ═══════════════════════════════════════════════════════════════════

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _canvas = (CanvasControl)GetTemplateChild("PART_Canvas");
            _tooltipBorder = (Border)GetTemplateChild("PART_TooltipBorder");
            _tooltipText   = (TextBlock)GetTemplateChild("PART_TooltipText");

            _canvas.Draw        += OnCanvasDraw;
            _canvas.SizeChanged += (_, _) => { UpdateBytesPerLine(); UpdateScrollBar(); _canvas.Invalidate(); };

            _canvas.PointerPressed  += OnPointerPressed;
            _canvas.PointerMoved    += OnPointerMoved;
            _canvas.PointerMoved    += OnPointerHover;
            _canvas.PointerReleased += OnPointerReleased;
            _canvas.RightTapped     += OnRightTapped;
            _canvas.PointerExited   += OnPointerExited;
            _canvas.PointerPressed  += OnSbPointerPressed;
            _canvas.PointerMoved    += OnSbPointerMoved;
            _canvas.PointerReleased += OnSbPointerReleased;

            ActualThemeChanged += (_, _) => { ApplyTheme(); _canvas.Invalidate(); };

            BuildContextMenu();
            MeasureFont();
            ApplyTheme();
            UpdateBytesPerLine();
            UpdateScrollBar();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════════════════

        public async Task OpenFileAsync(string filePath)
        {
            _selStart = -1; _selEnd = -1; _caretByte = -1; _topLine = 0;

            var info = new FileInfo(filePath);
            long size = info.Length;

            // Dispose previous data source
            ReleaseData();

            if (size == 0)
            {
                _fileSize = 0;
                SetFileSize(0L);
                UpdateBytesPerLine(); UpdateScrollBar(); _canvas?.Invalidate(); FireSelectionChanged();
                return;
            }

            await Task.Run(() =>
            {
                _mmf  = MemoryMappedFile.CreateFromFile(
                            filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                _mmva = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            });

            _fileSize = size;
            SetFileSize(_fileSize);
            UpdateBytesPerLine(); UpdateScrollBar(); _canvas?.Invalidate(); FireSelectionChanged();
        }

        public void LoadBytes(byte[] data)
        {
            ReleaseData();
            _bytes    = data ?? Array.Empty<byte>();
            _fileSize = _bytes.Length;
            SetFileSize(_fileSize);
            _selStart = -1; _selEnd = -1; _caretByte = -1; _topLine = 0;
            UpdateBytesPerLine(); UpdateScrollBar(); _canvas?.Invalidate(); FireSelectionChanged();
        }

        public void Clear()
        {
            ReleaseData();
            SetFileSize(0L);
            _selStart = -1; _selEnd = -1; _caretByte = -1; _topLine = 0;
            UpdateScrollBar(); _canvas?.Invalidate();
        }

        public void CopySelectionAsHex()
        {
            if (!HasSelection()) return;
            long s = Math.Min(_selStart, _selEnd), e = Math.Max(_selStart, _selEnd);
            var parts = new List<string>();
            for (long i = s; i <= e && i < DataLength; i++)
                parts.Add(ByteAt(i).ToString("X2"));
            SetClipboard(string.Join(" ", parts));
        }

        public void CopySelectionAsAscii()
        {
            if (!HasSelection()) return;
            long s = Math.Min(_selStart, _selEnd), e = Math.Max(_selStart, _selEnd);
            var sb = new System.Text.StringBuilder();
            for (long i = s; i <= e && i < DataLength; i++)
                sb.Append(ByteToAsciiChar(ByteAt(i)));
            SetClipboard(sb.ToString());
        }

        public void ScrollToOffset(long offset)
        {
            if (DataLength == 0 || _bytesPerLine == 0) return;
            int maxTop = Math.Max(0, _sbTotalLines - _sbVisibleLines);
            _topLine   = Math.Clamp((int)(offset / _bytesPerLine), 0, maxTop);
            _canvas?.Invalidate();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SEARCH
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Searches forward for <paramref name="pattern"/> starting after the
        /// current selection (or from the beginning when nothing is selected).
        /// Wraps around when reaching end of data.
        /// </summary>
        public Task<HexSearchResult> SearchAsync(byte[] pattern)
            => SearchCoreAsync(pattern, forward: true);

        /// <summary>
        /// Converts <paramref name="text"/> to bytes using
        /// <paramref name="encoding"/> (or <see cref="AsciiEncoding"/> when null)
        /// and searches forward. Wraps around when reaching end of data.
        /// </summary>
        public Task<HexSearchResult> SearchAsync(string text, Encoding? encoding = null)
        {
            var enc     = encoding ?? _asciiEncoding;
            var pattern = enc.GetBytes(text);
            return SearchCoreAsync(pattern, forward: true);
        }

        /// <summary>Repeats the last search forward.</summary>
        public Task<HexSearchResult> SearchNextAsync()
        {
            if (_lastPattern == null || _lastPattern.Length == 0)
                return Task.FromResult(HexSearchResult.NotFound);
            return SearchCoreAsync(_lastPattern, forward: true, fromAfterLast: true);
        }

        /// <summary>Repeats the last search backward.</summary>
        public Task<HexSearchResult> SearchPreviousAsync()
        {
            if (_lastPattern == null || _lastPattern.Length == 0)
                return Task.FromResult(HexSearchResult.NotFound);
            return SearchCoreAsync(_lastPattern, forward: false, fromAfterLast: true);
        }

        /// <summary>Clears the stored search pattern and search highlight.</summary>
        public void ClearSearch()
        {
            _lastPattern     = null;
            _lastMatchOffset = -1;
        }

        // ── Core search engine (Boyer-Moore-Horspool) ─────────────────────
        private async Task<HexSearchResult> SearchCoreAsync(
            byte[] pattern, bool forward, bool fromAfterLast = false)
        {
            if (DataLength == 0 || pattern.Length == 0)
                return HexSearchResult.NotFound;

            // Cancel any in-flight search
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            _lastPattern = pattern;

            // Determine start position
            long startPos;
            if (forward)
            {
                if (fromAfterLast && _lastMatchOffset >= 0)
                    startPos = _lastMatchOffset + 1;
                else if (_selEnd >= 0)
                    startPos = Math.Max(_selStart, _selEnd) + 1;
                else
                    startPos = 0;
                if (startPos >= DataLength) startPos = 0;
            }
            else
            {
                if (fromAfterLast && _lastMatchOffset >= 0)
                    startPos = _lastMatchOffset - 1;
                else if (_selStart >= 0)
                    startPos = Math.Min(_selStart, _selEnd) - 1;
                else
                    startPos = DataLength - 1;
                if (startPos < 0) startPos = DataLength - 1;
            }

            long matchOffset = await Task.Run(() =>
                forward
                    ? BmhForward(pattern, startPos, ct)
                    : BmhBackward(pattern, startPos, ct),
                ct).ConfigureAwait(false);

            if (ct.IsCancellationRequested) return HexSearchResult.NotFound;

            bool wrapped = false;

            // Wrap-around
            if (matchOffset < 0)
            {
                wrapped = true;
                matchOffset = await Task.Run(() =>
                    forward
                        ? BmhForward(pattern, 0, ct)
                        : BmhBackward(pattern, DataLength - 1, ct),
                    ct).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested) return HexSearchResult.NotFound;

            HexSearchResult result;
            if (matchOffset < 0)
            {
                result = HexSearchResult.NotFound;
            }
            else
            {
                _lastMatchOffset = matchOffset;
                result = new HexSearchResult(true, matchOffset, pattern.Length, wrapped);

                // Select the match and scroll into view on the UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    _selStart  = matchOffset;
                    _selEnd    = matchOffset + pattern.Length - 1;
                    _caretByte = _selEnd;
                    ScrollToOffset(matchOffset);
                    ScheduleRender();
                    FireSelectionChanged();
                });
            }

            // Raise event on UI thread
            DispatcherQueue.TryEnqueue(() => SearchResultFound?.Invoke(this, result));

            return result;
        }

        // ── Boyer-Moore-Horspool forward ──────────────────────────────────
        private long BmhForward(byte[] pat, long start, CancellationToken ct)
        {
            int  m   = pat.Length;
            long n   = DataLength;
            if (m > n) return -1;

            // Bad-character skip table
            var skip = new int[256];
            for (int i = 0; i < 256; i++) skip[i] = m;
            for (int i = 0; i < m - 1; i++) skip[pat[i]] = m - 1 - i;

            long i2 = start;
            while (i2 <= n - m)
            {
                if (ct.IsCancellationRequested) return -1;
                int j = m - 1;
                while (j >= 0 && pat[j] == ByteAt(i2 + j)) j--;
                if (j < 0) return i2;
                i2 += skip[ByteAt(i2 + m - 1)];
            }
            return -1;
        }

        // ── Boyer-Moore-Horspool backward ─────────────────────────────────
        private long BmhBackward(byte[] pat, long start, CancellationToken ct)
        {
            int  m = pat.Length;
            long n = DataLength;
            if (m > n) return -1;

            // Bad-character skip table (reversed pattern)
            var skip = new int[256];
            for (int i = 0; i < 256; i++) skip[i] = m;
            for (int i = m - 1; i > 0; i--) skip[pat[i]] = i;

            long i2 = Math.Min(start, n - m);
            while (i2 >= 0)
            {
                if (ct.IsCancellationRequested) return -1;
                int j = 0;
                while (j < m && pat[j] == ByteAt(i2 + j)) j++;
                if (j == m) return i2;
                i2 -= skip[ByteAt(i2)];
            }
            return -1;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PROPERTY CHANGE CALLBACKS
        // ═══════════════════════════════════════════════════════════════════

        private static void OnFontPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (BetterHexViewer)d;
            v.InvalidateTxFormats();
            v.MeasureFont();
            v.UpdateBytesPerLine(); v.UpdateScrollBar(); v._canvas?.Invalidate();
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (BetterHexViewer)d;
            v.InvalidateTxFormats();
            v.UpdateBytesPerLine(); v.UpdateScrollBar(); v._canvas?.Invalidate();
        }

        private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((BetterHexViewer)d)._canvas?.Invalidate();

        // ═══════════════════════════════════════════════════════════════════
        //  CONTEXT MENU
        // ═══════════════════════════════════════════════════════════════════

        private void BuildContextMenu()
        {
            _contextMenu = new MenuFlyout();
            var copyHex   = new MenuFlyoutItem { Text = "Copy bytes (hex)" };
            var copyAscii = new MenuFlyoutItem { Text = "Copy as ASCII" };
            copyHex.Click   += (_, _) => CopySelectionAsHex();
            copyAscii.Click += (_, _) => CopySelectionAsAscii();
            _contextMenu.Items.Add(copyHex);
            _contextMenu.Items.Add(copyAscii);
        }

        private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            bool hasSel = HasSelection();
            foreach (var item in _contextMenu.Items.OfType<MenuFlyoutItem>())
                item.IsEnabled = hasSel;
            _contextMenu.ShowAt(_canvas, e.GetPosition(_canvas));
        }

        // ═══════════════════════════════════════════════════════════════════
        //  POINTER HANDLING
        // ═══════════════════════════════════════════════════════════════════

        private bool IsInSbColumn(double x) => x >= _canvas.ActualWidth - ScrollBarWidth;

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var cp = e.GetCurrentPoint(_canvas);
            if (cp.Properties.IsRightButtonPressed) return;
            if (IsInSbColumn(cp.Position.X)) return;

            _canvas.CapturePointer(e.Pointer);
            _mouseDown = true;
            long idx = HitTest(cp.Position.X, cp.Position.Y);
            if (idx >= 0) { _selStart = idx; _selEnd = idx; _caretByte = idx; }
            else          { _selStart = -1;  _selEnd = -1;  _caretByte = -1; }
            ScheduleRender(); FireSelectionChanged();
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_mouseDown) return;
            var pt = e.GetCurrentPoint(_canvas).Position;

            // ── Auto-scroll when dragging beyond canvas edges ──────────────
            double contentTop    = _cachedDividerY;
            double contentBottom = _canvas?.ActualHeight ?? 0;
            double rowH          = _lineHeight + ExtraLineGap;
            int    maxTop        = Math.Max(0, _sbTotalLines - _sbVisibleLines);

            if (pt.Y < contentTop && _topLine > 0)
            {
                // Pointer above content area — scroll up proportionally
                int lines = Math.Max(1, (int)((contentTop - pt.Y) / rowH));
                _topLine  = Math.Max(0, _topLine - lines);
                // Extend selection to first byte of new top line
                long firstVisible = (long)_topLine * _bytesPerLine;
                _selEnd = Math.Max(0, firstVisible);
                _caretByte = _selEnd;
                ScheduleRender(); FireSelectionChanged();
                return;
            }

            if (pt.Y > contentBottom && _topLine < maxTop)
            {
                // Pointer below content area — scroll down proportionally
                int lines = Math.Max(1, (int)((pt.Y - contentBottom) / rowH));
                _topLine  = Math.Min(maxTop, _topLine + lines);
                // Extend selection to last byte of last visible line
                long lastVisible = Math.Min(DataLength - 1,
                    (long)(_topLine + _sbVisibleLines) * _bytesPerLine - 1);
                _selEnd = lastVisible;
                _caretByte = _selEnd;
                ScheduleRender(); FireSelectionChanged();
                return;
            }

            // ── Normal in-bounds move ──────────────────────────────────────
            long idx = HitTest(pt.X, pt.Y);
            if (idx >= 0 && idx != _selEnd)
            {
                _selEnd = idx; _caretByte = idx;
                ScheduleRender(); FireSelectionChanged();
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _mouseDown = false;
            _canvas.ReleasePointerCapture(e.Pointer);
        }

        private void OnPointerHover(object sender, PointerRoutedEventArgs e)
        {
            if (_mouseDown) return;
            var  pt  = e.GetCurrentPoint(_canvas).Position;
            long idx = HitTest(pt.X, pt.Y);

            // Update hover highlight
            if (idx != _hoverByte)
            {
                _hoverByte = idx;
                ScheduleRender();
            }

            if (idx < 0)
            {
                HideTooltip();
                return;
            }

            // Same byte already showing tooltip → do nothing
            if (idx == _tooltipByteIdx && _tooltipBorder.Visibility == Visibility.Visible)
                return;

            // New byte: restart delay timer
            HideTooltip();
            _tooltipByteIdx = idx;
            _tooltipPt      = pt;

            _tooltipTimer = new Microsoft.UI.Xaml.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _tooltipTimer.Tick += (_, _) =>
            {
                _tooltipTimer?.Stop();
                _tooltipTimer = null;
                ShowTooltip(_tooltipByteIdx, _tooltipPt);
            };
            _tooltipTimer.Start();
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_hoverByte != -1)
            {
                _hoverByte = -1;
                ScheduleRender();
            }
            HideTooltip();
        }

        private void ShowTooltip(long idx, Point pt)
        {
            if (idx < 0 || idx >= DataLength) return;
            string offsetStr = OffsetFormat switch
            {
                OffsetFormat.Decimal => $"Offset: {idx} (dec)",
                OffsetFormat.Octal   => $"Offset: {Convert.ToString(idx, 8).PadLeft(11, '0')} (oct)",
                _                   => $"Offset: 0x{idx:X8}",
            };
            string byteVal = $"Value: 0x{ByteAt(idx):X2} ({ByteAt(idx)})"
                           + (ByteToAsciiChar(ByteAt(idx)) != '·'
                              ? $" '{ByteToAsciiChar(ByteAt(idx))}'" : string.Empty);
            _tooltipText.Text = $"{offsetStr}\n{byteVal}";
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(_tooltipBorder, pt.X + 14);
            Microsoft.UI.Xaml.Controls.Canvas.SetTop (_tooltipBorder, pt.Y - 8);
            _tooltipBorder.Visibility = Visibility.Visible;
        }

        private void HideTooltip()
        {
            _tooltipTimer?.Stop();
            _tooltipTimer   = null;
            _tooltipByteIdx = -1;
            if (_tooltipBorder != null)
                _tooltipBorder.Visibility = Visibility.Collapsed;
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var pt = e.GetCurrentPoint(this).Position;
            if (pt.Y <= _cachedDividerY) return;

            double delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
            _wheelAccum -= delta / 30.0;
            int whole    = (int)_wheelAccum;
            _wheelAccum -= whole;
            if (whole != 0)
            {
                int maxTop = Math.Max(0, _sbTotalLines - _sbVisibleLines);
                _topLine   = Math.Clamp(_topLine + whole, 0, maxTop);
                ScheduleRender();
            }
            e.Handled = true;
        }

        private void OnSbPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(_canvas).Position;
            if (!IsInSbColumn(pt.X)) return;

            if (pt.Y < _sbBtnUpBottom)
            {
                _canvas.CapturePointer(e.Pointer);
                _sbRepeatDir = -1; _sbRepeatFirst = true;
                SbRepeatScroll(); StartSbRepeatTimer(400);
                e.Handled = true; return;
            }
            if (pt.Y > _sbBtnDnTop)
            {
                _canvas.CapturePointer(e.Pointer);
                _sbRepeatDir = +1; _sbRepeatFirst = true;
                SbRepeatScroll(); StartSbRepeatTimer(400);
                e.Handled = true; return;
            }

            if (_sbTotalLines <= _sbVisibleLines) return;
            _canvas.CapturePointer(e.Pointer);

            if (pt.Y >= _sbThumbTop && pt.Y <= _sbThumbBot)
            {
                _sbDragging     = true;
                _sbDragStartY   = pt.Y;
                _sbDragStartTop = _topLine;
            }
            else
            {
                double trackTop = _sbBtnUpBottom;
                double trackH   = Math.Max(1, _sbBtnDnTop - trackTop);
                double relY     = Math.Max(0, pt.Y - trackTop);
                int    maxTop   = _sbTotalLines - _sbVisibleLines;
                _topLine        = Math.Clamp((int)(relY / trackH * _sbTotalLines), 0, maxTop);
                _sbDragging     = true;
                _sbDragStartY   = pt.Y;
                _sbDragStartTop = _topLine;
            }
            ScheduleRender();
            e.Handled = true;
        }

        private void OnSbPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_sbDragging) return;
            var    pt     = e.GetCurrentPoint(_canvas).Position;
            double trackH = Math.Max(1, _sbBtnDnTop - _sbBtnUpBottom);
            double dy     = pt.Y - _sbDragStartY;
            int    maxTop = Math.Max(0, _sbTotalLines - _sbVisibleLines);
            _topLine      = Math.Clamp(_sbDragStartTop + (int)(dy * _sbTotalLines / trackH), 0, maxTop);
            ScheduleRender();
            e.Handled = true;
        }

        private void OnSbPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _sbDragging  = false;
            _sbRepeatDir = 0;
            StopSbRepeatTimer();
            _canvas.ReleasePointerCapture(e.Pointer);
        }

        private void SbRepeatScroll()
        {
            if (_sbRepeatDir == 0) return;
            int maxTop = Math.Max(0, _sbTotalLines - _sbVisibleLines);
            _topLine   = Math.Clamp(_topLine + _sbRepeatDir * _sbVisibleLines, 0, maxTop);
            ScheduleRender();
        }

        private void StartSbRepeatTimer(int initialDelayMs)
        {
            StopSbRepeatTimer();
            _sbRepeatTimer          = new Microsoft.UI.Xaml.DispatcherTimer();
            _sbRepeatTimer.Interval = TimeSpan.FromMilliseconds(initialDelayMs);
            _sbRepeatTimer.Tick    += (_, _) =>
            {
                if (_sbRepeatDir == 0) { StopSbRepeatTimer(); return; }
                if (_sbRepeatFirst) { _sbRepeatFirst = false; _sbRepeatTimer!.Interval = TimeSpan.FromMilliseconds(80); }
                SbRepeatScroll();
            };
            _sbRepeatTimer.Start();
        }

        private void StopSbRepeatTimer()
        {
            _sbRepeatTimer?.Stop();
            _sbRepeatTimer = null;
        }

        private void ScheduleRender()
        {
            if (_renderPending) return;
            _renderPending = true;
            DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.High,
                () => { _renderPending = false; _canvas?.Invalidate(); });
        }

        // ═══════════════════════════════════════════════════════════════════
        //  METRICS
        // ═══════════════════════════════════════════════════════════════════

        private void MeasureFont()
        {
            double fs   = FontSize > 0 ? FontSize : 13;
            _charWidth  = fs * 0.601;
            _lineHeight = fs * 1.35;
        }

        private double OffsetColumnWidth() => 15 * _charWidth;

        private void UpdateBytesPerLine()
        {
            if (_canvas == null) return;

            if (!FullWidth)
            {
                _bytesPerLine = 16;
                SetBytesPerLine(_bytesPerLine);
                return;
            }

            double cw = _canvas.ActualWidth - ScrollBarWidth;
            if (cw <= 0) cw = ActualWidth - ScrollBarWidth;
            if (cw <= 0) return;

            double offsetW = OffsetColumnWidth();
            double gap     = 2 * _charWidth;
            int    g       = (int)ColumnGroupSize;

            double availW = cw - offsetW - gap - HexExtraGap - gap - AsciiRightPad;
            if (g > 1) availW -= BytesSpacing;

            double asciiSpacing = _charWidth / 2;
            double byteAsciiW   = _charWidth + asciiSpacing;

            int newBpl;
            if (g <= 1)
            {
                double byteW = 2 * _charWidth + BytesSpacing + byteAsciiW;
                newBpl = Math.Max(MinBytesPerLine, (int)((availW + BytesSpacing) / byteW));
            }
            else
            {
                double groupHexW = GroupWidth(g) + BytesSpacing;
                double groupAscW = g * byteAsciiW;
                double groupUnit = groupHexW + groupAscW;
                int groups = Math.Max(1, (int)((availW + BytesSpacing) / groupUnit));
                newBpl = groups * g;
            }

            _bytesPerLine = newBpl;
            SetBytesPerLine(_bytesPerLine);
        }

        private void UpdateScrollBar()
        {
            _sbTotalLines   = DataLength == 0 || _bytesPerLine == 0
                              ? 0
                              : (int)((DataLength + _bytesPerLine - 1) / _bytesPerLine);
            _sbVisibleLines = Math.Max(1, VisibleLineCount());
            int maxTop      = Math.Max(0, _sbTotalLines - _sbVisibleLines);
            if (_topLine > maxTop) _topLine = maxTop;
        }

        private int VisibleLineCount()
        {
            if (_canvas == null) return 1;
            double h = _canvas.ActualHeight - _cachedDividerY;
            if (h <= 0) return 1;
            return Math.Max(1, (int)(h / (_lineHeight + ExtraLineGap)));
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HIT-TESTING
        // ═══════════════════════════════════════════════════════════════════

        private long HitTest(double x, double y)
        {
            if (DataLength == 0) return -1;
            double dy = y - _cachedDividerY;
            if (dy < 0) return -1;

            int  row     = (int)(dy / (_lineHeight + ExtraLineGap)) + _topLine;
            long baseIdx = (long)row * _bytesPerLine;
            if (baseIdx < 0 || baseIdx >= DataLength) return -1;

            int    g            = (int)ColumnGroupSize;
            double asciiSpacing = _charWidth / 2;

            double curX = _cachedHexStartX;
            for (int col = 0; col < _bytesPerLine; col++)
            {
                double nextX = curX + HexStep(col, g);
                if (x >= curX && x < nextX)
                {
                    long idx = baseIdx + col;
                    return idx < DataLength ? idx : -1;
                }
                curX = nextX;
            }

            if (ShowAsciiPanel && x >= _cachedAsciiStartX &&
                x < _cachedAsciiStartX + _cachedAsciiColWidth)
            {
                int col = (int)((x - _cachedAsciiStartX) / (_charWidth + asciiSpacing));
                if (col >= 0 && col < _bytesPerLine)
                {
                    long idx = baseIdx + col;
                    return idx < DataLength ? idx : -1;
                }
            }

            return -1;
        }

        private double HexStep(int index, int groupSize)
        {
            if (groupSize <= 1)
                return 2 * _charWidth + BytesSpacing;

            bool isLastInGroup = (index + 1) % groupSize == 0;
            bool isLastOverall = index == _bytesPerLine - 1;

            double step = 2 * _charWidth + InnerByteGap;
            if (isLastInGroup && !isLastOverall)
                step += BytesSpacing;

            return step;
        }

        private double GroupWidth(int groupSize)
        {
            if (groupSize <= 1) return 2 * _charWidth + BytesSpacing;
            return groupSize * (2 * _charWidth + InnerByteGap) - InnerByteGap;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  RENDERING  –  Win2D GPU DrawingSession
        // ═══════════════════════════════════════════════════════════════════

        private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            Render(args.DrawingSession);
        }

        private CanvasTextFormat GetFmt(bool bold)
        {
            double fs  = FontSize > 0 ? FontSize : 13;
            string src = FontFamily?.Source ?? "Courier New";
            string fam = src.Contains(',') ? src.Split(',')[0].Trim() : src;
            if (string.IsNullOrEmpty(fam)) fam = "Courier New";

            // Respect the FontWeight DP: use Bold if either bold param or FontWeight is Bold
            bool isBold = bold || FontWeight.Weight >= FontWeights.Bold.Weight;

            if (isBold)
            {
                if (_txFmtBold == null)
                    _txFmtBold = new CanvasTextFormat
                    {
                        FontFamily   = fam,
                        FontSize     = (float)fs,
                        FontWeight   = FontWeights.Bold,
                        WordWrapping = CanvasWordWrapping.NoWrap
                    };
                return _txFmtBold;
            }
            if (_txFmt == null)
                _txFmt = new CanvasTextFormat
                {
                    FontFamily   = fam,
                    FontSize     = (float)fs,
                    WordWrapping = CanvasWordWrapping.NoWrap
                };
            return _txFmt;
        }

        private void InvalidateTxFormats()
        {
            _txFmt?.Dispose();     _txFmt     = null;
            _txFmtBold?.Dispose(); _txFmtBold = null;
        }

        private static void TxCenter(CanvasDrawingSession ds, string text,
                                      double x, double y, double cellW, double cellH,
                                      Color color, CanvasTextFormat fmt)
        {
            using var tl = new CanvasTextLayout(ds, text, fmt, (float)cellW, (float)cellH);
            float tx = (float)(x + (cellW  - tl.LayoutBounds.Width)  / 2);
            float ty = (float)(y + (cellH  - tl.LayoutBounds.Height) / 2);
            ds.DrawTextLayout(tl, new Vector2(tx, ty), color);
        }

        private static void TxLeft(CanvasDrawingSession ds, string text,
                                    double x, double y, double cellH,
                                    Color color, CanvasTextFormat fmt)
        {
            using var tl = new CanvasTextLayout(ds, text, fmt, 4096f, (float)cellH);
            float ty = (float)(y + (cellH - tl.LayoutBounds.Height) / 2);
            ds.DrawTextLayout(tl, new Vector2((float)x, ty), color);
        }

        private static Color BlendColor(Color base_, Color toward, float t)
        {
            return Color.FromArgb(255,
                (byte)(base_.R + (toward.R - base_.R) * t),
                (byte)(base_.G + (toward.G - base_.G) * t),
                (byte)(base_.B + (toward.B - base_.B) * t));
        }

        private static Color BrushColor(Brush? b, Color fallback)
            => b is SolidColorBrush scb ? scb.Color : fallback;

        /// <summary>
        /// Returns the pixel rect [x, y, width, height] of a byte cell in the hex area.
        /// </summary>
        private (double x, double y, double w, double h) HexCellRect(long byteIdx, double dividerY, double rowH)
        {
            int row = (int)(byteIdx / _bytesPerLine) - _topLine;
            int col = (int)(byteIdx % _bytesPerLine);
            int g   = (int)ColumnGroupSize;
            double y = dividerY + row * rowH;
            double x = _cachedHexStartX;
            for (int i = 0; i < col; i++) x += HexStep(i, g);
            return (x, y, 2 * _charWidth, rowH);
        }

        /// <summary>
        /// Returns the pixel rect of a byte cell in the ASCII area.
        /// </summary>
        private (double x, double y, double w, double h) AsciiCellRect(long byteIdx, double dividerY, double rowH)
        {
            int    row          = (int)(byteIdx / _bytesPerLine) - _topLine;
            int    col          = (int)(byteIdx % _bytesPerLine);
            double asciiSpacing = _charWidth / 2;
            double y = dividerY + row * rowH;
            double x = _cachedAsciiStartX + col * (_charWidth + asciiSpacing);
            return (x, y, _charWidth + asciiSpacing, rowH);
        }

        private void Render(CanvasDrawingSession ds)
        {
            if (_canvas == null) return;
            double wf = _canvas.ActualWidth;
            double w  = wf - ScrollBarWidth;
            double h  = _canvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var fmt     = GetFmt(bold: false);
            var fmtBold = GetFmt(bold: true);

            double offsetW      = OffsetColumnWidth();
            double gap          = 2 * _charWidth;
            int    g            = (int)ColumnGroupSize;
            double asciiSpacing = _charWidth / 2;
            double rowH         = _lineHeight + ExtraLineGap;

            double dividerY  = _lineHeight + RulerExtraHeight + 5;
            double hexStartX = offsetW + gap + HexExtraGap;
            _cachedDividerY  = dividerY;
            _cachedHexStartX = hexStartX;

            double hexWidth = 0;
            for (int i = 0; i < _bytesPerLine; i++) hexWidth += HexStep(i, g);
            double asciiStartX   = hexStartX + hexWidth + gap + (g > 1 ? BytesSpacing : 0);
            _cachedAsciiStartX   = asciiStartX;
            double asciiColW     = _bytesPerLine * (_charWidth + asciiSpacing) + AsciiRightPad;
            _cachedAsciiColWidth = asciiColW;

            bool  dark     = IsDark;
            Color cBg      = BrushColor(Background,          dark ? Color.FromArgb(255,30,30,30)   : Color.FromArgb(255,254,254,254));
            Color cRulerBg = BlendColor(cBg, dark ? Colors.White : Colors.Black, 0.08f);
            // Bytes area: explicit Background DP or a very subtle shift from cBg
            Color cContent = BrushColor(Background,          dark ? Color.FromArgb(255,30,30,30)   : Color.FromArgb(255,254,254,254));
            Color cDivider = BrushColor(DividerBrush,        dark ? Color.FromArgb(255,80,80,80)   : Color.FromArgb(255,160,160,160));
            Color cOffset  = BrushColor(OffsetForeground,    dark ? Color.FromArgb(255,100,180,255): Color.FromArgb(255,0,0,139));
            // RulerForeground: use DP if set, otherwise auto-derive with high contrast from cBg
            Color cRulerFg = RulerForeground is SolidColorBrush rfb
                             ? rfb.Color
                             : BlendColor(cBg, (cBg.R * 0.299 + cBg.G * 0.587 + cBg.B * 0.114) < 128 ? Colors.White : Colors.Black, 0.80f);
            Color cData    = BrushColor(Foreground,          dark ? Color.FromArgb(255,220,220,220): Colors.Black);
            Color cSelBg   = BrushColor(SelectionBackground, dark ? Color.FromArgb(255,0,100,200)  : Color.FromArgb(255,0,120,215));
            Color cSelFg   = BrushColor(SelectionForeground, Colors.White);
            // OffsetBackground / AsciiBackground: use DP if set, otherwise derive from cBg
            // — blend toward black on light backgrounds, toward white on dark backgrounds
            bool  bgIsLight = (cBg.R * 0.299 + cBg.G * 0.587 + cBg.B * 0.114) >= 128;
            Color cOffsetBg = OffsetBackground is SolidColorBrush obb
                              ? obb.Color
                              : BlendColor(cBg, bgIsLight ? Colors.Black : Colors.White, 0.06f);
            Color cAsciiBg  = AsciiBackground is SolidColorBrush abb
                              ? abb.Color
                              : cOffsetBg;
            // Hover border: semi-transparent version of cRulerFg
            Color cHover   = Color.FromArgb(160, cRulerFg.R, cRulerFg.G, cRulerFg.B);

            // PASS 1 – backgrounds
            ds.FillRectangle(0, 0, (float)wf, (float)h, cBg);
            ds.FillRectangle(0, 0, (float)wf, (float)dividerY, cRulerBg);
            float contentX = (float)(offsetW + gap / 2);
            // Offset column band (darker)
            ds.FillRectangle(0, (float)(dividerY + 1),
                             contentX, (float)(h - dividerY - 1), cOffsetBg);
            // Hex bytes area — only up to the start of the ASCII panel (or end of hex if no ASCII)
            float hexEndX = ShowAsciiPanel
                ? (float)(asciiStartX - gap / 2)
                : (float)(hexStartX + hexWidth + gap);
            if (hexEndX > contentX)
                ds.FillRectangle(contentX, (float)(dividerY + 1),
                                 hexEndX - contentX, (float)(h - dividerY - 1), cContent);
            // ASCII panel band (same as offset by default) — only up to actual content width
            if (ShowAsciiPanel)
            {
                float ascX = (float)(asciiStartX - gap / 2);
                float ascW = (float)(_cachedAsciiColWidth + gap / 2);
                if (ascW > 0)
                    ds.FillRectangle(ascX, (float)(dividerY + 1),
                                     ascW, (float)(h - dividerY - 1), cAsciiBg);
            }

            // PASS 2 – selection highlights
            if (DataLength > 0)
            {
                long selS   = Math.Min(_selStart, _selEnd);
                long selE   = Math.Max(_selStart, _selEnd);
                bool hasSel = selS >= 0 && selE >= 0;
                int  vl     = VisibleLineCount() + 1;

                for (int line = 0; line < vl; line++)
                {
                    int  row    = _topLine + line;
                    long offset = (long)row * _bytesPerLine;
                    if (offset >= DataLength) break;

                    double y    = dividerY + line * rowH;
                    double xHex = hexStartX;
                    double xAsc = asciiStartX;

                    bool   inHexRun   = false;
                    double hexRunLeft = 0, lastSelX = 0;
                    long   lastSelIdx = -1;
                    double ascLeft = double.MaxValue, ascRight = double.MinValue;

                    for (int i = 0; i < _bytesPerLine; i++)
                    {
                        long bi   = offset + i;
                        if (bi >= DataLength) break;
                        bool isSel   = hasSel && bi >= selS && bi <= selE;
                        bool isCaret = bi == _caretByte && !isSel;
                        double step  = HexStep(i, g);

                        if (isSel)
                        {
                            if (!inHexRun)
                            {
                                inHexRun   = true;
                                hexRunLeft = xHex - (bi == selS ? SelPad : 0);
                            }
                            lastSelX = xHex; lastSelIdx = bi;
                            if (ShowAsciiPanel)
                            {
                                double al = xAsc - (bi == selS ? SelPad : 0);
                                double ar = xAsc + _charWidth + asciiSpacing + (bi == selE ? SelPad : 0);
                                if (al < ascLeft)  ascLeft  = al;
                                if (ar > ascRight) ascRight = ar;
                            }
                        }
                        else
                        {
                            if (inHexRun)
                            {
                                double rr = lastSelX + 2 * _charWidth + (lastSelIdx == selE ? SelPad : 0);
                                ds.FillRectangle((float)hexRunLeft, (float)y,
                                                 (float)(rr - hexRunLeft), (float)rowH, cSelBg);
                                inHexRun = false;
                            }
                            if (isCaret)
                                ds.DrawRectangle((float)(xHex - SelPad), (float)y,
                                                 (float)(2 * _charWidth + SelPad * 2), (float)rowH,
                                                 BrushColor(SelectionForeground, Colors.Navy));
                        }
                        xHex += step;
                        xAsc += _charWidth + asciiSpacing;
                    }
                    if (inHexRun)
                    {
                        double rr = lastSelX + 2 * _charWidth + (lastSelIdx == selE ? SelPad : 0);
                        ds.FillRectangle((float)hexRunLeft, (float)y,
                                         (float)(rr - hexRunLeft), (float)rowH, cSelBg);
                    }
                    if (ShowAsciiPanel && ascLeft < ascRight)
                        ds.FillRectangle((float)ascLeft, (float)y,
                                         (float)(ascRight - ascLeft), (float)rowH, cSelBg);
                }
            }

            // PASS 3 – divider lines
            double divX1 = offsetW + gap / 2;
            ds.DrawLine(0, (float)dividerY, (float)wf, (float)dividerY, cDivider);
            ds.DrawLine((float)divX1, (float)dividerY, (float)divX1, (float)h, cDivider);
            if (ShowAsciiPanel)
            {
                double divX2 = asciiStartX - gap / 2;
                ds.DrawLine((float)divX2, (float)dividerY, (float)divX2, (float)h, cDivider);
                double ascRight2 = asciiStartX + asciiColW;
                if (ascRight2 <= w)
                    ds.DrawLine((float)ascRight2, (float)dividerY, (float)ascRight2, (float)h, cDivider);
            }

            // PASS 4 – ruler labels
            TxCenter(ds, "OFFSET", 0, 0, offsetW, dividerY, cRulerFg, fmtBold);
            double rxHex = hexStartX;
            for (int i = 0; i < _bytesPerLine; i++)
            {
                double step = HexStep(i, g);
                TxCenter(ds, i.ToString("X2"), rxHex, 0,
                          2 * _charWidth, dividerY, cRulerFg, fmtBold);
                rxHex += step;
            }
            if (ShowAsciiPanel)
                TxCenter(ds, "ASCII", asciiStartX, 0,
                          asciiColW, dividerY, cRulerFg, fmtBold);

            if (DataLength == 0)
            {
                TxLeft(ds, "No data loaded.", hexStartX, dividerY + 4, _lineHeight, cData, fmt);
                RenderScrollBar(ds, wf, h);
                return;
            }

            // PASS 5 – data rows
            long sS      = Math.Min(_selStart, _selEnd);
            long sE      = Math.Max(_selStart, _selEnd);
            bool hasSelT = sS >= 0 && sE >= 0;
            int  vLines  = VisibleLineCount() + 1;

            for (int line = 0; line < vLines; line++)
            {
                int  row     = _topLine + line;
                long offset2 = (long)row * _bytesPerLine;
                if (offset2 >= DataLength) break;

                double y2   = dividerY + line * rowH;
                double xHex = hexStartX;
                double xAsc = asciiStartX;

                TxCenter(ds, FormatOffset(offset2), 0, y2, offsetW, rowH, cOffset, fmt);

                for (int i = 0; i < _bytesPerLine; i++)
                {
                    long bi = offset2 + i;
                    if (bi >= DataLength) break;
                    bool   isSel = hasSelT && bi >= sS && bi <= sE;
                    double step  = HexStep(i, g);
                    Color  hexFg = isSel ? cSelFg : cData;

                    TxCenter(ds, ByteAt(bi).ToString("X2"), xHex, y2,
                              2 * _charWidth, rowH, hexFg, fmt);

                    if (ShowAsciiPanel)
                    {
                        char  ch = ByteToAsciiChar(ByteAt(bi));
                        Color fg = isSel ? cSelFg : cData;
                        TxCenter(ds, ch.ToString(), xAsc, y2,
                                  _charWidth + asciiSpacing, rowH, fg, fmt);
                        xAsc += _charWidth + asciiSpacing;
                    }
                    xHex += step;
                }
            }

            // PASS 6 – hover highlight (1px border on hovered byte in both panels)
            if (_hoverByte >= 0 && _hoverByte < DataLength)
            {
                int hoverRow = (int)(_hoverByte / _bytesPerLine);
                // Only draw if row is visible
                if (hoverRow >= _topLine && hoverRow < _topLine + vLines)
                {
                    var (hx, hy, hw, hh) = HexCellRect(_hoverByte, dividerY, rowH);
                    ds.DrawRectangle((float)hx, (float)hy, (float)hw, (float)hh, cHover, 1f);

                    if (ShowAsciiPanel)
                    {
                        var (ax, ay, aw, ah) = AsciiCellRect(_hoverByte, dividerY, rowH);
                        ds.DrawRectangle((float)ax, (float)ay, (float)aw, (float)ah, cHover, 1f);
                    }
                }
            }

            RenderScrollBar(ds, wf, h);
        }

        private void RenderScrollBar(CanvasDrawingSession ds, double wf, double h)
        {
            double sbX    = wf - ScrollBarWidth;
            double sw     = ScrollBarWidth;
            bool   canScr = _sbTotalLines > _sbVisibleLines;
            bool   dark   = IsDark;

            Color trackColor = dark ? Color.FromArgb(255,44,44,44)    : Color.FromArgb(255,240,240,240);
            Color btnColor   = dark ? Color.FromArgb(255,60,60,60)    : Color.FromArgb(255,225,225,225);
            Color arrowColor = dark ? Color.FromArgb(255,180,180,180) : Color.FromArgb(255,100,100,100);
            Color thumbColor = !canScr ? Colors.Transparent
                             : dark
                                 ? (_sbDragging ? Color.FromArgb(255,190,190,190) : Color.FromArgb(255,120,120,120))
                                 : (_sbDragging ? Color.FromArgb(255,90,90,90)    : Color.FromArgb(255,152,152,152));

            double areaTop  = _cachedDividerY + 1;
            double btnH     = sw;
            double btnUpTop = areaTop;
            double btnUpBot = areaTop + btnH;
            _sbBtnUpBottom  = btnUpBot;

            ds.FillRectangle((float)sbX, (float)btnUpTop, (float)sw, (float)btnH, btnColor);
            float acx = (float)(sbX + sw / 2), amid = (float)(btnUpTop + btnH / 2);
            for (int i = 0; i < 4; i++)
                ds.FillRectangle(acx - (1+i), amid - 2 + i, (1+i)*2, 1, arrowColor);

            double btnDnTop = h - btnH;
            _sbBtnDnTop     = btnDnTop;
            ds.FillRectangle((float)sbX, (float)btnDnTop, (float)sw, (float)btnH, btnColor);
            float dmid = (float)(btnDnTop + btnH / 2);
            for (int i = 0; i < 4; i++)
                ds.FillRectangle(acx - (4-i), dmid - 2 + i, (4-i)*2, 1, arrowColor);

            double trackTop = btnUpBot;
            double trackH   = Math.Max(0, btnDnTop - trackTop);
            ds.FillRectangle((float)sbX, (float)trackTop, (float)sw, (float)trackH, trackColor);

            if (canScr && trackH > 0)
            {
                double thumbH = Math.Max(20, trackH * _sbVisibleLines / (double)_sbTotalLines);
                int    maxTop = _sbTotalLines - _sbVisibleLines;
                double thumbY = trackTop + (trackH - thumbH) * _topLine / (double)maxTop;
                _sbThumbTop   = thumbY;
                _sbThumbBot   = thumbY + thumbH;
                ds.FillRoundedRectangle((float)(sbX+3), (float)thumbY,
                                        (float)(sw-6), (float)thumbH, 3, 3, thumbColor);
            }
            else { _sbThumbTop = _sbThumbBot = 0; }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  THEME
        // ═══════════════════════════════════════════════════════════════════

        private bool IsDark => ActualTheme == ElementTheme.Dark;

        private void ApplyTheme()
        {
            bool dark = IsDark;
            Background = new SolidColorBrush(dark
                ? Color.FromArgb(255, 30, 30, 30)   : Color.FromArgb(255, 254, 254, 254));
            Foreground = new SolidColorBrush(dark
                ? Color.FromArgb(255,220,220,220)   : Color.FromArgb(255,  27,  27,  27));
            OffsetForeground = new SolidColorBrush(dark
                ? Color.FromArgb(255,100,180,255)   : Color.FromArgb(255,   0,   0, 139));
            DividerBrush = new SolidColorBrush(dark
                ? Color.FromArgb(255, 80, 80, 80)   : Color.FromArgb(255, 160, 160, 160));
            SelectionBackground = new SolidColorBrush(dark
                ? Color.FromArgb(255,  0,100,200)   : Color.FromArgb(255,   0, 120, 215));
            SelectionForeground = new SolidColorBrush(Colors.White);
            RulerForeground     = null;   // auto-derive from Background via luminance
            // Offset and ASCII bands: explicit values for both light and dark
            OffsetBackground = new SolidColorBrush(dark
                ? Color.FromArgb(255, 40, 40, 40)   : Color.FromArgb(255, 242, 242, 242));
            AsciiBackground  = new SolidColorBrush(dark
                ? Color.FromArgb(255, 40, 40, 40)   : Color.FromArgb(255, 242, 242, 242));
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts a single byte to its display character using the current
        /// <see cref="AsciiEncoding"/>. Non-printable / undefined glyphs are
        /// replaced with '·' (U+00B7) so they are visually distinct from '.'.
        /// </summary>
        private char ByteToAsciiChar(byte b)
        {
            try
            {
                string s = _asciiEncoding.GetString(new[] { b });
                if (s.Length == 0) return '·';
                char c = s[0];
                // Treat control characters and the replacement char as non-printable
                if (c < 0x20 || c == 0x7F || c == '\uFFFD') return '·';
                return c;
            }
            catch { return '·'; }
        }

        private string FormatOffset(long offset) => OffsetFormat switch
        {
            OffsetFormat.Hexadecimal => $"  {offset:X8}     ",
            OffsetFormat.Decimal     => $"  {offset,10}   ",
            OffsetFormat.Octal       => $"  {ToOctal(offset)}  ",
            _                        => $"  {offset:X8}     "
        };

        private static string ToOctal(long value)
        {
            if (value == 0) return "00000000000";
            var sb = new System.Text.StringBuilder();
            ulong v = (ulong)value;
            while (v > 0) { sb.Insert(0, (char)('0' + (v & 7))); v >>= 3; }
            while (sb.Length < 11) sb.Insert(0, '0');
            return sb.ToString();
        }

        private bool HasSelection() => _selStart >= 0 && _selEnd >= 0 && DataLength > 0;

        private static void SetClipboard(string text)
        {
            var pkg = new DataPackage(); pkg.SetText(text); Clipboard.SetContent(pkg);
        }

        private void FireSelectionChanged()
        {
            if (SelectionChanged == null) return;
            if (_selStart < 0 || _selEnd < 0 || DataLength == 0)
            {
                SelectionChanged(this, new HexSelectionChangedEventArgs(-1, 0, Array.Empty<byte>()));
                return;
            }
            long s = Math.Min(_selStart, _selEnd), e = Math.Max(_selStart, _selEnd);
            long len     = e - s + 1;
            int  copyLen = (int)Math.Min(len, 5 * 1024 * 1024);
            var  copy    = new byte[copyLen];
            if (_bytes != null)
                Array.Copy(_bytes, (int)s, copy, 0, copyLen);
            else if (_mmva != null)
                _mmva.ReadArray(s, copy, 0, copyLen);
            SelectionChanged(this, new HexSelectionChangedEventArgs(s, len, copy));
        }
    }
}
