using System.Drawing;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TextAlignment = System.Windows.TextAlignment;
using WpfColor = System.Windows.Media.Color;

namespace MgaWwiseIMImporter.UI;

internal sealed partial class WaveformView : System.Windows.FrameworkElement
{
    // WinForms WaveformView と同じデバイス px 定数（描画ビットマップもデバイス px）。
    private const int LabelWaveGapPx = 3;
    private const int LabelRowCount = 4;
    private const int InfoLanePadX = 8;
    private const int InfoLaneSeparatorPx = 3;
    private const int SourceMeterWidthPx = 12;
    private const int SourceMeterGapPx = 8;
    private const int ContentPadPx = 4;
    private const float RegionEdgeGlyphHalfW = 18f;
    private const float NameLaneFontMinPx = 8f;
    private const float NameLaneFontScale = 0.16f;
    private static IReadOnlyList<string> InfoRowLabels => UiStrings.WaveformInfoRowLabels;

    // MGA-CineAudio-Reviewer (transport-timeline.js) と同じ残光パラメータ
    /// <summary>軌跡の目標長さ（画面ピクセル）。ズームによらず見た目を揃える。</summary>
    private const float TrailTargetLengthPx = 360f;
    /// <summary>サンプル保持の上限（壁時計）。描画フェードはズームで短くなり得る。</summary>
    private const int TrailSampleRetainMs = 10400;
    private const float TrailPeakAlpha = 0.15f;
    private const float TrailPlayheadGapPx = 2f;
    private const double TrailMinSecDelta = 0.02;
    private const int TrailSampleMinIntervalMs = 24;
    private const int TrailMaxSamples = 900;
    private const double TrailDiscontinuitySec = 1.25;

    private WavPeakData? _peaks;
    private WavFileInfo? _wavInfo;
    private string _sourcePath = string.Empty;
    private IReadOnlyList<WaveformSourceSpan> _sourceSpans = [];
    private ExpectedWaveformFormat _expectedWaveformFormat = ExpectedWaveformFormat.Default;
    private string _sourceDisplayName = string.Empty;
    private bool _sourceNameEditable = true;
    private TextBox? _sourceNameEditor;
    private bool _sourceNameHovered;
    private bool _endingSourceNameEdit;
    private TextBox? _markerCommentEditor;
    private bool _endingMarkerCommentEdit;
    private readonly List<System.Windows.UIElement> _visualChildren = [];
    private readonly Dictionary<System.Windows.UIElement, System.Windows.Rect> _childArrangeRects = [];
    private long? _markerCommentEditSampleOffset;
    private long? _selectedMarkerSampleOffset;
    private bool _allowsSessionMarkerEdit;
    private bool _isDraggingMarker;
    private long _markerDragFromSample;
    private long? _markerDragPreviewSample;
    private int _markerDragStartX;
    private bool _markerDragMoved;
    private readonly List<MarkerHitRegion> _markerHitRegions = [];
    private IReadOnlyList<RegionEdgeFade> _regionEdgeFades = [];
    private readonly List<FadeHandleHitRegion> _fadeHandleHitRegions = [];
    private readonly List<FadeAreaHitRegion> _fadeAreaHitRegions = [];
    private ContextMenu? _fadeCurveMenu;

    /// <summary>新規リージョン端フェードの既定 Fade In カーブ（歯車メニューのアプリ設定）。</summary>
    public RegionFadeCurveKind DefaultFadeInCurve { get; set; } =
        RegionEdgeFade.BuiltinWaveformFadeInCurve;

    /// <summary>新規リージョン端フェードの既定 Fade Out カーブ（歯車メニューのアプリ設定）。</summary>
    public RegionFadeCurveKind DefaultFadeOutCurve { get; set; } =
        RegionEdgeFade.BuiltinWaveformFadeOutCurve;

    private bool _isDraggingFadeHandle;
    private bool _fadeDragIsIn;
    private long _fadeDragInSample;
    private long _fadeDragOutSample;
    private long? _fadeDragOtherHandleSample;
    private RegionEdgeFade? _fadeDragPreview;
    private int _fadeDragStartX;
    private bool _fadeDragMoved;
    private int _infoLaneWidth = 120;
    private float _outputLevel;
    private WavPeakData? _detailPeaks;
    private double _detailViewStart = double.NaN;
    private double _detailViewEnd = double.NaN;
    private int _detailPixelWidth = -1;
    private bool _detailIsApproximate;
    private WavPeakPyramid? _peakPyramid;
    private int _pyramidGeneration;
    private (double ViewStart, double ViewEnd, int Width)? _rawDetailWanted;
    private bool _rawDetailReading;
    private IReadOnlyList<WaveformBarMark> _bars = [];
    private IReadOnlyList<WaveformMarkerMark> _markers = [];
    private IReadOnlyList<WaveformRegionMark> _regions = [];
    private IReadOnlyList<WaveformOutputPart> _outputParts = [];
    private IReadOnlyDictionary<int, string> _playlistDisplayNames =
        new Dictionary<int, string>();
    private IReadOnlyDictionary<int, int> _playlistPartGroupIds =
        new Dictionary<int, int>();
    private IReadOnlyDictionary<int, WpfColor> _playlistGroupColors =
        new Dictionary<int, WpfColor>();
    private HashSet<int> _disabledPlaylistPartNumbers = [];
    private IReadOnlyList<WaveformSegmentNameMark> _segmentNames = [];
    private int? _hoveredPlaylistPartNumber;
    private int? _playlistHoverHighlightPartNumber;
    private int? _exportHighlightPartNumber;
    private readonly DispatcherTimer _exportGlowTimer;
    private string? _timelineTipText;

    // 時間軸ズーム（1=全体表示。既定より縮小しない）
    private const double TimeZoomMin = 1.0;
    private const double TimeZoomMax = 81920.0;
    // キーボード: ? 2^(1/8)。ホイールは少し大きめ ? 2^(1/4)
    private const double TimeZoomStep = 1.09050773267;
    private const double TimeZoomWheelStep = 1.189207115;
    private double _timeZoom = TimeZoomMin;
    private double _viewStart; // 表示左端の絶対進捗 0..1

    // 振幅ズーム（1=既定。既定より縮小しない）
    private const double AmpZoomMin = 1.0;
    private const double AmpZoomMax = 128.0;
    private const double AmpZoomStep = 1.09050773267;
    private const double AmpZoomWheelStep = 1.189207115;
    /// <summary>
    /// 1px あたりこのサンプル数以下なら縦棒ではなくサンプル折れ線にする。
    /// キーボード拡縮（<see cref="TimeZoomStep"/>）おおよそ 6 段階手前まで詳細表示する狙い
    /// （基準 4 × TimeZoomStep^6 ≒ 6.7、運用値は一段手前の 8）。
    /// </summary>
    private const int PolylineMaxSamplesPerPixel = 8;
    private double _ampZoom = AmpZoomMin;

    private double? _playheadProgress;
    private readonly List<(double Progress, long TickMs)> _trailSamples = [];
    private bool _trailActive;
    private readonly List<OverlayPlayheadState> _overlayPlayheads = [];
    private readonly List<OverlayPlayheadState> _overlayExitPlayheads = [];
    private readonly List<OverlayPlayheadState> _overlayFadeOutPlayheads = [];
    private double? _exitPlayheadProgress;
    private readonly List<(double Progress, long TickMs)> _exitTrailSamples = [];
    private bool _exitTrailActive;
    private double? _anacrusisPlayheadProgress;
    private readonly List<(double Progress, long TickMs)> _anacrusisTrailSamples = [];
    private bool _anacrusisTrailActive;
    private double? _fadeOutPlayheadProgress;
    private readonly List<(double Progress, long TickMs)> _fadeOutTrailSamples = [];
    private bool _fadeOutTrailActive;
    private bool _fadeOutPlayheadIsExit;
    private bool _isDraggingSeek;
    private int _seekDragStartX;
    private bool _seekMovedDuringDrag;
    private double _lastMouseSeekProgress = double.NaN;
    private MarkerEditMode? _markerEditMode;
    private int _markerStrokeLastX;
    private float? _mouseGuideX;
    private Bitmap? _staticLayer;
    private bool _staticLayerDirty = true;
    private int _presentationSuspendCount;
    private bool _holdScaffold;
    private bool _staticRebuildQueued;
    /// <summary>時間軸ズーム／パンの静的レイヤ再構築を 1 フレームにまとめる。</summary>
    private bool _timeViewRebuildQueued;

    private Bitmap? _frameBitmap;
    private Bitmap? _measureBitmap;
    private WriteableBitmap? _presentationBitmap;
    private bool _disposed;

    public static readonly System.Windows.DependencyProperty BackgroundProperty =
        Control.BackgroundProperty.AddOwner(
            typeof(WaveformView),
            new System.Windows.FrameworkPropertyMetadata(
                MgaWwiseIMImporter.UI.UiColors.Brush(
                    MgaWwiseIMImporter.UI.UiColors.ForControlBack(MgaWwiseIMImporter.UI.UiColors.WaveformBack)),
                System.Windows.FrameworkPropertyMetadataOptions.AffectsRender));

    public System.Windows.Media.Brush? Background
    {
        get => (System.Windows.Media.Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// GDI+ 描画フォント。WinForms Control.Font（"Yu Gothic UI" 8.5pt）と同じ。
    /// デバイス DPI ビットマップ上では GetHeight もデバイス px になるため、
    /// ホスト高は <see cref="DesignMetrics.WaveformHostHeight"/> で Form1 AutoScale に合わせる。
    /// </summary>
    private Font Font { get; set; } = new("Yu Gothic UI", 8.5F);

    public WaveformView()
    {
        // ホスト（waveformHostPanel）の * 行に追従させる。固定 Height だと Z 倍率時に下側が黒抜けする。
        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
        // デバイス px ビットマップを 1:1 で貼る（既定の Fant 拡大だと文字が滲む）
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(
            this, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
        System.Windows.Media.RenderOptions.SetEdgeMode(this, System.Windows.Media.EdgeMode.Aliased);
        TabStop = false;
        Cursor = null;
        Focusable = true;
        _exportGlowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _exportGlowTimer.Tick += (_, _) => Invalidate();
        UiStrings.LanguageChanged += (_, _) =>
        {
            if (!IsDisposed)
            {
                Invalidate();
            }
        };
        Unloaded += OnUnloaded;
    }

    protected override void OnDpiChanged(
        System.Windows.DpiScale oldDpi,
        System.Windows.DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        DisposeStaticLayer();
        _measureBitmap?.Dispose();
        _measureBitmap = null;
        _frameBitmap?.Dispose();
        _frameBitmap = null;
        _presentationBitmap = null;
        Invalidate();
    }

    private void OnUnloaded(object? sender, System.Windows.RoutedEventArgs e)
    {
        _exportGlowTimer.Stop();
        _fadeCurveMenu = null;
        DisposeStaticLayer();
        _frameBitmap?.Dispose();
        _frameBitmap = null;
        _measureBitmap?.Dispose();
        _measureBitmap = null;
        _presentationBitmap = null;
        _disposed = true;
    }

    // --- WinForms 由来 API の薄いシム（挙動はそのまま、名前だけ揃える） ---

    private void Invalidate() => InvalidateVisual();

    private void Update()
    {
        if (Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }
    }

    private bool Capture
    {
        get => IsMouseCaptured;
        set
        {
            if (value)
            {
                CaptureMouse();
            }
            else
            {
                ReleaseMouseCapture();
            }
        }
    }

    private bool TabStop
    {
        get => Focusable;
        set => Focusable = value;
    }

    private bool CanFocus => Focusable;

    private bool IsDisposed => _disposed;

    private bool IsHandleCreated => IsLoaded;

    /// <summary>DIP → デバイス px。WinForms ClientSize と同じ単位でレイアウトする。</summary>
    private double DpiScale
    {
        get
        {
            var scale = System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip;
            return scale > 0.01 ? scale : 1d;
        }
    }

    private float DeviceDpi => (float)(96d * DpiScale);

    /// <summary>デバイス px のクライアント矩形（WinForms ClientRectangle 相当）。</summary>
    private Rectangle ClientRectangle
    {
        get
        {
            var scale = DpiScale;
            return new(
                0,
                0,
                Math.Max(0, (int)Math.Round(ActualWidth * scale)),
                Math.Max(0, (int)Math.Round(ActualHeight * scale)));
        }
    }

    private Rectangle ContentBounds =>
        Rectangle.Inflate(ClientRectangle, -ContentPadPx, -ContentPadPx);

    private static Rectangle ContentBoundsOf(Rectangle bounds, int pad) =>
        Rectangle.Inflate(bounds, -pad, -pad);

    private Size ClientSize => ClientRectangle.Size;

    private void BeginInvoke(Action action) => Dispatcher.BeginInvoke(action);

    private static System.Windows.Input.ModifierKeys ModifierKeys => Keyboard.Modifiers;

    private Graphics CreateMeasureGraphics()
    {
        var dpi = DeviceDpi;
        if (_measureBitmap is null
            || Math.Abs(_measureBitmap.HorizontalResolution - dpi) > 0.1f)
        {
            _measureBitmap?.Dispose();
            _measureBitmap = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            _measureBitmap.SetResolution(dpi, dpi);
        }

        return Graphics.FromImage(_measureBitmap);
    }

    private Point ToGdiPoint(System.Windows.Point p)
    {
        var scale = DpiScale;
        return new((int)Math.Round(p.X * scale), (int)Math.Round(p.Y * scale));
    }

    private System.Windows.Point ToWpfPoint(Point p)
    {
        var scale = DpiScale;
        return new(p.X / scale, p.Y / scale);
    }

    private static double GdiPointsToWpfFontSize(float gdiPoints) => AppFonts.DipFromPoints(gdiPoints);

    /// <summary>Digits などの他のダーク系インライン入力欄と揃えた見た目の TextBox を作る。</summary>
    private TextBox CreateDarkInlineEditor(bool bold, TextAlignment alignment)
    {
        return new TextBox
        {
            Background = MgaWwiseIMImporter.UI.UiColors.Brush(
                MgaWwiseIMImporter.UI.UiColors.ForControlBack(MgaWwiseIMImporter.UI.UiColors.DialogInputBack)),
            Foreground = MgaWwiseIMImporter.UI.UiColors.Brush(MgaWwiseIMImporter.UI.UiColors.DialogFore),
            BorderBrush = MgaWwiseIMImporter.UI.UiColors.Brush(MgaWwiseIMImporter.UI.UiColors.ChromeMid),
            BorderThickness = new System.Windows.Thickness(1),
            FontFamily = new System.Windows.Media.FontFamily(Font.FontFamily.Name),
            FontWeight = bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal,
            FontSize = GdiPointsToWpfFontSize(Font.SizeInPoints),
            TextAlignment = alignment,
            Padding = new System.Windows.Thickness(2),
            Visibility = System.Windows.Visibility.Collapsed,
        };
    }

    private void RegisterVisualChild(System.Windows.UIElement child)
    {
        _visualChildren.Add(child);
        AddVisualChild(child);
        AddLogicalChild(child);
    }

    private void SetEditorBounds(System.Windows.UIElement editor, Rectangle bounds)
    {
        // bounds はデバイス px。WPF 子要素の Arrange は DIP。
        var scale = DpiScale;
        _childArrangeRects[editor] = new System.Windows.Rect(
            bounds.Left / scale,
            bounds.Top / scale,
            Math.Max(0, bounds.Width / scale),
            Math.Max(0, bounds.Height / scale));
        InvalidateArrange();
    }

    protected override int VisualChildrenCount => _visualChildren.Count;

    protected override System.Windows.Media.Visual GetVisualChild(int index) => _visualChildren[index];

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        foreach (var child in _visualChildren)
        {
            child.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        return availableSize;
    }

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        foreach (var child in _visualChildren)
        {
            var rect = _childArrangeRects.TryGetValue(child, out var r)
                ? r
                : new System.Windows.Rect(0, 0, 0, 0);
            child.Arrange(rect);
        }

        return finalSize;
    }

    public void SetPreview(
        WavPeakData peaks,
        string sourcePath,
        WavFileInfo? wavInfo = null,
        IReadOnlyList<WaveformBarMark>? bars = null,
        IReadOnlyList<WaveformMarkerMark>? markers = null,
        IReadOnlyList<WaveformRegionMark>? regions = null,
        IReadOnlyList<WaveformOutputPart>? outputParts = null,
        bool allowsSessionMarkerEdit = false,
        IReadOnlyList<WaveformSourceSpan>? sourceSpans = null,
        bool sourceNameEditable = true)
    {
        _peaks = peaks;
        _wavInfo = wavInfo;
        _sourcePath = sourcePath ?? string.Empty;
        _sourceSpans = sourceSpans ?? [];
        _sourceNameEditable = sourceNameEditable;
        _sourceDisplayName = string.IsNullOrWhiteSpace(sourcePath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(sourcePath);
        _outputLevel = 0f;
        ClearDetailPeaks();
        if (_sourceSpans.Count > 1)
        {
            // 仮想タイムライン用のピーク階層を背景構築（ズーム時の概要?精密ちらつきを防ぐ）
            StartPeakPyramidBuildFromSpans(_sourceSpans);
        }
        else
        {
            StartPeakPyramidBuild(wavInfo);
        }
        _bars = bars ?? [];
        _markers = markers ?? [];
        _regions = regions ?? [];
        _outputParts = outputParts ?? [];
        _allowsSessionMarkerEdit = allowsSessionMarkerEdit;
        EndSourceNameEdit(commit: false);
        ClearMarkerSessionEditState();
        ClearFadeDragState();
        _regionEdgeFades = [];
        TabStop = allowsSessionMarkerEdit;
        _playlistDisplayNames = new Dictionary<int, string>();
        _playlistPartGroupIds = new Dictionary<int, int>();
        _playlistGroupColors = new Dictionary<int, WpfColor>();
        SetHoveredPlaylistPart(null);
        SetPlaylistHoverHighlight(null);
        SetSourceNameHovered(false);
        RebuildSegmentNameMarks();
        ResetTimeZoom(refresh: false);
        ResetAmpZoom(refresh: false);
        ClearExportHighlight();
        ClearPlayhead();
        // 旧波形でのドラッグ／ホバー／Tip 状態を新しい波形へ持ち越さない。
        _isDraggingSeek = false;
        _seekMovedDuringDrag = false;
        _lastMouseSeekProgress = double.NaN;
        ClearMarkerDragState();
        _markerEditMode = null;
        Capture = false;
        UpdateTimelineTip(null);
        _mouseGuideX = null;
        Cursor = null;

        // 重いレイヤ生成の前にダークな足場だけ先に出す（白フラッシュ防止）
        DisposeStaticLayer();
        _holdScaffold = true;
        Invalidate();
        Update();

        var bounds = ClientRectangle;
        if (bounds.Width > 2 && bounds.Height > 2)
        {
            BuildStaticLayer(bounds);
        }

        _holdScaffold = false;
        Invalidate();
        TimeViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>波形フォーマット規定を設定し、Playlist 左下の表示色を更新する。</summary>
    public void SetExpectedWaveformFormat(ExpectedWaveformFormat format)
    {
        var normalized = ExpectedWaveformFormat.Normalize(
            (int)format.SampleRateHz,
            format.BitsPerSample,
            format.Channels);
        if (_expectedWaveformFormat.Equals(normalized))
        {
            return;
        }

        _expectedWaveformFormat = normalized;
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    public void SetMarkers(IReadOnlyList<WaveformMarkerMark> markers)
    {
        _markers = markers;
        if (_selectedMarkerSampleOffset is { } selected
            && !_markers.Any(marker => marker.SampleOffset == selected))
        {
            _selectedMarkerSampleOffset = null;
        }

        if (_markerCommentEditSampleOffset is { } editing
            && !_markers.Any(marker => marker.SampleOffset == editing))
        {
            EndMarkerCommentEdit(commit: false);
        }

        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    public void SetRegions(IReadOnlyList<WaveformRegionMark> regions)
    {
        _regions = regions ?? [];
        _regionEdgeFades = RegionEdgeFade.RemapToRuns(_regionEdgeFades, _regions);
        RebuildSegmentNameMarks();
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    /// <summary>連続リージョン固まりの端フェードを差し替える。</summary>
    public void SetRegionEdgeFades(IReadOnlyList<RegionEdgeFade> fades)
    {
        _regionEdgeFades = RegionEdgeFade.RemapToRuns(fades ?? [], _regions);
        if (!_isDraggingFadeHandle)
        {
            _fadeDragPreview = null;
        }

        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    public void SetOutputParts(IReadOnlyList<WaveformOutputPart> outputParts)
    {
        _outputParts = outputParts ?? [];
        RebuildSegmentNameMarks();
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    public void SetSourceDisplayName(string name)
    {
        var next = name.Trim();
        if (string.Equals(_sourceDisplayName, next, StringComparison.Ordinal))
        {
            return;
        }

        _sourceDisplayName = next;
        EndSourceNameEdit(commit: false);
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    public void SetPlaylistDisplayNames(
        IReadOnlyDictionary<int, string> names,
        IReadOnlyDictionary<int, int>? partGroupIds = null,
        IReadOnlyDictionary<int, WpfColor>? partGroupColors = null)
    {
        _playlistDisplayNames = new Dictionary<int, string>(names);
        _playlistPartGroupIds = partGroupIds is null
            ? new Dictionary<int, int>()
            : new Dictionary<int, int>(partGroupIds);
        _playlistGroupColors = partGroupColors is null
            ? new Dictionary<int, WpfColor>()
            : new Dictionary<int, WpfColor>(partGroupColors);
        RebuildSegmentNameMarks();
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    /// <summary>
    /// グループ帯の色だけを更新する（ドラッグ塗り中の軽量更新用。レイヤ再生成はしない）。
    /// </summary>
    public void SetPlaylistGroupColors(IReadOnlyDictionary<int, WpfColor> partGroupColors)
    {
        _playlistGroupColors = new Dictionary<int, WpfColor>(partGroupColors);
        InvalidateVisual();
    }

    /// <summary>
    /// 無効化した Playlist パート番号。上下レーン含め約 25% 不透明度に見せる。
    /// </summary>
    public void SetDisabledPlaylistParts(IEnumerable<int> partNumbers)
    {
        var next = partNumbers.ToHashSet();
        if (_disabledPlaylistPartNumbers.SetEquals(next))
        {
            return;
        }

        _disabledPlaylistPartNumbers = next;
        RebuildSegmentNameMarks();
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    private void RebuildSegmentNameMarks()
    {
        var enabledParts = _outputParts
            .Where(part => !_disabledPlaylistPartNumbers.Contains(part.Number))
            .ToArray();
        _segmentNames = enabledParts.Length == 0 || string.IsNullOrEmpty(_sourcePath)
            ? []
            : WwiseMusicPlanBuilder.BuildSegmentLabelMarks(
                _sourcePath,
                enabledParts,
                _regions,
                _playlistPartGroupIds,
                _playlistDisplayNames);
    }

    /// <summary>
    /// ファイル解析など重い処理の直前に呼び、現状の暗い描画を画面へ確定する。
    /// </summary>
    public void CommitDarkFrame()
    {
        Invalidate();
        Update();
    }

    /// <summary>
    /// UiColors 変更後に背景・静的レイヤを作り直す。
    /// </summary>
    public void RefreshAppearance()
    {
        DisposeStaticLayer();

        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        var bounds = ClientRectangle;
        if (bounds.Width <= 2 || bounds.Height <= 2)
        {
            Invalidate();
            return;
        }

        if (_peaks is not null && !_peaks.IsEmpty)
        {
            BuildStaticLayer(bounds);
        }

        Invalidate();
    }

    public void ClearPreview()
    {
        _holdScaffold = false;
        _peaks = null;
        _wavInfo = null;
        _sourcePath = string.Empty;
        _sourceSpans = [];
        _sourceDisplayName = string.Empty;
        _sourceNameEditable = true;
        EndSourceNameEdit(commit: false);
        ClearMarkerSessionEditState();
        _outputLevel = 0f;
        ClearDetailPeaks();
        _peakPyramid = null;
        _pyramidGeneration++;
        _bars = [];
        _markers = [];
        _regions = [];
        _outputParts = [];
        _regionEdgeFades = [];
        ClearFadeDragState();
        _allowsSessionMarkerEdit = false;
        TabStop = false;
        _playlistDisplayNames = new Dictionary<int, string>();
        _playlistPartGroupIds = new Dictionary<int, int>();
        _playlistGroupColors = new Dictionary<int, WpfColor>();
        _disabledPlaylistPartNumbers = [];
        UpdateTimelineTip(null);
        SetHoveredPlaylistPart(null);
        SetPlaylistHoverHighlight(null);
        SetSourceNameHovered(false);
        _segmentNames = [];
        ResetTimeZoom(refresh: false);
        ResetAmpZoom(refresh: false);
        ClearExportHighlight();
        _isDraggingSeek = false;
        _seekMovedDuringDrag = false;
        _lastMouseSeekProgress = double.NaN;
        ClearMarkerDragState();
        _markerEditMode = null;
        Capture = false;
        _mouseGuideX = null;
        ClearPlayhead();
        Cursor = null;
        DisposeStaticLayer();
        Invalidate();
        Update();
        TimeViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 再生位置を更新する。progress は 0?1。null で非表示。
    /// recordTrail が false のとき残光を消す（停止時など）。
    /// recordTrail が true（再生中）のときは、ズーム表示で画面外へ出たらページめくり追従する。
    /// ensureVisible が true のときは停止中でも表示窓を追従させる。
    /// </summary>
    public void SetPlayhead(
        double? progress,
        bool recordTrail = false,
        bool ensureVisible = false)
    {
        if (progress is null)
        {
            ClearPlayhead();
            Invalidate();
            return;
        }

        var clamped = Math.Clamp(progress.Value, 0d, 1d);
        _playheadProgress = clamped;
        _trailActive = recordTrail;
        if (!recordTrail)
        {
            ClearTrailSamples();
        }
        else
        {
            RecordTrailSample(clamped, _trailSamples, ref _trailActive);
            FollowPlayheadPaged(clamped);
        }

        if (ensureVisible)
        {
            EnsureAbsoluteVisible(clamped);
        }

        Invalidate();
    }

    /// <summary>
    /// グループ重ね再生用の追加シアンシークバー。見た目は主シークバーと同一。
    /// </summary>
    public void SetOverlayPlayheads(
        IReadOnlyList<double> progresses,
        bool recordTrail = false)
    {
        if (progresses.Count == 0)
        {
            if (_overlayPlayheads.Count == 0)
            {
                return;
            }

            _overlayPlayheads.Clear();
            Invalidate();
            return;
        }

        while (_overlayPlayheads.Count < progresses.Count)
        {
            _overlayPlayheads.Add(new OverlayPlayheadState());
        }

        while (_overlayPlayheads.Count > progresses.Count)
        {
            _overlayPlayheads.RemoveAt(_overlayPlayheads.Count - 1);
        }

        for (var i = 0; i < progresses.Count; i++)
        {
            var clamped = Math.Clamp(progresses[i], 0d, 1d);
            var state = _overlayPlayheads[i];
            state.Progress = clamped;
            state.TrailActive = recordTrail;
            if (!recordTrail)
            {
                state.TrailSamples.Clear();
                state.TrailActive = false;
            }
            else
            {
                var trailActive = state.TrailActive;
                RecordTrailSample(clamped, state.TrailSamples, ref trailActive);
                state.TrailActive = trailActive;
            }
        }

        Invalidate();
    }

    /// <summary>
    /// グループ重ね再生の Group Fade Out ヘッド（白）。見た目は遷移元フェードアウトと同じ。
    /// </summary>
    public void SetOverlayFadeOutPlayheads(
        IReadOnlyList<double> progresses,
        bool recordTrail = false)
    {
        if (progresses.Count == 0)
        {
            if (_overlayFadeOutPlayheads.Count == 0)
            {
                return;
            }

            _overlayFadeOutPlayheads.Clear();
            Invalidate();
            return;
        }

        while (_overlayFadeOutPlayheads.Count < progresses.Count)
        {
            _overlayFadeOutPlayheads.Add(new OverlayPlayheadState());
        }

        while (_overlayFadeOutPlayheads.Count > progresses.Count)
        {
            _overlayFadeOutPlayheads.RemoveAt(_overlayFadeOutPlayheads.Count - 1);
        }

        for (var i = 0; i < progresses.Count; i++)
        {
            var clamped = Math.Clamp(progresses[i], 0d, 1d);
            var state = _overlayFadeOutPlayheads[i];
            state.Progress = clamped;
            state.TrailActive = recordTrail;
            if (!recordTrail)
            {
                state.TrailSamples.Clear();
                state.TrailActive = false;
            }
            else
            {
                var trailActive = state.TrailActive;
                RecordTrailSample(clamped, state.TrailSamples, ref trailActive);
                state.TrailActive = trailActive;
            }
        }

        Invalidate();
    }

    /// <summary>
    /// グループ重ね再生の -E 二重再生ヘッド（赤）。見た目は主 Exit シークバーと同一。
    /// </summary>
    public void SetOverlayExitPlayheads(
        IReadOnlyList<double> progresses,
        bool recordTrail = false)
    {
        if (progresses.Count == 0)
        {
            if (_overlayExitPlayheads.Count == 0)
            {
                return;
            }

            _overlayExitPlayheads.Clear();
            Invalidate();
            return;
        }

        while (_overlayExitPlayheads.Count < progresses.Count)
        {
            _overlayExitPlayheads.Add(new OverlayPlayheadState());
        }

        while (_overlayExitPlayheads.Count > progresses.Count)
        {
            _overlayExitPlayheads.RemoveAt(_overlayExitPlayheads.Count - 1);
        }

        for (var i = 0; i < progresses.Count; i++)
        {
            var clamped = Math.Clamp(progresses[i], 0d, 1d);
            var state = _overlayExitPlayheads[i];
            state.Progress = clamped;
            state.TrailActive = recordTrail;
            if (!recordTrail)
            {
                state.TrailSamples.Clear();
                state.TrailActive = false;
            }
            else
            {
                var trailActive = state.TrailActive;
                RecordTrailSample(clamped, state.TrailSamples, ref trailActive);
                state.TrailActive = trailActive;
            }
        }

        Invalidate();
    }

    public void ClearPlayheadTrail()
    {
        ClearTrailSamples();
        Invalidate();
    }

    public void SetOutputLevel(float level, bool decay)
    {
        var target = Math.Clamp(level, 0f, 1f);
        var next = decay
            ? Math.Max(target, _outputLevel * 0.92f)
            : target;
        if (Math.Abs(next - _outputLevel) < 0.001f)
        {
            return;
        }

        _outputLevel = next;
        Invalidate();
    }

    /// <summary>
    /// -E 二重再生ヘッド（赤）。progress は 0?1。null で非表示。
    /// </summary>
    public void SetExitPlayhead(double? progress, bool recordTrail = false)
    {
        if (progress is null)
        {
            ClearExitPlayhead();
            Invalidate();
            return;
        }

        var clamped = Math.Clamp(progress.Value, 0d, 1d);
        _exitPlayheadProgress = clamped;
        _exitTrailActive = recordTrail;
        if (!recordTrail)
        {
            _exitTrailSamples.Clear();
        }
        else
        {
            RecordTrailSample(clamped, _exitTrailSamples, ref _exitTrailActive);
        }

        Invalidate();
    }

    /// <summary>
    /// -A 先行再生ヘッド（緑）。progress は 0?1。null で非表示。
    /// </summary>
    public void SetAnacrusisPlayhead(double? progress, bool recordTrail = false)
    {
        if (progress is null)
        {
            ClearAnacrusisPlayhead();
            Invalidate();
            return;
        }

        var clamped = Math.Clamp(progress.Value, 0d, 1d);
        _anacrusisPlayheadProgress = clamped;
        _anacrusisTrailActive = recordTrail;
        if (!recordTrail)
        {
            _anacrusisTrailSamples.Clear();
        }
        else
        {
            RecordTrailSample(
                clamped,
                _anacrusisTrailSamples,
                ref _anacrusisTrailActive);
        }

        Invalidate();
    }

    /// <summary>
    /// Playlist 遷移元のフェードアウトヘッド（グレー）。null で非表示。
    /// </summary>
    public void SetFadeOutPlayhead(
        double? progress,
        bool recordTrail = false,
        bool isExit = false)
    {
        if (progress is null)
        {
            ClearFadeOutPlayhead();
            Invalidate();
            return;
        }

        var clamped = Math.Clamp(progress.Value, 0d, 1d);
        if (_fadeOutPlayheadIsExit != isExit)
        {
            _fadeOutTrailSamples.Clear();
            _fadeOutTrailActive = false;
        }

        _fadeOutPlayheadIsExit = isExit;
        _fadeOutPlayheadProgress = clamped;
        _fadeOutTrailActive = recordTrail;
        if (!recordTrail)
        {
            _fadeOutTrailSamples.Clear();
        }
        else
        {
            RecordTrailSample(
                clamped,
                _fadeOutTrailSamples,
                ref _fadeOutTrailActive);
        }

        Invalidate();
    }

    /// <summary>
    /// 再生ヘッドが表示窓の外へ出たとき、ページ単位で表示窓を進める／戻す。
    /// 右はみ出し: 新ページの左端にプレイヘッド。左はみ出し: 新ページの右端付近に。
    /// </summary>
    private void FollowPlayheadPaged(double progress)
    {
        if (_peaks is null || _peaks.IsEmpty || _timeZoom <= TimeZoomMin + 1e-9)
        {
            return;
        }

        var span = ViewSpan;
        if (span >= 1.0 - 1e-12)
        {
            return;
        }

        var viewEnd = _viewStart + span;
        double newStart;
        if (progress >= viewEnd)
        {
            // ページ送り: はみ出した位置を次ページの左端に
            newStart = progress;
        }
        else if (progress < _viewStart)
        {
            // ページ戻し: プレイヘッドが新ページ右端に来るようずらす
            newStart = progress - span;
        }
        else
        {
            return;
        }

        var previous = _viewStart;
        _viewStart = newStart;
        ClampTimeViewWindow();
        if (Math.Abs(_viewStart - previous) < 1e-12)
        {
            return;
        }

        NotifyTimeViewChanged();
    }

    /// <summary>
    /// 書き出し中の出力パート枠を発光表示する。null で解除。
    /// </summary>
    public void SetExportHighlight(int? partNumber)
    {
        if (_exportHighlightPartNumber == partNumber)
        {
            if (partNumber is not null && !_exportGlowTimer.IsEnabled)
            {
                _exportGlowTimer.Start();
            }

            return;
        }

        _exportHighlightPartNumber = partNumber;
        if (partNumber is null)
        {
            _exportGlowTimer.Stop();
        }
        else if (!_exportGlowTimer.IsEnabled)
        {
            _exportGlowTimer.Start();
        }

        Invalidate();
    }

    public void ClearExportHighlight() => SetExportHighlight(null);

    /// <summary>Playlist 一覧のマウスオーバーに対応する波形範囲枠。null で解除。</summary>
    public void SetPlaylistHoverHighlight(int? partNumber)
    {
        if (_playlistHoverHighlightPartNumber == partNumber)
        {
            return;
        }

        _playlistHoverHighlightPartNumber = partNumber;
        Invalidate();
    }

    /// <summary>時間軸を拡大（既定より縮小しない）。</summary>
    public void ZoomTimeIn() => AdjustTimeZoom(TimeZoomStep, AnchorProgressForKeyboardZoom());

    /// <summary>時間軸を縮小（既定未満にはしない）。</summary>
    public void ZoomTimeOut() => AdjustTimeZoom(1.0 / TimeZoomStep, AnchorProgressForKeyboardZoom());

    /// <summary>時間軸ズームを既定（全体表示）に戻す。</summary>
    public void ResetTimeZoom() => ResetTimeZoom(refresh: true);

    /// <summary>時間軸を最大倍率にする。</summary>
    public void ZoomTimeToMax() => SetTimeZoomAbsolute(TimeZoomMax, AnchorProgressForKeyboardZoom());

    /// <summary>振幅を拡大（既定より縮小しない）。</summary>
    public void ZoomAmpIn() => AdjustAmpZoom(AmpZoomStep);

    /// <summary>振幅を縮小（既定未満にはしない）。</summary>
    public void ZoomAmpOut() => AdjustAmpZoom(1.0 / AmpZoomStep);

    /// <summary>振幅ズームを既定に戻す。</summary>
    public void ResetAmpZoom() => ResetAmpZoom(refresh: true);

    /// <summary>振幅を最大倍率にする。</summary>
    public void ZoomAmpToMax() => SetAmpZoomAbsolute(AmpZoomMax);

    /// <summary>表示窓を波形先頭へ。</summary>
    public void PanTimeToStart()
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        _viewStart = 0;
        NotifyTimeViewChanged();
    }

    /// <summary>表示窓を波形末尾へ。</summary>
    public void PanTimeToEnd()
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        _viewStart = Math.Max(0d, 1.0 - ViewSpan);
        NotifyTimeViewChanged();
    }

    /// <summary>再生位置を直前の小節線へ。成功したら true。</summary>
    public bool SeekToPreviousBar() => TrySeekAlongSamples(CollectBarSamples(), previous: true);

    /// <summary>再生位置を直後の小節線へ。成功したら true。</summary>
    public bool SeekToNextBar() => TrySeekAlongSamples(CollectBarSamples(), previous: false);

    /// <summary>再生位置を現在の表示幅 1 画面分だけ前へ。成功したら true。</summary>
    public bool SeekToPreviousPage() => TrySeekByVisiblePage(previous: true);

    /// <summary>再生位置を現在の表示幅 1 画面分だけ次へ。成功したら true。</summary>
    public bool SeekToNextPage() => TrySeekByVisiblePage(previous: false);

    /// <summary>再生位置を現在の表示幅の約 5% だけ前へ。成功したら true。</summary>
    public bool SeekByVisibleFractionPrevious() => TrySeekByVisibleFraction(-0.05d);

    /// <summary>再生位置を現在の表示幅の約 5% だけ次へ。成功したら true。</summary>
    public bool SeekByVisibleFractionNext() => TrySeekByVisibleFraction(0.05d);

    private bool TrySeekByVisiblePage(bool previous)
    {
        var delta = previous ? -ViewSpan : ViewSpan;
        return TrySeekByAbsoluteDelta(delta);
    }

    private bool TrySeekByVisibleFraction(double fractionOfView)
    {
        if (!double.IsFinite(fractionOfView) || Math.Abs(fractionOfView) < 1e-12)
        {
            return false;
        }

        return TrySeekByAbsoluteDelta(ViewSpan * fractionOfView);
    }

    private bool TrySeekByAbsoluteDelta(double absoluteDelta)
    {
        if (_peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0)
        {
            return false;
        }

        var current = Math.Clamp(_playheadProgress ?? 0d, 0d, 1d);
        var target = Math.Clamp(current + absoluteDelta, 0d, 1d);
        if (Math.Abs(target - current) < 1e-12)
        {
            return false;
        }

        // エンジン着地誤差の前に表示位置を確定し、キーリピートでも画面単位を維持する
        _playheadProgress = target;
        EnsureAbsoluteVisible(target);
        SeekRequested?.Invoke(this, target);
        return true;
    }

    /// <summary>
    /// 相対小節番号（1 始まり）の小節頭へシーク。成功したら true。
    /// </summary>
    public bool TrySeekToBarNumber(int barNumber)
    {
        if (barNumber < 1 || _peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0)
        {
            return false;
        }

        foreach (var bar in _bars)
        {
            if (bar.IsTempoChangeOnly || bar.BarNumber != barNumber)
            {
                continue;
            }

            var frameCount = _peaks.FrameCount;
            var sample = Math.Clamp(bar.SampleOffset, 0L, frameCount);
            var progress = Math.Clamp(sample / (double)frameCount, 0d, 1d);
            _playheadProgress = progress;
            EnsureAbsoluteVisible(progress);
            SeekRequested?.Invoke(this, progress);
            return true;
        }

        return false;
    }

    /// <summary>現在の再生位置に最も近い（直前を含む）相対小節番号。無ければ null。</summary>
    public int? GetNearestBarNumber()
    {
        if (_peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0 || _bars.Count == 0)
        {
            return null;
        }

        var frameCount = _peaks.FrameCount;
        var currentSample = (long)Math.Round((_playheadProgress ?? 0d) * frameCount);
        currentSample = Math.Clamp(currentSample, 0L, frameCount);

        int? best = null;
        long bestSample = long.MinValue;
        foreach (var bar in _bars)
        {
            if (bar.IsTempoChangeOnly || bar.BarNumber < 1)
            {
                continue;
            }

            var sample = Math.Clamp(bar.SampleOffset, 0L, frameCount);
            if (sample <= currentSample && sample >= bestSample)
            {
                bestSample = sample;
                best = bar.BarNumber;
            }
        }

        return best;
    }

    /// <summary>再生位置を直前のマーカーへ。成功したら true。</summary>
    public bool SeekToPreviousMarker() => TrySeekAlongSamples(CollectMarkerSamples(), previous: true);

    /// <summary>再生位置を直後のマーカーへ。成功したら true。</summary>
    public bool SeekToNextMarker() => TrySeekAlongSamples(CollectMarkerSamples(), previous: false);

    /// <summary>
    /// 直前の Music Playlist 先頭またはマーカーへ。
    /// Ctrl+← 用。Playlist 先頭に加え、区間内のマーカーにも止まる。
    /// </summary>
    public bool SeekToPreviousPlaylist() =>
        TrySeekAlongSamples(CollectPlaylistNavigationSamples(), previous: true);

    /// <summary>
    /// 直後の Music Playlist 先頭またはマーカーへ。
    /// Ctrl+→ 用。
    /// </summary>
    public bool SeekToNextPlaylist() =>
        TrySeekAlongSamples(CollectPlaylistNavigationSamples(), previous: false);

    private List<long> CollectBarSamples()
    {
        var result = new List<long>();
        if (_peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0)
        {
            return result;
        }

        var frameCount = _peaks.FrameCount;
        var seen = new HashSet<long>();
        foreach (var bar in _bars)
        {
            if (bar.IsTempoChangeOnly)
            {
                continue;
            }

            var sample = Math.Clamp(bar.SampleOffset, 0L, frameCount);
            if (seen.Add(sample))
            {
                result.Add(sample);
            }
        }

        return result;
    }

    /// <param name="includeSharedProjections">
    /// true ならグループ同期先の半透明マーカーも含める（Ctrl+←/→ 用）。
    /// </param>
    private List<long> CollectMarkerSamples(bool includeSharedProjections = false)
    {
        var result = new List<long>();
        if (_peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0)
        {
            return result;
        }

        var frameCount = _peaks.FrameCount;
        var seen = new HashSet<long>();
        foreach (var marker in _markers)
        {
            if (marker.IsSharedProjection && !includeSharedProjections)
            {
                continue;
            }

            var sample = Math.Clamp(marker.SampleOffset, 0L, frameCount);
            if (seen.Add(sample))
            {
                result.Add(sample);
            }
        }

        return result;
    }

    /// <summary>
    /// Ctrl+←/→ 用: 有効 Playlist の先頭と、表示マーカー位置をまとめた停止点。
    /// 隣接 Playlist 先頭だけだと区間内マーカーに止まらないため。
    /// グループ同期先の半透明マーカーも含める。
    /// </summary>
    private List<long> CollectPlaylistNavigationSamples()
    {
        var result = new List<long>();
        if (_peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0)
        {
            return result;
        }

        var frameCount = _peaks.FrameCount;
        var seen = new HashSet<long>();

        void Add(long sample)
        {
            sample = Math.Clamp(sample, 0L, frameCount);
            if (seen.Add(sample))
            {
                result.Add(sample);
            }
        }

        foreach (var part in _outputParts)
        {
            if (_disabledPlaylistPartNumbers.Contains(part.Number)
                || part.EndSampleOffset <= part.StartSampleOffset)
            {
                continue;
            }

            Add(part.StartSampleOffset);
        }

        foreach (var markerSample in CollectMarkerSamples(includeSharedProjections: true))
        {
            Add(markerSample);
        }

        return result;
    }

    private bool TrySeekAlongSamples(List<long> samples, bool previous)
    {
        if (_peaks is null || _peaks.IsEmpty || _peaks.FrameCount <= 0 || samples.Count == 0)
        {
            return false;
        }

        samples.Sort();
        var frameCount = _peaks.FrameCount;
        // 再生エンジンは時間ベースで僅かに手前へ着地し得るため、表示上の位置でサンプルを出す
        var currentSample = (long)Math.Round((_playheadProgress ?? 0d) * frameCount);
        currentSample = Math.Clamp(currentSample, 0L, frameCount);

        long? targetSample = null;
        if (previous)
        {
            for (var i = samples.Count - 1; i >= 0; i--)
            {
                if (samples[i] < currentSample)
                {
                    targetSample = samples[i];
                    break;
                }
            }
        }
        else
        {
            for (var i = 0; i < samples.Count; i++)
            {
                if (samples[i] > currentSample)
                {
                    targetSample = samples[i];
                    break;
                }
            }
        }

        if (targetSample is not long sample)
        {
            return false;
        }

        var progress = Math.Clamp(sample / (double)frameCount, 0d, 1d);
        // エンジン着地誤差の前に表示位置を確定し、連続ジャンプを可能にする
        _playheadProgress = progress;
        EnsureAbsoluteVisible(progress);
        SeekRequested?.Invoke(this, progress);
        return true;
    }

    /// <summary>指定の絶対進捗が見えるよう表示窓をずらす（既に見えていれば何もしない）。</summary>
    private void EnsureAbsoluteVisible(double absoluteProgress)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        absoluteProgress = Math.Clamp(absoluteProgress, 0d, 1d);

        // 全体表示（zoom=1 かつ先頭）なら既に全域が見える。
        if (_timeZoom <= TimeZoomMin + 1e-9 && _viewStart <= 1e-12)
        {
            return;
        }

        var span = ViewSpan;
        var margin = span * 0.05d;
        if (absoluteProgress >= _viewStart + margin && absoluteProgress <= ViewEnd - margin)
        {
            return;
        }

        _viewStart = absoluteProgress - span * 0.5d;
        ClampTimeViewWindow();
        NotifyTimeViewChanged();
    }

    /// <summary>
    /// 再生位置（シーク）は動かさず、表示窓だけ再生位置が中央になるようパンする。
    /// 全体表示時や再生位置が無いときは何もしない。
    /// </summary>
    public bool CenterViewOnPlayhead()
    {
        if (_peaks is null
            || _peaks.IsEmpty
            || _timeZoom <= TimeZoomMin + 1e-9
            || _playheadProgress is not { } playhead)
        {
            return false;
        }

        var absoluteProgress = Math.Clamp(playhead, 0d, 1d);
        var previous = _viewStart;
        _viewStart = absoluteProgress - ViewSpan * 0.5d;
        ClampTimeViewWindow();
        if (Math.Abs(_viewStart - previous) < 1e-12)
        {
            return false;
        }

        NotifyTimeViewChanged();
        return true;
    }

    /// <summary>
    /// マウスホイールによる時間軸ズーム。
    /// <paramref name="mouseXDip"/> は WPF DIP 座標。内部でデバイス px に変換する。
    /// </summary>
    public void ZoomTimeByWheel(int wheelDelta, int mouseXDip)
    {
        if (_peaks is null || _peaks.IsEmpty || wheelDelta == 0)
        {
            return;
        }

        var mouseX = (int)Math.Round(mouseXDip * DpiScale);

        // ノッチに応じた連続倍率（ホイールは 1/4 oct 刻み）
        var notches = Math.Max(1.0, Math.Abs(wheelDelta) / 120.0);
        var factor = Math.Pow(TimeZoomWheelStep, notches);
        if (wheelDelta < 0)
        {
            factor = 1.0 / factor;
        }

        var anchor = TryGetProgressFromX(mouseX, out var progress)
            ? progress
            : AnchorProgressForKeyboardZoom();
        AdjustTimeZoom(factor, anchor);
    }

    /// <summary>Shift+マウスホイールによる時間軸の左右スクロール。</summary>
    public void PanTimeByWheel(int wheelDelta)
    {
        if (_peaks is null
            || _peaks.IsEmpty
            || wheelDelta == 0
            || _timeZoom <= TimeZoomMin + 1e-9)
        {
            return;
        }

        var notches = Math.Max(1.0, Math.Abs(wheelDelta) / 120.0);
        var previous = _viewStart;
        var distance = ViewSpan * 0.1d * notches;
        _viewStart += wheelDelta < 0 ? distance : -distance;
        ClampTimeViewWindow();
        if (Math.Abs(_viewStart - previous) < 1e-12)
        {
            return;
        }

        NotifyTimeViewChanged();
    }

    /// <summary>スクロールバーから表示左端を設定する。</summary>
    public void SetTimeViewStart(double viewStart)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        var previous = _viewStart;
        _viewStart = viewStart;
        ClampTimeViewWindow();
        if (Math.Abs(_viewStart - previous) < 1e-12)
        {
            return;
        }

        NotifyTimeViewChanged();
    }

    public double TimeViewStart => _viewStart;

    public double TimeViewSpan => ViewSpan;

    /// <summary>Ctrl+マウスホイールによる縦方向（振幅）ズーム。</summary>
    public void ZoomAmpByWheel(int wheelDelta)
    {
        if (_peaks is null || _peaks.IsEmpty || wheelDelta == 0)
        {
            return;
        }

        var notches = Math.Max(1.0, Math.Abs(wheelDelta) / 120.0);
        var factor = Math.Pow(AmpZoomWheelStep, notches);
        if (wheelDelta < 0)
        {
            factor = 1.0 / factor;
        }

        AdjustAmpZoom(factor);
    }

    private double AnchorProgressForKeyboardZoom()
    {
        if (_playheadProgress is double playhead
            && playhead >= _viewStart
            && playhead <= ViewEnd)
        {
            return playhead;
        }

        return _viewStart + ViewSpan * 0.5d;
    }

    private double ViewSpan => 1.0 / Math.Max(_timeZoom, TimeZoomMin);

    private double ViewEnd => Math.Min(1.0, _viewStart + ViewSpan);

    private void ResetTimeZoom(bool refresh)
    {
        if (_peaks is not null
            && !_peaks.IsEmpty
            && TryGetNonExcludedContentAbsoluteRange(out var start, out var end))
        {
            // -R で隠している区間は「全体」に含めず、可視内容だけを表示幅いっぱいに収める。
            ApplyAbsoluteRangeFit(start, end, fillRatio: 1.0);
            if (refresh)
            {
                NotifyTimeViewChanged();
            }

            return;
        }

        _timeZoom = TimeZoomMin;
        _viewStart = 0;
        if (refresh)
        {
            NotifyTimeViewChanged();
        }
    }

    /// <summary>
    /// -R 以外のリージョンが覆うサンプル範囲を絶対進捗で返す。
    /// 除外が無いか可視範囲が取れないときは false。
    /// </summary>
    private bool TryGetNonExcludedContentAbsoluteRange(
        out double absoluteStart,
        out double absoluteEnd)
    {
        absoluteStart = 0d;
        absoluteEnd = 1d;
        if (_peaks is null || _peaks.FrameCount <= 0 || _regions.Count == 0)
        {
            return false;
        }

        var frameCount = _peaks.FrameCount;
        long? minStart = null;
        long? maxEnd = null;
        var hasExcluded = false;
        foreach (var region in _regions)
        {
            if (region.IsExcluded)
            {
                hasExcluded = true;
                continue;
            }

            if (region.EndSampleOffset <= region.StartSampleOffset)
            {
                continue;
            }

            if (minStart is null || region.StartSampleOffset < minStart.Value)
            {
                minStart = region.StartSampleOffset;
            }

            if (maxEnd is null || region.EndSampleOffset > maxEnd.Value)
            {
                maxEnd = region.EndSampleOffset;
            }
        }

        if (!hasExcluded
            || minStart is not { } startSample
            || maxEnd is not { } endSample
            || endSample <= startSample)
        {
            return false;
        }

        absoluteStart = SampleToAbsolute(startSample, frameCount);
        absoluteEnd = SampleToAbsolute(endSample, frameCount);
        return absoluteEnd > absoluteStart + 1e-12;
    }

    private void ResetAmpZoom(bool refresh)
    {
        _ampZoom = AmpZoomMin;
        if (refresh)
        {
            NotifyAmpViewChanged();
        }
    }

    private void SetTimeZoomAbsolute(double zoom, double anchorAbsolute)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        zoom = Math.Clamp(zoom, TimeZoomMin, TimeZoomMax);
        var oldSpan = ViewSpan;
        var rel = oldSpan > 1e-12
            ? Math.Clamp((anchorAbsolute - _viewStart) / oldSpan, 0d, 1d)
            : 0.5d;
        _timeZoom = zoom;
        _viewStart = anchorAbsolute - rel * ViewSpan;
        ClampTimeViewWindow();
        NotifyTimeViewChanged();
    }

    /// <summary>
    /// マウス X 直下の Music Playlist（出力パート）範囲を、表示幅の 90% になるようセンタリング表示する。
    /// 表示中のプレイリストがちょうど1つなら全体表示へ戻す。
    /// </summary>
    private void ZoomTimeToPlaylistUnderMouse(int mouseX)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        // 見えている範囲にプレイリストが1つだけならデフォルト（全体表示）へトグル
        if (CountPlaylistsIntersectingView() == 1)
        {
            ResetTimeZoom(refresh: true);
            return;
        }

        if (_outputParts.Count == 0 || !TryGetProgressFromX(mouseX, out var progress))
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        if (frameCount <= 0)
        {
            return;
        }

        // 進捗→サンプルは半開区間 [Start, End) と整合するよう Floor
        var sample = (long)Math.Floor(Math.Clamp(progress, 0d, 1d) * frameCount);
        if (sample >= frameCount)
        {
            sample = frameCount - 1;
        }

        WaveformOutputPart? hit = null;
        foreach (var candidate in _outputParts)
        {
            if (sample >= candidate.StartSampleOffset && sample < candidate.EndSampleOffset)
            {
                hit = candidate;
                break;
            }
        }

        if (hit is not WaveformOutputPart part)
        {
            return;
        }

        ZoomTimeToAbsoluteRangeCentered(
            SampleToAbsolute(part.StartSampleOffset, frameCount),
            SampleToAbsolute(part.EndSampleOffset, frameCount),
            fillRatio: 0.9);
    }

    /// <summary>現在の表示窓と交差する Music Playlist（出力パート）の個数。</summary>
    private int CountPlaylistsIntersectingView()
    {
        if (_peaks is null || _peaks.IsEmpty || _outputParts.Count == 0)
        {
            return 0;
        }

        var frameCount = _peaks.FrameCount;
        if (frameCount <= 0)
        {
            return 0;
        }

        var viewStart = _viewStart;
        var viewEnd = ViewEnd;
        var count = 0;
        foreach (var part in _outputParts)
        {
            var a0 = SampleToAbsolute(part.StartSampleOffset, frameCount);
            var a1 = SampleToAbsolute(part.EndSampleOffset, frameCount);
            if (a1 > viewStart && a0 < viewEnd)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 絶対進捗範囲を表示幅の <paramref name="fillRatio"/> になるようズームし、中央に置く。
    /// </summary>
    private void ZoomTimeToAbsoluteRangeCentered(double absoluteStart, double absoluteEnd, double fillRatio)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        ApplyAbsoluteRangeFit(absoluteStart, absoluteEnd, fillRatio);
        NotifyTimeViewChanged();
    }

    private void ApplyAbsoluteRangeFit(double absoluteStart, double absoluteEnd, double fillRatio)
    {
        if (absoluteEnd < absoluteStart)
        {
            (absoluteStart, absoluteEnd) = (absoluteEnd, absoluteStart);
        }

        absoluteStart = Math.Clamp(absoluteStart, 0d, 1d);
        absoluteEnd = Math.Clamp(absoluteEnd, 0d, 1d);
        var rangeSpan = Math.Max(absoluteEnd - absoluteStart, 1e-12);
        fillRatio = Math.Clamp(fillRatio, 0.01d, 1d);

        // rangeSpan = fillRatio * viewSpan → viewSpan = rangeSpan / fillRatio → zoom = 1 / viewSpan
        var desiredZoom = fillRatio / rangeSpan;
        _timeZoom = Math.Clamp(desiredZoom, TimeZoomMin, TimeZoomMax);

        var mid = (absoluteStart + absoluteEnd) * 0.5d;
        _viewStart = mid - ViewSpan * 0.5d;
        ClampTimeViewWindow();
    }

    private void SetAmpZoomAbsolute(double zoom)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        _ampZoom = Math.Clamp(zoom, AmpZoomMin, AmpZoomMax);
        NotifyAmpViewChanged();
    }

    private void AdjustAmpZoom(double factor)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        var newZoom = Math.Clamp(_ampZoom * factor, AmpZoomMin, AmpZoomMax);
        if (Math.Abs(newZoom - _ampZoom) < 1e-9)
        {
            return;
        }

        _ampZoom = newZoom;
        NotifyAmpViewChanged();
    }

    private void AdjustTimeZoom(double factor, double anchorAbsolute)
    {
        if (_peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        var newZoom = Math.Clamp(_timeZoom * factor, TimeZoomMin, TimeZoomMax);
        if (Math.Abs(newZoom - _timeZoom) < 1e-9
            && (newZoom <= TimeZoomMin || newZoom >= TimeZoomMax))
        {
            // これ以上縮小できないときも、表示窓が先頭以外なら先頭へ戻す。
            // すでに先頭なら再描画しない（複数波形で精密ピークの破棄→再読込ちらつきが出る）。
            if (newZoom <= TimeZoomMin && _viewStart > 1e-12)
            {
                _viewStart = 0;
                NotifyTimeViewChanged();
            }

            return;
        }

        SetTimeZoomAbsolute(newZoom, anchorAbsolute);
    }

    private void ClampTimeViewWindow()
    {
        if (_timeZoom <= TimeZoomMin + 1e-9)
        {
            _timeZoom = TimeZoomMin;
            _viewStart = 0;
            return;
        }

        var span = ViewSpan;
        _viewStart = Math.Clamp(_viewStart, 0d, Math.Max(0d, 1.0 - span));
    }

    private void ClearDetailPeaks()
    {
        _detailPeaks = null;
        _detailViewStart = double.NaN;
        _detailViewEnd = double.NaN;
        _detailPixelWidth = -1;
        _detailIsApproximate = false;
        // 進行中の raw 要求は消さない（ズーム連打で詳細読みが永久に完了しなくなるのを防ぐ）
    }

    private void NotifyAmpViewChanged() => RebuildPresentationLayers(clearDetailPeaks: false);

    private void NotifyTimeViewChanged()
    {
        // Form1 同等: ズームのたびに詳細キャッシュを捨て、新しい表示窓で取り直す。
        // 再構築自体は Render 優先度でまとめて、WPF の GDI→WriteableBitmap 連打を避ける。
        ClearDetailPeaks();
        TimeViewChanged?.Invoke(this, EventArgs.Empty);
        QueueTimeViewRebuild();
    }

    private void QueueTimeViewRebuild()
    {
        if (_timeViewRebuildQueued)
        {
            return;
        }

        _timeViewRebuildQueued = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _timeViewRebuildQueued = false;
                if (IsDisposed)
                {
                    return;
                }

                RebuildPresentationLayers(clearDetailPeaks: false);
            },
            DispatcherPriority.Render);
    }

    private void RebuildPresentationLayers(bool clearDetailPeaks)
    {
        if (clearDetailPeaks)
        {
            ClearDetailPeaks();
        }

        // Bitmap は破棄せずダーティ化のみ（直後の BuildStaticLayer で同サイズなら再利用）
        _staticLayerDirty = true;

        if (_presentationSuspendCount > 0)
        {
            return;
        }

        if (!IsHandleCreated || IsDisposed)
        {
            Invalidate();
            return;
        }

        var bounds = ClientRectangle;
        if (bounds.Width > 2 && bounds.Height > 2 && _peaks is not null && !_peaks.IsEmpty)
        {
            BuildStaticLayer(bounds);
        }

        Invalidate();
    }

    /// <summary>
    /// SetMarkers / SetRegions 等の連続更新で静的レイヤ再構築を 1 回にまとめる。
    /// </summary>
    public void SuspendPresentationRebuild() => _presentationSuspendCount++;

    public void ResumePresentationRebuild(bool clearDetailPeaks = false)
    {
        if (_presentationSuspendCount > 0)
        {
            _presentationSuspendCount--;
        }

        if (_presentationSuspendCount == 0)
        {
            RebuildPresentationLayers(clearDetailPeaks);
        }
    }

    private static double SampleToAbsolute(long sampleOffset, long frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        return Math.Clamp(sampleOffset / (double)frameCount, 0d, 1d);
    }

    private float AbsoluteToX(double absoluteProgress, Rectangle area)
    {
        var t = (absoluteProgress - _viewStart) / ViewSpan;
        return area.Left + (float)(t * area.Width);
    }

    private bool TryMapAbsoluteRange(
        double absoluteStart,
        double absoluteEnd,
        Rectangle area,
        out float x0,
        out float x1)
    {
        x0 = 0;
        x1 = 0;
        if (absoluteEnd < _viewStart || absoluteStart > ViewEnd)
        {
            return false;
        }

        var a0 = Math.Clamp(absoluteStart, _viewStart, ViewEnd);
        var a1 = Math.Clamp(absoluteEnd, _viewStart, ViewEnd);
        x0 = AbsoluteToX(a0, area);
        x1 = AbsoluteToX(a1, area);
        if (x1 < x0)
        {
            (x0, x1) = (x1, x0);
        }

        return true;
    }

    /// <summary>クリック／ドラッグでシーク（0?1）。</summary>
    public event EventHandler<double>? SeekRequested;

    /// <summary>時間軸の表示位置または倍率が変更された。</summary>
    public event EventHandler? TimeViewChanged;

    /// <summary>左側情報レーン幅が変わった（プロジェクト名コンボ幅の追従用）。</summary>
    public event EventHandler? InfoLaneWidthChanged;

    /// <summary>
    /// 情報レーン（Measure 等の色付き列）右端のクライアント X。
    /// セパレータは含まない。
    /// </summary>
    public int InfoLaneRightX
    {
        get
        {
            var content = ContentBounds;
            return content.Left + _infoLaneWidth;
        }
    }

    /// <summary>波形上のマウス操作に対応するトランスポート表示を要求する。</summary>
    public event EventHandler<TransportCommand>? TransportFeedbackRequested;

    /// <summary>Marker レーンで追加マーカーの描画／消去が要求された。</summary>
    public event EventHandler<MarkerEditRequestedEventArgs>? MarkerEditRequested;

    /// <summary>左側で編集されたソース名が確定された。</summary>
    public event EventHandler<SourceNameEditCommittedEventArgs>? SourceNameEditCommitted;

    public event EventHandler<SourceNameEditStateChangedEventArgs>? SourceNameEditStateChanged;

    /// <summary>Wave 単体モードでマーカーコメントが確定された。</summary>
    public event EventHandler<MarkerCommentEditCommittedEventArgs>? MarkerCommentEditCommitted;

    public event EventHandler<MarkerCommentEditStateChangedEventArgs>? MarkerCommentEditStateChanged;

    /// <summary>Wave 単体モードで選択マーカーの削除が要求された。</summary>
    public event EventHandler<MarkerSessionDeleteRequestedEventArgs>? MarkerSessionDeleteRequested;

    /// <summary>Wave 単体モードでマーカーのドラッグ移動が要求された。</summary>
    public event EventHandler<MarkerSessionMoveRequestedEventArgs>? MarkerSessionMoveRequested;

    /// <summary>リージョン端フェード（白三角）が変更された。</summary>
    public event EventHandler<RegionFadeChangedEventArgs>? RegionFadeChanged;

    /// <summary>Wave 単体モードで選択中のマーカー位置。未選択は null。</summary>
    public long? SelectedMarkerSampleOffset => _selectedMarkerSampleOffset;

    /// <summary>Wave 単体モードの選択マーカーを設定する。</summary>
    public void SetSelectedMarkerSampleOffset(long? sampleOffset) => SetSelectedMarker(sampleOffset);

    /// <summary>
    /// Wave 単体モードで、指定サンプル位置にちょうどあるマーカーのコメント編集を開始する。
    /// マーカーが無ければ false。
    /// </summary>
    public bool TryBeginMarkerCommentEditAtSample(long sampleOffset)
    {
        if (!_allowsSessionMarkerEdit
            || _peaks is null
            || _peaks.IsEmpty
            || _peaks.FrameCount <= 0)
        {
            return false;
        }

        WaveformMarkerMark? target = null;
        foreach (var marker in _markers)
        {
            if (marker.IsSharedProjection || marker.SampleOffset != sampleOffset)
            {
                continue;
            }

            target = marker;
            break;
        }

        if (target is not { } markerAtSample)
        {
            return false;
        }

        SetSelectedMarker(sampleOffset);
        var progress = Math.Clamp(sampleOffset / (double)_peaks.FrameCount, 0d, 1d);
        EnsureAbsoluteVisible(progress);
        Invalidate();
        Update();

        foreach (var hit in _markerHitRegions)
        {
            if (hit.SampleOffset != sampleOffset)
            {
                continue;
            }

            BeginMarkerCommentEdit(hit);
            return true;
        }

        // 表示外などでヒット領域が無い場合でも、三角付近にエディタを出す。
        BeginMarkerCommentEdit(CreateSyntheticMarkerHit(markerAtSample));
        return true;
    }

    private MarkerHitRegion CreateSyntheticMarkerHit(WaveformMarkerMark marker)
    {
        var content = ContentBounds;
        var labels = new Rectangle(
            content.Left + _infoLaneWidth + 6,
            content.Top,
            Math.Max(1, content.Width - _infoLaneWidth - 6),
            Math.Max(1, content.Height));
        using var measure = CreateMeasureGraphics();
        var rowHeight = Font.GetHeight(measure) + 2f;
        var markerRowTop = labels.Top + rowHeight * 3f;
        var tipY = markerRowTop + rowHeight - 1f;
        var triHalfW = Math.Min(5f, rowHeight * 0.35f);
        var triH = Math.Min(rowHeight - 3f, 9f);
        var frameCount = _peaks!.FrameCount;
        var abs = SampleToAbsolute(marker.SampleOffset, frameCount);
        var x = AbsoluteToX(abs, labels);
        var triangleBounds = RectangleF.FromLTRB(
            x - triHalfW - 2f,
            tipY - triH - 2f,
            x + triHalfW + 2f,
            tipY + 2f);
        var commentBounds = string.IsNullOrEmpty(marker.Comment)
            ? RectangleF.Empty
            : new RectangleF(
                x + triHalfW + 2f,
                markerRowTop,
                Math.Max(80f, marker.Comment.Length * Font.Size * 0.6f),
                rowHeight);
        return new MarkerHitRegion(
            marker.SampleOffset,
            marker.Comment,
            triangleBounds,
            commentBounds);
    }

    /// <summary>タイムライン描画領域の幅（マーカー 1px 移動の換算用）。</summary>
    public int TimelineContentWidth => Math.Max(0, GetTimelineContentRect().Width);

    /// <summary>
    /// ドラッグ付与時のスナップ単位。描画されるグリッド線には影響しない。
    /// </summary>
    public MarkerGridOverrideMode MarkerGridOverride { get; set; } =
        MarkerGridOverrideMode.Bar;

    /// <summary>マウス直下の Music Playlist 番号。範囲外では null。</summary>
    public event EventHandler<int?>? PlaylistHoverChanged;

    private void ClearPlayhead()
    {
        _playheadProgress = null;
        _trailActive = false;
        ClearTrailSamples();
        _overlayPlayheads.Clear();
        _overlayExitPlayheads.Clear();
        _overlayFadeOutPlayheads.Clear();
        ClearExitPlayhead();
        ClearAnacrusisPlayhead();
        ClearFadeOutPlayhead();
    }

    private void ClearExitPlayhead()
    {
        _exitPlayheadProgress = null;
        _exitTrailActive = false;
        _exitTrailSamples.Clear();
    }

    private void ClearAnacrusisPlayhead()
    {
        _anacrusisPlayheadProgress = null;
        _anacrusisTrailActive = false;
        _anacrusisTrailSamples.Clear();
    }

    private void ClearFadeOutPlayhead()
    {
        _fadeOutPlayheadProgress = null;
        _fadeOutTrailActive = false;
        _fadeOutPlayheadIsExit = false;
        _fadeOutTrailSamples.Clear();
    }

    private void ClearTrailSamples()
    {
        _trailSamples.Clear();
    }

    private void DisposeStaticLayer()
    {
        _staticLayerDirty = true;
        _staticLayer?.Dispose();
        _staticLayer = null;
    }

    protected override void OnRenderSizeChanged(System.Windows.SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        DisposeStaticLayer();
        if (_sourceNameEditor is { Visibility: System.Windows.Visibility.Visible } editor
            && TryGetSourceNameBounds(out var editorBounds))
        {
            SetEditorBounds(editor, GetSourceNameEditorBounds(editorBounds, editor));
        }

        if (!IsHandleCreated)
        {
            return;
        }

        if (_peaks is not null && !_peaks.IsEmpty && !_staticRebuildQueued)
        {
            _staticRebuildQueued = true;
            BeginInvoke(RebuildStaticLayerAfterResize);
        }
    }

    private void RebuildStaticLayerAfterResize()
    {
        _staticRebuildQueued = false;
        if (IsDisposed || _peaks is null || _peaks.IsEmpty)
        {
            return;
        }

        var bounds = ClientRectangle;
        if (bounds.Width > 2 && bounds.Height > 2)
        {
            BuildStaticLayer(bounds);
        }

        Invalidate();
    }

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

        if (!TryGetProgressFromX(location.X, out var progress))
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

    private static Rectangle GetSourceNameEditorBounds(Rectangle available, TextBox editor)
    {
        var preferredHeight = (int)Math.Ceiling(editor.FontSize * 1.6) + 6;
        var height = Math.Min(available.Height, Math.Max(22, preferredHeight));
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

        using var g = CreateMeasureGraphics();
        using var font = new Font(Font, FontStyle.Bold);
        var preferred = g.MeasureString("Ag", font).Height + 2;
        var height = Math.Min(available.Height, Math.Max(22, (int)Math.Ceiling(preferred)));
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
            // 波形ビューへ戻して点線枠の表示を避ける。
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
        var left = hit.CommentBounds.Width > 0
            ? (int)Math.Floor(hit.CommentBounds.Left)
            : (int)Math.Floor(hit.TriangleBounds.Right + 2f);
        var top = hit.CommentBounds.Width > 0
            ? (int)Math.Floor(hit.CommentBounds.Top) - 1
            : (int)Math.Floor(hit.TriangleBounds.Top) - 1;
        var width = hit.CommentBounds.Width > 0
            ? Math.Max(80, (int)Math.Ceiling(hit.CommentBounds.Width) + 16)
            : 120;
        var height = Math.Max(
            22,
            hit.CommentBounds.Width > 0
                ? (int)Math.Ceiling(hit.CommentBounds.Height) + 4
                : (int)Math.Ceiling(hit.TriangleBounds.Height) + 6);
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
                new MarkerEditRequestedEventArgs(mode, samples.Order().ToArray()));
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

        return points
            .GroupBy(point => point.SampleOffset)
            .Select(group => group.First())
            .OrderBy(point => point.X)
            .ToArray();
    }

    private readonly record struct MarkerGridPoint(long SampleOffset, float X);

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

        var x = Math.Clamp(mouseX, timeline.Left, timeline.Right);
        if (mouseX < timeline.Left)
        {
            SetHoveredPlaylistPart(null);
            if (_mouseGuideX is not null)
            {
                _mouseGuideX = null;
                RequestMouseGuideRepaint();
            }

            return;
        }

        UpdateHoveredPlaylistPart(mouseX);
        if (_mouseGuideX is float prev && Math.Abs(prev - x) < 0.25f)
        {
            return;
        }

        _mouseGuideX = x;
        RequestMouseGuideRepaint();
    }

    /// <summary>
    /// マウスガイド描画の更新要求。再生中は playhead タイマーが既に ~60fps で
    /// Invalidate しているため、移動ごとに全再描画するとシークバー更新が遅れる。
    /// </summary>
    private void RequestMouseGuideRepaint()
    {
        if (IsPlayheadTrailAnimating())
        {
            return;
        }

        Invalidate();
    }

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

    protected override void OnRender(System.Windows.Media.DrawingContext dc)
    {
        base.OnRender(dc);

        var dipWidth = ActualWidth;
        var dipHeight = ActualHeight;
        if (dipWidth <= 0 || dipHeight <= 0)
        {
            return;
        }

        // デバイス px ビットマップに WinForms と同じ単位で描き、DIP 矩形へ貼る。
        var scale = DpiScale;
        var dpi = DeviceDpi;
        var width = Math.Max(1, (int)Math.Round(dipWidth * scale));
        var height = Math.Max(1, (int)Math.Round(dipHeight * scale));

        if (_frameBitmap is null
            || _frameBitmap.Width != width
            || _frameBitmap.Height != height
            || Math.Abs(_frameBitmap.HorizontalResolution - dpi) > 0.1f)
        {
            _frameBitmap?.Dispose();
            _frameBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            _frameBitmap.SetResolution(dpi, dpi);
        }

        using (var g = Graphics.FromImage(_frameBitmap))
        {
            PaintFrame(g);
        }

        // DpiX/Y をデバイス DPI に合わせないと、WPF がビットマップを DIP 換算で再スケールして滲む。
        if (_presentationBitmap is null
            || _presentationBitmap.PixelWidth != width
            || _presentationBitmap.PixelHeight != height
            || Math.Abs(_presentationBitmap.DpiX - dpi) > 0.1
            || Math.Abs(_presentationBitmap.DpiY - dpi) > 0.1)
        {
            _presentationBitmap = new WriteableBitmap(
                width,
                height,
                dpi,
                dpi,
                System.Windows.Media.PixelFormats.Pbgra32,
                null);
        }

        CopyGdiBitmapToWriteableBitmap(_frameBitmap, _presentationBitmap);

        // 論理サイズは BitmapSource.Width/Height（= Pixel / (Dpi/96)）を使い、強制ストレッチしない。
        var destW = _presentationBitmap.Width;
        var destH = _presentationBitmap.Height;
        dc.DrawImage(_presentationBitmap, new System.Windows.Rect(0, 0, destW, destH));
    }

    /// <summary>
    /// GDI+ の <see cref="Bitmap"/> をロックし、そのピクセルを WPF の <see cref="WriteableBitmap"/> へコピーする。
    /// 両方とも事前乗算済み BGRA (Pbgra32 / Format32bppPArgb) のため、そのまま転送できる。
    /// </summary>
    private static void CopyGdiBitmapToWriteableBitmap(Bitmap source, WriteableBitmap target)
    {
        var rect = new Rectangle(0, 0, source.Width, source.Height);
        var data = source.LockBits(
            rect,
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            target.Lock();
            try
            {
                target.WritePixels(
                    new System.Windows.Int32Rect(0, 0, source.Width, source.Height),
                    data.Scan0,
                    data.Stride * source.Height,
                    data.Stride);
            }
            finally
            {
                target.Unlock();
            }
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    private void PaintFrame(Graphics g)
    {
        g.Clear(WaveformGdiColors.WaveformBack);
        // 毎フレームのプレイヘッド合成用。HighQuality はズーム／再生中のコピー負荷を増やすだけなので抑える。
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;

        var bounds = ClientRectangle;
        if (bounds.Width <= 2 || bounds.Height <= 2)
        {
            return;
        }

        if (_peaks is null || _peaks.IsEmpty || _holdScaffold)
        {
            DrawEmptyScaffold(g, bounds);
            return;
        }

        if (_staticLayerDirty || _staticLayer is null)
        {
            DrawEmptyScaffold(g, bounds);
        }
        else
        {
            g.DrawImageUnscaled(_staticLayer, 0, 0);
        }

        // 静的内容を先に暗くし、再生ヘッドやホバー枠は手前に残す。
        DrawDisabledPlaylistDimOverlay(g);

        var content = ContentBoundsOf(bounds, ContentPadPx);
        var timeline = GetTimelineRect(content);
        DrawSourceLevelMeter(g, content);
        DrawSourceNameHoverChrome(g);
        DrawPlaylistHoverOutline(g);
        DrawExportPartGlow(g, timeline);
        DrawPlayhead(g, timeline, _playheadProgress, _trailSamples, WaveformGdiColors.SeekCyan);
        foreach (var overlay in _overlayPlayheads)
        {
            DrawPlayhead(
                g,
                timeline,
                overlay.Progress,
                overlay.TrailSamples,
                WaveformGdiColors.SeekCyan);
        }

        DrawPlayhead(
            g,
            timeline,
            _fadeOutPlayheadProgress,
            _fadeOutTrailSamples,
            _fadeOutPlayheadIsExit
                ? WaveformGdiColors.SeekExit
                : WaveformGdiColors.SeekFadeOut);
        foreach (var overlayFadeOut in _overlayFadeOutPlayheads)
        {
            DrawPlayhead(
                g,
                timeline,
                overlayFadeOut.Progress,
                overlayFadeOut.TrailSamples,
                WaveformGdiColors.SeekFadeOut);
        }

        DrawPlayhead(g, timeline, _exitPlayheadProgress, _exitTrailSamples, WaveformGdiColors.SeekExit);
        foreach (var overlayExit in _overlayExitPlayheads)
        {
            DrawPlayhead(
                g,
                timeline,
                overlayExit.Progress,
                overlayExit.TrailSamples,
                WaveformGdiColors.SeekExit);
        }

        DrawPlayhead(
            g,
            timeline,
            _anacrusisPlayheadProgress,
            _anacrusisTrailSamples,
            WaveformGdiColors.SeekAnacrusis);
        DrawAltMarkerPairDragGuides(g, timeline);
        DrawMouseGuide(g, timeline);
        DrawPlaylistGroupNameLaneOverlays(g);
        // 不透明のグループ色の上に、コントラストを取った名前を載せ直す。
        DrawNameLaneLabelsOverGroupColors(g);
        // フォーマット表示は最前面（グループ色・-R・シークバーより上）。
        DrawPlaylistFormatLabelsTopmost(g);
    }
}
