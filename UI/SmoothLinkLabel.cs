using System.Drawing.Text;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 小サイズでも滲まないよう、GDI+ のアンチエイリアス（グリッドフィット付き）で
/// 描画する LinkLabel。フッタの権利表記など、7〜8pt の英字表示に使う。
/// </summary>
internal sealed class SmoothLinkLabel : LinkLabel
{
    private float _lineHeightScale = 1f;

    /// <summary>
    /// 複数行の行送り倍率（1 未満で詰める）。1 のときは標準 LinkLabel 描画。
    /// ヒット判定もこの倍率に合わせる。
    /// </summary>
    public float LineHeightScale
    {
        get => _lineHeightScale;
        set
        {
            var next = Math.Clamp(value, 0.5f, 1.5f);
            if (Math.Abs(_lineHeightScale - next) < 0.001f)
            {
                return;
            }

            _lineHeightScale = next;
            Invalidate();
        }
    }

    public SmoothLinkLabel()
    {
        // TextRenderingHint を効かせるため GDI+ 描画にする。
        UseCompatibleTextRendering = true;
    }

    private bool UseCompactLines =>
        _lineHeightScale < 0.999f
        && Text.Contains('\n', StringComparison.Ordinal);

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if (!UseCompactLines)
        {
            var state = e.Graphics.Save();
            // 小サイズ英字の視覚上寄りを 150% 設計 nudge で下げる（生 px 固定にしない）。
            e.Graphics.TranslateTransform(0f, DesignMetrics.VisualTextNudgeY(this));
            base.OnPaint(e);
            e.Graphics.Restore(state);
            return;
        }

        // 行間詰め時は自前描画（標準 LinkLabel は行送りを変えられない）。
        // Transparent を Clear すると黒塗りになるため、親の実背景色で塗る。
        using (var back = new SolidBrush(ResolvePaintBackColor()))
        {
            e.Graphics.FillRectangle(back, ClientRectangle);
        }

        DrawCompactLines(e.Graphics);
    }

    private Color ResolvePaintBackColor()
    {
        if (BackColor.A == 255)
        {
            return BackColor;
        }

        for (var p = Parent; p is not null; p = p.Parent)
        {
            if (p.BackColor.A == 255)
            {
                return p.BackColor;
            }
        }

        return UiColors.ForControlBack(UiColors.WindowBack);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (UseCompactLines)
        {
            Cursor = HitTestCompact(e.Location) is not null ? Cursors.Hand : Cursors.Default;
            return;
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (UseCompactLines && e.Button == MouseButtons.Left)
        {
            var link = HitTestCompact(e.Location);
            if (link is not null)
            {
                OnLinkClicked(new LinkLabelLinkClickedEventArgs(link));
                return;
            }
        }

        base.OnMouseUp(e);
    }

    private float CompactLinePitch(Graphics g) => Font.GetHeight(g) * _lineHeightScale;

    private static readonly StringFormat CompactFormat = new(StringFormat.GenericTypographic)
    {
        FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip,
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Near,
    };

    private float CompactBlockTop(Graphics g)
    {
        var lines = Math.Max(1, Text.Split('\n').Length);
        var blockH = CompactLinePitch(g) * lines;
        // コントロール内でも塊を高さ中央へ（nudge で下寄せしない）。
        return Math.Max(0f, (ClientSize.Height - blockH) / 2f);
    }

    private void DrawCompactLines(Graphics g)
    {
        var lines = Text.Split('\n');
        var pitch = CompactLinePitch(g);
        var y = CompactBlockTop(g);
        var charOffset = 0;
        using var normal = new SolidBrush(ForeColor);
        using var linkBrush = new SolidBrush(LinkColor);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            DrawLineWithLinks(g, line, charOffset, 0f, y, normal, linkBrush);
            charOffset += line.Length + (i < lines.Length - 1 ? 1 : 0);
            y += pitch;
        }
    }

    private void DrawLineWithLinks(
        Graphics g,
        string line,
        int lineStart,
        float x,
        float y,
        Brush normal,
        Brush linkBrush)
    {
        if (line.Length == 0)
        {
            return;
        }

        var pos = 0;
        while (pos < line.Length)
        {
            var abs = lineStart + pos;
            var link = FindLinkCovering(abs);
            if (link is null)
            {
                var nextLinkStart = NextLinkStart(abs, lineStart + line.Length);
                var end = nextLinkStart < 0
                    ? line.Length
                    : Math.Min(line.Length, nextLinkStart - lineStart);
                if (end <= pos)
                {
                    end = line.Length;
                }

                var chunk = line[pos..end];
                g.DrawString(chunk, Font, normal, x, y, CompactFormat);
                x += MeasureWidth(g, chunk);
                pos = end;
                continue;
            }

            var linkEndAbs = link.Start + link.Length;
            var linkEndInLine = Math.Min(line.Length, linkEndAbs - lineStart);
            var linkChunk = line[pos..linkEndInLine];
            g.DrawString(linkChunk, Font, linkBrush, x, y, CompactFormat);
            x += MeasureWidth(g, linkChunk);
            pos = linkEndInLine;
        }
    }

    private float MeasureWidth(Graphics g, string text) =>
        string.IsNullOrEmpty(text) ? 0f : g.MeasureString(text, Font, int.MaxValue, CompactFormat).Width;

    private Link? FindLinkCovering(int absoluteIndex)
    {
        foreach (Link link in Links)
        {
            if (absoluteIndex >= link.Start && absoluteIndex < link.Start + link.Length)
            {
                return link;
            }
        }

        return null;
    }

    private int NextLinkStart(int fromExclusive, int limit)
    {
        var best = -1;
        foreach (Link link in Links)
        {
            if (link.Start >= fromExclusive && link.Start < limit
                && (best < 0 || link.Start < best))
            {
                best = link.Start;
            }
        }

        return best;
    }

    private Link? HitTestCompact(Point client)
    {
        using var g = CreateGraphics();
        var pitch = CompactLinePitch(g);
        var y0 = CompactBlockTop(g);
        if (client.Y < y0)
        {
            return null;
        }

        var lines = Text.Split('\n');
        var lineIndex = (int)((client.Y - y0) / pitch);
        if (lineIndex < 0 || lineIndex >= lines.Length)
        {
            return null;
        }

        var line = lines[lineIndex];
        var charOffset = 0;
        for (var i = 0; i < lineIndex; i++)
        {
            charOffset += lines[i].Length + 1;
        }

        float x = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var w = MeasureWidth(g, line[i].ToString());
            if (client.X >= x && client.X < x + Math.Max(w, 1f))
            {
                return FindLinkCovering(charOffset + i);
            }

            x += w;
        }

        return null;
    }
}
