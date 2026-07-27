using System.Text;
using MgaWwiseIMImporter.UI;
using MgaWwiseIMImporter.Wave;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.Processing;

/// <summary>
/// ドロップされた Wave（＋任意で同名 XML）を読み、波形プレビュー用データを返す。
/// </summary>
internal static class DroppedFilesProcessor
{
    public static string Process(IEnumerable<string> paths, out WaveformPreviewData? preview)
    {
        WaveformPreviewData? lastPreview = null;
        var report = ProcessCore(paths, p => lastPreview = p);
        preview = lastPreview;
        return report;
    }

    private static string ProcessCore(IEnumerable<string> paths, Action<WaveformPreviewData>? preview)
    {
        // 不正なパス 1 件で全体（async void 呼び出し元まで）を落とさず、そのパスだけエラーにする。
        var dropped = new List<string>();
        var invalid = new List<(string Path, Exception Error)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (seen.Add(fullPath))
                {
                    dropped.Add(fullPath);
                }
            }
            catch (Exception ex)
            {
                invalid.Add((path, ex));
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine(UiStrings.LogDroppedFilesHeader(dropped.Count));
        foreach (var file in dropped)
        {
            sb.AppendLine($"- {file}");
        }

        sb.AppendLine();

        foreach (var (path, error) in invalid)
        {
            AppendError(sb, path, error);
        }

        var pairKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pairs = new List<(string WavPath, string XmlPath)>();

        foreach (var path in dropped.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var extension = Path.GetExtension(path);
            if (!extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(UiStrings.LogErrorHeader);
                sb.AppendLine($"{UiStrings.KeyPath} {path}");
                sb.AppendLine(UiStrings.LogDropNeedWavOrXml);
                sb.AppendLine();
                continue;
            }

            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var baseName = Path.GetFileNameWithoutExtension(path);
            if (!WwiseObjectNames.TryValidateBaseName(baseName, out var rejectReason))
            {
                sb.AppendLine(UiStrings.LogErrorHeader);
                sb.AppendLine($"{UiStrings.KeyPath} {path}");
                sb.AppendLine(rejectReason switch
                {
                    WwiseBaseNameRejectReason.StartsWithDigit =>
                        UiStrings.LogDropNameStartsWithDigit(baseName),
                    WwiseBaseNameRejectReason.ReservedWindowsName =>
                        UiStrings.LogDropNameReservedWindows(baseName),
                    _ => UiStrings.LogDropNameInvalidFileName(
                        string.IsNullOrEmpty(baseName) ? "(empty)" : baseName),
                });
                sb.AppendLine();
                continue;
            }

            var pairKey = Path.Combine(directory, baseName);
            if (!pairKeys.Add(pairKey))
            {
                continue;
            }

            var wavPath = Path.Combine(directory, baseName + ".wav");
            var xmlPath = Path.Combine(directory, baseName + ".xml");
            pairs.Add((wavPath, xmlPath));
        }

        // XML なし・WAV 2 本以上 → 複数波形モード（既存単体／XML 経路には混ぜない）
        if (TryGetMultiWaveOnlyPaths(pairs, out var multiWavPaths))
        {
            try
            {
                var multiPreview = MultiWaveOnlyProcessor.TryBuild(multiWavPaths, sb);
                if (multiPreview is not null)
                {
                    preview?.Invoke(multiPreview);
                }
            }
            catch (Exception ex)
            {
                // ピーク読取・連結準備の失敗を単体経路（ProcessPair）と同様にログへ落とす。
                AppendError(sb, multiWavPaths[0], ex);
            }

            return sb.ToString();
        }

        // 複数ペアはログには全部出すが、プレビューは最後の 1 件だけ残る。
        var warnMultiplePairs = pairs.Count >= 2;
        var mixedXmlModes = warnMultiplePairs && HasMixedXmlPresence(pairs);
        var keptWavPath = warnMultiplePairs ? pairs[^1].WavPath : null;

        foreach (var (wavPath, xmlPath) in pairs)
        {
            ProcessPair(sb, wavPath, xmlPath, preview);
        }

        if (warnMultiplePairs && keptWavPath is not null)
        {
            sb.AppendLine(UiStrings.LogWarningHeader);
            sb.AppendLine(UiStrings.LogMultiplePairsPreviewDiscarded(pairs.Count, keptWavPath));
            if (mixedXmlModes)
            {
                sb.AppendLine(UiStrings.LogMultiplePairsMixedXmlModes);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 存在する WAV ペアのうち、同名 XML ありと無しが混在しているか。
    /// </summary>
    private static bool HasMixedXmlPresence(IReadOnlyList<(string WavPath, string XmlPath)> pairs)
    {
        var sawWithXml = false;
        var sawWithoutXml = false;
        foreach (var (wavPath, xmlPath) in pairs)
        {
            if (!File.Exists(wavPath))
            {
                continue;
            }

            if (File.Exists(xmlPath))
            {
                sawWithXml = true;
            }
            else
            {
                sawWithoutXml = true;
            }

            if (sawWithXml && sawWithoutXml)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// すべてのペアが「WAV あり・同名 XML なし」で、かつ WAV が 2 本以上なら複数波形モード候補。
    /// </summary>
    private static bool TryGetMultiWaveOnlyPaths(
        IReadOnlyList<(string WavPath, string XmlPath)> pairs,
        out List<string> wavPaths)
    {
        wavPaths = [];
        if (pairs.Count < 2)
        {
            return false;
        }

        foreach (var (wavPath, xmlPath) in pairs)
        {
            if (!File.Exists(wavPath) || File.Exists(xmlPath))
            {
                return false;
            }

            wavPaths.Add(wavPath);
        }

        return wavPaths.Count >= 2;
    }

    private static void ProcessPair(
        StringBuilder sb,
        string wavPath,
        string xmlPath,
        Action<WaveformPreviewData>? preview)
    {
        var wavExists = File.Exists(wavPath);
        var xmlExists = File.Exists(xmlPath);

        if (!wavExists)
        {
            sb.AppendLine(UiStrings.LogErrorHeader);
            sb.AppendLine(UiStrings.LogWaveMissing(wavPath));
            sb.AppendLine(UiStrings.LogXmlPresence(xmlPath, xmlExists));
            sb.AppendLine(UiStrings.LogWaveRequired);
            sb.AppendLine();
            return;
        }

        try
        {
            var wavInfo = WavFileInfo.Read(wavPath);
            sb.AppendLine(wavInfo.ToDisplayText());

            IReadOnlyList<WaveformBarMark> bars = [];
            IReadOnlyList<WaveformMarkerMark> markers = [];
            IReadOnlyList<WaveformCycleMark> cycles = [];
            IReadOnlyList<WaveformRegionMark> regions = [];
            IReadOnlyList<WaveformOutputPart> outputParts = [];
            WaveformBarOverlayResult? barOverlay = null;
            var allowsSessionMarkerEdit = false;
            if (xmlExists)
            {
                sb.AppendLine(UiStrings.LogXmlPairHeader);
                sb.AppendLine(UiStrings.LogXmlPairModeName);
                sb.AppendLine();

                var tracklist = NuendoTracklistInfo.Read(xmlPath);
                sb.AppendLine(tracklist.ToDisplayText());
                barOverlay = WaveformBarOverlayBuilder.Build(tracklist, wavInfo);
                bars = barOverlay.Marks;
                markers = barOverlay.Markers;
                cycles = barOverlay.Cycles;
                regions = barOverlay.Regions;
                outputParts = barOverlay.OutputParts;

                if (!barOverlay.HasIXml || barOverlay.TimeReferenceSamples == 0)
                {
                    sb.AppendLine(UiStrings.LogWarningHeader);
                    sb.AppendLine(UiStrings.LogIxmlTimeRefMissing);
                    sb.AppendLine();
                }
            }
            else
            {
                // Wave 単体モード（既存 XML 経路には混ぜない）
                var embedded = WavEmbeddedMarkerInfo.Read(wavPath);
                var waveOnlyMode = WaveOnlyModeProcessor.Resolve(embedded);
                sb.AppendLine(UiStrings.LogWaveOnlyHeader);
                sb.AppendLine(UiStrings.LogWaveOnlyModeName(waveOnlyMode));

                if (waveOnlyMode == WaveOnlyMode.MarkersOnly
                    || waveOnlyMode == WaveOnlyMode.SmplLoop)
                {
                    var materializeRenames = new List<WaveOnlyModeProcessor.MarkerCommentRename>();

                    if (waveOnlyMode == WaveOnlyMode.MarkersOnly)
                    {
                        markers = WaveOnlyModeProcessor.BuildMarkersOnly(embedded, wavInfo.FrameCount);
                        var sessionMarkers = markers.ToList();
                        WaveOnlyModeProcessor.TryMaterializeImplicitLoopComments(
                            sessionMarkers,
                            wavInfo.FrameCount,
                            renames: materializeRenames);
                        markers = sessionMarkers;
                        sb.AppendLine(UiStrings.LogWaveOnlyMarkersOnlySummary(markers.Count));
                    }
                    else
                    {
                        var smplBuild = WaveOnlyModeProcessor.BuildMarkersFromSmplLoops(
                            embedded,
                            wavInfo.FrameCount);
                        markers = smplBuild.Markers;
                        sb.AppendLine(
                            UiStrings.LogWaveOnlySmplLoopSummary(
                                smplBuild.AcceptedLoopCount,
                                smplBuild.SkippedLoopCount));
                        WaveOnlyModeProcessor.AppendDiscardedEmbeddedMarks(sb, smplBuild.DiscardedMarks);
                    }

                    regions = WaveOnlyModeProcessor.BuildRegionsFromMarkers(
                        markers,
                        wavInfo.FrameCount);
                    if (regions.Count > 0)
                    {
                        outputParts = WaveformRegionBuilder.BuildOutputParts(regions, wavPath);
                    }

                    allowsSessionMarkerEdit = true;
                    foreach (var rename in materializeRenames)
                    {
                        sb.AppendLine(
                            UiStrings.LogWaveOnlyMarkerRenamed(
                                rename.FromComment,
                                rename.ToComment));
                    }

                    WaveOnlyModeProcessor.AppendRegionSummary(sb, markers, outputParts.Count);
                }
                else
                {
                    sb.AppendLine(UiStrings.LogWaveOnlyModeNotImplemented);
                }

                sb.AppendLine();
                sb.AppendLine(UiStrings.LogWarningHeader);
                sb.AppendLine(UiStrings.LogXmlMissing(xmlPath));
                sb.AppendLine(UiStrings.LogXmlMissingBars);
                sb.AppendLine();
            }

            var peaks = WavPeakReader.Read(wavInfo, peakCount: WavPeakReader.DefaultOverviewPeakCount);
            preview?.Invoke(new WaveformPreviewData(
                peaks,
                wavPath,
                wavInfo,
                bars,
                markers,
                cycles,
                regions,
                outputParts,
                allowsSessionMarkerEdit));

            sb.AppendLine(UiStrings.LogWaveformHeader);
            sb.AppendLine($"{UiStrings.KeySource} {wavPath}");
            sb.AppendLine(UiStrings.LogPeaksSummary(peaks.Mins.Length, peaks.FrameCount));
            sb.AppendLine($"{UiStrings.KeyRegions} {regions.Count}");
            sb.AppendLine($"{UiStrings.KeyOutputs} {outputParts.Count}");
            foreach (var part in outputParts)
            {
                sb.AppendLine(
                    $"  - {part.FileName}"
                    + $"  samples=[{part.StartSampleOffset:N0} .. {part.EndSampleOffset:N0})");
            }
            sb.AppendLine($"{UiStrings.KeyBars} {bars.Count}");
            if (barOverlay is not null)
            {
                sb.AppendLine(
                    $"{UiStrings.KeyTimeline} TimeRef={barOverlay.TimeReferenceSamples:N0}"
                    + $"  waveStartPpq={barOverlay.WaveStartPpq:0.###}"
                    + $"  waveEndPpq={barOverlay.WaveEndPpq:0.###}"
                    + $"  prevBarPpq={FormatOptionalPpq(barOverlay.PreviousBarPpqAtWaveStart)}");
                if (barOverlay.HasAnacrusis)
                {
                    sb.AppendLine(UiStrings.LogAnacrusisYes);
                }
                else
                {
                    sb.AppendLine(UiStrings.LogAnacrusisNo);
                }

                if (barOverlay.IgnoredOutsideMarks.Count > 0)
                {
                    sb.AppendLine(UiStrings.LogOutsideWaveHeader);
                    sb.AppendLine(UiStrings.LogOutsideWaveMessage);
                    sb.AppendLine(
                        $"{UiStrings.KeyWavePpq} [{barOverlay.WaveStartPpq:0.###} .. {barOverlay.WaveEndPpq:0.###}]");
                    foreach (var ignored in barOverlay.IgnoredOutsideMarks)
                    {
                        var span = ignored.Kind == "Cycle"
                            ? $"PPQ=[{ignored.StartPpq:0.###} .. {ignored.EndPpq:0.###}]"
                            : $"PPQ={ignored.StartPpq:0.###}";
                        sb.AppendLine(
                            $"  - {UiStrings.LabelIgnoredMarkKind(ignored.Kind)} \"{ignored.Name}\"  {span}"
                            + $"  ({ignored.Reason})");
                    }
                }
            }

            sb.AppendLine();
        }
        catch (Exception ex)
        {
            AppendError(sb, wavPath, ex);
        }
    }

    private static string FormatOptionalPpq(double? ppq)
    {
        return ppq is null ? "-" : ppq.Value.ToString("0.###");
    }

    private static void AppendError(StringBuilder sb, string path, Exception ex)
    {
        sb.AppendLine(UiStrings.LogErrorHeader);
        sb.AppendLine($"{UiStrings.KeyPath} {path}");
        sb.AppendLine($"{UiStrings.KeyMessage} {ex.Message}");
        sb.AppendLine();
    }
}
