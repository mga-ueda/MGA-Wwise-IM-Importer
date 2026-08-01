using System.Drawing.Drawing2D;

namespace MgaWwiseIMImporter.UI;

internal enum TransportCommand
{
    TogglePlayback,
    JumpToBar,
    GoToStart,
    PreviousPlaylist,
    PreviousBar,
    PreviousPage,
    NextPage,
    NextBar,
    NextPlaylist,
    GoToEnd,
    TimeZoomIn,
    TimeZoomOut,
    TimeZoomMax,
    TimeZoomReset,
    AmpZoomIn,
    AmpZoomOut,
    AmpZoomMax,
    AmpZoomReset,
    CycleWaveformHeight,
}

internal readonly record struct TransportPositionInfo(
    double Bpm,
    int Numerator,
    int Denominator,
    int Bar,
    int Beat,
    int Subdivision,
    TimeSpan Time,
    bool HasMusicalPosition = true);

/// <summary>波形操作のショートカットをアイコンで実行するフラットなトランスポートバー。</summary>
internal sealed class TransportBar : UserControl
{
    /// <summary>
    /// 旧定数（ボタン 30・バー 36 等）は AutoScale 前。150% 画面実寸は約 ×1.5。
    /// DesignMetrics の設計値はその実寸（ボタン 45・バー 54）とする。
    /// </summary>
    private const int ButtonSideDesign = 45;
    private const int BarHeightDesign = 54;
    private const int ButtonPitchDesign = 47;
    private const int GroupGapDesign = 6;
    private const int ButtonGapDesign = 2;
    private const int PadXDesign = 12;
    private const int PadYDesign = 5;

    private readonly FlowLayoutPanel _groups = new();
    private readonly TransportPositionDisplay _positionDisplay = new();
    private readonly Dictionary<TransportCommand, TransportIconButton> _commandButtons = [];
    private readonly System.Windows.Forms.Timer _commandRepeatTimer = new();
    private readonly List<(Label Label, Func<string> TitleProvider)> _groupLabels = [];
    private readonly TransportIconButton _playButton;
    private TransportCommand? _heldCommand;
    private TransportIconButton? _heldButton;
    private bool _repeatStarted;
    private bool _waveOnlyViewStepTips;
    private bool _waveOnlyMarkerTips;

    public TransportBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        // 幅不足時はフォーム MinimumSize で止め、バー内スクロールバーで潰さない。
        AutoScroll = false;
        BackColor = UiColors.ForControlBack(UiColors.TransportBack);
        Height = DesignMetrics.Px(BarHeightDesign, this);
        Padding = DesignMetrics.Pad(PadXDesign, PadYDesign, PadXDesign, PadYDesign, this);
        TabStop = false;

        _groups.AutoSize = true;
        _groups.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _groups.BackColor = BackColor;
        _groups.FlowDirection = FlowDirection.LeftToRight;
        _groups.Location = new Point(Padding.Left, Padding.Top);
        _groups.Margin = Padding.Empty;
        _groups.MinimumSize = new Size(1, DesignMetrics.Px(ButtonSideDesign, this));
        _groups.Padding = Padding.Empty;
        _groups.WrapContents = false;
        Controls.Add(_groups);
        _groups.Controls.Add(_positionDisplay);
        _positionDisplay.MetronomeInvoked += (_, _) => MetronomeInvoked?.Invoke(this, EventArgs.Empty);

        _playButton = AddGroup(
            () => UiStrings.LabelTransportGroup,
            repeatOnHold: false,
            (TransportCommand.TogglePlayback, TransportIcon.PlayPause),
            (TransportCommand.JumpToBar, TransportIcon.JumpToBar));

        AddGroup(
            () => UiStrings.LabelNavigationGroup,
            repeatOnHold: true,
            (TransportCommand.GoToStart, TransportIcon.GoToStart),
            (TransportCommand.PreviousPage, TransportIcon.PreviousPage),
            (TransportCommand.PreviousPlaylist, TransportIcon.PreviousRegion),
            (TransportCommand.PreviousBar, TransportIcon.PreviousBar),
            (TransportCommand.NextBar, TransportIcon.NextBar),
            (TransportCommand.NextPlaylist, TransportIcon.NextRegion),
            (TransportCommand.NextPage, TransportIcon.NextPage),
            (TransportCommand.GoToEnd, TransportIcon.GoToEnd));

        AddGroup(
            () => UiStrings.LabelTimeZoomGroup,
            repeatOnHold: true,
            (TransportCommand.TimeZoomIn, TransportIcon.TimeZoomIn),
            (TransportCommand.TimeZoomOut, TransportIcon.TimeZoomOut),
            (TransportCommand.TimeZoomMax, TransportIcon.TimeZoomMax),
            (TransportCommand.TimeZoomReset, TransportIcon.TimeZoomReset));

        AddGroup(
            () => UiStrings.LabelAmpZoomGroup,
            repeatOnHold: true,
            (TransportCommand.AmpZoomIn, TransportIcon.AmpZoomIn),
            (TransportCommand.AmpZoomOut, TransportIcon.AmpZoomOut),
            (TransportCommand.AmpZoomMax, TransportIcon.AmpZoomMax),
            (TransportCommand.AmpZoomReset, TransportIcon.AmpZoomReset));

        AddGroup(
            () => UiStrings.LabelWaveformHeightGroup,
            repeatOnHold: false,
            (TransportCommand.CycleWaveformHeight, TransportIcon.WaveformHeight));

        _commandRepeatTimer.Tick += (_, _) => RepeatHeldCommand();
        ApplyColors();
        _languageChangedHandler = (_, _) =>
        {
            if (!IsDisposed)
            {
                ApplyLocalizedTips();
            }
        };
        UiStrings.LanguageChanged += _languageChangedHandler;
        TightenVerticalLayout();
    }

    /// <summary>静的イベントの購読解除用（解除しないとコントロールが静的イベントに残り続ける）。</summary>
    private readonly EventHandler _languageChangedHandler;

    public event EventHandler<TransportCommand>? CommandInvoked;
    public event EventHandler? CommandHoldEnded;
    public event EventHandler? MetronomeInvoked;

    /// <summary>NAVIGATION / ZOOM ボタンがマウスで押下中か。</summary>
    public bool IsCommandHeld => _heldCommand.HasValue;

    /// <summary>
    /// 全グループを横スクロールなしで表示するために必要な幅。
    /// 左右 Padding と各グループの Margin も含む。
    /// </summary>
    public int RequiredWidth =>
        Padding.Horizontal
        + _groups.Controls
            .Cast<Control>()
            .Sum(control => control.Width + control.Margin.Horizontal);

    public bool IsPlaying
    {
        get => _playButton.IsPlaying;
        set => _playButton.IsPlaying = value;
    }

    public void SetPosition(TransportPositionInfo? position)
    {
        _positionDisplay.Position = position;
    }

    /// <summary>位置表示に載っている最新のテンポ／拍子／時刻。</summary>
    public TransportPositionInfo? CurrentPosition => _positionDisplay.Position;

    /// <summary>メトロノームのオン／オフ（既定オフ。テンポ／拍子が無いときは常にオフ）。</summary>
    public bool IsMetronomeEnabled
    {
        get => _positionDisplay.IsMetronomeEnabled;
        set => _positionDisplay.IsMetronomeEnabled = value;
    }

    /// <summary>画面座標が音符＋テンポの操作領域上なら true。</summary>
    public bool IsMetronomeHitAtScreenPoint(Point screenPoint) =>
        _positionDisplay.IsMetronomeHitAtScreenPoint(screenPoint);

    /// <summary>ホバー中なら静的メトロノーム Tips を再表示する。</summary>
    public void RestoreMetronomeTipIfHovered() =>
        _positionDisplay.RestoreMetronomeTipIfHovered();

    /// <summary>メトロノームボタンのキーボード操作表示を点灯して直ちにフェードする。</summary>
    public void PulseMetronomeFeedback() => _positionDisplay.PulseMetronomeFeedback();

    /// <summary>
    /// 小節ジャンプ／小節または表示ステップ／Playlist ナビの有効状態。
    /// 無効時は <see cref="UiColors.TransportDisabledFore"/> でグレーアウト。
    /// </summary>
    public void SetNavigationAvailability(
        bool jumpToBarEnabled,
        bool previousNextBarEnabled,
        bool playlistNavigationEnabled,
        bool waveOnlyViewStepTips = false,
        bool waveOnlyMarkerTips = false)
    {
        SetCommandEnabled(TransportCommand.JumpToBar, jumpToBarEnabled);
        SetCommandEnabled(TransportCommand.PreviousBar, previousNextBarEnabled);
        SetCommandEnabled(TransportCommand.NextBar, previousNextBarEnabled);
        SetCommandEnabled(TransportCommand.PreviousPlaylist, playlistNavigationEnabled);
        SetCommandEnabled(TransportCommand.NextPlaylist, playlistNavigationEnabled);

        if (_waveOnlyViewStepTips == waveOnlyViewStepTips
            && _waveOnlyMarkerTips == waveOnlyMarkerTips)
        {
            return;
        }

        _waveOnlyViewStepTips = waveOnlyViewStepTips;
        _waveOnlyMarkerTips = waveOnlyMarkerTips;
        ApplyLocalizedTips();
    }

    /// <summary>波形高さ倍率（1〜3）をアイコン表示へ反映する。</summary>
    public void SetWaveformHeightScale(int scale)
    {
        if (!_commandButtons.TryGetValue(TransportCommand.CycleWaveformHeight, out var button))
        {
            return;
        }

        button.WaveformHeightScale = scale is >= 1 and <= 3 ? scale : 1;
    }

    private void SetCommandEnabled(TransportCommand command, bool enabled)
    {
        if (_commandButtons.TryGetValue(command, out var button)
            && button.Enabled != enabled)
        {
            button.Enabled = enabled;
        }
    }

    /// <summary>表示言語に合わせて Tips・グループ見出し・アクセシビリティ名を付け直す。</summary>
    public void ApplyLocalizedTips()
    {
        foreach (var (command, button) in _commandButtons)
        {
            var tip = UiStrings.TipForTransportCommand(
                command,
                _waveOnlyViewStepTips,
                _waveOnlyMarkerTips);
            button.AccessibleName = tip;
            TipService.Set(button, tip);
        }

        foreach (var (label, titleProvider) in _groupLabels)
        {
            label.Text = titleProvider();
        }

        _positionDisplay.AccessibleName = UiStrings.AccessibleTransportPositionDisplay;
        _positionDisplay.ApplyLocalizedTips();
        RelayoutGroups();
        TightenVerticalLayout();
    }

    /// <summary>キーボード操作中のボタンをマウスオーバーと同じ表示にする。</summary>
    public void BeginShortcutFeedback(TransportCommand command)
    {
        if (_commandButtons.TryGetValue(command, out var button))
        {
            button.BeginShortcutFeedback();
        }
    }

    /// <summary>キーボード操作表示をホバー色からフェードアウトする。</summary>
    public void EndShortcutFeedback(TransportCommand command)
    {
        if (_commandButtons.TryGetValue(command, out var button))
        {
            button.EndShortcutFeedback();
        }
    }

    /// <summary>
    /// マウスホイール／スクロール操作に対応するボタンを点灯し、直ちにフェードアウトする。
    /// 連続操作時は呼び出すたびに点灯レベルを戻す。
    /// </summary>
    public void PulseCommandFeedback(TransportCommand command)
    {
        if (_commandButtons.TryGetValue(command, out var button))
        {
            button.BeginShortcutFeedback();
            button.EndShortcutFeedback();
        }
    }

    public void ApplyColors()
    {
        BackColor = UiColors.ForControlBack(UiColors.TransportBack);
        _groups.BackColor = BackColor;
        _positionDisplay.ApplyColors();

        foreach (var group in _groups.Controls.OfType<Panel>())
        {
            group.BackColor = BackColor;
            foreach (Control control in group.Controls)
            {
                control.BackColor = BackColor;
                if (control is Label label)
                {
                    label.ForeColor = UiColors.TransportSectionFore;
                }
                else if (control is FlowLayoutPanel buttons)
                {
                    buttons.BackColor = BackColor;
                    foreach (var button in buttons.Controls.OfType<TransportIconButton>())
                    {
                        button.ApplyColors();
                    }
                }
            }
        }

        Invalidate();
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        // 寸法は DesignMetrics で再適用する（AutoScale の幅拡大は打ち消す）。
        base.ScaleControl(factor, specified & ~BoundsSpecified.Width);
        ApplyFixedLayout();
    }

    /// <summary>
    /// コマンドボタンの正方形辺に合わせて、グループ行・位置表示・バー全体の高さを揃える。
    /// </summary>
    private void TightenVerticalLayout()
    {
        var side = _playButton.Height;
        if (side <= 0)
        {
            side = DesignMetrics.Px(ButtonSideDesign, this);
        }

        foreach (Control group in _groups.Controls)
        {
            if (group is TransportPositionDisplay position)
            {
                if (position.Height != side)
                {
                    position.Height = side;
                }

                continue;
            }

            if (group is not Panel panel)
            {
                continue;
            }

            if (panel.Height != side)
            {
                panel.Height = side;
            }

            foreach (Control child in panel.Controls)
            {
                if (child.Height != side)
                {
                    child.Height = side;
                }
            }
        }

        _groups.MinimumSize = new Size(1, side);
        var desiredHeight = Math.Max(DesignMetrics.Px(BarHeightDesign, this), Padding.Vertical + side);
        if (Height != desiredHeight)
        {
            Height = desiredHeight;
        }

        // Dock レイアウト後に潰れた場合のガード。
        if (Height < DesignMetrics.Px(36, this))
        {
            Height = desiredHeight;
        }

        _groups.Location = new Point(Padding.Left, Padding.Top);
        Visible = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyFixedLayout();
    }

    /// <summary>DPI / シミュレート変更後にボタン辺・グループ幅・バー高さを再適用する。</summary>
    public void ApplyFixedLayout()
    {
        Padding = DesignMetrics.Pad(PadXDesign, PadYDesign, PadXDesign, PadYDesign, this);
        _positionDisplay.ApplyFixedLayout();
        RelayoutGroups();
        TightenVerticalLayout();
    }

    /// <summary>グループ見出し幅・ボタンピッチを現在の LayoutDpi で組み直す。</summary>
    private void RelayoutGroups()
    {
        var buttonSide = DesignMetrics.Px(ButtonSideDesign, this);
        var buttonPitch = DesignMetrics.Px(ButtonPitchDesign, this);
        var buttonGap = DesignMetrics.Px(ButtonGapDesign, this);
        var groupMargin = DesignMetrics.Pad(0, 0, GroupGapDesign, 0, this);
        var labelPad = DesignMetrics.Px(6, this);
        var labelMin = DesignMetrics.Px(12, this);

        foreach (Control control in _groups.Controls)
        {
            if (control is TransportPositionDisplay || control is not Panel group)
            {
                continue;
            }

            Label? label = null;
            FlowLayoutPanel? buttonsHost = null;
            foreach (Control child in group.Controls)
            {
                switch (child)
                {
                    case Label l:
                        label = l;
                        break;
                    case FlowLayoutPanel flow:
                        buttonsHost = flow;
                        break;
                }
            }

            if (label is null || buttonsHost is null)
            {
                continue;
            }

            using var groupFont = new Font("Yu Gothic UI", 7F, FontStyle.Bold);
            var labelWidth = TextRenderer.MeasureText(
                label.Text,
                groupFont,
                Size.Empty,
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width + labelPad;
            labelWidth = Math.Max(labelWidth, labelMin);
            var count = buttonsHost.Controls.Count;
            label.Size = new Size(labelWidth, buttonSide);
            label.Padding = new Padding(0, 0, DesignMetrics.Px(3, this), 0);
            buttonsHost.Location = new Point(labelWidth, 0);
            buttonsHost.Size = new Size(Math.Max(1, count * buttonPitch), buttonSide);
            foreach (Control child in buttonsHost.Controls)
            {
                child.Size = new Size(buttonSide, buttonSide);
                child.Margin = new Padding(0, 0, buttonGap, 0);
            }

            group.Margin = groupMargin;
            group.Size = new Size(labelWidth + count * buttonPitch, buttonSide);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
    }

    private TransportIconButton AddGroup(
        Func<string> titleProvider,
        bool repeatOnHold,
        params (TransportCommand Command, TransportIcon Icon)[] definitions)
    {
        var buttonHeight = DesignMetrics.Px(ButtonSideDesign, this);
        var buttonPitch = DesignMetrics.Px(ButtonPitchDesign, this);
        var buttonGap = DesignMetrics.Px(ButtonGapDesign, this);
        var title = titleProvider();
        using var groupFont = new Font("Yu Gothic UI", 7F, FontStyle.Bold);
        var labelWidth = TextRenderer.MeasureText(
            title,
            groupFont,
            Size.Empty,
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width + DesignMetrics.Px(6, this);
        labelWidth = Math.Max(labelWidth, DesignMetrics.Px(12, this));
        var groupWidth = labelWidth + definitions.Length * buttonPitch;
        var group = new Panel
        {
            AutoSize = false,
            BackColor = BackColor,
            Margin = DesignMetrics.Pad(0, 0, GroupGapDesign, 0, this),
            Padding = Padding.Empty,
            Size = new Size(groupWidth, buttonHeight),
        };
        var label = new Label
        {
            BackColor = BackColor,
            Font = new Font(groupFont.FontFamily, groupFont.Size, groupFont.Style),
            ForeColor = UiColors.TransportSectionFore,
            Location = Point.Empty,
            Padding = new Padding(0, 0, DesignMetrics.Px(3, this), 0),
            Size = new Size(labelWidth, buttonHeight),
            Text = title,
            TextAlign = ContentAlignment.MiddleRight,
        };
        _groupLabels.Add((label, titleProvider));
        var buttons = new FlowLayoutPanel
        {
            AutoSize = false,
            BackColor = BackColor,
            FlowDirection = FlowDirection.LeftToRight,
            Location = new Point(labelWidth, 0),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = new Size(definitions.Length * buttonPitch, buttonHeight),
            WrapContents = false,
        };

        TransportIconButton? first = null;
        foreach (var definition in definitions)
        {
            var tip = UiStrings.TipForTransportCommand(
                definition.Command,
                _waveOnlyViewStepTips,
                _waveOnlyMarkerTips);
            var button = new TransportIconButton(definition.Icon)
            {
                AccessibleName = tip,
                Margin = new Padding(0, 0, buttonGap, 0),
                Tag = definition.Command,
            };
            if (repeatOnHold)
            {
                button.MouseDown += (_, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        BeginCommandHold(definition.Command, button);
                    }
                };
                button.MouseUp += (_, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        EndCommandHold();
                    }
                };
                button.MouseLeave += (_, _) => EndCommandHold();
                button.MouseCaptureChanged += (_, _) =>
                {
                    if (!button.Capture)
                    {
                        EndCommandHold();
                    }
                };
            }
            else
            {
                button.Click += (_, _) => CommandInvoked?.Invoke(this, definition.Command);
            }

            TipService.Set(button, tip);
            buttons.Controls.Add(button);
            _commandButtons[definition.Command] = button;
            first ??= button;
        }

        group.Controls.Add(buttons);
        group.Controls.Add(label);
        _groups.Controls.Add(group);
        return first!;
    }

    private void BeginCommandHold(TransportCommand command, TransportIconButton button)
    {
        EndCommandHold();
        _heldCommand = command;
        _heldButton = button;
        _repeatStarted = false;
        _commandRepeatTimer.Interval = (SystemInformation.KeyboardDelay + 1) * 250;
        _commandRepeatTimer.Start();
        CommandInvoked?.Invoke(this, command);
    }

    private void RepeatHeldCommand()
    {
        if (_heldCommand is not { } command
            || _heldButton is not { Enabled: true }
            || (MouseButtons & MouseButtons.Left) == 0)
        {
            EndCommandHold();
            return;
        }

        if (!_repeatStarted)
        {
            _repeatStarted = true;
            // Windows の KeyboardSpeed: 0=約2.5回/秒、31=約30回/秒。
            var repeatsPerSecond =
                2.5d + SystemInformation.KeyboardSpeed * (30d - 2.5d) / 31d;
            _commandRepeatTimer.Interval = Math.Max(
                20,
                (int)Math.Round(1000d / repeatsPerSecond));
        }

        CommandInvoked?.Invoke(this, command);
    }

    private void EndCommandHold()
    {
        if (_heldCommand is null)
        {
            return;
        }

        _commandRepeatTimer.Stop();
        _heldCommand = null;
        _heldButton = null;
        _repeatStarted = false;
        CommandHoldEnded?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UiStrings.LanguageChanged -= _languageChangedHandler;
            _commandRepeatTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class TransportPositionDisplay : Control
{
    /// <summary>
    /// 位置表示の幅・列座標は旧コードでも AutoScale されず画面実寸のまま。
    /// 高さだけボタン辺（150% 設計 45）に合わせる。
    /// </summary>
    private const int WidthDesign = 315;
    private const int HeightDesign = 45;
    private const int MetronomeHitWidthDesign = 57;
    private const int SignatureLeftDesign = 67;
    private const int SignatureWidthDesign = 38;
    private const int MusicalLeftDesign = 110;
    private const int MusicalWidthDesign = 74;
    private const int TimeLeftDesign = 189;
    private const int TimeWidthDesign = 124;

    private readonly TransportMetronomeButton _metronomeButton = new();
    private TransportPositionInfo? _position;
    private bool _metronomeEnabled;

    public TransportPositionDisplay()
    {
        AccessibleName = UiStrings.AccessibleTransportPositionDisplay;
        BackColor = UiColors.ForControlBack(UiColors.TransportBack);
        ForeColor = UiColors.TransportFore;
        Font = new Font("Yu Gothic UI", 9.5F, FontStyle.Bold);
        Margin = Padding.Empty;
        TabStop = false;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint,
            true);

        _metronomeButton.Location = new Point(0, 0);
        _metronomeButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
        _metronomeButton.Font = Font;
        _metronomeButton.Click += (_, _) => MetronomeInvoked?.Invoke(this, EventArgs.Empty);
        Controls.Add(_metronomeButton);
        ApplyFixedLayout();
        ApplyLocalizedTips();
        SyncMetronomeButtonContent();
    }

    private int MetronomeHitWidth => DesignMetrics.Px(MetronomeHitWidthDesign, this);

    public event EventHandler? MetronomeInvoked;

    /// <summary>メトロノームのオン／オフ。利用不可時は false に落とす。</summary>
    public bool IsMetronomeEnabled
    {
        get => _metronomeEnabled;
        set
        {
            var next = value && _metronomeButton.Enabled;
            if (_metronomeEnabled == next)
            {
                _metronomeButton.IsActive = next;
                return;
            }

            _metronomeEnabled = next;
            _metronomeButton.IsActive = next;
            Invalidate();
        }
    }

    public TransportPositionInfo? Position
    {
        get => _position;
        set
        {
            if (_position == value)
            {
                return;
            }

            _position = value;
            var available = value is { HasMusicalPosition: true };
            if (_metronomeButton.Enabled != available)
            {
                _metronomeButton.Enabled = available;
            }

            if (!available && _metronomeEnabled)
            {
                IsMetronomeEnabled = false;
            }

            SyncMetronomeButtonContent();
            Invalidate();
        }
    }

    public void ApplyColors()
    {
        BackColor = UiColors.ForControlBack(UiColors.TransportBack);
        ForeColor = UiColors.TransportFore;
        _metronomeButton.ApplyColors();
        SyncMetronomeButtonContent();
        Invalidate();
    }

    /// <summary>DPI / シミュレート変更後に幅・ヒット領域を再適用する。</summary>
    public void ApplyFixedLayout()
    {
        Size = new Size(DesignMetrics.Px(WidthDesign, this), DesignMetrics.Px(HeightDesign, this));
        var hitW = MetronomeHitWidth;
        _metronomeButton.SetBounds(0, 0, hitW, Height);
        Invalidate();
    }

    public void ApplyLocalizedTips()
    {
        var tip = UiStrings.TipTransportMetronome;
        _metronomeButton.AccessibleName = tip;
        TipService.Set(_metronomeButton, tip);
    }

    public void PulseMetronomeFeedback()
    {
        _metronomeButton.BeginShortcutFeedback();
        _metronomeButton.EndShortcutFeedback();
    }

    /// <summary>画面座標が音符＋テンポ領域上で、かつメトロノーム操作が可能なとき true。</summary>
    public bool IsMetronomeHitAtScreenPoint(Point screenPoint) =>
        _metronomeButton is { IsDisposed: false, Enabled: true, Visible: true }
        && _metronomeButton.RectangleToScreen(_metronomeButton.ClientRectangle).Contains(screenPoint);

    /// <summary>音量 Tips 消去後、まだホバー中なら通常 Tips を戻す。</summary>
    public void RestoreMetronomeTipIfHovered()
    {
        if (!IsMetronomeHitAtScreenPoint(Control.MousePosition))
        {
            return;
        }

        TipService.Show(UiStrings.TipTransportMetronome, _metronomeButton);
    }

    private void SyncMetronomeButtonContent()
    {
        var hasMusical = _position is { HasMusicalPosition: true };
        _metronomeButton.BpmText = hasMusical && _position is { } p
            ? Math.Round(p.Bpm).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "---";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var position = Position;
        var hasMusical = position is { HasMusicalPosition: true };
        var musicalFore = hasMusical ? ForeColor : UiColors.TransportDisabledFore;
        var timeFore = position is not null ? ForeColor : UiColors.TransportDisabledFore;

        var signature = hasMusical && position is { } signaturePosition
            ? $"{signaturePosition.Numerator}/{signaturePosition.Denominator}"
            : "--/--";
        var musicalPosition = hasMusical && position is { } musical
            ? $"{Math.Max(0, musical.Bar):000}:{musical.Beat}:{musical.Subdivision}"
            : "000:1:1";
        var elapsed = position?.Time ?? TimeSpan.Zero;
        var hours = Math.Max(0L, (long)elapsed.TotalHours);
        var time = $"{hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}";

        // BPM はメトロノームヒット領域側で描画（ホバー／クリック範囲を音符＋テンポで共有）。
        DrawText(
            g,
            signature,
            new Rectangle(
                DesignMetrics.Px(SignatureLeftDesign, this),
                0,
                DesignMetrics.Px(SignatureWidthDesign, this),
                Height),
            musicalFore);
        DrawText(
            g,
            musicalPosition,
            new Rectangle(
                DesignMetrics.Px(MusicalLeftDesign, this),
                0,
                DesignMetrics.Px(MusicalWidthDesign, this),
                Height),
            musicalFore);
        DrawText(
            g,
            time,
            new Rectangle(
                DesignMetrics.Px(TimeLeftDesign, this),
                0,
                DesignMetrics.Px(TimeWidthDesign, this),
                Height),
            timeFore);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        var hitW = MetronomeHitWidth;
        if (_metronomeButton.Height != Height || _metronomeButton.Width != hitW)
        {
            _metronomeButton.SetBounds(0, 0, hitW, Height);
        }
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        // 幅・内部座標は DesignMetrics で再適用するため、AutoScale の幅拡大は打ち消す。
        base.ScaleControl(factor, specified & ~BoundsSpecified.Width);
        ApplyFixedLayout();
    }

    private void DrawText(Graphics g, string text, Rectangle bounds, Color foreColor)
    {
        TextRenderer.DrawText(
            g,
            text,
            Font,
            bounds,
            foreColor,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.SingleLine);
    }
}

/// <summary>
/// 音符＋テンポ数値を囲むメトロノーム操作領域。
/// 見た目の座標は従来どおり、ヒット／ホバー範囲だけをまとめる。
/// </summary>
internal sealed class TransportMetronomeButton : Button
{
    private const double ShortcutFadeDurationMs = 180d;
    // 音符ステムとの間隔を少し確保（旧 22 → 25）。
    private const int BpmTextLeftDesign = 25;
    private const int BpmTextWidthDesign = 32;
    /// <summary>音符グリフの設計座標空間（スケール前）。ボタン高さ 45 とは別。</summary>
    private const int NoteGlyphDesign = 30;
    private const int DesignHeight = 45;
    private readonly System.Windows.Forms.Timer _shortcutFadeTimer = new() { Interval = 16 };
    private bool _hovered;
    private bool _pressed;
    private bool _isActive;
    private string _bpmText = "---";
    private double _shortcutFeedbackLevel;
    private long _shortcutFadeStartTickMs;

    public TransportMetronomeButton()
    {
        AccessibleRole = AccessibleRole.PushButton;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Size = new Size(DesignMetrics.Px(54, this), DesignMetrics.Px(DesignHeight, this));
        TabStop = false;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint,
            true);
        SetStyle(ControlStyles.Selectable, false);
        _shortcutFadeTimer.Tick += (_, _) => UpdateShortcutFeedbackFade();
        ApplyColors();
        Enabled = false;
    }

    public Color HoverBackColor { get; set; }
    public Color PressedBackColor { get; set; }
    public Color ActiveForeColor { get; set; }

    public string BpmText
    {
        get => _bpmText;
        set
        {
            var next = value ?? "---";
            if (_bpmText == next)
            {
                return;
            }

            _bpmText = next;
            Invalidate();
        }
    }

    /// <summary>メトロノームがオンのとき true（シアン表示）。</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            Invalidate();
        }
    }

    public void ApplyColors()
    {
        BackColor = UiColors.ForControlBack(UiColors.TransportBack);
        ForeColor = UiColors.TransportFore;
        HoverBackColor = UiColors.ForControlBack(UiColors.TransportHoverBack);
        PressedBackColor = UiColors.ForControlBack(UiColors.TransportPressedBack);
        ActiveForeColor = UiColors.ForControlBack(UiColors.SeekCyan);
        Invalidate();
    }

    public void BeginShortcutFeedback()
    {
        if (!Enabled)
        {
            return;
        }

        _shortcutFadeTimer.Stop();
        _shortcutFeedbackLevel = 1d;
        Invalidate();
    }

    public void EndShortcutFeedback()
    {
        if (_shortcutFeedbackLevel <= 0d)
        {
            return;
        }

        _shortcutFadeStartTickMs = Environment.TickCount64;
        _shortcutFadeTimer.Start();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            _hovered = false;
            _pressed = false;
            _shortcutFeedbackLevel = 0d;
            _shortcutFadeTimer.Stop();
            Cursor = Cursors.Default;
        }
        else
        {
            Cursor = Cursors.Hand;
        }

        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shortcutFadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = e.Button == MouseButtons.Left;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var hoverLevel = Enabled
            ? (_hovered ? 1d : _shortcutFeedbackLevel)
            : 0d;
        var back = _pressed
            ? PressedBackColor
            : BlendColor(BackColor, HoverBackColor, hoverLevel);
        if (_pressed || hoverLevel > 0d)
        {
            var insetX = DesignMetrics.Px(2, this);
            var insetY = DesignMetrics.Px(5, this);
            var hoverBounds = new Rectangle(
                insetX,
                insetY,
                Math.Max(0, Width - insetX * 2),
                Math.Max(0, Height - insetY * 2));
            using var hoverBrush = new SolidBrush(back);
            g.FillRectangle(hoverBrush, hoverBounds);
        }

        var fore = !Enabled
            ? UiColors.TransportDisabledFore
            : _isActive
                ? ActiveForeColor
                : ForeColor;
        // 音符は 30 設計座標を高さへ均一スケール（150% で従来どおり約 1.5 倍）。
        var noteScale = Height > 0 ? Height / (float)NoteGlyphDesign : 0f;
        if (noteScale > 0f)
        {
            using var iconPen = new Pen(fore, Math.Max(1f, DesignMetrics.PxF(1.6f, this)))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            using var iconBrush = new SolidBrush(fore);
            var noteState = g.Save();
            g.ScaleTransform(noteScale, noteScale);
            DrawQuarterNote(g, iconPen, iconBrush, 5f, 7f);
            g.Restore(noteState);
        }

        TextRenderer.DrawText(
            g,
            _bpmText,
            Font,
            new Rectangle(
                DesignMetrics.Px(BpmTextLeftDesign, this),
                0,
                DesignMetrics.Px(BpmTextWidthDesign, this),
                Height),
            fore,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.SingleLine);
    }

    private void UpdateShortcutFeedbackFade()
    {
        var elapsed = Math.Max(0L, Environment.TickCount64 - _shortcutFadeStartTickMs);
        var progress = Math.Clamp(elapsed / ShortcutFadeDurationMs, 0d, 1d);
        _shortcutFeedbackLevel = 1d - progress;
        if (progress >= 1d)
        {
            _shortcutFadeTimer.Stop();
            _shortcutFeedbackLevel = 0d;
        }

        Invalidate();
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (int)Math.Round(from.A + (to.A - from.A) * amount),
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }

    /// <summary>30×30 設計座標で四分音符を描く（呼び出し側で ScaleTransform する）。</summary>
    private static void DrawQuarterNote(Graphics g, Pen pen, Brush brush, float x, float y)
    {
        g.FillEllipse(brush, x, y + 11f, 7f, 5f);
        g.DrawLine(pen, x + 6f, y + 12f, x + 6f, y);
    }
}

internal enum TransportIcon
{
    PlayPause,
    JumpToBar,
    GoToStart,
    PreviousRegion,
    PreviousBar,
    PreviousPage,
    NextPage,
    NextBar,
    NextRegion,
    GoToEnd,
    TimeZoomIn,
    TimeZoomOut,
    TimeZoomMax,
    TimeZoomReset,
    AmpZoomIn,
    AmpZoomOut,
    AmpZoomMax,
    AmpZoomReset,
    WaveformHeight,
    Clear,
    Copy,
    Download,
    Folder,
    Delete,
    Lock,
    Unlock,
}

internal sealed class TransportIconButton : Button
{
    private const double ShortcutFadeDurationMs = 180d;
    private readonly System.Windows.Forms.Timer _shortcutFadeTimer = new() { Interval = 16 };
    private bool _hovered;
    private bool _pressed;
    private bool _isPlaying;
    private int _waveformHeightScale = 1;
    private double _shortcutFeedbackLevel;
    private long _shortcutFadeStartTickMs;

    public TransportIconButton(TransportIcon icon)
    {
        Icon = icon;
        AccessibleRole = AccessibleRole.PushButton;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        var side = DesignMetrics.Px(45, this);
        Size = new Size(side, side);
        TabStop = false;
        UseVisualStyleBackColor = false;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint,
            true);
        // クリックでフォーカスを奪わず、上下キーの波形拡縮を阻害しない。
        SetStyle(ControlStyles.Selectable, false);
        _shortcutFadeTimer.Tick += (_, _) => UpdateShortcutFeedbackFade();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            _hovered = false;
            _pressed = false;
            _shortcutFeedbackLevel = 0d;
            _shortcutFadeTimer.Stop();
        }

        Invalidate();
        base.OnEnabledChanged(e);
    }

    public TransportIcon Icon { get; private set; }

    public void SetIcon(TransportIcon icon)
    {
        if (Icon == icon)
        {
            return;
        }

        Icon = icon;
        Invalidate();
    }

    public Color HoverBackColor { get; set; }
    public Color PressedBackColor { get; set; }
    public Color AccentColor { get; set; }
    public Color ActiveForeColor { get; set; }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value)
            {
                return;
            }

            _isPlaying = value;
            Invalidate();
        }
    }

    /// <summary>波形高さアイコン用の現在倍率（1〜3）。</summary>
    public int WaveformHeightScale
    {
        get => _waveformHeightScale;
        set
        {
            var next = value is >= 1 and <= 3 ? value : 1;
            if (_waveformHeightScale == next)
            {
                return;
            }

            _waveformHeightScale = next;
            Invalidate();
        }
    }

    public void ApplyColors()
    {
        BackColor = UiColors.ForControlBack(UiColors.TransportBack);
        ForeColor = UiColors.TransportFore;
        HoverBackColor = UiColors.ForControlBack(UiColors.TransportHoverBack);
        PressedBackColor = UiColors.ForControlBack(UiColors.TransportPressedBack);
        AccentColor = Color.Empty;
        ActiveForeColor = UiColors.ForControlBack(UiColors.SeekCyan);
        Invalidate();
    }

    /// <summary>
    /// <see cref="AutoScaleMode.Font"/> はフォントの横／縦メトリクスで倍率が分かれ、
    /// 正方形のアイコンボタンが長方形になる。元が正方形なら短い辺に揃える。
    /// </summary>
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        var keepSquare = Width == Height;
        base.ScaleControl(factor, specified);
        if (keepSquare && Width != Height)
        {
            var side = Math.Min(Width, Height);
            Size = new Size(side, side);
        }
    }

    public void BeginShortcutFeedback()
    {
        _shortcutFadeTimer.Stop();
        _shortcutFeedbackLevel = 1d;
        Invalidate();
    }

    public void EndShortcutFeedback()
    {
        if (_shortcutFeedbackLevel <= 0d)
        {
            return;
        }

        _shortcutFadeStartTickMs = Environment.TickCount64;
        _shortcutFadeTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shortcutFadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = e.Button == MouseButtons.Left;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var hoverLevel = Enabled
            ? (_hovered ? 1d : _shortcutFeedbackLevel)
            : 0d;
        var back = _pressed
            ? PressedBackColor
            : BlendColor(BackColor, HoverBackColor, hoverLevel);
        if (_pressed || hoverLevel > 0d)
        {
            var inset = Math.Max(1, DesignMetrics.Px(3, this));
            var hoverBounds = new Rectangle(
                inset,
                inset,
                Math.Max(0, Width - inset * 2),
                Math.Max(0, Height - inset * 2));
            using var hoverBrush = new SolidBrush(back);
            g.FillRectangle(hoverBrush, hoverBounds);
            if (!AccentColor.IsEmpty)
            {
                using var accent = new Pen(AccentColor, Math.Max(1f, DesignMetrics.From96F(1f, this)));
                g.DrawRectangle(
                    accent,
                    hoverBounds.X + 0.5f,
                    hoverBounds.Y + 0.5f,
                    hoverBounds.Width - 1f,
                    hoverBounds.Height - 1f);
            }
        }

        using var brush = new SolidBrush(
            !Enabled
                ? UiColors.TransportDisabledFore
                : Icon == TransportIcon.PlayPause && IsPlaying
                    ? ActiveForeColor
                    : ForeColor);

        if (Icon == TransportIcon.WaveformHeight)
        {
            DrawWaveformHeightLabel(e.Graphics, brush.Color, WaveformHeightScale);
            return;
        }

        // グリフは 34×36 設計。幅／高さで別スケールすると 100% で歪むため均一スケール＋中央配置。
        const float designW = 34f;
        const float designH = 36f;
        var scale = Math.Min(Width / designW, Height / designH);
        if (scale <= 0f)
        {
            return;
        }

        using var pen = new Pen(
            Enabled ? ForeColor : UiColors.TransportDisabledFore,
            Math.Max(1f, DesignMetrics.PxF(1.8f, this)))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        var iconState = g.Save();
        // Clear/Copy/Download は設計座標がやや上寄りなので、ログボタン等で見た目中央へ下げる。
        var glyphNudgeY = Icon is TransportIcon.Clear or TransportIcon.Copy or TransportIcon.Download
            ? DesignMetrics.PxF(2f, this)
            : 0f;
        g.TranslateTransform(
            (Width - designW * scale) * 0.5f,
            (Height - designH * scale) * 0.5f + glyphNudgeY);
        g.ScaleTransform(scale, scale);
        DrawIcon(g, pen, brush);
        g.Restore(iconState);
    }

    private void UpdateShortcutFeedbackFade()
    {
        var elapsed = Math.Max(0L, Environment.TickCount64 - _shortcutFadeStartTickMs);
        var progress = Math.Clamp(elapsed / ShortcutFadeDurationMs, 0d, 1d);
        _shortcutFeedbackLevel = 1d - progress;
        if (progress >= 1d)
        {
            _shortcutFadeTimer.Stop();
            _shortcutFeedbackLevel = 0d;
        }

        Invalidate();
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (int)Math.Round(from.A + (to.A - from.A) * amount),
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }

    private void DrawIcon(Graphics g, Pen pen, Brush brush)
    {
        const float cx = 17f;
        const float cy = 18f;
        switch (Icon)
        {
            case TransportIcon.PlayPause:
                if (IsPlaying)
                {
                    g.FillRectangle(brush, 12, 11, 4, 14);
                    g.FillRectangle(brush, 19, 11, 4, 14);
                }
                else
                {
                    g.FillPolygon(brush, [new PointF(12, 9), new PointF(25, 18), new PointF(12, 27)]);
                }
                break;
            case TransportIcon.JumpToBar:
                DrawHash(g, pen, 10, 10, 19, 16);
                break;
            case TransportIcon.GoToStart:
            case TransportIcon.GoToEnd:
                var start = Icon == TransportIcon.GoToStart;
                var lineX = start ? 9f : 25f;
                g.DrawLine(pen, lineX, 9, lineX, 27);
                DrawChevron(g, pen, cx + (start ? 2 : -2), cy, start);
                break;
            case TransportIcon.PreviousRegion:
            case TransportIcon.NextRegion:
                var previousRegion = Icon == TransportIcon.PreviousRegion;
                DrawChevron(g, pen, cx + (previousRegion ? 3 : -3), cy, previousRegion);
                g.DrawLine(pen, previousRegion ? 10 : 24, 10, previousRegion ? 10 : 24, 26);
                g.DrawLine(pen, previousRegion ? 13 : 21, 13, previousRegion ? 13 : 21, 23);
                break;
            case TransportIcon.PreviousBar:
            case TransportIcon.NextBar:
                var previousBar = Icon == TransportIcon.PreviousBar;
                DrawChevron(g, pen, cx, cy, previousBar);
                g.DrawLine(pen, previousBar ? 10 : 24, 10, previousBar ? 10 : 24, 26);
                break;
            case TransportIcon.PreviousPage:
            case TransportIcon.NextPage:
                var previousPage = Icon == TransportIcon.PreviousPage;
                DrawChevron(g, pen, cx + (previousPage ? -2 : 2), cy, previousPage);
                DrawChevron(g, pen, cx + (previousPage ? 5 : -5), cy, previousPage);
                break;
            case TransportIcon.TimeZoomIn:
            case TransportIcon.TimeZoomOut:
            case TransportIcon.TimeZoomMax:
            case TransportIcon.TimeZoomReset:
                DrawHorizontalZoomIcon(g, pen);
                DrawZoomModifier(g, pen, brush, Icon, cx, cy);
                break;
            case TransportIcon.AmpZoomIn:
            case TransportIcon.AmpZoomOut:
            case TransportIcon.AmpZoomMax:
            case TransportIcon.AmpZoomReset:
                DrawVerticalZoomIcon(g, pen);
                DrawZoomModifier(g, pen, brush, Icon, cx, cy);
                break;
            case TransportIcon.Clear:
                g.DrawRectangle(pen, 12, 13, 10, 13);
                g.DrawLine(pen, 10, 11, 24, 11);
                g.DrawLine(pen, 14, 8, 20, 8);
                g.DrawLine(pen, 15, 16, 15, 23);
                g.DrawLine(pen, 19, 16, 19, 23);
                break;
            case TransportIcon.Copy:
                g.DrawRectangle(pen, 9, 8, 12, 14);
                g.DrawRectangle(pen, 13, 12, 12, 14);
                break;
            case TransportIcon.Download:
                g.DrawLine(pen, 17, 7, 17, 20);
                g.DrawLines(pen, [new PointF(12, 16), new PointF(17, 21), new PointF(22, 16)]);
                g.DrawLine(pen, 9, 26, 25, 26);
                break;
            // Folder / Delete は 16×16（x 9..25, y 10..26）の正方形グリフに揃える。
            case TransportIcon.Folder:
                g.DrawLines(pen, [
                    new PointF(9, 12),
                    new PointF(9, 10),
                    new PointF(15, 10),
                    new PointF(17, 12),
                    new PointF(25, 12),
                    new PointF(25, 26),
                    new PointF(9, 26),
                    new PointF(9, 12),
                ]);
                g.DrawLine(pen, 9, 15, 25, 15);
                break;
            case TransportIcon.Delete:
                // ゴミ箱
                g.DrawLine(pen, 9, 13, 25, 13);
                g.DrawLine(pen, 14, 10, 20, 10);
                g.DrawLines(pen, [
                    new PointF(11, 13),
                    new PointF(12, 26),
                    new PointF(22, 26),
                    new PointF(23, 13),
                ]);
                g.DrawLine(pen, 14, 16, 14, 23);
                g.DrawLine(pen, 17, 16, 17, 23);
                g.DrawLine(pen, 20, 16, 20, 23);
                break;
            case TransportIcon.Lock:
                DrawPadlockBody(g, pen);
                // 閉じたツメ：左右とも胴体に接続
                g.DrawLine(pen, 12.5f, 16f, 12.5f, 12.5f);
                g.DrawArc(pen, 12.5f, 7.5f, 9f, 9f, 180f, 180f);
                g.DrawLine(pen, 21.5f, 12.5f, 21.5f, 16f);
                break;
            case TransportIcon.Unlock:
                DrawPadlockBody(g, pen);
                // 開いたツメ：左だけ接続、右は下向きだが胴体との間に隙間を空ける
                g.DrawLine(pen, 12.5f, 16f, 12.5f, 11.5f);
                g.DrawArc(pen, 12.5f, 6.5f, 9.5f, 9.5f, 180f, 180f);
                g.DrawLine(pen, 22f, 11.5f, 22f, 13.5f);
                break;
        }
    }

    private static void DrawChevron(Graphics g, Pen pen, float centerX, float centerY, bool left)
    {
        var direction = left ? -1f : 1f;
        g.DrawLines(
            pen,
            [
                new PointF(centerX - direction * 4, centerY - 7),
                new PointF(centerX + direction * 3, centerY),
                new PointF(centerX - direction * 4, centerY + 7),
            ]);
    }

    private static void DrawPadlockBody(Graphics g, Pen pen)
    {
        g.DrawRectangle(pen, 10f, 16f, 14f, 11f);
        g.DrawEllipse(pen, 15.5f, 18.5f, 3f, 3f);
        g.DrawLine(pen, 17f, 21.5f, 17f, 24.5f);
    }

    private static void DrawHash(Graphics g, Pen pen, float x, float y, float width, float height)
    {
        g.DrawLine(pen, x + 4, y, x + 2, y + height);
        g.DrawLine(pen, x + 10, y, x + 8, y + height);
        g.DrawLine(pen, x, y + 5, x + width - 5, y + 5);
        g.DrawLine(pen, x, y + 11, x + width - 5, y + 11);
    }

    private static void DrawHorizontalZoomIcon(Graphics g, Pen pen)
    {
        g.DrawLine(pen, 7, 18, 27, 18);
        g.DrawLines(pen, [new PointF(11, 14), new PointF(7, 18), new PointF(11, 22)]);
        g.DrawLines(pen, [new PointF(23, 14), new PointF(27, 18), new PointF(23, 22)]);
    }

    private static void DrawVerticalZoomIcon(Graphics g, Pen pen)
    {
        g.DrawLine(pen, 17, 8, 17, 28);
        g.DrawLines(pen, [new PointF(13, 12), new PointF(17, 8), new PointF(21, 12)]);
        g.DrawLines(pen, [new PointF(13, 24), new PointF(17, 28), new PointF(21, 24)]);
    }

    /// <summary>
    /// 波形エリア高さの循環ラベル。JP／EN と同じ測って中央配置。
    /// </summary>
    private void DrawWaveformHeightLabel(Graphics g, Color color, int scale)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        scale = scale is >= 1 and <= 3 ? scale : 1;
        var label = "x" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var font = new Font("Yu Gothic UI", 7.5F, FontStyle.Bold);
        var textSize = TextRenderer.MeasureText(
            g,
            label,
            font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        var textX = (Width - textSize.Width) / 2;
        var textY = (Height - textSize.Height) / 2;
        TextRenderer.DrawText(
            g,
            label,
            font,
            new Point(textX, textY),
            color,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    private static void DrawZoomModifier(
        Graphics g,
        Pen pen,
        Brush brush,
        TransportIcon icon,
        float cx,
        float cy)
    {
        var isIn = icon is TransportIcon.TimeZoomIn or TransportIcon.AmpZoomIn;
        var isOut = icon is TransportIcon.TimeZoomOut or TransportIcon.AmpZoomOut;
        var isMax = icon is TransportIcon.TimeZoomMax or TransportIcon.AmpZoomMax;
        if (isIn || isOut)
        {
            using var badgeBrush = new SolidBrush(Color.FromArgb(220, UiColors.TransportBadgeBack));
            g.FillEllipse(badgeBrush, cx - 5, cy - 5, 10, 10);
            g.DrawLine(pen, cx - 3, cy, cx + 3, cy);
            if (isIn)
            {
                g.DrawLine(pen, cx, cy - 3, cx, cy + 3);
            }
        }
        else if (isMax)
        {
            g.FillRectangle(brush, cx - 3, cy - 3, 6, 6);
        }
        else
        {
            g.DrawEllipse(pen, cx - 4, cy - 4, 8, 8);
        }
    }
}
