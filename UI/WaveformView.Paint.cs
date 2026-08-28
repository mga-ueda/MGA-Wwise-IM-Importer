using System.Drawing;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TextAlignment = System.Windows.TextAlignment;
using WpfColor = System.Windows.Media.Color;

namespace MgaWwiseIMImporter.UI;

internal sealed partial class WaveformView
{
    /// <summary>
    /// Alt+マーカードラッグ中、移動ペア（ドラッグ中／一つ前）の位置に薄い縦ガイドを出す。
    /// </summary>
    private void DrawAltMarkerPairDragGuides(Graphics g, Rectangle timeline)
    {
        if (!_isDraggingMarker
            || !_allowsSessionMarkerEdit
            || (ModifierKeys & System.Windows.Input.ModifierKeys.Alt) == 0
            || _markerDragPreviewSample is not { } preview
            || _peaks is null
            || _peaks.FrameCount <= 0
            || timeline.Width <= 0)
        {
            return;
        }

        if (!TryGetPreviousMarkerSample(_markerDragFromSample, out var previousSample))
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        var delta = preview - _markerDragFromSample;
        var draggedSample = preview;
        var pairedSample = previousSample + delta;

        using var pen = new Pen(Color.FromArgb(150, WaveformGdiColors.MarkerTriangle), 1f);
        DrawMarkerPositionGuideLine(g, timeline, draggedSample, frameCount, pen);
        DrawMarkerPositionGuideLine(g, timeline, pairedSample, frameCount, pen);
    }

    private void DrawMarkerPositionGuideLine(
        Graphics g,
        Rectangle timeline,
        long sampleOffset,
        long frameCount,
        Pen pen)
    {
        var absolute = SampleToAbsolute(sampleOffset, frameCount);
        if (absolute < _viewStart - 1e-9 || absolute > ViewEnd + 1e-9)
        {
            return;
        }

        var x = AbsoluteToX(absolute, timeline);
        g.DrawLine(pen, x, timeline.Top, x, timeline.Bottom);
    }

    /// <summary>
    /// 無効 Playlist の範囲をテーマ背景色で覆い、約 25% 不透明度に見せる。
    /// Measure?Marker・波形・Music Segment／Playlist レーン全体を覆う。
    /// </summary>
    private void DrawDisabledPlaylistDimOverlay(Graphics g)
    {
        if (_disabledPlaylistPartNumbers.Count == 0
            || _peaks is null
            || _peaks.FrameCount <= 0
            || _outputParts.Count == 0)
        {
            return;
        }

        var layoutContent = ContentBounds;
        var (_, labels, wave, playlistLane, segmentLane, _) = GetLayout(layoutContent, g);
        if (wave.Width <= 0)
        {
            return;
        }

        var top = labels.Height > 0 ? labels.Top : wave.Top;
        var bottom = wave.Bottom;
        if (segmentLane.Height > 0)
        {
            bottom = Math.Max(bottom, segmentLane.Bottom);
        }

        if (playlistLane.Height > 0)
        {
            bottom = Math.Max(bottom, playlistLane.Bottom);
        }

        var bandHeight = bottom - top;
        if (bandHeight <= 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        // 191/255 ? 75%。背景で覆うと下の描画が約 25% 残って見える。
        using var brush = new SolidBrush(Color.FromArgb(191, WaveformGdiColors.WaveformBack));
        var previousSmoothing = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        try
        {
            foreach (var part in _outputParts)
            {
                if (!_disabledPlaylistPartNumbers.Contains(part.Number))
                {
                    continue;
                }

                var a0 = SampleToAbsolute(part.StartSampleOffset, frameCount);
                var a1 = SampleToAbsolute(part.EndSampleOffset, frameCount);
                if (!TryMapAbsoluteRange(a0, a1, wave, out var x0, out var x1))
                {
                    continue;
                }

                // 境界線が見切れないよう、対象範囲の前後 1px も含める。
                x0 = Math.Max(wave.Left, x0 - 1f);
                x1 = Math.Min(wave.Right, x1 + 1f);
                var width = Math.Max(1f, x1 - x0);
                g.FillRectangle(brush, x0, top, width, bandHeight);
            }
        }
        finally
        {
            g.SmoothingMode = previousSmoothing;
        }
    }

    /// <summary>
    /// Music Playlist 側のグループ色をそのまま（アルファ編集なし）で
    /// Music Segment／Playlist 名前レーンへ塗る。波形本体には着色しない。
    /// </summary>
    private void DrawPlaylistGroupNameLaneOverlays(Graphics g)
    {
        if (_playlistGroupColors.Count == 0
            || _peaks is null
            || _peaks.FrameCount <= 0
            || _outputParts.Count == 0)
        {
            return;
        }

        var layoutContent = ContentBounds;
        var (_, _, wave, playlistLane, segmentLane, _) = GetLayout(layoutContent, g);
        if (wave.Width <= 0
            || (playlistLane.Height <= 0 && segmentLane.Height <= 0))
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        var previousSmoothing = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        try
        {
            foreach (var part in _outputParts)
            {
                if (_disabledPlaylistPartNumbers.Contains(part.Number))
                {
                    continue;
                }

                if (!_playlistGroupColors.TryGetValue(part.Number, out var color))
                {
                    continue;
                }

                var start = SampleToAbsolute(part.StartSampleOffset, frameCount);
                var end = SampleToAbsolute(part.EndSampleOffset, frameCount);
                if (!TryMapAbsoluteRange(start, end, wave, out var x0, out var x1)
                    || x1 - x0 < 1f)
                {
                    continue;
                }

                // Playlist スウォッチと同じ色を不透明のまま使う（薄めない）。
                // Segment レーンだけ少し濃くして上下の見分けを付ける。
                var drawing = WaveformGdiColors.ToDrawing(color);
                if (segmentLane.Height > 0)
                {
                    using var segmentFill = new SolidBrush(DarkenGroupColorForSegmentLane(drawing));
                    g.FillRectangle(segmentFill, x0, segmentLane.Top, x1 - x0, segmentLane.Height);
                }

                if (playlistLane.Height > 0)
                {
                    using var fill = new SolidBrush(drawing);
                    g.FillRectangle(fill, x0, playlistLane.Top, x1 - x0, playlistLane.Height);
                }
            }
        }
        finally
        {
            g.SmoothingMode = previousSmoothing;
        }
    }

    /// <summary>
    /// グループ色で塗りつぶしたレーン上に、白／黒のコントラスト文字で名前を描き直す。
    /// </summary>
    private void DrawNameLaneLabelsOverGroupColors(Graphics g)
    {
        if (_playlistGroupColors.Count == 0
            || _peaks is null
            || _peaks.FrameCount <= 0)
        {
            return;
        }

        var layoutContent = ContentBounds;
        var (_, _, wave, playlistLane, segmentLane, _) = GetLayout(layoutContent, g);
        if (wave.Width <= 0)
        {
            return;
        }

        if (playlistLane.Height > 0 && _outputParts.Count > 0)
        {
            var items = new List<(string Text, long Start, long End, Color Back)>();
            foreach (var part in _outputParts)
            {
                if (_disabledPlaylistPartNumbers.Contains(part.Number)
                    || !_playlistGroupColors.TryGetValue(part.Number, out var color))
                {
                    continue;
                }

                var name = _playlistDisplayNames.TryGetValue(part.Number, out var displayName)
                    ? displayName
                    : Path.GetFileNameWithoutExtension(part.FileName);
                if (string.IsNullOrEmpty(name))
                {
                    name = part.FileName;
                }

                items.Add((
                    $"{name} (.wav)",
                    part.StartSampleOffset,
                    part.EndSampleOffset,
                    WaveformGdiColors.ToDrawing(color)));
            }

            DrawTimedNameLaneWithBackColors(g, wave, playlistLane, items, FontStyle.Regular);
        }

        if (segmentLane.Height > 0 && _segmentNames.Count > 0)
        {
            var items = new List<(string Text, long Start, long End, Color Back)>();
            foreach (var segment in _segmentNames)
            {
                if (!TryGetGroupColorCoveringSample(segment.StartSampleOffset, out var color))
                {
                    continue;
                }

                items.Add((
                    segment.Name,
                    segment.StartSampleOffset,
                    segment.EndSampleOffset,
                    DarkenGroupColorForSegmentLane(color)));
            }

            DrawTimedNameLaneWithBackColors(g, wave, segmentLane, items, FontStyle.Regular);
            DrawSegmentLaneDividers(g, wave, segmentLane);
        }
    }

    /// <summary>Music Segment Name レーン用にグループ色を少し濃くする（Playlist レーンとの差）。</summary>
    private static Color DarkenGroupColorForSegmentLane(Color color)
    {
        const float factor = 0.5f;
        return Color.FromArgb(
            color.A,
            (int)Math.Round(color.R * factor),
            (int)Math.Round(color.G * factor),
            (int)Math.Round(color.B * factor));
    }

    private bool TryGetGroupColorCoveringSample(long sampleOffset, out Color color)
    {
        foreach (var part in _outputParts)
        {
            if (_disabledPlaylistPartNumbers.Contains(part.Number)
                || sampleOffset < part.StartSampleOffset
                || sampleOffset >= part.EndSampleOffset
                || !_playlistGroupColors.TryGetValue(part.Number, out var wpf))
            {
                continue;
            }

            color = WaveformGdiColors.ToDrawing(wpf);
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>
    /// 背景が明るいときは黒、暗いときは白（既定の OutputPartFg＝白系）を選ぶ。
    /// </summary>
    private static Color PickContrastingForeColor(Color back)
    {
        // ITU-R BT.601 近似。閾値付近の暖色でも白が沈まないようやや高め。
        var y = (back.R * 299 + back.G * 587 + back.B * 114) / 1000;
        return y >= 140 ? Color.Black : WaveformGdiColors.OutputPartFg;
    }

    private void DrawEmptyScaffold(Graphics g, Rectangle bounds)
    {
        var content = ContentBoundsOf(bounds, ContentPadPx);
        var (info, labels, wave, playlistLane, segmentLane, rowHeight) = GetLayout(content, g);
        DrawInfoLane(g, info, labels, wave, playlistLane, segmentLane, rowHeight, LabelRowCount);
        DrawLabelRows(g, labels, rowHeight, LabelRowCount);
        DrawNameLaneBackgrounds(g, playlistLane, segmentLane);

        if (_peaks is not null && !_peaks.IsEmpty)
        {
            return;
        }

        using var brush = new SolidBrush(WaveformGdiColors.EmptyHint);
        var message = UiStrings.WaveformEmptyHint;
        var size = g.MeasureString(message, Font);
        var centerX = wave.Width > 0
            ? wave.Left + (wave.Width - size.Width) / 2f
            : (bounds.Width - size.Width) / 2f;
        var centerY = wave.Height > 0
            ? wave.Top + (wave.Height - size.Height) / 2f
            : (bounds.Height - size.Height) / 2f;
        g.DrawString(message, Font, brush, centerX, centerY);
    }

    /// <summary>
    /// 左: 行ラベル／波形名、右上: ラベル4行、右中: 波形、
    /// 右下: Music Segment Name / Music Playlist Name。
    /// </summary>
    private (
        Rectangle Info,
        Rectangle Labels,
        Rectangle Wave,
        Rectangle PlaylistLane,
        Rectangle SegmentLane,
        float RowHeight)
        GetLayout(Rectangle content, Graphics g)
    {
        // WinForms と同じ: Font.GetHeight(g)+2（デバイス px ビットマップ上）。
        var rowHeight = Font.GetHeight(g) + 2f;
        var labelsHeight = (int)Math.Ceiling(rowHeight * LabelRowCount);
        var nameLaneHeight = (int)Math.Ceiling(rowHeight);
        var infoLaneWidth = MeasureInfoLaneWidth(g, content.Width);
        if (infoLaneWidth != _infoLaneWidth)
        {
            _infoLaneWidth = infoLaneWidth;
            if (IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    if (!IsDisposed)
                    {
                        InfoLaneWidthChanged?.Invoke(this, EventArgs.Empty);
                    }
                });
            }
        }

        var mainLeft = content.Left + _infoLaneWidth + InfoLaneSeparatorPx;
        var mainWidth = Math.Max(0, content.Width - _infoLaneWidth - InfoLaneSeparatorPx);

        var info = new Rectangle(content.Left, content.Top, _infoLaneWidth, content.Height);
        var labels = new Rectangle(mainLeft, content.Top, mainWidth, labelsHeight);
        var waveTop = content.Top + labelsHeight + LabelWaveGapPx;
        var belowLabels = Math.Max(0, content.Bottom - waveTop);

        const int bottomLaneCount = 2;
        var bottomTotal = belowLabels >= LabelWaveGapPx + nameLaneHeight * bottomLaneCount
            ? nameLaneHeight * bottomLaneCount
            : 0;

        var waveHeight = Math.Max(
            0,
            belowLabels - (bottomTotal > 0 ? LabelWaveGapPx + bottomTotal : 0));
        var wave = new Rectangle(mainLeft, waveTop, mainWidth, waveHeight);

        Rectangle playlistLane;
        Rectangle segmentLane;
        if (bottomTotal > 0)
        {
            // 上: Music Segment Name / 下: Music Playlist Name（高さは Measure 行と同じ）
            playlistLane = new Rectangle(
                mainLeft,
                content.Bottom - nameLaneHeight,
                mainWidth,
                nameLaneHeight);
            segmentLane = new Rectangle(
                mainLeft,
                content.Bottom - nameLaneHeight * 2,
                mainWidth,
                nameLaneHeight);
        }
        else
        {
            playlistLane = Rectangle.Empty;
            segmentLane = Rectangle.Empty;
        }

        return (info, labels, wave, playlistLane, segmentLane, rowHeight);
    }

    private int MeasureInfoLaneWidth(Graphics g, int contentWidth)
    {
        float maxText = 0f;
        using var infoFont = new Font(Font, FontStyle.Bold);
        foreach (var label in InfoRowLabels)
        {
            maxText = Math.Max(maxText, g.MeasureString(label, infoFont).Width);
        }

        maxText = Math.Max(maxText, g.MeasureString(UiStrings.LabelMusicPlaylistName, infoFont).Width);
        maxText = Math.Max(maxText, g.MeasureString(UiStrings.LabelMusicSegmentName, infoFont).Width);
        if (_sourceDisplayName.Length > 0)
        {
            maxText = Math.Max(
                maxText,
                g.MeasureString(_sourceDisplayName, infoFont).Width
                + 2f
                + SourceMeterGapPx
                + SourceMeterWidthPx);
        }

        // ファイル名と右側メーターが一行で収まる必要幅へ自動調整する。
        var width = (int)Math.Ceiling(maxText) + InfoLanePadX * 2;
        var maxAllowed = Math.Max(
            InfoLanePadX * 2 + 1,
            contentWidth - InfoLaneSeparatorPx);
        return Math.Clamp(width, InfoLanePadX * 2 + 1, maxAllowed);
    }

    private Rectangle GetTimelineContentRect()
    {
        var content = ContentBounds;
        return GetTimelineRect(content);
    }

    private Rectangle GetTimelineRect(Rectangle content)
    {
        var inset = _infoLaneWidth + InfoLaneSeparatorPx;
        var mainLeft = content.Left + inset;
        var mainWidth = Math.Max(0, content.Width - inset);
        return new Rectangle(mainLeft, content.Top, mainWidth, content.Height);
    }

    private void BuildStaticLayer(Rectangle bounds)
    {
        // サイズ・DPI が同じなら Bitmap を作り直さず再利用する（ズーム連打時の GC 圧を抑える）
        var dpi = DeviceDpi;
        if (_staticLayer is null
            || _staticLayer.Width != bounds.Width
            || _staticLayer.Height != bounds.Height
            || Math.Abs(_staticLayer.HorizontalResolution - dpi) > 0.1f)
        {
            DisposeStaticLayer();
            _staticLayer = new Bitmap(
                bounds.Width,
                bounds.Height,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            _staticLayer.SetResolution(dpi, dpi);
        }

        using var g = Graphics.FromImage(_staticLayer);
        g.Clear(WaveformGdiColors.WaveformBack);
        // 文字は AntiAlias、波形柱は DrawWaveform 側で SmoothingMode.None。
        // HighQuality 合成はズーム連打時の負荷が大きいので抑える（Form1 OnPaint 相当）。
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;

        var content = ContentBoundsOf(bounds, ContentPadPx);
        var (info, labels, wave, playlistLane, segmentLane, rowHeight) = GetLayout(content, g);
        DrawInfoLane(g, info, labels, wave, playlistLane, segmentLane, rowHeight, LabelRowCount);
        DrawLabelRows(g, labels, rowHeight, LabelRowCount);
        DrawNameLaneBackgrounds(g, playlistLane, segmentLane);
        DrawWaveform(g, wave);
        DrawBars(g, labels, wave, rowHeight);
        // Entry/Exit の上にマーカーを重ねる（同位置でもマーカーを優先表示）
        DrawContiguousRegionCueMarkers(g, wave, labels, rowHeight);
        DrawMarkers(g, labels, rowHeight);
        DrawPlaylistNameLabels(g, wave, playlistLane);
        DrawSegmentNameLabels(g, wave, segmentLane);
        // -R は名前レーン上にも被せる（波形上は DrawWaveform 内で済み）
        DrawExcludedRegionOverlaysOnNameLanes(g, wave, segmentLane, playlistLane);
        _staticLayerDirty = false;
    }

    private static void DrawNameLaneBackgrounds(
        Graphics g,
        Rectangle playlistLane,
        Rectangle segmentLane)
    {
        if (segmentLane.Height > 0)
        {
            using var segmentBg = new SolidBrush(WaveformGdiColors.MusicSegmentLaneBg);
            g.FillRectangle(segmentBg, segmentLane);
        }

        if (playlistLane.Height > 0)
        {
            using var playlistBg = new SolidBrush(WaveformGdiColors.MusicPlaylistLaneBg);
            g.FillRectangle(playlistBg, playlistLane);
        }
    }

    /// <summary>情報レーン 4 行（小節番号／テンポ／拍子／マーカー）の背景色。都度取得（色は実行時に変わり得る）。</summary>
    private static Color[] InfoRowBackColors =>
    [
        WaveformGdiColors.BarNumberBg,
        WaveformGdiColors.TempoBg,
        WaveformGdiColors.SignatureBg,
        WaveformGdiColors.MarkerRowBg,
    ];

    private void DrawInfoLane(
        Graphics g,
        Rectangle info,
        Rectangle labels,
        Rectangle wave,
        Rectangle playlistLane,
        Rectangle segmentLane,
        float rowHeight,
        int visibleRowCount)
    {
        if (info.Width <= 0 || info.Height <= 0 || visibleRowCount <= 0)
        {
            return;
        }

        var rowColors = InfoRowBackColors;

        using var textBrush = new SolidBrush(WaveformGdiColors.WaveformInfoFg);
        using var disabledTextBrush = new SolidBrush(WaveformGdiColors.TransportDisabledFore);
        using var infoFont = new Font(Font, FontStyle.Bold);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        // 小節情報がないとき（Wave 単体など）は Measure / Tempo / Signature をグレーアウト。
        var musicalEnabled = _bars.Count > 0;
        var count = Math.Min(visibleRowCount, InfoRowLabels.Count);
        for (var i = 0; i < count; i++)
        {
            var top = labels.Top + i * rowHeight;
            using var bg = new SolidBrush(rowColors[i]);
            g.FillRectangle(bg, info.Left, top, info.Width, rowHeight);
            var labelBrush = !musicalEnabled && i < 3
                ? disabledTextBrush
                : textBrush;
            g.DrawString(
                InfoRowLabels[i],
                infoFont,
                labelBrush,
                new RectangleF(
                    info.Left + InfoLanePadX,
                    top,
                    Math.Max(0, info.Width - InfoLanePadX * 2),
                    rowHeight),
                format);
        }

        // 情報レーンとタイムラインの区切り（波形背景色・3px）
        using (var sepBrush = new SolidBrush(WaveformGdiColors.WaveformBack))
        {
            g.FillRectangle(
                sepBrush,
                info.Right,
                info.Top,
                InfoLaneSeparatorPx,
                info.Height);
        }

        DrawBottomLaneInfoLabel(
            g,
            info,
            segmentLane,
            UiStrings.LabelMusicSegmentName,
            infoFont,
            textBrush,
            format,
            WaveformGdiColors.MusicSegmentLaneBg);
        DrawBottomLaneInfoLabel(
            g,
            info,
            playlistLane,
            UiStrings.LabelMusicPlaylistName,
            infoFont,
            textBrush,
            format,
            WaveformGdiColors.MusicPlaylistLaneBg);

        if (_sourceDisplayName.Length == 0 || wave.Height <= 0)
        {
            return;
        }

        // マーカー行の下＝波形エリア左。右側の縦メーターを避け、一行で表示する。
        var nameWidth = Math.Max(
            0,
            info.Width
            - InfoLanePadX * 2
            - SourceMeterGapPx
            - SourceMeterWidthPx);
        var namePadY = 2f;
        var nameHeight = Math.Max(0, wave.Height - namePadY * 2f);
        if (nameWidth <= 0 || nameHeight <= 0)
        {
            return;
        }

        using var nameFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(
            _sourceDisplayName,
            infoFont,
            textBrush,
            new RectangleF(info.Left + InfoLanePadX, wave.Top + namePadY, nameWidth, nameHeight),
            nameFormat);
    }

    /// <summary>
    /// ファイル名ホバー枠。静的レイヤには焼き込まず、毎フレーム最前面に描く
    /// （編集中 TextBox と同じ矩形・枠色）。
    /// </summary>
    private void DrawSourceNameHoverChrome(Graphics g)
    {
        if (!_sourceNameEditable
            || !_sourceNameHovered
            || _sourceNameEditor is { Visibility: System.Windows.Visibility.Visible }
            || !TryGetSourceNameBounds(out var available))
        {
            return;
        }

        var hoverBounds = GetSourceNameHoverBounds(available);
        if (hoverBounds.Width <= 0 || hoverBounds.Height <= 0)
        {
            return;
        }

        using var fill = new SolidBrush(WaveformGdiColors.ForControlBack(WaveformGdiColors.DialogInputBack));
        g.FillRectangle(fill, hoverBounds);

        using var font = new Font(Font, FontStyle.Bold);
        using var nameBrush = new SolidBrush(WaveformGdiColors.DialogFore);
        using var nameFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(_sourceDisplayName, font, nameBrush, hoverBounds, nameFormat);

        // Digits エディタ（FixedSingle）と同じ枠色で描く。
        using var hoverPen = new Pen(GetFixedSingleBorderColor());
        g.DrawRectangle(
            hoverPen,
            hoverBounds.Left,
            hoverBounds.Top,
            hoverBounds.Width - 1,
            hoverBounds.Height - 1);
    }

    /// <summary>
    /// WPF の TextBox 枠色（ChromeMid）に合わせた固定値。
    /// WinForms 版は実際の TextBox を描画して実測していたが、WPF 版ではテーマ色を直接使う。
    /// </summary>
    private static Color GetFixedSingleBorderColor() => WaveformGdiColors.ChromeMid;

    private void DrawSourceLevelMeter(Graphics g, Rectangle content)
    {
        if (_sourceDisplayName.Length == 0)
        {
            return;
        }

        var (info, _, wave, _, _, _) = GetLayout(content, g);
        var meter = new Rectangle(
            info.Right - SourceMeterWidthPx,
            wave.Top,
            SourceMeterWidthPx,
            wave.Height);
        if (meter.Width <= 0 || meter.Height <= 0)
        {
            return;
        }

        using var trackBrush = new SolidBrush(WaveformGdiColors.WaveformSourceMeterTrack);
        g.FillRectangle(trackBrush, meter);

        var fillHeight = (int)Math.Round(meter.Height * _outputLevel);
        if (fillHeight <= 0)
        {
            return;
        }

        using var levelBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
            meter,
            WaveformGdiColors.WaveformSourceMeterMaximum,
            WaveformGdiColors.WaveformSourceMeterMinimum,
            System.Drawing.Drawing2D.LinearGradientMode.Vertical);
        g.FillRectangle(
            levelBrush,
            meter.X,
            meter.Bottom - fillHeight,
            meter.Width,
            fillHeight);
    }

    private void DrawBottomLaneInfoLabel(
        Graphics g,
        Rectangle info,
        Rectangle lane,
        string text,
        Font font,
        Brush textBrush,
        StringFormat format,
        Color laneBackColor)
    {
        if (lane.Height <= 0)
        {
            return;
        }

        var padX = InfoLanePadX;
        using var laneBg = new SolidBrush(laneBackColor);
        g.FillRectangle(laneBg, info.Left, lane.Top, info.Width, lane.Height);
        g.DrawString(
            text,
            font,
            textBrush,
            new RectangleF(
                info.Left + padX,
                lane.Top,
                Math.Max(0, info.Width - padX * 2),
                lane.Height),
            format);
    }

    private static void DrawLabelRows(Graphics g, Rectangle labels, float rowHeight, int visibleRowCount)
    {
        if (labels.Width <= 0 || labels.Height <= 0 || visibleRowCount <= 0)
        {
            return;
        }

        var rowColors = InfoRowBackColors;
        var count = Math.Min(visibleRowCount, rowColors.Length);
        for (var i = 0; i < count; i++)
        {
            using var brush = new SolidBrush(rowColors[i]);
            g.FillRectangle(brush, labels.Left, labels.Top + i * rowHeight, labels.Width, rowHeight);
        }
    }

    private void DrawWaveform(Graphics g, Rectangle wave)
    {
        if (wave.Width <= 0 || wave.Height <= 0)
        {
            return;
        }

        // -L / -A / -E / 通常グレーは下塗り。-R は後で波形上へ重ねる。
        DrawRegionBackgrounds(g, wave);

        var peaks = _peaks!;
        var midY = wave.Top + wave.Height / 2f;
        using (var zeroDbPen = new Pen(WaveformGdiColors.WaveZeroDbLine, 1f))
        {
            g.DrawLine(zeroDbPen, wave.Left, midY, wave.Right, midY);
        }

        if (peaks.Mins.Length == 0)
        {
            DrawExcludedRegionOverlays(g, wave);
            return;
        }

        // 縦ズーム込み。±1.0 が既定で波形上下端、それ以上はクリップ
        var amplitude = wave.Height * 0.5f * (float)_ampZoom;
        using var wavePen = new Pen(WaveformGdiColors.WaveFill, 1f);
        var displayFades = GetDisplayRegionEdgeFades();
        var frameCount = peaks.FrameCount;

        // 縦 1px 線に AA は不要。無効化すると列描画が大幅に速くなる
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        try
        {
            var detail = EnsureDetailPeaks(wave);
            if (detail is not null && !detail.IsEmpty)
            {
                var startFrame = (long)Math.Floor(_viewStart * frameCount);
                var endFrame = (long)Math.Ceiling(ViewEnd * frameCount);
                startFrame = Math.Clamp(startFrame, 0, frameCount);
                endFrame = Math.Clamp(endFrame, startFrame, frameCount);
                var rangeFrames = endFrame - startFrame;

                // 表示内サンプルが疎?中密度なら、DAW 定番のサンプル間直線折れ線。
                // （曲線補間は実サンプルを偽るので使わない）
                if (CanDrawSamplePolyline(detail, rangeFrames, wave.Width))
                {
                    DrawSamplePolyline(
                        g,
                        wavePen,
                        wave,
                        midY,
                        amplitude,
                        detail,
                        startFrame,
                        frameCount,
                        displayFades);
                }
                else
                {
                    var bucketCount = detail.Mins.Length;
                    for (var px = 0; px < wave.Width; px++)
                    {
                        var bucket = bucketCount == wave.Width
                            ? Math.Clamp(px, 0, bucketCount - 1)
                            : (int)Math.Clamp(
                                Math.Floor((px + 0.5d) / wave.Width * bucketCount),
                                0,
                                bucketCount - 1);
                        var abs = _viewStart + ((px + 0.5d) / wave.Width) * ViewSpan;
                        var sample = (long)Math.Clamp(
                            Math.Floor(abs * frameCount),
                            0,
                            Math.Max(0L, frameCount - 1));
                        var gain = RegionEdgeFade.GainAt(sample, displayFades);
                        DrawPeakColumn(
                            g,
                            wavePen,
                            wave,
                            midY,
                            amplitude * gain,
                            wave.Left + px + 0.5f,
                            detail.Mins[bucket],
                            detail.Maxs[bucket]);
                    }
                }
            }
            else
            {
                // フォールバック: 全体概要ピークを表示窓に写像
                var overviewCount = peaks.Mins.Length;
                for (var px = 0; px < wave.Width; px++)
                {
                    var abs = _viewStart + ((px + 0.5d) / wave.Width) * ViewSpan;
                    var bucket = (int)Math.Clamp(Math.Floor(abs * overviewCount), 0, overviewCount - 1);
                    var sample = (long)Math.Clamp(
                        Math.Floor(abs * frameCount),
                        0,
                        Math.Max(0L, frameCount - 1));
                    var gain = RegionEdgeFade.GainAt(sample, displayFades);
                    DrawPeakColumn(
                        g,
                        wavePen,
                        wave,
                        midY,
                        amplitude * gain,
                        wave.Left + px + 0.5f,
                        peaks.Mins[bucket],
                        peaks.Maxs[bucket]);
                }
            }
        }
        finally
        {
            g.SmoothingMode = smoothing;
        }

        // -R だけ波形の上に重ねる（境界線・Cue 線は別描画）
        DrawExcludedRegionOverlays(g, wave);
    }

    /// <summary>
    /// 深いズーム用: 各サンプルを点として、隣同士を直線で結ぶ（線形補間表示）。
    /// 振幅拡大時は表示矩形へ Y をピン留めせず、クリップで切る（辺張り付きによる破綻を防ぐ）。
    /// 1px に複数サンプルある区間は全点接続せず 1px 1 点に間引き、塗りつぶし状の汚れを防ぐ。
    /// 時間軸が最大ズームのときだけ、実サンプル位置に点を重ねる。
    /// </summary>
    private void DrawSamplePolyline(
        Graphics g,
        Pen wavePen,
        Rectangle wave,
        float midY,
        float amplitude,
        WavPeakData detail,
        long startFrame,
        long frameCount,
        IReadOnlyList<RegionEdgeFade> displayFades)
    {
        var count = detail.Mins.Length;
        if (count <= 0 || frameCount <= 0)
        {
            return;
        }

        // GDI+ は極端な座標で線分が壊れることがあるため、クリップ外に十分な余白だけ残す。
        const float YOverflow = 8192f;
        float SampleY(float sample, float gain)
        {
            var y = midY - sample * amplitude * gain;
            return Math.Clamp(y, wave.Top - YOverflow, wave.Bottom + YOverflow);
        }

        float SampleAt(int index)
        {
            // 1 フレーム＝1 バケットの精密読みでは min==max。
            return detail.Mins[index];
        }

        var state = g.Save();
        try
        {
            g.SetClip(wave, System.Drawing.Drawing2D.CombineMode.Intersect);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            using var linePen = new Pen(wavePen.Color, 1.2f)
            {
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };

            if (count == 1)
            {
                var frame = startFrame;
                var sample = SampleAt(0);
                var gain = RegionEdgeFade.GainAt(frame, displayFades);
                var x = AbsoluteToX(frame / (double)frameCount, wave);
                var y = SampleY(sample, gain);
                g.DrawLine(linePen, x, y - 2.5f, x, y + 2.5f);
                if (IsTimeZoomAtMax)
                {
                    DrawSamplePoints(g, wavePen.Color, [new PointF(x, y)]);
                }

                return;
            }

            // サンプルが画面幅以下: 全点を結ぶ（真のサンプル表示）。
            // それ以上: 1px あたり複数点が往復して線が汚くなるので、列ごとに代表点へ間引く。
            PointF[] points;
            if (count <= wave.Width)
            {
                points = new PointF[count];
                for (var i = 0; i < count; i++)
                {
                    var frame = startFrame + i;
                    var gain = RegionEdgeFade.GainAt(frame, displayFades);
                    var x = AbsoluteToX(frame / (double)frameCount, wave);
                    points[i] = new PointF(x, SampleY(SampleAt(i), gain));
                }
            }
            else
            {
                points = new PointF[wave.Width];
                for (var px = 0; px < wave.Width; px++)
                {
                    var i0 = (int)((long)px * count / wave.Width);
                    var i1 = (int)((long)(px + 1) * count / wave.Width);
                    if (i1 <= i0)
                    {
                        i1 = Math.Min(count, i0 + 1);
                    }

                    // 列内で |sample| 最大の点を代表にすると、ピークを落としにくく線も一本に保たれる。
                    var bestIndex = i0;
                    var bestAbs = Math.Abs(SampleAt(i0));
                    for (var i = i0 + 1; i < i1; i++)
                    {
                        var abs = Math.Abs(SampleAt(i));
                        if (abs > bestAbs)
                        {
                            bestAbs = abs;
                            bestIndex = i;
                        }
                    }

                    var frame = startFrame + bestIndex;
                    var gain = RegionEdgeFade.GainAt(frame, displayFades);
                    var x = wave.Left + px + 0.5f;
                    points[px] = new PointF(x, SampleY(SampleAt(bestIndex), gain));
                }
            }

            g.DrawLines(linePen, points);

            // 最大ズームかつ 1 サンプルが 1px 以上空くときだけ点を重ねる。
            // 密なままだと点が線に溶けて塗りつぶしになる。
            if (IsTimeZoomAtMax && count <= wave.Width)
            {
                DrawSamplePoints(g, wavePen.Color, points);
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static void DrawSamplePoints(Graphics g, Color color, PointF[] points)
    {
        const float radius = 4f;
        var diameter = radius * 2f;
        using var brush = new SolidBrush(color);
        for (var i = 0; i < points.Length; i++)
        {
            var p = points[i];
            g.FillEllipse(brush, p.X - radius, p.Y - radius, diameter, diameter);
        }
    }

    /// <summary>表示窓がサンプル折れ線向きか（縦棒 min/max だと点描に見える密度）。</summary>
    private static bool IsPolylineZoom(long rangeFrames, int width) =>
        rangeFrames > 0
        && width > 0
        && rangeFrames <= (long)width * PolylineMaxSamplesPerPixel;

    private bool CanDrawSamplePolyline(WavPeakData detail, long rangeFrames, int width) =>
        !_detailIsApproximate
        && IsPolylineZoom(rangeFrames, width)
        && detail.Mins.Length == rangeFrames;

    /// <summary>
    /// 除外で区切られた連続リージョン固まりごとに:
    /// <list type="bullet">
    /// <item>白: 固まりの頭／末尾のみ。縦線は端、半三角はフェード端（波形上端）</item>
    /// <item>ライム Entry: 頭。ただし先頭が -A ならその直後（開始形）。Marker 段の半三角。白より手前、通常マーカーより奥</item>
    /// <item>赤 Exit: 末尾。ただし末尾が -E ならその直前（終了形）。Marker 段の半三角。白より手前、通常マーカーより奥</item>
    /// </list>
    /// </summary>
    private void DrawContiguousRegionCueMarkers(
        Graphics g,
        Rectangle wave,
        Rectangle labels,
        float rowHeight)
    {
        _fadeHandleHitRegions.Clear();
        _fadeAreaHitRegions.Clear();
        if (_peaks is null || _peaks.FrameCount <= 0 || _regions.Count == 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        var markerRowTop = labels.Top + rowHeight * 3f;
        var white = WaveformGdiColors.ForControlBack(WaveformGdiColors.RegionBoundaryMarker);
        var entryColor = WaveformGdiColors.ForControlBack(WaveformGdiColors.EntryCueMarker);
        var exitColor = WaveformGdiColors.ForControlBack(WaveformGdiColors.ExitCueMarker);
        var fadeCurve = WaveformGdiColors.ForControlBack(WaveformGdiColors.RegionFadeCurve);
        var displayFades = GetDisplayRegionEdgeFades();

        using var whiteHandlePen = new Pen(white, 1f);
        using var whiteBrush = new SolidBrush(white);
        using var entryPen = new Pen(entryColor, 2f);
        using var entryBrush = new SolidBrush(entryColor);
        using var exitPen = new Pen(exitColor, 2f);
        using var exitBrush = new SolidBrush(exitColor);
        using var fadePen = new Pen(fadeCurve, 1.5f);

        foreach (var run in CollectNonExcludedRuns(_regions))
        {
            if (run.Count == 0)
            {
                continue;
            }

            var first = run[0];
            var last = run[^1];
            var inSample = first.StartSampleOffset;
            var outSample = last.EndSampleOffset;
            var fade = displayFades.FirstOrDefault(f =>
                f.InSample == inSample && f.OutSample == outSample);
            if (fade.OutSample <= fade.InSample)
            {
                fade = new RegionEdgeFade(
                    inSample,
                    outSample,
                    null,
                    null,
                    DefaultFadeInCurve,
                    DefaultFadeOutCurve);
            }

            var fadeInEnd = fade.EffectiveFadeInEnd;
            var fadeOutStart = fade.EffectiveFadeOutStart;

            if (fade.HasFadeIn)
            {
                DrawRegionFadeCurve(
                    g,
                    fadePen,
                    wave,
                    frameCount,
                    inSample,
                    fadeInEnd,
                    fade.FadeInCurve,
                    isFadeIn: true);
            }

            if (fade.HasFadeOut)
            {
                DrawRegionFadeCurve(
                    g,
                    fadePen,
                    wave,
                    frameCount,
                    fadeOutStart,
                    outSample,
                    fade.FadeOutCurve,
                    isFadeIn: false);
            }

            // 白: 三角はフェードハンドル位置、縦線は三角から真下へ 1px
            if (TryGetWaveX(inSample, frameCount, wave, out var xInEdge))
            {
                var handleSample = fadeInEnd;
                if (!TryGetWaveX(handleSample, frameCount, wave, out var xInHandle))
                {
                    xInHandle = xInEdge;
                }

                DrawRegionEdgeGlyph(
                    g,
                    whiteHandlePen,
                    whiteBrush,
                    wave,
                    xInHandle,
                    xInHandle,
                    isStart: true,
                    dropLineAtGlyph: true);
                _fadeHandleHitRegions.Add(CreateFadeHandleHit(xInHandle, wave.Top, isStart: true, inSample, outSample, isFadeIn: true));
                if (fade.HasFadeIn
                    && TryMapAbsoluteRange(
                        SampleToAbsolute(inSample, frameCount),
                        SampleToAbsolute(fadeInEnd, frameCount),
                        wave,
                        out var fadeInX0,
                        out var fadeInX1))
                {
                    _fadeAreaHitRegions.Add(new FadeAreaHitRegion(
                        inSample,
                        outSample,
                        IsFadeIn: true,
                        RectangleF.FromLTRB(
                            Math.Min(fadeInX0, fadeInX1),
                            wave.Top,
                            Math.Max(fadeInX0, fadeInX1),
                            wave.Bottom)));
                }
            }

            if (TryGetWaveX(outSample, frameCount, wave, out var xOutEdge))
            {
                var handleSample = fadeOutStart;
                if (!TryGetWaveX(handleSample, frameCount, wave, out var xOutHandle))
                {
                    xOutHandle = xOutEdge;
                }

                DrawRegionEdgeGlyph(
                    g,
                    whiteHandlePen,
                    whiteBrush,
                    wave,
                    xOutHandle,
                    xOutHandle,
                    isStart: false,
                    dropLineAtGlyph: true);
                _fadeHandleHitRegions.Add(CreateFadeHandleHit(xOutHandle, wave.Top, isStart: false, inSample, outSample, isFadeIn: false));
                if (fade.HasFadeOut
                    && TryMapAbsoluteRange(
                        SampleToAbsolute(fadeOutStart, frameCount),
                        SampleToAbsolute(outSample, frameCount),
                        wave,
                        out var fadeOutX0,
                        out var fadeOutX1))
                {
                    _fadeAreaHitRegions.Add(new FadeAreaHitRegion(
                        inSample,
                        outSample,
                        IsFadeIn: false,
                        RectangleF.FromLTRB(
                            Math.Min(fadeOutX0, fadeOutX1),
                            wave.Top,
                            Math.Max(fadeOutX0, fadeOutX1),
                            wave.Bottom)));
                }
            }

            // Entry: 頭。先頭 -A ならその後（Marker 段の半三角）
            var entrySample = IsAnacrusisSuffix(first) && run.Count > 1
                ? run[1].StartSampleOffset
                : first.StartSampleOffset;
            if (TryGetWaveX(entrySample, frameCount, wave, out var xEntry))
            {
                DrawRegionEdgeGlyph(
                    g,
                    entryPen,
                    entryBrush,
                    wave,
                    xEntry,
                    xEntry,
                    isStart: true,
                    glyphAnchorY: markerRowTop);
            }

            // Exit: 末尾。末尾が -E ならその前（Marker 段の半三角）
            var exitSample = IsExitSuffix(last) && run.Count > 1
                ? last.StartSampleOffset
                : last.EndSampleOffset;
            if (TryGetWaveX(exitSample, frameCount, wave, out var xExit))
            {
                DrawRegionEdgeGlyph(
                    g,
                    exitPen,
                    exitBrush,
                    wave,
                    xExit,
                    xExit,
                    isStart: false,
                    glyphAnchorY: markerRowTop);
            }
        }
    }

    private FadeHandleHitRegion CreateFadeHandleHit(
        float glyphX,
        float baseY,
        bool isStart,
        long inSample,
        long outSample,
        bool isFadeIn)
    {
        var halfW = RegionEdgeGlyphHalfW;
        var pad = 2f;
        var triH = halfW * MathF.Sqrt(3f) / 2f;
        var left = isStart ? glyphX - pad : glyphX - halfW - pad;
        var right = isStart ? glyphX + halfW + pad : glyphX + pad;
        return new FadeHandleHitRegion(
            inSample,
            outSample,
            isFadeIn,
            RectangleF.FromLTRB(left, baseY - pad, right, baseY + triH + pad));
    }

    private void DrawRegionFadeCurve(
        Graphics g,
        Pen pen,
        Rectangle wave,
        long frameCount,
        long startSample,
        long endSample,
        RegionFadeCurveKind curveKind,
        bool isFadeIn)
    {
        if (endSample <= startSample || frameCount <= 0)
        {
            return;
        }

        var topY = wave.Top;
        var bottomY = wave.Bottom - 1f;
        // 表示幅に応じてサンプルし、スプラインではなく折れ線で正確に描く
        var pixelSpan = Math.Max(
            2f,
            Math.Abs(
                AbsoluteToX(SampleToAbsolute(endSample, frameCount), wave)
                - AbsoluteToX(SampleToAbsolute(startSample, frameCount), wave)));
        var steps = Math.Clamp((int)Math.Ceiling(pixelSpan), 16, 256);
        var points = new List<PointF>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            var sample = startSample + (long)Math.Round((endSample - startSample) * t);
            var abs = SampleToAbsolute(sample, frameCount);
            if (abs < _viewStart - 1e-9 || abs > ViewEnd + 1e-9)
            {
                continue;
            }

            var x = AbsoluteToX(abs, wave);
            var gain = isFadeIn
                ? RegionEdgeFade.EvaluateRising(curveKind, t)
                : RegionEdgeFade.EvaluateFalling(curveKind, t);
            var y = bottomY - (bottomY - topY) * gain;
            points.Add(new PointF(x, y));
        }

        if (points.Count >= 2)
        {
            g.DrawLines(pen, points.ToArray());
        }
    }

    private bool TryGetWaveX(long sampleOffset, long frameCount, Rectangle wave, out float x)
    {
        var abs = SampleToAbsolute(sampleOffset, frameCount);
        if (abs < _viewStart - 1e-9 || abs > ViewEnd + 1e-9)
        {
            x = 0f;
            return false;
        }

        x = AbsoluteToX(abs, wave);
        return true;
    }

    /// <summary>
    /// DAW 風エッジ: 縦線＋半欠け三角（開始=右半分、終了=左半分）。
    /// 既定は <paramref name="lineX"/> に太線。白ハンドルは <paramref name="dropLineAtGlyph"/> で
    /// 三角位置から真下へ 1px 線を引く。
    /// Entry/Exit Cue は <paramref name="glyphAnchorY"/> で Marker 段へ置く。
    /// </summary>
    private void DrawRegionEdgeGlyph(
        Graphics g,
        Pen pen,
        Brush brush,
        Rectangle wave,
        float lineX,
        float glyphX,
        bool isStart,
        float? glyphAnchorY = null,
        bool dropLineAtGlyph = false)
    {
        var halfW = RegionEdgeGlyphHalfW;

        var baseY = glyphAnchorY ?? wave.Top;
        if (dropLineAtGlyph)
        {
            g.DrawLine(pen, glyphX, baseY, glyphX, wave.Bottom);
        }
        else
        {
            g.DrawLine(pen, lineX, baseY, lineX, wave.Bottom);
        }

        // 半欠けの ▼ は「正三角形の半分」(高さ=半幅×√3) だと縦長に見えるため、
        // 見かけのバランスを優先して高さ = 半幅 × √3/2 にする。
        var triH = halfW * MathF.Sqrt(3f) / 2f;
        var tipY = baseY + triH;
        PointF[] triangle = isStart
            ?
            [
                new(glyphX, baseY),
                new(glyphX + halfW, baseY),
                new(glyphX, tipY),
            ]
            :
            [
                new(glyphX - halfW, baseY),
                new(glyphX, baseY),
                new(glyphX, tipY),
            ];
        g.FillPolygon(brush, triangle);
    }

    private static List<List<WaveformRegionMark>> CollectNonExcludedRuns(
        IReadOnlyList<WaveformRegionMark> regions)
    {
        var runs = new List<List<WaveformRegionMark>>();
        List<WaveformRegionMark>? current = null;
        foreach (var region in regions)
        {
            if (region.IsExcluded)
            {
                current = null;
                continue;
            }

            if (current is null)
            {
                current = [];
                runs.Add(current);
            }

            current.Add(region);
        }

        return runs;
    }

    private static bool IsAnacrusisSuffix(WaveformRegionMark region) =>
        region.NameSuffix.Equals(WaveformRegionBuilder.AnacrusisSuffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsExitSuffix(WaveformRegionMark region) =>
        region.NameSuffix.Equals(WaveformRegionBuilder.LoopEndSuffix, StringComparison.OrdinalIgnoreCase);

    private static void DrawPeakColumn(
        Graphics g,
        Pen wavePen,
        Rectangle wave,
        float midY,
        float amplitude,
        float x,
        float min,
        float max)
    {
        var y1 = Math.Clamp(midY - max * amplitude, wave.Top, wave.Bottom);
        var y2 = Math.Clamp(midY - min * amplitude, wave.Top, wave.Bottom);
        if (Math.Abs(y2 - y1) < 1f)
        {
            y2 = Math.Min(wave.Bottom, y1 + 1f);
        }

        g.DrawLine(wavePen, x, y1, x, y2);
    }

    private void DrawRegionBackgrounds(Graphics g, Rectangle wave)
    {
        if (_peaks is null || _peaks.FrameCount <= 0 || _regions.Count == 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        using var gray = new SolidBrush(WaveformGdiColors.RegionWaveFillGray);
        using var loop = new SolidBrush(WaveformGdiColors.RegionWaveFillLoop);
        using var anacrusis = new SolidBrush(WaveformGdiColors.RegionWaveFillAnacrusis);
        using var exit = new SolidBrush(WaveformGdiColors.RegionWaveFillExit);

        foreach (var region in _regions)
        {
            if (region.IsExcluded)
            {
                continue;
            }

            var a0 = SampleToAbsolute(region.StartSampleOffset, frameCount);
            var a1 = SampleToAbsolute(region.EndSampleOffset, frameCount);
            if (!TryMapAbsoluteRange(a0, a1, wave, out var x0, out var x1))
            {
                continue;
            }

            var width = Math.Max(1f, x1 - x0);
            Brush fill;
            if (TryGetSuffixRegionBrush(region.NameSuffix, loop, anacrusis, exit, out var suffixFill))
            {
                fill = suffixFill;
            }
            else
            {
                fill = gray;
            }

            g.FillRectangle(fill, x0, wave.Top, width, wave.Height);
        }
    }

    /// <summary>
    /// -R 範囲を波形の上に重ねる。
    /// </summary>
    private void DrawExcludedRegionOverlays(Graphics g, Rectangle wave)
    {
        DrawExcludedRegionOverlays(g, wave, wave);
    }

    /// <summary>
    /// -R 範囲を Music Segment / Playlist レーンの上にも被せる。
    /// </summary>
    private void DrawExcludedRegionOverlaysOnNameLanes(
        Graphics g,
        Rectangle wave,
        Rectangle segmentLane,
        Rectangle playlistLane)
    {
        if (segmentLane.Height <= 0 && playlistLane.Height <= 0)
        {
            return;
        }

        var top = segmentLane.Height > 0 ? segmentLane.Top : playlistLane.Top;
        var bottom = playlistLane.Height > 0 ? playlistLane.Bottom : segmentLane.Bottom;
        if (segmentLane.Height > 0 && playlistLane.Height > 0)
        {
            top = Math.Min(segmentLane.Top, playlistLane.Top);
            bottom = Math.Max(segmentLane.Bottom, playlistLane.Bottom);
        }

        var band = new Rectangle(wave.Left, top, wave.Width, Math.Max(0, bottom - top));
        if (band.Height <= 0)
        {
            return;
        }

        DrawExcludedRegionOverlays(g, wave, band);
    }

    /// <summary>
    /// -R 範囲を <paramref name="fillBounds"/> の縦範囲に重ねる。横位置は <paramref name="xRef"/>（波形）基準。
    /// </summary>
    private void DrawExcludedRegionOverlays(Graphics g, Rectangle xRef, Rectangle fillBounds)
    {
        if (_peaks is null
            || _peaks.FrameCount <= 0
            || _regions.Count == 0
            || xRef.Width <= 0
            || fillBounds.Height <= 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        using var excluded = new SolidBrush(WaveformGdiColors.RegionWaveFillExcluded);

        foreach (var region in _regions)
        {
            if (!region.IsExcluded)
            {
                continue;
            }

            var a0 = SampleToAbsolute(region.StartSampleOffset, frameCount);
            var a1 = SampleToAbsolute(region.EndSampleOffset, frameCount);
            if (!TryMapAbsoluteRange(a0, a1, xRef, out var x0, out var x1))
            {
                continue;
            }

            var width = Math.Max(1f, x1 - x0);
            g.FillRectangle(excluded, x0, fillBounds.Top, width, fillBounds.Height);
        }
    }

    /// <summary>-L=シアン、-A=ライム、-E=赤。該当しなければ false。</summary>
    private static bool TryGetSuffixRegionBrush(
        string nameSuffix,
        Brush loop,
        Brush anacrusis,
        Brush exit,
        out Brush fill)
    {
        if (nameSuffix.Equals(WaveformRegionBuilder.LoopLeftSuffix, StringComparison.OrdinalIgnoreCase))
        {
            fill = loop;
            return true;
        }

        if (nameSuffix.Equals(WaveformRegionBuilder.AnacrusisSuffix, StringComparison.OrdinalIgnoreCase))
        {
            fill = anacrusis;
            return true;
        }

        if (nameSuffix.Equals(WaveformRegionBuilder.LoopEndSuffix, StringComparison.OrdinalIgnoreCase))
        {
            fill = exit;
            return true;
        }

        fill = null!;
        return false;
    }

    private void DrawPlaylistNameLabels(Graphics g, Rectangle wave, Rectangle playlistLane)
    {
        if (_outputParts.Count == 0 || playlistLane.Height <= 0)
        {
            return;
        }

        // Playlist 名に " (.wav)" を添えて表示。無効パートはレーンに名前を出さない。
        var items = new List<(string Text, long Start, long End)>(_outputParts.Count);
        foreach (var part in _outputParts)
        {
            if (_disabledPlaylistPartNumbers.Contains(part.Number))
            {
                continue;
            }

            var name = _playlistDisplayNames.TryGetValue(part.Number, out var displayName)
                ? displayName
                : Path.GetFileNameWithoutExtension(part.FileName);
            if (string.IsNullOrEmpty(name))
            {
                name = part.FileName;
            }

            items.Add(($"{name} (.wav)", part.StartSampleOffset, part.EndSampleOffset));
        }

        DrawTimedNameLane(g, wave, playlistLane, items, FontStyle.Regular, WaveformGdiColors.MusicPlaylistLaneBg);
    }

    /// <summary>
    /// 各 Playlist 区間の波形左下へ "48kHz 24bit 2ch" を最前面描画する。
    /// 規定フォーマットと異なる場合は警告色。影ではなく黒縁。
    /// 幅に収まらないときは横圧縮せずフォントサイズを下げる。
    /// </summary>
    private void DrawPlaylistFormatLabelsTopmost(Graphics g)
    {
        if (_peaks is null
            || _peaks.IsEmpty
            || _peaks.FrameCount <= 0
            || _outputParts.Count == 0)
        {
            return;
        }

        var content = ContentBounds;
        var (_, _, wave, _, _, _) = GetLayout(content, g);
        if (wave.Width <= 0 || wave.Height <= 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        // 波形ビュー共通の Yu Gothic UI（ボールドなし）。
        const FontStyle fontStyle = FontStyle.Regular;
        var idealFontSize = Font.Size;
        const float minFontSize = 0.5f;
        const float leftPad = 4f;
        const float bottomPad = 3f;

        using var normalBrush = new SolidBrush(WaveformGdiColors.OutputPartFg);
        using var warningBrush = new SolidBrush(WaveformGdiColors.LogWarning);
        using var outlineBrush = new SolidBrush(Color.Black);

        foreach (var part in _outputParts)
        {
            if (_disabledPlaylistPartNumbers.Contains(part.Number))
            {
                continue;
            }

            if (ResolvePartWavInfo(part) is not { } wavInfo)
            {
                continue;
            }

            var a0 = SampleToAbsolute(part.StartSampleOffset, frameCount);
            var a1 = SampleToAbsolute(part.EndSampleOffset, frameCount);
            if (!TryMapAbsoluteRange(a0, a1, wave, out var x0, out var x1))
            {
                continue;
            }

            var slotWidth = Math.Max(1f, x1 - x0);
            if (slotWidth < 8f)
            {
                continue;
            }

            var text = ExpectedWaveformFormat.FormatCompact(wavInfo);
            var available = Math.Max(1f, slotWidth - leftPad);
            var fontSize = idealFontSize;
            using (var probe = new Font(Font.FontFamily, idealFontSize, fontStyle))
            {
                var idealWidth = g.MeasureString(text, probe).Width;
                if (idealWidth > available && idealWidth > 0.01f)
                {
                    fontSize = Math.Max(minFontSize, idealFontSize * available / idealWidth);
                }
            }

            // 測定誤差でまだはみ出す場合はさらに縮小
            for (var attempt = 0; attempt < 4; attempt++)
            {
                using var measureFont = new Font(Font.FontFamily, fontSize, fontStyle);
                var measured = g.MeasureString(text, measureFont).Width;
                if (measured <= available || measured <= 0.01f)
                {
                    break;
                }

                fontSize = Math.Max(minFontSize, fontSize * available / measured);
            }

            using var labelFont = new Font(Font.FontFamily, fontSize, fontStyle);
            var labelHeight = labelFont.GetHeight(g);
            var y = wave.Bottom - labelHeight - bottomPad;
            if (y < wave.Top)
            {
                y = wave.Top;
            }

            var x = x0 + leftPad;
            var fill = _expectedWaveformFormat.Matches(wavInfo) ? normalBrush : warningBrush;
            DrawOutlinedString(g, text, labelFont, fill, outlineBrush, x, y);
        }
    }

    /// <summary>黒縁＋本体色で文字列を描く（影は使わない）。</summary>
    private static void DrawOutlinedString(
        Graphics g,
        string text,
        Font font,
        Brush fill,
        Brush outline,
        float x,
        float y)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                g.DrawString(text, font, outline, x + dx, y + dy);
            }
        }

        g.DrawString(text, font, fill, x, y);
    }

    private WavFileInfo? ResolvePartWavInfo(WaveformOutputPart part)
    {
        if (!string.IsNullOrEmpty(part.SourcePath) && _sourceSpans.Count > 0)
        {
            foreach (var span in _sourceSpans)
            {
                if (string.Equals(span.Path, part.SourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    return span.WavInfo;
                }
            }
        }

        return _wavInfo;
    }

    private void DrawSegmentNameLabels(Graphics g, Rectangle wave, Rectangle segmentLane)
    {
        if (_segmentNames.Count == 0 || segmentLane.Height <= 0)
        {
            return;
        }

        // リージョン束ね単位 = Music Segment（_a / _b …）。Playlist より細かい。
        var items = new List<(string Text, long Start, long End)>(_segmentNames.Count);
        foreach (var segment in _segmentNames)
        {
            items.Add((segment.Name, segment.StartSampleOffset, segment.EndSampleOffset));
        }

        DrawTimedNameLane(g, wave, segmentLane, items, FontStyle.Regular, WaveformGdiColors.MusicSegmentLaneBg);
        DrawSegmentLaneDividers(g, wave, segmentLane);
    }

    /// <summary>
    /// 隣り合う Music Segment の境に、波形背景色の縦線をレーン内だけ描く。
    /// </summary>
    private void DrawSegmentLaneDividers(Graphics g, Rectangle wave, Rectangle segmentLane)
    {
        if (_peaks is null || _peaks.FrameCount <= 0 || _segmentNames.Count < 2 || segmentLane.Height <= 0)
        {
            return;
        }

        var ordered = _segmentNames
            .OrderBy(s => s.StartSampleOffset)
            .ToList();
        var frameCount = _peaks.FrameCount;
        using var pen = new Pen(WaveformGdiColors.WaveformBack, 3f);

        for (var i = 1; i < ordered.Count; i++)
        {
            // 隙間（-R など）がある場合は隣り合っていないので線を引かない
            if (ordered[i - 1].EndSampleOffset != ordered[i].StartSampleOffset)
            {
                continue;
            }

            if (!TryGetWaveX(ordered[i].StartSampleOffset, frameCount, wave, out var x))
            {
                continue;
            }

            g.DrawLine(pen, x, segmentLane.Top, x, segmentLane.Bottom);
        }
    }

    /// <summary>
    /// 波形時間範囲に紐づく名前を下部レーンへ描画。
    /// 隣ラベルと重ならないよう幅に応じて縮小する。クリップはせず範囲外へのはみ出しは許容。
    /// </summary>
    private void DrawTimedNameLane(
        Graphics g,
        Rectangle wave,
        Rectangle lane,
        IReadOnlyList<(string Text, long Start, long End)> items,
        FontStyle fontStyle,
        Color laneBackColor)
    {
        if (_peaks is null
            || _peaks.FrameCount <= 0
            || items.Count == 0
            || wave.Width <= 0
            || lane.Height <= 0)
        {
            return;
        }

        using (var laneBg = new SolidBrush(laneBackColor))
        {
            g.FillRectangle(laneBg, lane);
        }

        var withBack = new List<(string Text, long Start, long End, Color Back)>(items.Count);
        foreach (var (text, start, end) in items)
        {
            withBack.Add((text, start, end, laneBackColor));
        }

        DrawTimedNameLaneWithBackColors(g, wave, lane, withBack, fontStyle);
    }

    /// <summary>
    /// 各区間の背景色に合わせたコントラスト文字で名前レーンを描く。
    /// </summary>
    private void DrawTimedNameLaneWithBackColors(
        Graphics g,
        Rectangle wave,
        Rectangle lane,
        IReadOnlyList<(string Text, long Start, long End, Color Back)> items,
        FontStyle fontStyle)
    {
        if (_peaks is null
            || _peaks.FrameCount <= 0
            || items.Count == 0
            || wave.Width <= 0
            || lane.Height <= 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        // レーン高さに収まる最大サイズ（上下レーン同士の縦重なり防止）
        var fontMaxPx = FitFontSizeToLaneHeight(g, Font.FontFamily, fontStyle, lane.Height);
        var idealFontSize = Math.Clamp(
            wave.Height * NameLaneFontScale,
            NameLaneFontMinPx,
            fontMaxPx);
        // セグメント幅に収まるまで縮小（見えなくても拡大で読める）
        const float minFontSize = 0.5f;

        var parts = new List<(string Text, float X0, float X1, Color Back)>(items.Count);
        foreach (var (text, start, end, back) in items)
        {
            var a0 = SampleToAbsolute(start, frameCount);
            var a1 = SampleToAbsolute(end, frameCount);
            if (!TryMapAbsoluteRange(a0, a1, wave, out var x0, out var x1))
            {
                continue;
            }

            if (x1 - x0 < 1f)
            {
                continue;
            }

            parts.Add((text, x0, x1, back));
        }

        if (parts.Count == 0)
        {
            return;
        }

        for (var i = 0; i < parts.Count; i++)
        {
            // 白い境界線の内側＝セグメント幅に収める
            var slotWidth = Math.Max(1f, parts[i].X1 - parts[i].X0);

            var fontSize = idealFontSize;
            using (var probe = new Font(Font.FontFamily, idealFontSize, fontStyle, GraphicsUnit.Pixel))
            {
                var idealWidth = g.MeasureString(parts[i].Text, probe).Width;
                if (idealWidth > slotWidth && idealWidth > 0.01f)
                {
                    fontSize = Math.Max(minFontSize, idealFontSize * slotWidth / idealWidth);
                }
            }

            // 測定誤差でまだはみ出す場合はさらに縮小
            for (var attempt = 0; attempt < 4; attempt++)
            {
                using var measureFont = new Font(Font.FontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
                var measured = g.MeasureString(parts[i].Text, measureFont).Width;
                if (measured <= slotWidth || measured <= 0.01f)
                {
                    break;
                }

                fontSize = Math.Max(minFontSize, fontSize * slotWidth / measured);
            }

            using var labelFont = new Font(Font.FontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
            var textWidth = Math.Max(0.01f, g.MeasureString(parts[i].Text, labelFont).Width);
            // 下限フォントでも幅が足りないときは横だけ潰して収める
            var scaleX = textWidth > slotWidth ? slotWidth / textWidth : 1f;
            var drawWidth = textWidth * scaleX;
            var x = parts[i].X0 + (slotWidth - drawWidth) * 0.5f;
            var labelHeight = labelFont.GetHeight(g);
            var y = lane.Top + (lane.Height - labelHeight) * 0.5f;
            using var brush = new SolidBrush(PickContrastingForeColor(parts[i].Back));

            if (scaleX < 0.999f)
            {
                var state = g.Save();
                g.TranslateTransform(x, y);
                g.ScaleTransform(scaleX, 1f);
                g.DrawString(parts[i].Text, labelFont, brush, 0f, 0f);
                g.Restore(state);
            }
            else
            {
                g.DrawString(parts[i].Text, labelFont, brush, x, y);
            }
        }
    }

    /// <summary>GetHeight がレーン高さに収まる最大ピクセルサイズを求める。</summary>
    private static float FitFontSizeToLaneHeight(
        Graphics g,
        FontFamily family,
        FontStyle style,
        int laneHeight)
    {
        var maxTry = Math.Max(NameLaneFontMinPx, laneHeight - 1f);
        for (var size = maxTry; size >= NameLaneFontMinPx; size -= 0.5f)
        {
            using var probe = new Font(family, size, style, GraphicsUnit.Pixel);
            if (probe.GetHeight(g) <= laneHeight - 1f)
            {
                return size;
            }
        }

        return NameLaneFontMinPx;
    }

    /// <summary>
    /// 書き出し中パートの枠をパルス発光させる（進行中の見た目用）。
    /// </summary>
    private void DrawExportPartGlow(Graphics g, Rectangle timelineBounds)
    {
        if (_exportHighlightPartNumber is not int partNumber
            || _peaks is null
            || _peaks.FrameCount <= 0
            || timelineBounds.Width <= 0)
        {
            return;
        }

        WaveformOutputPart? target = null;
        foreach (var part in _outputParts)
        {
            if (part.Number == partNumber)
            {
                target = part;
                break;
            }
        }

        if (target is not WaveformOutputPart highlight)
        {
            return;
        }

        var layoutContent = ContentBounds;
        var (_, _, wave, _, _, _) = GetLayout(layoutContent, g);
        if (wave.Width <= 0 || wave.Height <= 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        var a0 = SampleToAbsolute(highlight.StartSampleOffset, frameCount);
        var a1 = SampleToAbsolute(highlight.EndSampleOffset, frameCount);
        if (!TryMapAbsoluteRange(a0, a1, wave, out var x0, out var x1))
        {
            return;
        }

        var width = Math.Max(2f, x1 - x0);
        var rect = new RectangleF(x0, wave.Top, width, wave.Height);

        // 約 1.1 秒周期で明滅（巨大ファイル書き出し中も動き続ける）
        var phase = (Environment.TickCount64 % 1100) / 1100f;
        var pulse = 0.40f + 0.60f * (0.5f + 0.5f * MathF.Sin(phase * MathF.PI * 2f));
        var baseColor = WaveformGdiColors.ExportPartGlow;

        // 内側を半透明で塗り、「今この固まり」をはっきり見せる
        using (var fill = new SolidBrush(Color.FromArgb((int)(72 * pulse), baseColor)))
        {
            g.FillRectangle(fill, rect);
        }

        // 細い外光（太くしすぎない）
        using (var softPen = new Pen(Color.FromArgb((int)(55 * pulse), baseColor), 2f))
        {
            g.DrawRectangle(softPen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        // コアの細線
        using var corePen = new Pen(Color.FromArgb((int)(220 * pulse), baseColor), 1f);
        g.DrawRectangle(corePen, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>Playlist 一覧でポイント中の出力パートを、波形内の 1px 枠で示す。</summary>
    private void DrawPlaylistHoverOutline(Graphics g)
    {
        if (_playlistHoverHighlightPartNumber is not int partNumber
            || _peaks is null
            || _peaks.FrameCount <= 0)
        {
            return;
        }

        var target = _outputParts.FirstOrDefault(part => part.Number == partNumber);
        if (target.Number != partNumber)
        {
            return;
        }

        var layoutContent = ContentBounds;
        var (_, _, wave, _, _, _) = GetLayout(layoutContent, g);
        if (wave.Width <= 0 || wave.Height <= 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        var start = SampleToAbsolute(target.StartSampleOffset, frameCount);
        var end = SampleToAbsolute(target.EndSampleOffset, frameCount);
        if (!TryMapAbsoluteRange(start, end, wave, out var x0, out var x1))
        {
            return;
        }

        var width = Math.Max(1f, x1 - x0);
        var rect = new RectangleF(
            x0,
            wave.Top,
            width,
            Math.Max(1f, wave.Height - 1f));
        using (var fill = new SolidBrush(Color.FromArgb(26, WaveformGdiColors.PlaylistHoverBorder)))
        {
            g.FillRectangle(fill, rect);
        }

        using var pen = new Pen(WaveformGdiColors.PlaylistHoverBorder, 1f);
        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
    }

    private void DrawBars(Graphics g, Rectangle labels, Rectangle wave, float rowHeight)
    {
        if (_peaks is null || _peaks.FrameCount <= 0 || _bars.Count == 0 || labels.Width <= 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        var barRowTop = labels.Top;
        var tempoRowTop = barRowTop + rowHeight;
        var signatureRowTop = tempoRowTop + rowHeight;
        var lineTop = labels.Top;
        var lineBottom = wave.Height > 0 ? wave.Bottom : labels.Bottom;

        DrawBeatLines(g, labels, wave, rowHeight, frameCount);

        // 表示窓内の隣接小節頭の平均ピクセル間隔から、間引き段階を決める。
        // 番号幅は常に 3 桁想定（"000"）で、拡大時に桁が増えても重ならないようにする。
        var averageGapPx = EstimateVisibleBarGapPx(labels, frameCount);
        var threeDigitWidth = g.MeasureString("000", Font).Width;
        var minBarNumberGap = threeDigitWidth + 6f; // 描画オフセット分の余白
        var barStep = ChooseBarThinningStep(averageGapPx, minBarNumberGap);

        using var barPen = new Pen(WaveformGdiColors.BarLine, 1f);
        using var tempoChangePen = new Pen(WaveformGdiColors.TempoChangeLine, 1f)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
            DashPattern = [3f, 3f],
        };
        // 小節番号／テンポ／拍子ラベルは同色（WaveformInfoFg）で 1 本のブラシを共有する。
        using var infoLabelBrush = new SolidBrush(WaveformGdiColors.WaveformInfoFg);

        var barLabelY = barRowTop + 1f;
        var tempoLabelY = tempoRowTop + 1f;
        var signatureLabelY = signatureRowTop + 1f;
        var lastTempoLabelX = float.NegativeInfinity;
        int? prevBarTempo = null;
        int? prevBarNumerator = null;
        int? prevBarDenominator = null;
        int? lastShownTempo = null;

        foreach (var bar in _bars)
        {
            var tempoRounded = (int)Math.Round(bar.Bpm, MidpointRounding.AwayFromZero);
            var tempoLabel = tempoRounded.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var abs = SampleToAbsolute(bar.SampleOffset, frameCount);
            var inView = abs >= _viewStart - 1e-9 && abs <= ViewEnd + 1e-9;

            if (bar.IsTempoChangeOnly)
            {
                if (inView)
                {
                    var tempoX = AbsoluteToX(abs, labels);
                    g.DrawLine(tempoChangePen, tempoX, tempoRowTop, tempoX, tempoRowTop + rowHeight);
                    TryDrawTempoLabel(g, tempoLabel, tempoRounded, tempoX, tempoLabelY, infoLabelBrush,
                        ref lastTempoLabelX, ref lastShownTempo, minLabelGap: 0f, force: true);
                }

                continue;
            }

            var tempoChanged = prevBarTempo is int pt && pt != tempoRounded;
            var signatureChanged = prevBarNumerator is int pn
                && prevBarDenominator is int pd
                && (pn != bar.Numerator || pd != bar.Denominator);
            var isStructural = tempoChanged || signatureChanged || prevBarTempo is null;

            if (!inView)
            {
                prevBarTempo = tempoRounded;
                prevBarNumerator = bar.Numerator;
                prevBarDenominator = bar.Denominator;
                continue;
            }

            var x = AbsoluteToX(abs, labels);
            var onGrid = IsBarOnThinningGrid(bar.BarNumber, barStep);
            var drawLine = isStructural || onGrid;
            if (drawLine)
            {
                g.DrawLine(barPen, x, lineTop, x, lineBottom);
            }

            var drawNumber = isStructural || onGrid;
            if (drawNumber)
            {
                g.DrawString(bar.BarNumber.ToString(), Font, infoLabelBrush, x + 3f, barLabelY);
            }

            // 拍子／テンポ変化（および先頭）では必ずテンポ・拍子ラベルも出す
            TryDrawTempoLabel(
                g,
                tempoLabel,
                tempoRounded,
                x,
                tempoLabelY,
                infoLabelBrush,
                ref lastTempoLabelX,
                ref lastShownTempo,
                minLabelGap: minBarNumberGap,
                force: isStructural);

            if (isStructural)
            {
                var signatureLabel = $"{bar.Numerator}/{bar.Denominator}";
                g.DrawString(signatureLabel, Font, infoLabelBrush, x + 3f, signatureLabelY);
            }

            prevBarTempo = tempoRounded;
            prevBarNumerator = bar.Numerator;
            prevBarDenominator = bar.Denominator;
        }
    }

    private void DrawBeatLines(
        Graphics g,
        Rectangle labels,
        Rectangle wave,
        float rowHeight,
        long frameCount)
    {
        var barStarts = _bars
            .Where(bar => !bar.IsTempoChangeOnly)
            .OrderBy(bar => bar.SampleOffset)
            .ToArray();
        if (barStarts.Length < 2)
        {
            return;
        }

        if (CalculateVisibleBarCount(barStarts, frameCount) >= 8d - 1e-9)
        {
            return;
        }

        var lineTop = labels.Top + rowHeight;
        var lineBottom = wave.Height > 0 ? wave.Bottom : labels.Bottom;
        if (lineBottom <= lineTop)
        {
            return;
        }

        using var beatPen = new Pen(WaveformGdiColors.BeatLine, 1f);
        for (var i = 0; i + 1 < barStarts.Length; i++)
        {
            var bar = barStarts[i];
            var next = barStarts[i + 1];
            if (bar.Numerator <= 1 || next.SampleOffset <= bar.SampleOffset)
            {
                continue;
            }

            for (var beat = 1; beat < bar.Numerator; beat++)
            {
                var sample = bar.SampleOffset
                    + (next.SampleOffset - bar.SampleOffset) * beat / (double)bar.Numerator;
                var absolute = sample / frameCount;
                if (absolute < _viewStart - 1e-9 || absolute > ViewEnd + 1e-9)
                {
                    continue;
                }

                var x = AbsoluteToX(absolute, labels);
                g.DrawLine(beatPen, x, lineTop, x, lineBottom);
            }
        }
    }

    private double CalculateVisibleBarCount(
        IReadOnlyList<WaveformBarMark> barStarts,
        long frameCount)
    {
        var visibleBarCount = 0d;
        for (var i = 0; i + 1 < barStarts.Count; i++)
        {
            var start = SampleToAbsolute(barStarts[i].SampleOffset, frameCount);
            var end = SampleToAbsolute(barStarts[i + 1].SampleOffset, frameCount);
            if (end <= start)
            {
                continue;
            }

            var overlap = Math.Min(end, ViewEnd) - Math.Max(start, _viewStart);
            if (overlap > 0d)
            {
                visibleBarCount += overlap / (end - start);
            }
        }

        return visibleBarCount;
    }

    /// <summary>
    /// 十分な間隔がある限り密に。足りなければ 2→4→8→16→32→64 のグリッドへ間引く。
    /// グリッドは「1 と N の倍数」（例: N=8 → 1,8,16,24…／N=2 → 1,2,4,6…）。
    /// </summary>
    private static int ChooseBarThinningStep(double averageGapPx, float minGapPx)
    {
        ReadOnlySpan<int> steps = [1, 2, 4, 8, 16, 32, 64];
        foreach (var step in steps)
        {
            if (averageGapPx * step >= minGapPx)
            {
                return step;
            }
        }

        return 64;
    }

    /// <summary>
    /// step=1 は全小節。それ以外は 1 と step の倍数（2 → 1,2,4,6…／4 → 1,4,8,12…）。
    /// </summary>
    private static bool IsBarOnThinningGrid(int barNumber, int step)
    {
        if (step <= 1)
        {
            return true;
        }

        return barNumber == 1 || barNumber % step == 0;
    }

    /// <summary>表示窓内の隣接する小節頭の平均 X 間隔（無い／1 本だけなら十分広い値）。</summary>
    private double EstimateVisibleBarGapPx(Rectangle labels, long frameCount)
    {
        float? prevX = null;
        double sum = 0;
        var count = 0;
        foreach (var bar in _bars)
        {
            if (bar.IsTempoChangeOnly)
            {
                continue;
            }

            var abs = SampleToAbsolute(bar.SampleOffset, frameCount);
            if (abs < _viewStart - 1e-9 || abs > ViewEnd + 1e-9)
            {
                continue;
            }

            var x = AbsoluteToX(abs, labels);
            if (prevX is float px)
            {
                var gap = x - px;
                if (gap > 0.5f)
                {
                    sum += gap;
                    count++;
                }
            }

            prevX = x;
        }

        return count > 0 ? sum / count : labels.Width;
    }

    private void TryDrawTempoLabel(
        Graphics g,
        string tempoLabel,
        int tempoRounded,
        float x,
        float y,
        Brush brush,
        ref float lastTempoLabelX,
        ref int? lastShownTempo,
        float minLabelGap,
        bool force = false)
    {
        if (!force && lastShownTempo == tempoRounded)
        {
            return;
        }

        if (!force && x - lastTempoLabelX < minLabelGap)
        {
            return;
        }

        g.DrawString(tempoLabel, Font, brush, x + 3f, y);
        lastTempoLabelX = x;
        lastShownTempo = tempoRounded;
    }

    private void DrawPlayhead(
        Graphics g,
        Rectangle content,
        double? playheadProgress,
        List<(double Progress, long TickMs)> trailSamples,
        Color color)
    {
        if (playheadProgress is null || content.Width <= 0)
        {
            return;
        }

        var abs = playheadProgress.Value;
        if (abs < _viewStart - 1e-9 || abs > ViewEnd + 1e-9)
        {
            return;
        }

        var x = AbsoluteToX(abs, content);
        DrawSeekPlaybackTrail(g, content, x, trailSamples, color);

        // ソフトグロー（細め）
        using (var glowOuter = new Pen(Color.FromArgb(40, color), 3f))
        {
            g.DrawLine(glowOuter, x, content.Top, x, content.Bottom);
        }

        using (var glowInner = new Pen(Color.FromArgb(90, color), 1.5f))
        {
            g.DrawLine(glowInner, x, content.Top, x, content.Bottom);
        }

        // コア線
        using var corePen = new Pen(color, 1f);
        g.DrawLine(corePen, x, content.Top, x, content.Bottom);
    }

    /// <summary>
    /// シーク軌跡。ピクセル距離ベースの線形グラデで描き、ズームで縦縞が出ないようにする。
    /// 長さはおおよそ <see cref="TrailTargetLengthPx"/>（サンプルで到達範囲を決める）。
    /// </summary>
    private void DrawSeekPlaybackTrail(
        Graphics g,
        Rectangle content,
        float playheadX,
        List<(double Progress, long TickMs)> trailSamples,
        Color color)
    {
        var now = Environment.TickCount64;
        PruneTrailSamplesByAge(now, trailSamples);
        if (trailSamples.Count < 2 || content.Width <= 0)
        {
            return;
        }

        var trailRightX = playheadX - TrailPlayheadGapPx;
        var trailLeftLimit = playheadX - TrailTargetLengthPx;
        if (trailRightX <= content.Left || trailRightX <= trailLeftLimit)
        {
            return;
        }

        var fadeMs = TrailFadeMsForView(content.Width);
        float? coveredLeft = null;
        foreach (var sample in trailSamples)
        {
            if (now - sample.TickMs >= fadeMs)
            {
                continue;
            }

            var x = AbsoluteToX(sample.Progress, content);
            if (x > trailRightX)
            {
                continue;
            }

            coveredLeft = coveredLeft is float left ? Math.Min(left, x) : x;
        }

        if (coveredLeft is null)
        {
            return;
        }

        var drawLeft = Math.Max(content.Left, Math.Max(trailLeftLimit, coveredLeft.Value));
        var drawRight = Math.Min(content.Right, trailRightX);
        var drawW = drawRight - drawLeft;
        if (drawW < 1f)
        {
            return;
        }

        // 再生ヘッド基準の距離グラデ（長さは TrailTargetLengthPx のまま）。
        // 左端付近だけ長く透明寄りにして切れ目をぼかし、本体の立ち上がりは従来に近い位置にする。
        var gradLeft = playheadX - TrailTargetLengthPx;
        var gradRight = playheadX;
        if (gradRight - gradLeft < 1f)
        {
            return;
        }

        var peak = Color.FromArgb(ToByteAlpha(TrailPeakAlpha), color);
        var mid = Color.FromArgb(ToByteAlpha(TrailPeakAlpha * 0.25f), color);
        var soft = Color.FromArgb(ToByteAlpha(TrailPeakAlpha * 0.06f), color);
        var clear = Color.FromArgb(0, color);
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new PointF(gradLeft, content.Top),
            new PointF(gradRight, content.Top),
            clear,
            peak)
        {
            InterpolationColors = new System.Drawing.Drawing2D.ColorBlend
            {
                Positions = [0f, 0.14f, 0.42f, 0.72f, 1f],
                Colors = [clear, clear, soft, mid, peak],
            },
        };

        var oldMode = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.FillRectangle(brush, drawLeft, content.Top, drawW, content.Height);
        g.SmoothingMode = oldMode;
    }

    private void RecordTrailSample(
        double progress,
        List<(double Progress, long TickMs)> trailSamples,
        ref bool trailActive)
    {
        if (!trailActive)
        {
            return;
        }

        var now = Environment.TickCount64;
        var durationSec = _peaks?.DurationSeconds ?? 0;
        if (trailSamples.Count > 0)
        {
            var last = trailSamples[^1];
            var secDelta = ProgressToSec(Math.Abs(progress - last.Progress), durationSec);
            if (secDelta >= DiscontinuitySec(durationSec))
            {
                trailSamples.Clear();
            }
        }

        if (trailSamples.Count > 0)
        {
            var last = trailSamples[^1];
            var secDelta = ProgressToSec(Math.Abs(progress - last.Progress), durationSec);
            if (now - last.TickMs < TrailSampleMinIntervalMs && secDelta < TrailMinSecDelta)
            {
                return;
            }
        }

        trailSamples.Add((progress, now));
        PruneTrailSamplesByAge(now, trailSamples);
        if (trailSamples.Count > TrailMaxSamples)
        {
            trailSamples.RemoveRange(0, trailSamples.Count - TrailMaxSamples);
            PruneTrailSamplesByAge(now, trailSamples);
        }
    }

    private float ContentWidthForTrail()
    {
        var timeline = GetTimelineContentRect();
        return Math.Max(0, timeline.Width);
    }

    private void PruneTrailSamplesByAge(long now, List<(double Progress, long TickMs)> trailSamples)
    {
        var retainMs = Math.Max(TrailSampleRetainMs, TrailFadeMsForView(ContentWidthForTrail()));
        var remove = 0;
        while (remove < trailSamples.Count && now - trailSamples[remove].TickMs >= retainMs)
        {
            remove++;
        }

        if (remove > 0)
        {
            trailSamples.RemoveRange(0, remove);
        }
    }

    /// <summary>
    /// 表示窓で <see cref="TrailTargetLengthPx"/> 相当になるフェード時間（ms）。
    /// </summary>
    private double TrailFadeMsForView(float contentWidth)
    {
        var durationSec = _peaks?.DurationSeconds ?? 0;
        if (durationSec <= 0 || contentWidth <= 1f)
        {
            return TrailSampleRetainMs;
        }

        var viewDurationSec = durationSec * ViewSpan;
        var fadeSec = TrailTargetLengthPx / contentWidth * viewDurationSec;
        // 極端なズームでも帯が消え／伸びすぎないようクランプ（上限は長い曲の全体表示用）
        fadeSec = Math.Clamp(fadeSec, 0.2, 60.0);
        return fadeSec * 1000.0;
    }

    private static double ProgressToSec(double progressDelta, double durationSec)
    {
        if (durationSec > 0)
        {
            return progressDelta * durationSec;
        }

        // 尺不明時は progress 差分を秒に見立てないで時間ゲートだけ効かせる
        return progressDelta * 60d;
    }

    private static double DiscontinuitySec(double durationSec)
    {
        if (durationSec <= 0)
        {
            return TrailDiscontinuitySec;
        }

        return Math.Max(TrailDiscontinuitySec, durationSec * 0.025);
    }

    private static int ToByteAlpha(float a)
    {
        return Math.Clamp((int)MathF.Round(a * 255f), 0, 255);
    }

    private void DrawMouseGuide(Graphics g, Rectangle content)
    {
        if (_mouseGuideX is not float mx)
        {
            return;
        }

        using var pen = new Pen(WaveformGdiColors.MouseGuide, 1f);
        g.DrawLine(pen, mx, content.Top, mx, content.Bottom);
    }

    private sealed class OverlayPlayheadState
    {
        public double Progress;
        public bool TrailActive;
        public readonly List<(double Progress, long TickMs)> TrailSamples = [];
    }
}
