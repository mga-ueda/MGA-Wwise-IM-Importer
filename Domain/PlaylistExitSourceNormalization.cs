namespace MgaWwiseIMImporter.Domain;

/// <summary>
/// Wave Only セッションでは小節／拍グリッドが無いため、
/// Next Bar / Next Beat を Immediate に正規化する。
/// </summary>
internal static class PlaylistExitSourceNormalization
{
    public static PlaylistExitSourceMode NormalizeForWaveOnly(
        PlaylistExitSourceMode mode,
        bool waveOnlySessionMarkers)
    {
        if (waveOnlySessionMarkers
            && mode is PlaylistExitSourceMode.NextBar or PlaylistExitSourceMode.NextBeat)
        {
            return PlaylistExitSourceMode.Immediate;
        }

        return mode;
    }
}
