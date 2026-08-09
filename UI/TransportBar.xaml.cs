using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MgaWwiseIMImporter.UI;

internal sealed partial class TransportBar : UserControl
{
    private readonly Dictionary<TransportCommand, TransportIconButton> _commandButtons = [];
    private readonly List<(TextBlock Label, Func<string> TitleProvider)> _groupLabels = [];
    private readonly DispatcherTimer _commandRepeatTimer;
    private readonly TransportPositionDisplay _positionDisplay = new();
    private TransportIconButton _playButton = null!;
    private TransportCommand? _heldCommand;
    private TransportIconButton? _heldButton;
    private bool _repeatStarted;
    private bool _waveOnlyViewStepTips;
    private bool _waveOnlyMarkerTips;
    private readonly EventHandler _languageChangedHandler;

    public TransportBar()
    {
        InitializeComponent();
        Height = DesignMetrics.TransportBarHeight;
        RootBorder.Padding = new Thickness(
            DesignMetrics.TransportPadX,
            DesignMetrics.TransportPadY,
            DesignMetrics.TransportPadX,
            DesignMetrics.TransportPadY);
        Focusable = false;

        _commandRepeatTimer = new DispatcherTimer();
        _commandRepeatTimer.Tick += (_, _) => RepeatHeldCommand();

        _positionDisplay.MetronomeInvoked += (_, _) => MetronomeInvoked?.Invoke(this, EventArgs.Empty);
        GroupsHost.Children.Add(_positionDisplay);

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

        ApplyColors();
        _languageChangedHandler = (_, _) => ApplyLocalizedTips();
        UiStrings.LanguageChanged += _languageChangedHandler;
    }

    public event EventHandler<TransportCommand>? CommandInvoked;
    public event EventHandler? CommandHoldEnded;
    public event EventHandler? MetronomeInvoked;

    public bool IsCommandHeld => _heldCommand.HasValue;

    public int RequiredWidth
    {
        get
        {
            GroupsHost.Measure(new Size(double.PositiveInfinity, DesignMetrics.TransportBarHeight));
            return (int)Math.Ceiling(DesignMetrics.TransportPadX * 2 + GroupsHost.DesiredSize.Width);
        }
    }

    public bool IsPlaying
    {
        get => _playButton.IsPlaying;
        set => _playButton.IsPlaying = value;
    }

    public TransportPositionInfo? CurrentPosition => _positionDisplay.Position;

    public bool IsMetronomeEnabled
    {
        get => _positionDisplay.IsMetronomeEnabled;
        set => _positionDisplay.IsMetronomeEnabled = value;
    }

    public void SetPosition(TransportPositionInfo? position) => _positionDisplay.Position = position;

    public bool IsMetronomeHitAtScreenPoint(Point screenPoint) =>
        _positionDisplay.IsMetronomeHitAtScreenPoint(screenPoint);

    public void RestoreMetronomeTipIfHovered() => _positionDisplay.RestoreMetronomeTipIfHovered();

    public void PulseMetronomeFeedback() => _positionDisplay.PulseMetronomeFeedback();

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

    public void SetWaveformHeightScale(int scale)
    {
        if (!_commandButtons.TryGetValue(TransportCommand.CycleWaveformHeight, out var button))
        {
            return;
        }

        button.WaveformHeightScale = scale is >= 1 and <= 3 ? scale : 1;
    }

    public void ApplyLocalizedTips()
    {
        foreach (var (command, button) in _commandButtons)
        {
            var tip = UiStrings.TipForTransportCommand(
                command,
                _waveOnlyViewStepTips,
                _waveOnlyMarkerTips);
            button.ToolTip = null;
            TipService.Set(button, tip);
        }

        foreach (var (label, titleProvider) in _groupLabels)
        {
            label.Text = titleProvider();
        }

        _positionDisplay.ApplyLocalizedTips();
    }

    public void BeginShortcutFeedback(TransportCommand command)
    {
        if (_commandButtons.TryGetValue(command, out var button))
        {
            button.BeginShortcutFeedback();
        }
    }

    public void EndShortcutFeedback(TransportCommand command)
    {
        if (_commandButtons.TryGetValue(command, out var button))
        {
            button.EndShortcutFeedback();
        }
    }

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
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.TransportBack));
        _positionDisplay.ApplyColors();
        foreach (var button in _commandButtons.Values)
        {
            button.ApplyColors();
        }

        foreach (var (label, _) in _groupLabels)
        {
            label.Foreground = UiColors.Brush(UiColors.TransportSectionFore);
        }
    }

    private void SetCommandEnabled(TransportCommand command, bool enabled)
    {
        if (_commandButtons.TryGetValue(command, out var button)
            && button.IsEnabled != enabled)
        {
            button.IsEnabled = enabled;
        }
    }

    private TransportIconButton AddGroup(
        Func<string> titleProvider,
        bool repeatOnHold,
        params (TransportCommand Command, TransportIcon Icon)[] definitions)
    {
        var group = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, DesignMetrics.TransportGroupGap, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var label = new TextBlock
        {
            Text = titleProvider(),
            Foreground = UiColors.Brush(UiColors.TransportSectionFore),
            FontWeight = FontWeights.Bold,
            FontSize = AppFonts.DipFromPoints(7),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, DesignMetrics.Dip(3), 0),
        };
        _groupLabels.Add((label, titleProvider));
        group.Children.Add(label);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
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
                Width = DesignMetrics.TransportButtonSide,
                Height = DesignMetrics.TransportButtonSide,
                Margin = new Thickness(0, 0, DesignMetrics.TransportButtonGap, 0),
                Tag = definition.Command,
            };
            TipService.Set(button, tip);

            if (repeatOnHold)
            {
                button.PreviewMouseLeftButtonDown += (_, e) => BeginCommandHold(definition.Command, button);
                button.PreviewMouseLeftButtonUp += (_, _) => EndCommandHold();
                button.MouseLeave += (_, _) => EndCommandHold();
                button.LostMouseCapture += (_, _) => EndCommandHold();
            }
            else
            {
                button.Click += (_, _) => CommandInvoked?.Invoke(this, definition.Command);
            }

            buttons.Children.Add(button);
            _commandButtons[definition.Command] = button;
            first ??= button;
        }

        group.Children.Add(buttons);
        GroupsHost.Children.Add(group);
        return first!;
    }

    private void BeginCommandHold(TransportCommand command, TransportIconButton button)
    {
        EndCommandHold();
        _heldCommand = command;
        _heldButton = button;
        _repeatStarted = false;
        _commandRepeatTimer.Interval = TimeSpan.FromMilliseconds((SystemParameters.KeyboardDelay + 1) * 250);
        _commandRepeatTimer.Start();
        CommandInvoked?.Invoke(this, command);
    }

    private void RepeatHeldCommand()
    {
        if (_heldCommand is not { } command
            || _heldButton is not { IsEnabled: true }
            || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            EndCommandHold();
            return;
        }

        if (!_repeatStarted)
        {
            _repeatStarted = true;
            var speed = SystemParameters.KeyboardSpeed;
            var repeatsPerSecond = 2.5d + speed * (30d - 2.5d) / 31d;
            _commandRepeatTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, (int)Math.Round(1000d / repeatsPerSecond)));
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
}
