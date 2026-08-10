using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.UI.Services;

/// <summary>WAAPI プローブ呼び出しの薄いラッパ（UI 状態は持たない）。</summary>
internal sealed class WaapiConnectionService
{
    private readonly WaapiSettings _settings;

    public WaapiConnectionService(WaapiSettings settings)
    {
        _settings = settings;
    }

    public Task<WaapiProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        WaapiStartupProbe.RunAsync(_settings, cancellationToken);

    public Task<(string Path, string Type)> RefreshSelectionAsync(
        CancellationToken cancellationToken = default) =>
        WaapiStartupProbe.RefreshSelectionAsync(_settings, cancellationToken);
}
