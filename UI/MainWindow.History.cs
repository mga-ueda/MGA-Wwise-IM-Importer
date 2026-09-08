using System.Windows;
using System.Windows.Input;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

public partial class MainWindow
{
    private int _historyAnchorIndex;
    private int _historySelectedIndex;
    private bool _historySwallowMouseUp;

    private bool HistoryOpen => HistoryOverlay.Visibility == Visibility.Visible;

    private void OpenEditHistory()
    {
        if (_previewSession is null)
        {
            return;
        }

        StopWaveOnlyMarkerNudgeHold(flushPersist: true);
        StopPlaybackForExport();
        _historyAnchorIndex = _editHistory.CurrentIndex;
        _historySelectedIndex = _historyAnchorIndex;
        RefreshHistoryOverlay();
        HistoryDismissLayer.Visibility = Visibility.Visible;
        HistoryOverlay.Visibility = Visibility.Visible;
    }

    private void CloseEditHistory(bool commit)
    {
        if (!HistoryOpen)
        {
            return;
        }

        if (!commit && _previewSession is not null)
        {
            ApplyHistoryIndex(_historyAnchorIndex, persistSession: true);
        }
        else if (commit)
        {
            SaveLastWaveSessionIfLoaded();
        }

        HideHistoryOverlay();
    }

    private void DismissEditHistory()
    {
        HideHistoryOverlay();
    }

    private void HideHistoryOverlay()
    {
        HistoryOverlay.Visibility = Visibility.Collapsed;
        HistoryDismissLayer.Visibility = Visibility.Collapsed;
    }

    private void CloseEditHistoryFromMouse(bool commit)
    {
        CloseEditHistory(commit);
        _historySwallowMouseUp = true;
    }

    private bool TryProcessHistoryShortcut(Key key, ModifierKeys modifiers)
    {
        if (!HistoryOpen)
        {
            return false;
        }

        if (modifiers != ModifierKeys.None)
        {
            return true;
        }

        switch (key)
        {
            case Key.Escape:
                CloseEditHistory(commit: false);
                return true;
            case Key.Enter:
            case Key.U:
                CloseEditHistory(commit: true);
                return true;
            case Key.Up:
                MoveHistorySelection(-1);
                return true;
            case Key.Down:
                MoveHistorySelection(1);
                return true;
            case Key.PageUp:
                MoveHistorySelection(-8);
                return true;
            case Key.PageDown:
                MoveHistorySelection(8);
                return true;
            case Key.Home:
                PreviewHistoryIndex(0);
                return true;
            case Key.End:
                PreviewHistoryIndex(_editHistory.TotalCount);
                return true;
            default:
                return true;
        }
    }

    private void MoveHistorySelection(int delta)
    {
        PreviewHistoryIndex(_historySelectedIndex + delta);
    }

    private void PreviewHistoryIndex(int index)
    {
        if (_previewSession is null)
        {
            return;
        }

        var next = Math.Clamp(index, 0, _editHistory.TotalCount);
        if (next == _historySelectedIndex && _editHistory.CurrentIndex == next)
        {
            RefreshHistoryOverlay();
            return;
        }

        _historySelectedIndex = next;
        ApplyHistoryIndex(next, persistSession: false);
        RefreshHistoryOverlay();
    }

    private bool ApplyHistoryIndex(int index, bool persistSession)
    {
        if (_previewSession is null || !_editHistory.JumpTo(index, out var frame))
        {
            return false;
        }

        ApplyHistoryFrame(frame, persistSession);
        return true;
    }

    private void ApplyHistoryFrame(EditHistoryFrame frame, bool persistSession)
    {
        if (_previewSession is null)
        {
            return;
        }

        var session = _previewSession;
        var beforeParts = session.EffectiveOutputParts.ToArray();
        if (frame.Markers is not null && session.AllowsSessionMarkerEdit)
        {
            session.TryReplaceWaveOnlySessionMarkers(frame.Markers);
        }

        session.SetRegionEdgeFades(frame.Fades);
        ApplyWaveOnlySessionPresentation(
            session,
            refreshPlaylists: !AreOutputPartsEquivalent(beforeParts, session.EffectiveOutputParts));
        waveformView.SetSelectedMarkerSampleOffset(null);
        if (persistSession)
        {
            SaveLastWaveSessionIfLoaded();
        }
    }

    private bool TryUndoEditHistory()
    {
        CommitMarkerMoveHistorySession();
        if (_previewSession is null || !_editHistory.CanUndo)
        {
            return false;
        }

        return ApplyHistoryIndex(_editHistory.CurrentIndex - 1, persistSession: true);
    }

    private bool TryRedoEditHistory()
    {
        CommitMarkerMoveHistorySession();
        if (_previewSession is null || !_editHistory.CanRedo)
        {
            return false;
        }

        return ApplyHistoryIndex(_editHistory.CurrentIndex + 1, persistSession: true);
    }

    private void RefreshHistoryOverlay()
    {
        HistoryOverlay.SetItems(_editHistory.Snapshot(), _historySelectedIndex);
    }

    private void HistoryOverlay_ItemChosen(object sender, int index)
    {
        PreviewHistoryIndex(index);
    }

    private void HistoryOverlay_ItemCommitted(object sender, int index)
    {
        PreviewHistoryIndex(index);
        CloseEditHistoryFromMouse(commit: true);
    }

    private void HistoryDismissLayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        e.Handled = true;
        CloseEditHistoryFromMouse(commit: false);
    }

    private void TrySwallowHistoryMouseUp(MouseButtonEventArgs e)
    {
        if (!_historySwallowMouseUp || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _historySwallowMouseUp = false;
        e.Handled = true;
    }
}
