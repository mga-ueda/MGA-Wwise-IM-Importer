using System.Windows.Input;

namespace MgaWwiseIMImporter.UI;

public partial class MainWindow
{
    /// <summary>波形ビュー操作用ショートカット（Form1 TryProcessWaveformShortcut 相当）。</summary>
    private bool TryProcessWaveformShortcut(Key key, ModifierKeys modifiers, bool showUiFeedback = true)
    {
        if (_uiInteractionLocks != UiInteractionLock.None)
        {
            return false;
        }

        if (showUiFeedback
            && TryGetTransportCommandForShortcut(key, modifiers, out var feedbackCommand)
            && IsTransportCommandAvailable(feedbackCommand))
        {
            if (_activeTransportShortcutCommand is { } activeCommand
                && activeCommand != feedbackCommand)
            {
                transportBar.EndShortcutFeedback(activeCommand);
            }

            _activeTransportShortcutCommand = feedbackCommand;
            _activeTransportShortcutKey = key;
            transportBar.BeginShortcutFeedback(feedbackCommand);
        }

        if (key == Key.G && modifiers == ModifierKeys.None)
        {
            if (!HasTransportBarNavigation())
            {
                return true;
            }

            EndActiveTransportShortcutFeedback();
            ShowBarJumpDialog();
            return true;
        }

        if (key == Key.Space && modifiers == ModifierKeys.Control)
        {
            _resumePlaybackAfterBackwardSeek = false;
            StartPrerollPlayback();
            return true;
        }

        if (key == Key.Enter && modifiers == ModifierKeys.Alt)
        {
            _resumePlaybackAfterBackwardSeek = false;
            RestartFromLastPlaybackStart();
            return true;
        }

        if (key == Key.Space && modifiers == ModifierKeys.None)
        {
            _resumePlaybackAfterBackwardSeek = false;
            TogglePlayback();
            return true;
        }

        if (key == Key.M && modifiers == ModifierKeys.None && !IsTextEntryFocusActive())
        {
            TryToggleMetronome();
            return true;
        }

        if ((key is Key.C or Key.OemPeriod or Key.Decimal)
            && modifiers == ModifierKeys.None
            && !IsTextEntryFocusActive())
        {
            waveformView.CenterViewOnPlayhead();
            return true;
        }

        if (key == Key.L && modifiers == ModifierKeys.None && !IsTextEntryFocusActive())
        {
            TrySeekNearActiveLoopEnd();
            return true;
        }

        if (key == Key.Z && modifiers == ModifierKeys.None && TryCycleWaveformHeightScale())
        {
            return true;
        }

        if (key == Key.Up && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            waveformView.ZoomAmpToMax();
            return true;
        }

        if (key == Key.Down && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            waveformView.ResetAmpZoom();
            return true;
        }

        if (key == Key.Up && modifiers == ModifierKeys.Control)
        {
            waveformView.ZoomTimeToMax();
            return true;
        }

        if (key == Key.Down && modifiers == ModifierKeys.Control)
        {
            waveformView.ResetTimeZoom();
            return true;
        }

        if (key == Key.Home && modifiers == ModifierKeys.Control)
        {
            PauseForBackwardSeekHold();
            waveformView.PanTimeToStart();
            SeekPlayback(0);
            return true;
        }

        if (key == Key.End && modifiers == ModifierKeys.Control)
        {
            waveformView.PanTimeToEnd();
            SeekPlayback(1);
            return true;
        }

        if (key == Key.Left && modifiers == ModifierKeys.Control)
        {
            if (!HasTransportPlaylistNavigation())
            {
                return true;
            }

            PauseForBackwardSeekHold();
            waveformView.SeekToPreviousPlaylist();
            return true;
        }

        if (key == Key.Right && modifiers == ModifierKeys.Control)
        {
            if (!HasTransportPlaylistNavigation())
            {
                return true;
            }

            waveformView.SeekToNextPlaylist();
            return true;
        }

        if (key == Key.Left && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (!HasWaveOnlyMarkerNavigation())
            {
                return true;
            }

            PauseForBackwardSeekHold();
            waveformView.SeekToPreviousMarker();
            return true;
        }

        if (key == Key.Right && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (!HasWaveOnlyMarkerNavigation())
            {
                return true;
            }

            waveformView.SeekToNextMarker();
            return true;
        }

        if (key == Key.Home && modifiers == ModifierKeys.None)
        {
            if (HasWaveOnlyViewStepNavigation())
            {
                PauseForBackwardSeekHold();
                waveformView.SeekByVisibleFractionPrevious();
                return true;
            }

            if (!HasTransportBarNavigation())
            {
                return true;
            }

            PauseForBackwardSeekHold();
            waveformView.SeekToPreviousBar();
            return true;
        }

        if (key == Key.End && modifiers == ModifierKeys.None)
        {
            if (HasWaveOnlyViewStepNavigation())
            {
                waveformView.SeekByVisibleFractionNext();
                return true;
            }

            if (!HasTransportBarNavigation())
            {
                return true;
            }

            waveformView.SeekToNextBar();
            return true;
        }

        if (key == Key.PageUp && modifiers == ModifierKeys.None)
        {
            PauseForBackwardSeekHold();
            waveformView.SeekToPreviousPage();
            return true;
        }

        if (key == Key.PageDown && modifiers == ModifierKeys.None)
        {
            waveformView.SeekToNextPage();
            return true;
        }

        if (key == Key.Up && modifiers == ModifierKeys.Shift)
        {
            waveformView.ZoomAmpIn();
            return true;
        }

        if (key == Key.Down && modifiers == ModifierKeys.Shift)
        {
            waveformView.ZoomAmpOut();
            return true;
        }

        if (key == Key.Up && modifiers == ModifierKeys.None)
        {
            waveformView.ZoomTimeIn();
            return true;
        }

        if (key == Key.Down && modifiers == ModifierKeys.None)
        {
            waveformView.ZoomTimeOut();
            return true;
        }

#if DEBUG
        if (key == Key.C && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ShowColorDevPanel();
            return true;
        }
#endif

        return false;
    }

    private static bool TryGetTransportCommandForShortcut(
        Key key,
        ModifierKeys modifiers,
        out TransportCommand command)
    {
        TransportCommand? mapped = (key, modifiers) switch
        {
            (Key.Space, ModifierKeys.None) => TransportCommand.TogglePlayback,
            (Key.G, ModifierKeys.None) => TransportCommand.JumpToBar,
            (Key.Home, ModifierKeys.Control) => TransportCommand.GoToStart,
            (Key.Left, ModifierKeys.Control) => TransportCommand.PreviousPlaylist,
            (Key.Home, ModifierKeys.None) => TransportCommand.PreviousBar,
            (Key.PageUp, ModifierKeys.None) => TransportCommand.PreviousPage,
            (Key.PageDown, ModifierKeys.None) => TransportCommand.NextPage,
            (Key.End, ModifierKeys.None) => TransportCommand.NextBar,
            (Key.Right, ModifierKeys.Control) => TransportCommand.NextPlaylist,
            (Key.End, ModifierKeys.Control) => TransportCommand.GoToEnd,
            (Key.Up, ModifierKeys.None) => TransportCommand.TimeZoomIn,
            (Key.Down, ModifierKeys.None) => TransportCommand.TimeZoomOut,
            (Key.Up, ModifierKeys.Control) => TransportCommand.TimeZoomMax,
            (Key.Down, ModifierKeys.Control) => TransportCommand.TimeZoomReset,
            (Key.Up, ModifierKeys.Shift) => TransportCommand.AmpZoomIn,
            (Key.Down, ModifierKeys.Shift) => TransportCommand.AmpZoomOut,
            (Key.Up, ModifierKeys.Control | ModifierKeys.Shift) => TransportCommand.AmpZoomMax,
            (Key.Down, ModifierKeys.Control | ModifierKeys.Shift) => TransportCommand.AmpZoomReset,
            (Key.Z, ModifierKeys.None) => TransportCommand.CycleWaveformHeight,
            _ => null,
        };

        command = mapped.GetValueOrDefault();
        return mapped.HasValue;
    }

}
