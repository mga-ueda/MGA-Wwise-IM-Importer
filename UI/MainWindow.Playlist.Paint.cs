using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// Music Playlist の Shift/Ctrl ドラッグ塗り（Form1 PlaylistGroupTarget_* 相当）。
/// </summary>
public partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    private void WirePlaylistPaintHandlers(FrameworkElement target)
    {
        target.PreviewMouseLeftButtonDown += PlaylistGroupTarget_PreviewMouseLeftButtonDown;
        // Capture しない（Capture 中は子要素の色 Present がワンテンポ遅れる）。
        // 途中行の取りこぼしは範囲補間で吸収し、Move/Up は Window レベルで受ける。
    }

    private void PlaylistGroupTarget_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_populatingPlaylistChoices
            || e.ChangedButton != MouseButton.Left
            || sender is not FrameworkElement { Tag: int partNumber })
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var ctrl = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        var alt = (modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        if (alt && !ctrl && !shift)
        {
            _suppressNextPlaylistClick = sender is FlatPlaylistButton;
            if (!_disabledPartNumbers.Contains(partNumber)
                && TryGetOutputPart(partNumber) is { } part)
            {
                SelectPlaylistPart(partNumber, seekAndPlay: false);
                RequestPlaylistOverlayToggle(part);
            }

            e.Handled = true;
            return;
        }

        if (ctrl && shift)
        {
            _suppressNextPlaylistClick = sender is FlatPlaylistButton;
            TipService.Suspend();
            _playlistDisablePaintActive = true;
            _playlistDisablePaintSetDisabled = !_disabledPartNumbers.Contains(partNumber);
            _playlistDisablePaintLastPartNumber = null;
            ApplyPlaylistDisablePaintToPart(partNumber);
            e.Handled = true;
            return;
        }

        var erase = ctrl;
        var paint = shift;
        if (!erase && !paint)
        {
            return;
        }

        if (_disabledPartNumbers.Contains(partNumber))
        {
            return;
        }

        _suppressNextPlaylistClick = sender is FlatPlaylistButton;
        TipService.Suspend();
        _playlistGroupPaintActive = true;
        _playlistGroupPaintErase = erase;
        _playlistGroupPaintLastPartNumber = null;
        _playlistGroupPaintSeedPartNumber = partNumber;

        if (erase)
        {
            _playlistGroupPaintGroupId = null;
        }
        else if (_playlistGroupPaintStickyGroupId is int stickyGroupId)
        {
            _playlistGroupPaintGroupId = stickyGroupId;
        }
        else
        {
            var groupId = _nextGroupId++;
            _groupColorIndexes[groupId] = _nextColorIndex++;
            _playlistGroupPaintGroupId = groupId;
            _playlistGroupPaintStickyGroupId = groupId;
        }

        // 押下行を先に塗る（Form1 HitTest のみだと再生中に起点が外れる）。
        ApplyPlaylistGroupPaintToPart(partNumber);
        ApplyPlaylistGroupPaintAtCursor();
        e.Handled = true;
    }

    private void MainWindow_PreviewMouseMoveForPlaylistPaint(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (_playlistDisablePaintActive)
        {
            ApplyPlaylistDisablePaintAtCursor();
            return;
        }

        if (!_playlistGroupPaintActive)
        {
            return;
        }

        ApplyPlaylistGroupPaintAtCursor();
    }

    private void MainWindow_PreviewMouseLeftButtonUpForPlaylistPaint(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (_playlistGroupPaintActive || _playlistDisablePaintActive)
        {
            FinishPlaylistPaintAtCursor();
        }
    }

    private void FinishPlaylistPaintAtCursor()
    {
        if (_playlistDisablePaintActive)
        {
            ApplyPlaylistDisablePaintAtCursor();
            EndPlaylistDisablePaint();
            ReleaseSuppressedPlaylistClick();
            return;
        }

        if (!_playlistGroupPaintActive)
        {
            return;
        }

        ApplyPlaylistGroupPaintAtCursor();
        EndPlaylistGroupPaint();
        ReleaseSuppressedPlaylistClick();
    }

    private void EndPlaylistGroupPaint()
    {
        var wasActive = _playlistGroupPaintActive;
        var erase = _playlistGroupPaintErase;
        var groupId = _playlistGroupPaintGroupId;
        var seedPart = _playlistGroupPaintSeedPartNumber;
        _playlistGroupPaintActive = false;
        _playlistGroupPaintErase = false;
        _playlistGroupPaintGroupId = null;
        _playlistGroupPaintLastPartNumber = null;
        _playlistGroupPaintSeedPartNumber = null;
        if (wasActive)
        {
            if (!erase
                && groupId is int gid
                && seedPart is int seed
                && !_disabledPartNumbers.Contains(seed))
            {
                AssignPlaylistPartToGroup(seed, gid);
            }

            TipService.Resume();
            ApplyPlaylistGroupMarkerSharing();
            ApplyPlaylistGroupColorsOnly();
            PersistPlaylistGroupsToSession();
            AutosaveCurrentProject();
            SaveLastWaveSessionIfLoaded();
        }
    }

    private void EndPlaylistDisablePaint()
    {
        var wasActive = _playlistDisablePaintActive;
        _playlistDisablePaintActive = false;
        _playlistDisablePaintLastPartNumber = null;
        if (wasActive)
        {
            // Form1 同等: 各パート変更時に ApplyPlaylistDisableUi 済み。終了時は Tip 復帰と永続化のみ。
            TipService.Resume();
            ApplyPlaylistButtonColors();
            AutosaveCurrentProject();
            SaveLastWaveSessionIfLoaded();
        }
    }

    private void ClearPlaylistGroupPaintStickyId() => _playlistGroupPaintStickyGroupId = null;

    private void ReleaseSuppressedPlaylistClick()
    {
        if (!_suppressNextPlaylistClick)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => _suppressNextPlaylistClick = false);
    }

    private int? HitTestPlaylistPartAtCursor()
    {
        if (playlistListLayout is null || !playlistListLayout.IsLoaded)
        {
            return null;
        }

        if (!GetCursorPos(out var screen))
        {
            return null;
        }

        Point point;
        try
        {
            point = playlistListLayout.PointFromScreen(new Point(screen.X, screen.Y));
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        foreach (var row in playlistListLayout.Children.OfType<FrameworkElement>())
        {
            if (row.Tag is not int partNumber || row.ActualHeight <= 0)
            {
                continue;
            }

            try
            {
                var topLeft = row.TransformToAncestor(playlistListLayout).Transform(new Point(0, 0));
                var bounds = new Rect(
                    0,
                    topLeft.Y,
                    Math.Max(playlistListLayout.ActualWidth, row.ActualWidth),
                    row.ActualHeight);
                if (bounds.Contains(point))
                {
                    return partNumber;
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        return null;
    }

    private void ApplyPlaylistGroupPaintAtCursor()
    {
        if (!_playlistGroupPaintActive)
        {
            return;
        }

        if (HitTestPlaylistPartAtCursor() is not { } partNumber)
        {
            return;
        }

        ApplyPlaylistGroupPaintToPart(partNumber);
    }

    private void ApplyPlaylistGroupPaintToPart(int partNumber)
    {
        if (!_playlistGroupPaintActive)
        {
            return;
        }

        if (_playlistGroupPaintLastPartNumber == partNumber)
        {
            return;
        }

        var previous = _playlistGroupPaintLastPartNumber;
        _playlistGroupPaintLastPartNumber = partNumber;

        // MouseMove が行を飛ばしても、直前〜現在の一覧順をすべて塗る。
        foreach (var number in EnumeratePlaylistPartsBetween(previous, partNumber))
        {
            if (_disabledPartNumbers.Contains(number))
            {
                continue;
            }

            if (_playlistGroupPaintErase)
            {
                RemovePlaylistPartFromGroup(number);
            }
            else if (_playlistGroupPaintGroupId is int groupId)
            {
                AssignPlaylistPartToGroup(number, groupId);
            }

            RefreshPlaylistSwatch(number);
        }

        // Form1 同様、塗り中も波形のグループ帯色だけ即時更新する
        //（SetPlaylistGroupColors はレイヤ再生成なしの軽量パス）。
        waveformView.SetPlaylistGroupColors(BuildPlaylistGroupColorMap());
    }

    private void RefreshPlaylistSwatch(int partNumber)
    {
        if (!_playlistButtons.TryGetValue(partNumber, out var button)
            || button.Parent is not Panel row)
        {
            return;
        }

        if (row.Children.OfType<PlaylistGroupSwatch>().FirstOrDefault() is not { } swatch)
        {
            return;
        }

        swatch.Fill = TryGetPlaylistGroupColor(partNumber);
    }

    /// <summary>
    /// プレイリスト表示順で from〜to を含む区間のパート番号を返す。
    /// 高速ドラッグで HitTest が行を飛ばしても途中を取りこぼさないための補間。
    /// </summary>
    private IEnumerable<int> EnumeratePlaylistPartsBetween(int? fromPartNumber, int toPartNumber)
    {
        var order = playlistListLayout.Children
            .OfType<FrameworkElement>()
            .Select(row => row.Tag is int number ? number : (int?)null)
            .Where(number => number is not null)
            .Select(number => number!.Value)
            .ToList();

        if (order.Count == 0)
        {
            yield return toPartNumber;
            yield break;
        }

        if (fromPartNumber is not int from)
        {
            yield return toPartNumber;
            yield break;
        }

        var startIndex = order.IndexOf(from);
        var endIndex = order.IndexOf(toPartNumber);
        if (startIndex < 0 && endIndex < 0)
        {
            yield return toPartNumber;
            yield break;
        }

        if (startIndex < 0)
        {
            yield return toPartNumber;
            yield break;
        }

        if (endIndex < 0)
        {
            yield return from;
            yield break;
        }

        var lo = Math.Min(startIndex, endIndex);
        var hi = Math.Max(startIndex, endIndex);
        for (var i = lo; i <= hi; i++)
        {
            yield return order[i];
        }
    }

    private void ApplyPlaylistDisablePaintAtCursor()
    {
        if (!_playlistDisablePaintActive || HitTestPlaylistPartAtCursor() is not { } partNumber)
        {
            return;
        }

        ApplyPlaylistDisablePaintToPart(partNumber);
    }

    private void ApplyPlaylistDisablePaintToPart(int partNumber)
    {
        if (!_playlistDisablePaintActive)
        {
            return;
        }

        if (_playlistDisablePaintLastPartNumber == partNumber)
        {
            return;
        }

        var previous = _playlistDisablePaintLastPartNumber;
        _playlistDisablePaintLastPartNumber = partNumber;
        foreach (var number in EnumeratePlaylistPartsBetween(previous, partNumber))
        {
            SetPlaylistPartDisabled(number, _playlistDisablePaintSetDisabled);
        }
    }

    private void ApplyPlaylistGroupColorsOnly()
    {
        foreach (var row in playlistListLayout.Children.OfType<Panel>())
        {
            if (row.Children.OfType<PlaylistGroupSwatch>().FirstOrDefault() is not { Tag: int partNumber } swatch)
            {
                continue;
            }

            swatch.Fill = TryGetPlaylistGroupColor(partNumber);
        }

        waveformView.SetPlaylistGroupColors(BuildPlaylistGroupColorMap());

        // Form1 同等: グループ名の付け替えをリスト側へ即時反映（波形レイヤ再生成はしない）。
        if (_loadedPreview is not null && !_playlistDisablePaintActive)
        {
            UpdatePlaylistDisplayNames(GetEffectiveOutputParts(), updateWaveform: false);
        }

        if (!_playlistGroupPaintActive && !_playlistDisablePaintActive)
        {
            UpdateLayerMusicOptionEnabled();
        }
    }
}
