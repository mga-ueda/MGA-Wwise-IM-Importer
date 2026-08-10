namespace MgaWwiseIMImporter.UI;

/// <summary>Last Session（グループ・無効化・トランジション設定・マーカー）の保存と復元。</summary>
public partial class MainWindow
{
    private void SaveLastWaveSessionIfLoaded()
    {
        if (_closing
            || _creatingNewProject
            || _loadedPreview is null
            || _previewSession is null
            || !_projectStore.ContainsName(_loadedProjectName))
        {
            return;
        }

        try
        {
            var wavePaths = _loadedPreview.IsMultiWaveOnly
                ? _loadedPreview.SourceSpans.Select(s => s.Path).ToArray()
                : null;
            var state = LastWaveSessionState.Capture(
                _loadedPreview.SourcePath,
                _previewSession.EffectiveOutputParts,
                _partGroupIds,
                _groupColorIndexes,
                _nextGroupId,
                _nextColorIndex,
                _previewSession.GetUserMarkerSampleOffsets(),
                _disabledPartNumbers,
                _partExitSourceModes,
                _partChangeOccursAtModes,
                _partFadeInSeconds,
                _partFadeOutSeconds,
                _partFadeInCurves,
                _partFadeOutCurves,
                _partGroupFadeSeconds,
                _partPlayPostExit,
                _partAdditiveLayers,
                _previewSession.GetWaveOnlySessionMarkers(),
                _previewSession.RegionEdgeFades,
                wavePaths,
                _sourceBaseNameOverride);
            _projectStore.SaveLastWaveSession(_loadedProjectName, state);
        }
        catch
        {
            // セッション保存失敗は作業を止めない。
        }
    }

    /// <summary>読み込み済み波形パスを Keep Last Session 用フィールドへ反映する。</summary>
    private void RememberLoadedWavePaths(WaveformPreviewData preview)
    {
        var loaded = LastWaveSessionState.GetLoadedWavePaths(preview);
        _lastWavePaths = loaded;
        _lastWavePath = loaded.Count > 0 ? loaded[0] : string.Empty;
    }

    /// <summary>設定／セッション JSON から復元用パス一覧を得る。</summary>
    private IReadOnlyList<string> ResolveLastWavePathsForRestore()
    {
        if (_lastWavePaths.Count > 0)
        {
            return _lastWavePaths;
        }

        if (_projectStore.TryReadLastWaveSession(_loadedProjectName, out var state)
            && state is not null)
        {
            var fromSession = state.GetWavePaths();
            if (fromSession.Count > 0)
            {
                return fromSession;
            }
        }

        return ResolveStoredLastWavePaths(_lastWavePath, joinedPaths: null);
    }

    private static IReadOnlyList<string> ResolveStoredLastWavePaths(
        string? primaryPath,
        string? joinedPaths)
    {
        var fromJoined = LastWaveSessionState.SplitWavePaths(joinedPaths);
        if (fromJoined.Count > 0)
        {
            return fromJoined;
        }

        if (string.IsNullOrWhiteSpace(primaryPath))
        {
            return [];
        }

        try
        {
            return [Path.GetFullPath(primaryPath.Trim())];
        }
        catch
        {
            return [primaryPath.Trim()];
        }
    }

    /// <summary>復元したセッション状態を、いま読み込んだ preview セッションへ適用する。</summary>
    private void RestoreSessionIntoPreview(LastWaveSessionState state)
    {
        if (_previewSession is null || !state.MatchesLoadedWave(_loadedPreview!))
        {
            return;
        }

        _sourceBaseNameOverride = state.SourceBaseNameOverride;
        _disabledPartNumbers.Clear();
        foreach (var number in state.DisabledPartNumbers)
        {
            _disabledPartNumbers.Add(number);
        }

        _previewSession.SetDisabledPartNumbers(_disabledPartNumbers);

        if (state.TryGetPartGroupIds(out var partGroupIds))
        {
            _partGroupIds.Clear();
            foreach (var pair in partGroupIds)
            {
                _partGroupIds[pair.Key] = pair.Value;
            }

            _previewSession.SetPlaylistGroups(_partGroupIds);
        }

        if (state.TryGetGroupColorIndexes(out var groupColorIndexes))
        {
            _groupColorIndexes.Clear();
            foreach (var pair in groupColorIndexes)
            {
                _groupColorIndexes[pair.Key] = pair.Value;
            }
        }

        _nextGroupId = Math.Max(1, state.NextGroupId);
        _nextColorIndex = Math.Max(0, state.NextColorIndex);

        if (state.TryGetPartExitSourceModes(out var exitModes))
        {
            _partExitSourceModes.Clear();
            foreach (var pair in exitModes)
            {
                _partExitSourceModes[pair.Key] = pair.Value;
            }
        }

        if (state.TryGetPartChangeOccursAtModes(out var changeModes))
        {
            _partChangeOccursAtModes.Clear();
            foreach (var pair in changeModes)
            {
                _partChangeOccursAtModes[pair.Key] = pair.Value;
            }
        }

        if (state.TryGetPartFadeSeconds(out var fadeIn, out var fadeOut, out var groupFade))
        {
            // 保存値がラジオの選択肢に無い場合は最寄りへ丸める（Group Fade には適用しない）。
            _partFadeInSeconds.Clear();
            foreach (var pair in fadeIn)
            {
                _partFadeInSeconds[pair.Key] = NormalizeTransitionFadeSeconds(pair.Value);
            }

            _partFadeOutSeconds.Clear();
            foreach (var pair in fadeOut)
            {
                _partFadeOutSeconds[pair.Key] = NormalizeTransitionFadeSeconds(pair.Value);
            }

            _partGroupFadeSeconds.Clear();
            foreach (var pair in groupFade)
            {
                _partGroupFadeSeconds[pair.Key] = pair.Value;
            }
        }

        if (state.TryGetPartFadeCurves(out var fadeInCurves, out var fadeOutCurves))
        {
            _partFadeInCurves.Clear();
            foreach (var pair in fadeInCurves)
            {
                _partFadeInCurves[pair.Key] = pair.Value;
            }

            _partFadeOutCurves.Clear();
            foreach (var pair in fadeOutCurves)
            {
                _partFadeOutCurves[pair.Key] = pair.Value;
            }
        }

        if (state.TryGetPartPlayPostExit(out var playPostExit))
        {
            _partPlayPostExit.Clear();
            foreach (var pair in playPostExit)
            {
                _partPlayPostExit[pair.Key] = pair.Value;
            }
        }

        if (state.TryGetPartAdditiveLayers(out var additiveLayers))
        {
            _partAdditiveLayers.Clear();
            foreach (var pair in additiveLayers)
            {
                _partAdditiveLayers[pair.Key] = pair.Value;
            }
        }

        if (state.UserMarkerSampleOffsets.Count > 0)
        {
            _previewSession.AddMarkers(state.UserMarkerSampleOffsets);
        }

        if (state.WaveOnlySessionMarkers is { } waveOnlyMarkers)
        {
            var markers = waveOnlyMarkers
                .Select(m => new WaveformMarkerMark(m.SampleOffset, m.Comment, IsFromWaveEmbedded: m.IsFromWaveEmbedded))
                .ToList();
            _previewSession.TryReplaceWaveOnlySessionMarkers(markers);
        }

        if (state.RegionEdgeFades.Count > 0)
        {
            var fades = state.RegionEdgeFades
                .Select(f => new RegionEdgeFade(
                    f.InSample,
                    f.OutSample,
                    f.FadeInEndSample,
                    f.FadeOutStartSample,
                    Enum.TryParse<RegionFadeCurveKind>(f.FadeInCurve, ignoreCase: true, out var fadeInCurve)
                        ? fadeInCurve
                        : RegionEdgeFade.BuiltinWaveformFadeInCurve,
                    Enum.TryParse<RegionFadeCurveKind>(f.FadeOutCurve, ignoreCase: true, out var fadeOutCurve)
                        ? fadeOutCurve
                        : RegionEdgeFade.BuiltinWaveformFadeOutCurve))
                .ToList();
            _previewSession.SetRegionEdgeFades(fades);
        }

        // グループ内の遷移設定はリーダー値で全メンバーへ揃える（Form1 同等）。
        SyncTransitionSettingsAcrossAllGroups();
    }

    /// <summary>
    /// プロジェクト起動・切替時、KeepLastSession が有効なら直前の波形を自動で読み込む。
    /// 読み込みを開始した場合は true（起動時すりガラスの解除を呼び出し側に委ねる）。
    /// </summary>
    private async Task<bool> RestoreKeepLastSessionAsync()
    {
        if (keepLastSessionCheckBox.IsChecked != true || _creatingNewProject)
        {
            return false;
        }

        var candidatePaths = ResolveLastWavePathsForRestore();
        if (candidatePaths.Count == 0)
        {
            return false;
        }

        var existingPaths = new List<string>(candidatePaths.Count);
        foreach (var path in candidatePaths)
        {
            string wavPath;
            try
            {
                wavPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                AppendColoredLine(UiStrings.LogLastWaveBadPath(ex.Message));
                return false;
            }

            if (!File.Exists(wavPath))
            {
                AppendColoredLine(UiStrings.LogLastWaveMissing(wavPath));
                return false;
            }

            existingPaths.Add(wavPath);
        }

        LastWaveSessionState? captured = null;
        if (_projectStore.TryReadLastWaveSession(_loadedProjectName, out var state) && state is not null)
        {
            captured = state;
        }

        // MatchesLoadedWave は解析後に ProcessDroppedFilesAsync 内で照合する
        await ProcessDroppedFilesAsync(
                existingPaths,
                isLastSessionLoad: true,
                capturedSession: captured)
            .ConfigureAwait(true);

        return true;
    }
}
