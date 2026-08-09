using System.Windows;

namespace MgaWwiseIMImporter.UI;

/// <summary>カラー設定パネルでの変更を、動的着色コントロールへ反映する。</summary>
public partial class MainWindow
{
#if DEBUG
    private ColorDevPanelWindow? _colorDevPanel;
#endif

    private void ApplyUiColors()
    {
        Background = UiColors.Brush(UiColors.WindowBack);
        actionBar.Background = UiColors.Brush(UiColors.ActionBarBack);
        projectBar.Background = UiColors.Brush(UiColors.ProjectBarBack);
        waveformHostPanel.Background = UiColors.Brush(UiColors.WaveformScrollTrack);
        logAreaPanel.Background = UiColors.Brush(UiColors.LogBack);
        tipsPanel.Background = UiColors.Brush(UiColors.LogBack);
        tipsLabel.Foreground = UiColors.Brush(UiColors.LogDefault);
        logButtonPanel.Background = UiColors.Brush(UiColors.LogBack);
        editorTextBox.Background = UiColors.Brush(UiColors.LogBack);
        editorTextBox.Foreground = UiColors.Brush(UiColors.LogDefault);
        transitionTimePanel.Background = UiColors.Brush(UiColors.SurfaceBack);
        rightSidePanel.Background = UiColors.Brush(UiColors.SurfaceBack);
        playlistSelectorPanel.Background = UiColors.Brush(UiColors.PlaylistBack);
        projectSpectrumView.Background = UiColors.Brush(UiColors.ProjectBarBack);

        ApplySectionHeaderColors(logHeaderLabel);
        ApplySectionHeaderColors(tipsHeaderLabel);
        ApplySectionHeaderColors(playlistHeaderLabel);
        ApplySectionHeaderColors(fadeInHeaderLabel);
        ApplySectionHeaderColors(transitionTimeHeaderLabel);
        ApplySectionHeaderColors(optionsHeaderLabel);
        ApplySectionHeaderColors(fadeInGroupDividerLabel);
        ApplySectionHeaderColors(exitSourceAtHeaderLabel);
        ApplySectionHeaderColors(changeOccursAtHeaderLabel);

        ApplyActionBarButtonColors();
        ApplyLogButtonColors();
        copyrightLinkLabel.ApplyColors();

        transportBar.ApplyColors();
        waapiStatusBar.ApplyColors();
        markerOptionsPanel.ApplyColors();
        projectNameComboBox.ApplyColors();
        projectOutputPathTextBox.ApplyColors();
        waveformHorizontalScrollBar.ApplyColors();
        waveformView.RefreshAppearance();

        ApplyProjectBarIconColors(projectFolderButton);
        ApplyProjectBarIconColors(projectDeleteButton);

        foreach (var radio in FadeInRadios.Concat(TransitionTimeRadios).Concat(FadeInGroupRadios)
            .Concat(ExitSourceRadios).Concat(ChangeOccursRadios))
        {
            radio.Foreground = UiColors.Brush(UiColors.PlaylistOptionFore);
            radio.ApplyColors();
        }

        foreach (var checkBox in new[]
        {
            keepLastSessionCheckBox, topMostCheckBox, detailedLogCheckBox, compactFileNumbersCheckBox,
            playMinusECheckBox, additiveLayersCheckBox,
        })
        {
            checkBox.Foreground = UiColors.Brush(
                ReferenceEquals(checkBox, detailedLogCheckBox)
                    ? UiColors.ActionOptionFore
                    : ReferenceEquals(checkBox, keepLastSessionCheckBox)
                      || ReferenceEquals(checkBox, topMostCheckBox)
                        ? UiColors.PrimaryFore
                        : UiColors.PlaylistOptionFore);
            checkBox.ApplyColors();
        }

        fadeInCurveIcon.Opacity = 1;
        fadeOutCurveIcon.Opacity = 1;
        RefreshFadeCurveIcons();
        ApplyPlaylistButtonColors();
    }

    private static void ApplyProjectBarIconColors(TransportIconButton button)
    {
        button.Background = UiColors.Brush(UiColors.ProjectBarBack);
        button.Foreground = UiColors.Brush(UiColors.PrimaryFore);
        button.HoverBackColor = UiColors.ChromeMid;
        button.PressedBackColor = UiColors.ChromeDim;
        button.InvalidateVisual();
    }

    private static void ApplySectionHeaderColors(SectionHeaderLabel label)
    {
        label.BarColor = UiColors.SectionHeaderBack;
        label.Foreground = UiColors.Brush(UiColors.PlaylistDefaultFore);
    }

    private void ApplyActionBarButtonColors()
    {
        var innerBack = UiColors.ActionButtonInnerBack;

        exportButton.Background = UiColors.Brush(UiColors.ExportButtonFill);
        exportButton.Foreground = UiColors.Brush(UiColors.ExportButtonFore);
        exportButton.HoverBackColor = UiColors.ExportButtonHoverFill;
        exportButton.PressedBackColor = UiColors.ExportButtonHoverFill;
        exportButton.DisabledBackColor = innerBack;
        exportButton.DisabledForeColor = UiColors.ActionButtonDisabledFore;
        exportButton.BorderColor = UiColors.ExportButtonBack;
        exportButton.HoverBorderColor = UiColors.ExportButtonHoverBack;
        exportButton.PressedBorderColor = UiColors.ExportButtonPressedBack;
        exportButton.DisabledBorderColor = UiColors.ActionButtonDisabledBorder;
        exportButton.BorderSize = 2;

        reloadButton.Background = UiColors.Brush(UiColors.ReloadButtonFill);
        reloadButton.Foreground = UiColors.Brush(UiColors.ReloadButtonFore);
        reloadButton.HoverBackColor = UiColors.ReloadButtonHoverFill;
        reloadButton.PressedBackColor = UiColors.ReloadButtonHoverFill;
        reloadButton.DisabledBackColor = innerBack;
        reloadButton.DisabledForeColor = UiColors.ActionButtonDisabledFore;
        reloadButton.BorderColor = UiColors.ReloadButtonBack;
        reloadButton.HoverBorderColor = UiColors.ReloadButtonHoverBack;
        reloadButton.PressedBorderColor = UiColors.ReloadButtonPressedBack;
        reloadButton.DisabledBorderColor = UiColors.ActionButtonDisabledBorder;
        reloadButton.BorderSize = 2;

        clearButton.Background = UiColors.Brush(UiColors.ClearButtonFill);
        clearButton.Foreground = UiColors.Brush(UiColors.ClearButtonFore);
        clearButton.HoverBackColor = UiColors.ClearButtonHoverFill;
        clearButton.PressedBackColor = UiColors.ClearButtonHoverFill;
        clearButton.DisabledBackColor = innerBack;
        clearButton.DisabledForeColor = UiColors.ActionButtonDisabledFore;
        clearButton.BorderColor = UiColors.ClearButtonBack;
        clearButton.HoverBorderColor = UiColors.ClearButtonHoverBack;
        clearButton.PressedBorderColor = UiColors.ClearButtonPressedBack;
        clearButton.DisabledBorderColor = UiColors.ActionButtonDisabledBorder;
        clearButton.BorderSize = 2;

        detailedLogCheckBox.Foreground = UiColors.Brush(UiColors.ActionOptionFore);
        detailedLogCheckBox.ApplyColors();
        exportButton.InvalidateVisual();
        reloadButton.InvalidateVisual();
        clearButton.InvalidateVisual();
    }

    private void ApplyLogButtonColors()
    {
        // ApplyColors() は TransportBack（青みのある Chrome）を入れてしまうので使わない。
        foreach (var button in new[] { logClearButton, logCopyButton, logDownloadButton })
        {
            button.Background = UiColors.Brush(UiColors.LogButtonBack);
            button.Foreground = UiColors.Brush(UiColors.LogButtonFore);
            button.HoverBackColor = UiColors.ChromeMid;
            button.PressedBackColor = UiColors.ChromeDim;
            button.InvalidateVisual();
        }
    }

#if DEBUG
    private void ShowColorDevPanel()
    {
        if (_colorDevPanel is null)
        {
            _colorDevPanel = new ColorDevPanelWindow
            {
                Owner = this,
            };
            _colorDevPanel.ColorsChanged += (_, _) => ApplyUiColors();
            _colorDevPanel.Closed += (_, _) => _colorDevPanel = null;
            PositionColorDevPanel(_colorDevPanel);
        }

        _colorDevPanel.RefreshRows();
        _colorDevPanel.Show();
        _colorDevPanel.Activate();
    }

    private void PositionColorDevPanel(ColorDevPanelWindow panel)
    {
        // メイン窓の右隣。SizeToContent 後の実幅で収める。
        panel.WindowStartupLocation = WindowStartupLocation.Manual;
        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var panelWidth = panel.DesiredSize.Width;
        if (panelWidth <= 1)
        {
            panelWidth = panel.Width;
        }

        var work = SystemParameters.WorkArea;
        var x = Math.Min(Left + ActualWidth + 8, work.Right - panelWidth);
        var y = Math.Max(work.Top, Top);
        panel.Left = Math.Max(work.Left, x);
        panel.Top = y;
    }
#endif
}
