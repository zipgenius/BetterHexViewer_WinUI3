using Microsoft.UI.Xaml.Controls;

namespace BetterHexViewer.Demo
{
    public sealed partial class MainWindow
    {
        // Resolve resources once at startup, before displaying the window.
        private void ApplyLocalization()
        {
            Title = Locale.Get("WindowTitle");
            HexViewer.NoDataLoadedText = Locale.Get("DropFile");
            TxtFileName.Text = Locale.Get("NoFile");
            LabelFileSection.Text = Locale.Get("FileSection");
            BtnOpenFile.Content = Locale.Get("OpenFile");
            ToolTipService.SetToolTip(BtnOpenFile, Locale.Get("OpenFileTip"));
            BtnShowSelection.Content = Locale.Get("ShowSelection");
            ToolTipService.SetToolTip(BtnShowSelection, Locale.Get("ShowSelectionTip"));
            LabelSearchSection.Text = Locale.Get("SearchSection");
            RbSearchText.Content = Locale.Get("TextMode");
            RbSearchHex.Content = Locale.Get("HexMode");
            TxtSearch.PlaceholderText = Locale.Get("SearchPlaceholder");
            TxtSearchHint.Text = Locale.Get("SearchHint");
            ChkIgnoreCase.Content = Locale.Get("IgnoreCase");
            BtnSearchPrev.Content = Locale.Get("Previous");
            ToolTipService.SetToolTip(BtnSearchPrev, Locale.Get("PreviousTip"));
            BtnSearchNext.Content = Locale.Get("Next");
            ToolTipService.SetToolTip(BtnSearchNext, Locale.Get("NextTip"));
            BtnFindAll.Content = Locale.Get("FindAll");
            ToolTipService.SetToolTip(BtnFindAll, Locale.Get("FindAllTip"));
            LabelFontSection.Text = Locale.Get("FontSection");
            LabelFontFamily.Text = Locale.Get("FontFamily");
            ToolTipService.SetToolTip(CmbFont, Locale.Get("FontFamilyTip"));
            LabelFontSize.Text = Locale.Get("FontSize");
            ToolTipService.SetToolTip(BtnFontMinus, Locale.Get("DecreaseFont"));
            ToolTipService.SetToolTip(BtnFontPlus, Locale.Get("IncreaseFont"));
            TglBold.Content = Locale.Get("Bold");
            ToolTipService.SetToolTip(TglBold, Locale.Get("BoldTip"));
            LabelLayoutSection.Text = Locale.Get("LayoutSection");
            LabelOffsetFormat.Text = Locale.Get("OffsetFormat");
            ToolTipService.SetToolTip(CmbOffset, Locale.Get("OffsetFormatTip"));
            LabelHexadecimal.Content = Locale.Get("Hexadecimal");
            LabelDecimal.Content = Locale.Get("Decimal");
            LabelOctal.Content = Locale.Get("Octal");
            LabelColumnGrouping.Text = Locale.Get("ColumnGrouping");
            ToolTipService.SetToolTip(CmbGroup, Locale.Get("ColumnGroupingTip"));
            LabelOneByte.Content = Locale.Get("OneByte");
            LabelByteCount2.Content = Locale.Format("ByteCount", 2);
            LabelByteCount4.Content = Locale.Format("ByteCount", 4);
            LabelByteCount8.Content = Locale.Format("ByteCount", 8);
            LabelByteCount16.Content = Locale.Format("ByteCount", 16);
            LabelAsciiEncoding.Text = Locale.Get("AsciiEncoding");
            ToolTipService.SetToolTip(CmbEncoding, Locale.Get("AsciiEncodingTip"));
            LabelByteSpacing.Text = Locale.Get("ByteSpacing");
            ToolTipService.SetToolTip(SldrSpacing, Locale.Get("ByteSpacingTip"));
            LabelRowGap.Text = Locale.Get("RowGap");
            ToolTipService.SetToolTip(SldrLineGap, Locale.Get("RowGapTip"));
            TglFullWidth.Content = Locale.Get("FullWidth");
            ToolTipService.SetToolTip(TglFullWidth, Locale.Get("FullWidthTip"));
            TglAscii.Content = Locale.Get("AsciiPanel");
            ToolTipService.SetToolTip(TglAscii, Locale.Get("AsciiPanelTip"));
            LabelColoursSection.Text = Locale.Get("ColoursSection");
            LabelThemePreset.Text = Locale.Get("ThemePreset");
            ToolTipService.SetToolTip(CmbTheme, Locale.Get("ThemePresetTip"));
            LabelLight.Content = Locale.Get("Light");
            LabelDark.Content = Locale.Get("Dark");
            LabelMatrix.Content = Locale.Get("Matrix");
            LabelBackground.Text = Locale.Get("Background");
            ToolTipService.SetToolTip(BtnColorBg, Locale.Get("BackgroundTip"));
            LabelForeground.Text = Locale.Get("Foreground");
            ToolTipService.SetToolTip(BtnColorFg, Locale.Get("ForegroundTip"));
            LabelOffsetText.Text = Locale.Get("OffsetText");
            ToolTipService.SetToolTip(BtnColorOffset, Locale.Get("OffsetTextTip"));
            LabelOffsetBackground.Text = Locale.Get("OffsetBackground");
            ToolTipService.SetToolTip(BtnColorOffsetBg, Locale.Get("OffsetBackgroundTip"));
            LabelAsciiBackground.Text = Locale.Get("AsciiBackground");
            ToolTipService.SetToolTip(BtnColorAsciiBg, Locale.Get("AsciiBackgroundTip"));
            LabelDivider.Text = Locale.Get("Divider");
            ToolTipService.SetToolTip(BtnColorDivider, Locale.Get("DividerTip"));
            LabelSelectionBackground.Text = Locale.Get("SelectionBackground");
            ToolTipService.SetToolTip(BtnColorSelBg, Locale.Get("SelectionBackgroundTip"));
            LabelSelectionText.Text = Locale.Get("SelectionText");
            ToolTipService.SetToolTip(BtnColorSelFg, Locale.Get("SelectionTextTip"));
            BtnResetColors.Content = Locale.Get("ResetColours");
            ToolTipService.SetToolTip(BtnResetColors, Locale.Get("ResetColoursTip"));
        }
    }
}
