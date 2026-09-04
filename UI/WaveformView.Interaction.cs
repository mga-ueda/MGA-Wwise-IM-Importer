using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TextAlignment = System.Windows.TextAlignment;
using WpfColor = System.Windows.Media.Color;

namespace MgaWwiseIMImporter.UI;

internal sealed partial class WaveformView
{
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        var location = ToGdiPoint(e.GetPosition(this));
        UpdateMouseGuide(location.X);
        if (e.ChangedButton == MouseButton.Right)
        {
            TryShowFadeCurveMenu(location);
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            HandleMouseDoubleClick(location);
            return;
        }

        // ログ等からフォーカスを奪い、ショートカット（ジャンプ系含む）を復帰させる。
        if (CanFocus)
        {
            Focus();
        }

        if (TryBeginMarkerStroke(location))
        {
            return;
        }

        if (_allowsSessionMarkerEdit
            && TryHitSessionMarker(location, out var hit, out _))
        {
            EndMarkerCommentEdit(commit: true);
            SetSelectedMarker(hit.SampleOffset);
            SeekToSample(hit.SampleOffset);
            _isDraggingMarker = true;
            _markerDragFromSample = hit.SampleOffset;
            _markerDragPreviewSample = hit.SampleOffset;
            _markerDragStartX = location.X;
            _markerDragMoved = false;
            Capture = true;
            Cursor = Cursors.SizeWE;
            return;
        }

        if (_allowsSessionMarkerEdit && _selectedMarkerSampleOffset is not null)
        {
            SetSelectedMarker(null);
        }

        if (TryBeginFadeHandleDrag(location))
        {
            return;
        }

        if (!TryResolveSeekProgress(location.X, out var progress))
        {
            return;
        }

        _isDraggingSeek = true;
        _seekDragStartX = location.X;
        _seekMovedDuringDrag = false;
        Capture = true;
        // シングルクリックは MouseDown のみでシークする。
        // MouseUp でもう一度 Seek すると再生中に一瞬鳴ってからやり直すことがある。
        _lastMouseSeekProgress = progress;
        SeekRequested?.Invoke(this, progress);
    }

    private void HandleMouseDoubleClick(Point location)
    {
        if (_markerEditMode is not null)
        {
            return;
        }

        // 2回目の MouseDown で始まったシーク／マーカードラッグを打ち切り、
        // ズーム後の MouseUp が別の絶対位置へシークしないようにする
        _isDraggingSeek = false;
        _seekMovedDuringDrag = false;
        ClearMarkerDragState();
        ClearFadeDragState();
        Capture = false;
        Cursor = null;

        if (_sourceNameEditable && IsSourceNamePoint(location))
        {
            BeginSourceNameEdit();
            return;
        }

        if (_allowsSessionMarkerEdit
            && TryHitSessionMarker(location, out var hit, out _))
        {
            SetSelectedMarker(hit.SampleOffset);
            BeginMarkerCommentEdit(hit);
            return;
        }

        var previousZoom = _timeZoom;
        ZoomTimeToPlaylistUnderMouse(location.X);
        if (_timeZoom > previousZoom + 1e-9)
        {
            TransportFeedbackRequested?.Invoke(this, TransportCommand.TimeZoomIn);
        }
        else if (_timeZoom < previousZoom - 1e-9)
        {
            TransportFeedbackRequested?.Invoke(this, TransportCommand.TimeZoomOut);
        }
        UpdateTimelineTip(location);
    }

    private static bool IsAltKey(KeyEventArgs e) =>
        e.Key is Key.LeftAlt or Key.RightAlt
        || (e.Key == Key.System && e.SystemKey is Key.LeftAlt or Key.RightAlt);

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (_isDraggingMarker && IsAltKey(e))
        {
            RebuildPresentationLayers(clearDetailPeaks: false);
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (_isDraggingMarker && IsAltKey(e))
        {
            RebuildPresentationLayers(clearDetailPeaks: false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_allowsSessionMarkerEdit
            && e.Key == Key.Delete
            && _selectedMarkerSampleOffset is { } sampleOffset
            && _markerCommentEditor is not { Visibility: System.Windows.Visibility.Visible })
        {
            MarkerSessionDeleteRequested?.Invoke(
                this,
                new MarkerSessionDeleteRequestedEventArgs(sampleOffset));
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private bool IsSourceNamePoint(Point location)
    {
        // 編集用 TextBox と同じ領域（ファイル名レーン全体）をヒット対象にする。
        return TryGetSourceNameBounds(out var bounds) && bounds.Contains(location);
    }

    private void SetSourceNameHovered(bool hovered)
    {
        if (_sourceNameHovered == hovered)
        {
            return;
        }

        _sourceNameHovered = hovered;
        Cursor = hovered ? Cursors.IBeam : null;
        Invalidate();
    }

    private bool TryGetSourceNameBounds(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (_sourceDisplayName.Length == 0
            || _peaks is null
            || _peaks.IsEmpty
            || ClientSize.Width <= 8
            || ClientSize.Height <= 8)
        {
            return false;
        }

        using var g = CreateMeasureGraphics();
        var content = ContentBounds;
        var (info, _, wave, _, _, _) = GetLayout(content, g);
        var nameWidth = Math.Max(
            0,
            info.Width
            - InfoLanePadX * 2
            - SourceMeterGapPx
            - SourceMeterWidthPx);
        bounds = new Rectangle(
            info.Left + InfoLanePadX,
            wave.Top + 2,
            nameWidth,
            Math.Max(0, wave.Height - 4));
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private void BeginSourceNameEdit()
    {
        if (!_sourceNameEditable || !TryGetSourceNameBounds(out var bounds))
        {
            return;
        }

        _sourceNameEditor ??= CreateSourceNameEditor();
        SetEditorBounds(_sourceNameEditor, GetSourceNameEditorBounds(bounds, _sourceNameEditor));
        _sourceNameEditor.Text = _sourceDisplayName;
        _sourceNameEditor.Visibility = System.Windows.Visibility.Visible;
        _sourceNameEditor.Focus();
        _sourceNameEditor.SelectAll();
        SourceNameEditStateChanged?.Invoke(
            this,
            new SourceNameEditStateChangedEventArgs(isEditing: true));
    }

    /// <summary>
    /// 編集用 TextBox の矩形（デバイス px）。
    /// FontSize／Padding／Border は DIP なので、必ず DpiScale で px に換算してから
    /// available（GDI レイアウト＝デバイス px）と比較する。
    /// </summary>
    private Rectangle GetSourceNameEditorBounds(Rectangle available, TextBox editor)
    {
        var scale = DpiScale;
        // DIP: フォント＋枠＋余白。選択ハイライト／キャレット分に +2。
        var preferredHeightDip =
            editor.FontSize
            + editor.Padding.Top + editor.Padding.Bottom
            + editor.BorderThickness.Top + editor.BorderThickness.Bottom
            + 2;
        // Bold や Yu Gothic UI の実メトリクス余裕。
        preferredHeightDip = Math.Max(preferredHeightDip, editor.FontSize * 1.45
            + editor.Padding.Top + editor.Padding.Bottom
            + editor.BorderThickness.Top + editor.BorderThickness.Bottom);

        var preferredHeightPx = (int)Math.Ceiling(preferredHeightDip * scale);
        var minHeightPx = (int)Math.Ceiling(22 * scale);
        var height = Math.Min(available.Height, Math.Max(minHeightPx, preferredHeightPx));
        return new Rectangle(
            available.Left,
            available.Top + Math.Max(0, (available.Height - height) / 2),
            available.Width,
            height);
    }

    /// <summary>編集用 TextBox と同じ寸法のホバー枠。Paint 中にコントロールを生成しない。</summary>
    private Rectangle GetSourceNameHoverBounds(Rectangle available)
    {
        if (_sourceNameEditor is { } editor)
        {
            return GetSourceNameEditorBounds(available, editor);
        }

        // エディタ未生成時も GetSourceNameEditorBounds と同じ換算で揃える。
        var scale = DpiScale;
        var fontSizeDip = GdiPointsToWpfFontSize(Font.SizeInPoints);
        const double paddingY = 1 + 1;
        const double borderY = 1 + 1;
        var preferredHeightDip = Math.Max(fontSizeDip + paddingY + borderY + 2, fontSizeDip * 1.45 + paddingY + borderY);
        var preferredHeightPx = (int)Math.Ceiling(preferredHeightDip * scale);
        var minHeightPx = (int)Math.Ceiling(22 * scale);
        var height = Math.Min(available.Height, Math.Max(minHeightPx, preferredHeightPx));
        return new Rectangle(
            available.Left,
            available.Top + Math.Max(0, (available.Height - height) / 2),
            available.Width,
            height);
    }

    private TextBox CreateSourceNameEditor()
    {
        // Digits などのオプション入力と同じ、システム描画の FixedSingle 枠。
        var editor = CreateDarkInlineEditor(bold: true, TextAlignment.Center);
        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                EndSourceNameEdit(commit: true);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                EndSourceNameEdit(commit: false);
            }
        };
        editor.LostFocus += (_, _) => EndSourceNameEdit(commit: true);
        RegisterVisualChild(editor);
        return editor;
    }

    private void EndSourceNameEdit(bool commit)
    {
        if (_endingSourceNameEdit
            || _sourceNameEditor is not { Visibility: System.Windows.Visibility.Visible } editor)
        {
            return;
        }

        _endingSourceNameEdit = true;
        try
        {
            var name = editor.Text.Trim();
            editor.Visibility = System.Windows.Visibility.Collapsed;
            // TextBox を隠すと次の TabStop（フッタの GitHub 等）へフォーカスが飛ぶため、
            // 波形ビューへ戻す（FocusVisual は無効・枠線なし）。
            if (IsHandleCreated && CanFocus)
            {
                Focus();
            }

            if (commit)
            {
                // 空欄も Form1 側へ渡し、元のファイル名へ戻す。
                SourceNameEditCommitted?.Invoke(
                    this,
                    new SourceNameEditCommittedEventArgs(name));
            }
        }
        finally
        {
            _endingSourceNameEdit = false;
            SourceNameEditStateChanged?.Invoke(
                this,
                new SourceNameEditStateChangedEventArgs(isEditing: false));
        }
    }

    private void ClearMarkerSessionEditState()
    {
        EndMarkerCommentEdit(commit: false);
        ClearMarkerDragState();
        _selectedMarkerSampleOffset = null;
        _markerHitRegions.Clear();
    }

    private void ClearMarkerDragState()
    {
        _isDraggingMarker = false;
        _markerDragPreviewSample = null;
        _markerDragMoved = false;
    }

    private long GetDisplayedMarkerSample(long sampleOffset)
    {
        if (!_isDraggingMarker || _markerDragPreviewSample is not { } preview)
        {
            return sampleOffset;
        }

        if (sampleOffset == _markerDragFromSample)
        {
            return preview;
        }

        // Alt+ドラッグ: 一つ前のマーカーも同じ差分だけプレビュー移動する。
        // プレビュー自体をペア移動可能範囲に制限済みなので、単純な差分でよい。
        if ((ModifierKeys & System.Windows.Input.ModifierKeys.Alt) != 0
            && TryGetPreviousMarkerSample(_markerDragFromSample, out var previousSample)
            && sampleOffset == previousSample)
        {
            var delta = preview - _markerDragFromSample;
            return previousSample + delta;
        }

        return sampleOffset;
    }

    /// <summary>
    /// ドラッグ中の到達サンプルを同一 Playlist 内に収める。
    /// Alt ペア移動時は、一つ前マーカーが止まった方向へ主マーカーも進めない。
    /// </summary>
    private long ClampMarkerDragPreviewSample(long desiredSampleOffset)
    {
        desiredSampleOffset = ClampMarkerSample(desiredSampleOffset);
        if (_peaks is null || _peaks.FrameCount <= 0)
        {
            return desiredSampleOffset;
        }

        if (!TryGetHostOutputPartRange(
                _markerDragFromSample,
                out var rangeMin,
                out var rangeMax))
        {
            rangeMin = 0;
            rangeMax = _peaks.FrameCount - 1;
        }

        desiredSampleOffset = Math.Clamp(desiredSampleOffset, rangeMin, rangeMax);

        if ((ModifierKeys & System.Windows.Input.ModifierKeys.Alt) == 0
            || !TryGetPreviousMarkerSample(_markerDragFromSample, out var previousSample))
        {
            return desiredSampleOffset;
        }

        var occupied = new List<long>(_markers.Count);
        foreach (var marker in _markers)
        {
            occupied.Add(marker.SampleOffset);
        }

        return WaveformPreviewSession.ClampPairedMarkerDestination(
            _markerDragFromSample,
            previousSample,
            desiredSampleOffset,
            rangeMin,
            rangeMax,
            occupied);
    }

    private bool TryGetHostOutputPartRange(
        long sampleOffset,
        out long rangeMinInclusive,
        out long rangeMaxInclusive)
    {
        rangeMinInclusive = 0;
        rangeMaxInclusive = 0;
        foreach (var part in _outputParts)
        {
            if (sampleOffset < part.StartSampleOffset
                || sampleOffset >= part.EndSampleOffset
                || part.EndSampleOffset <= part.StartSampleOffset)
            {
                continue;
            }

            rangeMinInclusive = part.StartSampleOffset;
            rangeMaxInclusive = part.EndSampleOffset - 1;
            return rangeMaxInclusive >= rangeMinInclusive;
        }

        return false;
    }

    private bool TryGetPreviousMarkerSample(long sampleOffset, out long previousSample)
    {
        previousSample = 0;
        var limitToPlaylist = TryGetHostOutputPartRange(
            sampleOffset,
            out var rangeMin,
            out var rangeMax);

        long? best = null;
        foreach (var marker in _markers)
        {
            if (marker.IsSharedProjection || marker.SampleOffset >= sampleOffset)
            {
                continue;
            }

            // 同一 Playlist 内の前マーカーだけをペア移動対象にする。
            if (limitToPlaylist
                && (marker.SampleOffset < rangeMin || marker.SampleOffset > rangeMax))
            {
                continue;
            }

            if (best is null || marker.SampleOffset > best.Value)
            {
                best = marker.SampleOffset;
            }
        }

        if (best is not { } found)
        {
            return false;
        }

        previousSample = found;
        return true;
    }

    private long ClampMarkerSample(long sampleOffset)
    {
        if (_peaks is null || _peaks.FrameCount <= 0)
        {
            return Math.Max(0L, sampleOffset);
        }

        return Math.Clamp(sampleOffset, 0L, _peaks.FrameCount - 1);
    }

    private bool TryGetSampleFromX(int mouseX, out long sampleOffset)
    {
        sampleOffset = 0;
        if (_wavInfo is null || _wavInfo.FrameCount <= 0)
        {
            return false;
        }

        if (!TryGetProgressFromX(mouseX, out var progress))
        {
            return false;
        }

        sampleOffset = (long)Math.Round(progress * _wavInfo.FrameCount);
        sampleOffset = Math.Clamp(sampleOffset, 0L, _wavInfo.FrameCount - 1);
        return true;
    }

    private void SetSelectedMarker(long? sampleOffset)
    {
        if (_selectedMarkerSampleOffset == sampleOffset)
        {
            return;
        }

        _selectedMarkerSampleOffset = sampleOffset;
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    private bool TryHitSessionMarker(
        Point location,
        out MarkerHitRegion hit,
        out bool hitTriangle)
    {
        hit = default;
        hitTriangle = false;
        for (var i = _markerHitRegions.Count - 1; i >= 0; i--)
        {
            var candidate = _markerHitRegions[i];
            if (candidate.TriangleBounds.Contains(location))
            {
                hit = candidate;
                hitTriangle = true;
                return true;
            }

            if (candidate.CommentBounds.Width > 0
                && candidate.CommentBounds.Contains(location))
            {
                hit = candidate;
                return true;
            }
        }

        return false;
    }

    private void BeginMarkerCommentEdit(MarkerHitRegion hit)
    {
        if (!_allowsSessionMarkerEdit)
        {
            return;
        }

        EndSourceNameEdit(commit: false);
        _markerCommentEditor ??= CreateMarkerCommentEditor();
        var editBounds = GetMarkerCommentEditorBounds(hit);
        SetEditorBounds(_markerCommentEditor, editBounds);
        _markerCommentEditSampleOffset = hit.SampleOffset;
        _markerCommentEditor.Text = hit.Comment;
        _markerCommentEditor.Visibility = System.Windows.Visibility.Visible;
        _markerCommentEditor.Focus();
        _markerCommentEditor.SelectAll();
        MarkerCommentEditStateChanged?.Invoke(
            this,
            new MarkerCommentEditStateChangedEventArgs(isEditing: true));
    }

    private Rectangle GetMarkerCommentEditorBounds(MarkerHitRegion hit)
    {
        var scale = DpiScale;
        var left = hit.CommentBounds.Width > 0
            ? (int)Math.Floor(hit.CommentBounds.Left)
            : (int)Math.Floor(hit.TriangleBounds.Right + 2f);
        var top = hit.CommentBounds.Width > 0
            ? (int)Math.Floor(hit.CommentBounds.Top) - 1
            : (int)Math.Floor(hit.TriangleBounds.Top) - 1;
        var width = hit.CommentBounds.Width > 0
            ? Math.Max(80, (int)Math.Ceiling(hit.CommentBounds.Width) + 16)
            : 120;
        // CommentBounds はデバイス px。WPF TextBox の枠・Padding 分を足し、
        // 下限 22 も DIP→px 換算する（高 DPI で字形が欠けないように）。
        var contentHeightPx = hit.CommentBounds.Width > 0
            ? (int)Math.Ceiling(hit.CommentBounds.Height)
            : (int)Math.Ceiling(hit.TriangleBounds.Height);
        var chromePx = (int)Math.Ceiling((1 + 1 + 1 + 1) * scale); // Border+Padding 上下
        var minHeightPx = (int)Math.Ceiling(22 * scale);
        var height = Math.Max(minHeightPx, contentHeightPx + chromePx + (int)Math.Ceiling(2 * scale));
        width = Math.Min(width, Math.Max(40, ClientSize.Width - left - 4));
        return new Rectangle(left, top, width, height);
    }

    private TextBox CreateMarkerCommentEditor()
    {
        var editor = CreateDarkInlineEditor(bold: false, TextAlignment.Left);
        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                EndMarkerCommentEdit(commit: true);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                EndMarkerCommentEdit(commit: false);
            }
        };
        editor.LostFocus += (_, _) => EndMarkerCommentEdit(commit: true);
        RegisterVisualChild(editor);
        return editor;
    }

    private void EndMarkerCommentEdit(bool commit)
    {
        if (_endingMarkerCommentEdit
            || _markerCommentEditor is not { Visibility: System.Windows.Visibility.Visible } editor
            || _markerCommentEditSampleOffset is not { } sampleOffset)
        {
            return;
        }

        _endingMarkerCommentEdit = true;
        try
        {
            var comment = editor.Text.Trim();
            editor.Visibility = System.Windows.Visibility.Collapsed;
            _markerCommentEditSampleOffset = null;
            if (IsHandleCreated && CanFocus)
            {
                Focus();
            }

            if (commit)
            {
                MarkerCommentEditCommitted?.Invoke(
                    this,
                    new MarkerCommentEditCommittedEventArgs(sampleOffset, comment));
            }
        }
        finally
        {
            _endingMarkerCommentEdit = false;
            MarkerCommentEditStateChanged?.Invoke(
                this,
                new MarkerCommentEditStateChangedEventArgs(isEditing: false));
        }
    }

    private readonly record struct MarkerHitRegion(
        long SampleOffset,
        string Comment,
        RectangleF TriangleBounds,
        RectangleF CommentBounds);

    private readonly record struct FadeHandleHitRegion(
        long InSample,
        long OutSample,
        bool IsFadeIn,
        RectangleF TriangleBounds);

    private readonly record struct FadeAreaHitRegion(
        long InSample,
        long OutSample,
        bool IsFadeIn,
        RectangleF AreaBounds);

    private bool TryBeginFadeHandleDrag(Point location)
    {
        if (!TryHitFadeHandle(location, out var hit))
        {
            return false;
        }

        EndMarkerCommentEdit(commit: true);
        var existing = FindFade(hit.InSample, hit.OutSample);
        _isDraggingFadeHandle = true;
        _fadeDragIsIn = hit.IsFadeIn;
        _fadeDragInSample = hit.InSample;
        _fadeDragOutSample = hit.OutSample;
        _fadeDragOtherHandleSample = hit.IsFadeIn
            ? existing?.EffectiveFadeOutStart
            : existing?.EffectiveFadeInEnd;
        _fadeDragPreview = existing ?? new RegionEdgeFade(
            hit.InSample,
            hit.OutSample,
            null,
            null,
            DefaultFadeInCurve,
            DefaultFadeOutCurve);
        _fadeDragStartX = location.X;
        _fadeDragMoved = false;
        Capture = true;
        Cursor = Cursors.SizeWE;
        return true;
    }

    private bool TryShowFadeCurveMenu(Point location)
    {
        if (!TryHitFadeArea(location, out var area))
        {
            return false;
        }

        var existing = FindFade(area.InSample, area.OutSample);
        if (existing is null
            || (area.IsFadeIn && !existing.Value.HasFadeIn)
            || (!area.IsFadeIn && !existing.Value.HasFadeOut))
        {
            return false;
        }

        EndMarkerCommentEdit(commit: true);
        ShowFadeCurveContextMenu(location, existing.Value, area.IsFadeIn);
        return true;
    }

    private bool TryHitFadeArea(Point location, out FadeAreaHitRegion hit)
    {
        for (var i = _fadeAreaHitRegions.Count - 1; i >= 0; i--)
        {
            var candidate = _fadeAreaHitRegions[i];
            if (candidate.AreaBounds.Contains(location))
            {
                hit = candidate;
                return true;
            }
        }

        hit = default;
        return false;
    }

    private void ShowFadeCurveContextMenu(Point location, RegionEdgeFade fade, bool isFadeIn)
    {
        var current = isFadeIn ? fade.FadeInCurve : fade.FadeOutCurve;
        FadeCurveIcons.ShowPicker(
            this,
            ToWpfPoint(location),
            current,
            isFadeIn,
            kind => ApplyFadeCurve(fade, isFadeIn, kind),
            ref _fadeCurveMenu);
    }

    private void ApplyFadeCurve(RegionEdgeFade fade, bool isFadeIn, RegionFadeCurveKind kind)
    {
        var next = isFadeIn
            ? fade.WithCurves(kind, fade.FadeOutCurve)
            : fade.WithCurves(fade.FadeInCurve, kind);
        long? firstEnd = null;
        long? lastStart = null;
        if (RegionEdgeFade.TryGetRunSegmentLimits(
                _regions,
                fade.InSample,
                fade.OutSample,
                out var limits))
        {
            firstEnd = limits.FirstSegmentEndSample;
            lastStart = limits.LastSegmentStartSample;
        }

        RegionFadeChanged?.Invoke(
            this,
            new RegionFadeChangedEventArgs(next.Normalized(firstEnd, lastStart)));
    }

    private bool TryHitFadeHandle(Point location, out FadeHandleHitRegion hit)
    {
        for (var i = _fadeHandleHitRegions.Count - 1; i >= 0; i--)
        {
            var candidate = _fadeHandleHitRegions[i];
            if (candidate.TriangleBounds.Contains(location))
            {
                hit = candidate;
                return true;
            }
        }

        hit = default;
        return false;
    }

    private RegionEdgeFade? FindFade(long inSample, long outSample)
    {
        foreach (var fade in _regionEdgeFades)
        {
            if (fade.InSample == inSample && fade.OutSample == outSample)
            {
                return fade;
            }
        }

        return null;
    }

    private IReadOnlyList<RegionEdgeFade> GetDisplayRegionEdgeFades()
    {
        if (_fadeDragPreview is not { } preview)
        {
            return _regionEdgeFades;
        }

        var list = new List<RegionEdgeFade>(_regionEdgeFades.Count + 1);
        foreach (var fade in _regionEdgeFades)
        {
            if (fade.InSample == preview.InSample && fade.OutSample == preview.OutSample)
            {
                continue;
            }

            list.Add(fade);
        }

        if (preview.HasAnyFade
            || (_isDraggingFadeHandle
                && preview.InSample == _fadeDragInSample
                && preview.OutSample == _fadeDragOutSample))
        {
            long? firstEnd = null;
            long? lastStart = null;
            if (RegionEdgeFade.TryGetRunSegmentLimits(
                    _regions,
                    preview.InSample,
                    preview.OutSample,
                    out var limits))
            {
                firstEnd = limits.FirstSegmentEndSample;
                lastStart = limits.LastSegmentStartSample;
            }

            list.Add(preview.Normalized(firstEnd, lastStart));
        }

        return list;
    }

    private void ClearFadeDragState()
    {
        _isDraggingFadeHandle = false;
        _fadeDragMoved = false;
        _fadeDragPreview = null;
        _fadeDragOtherHandleSample = null;
    }

    private void UpdateFadeDragPreview(long sampleOffset)
    {
        var inSample = _fadeDragInSample;
        var outSample = _fadeDragOutSample;
        if (outSample <= inSample)
        {
            return;
        }

        RegionEdgeFade.TryGetRunSegmentLimits(
            _regions,
            inSample,
            outSample,
            out var limits);
        long? firstSegmentEnd = limits.OutSample > limits.InSample
            ? limits.FirstSegmentEndSample
            : null;
        long? lastSegmentStart = limits.OutSample > limits.InSample
            ? limits.LastSegmentStartSample
            : null;

        RegionEdgeFade next;
        var inCurve = _fadeDragPreview?.FadeInCurve ?? DefaultFadeInCurve;
        var outCurve = _fadeDragPreview?.FadeOutCurve ?? DefaultFadeOutCurve;
        if (_fadeDragIsIn)
        {
            var maxEnd = _fadeDragOtherHandleSample is { } other && other > inSample
                ? other
                : outSample;
            if (firstSegmentEnd is { } segmentEnd)
            {
                maxEnd = Math.Min(maxEnd, segmentEnd);
            }

            var fadeInEnd = Math.Clamp(sampleOffset, inSample, maxEnd);
            next = RegionEdgeFade.WithFadeInEnd(
                inSample,
                outSample,
                fadeInEnd,
                _fadeDragOtherHandleSample is { } o && o < outSample ? o : null,
                inCurve,
                outCurve,
                firstSegmentEnd,
                lastSegmentStart);
        }
        else
        {
            var minStart = _fadeDragOtherHandleSample is { } other && other < outSample
                ? other
                : inSample;
            if (lastSegmentStart is { } segmentStart)
            {
                minStart = Math.Max(minStart, segmentStart);
            }

            var fadeOutStart = Math.Clamp(sampleOffset, minStart, outSample);
            next = RegionEdgeFade.WithFadeOutStart(
                inSample,
                outSample,
                _fadeDragOtherHandleSample is { } o && o > inSample ? o : null,
                fadeOutStart,
                inCurve,
                outCurve,
                firstSegmentEnd,
                lastSegmentStart);
        }

        if (_fadeDragPreview is { } current
            && current.FadeInEndSample == next.FadeInEndSample
            && current.FadeOutStartSample == next.FadeOutStartSample)
        {
            return;
        }

        _fadeDragPreview = next;
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var location = ToGdiPoint(e.GetPosition(this));
        UpdateMouseGuide(location.X);
        SetSourceNameHovered(_sourceNameEditable && IsSourceNamePoint(location));
        UpdateTimelineTip(location);

        if (_markerEditMode is not null)
        {
            ApplyMarkerStroke(_markerStrokeLastX, location.X, includeNearest: true);
            _markerStrokeLastX = location.X;
            return;
        }

        if (_isDraggingMarker)
        {
            if (!_markerDragMoved
                && Math.Abs(location.X - _markerDragStartX) < 3)
            {
                return;
            }

            _markerDragMoved = true;
            if (!TryGetSampleFromX(location.X, out var sampleOffset))
            {
                return;
            }

            sampleOffset = ClampMarkerDragPreviewSample(sampleOffset);
            if (_markerDragPreviewSample == sampleOffset)
            {
                return;
            }

            _markerDragPreviewSample = sampleOffset;
            RebuildPresentationLayers(clearDetailPeaks: false);
            return;
        }

        if (_isDraggingFadeHandle)
        {
            if (!_fadeDragMoved
                && Math.Abs(location.X - _fadeDragStartX) < 3)
            {
                return;
            }

            _fadeDragMoved = true;
            if (!TryGetSampleFromX(location.X, out var sampleOffset))
            {
                return;
            }

            UpdateFadeDragPreview(sampleOffset);
            return;
        }

        if (!_isDraggingSeek
            && !_isDraggingMarker
            && !_isDraggingFadeHandle
            && TryHitFadeHandle(location, out _))
        {
            Cursor = Cursors.SizeWE;
        }
        else if (!_isDraggingSeek && !_isDraggingMarker && !_isDraggingFadeHandle)
        {
            Cursor = null;
        }

        if (!_isDraggingSeek || !TryGetProgressFromX(location.X, out var progress))
        {
            return;
        }

        // クリックとドラッグを分ける（微小なマウスブレは無視）
        if (!_seekMovedDuringDrag
            && Math.Abs(location.X - _seekDragStartX) < 3)
        {
            return;
        }

        _seekMovedDuringDrag = true;
        if (!double.IsNaN(_lastMouseSeekProgress)
            && Math.Abs(progress - _lastMouseSeekProgress) < 1e-9)
        {
            return;
        }

        _lastMouseSeekProgress = progress;
        SeekRequested?.Invoke(this, progress);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        var location = ToGdiPoint(e.GetPosition(this));
        UpdateMouseGuide(location.X);
        if (e.ChangedButton == MouseButton.Left && _markerEditMode is not null)
        {
            ApplyMarkerStroke(_markerStrokeLastX, location.X, includeNearest: true);
            _markerEditMode = null;
            Capture = false;
            return;
        }

        if (e.ChangedButton == MouseButton.Left && _isDraggingMarker)
        {
            var markerMoved = _markerDragMoved;
            var fromSample = _markerDragFromSample;
            var toSample = _markerDragPreviewSample ?? fromSample;
            ClearMarkerDragState();
            Capture = false;
            Cursor = null;

            if (markerMoved && toSample != fromSample)
            {
                MarkerSessionMoveRequested?.Invoke(
                    this,
                    new MarkerSessionMoveRequestedEventArgs(
                        fromSample,
                        toSample,
                        shiftPreviousMarker: (ModifierKeys & System.Windows.Input.ModifierKeys.Alt) != 0));
            }
            else
            {
                Invalidate();
            }

            return;
        }

        if (e.ChangedButton == MouseButton.Left && _isDraggingFadeHandle)
        {
            var preview = _fadeDragPreview;
            var fadeMoved = _fadeDragMoved;
            ClearFadeDragState();
            Capture = false;
            Cursor = null;

            if (fadeMoved && preview is { } fade)
            {
                long? firstEnd = null;
                long? lastStart = null;
                if (RegionEdgeFade.TryGetRunSegmentLimits(
                        _regions,
                        fade.InSample,
                        fade.OutSample,
                        out var limits))
                {
                    firstEnd = limits.FirstSegmentEndSample;
                    lastStart = limits.LastSegmentStartSample;
                }

                RegionFadeChanged?.Invoke(
                    this,
                    new RegionFadeChangedEventArgs(fade.Normalized(firstEnd, lastStart)));
            }
            else
            {
                RebuildPresentationLayers(clearDetailPeaks: false);
            }

            return;
        }

        if (e.ChangedButton != MouseButton.Left || !_isDraggingSeek)
        {
            return;
        }

        var moved = _seekMovedDuringDrag;
        _isDraggingSeek = false;
        _seekMovedDuringDrag = false;
        Capture = false;
        // ドラッグ終了位置だけ確定。クリックのみの場合は MouseDown 済みなので再シークしない。
        if (moved
            && TryGetProgressFromX(location.X, out var progress)
            && (double.IsNaN(_lastMouseSeekProgress)
                || Math.Abs(progress - _lastMouseSeekProgress) >= 1e-9))
        {
            _lastMouseSeekProgress = progress;
            SeekRequested?.Invoke(this, progress);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateTimelineTip(null);
        SetSourceNameHovered(false);
        SetHoveredPlaylistPart(null);
        if (_isDraggingSeek || _isDraggingMarker || _isDraggingFadeHandle || _markerEditMode is not null)
        {
            return;
        }

        if (_mouseGuideX is not null)
        {
            _mouseGuideX = null;
            _mouseGuideSnapSample = null;
            RequestMouseGuideRepaint();
        }
    }

    private bool TryBeginMarkerStroke(Point location)
    {
        var modifiers = ModifierKeys;
        var editMode = (modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control
            ? MarkerEditMode.Remove
            : (modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift
                ? MarkerEditMode.Add
                : (MarkerEditMode?)null;
        if (editMode is null
            || _allowsSessionMarkerEdit
            || _peaks is null
            || _peaks.IsEmpty
            || !TryGetMarkerLane(out var markerLane, out _)
            || !markerLane.Contains(location))
        {
            return false;
        }

        _isDraggingSeek = false;
        _markerEditMode = editMode;
        _markerStrokeLastX = location.X;
        Capture = true;
        ApplyMarkerStroke(location.X, location.X, includeNearest: true);
        return true;
    }

    private void ApplyMarkerStroke(int fromX, int toX, bool includeNearest)
    {
        if (_markerEditMode is not { } mode
            || !TryGetMarkerLane(out _, out var labels)
            || labels.Width <= 0)
        {
            return;
        }

        var points = EnumerateVisibleMarkerGrid(labels);
        if (points.Count == 0)
        {
            return;
        }

        var minX = Math.Min(fromX, toX);
        var maxX = Math.Max(fromX, toX);
        var samples = points
            .Where(point => point.X >= minX - 0.5f && point.X <= maxX + 0.5f)
            .Select(point => point.SampleOffset)
            .ToHashSet();

        if (includeNearest)
        {
            var clampedX = Math.Clamp(toX, labels.Left, labels.Right);
            var nearest = points.MinBy(point => Math.Abs(point.X - clampedX));
            samples.Add(nearest.SampleOffset);
        }

        if (samples.Count > 0)
        {
            MarkerEditRequested?.Invoke(
                this,
                new MarkerEditRequestedEventArgs(mode, [.. samples.Order()]));
        }
    }

    private bool TryGetMarkerLane(out Rectangle markerLane, out Rectangle labels)
    {
        markerLane = Rectangle.Empty;
        labels = Rectangle.Empty;
        if (_peaks is null || _peaks.IsEmpty || ClientSize.Width <= 8 || ClientSize.Height <= 8)
        {
            return false;
        }

        using var g = CreateMeasureGraphics();
        var content = ContentBounds;
        (_, labels, _, _, _, var rowHeight) = GetLayout(content, g);
        if (labels.Width <= 0 || rowHeight <= 0f)
        {
            return false;
        }

        markerLane = Rectangle.FromLTRB(
            labels.Left,
            (int)Math.Floor(labels.Top + rowHeight * 3f),
            labels.Right,
            (int)Math.Ceiling(labels.Top + rowHeight * 4f));
        return markerLane.Height > 0;
    }

    private IReadOnlyList<MarkerGridPoint> EnumerateVisibleMarkerGrid(Rectangle labels)
    {
        if (_peaks is null || _peaks.FrameCount <= 0)
        {
            return [];
        }

        var frameCount = _peaks.FrameCount;
        var barStarts = _bars
            .Where(bar => !bar.IsTempoChangeOnly)
            .OrderBy(bar => bar.SampleOffset)
            .ToArray();
        if (barStarts.Length == 0)
        {
            return [];
        }

        var points = new List<MarkerGridPoint>();
        void AddPoint(double sample)
        {
            var absolute = sample / frameCount;
            if (absolute < _viewStart - 1e-9 || absolute > ViewEnd + 1e-9)
            {
                return;
            }

            var sampleOffset = (long)Math.Clamp(
                Math.Round(sample, MidpointRounding.AwayFromZero),
                0d,
                Math.Max(0L, frameCount - 1));
            var hostPart = _outputParts.FirstOrDefault(part =>
                sampleOffset >= part.StartSampleOffset
                && sampleOffset < part.EndSampleOffset);
            if (hostPart.EndSampleOffset <= hostPart.StartSampleOffset
                || _disabledPlaylistPartNumbers.Contains(hostPart.Number))
            {
                return;
            }

            if (_regions.Any(region =>
                    sampleOffset >= region.StartSampleOffset
                    && sampleOffset < region.EndSampleOffset
                    && (string.Equals(region.NameSuffix, "-A", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(region.NameSuffix, "-E", StringComparison.OrdinalIgnoreCase))))
            {
                return;
            }

            points.Add(new MarkerGridPoint(sampleOffset, AbsoluteToX(absolute, labels)));
        }

        // Override 指定時は表示グリッド（ズーム状態）を無視して単位を固定する。
        // 縦線の描画には影響しない（スナップ候補のみ変更）。
        var includeBeats = MarkerGridOverride switch
        {
            MarkerGridOverrideMode.Bar => false,
            MarkerGridOverrideMode.Beat => true,
            _ => CalculateVisibleBarCount(barStarts, frameCount) < 8d - 1e-9,
        };
        if (includeBeats)
        {
            for (var i = 0; i < barStarts.Length; i++)
            {
                var bar = barStarts[i];
                AddPoint(bar.SampleOffset);
                if (i + 1 >= barStarts.Length
                    || bar.Numerator <= 1
                    || barStarts[i + 1].SampleOffset <= bar.SampleOffset)
                {
                    continue;
                }

                var next = barStarts[i + 1];
                for (var beat = 1; beat < bar.Numerator; beat++)
                {
                    AddPoint(
                        bar.SampleOffset
                        + (next.SampleOffset - bar.SampleOffset) * beat / (double)bar.Numerator);
                }
            }
        }
        else if (MarkerGridOverride == MarkerGridOverrideMode.Bar)
        {
            // 常に小節単位: 表示上の間引きに関わらず全小節頭を候補にする。
            foreach (var bar in barStarts)
            {
                AddPoint(bar.SampleOffset);
            }
        }
        else
        {
            var averageGapPx = EstimateVisibleBarGapPx(labels, frameCount);
            using var g = CreateMeasureGraphics();
            var minGap = g.MeasureString("000", Font).Width + 6f;
            var step = ChooseBarThinningStep(averageGapPx, minGap);
            int? previousTempo = null;
            int? previousNumerator = null;
            int? previousDenominator = null;

            foreach (var bar in barStarts)
            {
                var tempo = (int)Math.Round(bar.Bpm, MidpointRounding.AwayFromZero);
                var structural = previousTempo is null
                    || previousTempo != tempo
                    || previousNumerator != bar.Numerator
                    || previousDenominator != bar.Denominator;
                if (structural || IsBarOnThinningGrid(bar.BarNumber, step))
                {
                    AddPoint(bar.SampleOffset);
                }

                previousTempo = tempo;
                previousNumerator = bar.Numerator;
                previousDenominator = bar.Denominator;
            }
        }

        return [.. points
            .GroupBy(point => point.SampleOffset)
            .Select(group => group.First())
            .OrderBy(point => point.X)];
    }

    private readonly record struct MarkerGridPoint(long SampleOffset, float X);

    private const float MouseGuideMarkerSnapPx = 8f;

    private void UpdateMouseGuide(int mouseX)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            SetHoveredPlaylistPart(null);
            return;
        }

        var timeline = GetTimelineContentRect();
        if (timeline.Width <= 0)
        {
            SetHoveredPlaylistPart(null);
            return;
        }

        if (mouseX < timeline.Left)
        {
            SetHoveredPlaylistPart(null);
            if (_mouseGuideX is not null)
            {
                _mouseGuideX = null;
                _mouseGuideSnapSample = null;
                RequestMouseGuideRepaint();
            }

            return;
        }

        UpdateHoveredPlaylistPart(mouseX);
        ResolveMouseGuideX(mouseX, timeline, out var x, out var snapSample);
        if (_mouseGuideX is float prev
            && Math.Abs(prev - x) < 0.25f
            && _mouseGuideSnapSample == snapSample)
        {
            return;
        }

        _mouseGuideX = x;
        _mouseGuideSnapSample = snapSample;
        RequestMouseGuideRepaint();
    }

    /// <summary>
    /// マウスガイドは GDI フレームではなく WPF オーバーレイ。
    /// 再生中の全再描画はシークバーを遅らせるので、ガイドだけを動かす。
    /// </summary>
    private void RequestMouseGuideRepaint()
    {
        if (IsPlayheadTrailAnimating())
        {
            EnsureMouseGuideLiveTracking();
        }

        ApplyMouseGuideOverlay();
    }

    private void EnsureMouseGuideLiveTracking()
    {
        if (_mouseGuideRenderingHooked || IsDisposed)
        {
            return;
        }

        CompositionTarget.Rendering += OnMouseGuideRendering;
        _mouseGuideRenderingHooked = true;
    }

    private void StopMouseGuideLiveTracking()
    {
        if (!_mouseGuideRenderingHooked)
        {
            return;
        }

        CompositionTarget.Rendering -= OnMouseGuideRendering;
        _mouseGuideRenderingHooked = false;
    }

    private void OnMouseGuideRendering(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            StopMouseGuideLiveTracking();
            return;
        }

        if (!IsPlayheadTrailAnimating())
        {
            StopMouseGuideLiveTracking();
            ApplyMouseGuideOverlay();
            return;
        }

        SyncMouseGuideFromScreenCursor();
        ApplyMouseGuideOverlay();
    }

    /// <summary>
    /// WPF の最後の MouseMove ではなく、OS の現在カーソル位置を使う。
    /// 再生描画で入力が滞ってもガイドがカーソルに付く。
    /// </summary>
    private void SyncMouseGuideFromScreenCursor()
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        var dragging = _isDraggingSeek
            || _isDraggingMarker
            || _isDraggingFadeHandle
            || _markerEditMode is not null;
        if (!IsMouseOver && !dragging)
        {
            return;
        }

        if (!TryGetLiveMouseGdiX(out var mouseX))
        {
            return;
        }

        var timeline = GetTimelineContentRect();
        if (timeline.Width <= 0)
        {
            return;
        }

        if (!dragging && mouseX < timeline.Left)
        {
            _mouseGuideX = null;
            _mouseGuideSnapSample = null;
            SetHoveredPlaylistPart(null);
            return;
        }

        UpdateHoveredPlaylistPart(mouseX);
        ResolveMouseGuideX(mouseX, timeline, out var x, out var snapSample);
        _mouseGuideX = x;
        _mouseGuideSnapSample = snapSample;
    }

    private bool CanSnapMouseGuideToMarkers =>
        !_isDraggingSeek
        && !_isDraggingMarker
        && !_isDraggingFadeHandle
        && _markerEditMode is null;

    private void ResolveMouseGuideX(
        int mouseX,
        Rectangle timeline,
        out float x,
        out long? snapSample)
    {
        if (CanSnapMouseGuideToMarkers
            && TrySnapMouseXToMarker(mouseX, timeline, out var snappedX, out var sample))
        {
            x = snappedX;
            snapSample = sample;
            return;
        }

        x = Math.Clamp(mouseX, timeline.Left, timeline.Right);
        snapSample = null;
    }

    private bool TrySnapMouseXToMarker(
        int mouseX,
        Rectangle timeline,
        out float snappedX,
        out long sampleOffset)
    {
        snappedX = 0f;
        sampleOffset = 0;
        if (_peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0 || _markers.Count == 0)
        {
            return false;
        }

        var frameCount = _peaks.FrameCount;
        var bestDist = MouseGuideMarkerSnapPx;
        long? bestSample = null;
        var bestX = 0f;
        foreach (var marker in _markers)
        {
            var sample = Math.Clamp(marker.SampleOffset, 0L, frameCount);
            var abs = SampleToAbsolute(sample, frameCount);
            if (abs < _viewStart - 1e-9 || abs > ViewEnd + 1e-9)
            {
                continue;
            }

            var markerX = AbsoluteToX(abs, timeline);
            var dist = Math.Abs(markerX - mouseX);
            if (dist > bestDist)
            {
                continue;
            }

            bestDist = dist;
            bestSample = sample;
            bestX = markerX;
        }

        if (bestSample is not { } found)
        {
            return false;
        }

        snappedX = bestX;
        sampleOffset = found;
        return true;
    }

    private bool TryResolveSeekProgress(int mouseX, out double progress)
    {
        if (_mouseGuideSnapSample is { } sample
            && _peaks is not null
            && _peaks.FrameCount > 0)
        {
            progress = SampleToAbsolute(sample, _peaks.FrameCount);
            return true;
        }

        return TryGetProgressFromX(mouseX, out progress);
    }

    private void SeekToSample(long sampleOffset)
    {
        if (_peaks is null || _peaks.FrameCount <= 0)
        {
            return;
        }

        var progress = SampleToAbsolute(sampleOffset, _peaks.FrameCount);
        _lastMouseSeekProgress = progress;
        SeekRequested?.Invoke(this, progress);
    }

    private bool TryGetLiveMouseGdiX(out int mouseX)
    {
        mouseX = 0;
        if (!GetCursorPos(out var screen))
        {
            return false;
        }

        // PointFromScreen は画面のデバイス px（GetCursorPos と同じ）→ 要素ローカル DIP。
        // TransformFromDevice を先に掛けると DPI が二重になり、ガイドが左へ寄る。
        var local = PointFromScreen(new System.Windows.Point(screen.X, screen.Y));
        mouseX = ToGdiPoint(local).X;
        return true;
    }

    private void ApplyMouseGuideOverlay()
    {
        if (_mouseGuideX is not float mx || _peaks is null || _peaks.IsEmpty)
        {
            _mouseGuideLine.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        var wave = GetWaveformContentRect();
        if (wave.Width <= 0 || wave.Height <= 0)
        {
            _mouseGuideLine.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        var scale = DpiScale;
        var x = Math.Round(mx) / scale;
        _mouseGuideLine.Stroke = MgaWwiseIMImporter.UI.UiColors.Brush(
            MgaWwiseIMImporter.UI.UiColors.MouseGuide);
        _mouseGuideLine.X1 = x;
        _mouseGuideLine.X2 = x;
        _mouseGuideLine.Y1 = wave.Top / scale;
        _mouseGuideLine.Y2 = wave.Bottom / scale;
        _mouseGuideLine.Visibility = System.Windows.Visibility.Visible;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeScreenPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativeScreenPoint lpPoint);

    private bool IsPlayheadTrailAnimating()
    {
        if (_trailActive || _exitTrailActive || _anacrusisTrailActive || _fadeOutTrailActive)
        {
            return true;
        }

        foreach (var overlay in _overlayPlayheads)
        {
            if (overlay.TrailActive)
            {
                return true;
            }
        }

        foreach (var overlay in _overlayExitPlayheads)
        {
            if (overlay.TrailActive)
            {
                return true;
            }
        }

        foreach (var overlay in _overlayFadeOutPlayheads)
        {
            if (overlay.TrailActive)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateTimelineTip(Point? mouseLocation)
    {
        string? text = null;
        if (_peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0)
        {
            if (mouseLocation is not null)
            {
                text = UiStrings.TipWaveformDropZone;
            }
        }
        else if (mouseLocation is { } sourceLocation
            && _sourceNameEditable
            && IsSourceNamePoint(sourceLocation))
        {
            text = UiStrings.TipWaveformEditSourceName;
        }
        else if (mouseLocation is { } fadeLocation
            && !_isDraggingSeek
            && !_isDraggingMarker
            && !_isDraggingFadeHandle
            && _markerEditMode is null
            && (TryHitFadeHandle(fadeLocation, out _) || TryHitFadeArea(fadeLocation, out _))
            && GetTimelineContentRect().Contains(fadeLocation))
        {
            text = UiStrings.TipWaveformRegionFadeHandle;
        }
        else if (mouseLocation is { } markerLaneLocation
            && !_isDraggingSeek
            && !_isDraggingMarker
            && !_isDraggingFadeHandle
            && _markerEditMode is null
            && TryGetMarkerLane(out var markerLane, out _)
            && markerLane.Contains(markerLaneLocation)
            && _allowsSessionMarkerEdit)
        {
            text = UiStrings.TipWaveformMarkerLaneSessionEdit;
        }
        else if (mouseLocation is { } zoomLocation
            && !_isDraggingSeek
            && !_isDraggingMarker
            && !_isDraggingFadeHandle
            && _markerEditMode is null
            && _outputParts.Count > 0
            && GetTimelineContentRect().Contains(zoomLocation))
        {
            if (TryGetMarkerLane(out var addMarkerLane, out _)
                && addMarkerLane.Contains(zoomLocation))
            {
                text = UiStrings.TipWaveformMarkerLane
                    + Environment.NewLine
                    + UiStrings.TipWaveformCommonKeys;
            }
            else
            {
                text = (CountPlaylistsIntersectingView() == 1
                        ? UiStrings.TipWaveformZoomFitAll
                        : UiStrings.TipWaveformZoomPlaylist)
                    + Environment.NewLine
                    + UiStrings.TipWaveformCommonKeys;
            }
        }

        if (string.Equals(_timelineTipText, text, StringComparison.Ordinal))
        {
            return;
        }

        _timelineTipText = text;
        if (string.IsNullOrEmpty(text))
        {
            TipService.Clear(this);
        }
        else
        {
            TipService.Show(text, this);
        }
    }

    /// <summary>表示言語切替後に Tips 文言を付け直す。</summary>
    public void RefreshLocalizedTips()
    {
        _timelineTipText = null;
        var client = ToGdiPoint(Mouse.GetPosition(this));
        UpdateTimelineTip(ClientRectangle.Contains(client) ? client : null);
    }

    private void UpdateHoveredPlaylistPart(int mouseX)
    {
        if (_peaks is null
            || _peaks.IsEmpty
            || _peaks.FrameCount <= 0
            || !TryGetProgressFromX(mouseX, out var progress))
        {
            SetHoveredPlaylistPart(null);
            return;
        }

        var frameCount = _peaks.FrameCount;
        var sample = (long)Math.Clamp(
            Math.Floor(progress * frameCount),
            0d,
            Math.Max(0L, frameCount - 1));
        var partNumber = _outputParts
            .Where(p => sample >= p.StartSampleOffset && sample < p.EndSampleOffset)
            .Select(p => (int?)p.Number)
            .FirstOrDefault();
        SetHoveredPlaylistPart(partNumber);
    }

    private void SetHoveredPlaylistPart(int? partNumber)
    {
        if (_hoveredPlaylistPartNumber == partNumber)
        {
            return;
        }

        _hoveredPlaylistPartNumber = partNumber;
        PlaylistHoverChanged?.Invoke(this, partNumber);
    }

    private bool TryGetProgressFromX(int mouseX, out double progress)
    {
        progress = 0;
        if (_peaks is null || _peaks.IsEmpty)
        {
            return false;
        }

        var timeline = GetTimelineContentRect();
        if (timeline.Width <= 0 || mouseX < timeline.Left)
        {
            return false;
        }

        var local = Math.Clamp((mouseX - timeline.Left) / (double)timeline.Width, 0d, 1d);
        progress = Math.Clamp(_viewStart + local * ViewSpan, 0d, 1d);
        return true;
    }

}
