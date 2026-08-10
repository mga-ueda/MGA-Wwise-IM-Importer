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
    private void DrawMarkers(Graphics g, Rectangle labels, float rowHeight)
    {
        _markerHitRegions.Clear();
        if (_peaks is null || _peaks.FrameCount <= 0 || _markers.Count == 0 || labels.Width <= 0)
        {
            return;
        }

        var frameCount = _peaks.FrameCount;
        var markerRowTop = labels.Top + rowHeight * 3f;
        var markerRowBottom = markerRowTop + rowHeight;
        // ▼ の先端をマーカー時刻の X に厳密に合わせる（下向き）
        var tipY = markerRowBottom - 1f;
        // WinForms と同じ 5/9 device px 上限。
        var triHalfW = Math.Min(5f, rowHeight * 0.35f);
        var triH = Math.Min(rowHeight - 3f, 9f);

        using var triangleBrush = new SolidBrush(WaveformGdiColors.MarkerTriangle);
        using var selectedTriangleBrush = new SolidBrush(WaveformGdiColors.MarkerTriangleSelected);
        using var sharedTriangleBrush = new SolidBrush(Color.FromArgb(64, WaveformGdiColors.MarkerTriangle));
        using var selectedTriangleOutlinePen = new Pen(Color.White, 1.5f);
        using var textBrush = new SolidBrush(WaveformGdiColors.WaveformInfoFg);
        using var selectedTextBrush = new SolidBrush(WaveformGdiColors.MarkerCommentSelected);

        // 三角は時刻順に描画
        foreach (var marker in _markers)
        {
            var displaySample = GetDisplayedMarkerSample(marker.SampleOffset);
            var abs = SampleToAbsolute(displaySample, frameCount);
            if (abs < _viewStart - 1e-9 || abs > ViewEnd + 1e-9)
            {
                continue;
            }

            var x = AbsoluteToX(abs, labels);
            var selected = _allowsSessionMarkerEdit
                && _selectedMarkerSampleOffset == marker.SampleOffset;

            PointF[] triangle =
            [
                new(x, tipY),
                new(x - triHalfW, tipY - triH),
                new(x + triHalfW, tipY - triH),
            ];
            var brush = marker.IsSharedProjection
                ? sharedTriangleBrush
                : selected
                    ? selectedTriangleBrush
                    : triangleBrush;
            g.FillPolygon(brush, triangle);
            if (selected && !marker.IsSharedProjection)
            {
                g.DrawPolygon(selectedTriangleOutlinePen, triangle);
            }

            if (_allowsSessionMarkerEdit && !marker.IsSharedProjection)
            {
                var triangleBounds = RectangleF.FromLTRB(
                    x - triHalfW - 2f,
                    tipY - triH - 2f,
                    x + triHalfW + 2f,
                    tipY + 2f);
                _markerHitRegions.Add(new MarkerHitRegion(
                    marker.SampleOffset,
                    marker.Comment,
                    triangleBounds,
                    RectangleF.Empty));
            }
        }

        // コメントは左から配置。好みの位置に収まらない／前の文字と重なる場合は描かない
        // （ズームで間隔が広がれば表示される）。
        const float commentGapPx = 2f;
        var lastOccupiedRight = (float)labels.Left;
        foreach (var marker in _markers.OrderBy(m => GetDisplayedMarkerSample(m.SampleOffset)))
        {
            if (marker.IsSharedProjection)
            {
                continue;
            }

            var displaySample = GetDisplayedMarkerSample(marker.SampleOffset);
            var abs = SampleToAbsolute(displaySample, frameCount);
            if (abs < _viewStart - 1e-9 || abs > ViewEnd + 1e-9)
            {
                continue;
            }

            var x = AbsoluteToX(abs, labels);
            var selected = _allowsSessionMarkerEdit
                && _selectedMarkerSampleOffset == marker.SampleOffset;
            var comment = marker.Comment;
            if (string.IsNullOrEmpty(comment) && !selected)
            {
                continue;
            }

            var displayComment = string.IsNullOrEmpty(comment) ? " " : comment;
            var size = g.MeasureString(displayComment, Font);
            var textX = x + triHalfW + commentGapPx;
            var textRight = textX + size.Width;
            if (!selected
                && (textX < lastOccupiedRight + commentGapPx
                    || textRight > labels.Right + 1e-3f))
            {
                continue;
            }

            var textY = markerRowTop + Math.Max(0f, (rowHeight - size.Height) * 0.5f);
            if (!string.IsNullOrEmpty(comment))
            {
                g.DrawString(
                    comment,
                    Font,
                    selected ? selectedTextBrush : textBrush,
                    textX,
                    textY);
                lastOccupiedRight = textRight;
            }

            if (_allowsSessionMarkerEdit)
            {
                var commentBounds = string.IsNullOrEmpty(comment)
                    ? new RectangleF(textX, markerRowTop, Math.Max(24f, triHalfW * 4f), rowHeight)
                    : new RectangleF(textX, textY, size.Width, size.Height);
                for (var i = 0; i < _markerHitRegions.Count; i++)
                {
                    if (_markerHitRegions[i].SampleOffset != marker.SampleOffset)
                    {
                        continue;
                    }

                    _markerHitRegions[i] = _markerHitRegions[i] with
                    {
                        CommentBounds = commentBounds,
                    };
                    break;
                }
            }
        }
    }

}
