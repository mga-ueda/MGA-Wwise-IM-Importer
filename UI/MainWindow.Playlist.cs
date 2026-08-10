using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.UI;

/// <summary>プレイリスト行の生成・グループ／フェード／Exit Source 設定。</summary>
public partial class MainWindow
{
    private static readonly Color[] GroupColorPalette =
    {
        Color.FromRgb(0, 200, 220),
        Color.FromRgb(220, 140, 0),
        Color.FromRgb(140, 200, 60),
        Color.FromRgb(220, 90, 200),
        Color.FromRgb(90, 140, 220),
        Color.FromRgb(220, 60, 60),
    };

    private readonly HashSet<int> _disabledPartNumbers = [];
    private readonly Dictionary<int, int> _partGroupIds = [];
    private readonly Dictionary<int, int> _groupColorIndexes = [];
    private readonly Dictionary<int, PlaylistExitSourceMode> _partExitSourceModes = [];
    private readonly Dictionary<int, PlaylistExitSourceMode> _partChangeOccursAtModes = [];
    private readonly Dictionary<int, double> _partFadeInSeconds = [];
    private readonly Dictionary<int, double> _partFadeOutSeconds = [];
    private readonly Dictionary<int, RegionFadeCurveKind> _partFadeInCurves = [];
    private readonly Dictionary<int, RegionFadeCurveKind> _partFadeOutCurves = [];
    private readonly Dictionary<int, double> _partGroupFadeSeconds = [];
    private readonly Dictionary<int, bool> _partPlayPostExit = [];
    private readonly Dictionary<int, bool> _partAdditiveLayers = [];
    private readonly Dictionary<int, FlatPlaylistButton> _playlistButtons = [];
    private int? _selectedPlaylistPartNumber;
    private bool _populatingPlaylistChoices;

    /// <summary>波形プレイリストレーン上でポイント中のパート（一覧のホバー色用）。</summary>
    private int? _hoveredPlaylistPartNumber;

    /// <summary>Music Playlist 一覧上でポイント中のパート（波形白枠用）。</summary>
    private int? _hoveredPlaylistListPartNumber;

    private bool _playlistHoverColorRefreshQueued;
    private int? _lastAutoScrolledPlaylistPartNumber;

    /// <summary>Shift ドラッグでグループ塗り／Ctrl で解除／Ctrl+Shift で無効化を行う。</summary>
    private bool _playlistGroupPaintActive;
    private bool _playlistGroupPaintErase;
    private int? _playlistGroupPaintGroupId;
    private int? _playlistGroupPaintLastPartNumber;
    /// <summary>MouseDown 時点の起点パート。End 時に必ずグループへ含める（ヒットずれ対策）。</summary>
    private int? _playlistGroupPaintSeedPartNumber;
    private int? _playlistGroupPaintStickyGroupId;
    private bool _playlistDisablePaintActive;
    private bool _playlistDisablePaintSetDisabled;
    private int? _playlistDisablePaintLastPartNumber;
    private bool _suppressNextPlaylistClick;

    private FlatOptionRadioButton[] FadeInRadios => new[]
    {
        fadeInNoneRadio, fadeInHalfSecondRadio, fadeInOneSecondRadio, fadeInThreeSecondsRadio, fadeInSixSecondsRadio,
    };

    private FlatOptionRadioButton[] TransitionTimeRadios => new[]
    {
        transitionTimeNoneRadio, transitionTimeHalfSecondRadio, transitionTimeOneSecondRadio,
        transitionTimeThreeSecondsRadio, transitionTimeSixSecondsRadio,
    };

    private FlatOptionRadioButton[] FadeInGroupRadios => new[]
    {
        fadeInGroupNoneRadio, fadeInGroupOneSecondRadio, fadeInGroupThreeSecondsRadio,
        fadeInGroupSixSecondsRadio, fadeInGroupNineSecondsRadio,
    };

    private FlatOptionRadioButton[] ExitSourceRadios => new[]
    {
        exitSourceImmediateRadio, exitSourceNextBarRadio, exitSourceNextBeatRadio,
        exitSourceNextCueRadio, exitSourceExitCueRadio,
    };

    private FlatOptionRadioButton[] ChangeOccursRadios => new[]
    {
        changeOccursImmediateRadio, changeOccursNextBarRadio, changeOccursNextBeatRadio,
        changeOccursNextCueRadio, changeOccursExitCueRadio,
    };

    private static void SelectFadeRadio(IEnumerable<FlatOptionRadioButton> radios, double seconds)
    {
        foreach (var radio in radios)
        {
            radio.IsChecked = TagToSeconds(radio) is { } tagSeconds && Math.Abs(tagSeconds - seconds) < 0.001;
        }
    }

    private static double? ResolveCheckedTag(IEnumerable<FlatOptionRadioButton> radios) =>
        radios.FirstOrDefault(r => r.IsChecked == true) is { } checked_ ? TagToSeconds(checked_) : null;

    private static double? TagToSeconds(FlatOptionRadioButton radio) =>
        radio.Tag is string text && double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static void SelectExitSourceRadio(IEnumerable<FlatOptionRadioButton> radios, PlaylistExitSourceMode mode)
    {
        foreach (var radio in radios)
        {
            radio.IsChecked = TagToExitSource(radio) == mode;
        }
    }

    private static PlaylistExitSourceMode? ResolveCheckedExitSource(IEnumerable<FlatOptionRadioButton> radios) =>
        radios.FirstOrDefault(r => r.IsChecked == true) is { } checked_ ? TagToExitSource(checked_) : null;

    private static PlaylistExitSourceMode? TagToExitSource(FlatOptionRadioButton radio) =>
        radio.Tag is string text && Enum.TryParse<PlaylistExitSourceMode>(text, ignoreCase: true, out var mode)
            ? mode
            : null;

    private void FadeRadio_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressProjectUiEvents || _populatingPlaylistChoices || _selectedPlaylistPartNumber is not { } part)
        {
            if (!_populatingPlaylistChoices && !_suppressProjectUiEvents)
            {
                AutosaveCurrentProject();
            }

            return;
        }

        if (ResolveCheckedTag(FadeInRadios) is { } fadeIn)
        {
            foreach (var number in EnumerateTransitionSettingsScope(part))
            {
                _partFadeInSeconds[number] = fadeIn;
            }
        }

        if (ResolveCheckedTag(TransitionTimeRadios) is { } fadeOut)
        {
            foreach (var number in EnumerateTransitionSettingsScope(part))
            {
                _partFadeOutSeconds[number] = fadeOut;
            }
        }

        if (ResolveCheckedTag(FadeInGroupRadios) is { } groupFade)
        {
            foreach (var number in EnumerateTransitionSettingsScope(part))
            {
                _partGroupFadeSeconds[number] = groupFade;
            }
        }

        AutosaveCurrentProject();
        SaveLastWaveSessionIfLoaded();
    }

    private void ExitSourceRadio_CheckedChanged(FlatOptionRadioButton radio, bool isChangeOccursAt)
    {
        if (_suppressProjectUiEvents || _populatingPlaylistChoices || TagToExitSource(radio) is not { } mode)
        {
            return;
        }

        var scope = _selectedPlaylistPartNumber is { } part
            ? EnumerateTransitionSettingsScope(part)
            : Enumerable.Empty<int>();
        foreach (var number in scope)
        {
            if (isChangeOccursAt)
            {
                _partChangeOccursAtModes[number] = mode;
            }
            else
            {
                _partExitSourceModes[number] = mode;
            }
        }

        AutosaveCurrentProject();
        SaveLastWaveSessionIfLoaded();
    }

    /// <summary>選択パートと同一グループのパート番号を返す（グループ未設定なら自分だけ）。</summary>
    private IEnumerable<int> EnumerateTransitionSettingsScope(int partNumber)
    {
        if (!_partGroupIds.TryGetValue(partNumber, out var groupId))
        {
            yield return partNumber;
            yield break;
        }

        foreach (var pair in _partGroupIds)
        {
            if (pair.Value == groupId)
            {
                yield return pair.Key;
            }
        }
    }

    private void StorePlayPostExitForSelectedPart(bool enabled)
    {
        if (_selectedPlaylistPartNumber is not { } part)
        {
            return;
        }

        StorePlayPostExit(part, enabled);
    }

    private void StorePlayPostExit(int partNumber, bool enabled)
    {
        foreach (var number in EnumerateTransitionSettingsScope(partNumber))
        {
            _partPlayPostExit[number] = enabled;
        }
    }

    private void StoreAdditiveLayersForSelectedPart(bool enabled)
    {
        if (_selectedPlaylistPartNumber is not { } part)
        {
            return;
        }

        foreach (var number in EnumerateTransitionSettingsScope(part))
        {
            _partAdditiveLayers[number] = enabled;
        }
    }

    private void ClearPlaylistChoices(string? statusMessage = null)
    {
        playlistListLayout.Children.Clear();
        if (!string.IsNullOrEmpty(statusMessage))
        {
            AddPlaylistStatusLabel(statusMessage);
        }

        _playlistButtons.Clear();
        _disabledPartNumbers.Clear();
        _partGroupIds.Clear();
        _groupColorIndexes.Clear();
        _partExitSourceModes.Clear();
        _partChangeOccursAtModes.Clear();
        _partFadeInSeconds.Clear();
        _partFadeOutSeconds.Clear();
        _partFadeInCurves.Clear();
        _partFadeOutCurves.Clear();
        _partGroupFadeSeconds.Clear();
        _partPlayPostExit.Clear();
        _partAdditiveLayers.Clear();
        _selectedPlaylistPartNumber = null;
        _hoveredPlaylistPartNumber = null;
        _hoveredPlaylistListPartNumber = null;
        _lastAutoScrolledPlaylistPartNumber = null;
        _nextGroupId = 1;
        _nextColorIndex = 0;
        _playlistGroupPaintStickyGroupId = null;
        _playlistGroupPaintSeedPartNumber = null;
        waveformView.ClearExportHighlight();
        waveformView.SetPlaylistHoverHighlight(null);
        EndPlaylistGroupPaint();
        EndPlaylistDisablePaint();
    }

    /// <summary>波形が空のときに一覧へ出すステータス表示（Form1 AddPlaylistStatusLabel 相当）。</summary>
    private void AddPlaylistStatusLabel(string message)
    {
        playlistListLayout.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = UiColors.Brush(UiColors.PrimaryFore),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Height = DesignMetrics.FlatOptionRowHeight,
            Padding = new Thickness(DesignMetrics.From96(2), 0, DesignMetrics.From96(2), 0),
            Margin = new Thickness(
                DesignMetrics.PlaylistItemIndent,
                DesignMetrics.From96(1),
                DesignMetrics.From96(3),
                DesignMetrics.From96(1)),
        });
    }

    /// <summary>波形読込・グループ変更・有効無効切替後に、プレイリスト行 UI を再構築する。</summary>
    private void RefreshPlaylistButtons()
    {
        if (_previewSession is null)
        {
            return;
        }

        _populatingPlaylistChoices = true;
        try
        {
            _hoveredPlaylistListPartNumber = null;
            waveformView.SetPlaylistHoverHighlight(null);
            playlistListLayout.Children.Clear();
            _playlistButtons.Clear();
            var parts = _previewSession.EffectiveOutputParts;
            var displayNames = BuildPlaylistDisplayNameMap(parts);
            var groupColors = new Dictionary<int, Color>();

            foreach (var part in parts)
            {
                _partFadeInSeconds.TryAdd(part.Number, 0d);
                _partFadeOutSeconds.TryAdd(part.Number, 0d);
                _partGroupFadeSeconds.TryAdd(part.Number, 0d);
                _partFadeInCurves.TryAdd(part.Number, _appSettings.DefaultPlaylistFadeInCurve);
                _partFadeOutCurves.TryAdd(part.Number, _appSettings.DefaultPlaylistFadeOutCurve);
                _partExitSourceModes.TryAdd(part.Number, PlaylistExitSourceMode.Immediate);
                _partChangeOccursAtModes.TryAdd(part.Number, PlaylistExitSourceMode.Immediate);
                _partPlayPostExit.TryAdd(part.Number, true);

                if (!displayNames.TryGetValue(part.Number, out var displayName)
                    || string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = Path.GetFileNameWithoutExtension(part.FileName);
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = part.FileName;
                    }
                }

                var rowH = DesignMetrics.FlatOptionRowHeight;
                // Form1 TableLayout: スウォッチ Auto + ボタン Percent(100%)。
                // 固定 Width だと列幅よりボタンが広く、右枠が ScrollViewer に欠ける。
                var rowMarginY = DesignMetrics.From96(1);
                var row = new Grid
                {
                    Height = rowH + rowMarginY * 2d,
                    Margin = new Thickness(0),
                    Tag = part.Number,
                    ClipToBounds = false,
                    Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                });
                var swatch = new PlaylistGroupSwatch
                {
                    Tag = part.Number,
                    Fill = TryGetPlaylistGroupColor(part.Number),
                    Height = rowH,
                    VerticalAlignment = VerticalAlignment.Center,
                    // Form1: Margin = Padding(PlaylistItemIndent, 1, 0, 1)
                    Margin = new Thickness(DesignMetrics.PlaylistItemIndent, rowMarginY, 0, rowMarginY),
                    Cursor = Cursors.Hand,
                };
                if (swatch.Fill is { } fill)
                {
                    groupColors[part.Number] = fill;
                }

                var button = new FlatPlaylistButton
                {
                    Content = displayName,
                    Tag = part.Number,
                    Height = rowH,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    // Form1 FlatPlaylistButton.Margin = (3,1,3,1)
                    Margin = DesignMetrics.FlatOptionControlMargin,
                    ClipToBounds = false,
                    // 無効でもドラッグ塗りのヒット対象にするため IsEnabled は落とさない。
                    // 無効表示は Opacity ではなく Foreground=LogError（Form1 同等）。
                };
                button.ApplyIdleStyle();
                if (_disabledPartNumbers.Contains(part.Number))
                {
                    button.Foreground = UiColors.Brush(UiColors.LogError);
                }

                button.Click += PlaylistButton_Click;
                button.MouseRightButtonUp += (_, _) => ToggleDisabledPart(part.Number);
                WirePlaylistPaintHandlers(button);
                WirePlaylistPaintHandlers(swatch);
                WirePlaylistHoverHandlers(button);
                WirePlaylistHoverHandlers(swatch);
                TipService.Set(button, BuildPlaylistGroupTip(part));
                TipService.Set(swatch, BuildPlaylistGroupTip(part));

                Grid.SetColumn(swatch, 0);
                Grid.SetColumn(button, 1);
                row.Children.Add(swatch);
                row.Children.Add(button);
                playlistListLayout.Children.Add(row);
                _playlistButtons[part.Number] = button;
            }

            var enabledGroups = BuildEnabledPartGroupIds();
            waveformView.SetPlaylistDisplayNames(displayNames, enabledGroups, groupColors);
            waveformView.SetDisabledPlaylistParts(_disabledPartNumbers);
            UpdateLayerMusicOptionEnabled();
            UpdateCompactFileNumbersEnabled();

            if (_selectedPlaylistPartNumber is null || !parts.Any(p => p.Number == _selectedPlaylistPartNumber))
            {
                _selectedPlaylistPartNumber = parts.Count > 0 ? parts[0].Number : null;
            }

            if (_selectedPlaylistPartNumber is { } selected)
            {
                SelectPlaylistPart(selected, seekAndPlay: false);
            }

            ApplyPlaylistButtonColors();
            AlignCompactFileNumbersCheckBox();
            QueuePlaylistSelectorWidthUpdate();
        }
        finally
        {
            _populatingPlaylistChoices = false;
        }
    }

    /// <summary>
    /// Compact Num. のチェック枠左端を、プレイリスト行のグループ枠
    /// （左マージン = <see cref="DesignMetrics.PlaylistItemIndent"/>）に揃える。
    /// チェック枠は Padding.Left から約 From96(3) 内側に描かれるため差し引く（Form1 同等）。
    /// </summary>
    private void AlignCompactFileNumbersCheckBox()
    {
        var glyphInset = DesignMetrics.From96(3);
        compactFileNumbersCheckBox.Margin = new Thickness(0);
        compactFileNumbersCheckBox.Padding = new Thickness(
            Math.Max(0d, DesignMetrics.PlaylistItemIndent - glyphInset),
            0,
            0,
            0);
    }

    private void RefreshPlaylistLocalizedText()
    {
        if (_playlistButtons.Count == 0)
        {
            return;
        }

        RefreshPlaylistButtons();
    }

    private void ApplyPlaylistItemTips()
    {
        if (_previewSession is null)
        {
            return;
        }

        foreach (var part in _previewSession.EffectiveOutputParts)
        {
            if (_playlistButtons.TryGetValue(part.Number, out var button))
            {
                TipService.Set(button, BuildPlaylistGroupTip(part));
            }
        }

        foreach (var child in playlistListLayout.Children.OfType<Panel>())
        {
            foreach (var swatch in child.Children.OfType<PlaylistGroupSwatch>())
            {
                if (swatch.Tag is int partNumber
                    && TryGetOutputPart(partNumber) is { } part)
                {
                    TipService.Set(swatch, BuildPlaylistGroupTip(part));
                }
            }
        }
    }

    private string BuildPlaylistGroupTip(WaveformOutputPart part)
    {
        var name = _playlistButtons.TryGetValue(part.Number, out var button) && button.Content is string text
            ? text
            : Path.GetFileNameWithoutExtension(part.FileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = part.FileName;
        }

        return UiStrings.TipPlaylistItem(name, _partAdditiveLayers.GetValueOrDefault(part.Number, false));
    }

    /// <summary>
    /// Form1 UpdatePlaylistDisplayNames 相当。無効パートは Excluded Region N、
    /// 有効パートはグループ／Compact 規則に従った表示名にする。
    /// </summary>
    private void UpdatePlaylistDisplayNames(
        IReadOnlyList<WaveformOutputPart>? parts = null,
        bool updateWaveform = true)
    {
        parts ??= GetEffectiveOutputParts();
        var names = BuildPlaylistDisplayNameMap(parts);

        foreach (var (partNumber, button) in _playlistButtons)
        {
            if (!names.TryGetValue(partNumber, out var name))
            {
                var part = parts.FirstOrDefault(p => p.Number == partNumber);
                name = part.Number == partNumber
                    ? Path.GetFileNameWithoutExtension(part.FileName)
                    : partNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(name) && part.Number == partNumber)
                {
                    name = part.FileName;
                }
            }

            button.Content = name;
            button.InvalidateVisual();
            if (TryGetOutputPart(partNumber) is { } tipPart)
            {
                TipService.Set(button, BuildPlaylistGroupTip(tipPart));
            }
        }

        foreach (var row in playlistListLayout.Children.OfType<Panel>())
        {
            if (row.Children.OfType<PlaylistGroupSwatch>().FirstOrDefault() is not { Tag: int partNumber } swatch)
            {
                continue;
            }

            swatch.Fill = TryGetPlaylistGroupColor(partNumber);
            if (TryGetOutputPart(partNumber) is { } tipPart)
            {
                TipService.Set(swatch, BuildPlaylistGroupTip(tipPart));
            }
        }

        if (updateWaveform)
        {
            waveformView.SetPlaylistDisplayNames(
                names,
                BuildEnabledPartGroupIds(),
                BuildPlaylistGroupColorMap());
            waveformView.SetDisabledPlaylistParts(_disabledPartNumbers);
        }

        QueuePlaylistSelectorWidthUpdate();
    }

    private Dictionary<int, string> BuildPlaylistDisplayNameMap(IReadOnlyList<WaveformOutputPart> parts)
    {
        var sourcePath = _loadedPreview?.SourcePath;
        Dictionary<int, string> names;
        if (string.IsNullOrEmpty(sourcePath))
        {
            names = new Dictionary<int, string>();
            foreach (var part in parts)
            {
                names[part.Number] = compactFileNumbersCheckBox.IsChecked == true
                    ? part.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : Path.GetFileNameWithoutExtension(part.FileName);
            }
        }
        else
        {
            var namingSourcePath = BuildNamingSourcePath(sourcePath);
            var enabledParts = BuildProjectedEnabledParts(parts, namingSourcePath);
            var enabledGroups = BuildEnabledPartGroupIds();
            var nameOverrides = BuildPlaylistNameOverrides(enabledParts);
            names = enabledParts.Length == 0
                ? new Dictionary<int, string>()
                : WwiseMusicPlanBuilder.BuildPlaylistDisplayNames(
                        namingSourcePath,
                        enabledParts,
                        enabledGroups,
                        nameOverrides)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);

            if (_loadedPreview?.IsMultiWaveOnly == true)
            {
                foreach (var part in enabledParts)
                {
                    if (nameOverrides.TryGetValue(part.Number, out var overrideName)
                        && !string.IsNullOrWhiteSpace(overrideName))
                    {
                        names[part.Number] = overrideName;
                    }
                }
            }
        }

        var excludedIndex = 0;
        foreach (var part in parts.OrderBy(part => part.StartSampleOffset).ThenBy(part => part.Number))
        {
            if (_disabledPartNumbers.Contains(part.Number))
            {
                names[part.Number] = UiStrings.LabelExcludedRegion(++excludedIndex);
            }
        }

        return names;
    }

    private WaveformOutputPart[] BuildProjectedEnabledParts(
        IReadOnlyList<WaveformOutputPart> parts,
        string sourcePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "wave";
        }

        var enabled = parts
            .Where(part => !_disabledPartNumbers.Contains(part.Number))
            .OrderBy(part => part.StartSampleOffset)
            .ThenBy(part => part.Number)
            .ToArray();
        var projected = new WaveformOutputPart[enabled.Length];
        var multiWave = _loadedPreview?.IsMultiWaveOnly == true;
        for (var i = 0; i < enabled.Length; i++)
        {
            var part = enabled[i];
            if (multiWave)
            {
                projected[i] = part;
                continue;
            }

            var fileNumber = compactFileNumbersCheckBox.IsChecked == true ? i + 1 : part.Number;
            var partBaseName = !string.IsNullOrEmpty(part.SourcePath)
                ? Path.GetFileNameWithoutExtension(part.SourcePath)
                : baseName;
            if (string.IsNullOrEmpty(partBaseName))
            {
                partBaseName = baseName;
            }

            projected[i] = part with { FileName = $"{partBaseName}_{fileNumber}.wav" };
        }

        return projected;
    }

    private string BuildNamingSourcePath(string sourcePath)
    {
        var baseName = _sourceBaseNameOverride;
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return sourcePath;
        }

        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        return Path.Combine(directory, baseName + Path.GetExtension(sourcePath));
    }

    private Dictionary<int, string> BuildPlaylistNameOverrides(
        IReadOnlyList<WaveformOutputPart> enabledParts)
    {
        if (_loadedPreview?.IsMultiWaveOnly == true)
        {
            return enabledParts.ToDictionary(
                part => part.Number,
                part =>
                {
                    var name = Path.GetFileNameWithoutExtension(part.FileName);
                    return string.IsNullOrWhiteSpace(name) ? part.FileName : name;
                });
        }

        if (_disabledPartNumbers.Count == 0
            || compactFileNumbersCheckBox.IsChecked == true)
        {
            return [];
        }

        return enabledParts.ToDictionary(
            part => part.Number,
            part => Path.GetFileNameWithoutExtension(part.FileName));
    }

    private void PlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        if (_populatingPlaylistChoices || sender is not FlatPlaylistButton { Tag: int partNumber })
        {
            return;
        }

        if (_suppressNextPlaylistClick)
        {
            _suppressNextPlaylistClick = false;
            return;
        }

        if (_disabledPartNumbers.Contains(partNumber))
        {
            return;
        }

        SelectPlaylistPart(partNumber, seekAndPlay: true);
    }

    private void WirePlaylistHoverHandlers(FrameworkElement target)
    {
        target.MouseEnter += PlaylistButton_MouseEnter;
        target.MouseLeave += PlaylistButton_MouseLeave;
    }

    private void PlaylistButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int partNumber })
        {
            return;
        }

        _hoveredPlaylistListPartNumber = partNumber;
        waveformView.SetPlaylistHoverHighlight(partNumber);
        ApplyPlaylistButtonColors();
    }

    private void PlaylistButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int partNumber }
            || _hoveredPlaylistListPartNumber != partNumber)
        {
            return;
        }

        _hoveredPlaylistListPartNumber = null;
        waveformView.SetPlaylistHoverHighlight(null);
        ApplyPlaylistButtonColors();
    }

    private void AssignPlaylistPartToGroup(int partNumber, int groupId)
    {
        if (_disabledPartNumbers.Contains(partNumber))
        {
            return;
        }

        if (_partGroupIds.TryGetValue(partNumber, out var previous) && previous == groupId)
        {
            return;
        }

        if (_partGroupIds.TryGetValue(partNumber, out var oldGroupId))
        {
            _partGroupIds.Remove(partNumber);
            DiscardPlaylistGroupIfEmpty(oldGroupId);
        }

        _partGroupIds[partNumber] = groupId;
        if (!_groupColorIndexes.ContainsKey(groupId))
        {
            _groupColorIndexes[groupId] = _nextColorIndex++;
        }

        SyncTransitionSettingsForGroup(groupId);
        // Form1 同等: 再生中パートがグループに入ったら Group / Chg Occ At を即有効化。
        UpdateGroupFadeRadioEnabled();
        if (!_playlistGroupPaintActive && !_playlistDisablePaintActive)
        {
            UpdateLayerMusicOptionEnabled();
        }
    }

    private void RemovePlaylistPartFromGroup(int partNumber)
    {
        if (!_partGroupIds.Remove(partNumber, out var groupId))
        {
            return;
        }

        DiscardPlaylistGroupIfEmpty(groupId);
        UpdateGroupFadeRadioEnabled();
        if (!_playlistGroupPaintActive && !_playlistDisablePaintActive)
        {
            UpdateLayerMusicOptionEnabled();
        }
    }

    private void DiscardPlaylistGroupIfEmpty(int groupId)
    {
        if (_partGroupIds.Values.Any(id => id == groupId))
        {
            return;
        }

        _groupColorIndexes.Remove(groupId);
    }

    /// <summary>
    /// Wave 単体モードでは小節／拍情報がないため Next Bar / Next Beat を使わない
    /// （Form1 UpdateWaveOnlyExitSourceOptionsEnabled 同等）。
    /// </summary>
    private void UpdateWaveOnlyExitSourceOptionsEnabled()
    {
        var waveOnly = _previewSession?.AllowsSessionMarkerEdit == true;
        exitSourceNextBarRadio.IsEnabled = !waveOnly;
        exitSourceNextBeatRadio.IsEnabled = !waveOnly;

        if (waveOnly
            && (exitSourceNextBarRadio.IsChecked == true || exitSourceNextBeatRadio.IsChecked == true))
        {
            var suppressed = _suppressProjectUiEvents;
            _suppressProjectUiEvents = true;
            try
            {
                SelectExitSourceRadio(ExitSourceRadios, PlaylistExitSourceMode.Immediate);
            }
            finally
            {
                _suppressProjectUiEvents = suppressed;
            }

            if (_selectedPlaylistPartNumber is int partNumber)
            {
                foreach (var number in EnumerateTransitionSettingsScope(partNumber))
                {
                    _partExitSourceModes[number] = PlaylistExitSourceMode.Immediate;
                }
            }
        }

        // Change Occurs At 側の wave-only 制約は Group 有効状態と合わせて更新する。
        UpdateGroupFadeRadioEnabled();
    }

    /// <summary>
    /// Fade In / Fade Out の保存値をラジオの選択肢（None / 0.5 / 1 / 3 / 6 秒）の最寄りへ丸める。
    /// 旧バージョンで保存した 9.0 など、選択肢に無い値と UI 表示の食い違いを防ぐ。
    /// Group Fade には適用しない。
    /// </summary>
    private static double NormalizeTransitionFadeSeconds(double seconds)
    {
        double[] choices = [0d, 0.5d, 1d, 3d, 6d];
        var best = choices[0];
        foreach (var choice in choices)
        {
            if (Math.Abs(choice - seconds) < Math.Abs(best - seconds))
            {
                best = choice;
            }
        }

        return best;
    }

    /// <summary>セッション復元後などに全グループの遷移設定をリーダー値で揃える（Form1 同等）。</summary>
    private void SyncTransitionSettingsAcrossAllGroups()
    {
        foreach (var groupId in _partGroupIds.Values.Distinct().ToArray())
        {
            SyncTransitionSettingsForGroup(groupId);
        }
    }

    private void SyncTransitionSettingsForGroup(int groupId)
    {
        var members = _partGroupIds
            .Where(pair => pair.Value == groupId)
            .Select(pair => pair.Key)
            .OrderBy(number => number)
            .ToArray();
        if (members.Length < 2)
        {
            return;
        }

        var leader = members[0];
        var exit = _partExitSourceModes.GetValueOrDefault(leader, PlaylistExitSourceMode.Immediate);
        var playPostExit = _partPlayPostExit.GetValueOrDefault(leader, true);
        var additiveLayers = _partAdditiveLayers.GetValueOrDefault(leader, false);
        var fadeIn = _partFadeInSeconds.GetValueOrDefault(leader);
        var fadeOut = _partFadeOutSeconds.GetValueOrDefault(leader);
        var fadeInCurve = _partFadeInCurves.GetValueOrDefault(leader, _appSettings.DefaultPlaylistFadeInCurve);
        var fadeOutCurve = _partFadeOutCurves.GetValueOrDefault(leader, _appSettings.DefaultPlaylistFadeOutCurve);
        foreach (var member in members)
        {
            _partExitSourceModes[member] = exit;
            _partPlayPostExit[member] = playPostExit;
            _partAdditiveLayers[member] = additiveLayers;
            _partFadeInSeconds[member] = fadeIn;
            _partFadeOutSeconds[member] = fadeOut;
            _partFadeInCurves[member] = fadeInCurve;
            _partFadeOutCurves[member] = fadeOutCurve;
        }
    }

    private Color? TryGetPlaylistGroupColor(int partNumber)
    {
        if (!_partGroupIds.TryGetValue(partNumber, out var groupId)
            || _disabledPartNumbers.Contains(partNumber))
        {
            return null;
        }

        if (!_groupColorIndexes.TryGetValue(groupId, out var colorIndex))
        {
            colorIndex = _nextColorIndex % GroupColorPalette.Length;
            _groupColorIndexes[groupId] = colorIndex;
            _nextColorIndex++;
        }

        return GroupColorPalette[colorIndex % GroupColorPalette.Length];
    }

    private Dictionary<int, Color> BuildPlaylistGroupColorMap()
    {
        var map = new Dictionary<int, Color>();
        foreach (var partNumber in _partGroupIds.Keys)
        {
            if (TryGetPlaylistGroupColor(partNumber) is { } color)
            {
                map[partNumber] = color;
            }
        }

        return map;
    }

    private Dictionary<int, int> BuildEnabledPartGroupIds() =>
        _partGroupIds
            .Where(pair => !_disabledPartNumbers.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    private void ApplyPlaylistGroupMarkerSharing()
    {
        UpdateLayerMusicOptionEnabled();
        if (_previewSession is not { } session)
        {
            return;
        }

        session.SetDisabledPartNumbers(_disabledPartNumbers);
        session.SetPlaylistGroups(BuildEnabledPartGroupIds());
        waveformView.SetMarkers(session.EffectiveMarkers);
    }

    private void SetPlaylistPartDisabled(int partNumber, bool disabled)
    {
        var changed = disabled
            ? _disabledPartNumbers.Add(partNumber)
            : _disabledPartNumbers.Remove(partNumber);
        if (!changed)
        {
            return;
        }

        if (disabled)
        {
            CancelPlaybackForDisabledPart(partNumber);
            RemovePlaylistPartFromGroup(partNumber);
        }

        // Form1 同等: 無効化のたびに UI（赤文字・波形）を即時更新する。
        ApplyPlaylistDisableUi();
        if (!_playlistDisablePaintActive)
        {
            AutosaveCurrentProject();
            SaveLastWaveSessionIfLoaded();
        }
    }

    /// <summary>
    /// 再生中・予約中のパートを無効化されたら、そのパートの再生／遷移予約を止める（Form1 同等）。
    /// </summary>
    private void CancelPlaybackForDisabledPart(int partNumber)
    {
        if (_pendingOverlayPartNumber == partNumber)
        {
            ClearPendingOverlay();
        }

        if (_requestedPlaylistPartNumber == partNumber)
        {
            _audioPlayer.CancelPlaylistTransition();
            ClearPendingPlaylistUiTransition();
            _requestedPlaylistPartNumber = null;
        }

        if (_activeAutomaticPlaylistPartNumber == partNumber
            || _manualPlaylistPartNumber == partNumber)
        {
            _audioPlayer.CancelPlaylistTransition();
            ClearPendingPlaylistUiTransition();
            if (_audioPlayer.IsPlaying)
            {
                _audioPlayer.Stop();
                UpdateTransportPlaybackState();
                _playheadTimer.Stop();
            }

            ClearPlaylistPlaybackSelection();
        }
    }

    private void ApplyPlaylistDisableUi()
    {
        if (_previewSession is { } session)
        {
            session.SetDisabledPartNumbers(_disabledPartNumbers);
            session.SetPlaylistGroups(BuildEnabledPartGroupIds());
            waveformView.SetMarkers(session.EffectiveMarkers);
        }

        if (_loadedPreview is not null)
        {
            UpdatePlaylistDisplayNames(GetEffectiveOutputParts());
        }
        else
        {
            waveformView.SetDisabledPlaylistParts(_disabledPartNumbers);
            ApplyPlaylistGroupColorsOnly();
        }

        ApplyPlaylistButtonColors();
        UpdateExportEnabled();
        UpdateCompactFileNumbersEnabled();
        UpdateLayerMusicOptionEnabled();
    }

    private void UpdateLayerMusicOptionEnabled()
    {
        var hasEffectiveGroup = BuildEnabledPartGroupIds()
            .GroupBy(pair => pair.Value)
            .Any(group => group.Count() >= 2);
        markerOptionsPanel.SetLayerMusicOptionEnabled(hasEffectiveGroup);
        UpdateAdditiveLayersOptionEnabled();
    }

    private void UpdateAdditiveLayersOptionEnabled()
    {
        var enabled = _selectedPlaylistPartNumber is { } part
            && _partGroupIds.TryGetValue(part, out var groupId)
            && !_disabledPartNumbers.Contains(part)
            && _partGroupIds.Count(pair => pair.Value == groupId && !_disabledPartNumbers.Contains(pair.Key)) >= 2;
        additiveLayersCheckBox.IsEnabled = enabled;
        if (!enabled && additiveLayersCheckBox.IsChecked == true && !_suppressProjectUiEvents)
        {
            // グループ解消時は見た目だけ戻す（保存は呼び出し側）。
            _suppressProjectUiEvents = true;
            try
            {
                additiveLayersCheckBox.IsChecked = false;
            }
            finally
            {
                _suppressProjectUiEvents = false;
            }
        }
    }

    private void UpdateCompactFileNumbersEnabled()
    {
        var enabled = _disabledPartNumbers.Count > 0;
        if (compactFileNumbersCheckBox.IsEnabled == enabled)
        {
            return;
        }

        compactFileNumbersCheckBox.IsEnabled = enabled;
    }

    private void ToggleDisabledPart(int partNumber)
    {
        SetPlaylistPartDisabled(partNumber, !_disabledPartNumbers.Contains(partNumber));
        ApplyPlaylistDisableUi();
        AutosaveCurrentProject();
        SaveLastWaveSessionIfLoaded();
    }

    private void PersistPlaylistGroupsToSession() =>
        _previewSession?.SetPlaylistGroups(BuildEnabledPartGroupIds());

    private void SelectPlaylistPart(int partNumber, bool seekAndPlay)
    {
        _selectedPlaylistPartNumber = partNumber;
        // SetExportHighlight は EXPORT 中パートのパルス発光専用。選択ハイライトに流用しない
        //（WinForms でも選択時は呼んでいない。誤用すると約 1.1 秒周期で点滅する）。
        waveformView.ClearExportHighlight();

        _suppressProjectUiEvents = true;
        try
        {
            SelectFadeRadio(FadeInRadios, _partFadeInSeconds.GetValueOrDefault(partNumber));
            SelectFadeRadio(TransitionTimeRadios, _partFadeOutSeconds.GetValueOrDefault(partNumber));
            SelectFadeRadio(FadeInGroupRadios, _partGroupFadeSeconds.GetValueOrDefault(partNumber));
            SelectExitSourceRadio(ExitSourceRadios, _partExitSourceModes.GetValueOrDefault(partNumber, PlaylistExitSourceMode.Immediate));
            SelectExitSourceRadio(ChangeOccursRadios, _partChangeOccursAtModes.GetValueOrDefault(partNumber, PlaylistExitSourceMode.Immediate));
            playMinusECheckBox.IsChecked = _partPlayPostExit.GetValueOrDefault(partNumber, true);
            additiveLayersCheckBox.IsChecked = _partAdditiveLayers.GetValueOrDefault(partNumber, false);
            UpdateAdditiveLayersOptionEnabled();
        }
        finally
        {
            _suppressProjectUiEvents = false;
        }

        if (!seekAndPlay || _previewSession is null)
        {
            ApplyPlaylistButtonColors();
            return;
        }

        var part = TryGetOutputPart(partNumber);
        if (part is null)
        {
            return;
        }

        if (ShouldUseAdditiveLayerClick(part.Value))
        {
            RequestPlaylistOverlayToggle(part.Value);
        }
        else
        {
            RequestPlaylistPlayback(part.Value);
        }
    }
}
