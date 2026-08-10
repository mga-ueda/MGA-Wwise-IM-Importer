using System.Text.Json;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>getInfo / getProjectInfo / bringToForeground の共通呼び出し。</summary>
internal static class WaapiCoreCalls
{
    public static Task<JsonElement> GetInfoAsync(
        WaapiHttpClient client,
        CancellationToken cancellationToken = default) =>
        client.CallAsync(WaapiUris.CoreGetInfo, cancellationToken: cancellationToken);

    public static Task<JsonElement> GetProjectInfoAsync(
        WaapiHttpClient client,
        CancellationToken cancellationToken = default) =>
        client.CallAsync(WaapiUris.CoreGetProjectInfo, cancellationToken: cancellationToken);

    public static Task<JsonElement> BringToForegroundAsync(
        WaapiHttpClient client,
        CancellationToken cancellationToken = default) =>
        client.CallAsync(WaapiUris.UiBringToForeground, cancellationToken: cancellationToken);
}
