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
    /// 表示窓の範囲を画面幅分のピークに集計する（ズーム時の粒度を確保）。
    /// ピラミッド粒度が足りない中間ズームでは、軽い範囲を同期で精密化し、
    /// それ以外は近似を即描画しつつ背景読みで差し替える（Form1 相当）。
    /// </summary>
    private WavPeakData? EnsureDetailPeaks(Rectangle wave)
    {
        if (_wavInfo is null || _peaks is null || _peaks.IsEmpty || wave.Width <= 0)
        {
            return null;
        }

        var viewEnd = ViewEnd;
        var frameCount = _peaks.FrameCount;
        var startFrame = (long)Math.Floor(_viewStart * frameCount);
        var endFrame = (long)Math.Ceiling(viewEnd * frameCount);
        startFrame = Math.Clamp(startFrame, 0, frameCount);
        endFrame = Math.Clamp(endFrame, startFrame, frameCount);
        var rangeFrames = endFrame - startFrame;
        var polylineZoom = IsPolylineZoom(rangeFrames, wave.Width);

        if (IsDetailCacheValid(wave.Width, viewEnd))
        {
            _detailPixelWidth = wave.Width;

            var hasPolylineSamples = !_detailIsApproximate
                && _detailPeaks!.Mins.Length == rangeFrames;
            if (polylineZoom && !hasPolylineSamples)
            {
                if (TryLoadRawDetailPeaks(startFrame, endFrame, wave.Width, polylineZoom: true))
                {
                    return _detailPeaks;
                }

                RequestRawDetail(_viewStart, viewEnd, wave.Width);
                return _detailIsApproximate ? _detailPeaks : null;
            }

            if (_detailIsApproximate)
            {
                // 近似キャッシュ中は精密化を促す（同期が取れれば即差し替え）。
                if (TryLoadRawDetailPeaks(startFrame, endFrame, wave.Width, polylineZoom: false))
                {
                    return _detailPeaks;
                }

                RequestRawDetail(_viewStart, viewEnd, wave.Width);
            }

            return _detailPeaks;
        }

        if (rangeFrames <= 0)
        {
            ClearDetailPeaks();
            return null;
        }

        var pyramid = _peakPyramid;
        if (polylineZoom)
        {
            // 折れ線は表示内の全サンプル点が必要。幅バケットのピラミッド集計では足りない。
            if (pyramid is not null
                && pyramid.HasFullDetailFor(startFrame, endFrame, (int)rangeFrames))
            {
                _detailPeaks = pyramid.ReadRange(startFrame, endFrame, (int)rangeFrames);
                _detailViewStart = _viewStart;
                _detailViewEnd = viewEnd;
                _detailPixelWidth = wave.Width;
                _detailIsApproximate = false;
                return _detailPeaks;
            }

            if (TryLoadRawDetailPeaks(startFrame, endFrame, wave.Width, polylineZoom: true))
            {
                return _detailPeaks;
            }

            RequestRawDetail(_viewStart, viewEnd, wave.Width);
            return AssignApproximateDetail(pyramid, startFrame, endFrame, viewEnd, wave.Width);
        }

        if (pyramid is not null)
        {
            var fullDetail = pyramid.HasFullDetailFor(startFrame, endFrame, wave.Width);
            if (fullDetail)
            {
                _detailPeaks = pyramid.ReadRange(startFrame, endFrame, wave.Width);
                _detailViewStart = _viewStart;
                _detailViewEnd = viewEnd;
                _detailPixelWidth = wave.Width;
                _detailIsApproximate = false;
                return _detailPeaks;
            }

            // ピラミッド不足 = 1px あたり基底バケット未満。読み量は小さく同期で即精密化できる。
            // （Form1 の背景読みは WinForms ではすぐ届くが、WPF では遅延し約 45 段ズームまで荒いまま固まった）
            if (TryLoadRawDetailPeaks(startFrame, endFrame, wave.Width, polylineZoom: false))
            {
                return _detailPeaks;
            }

            RequestRawDetail(_viewStart, viewEnd, wave.Width);
            return AssignApproximateDetail(pyramid, startFrame, endFrame, viewEnd, wave.Width);
        }

        // 階層構築中: 軽い範囲は同期、だめなら背景読み。
        if (TryLoadRawDetailPeaks(startFrame, endFrame, wave.Width, polylineZoom: false))
        {
            return _detailPeaks;
        }

        var overviewFramesPerBucket = frameCount / (double)Math.Max(1, _peaks.Mins.Length);
        if (rangeFrames / (double)wave.Width < overviewFramesPerBucket)
        {
            RequestRawDetail(_viewStart, viewEnd, wave.Width);
        }

        return null;
    }

    private WavPeakData? AssignApproximateDetail(
        WavPeakPyramid? pyramid,
        long startFrame,
        long endFrame,
        double viewEnd,
        int waveWidth)
    {
        if (pyramid is null)
        {
            return null;
        }

        _detailPeaks = pyramid.ReadRange(startFrame, endFrame, waveWidth);
        _detailViewStart = _viewStart;
        _detailViewEnd = viewEnd;
        _detailPixelWidth = waveWidth;
        _detailIsApproximate = true;
        return _detailPeaks;
    }

    private bool IsDetailCacheValid(int waveWidth, double viewEnd)
    {
        if (_detailPeaks is null || _detailPeaks.IsEmpty)
        {
            return false;
        }

        const double viewEpsilon = 1e-9;
        if (Math.Abs(_detailViewStart - _viewStart) > viewEpsilon
            || Math.Abs(_detailViewEnd - viewEnd) > viewEpsilon)
        {
            return false;
        }

        return Math.Abs(_detailPixelWidth - waveWidth) <= 1;
    }

    /// <summary>
    /// 生サンプルから詳細ピークを同期読みする。
    /// 折れ線は短窓の全サンプル、中間ズームは画面幅バケット（ピラミッド不足時は軽い）。
    /// </summary>
    private bool TryLoadRawDetailPeaks(
        long startFrame,
        long endFrame,
        int width,
        bool polylineZoom)
    {
        var info = _wavInfo;
        var peaks = _peaks;
        if (info is null || peaks is null || peaks.IsEmpty || width <= 0)
        {
            return false;
        }

        var rangeFrames = endFrame - startFrame;
        if (rangeFrames <= 0)
        {
            return false;
        }

        int peakCount;
        if (polylineZoom)
        {
            if (rangeFrames > 96_000L)
            {
                return false;
            }

            peakCount = (int)rangeFrames;
        }
        else
        {
            // 中間ズームの安全上限（通常は width×BaseBucketFrames 未満でここまで来ない）
            if (rangeFrames > (long)width * 1024L)
            {
                return false;
            }

            peakCount = width;
        }

        try
        {
            var data = _sourceSpans.Count > 1
                ? WavPeakReader.ReadVirtualRange(_sourceSpans, startFrame, endFrame, peakCount)
                : WavPeakReader.ReadRange(info, startFrame, endFrame, peakCount);
            if (data is null || data.IsEmpty)
            {
                return false;
            }

            _detailPeaks = data;
            _detailViewStart = _viewStart;
            _detailViewEnd = ViewEnd;
            _detailPixelWidth = width;
            _detailIsApproximate = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 生サンプルからの精密ピーク読みを背景スレッドへ依頼する（最新の要求のみ実行）。
    /// </summary>
    private void RequestRawDetail(double viewStart, double viewEnd, int width)
    {
        _rawDetailWanted = (viewStart, viewEnd, width);
        PumpRawDetailRead();
    }

    private void PumpRawDetailRead()
    {
        if (_rawDetailReading || _rawDetailWanted is not { } wanted)
        {
            return;
        }

        var info = _wavInfo;
        var peaks = _peaks;
        if (info is null || peaks is null || peaks.IsEmpty)
        {
            _rawDetailWanted = null;
            return;
        }

        _rawDetailWanted = null;

        var frameCount = peaks.FrameCount;
        var startFrame = Math.Clamp((long)Math.Floor(wanted.ViewStart * frameCount), 0, frameCount);
        var endFrame = Math.Clamp((long)Math.Ceiling(wanted.ViewEnd * frameCount), startFrame, frameCount);
        var rangeFrames = endFrame - startFrame;
        var polylineZoom = IsPolylineZoom(rangeFrames, wanted.Width);

        // 既に同じ表示窓の、必要な粒度の精密ピークを持っているなら読み直さない
        if (!_detailIsApproximate
            && _detailPeaks is not null
            && !_detailPeaks.IsEmpty
            && Math.Abs(_detailPixelWidth - wanted.Width) <= 1
            && Math.Abs(_detailViewStart - wanted.ViewStart) <= 1e-9
            && Math.Abs(_detailViewEnd - wanted.ViewEnd) <= 1e-9
            && (!polylineZoom || _detailPeaks.Mins.Length == rangeFrames))
        {
            return;
        }

        _rawDetailReading = true;
        var generation = _pyramidGeneration;
        var sourceSpans = _sourceSpans;
        // 折れ線領域では表示内の全サンプルを 1 点ずつ読む。
        var peakCount = polylineZoom && rangeFrames > 0
            ? (int)rangeFrames
            : wanted.Width;

        Task.Run(() =>
        {
            WavPeakData? data = null;
            try
            {
                data = sourceSpans.Count > 1
                    ? WavPeakReader.ReadVirtualRange(sourceSpans, startFrame, endFrame, peakCount)
                    : WavPeakReader.ReadRange(info, startFrame, endFrame, peakCount);
            }
            catch
            {
                // 読み失敗時は近似のまま表示を続ける
            }

            void CompleteOnUi()
            {
                _rawDetailReading = false;
                if (IsDisposed)
                {
                    return;
                }

                if (generation == _pyramidGeneration)
                {
                    ApplyRawDetail(wanted, data);
                }

                PumpRawDetailRead();
            }

            try
            {
                if (IsDisposed)
                {
                    _rawDetailReading = false;
                    return;
                }

                Dispatcher.BeginInvoke(() => CompleteOnUi(), DispatcherPriority.Render);
            }
            catch (InvalidOperationException)
            {
                _rawDetailReading = false;
            }
        });
    }

    private void ApplyRawDetail((double ViewStart, double ViewEnd, int Width) wanted, WavPeakData? data)
    {
        if (data is null || data.IsEmpty)
        {
            return;
        }

        // ズーム連打中に届いた古い結果は捨てる（表示窓が一致するときだけ反映）。
        const double viewEpsilon = 1e-9;
        if (Math.Abs(wanted.ViewStart - _viewStart) > viewEpsilon
            || Math.Abs(wanted.ViewEnd - ViewEnd) > viewEpsilon)
        {
            return;
        }

        _detailPeaks = data;
        _detailViewStart = wanted.ViewStart;
        _detailViewEnd = wanted.ViewEnd;
        _detailPixelWidth = wanted.Width;
        _detailIsApproximate = false;
        RebuildPresentationLayers(clearDetailPeaks: false);
    }

    /// <summary>ピーク階層をバックグラウンドで構築し、完成したら差し替える。</summary>
    private void StartPeakPyramidBuild(WavFileInfo? wavInfo)
    {
        _peakPyramid = null;
        var generation = ++_pyramidGeneration;
        if (wavInfo is null)
        {
            return;
        }

        Task.Run(() =>
        {
            WavPeakPyramid pyramid;
            try
            {
                pyramid = WavPeakPyramid.Build(wavInfo);
            }
            catch
            {
                return;
            }

            ApplyPeakPyramidOnUi(generation, pyramid);
        });
    }

    /// <summary>複数波形の仮想タイムライン向けピーク階層を背景構築する。</summary>
    private void StartPeakPyramidBuildFromSpans(IReadOnlyList<WaveformSourceSpan> spans)
    {
        _peakPyramid = null;
        var generation = ++_pyramidGeneration;
        if (spans.Count == 0)
        {
            return;
        }

        // BeginInvoke 後も壊れないようスナップショットを渡す
        var spansCopy = spans.ToArray();
        Task.Run(() =>
        {
            WavPeakPyramid pyramid;
            try
            {
                pyramid = WavPeakPyramid.BuildFromSpans(spansCopy);
            }
            catch
            {
                return;
            }

            ApplyPeakPyramidOnUi(generation, pyramid);
        });
    }

    private void ApplyPeakPyramidOnUi(int generation, WavPeakPyramid pyramid)
    {
        try
        {
            // 背景スレッドから IsLoaded を見ると落とすことがある。常に UI へ載せる。
            Dispatcher.BeginInvoke(
                () =>
                {
                    if (generation != _pyramidGeneration || IsDisposed)
                    {
                        return;
                    }

                    _peakPyramid = pyramid;
                    // 初回表示は概要の間引きピークのまま焼き付いていることがある。
                    // 階層完成後に再構築しないと、初回ズーム時に初めて真のピークへ切り替わり
                    // 「縦が少し大きくなった」ように見える。
                    RebuildPresentationLayers(clearDetailPeaks: true);
                },
                DispatcherPriority.Normal);
        }
        catch (InvalidOperationException)
        {
            // Dispatcher 破棄後などは無視（次回 SetPreview で再構築）
        }
    }
}
