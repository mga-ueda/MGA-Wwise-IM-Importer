namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// WAAPI 接続設定（アプリ内固定。ユーザー設定には書かない）。
/// </summary>
internal sealed class WaapiSettings
{
    /// <summary>HTTP WAAPI の URL。</summary>
    public const string DefaultUrl = "http://127.0.0.1:8090/waapi";

    /// <summary>接続・RPC のタイムアウト（ミリ秒）。</summary>
    public const int DefaultTimeoutMs = 3000;

    /// <summary>起動時に接続確認するか。</summary>
    public bool ProbeOnStartup { get; init; } = true;

    /// <summary>HTTP WAAPI の URL。</summary>
    public string Url { get; init; } = DefaultUrl;

    /// <summary>接続・RPC のタイムアウト（ミリ秒）。</summary>
    public int TimeoutMs { get; init; } = DefaultTimeoutMs;

    /// <summary>アプリ固定値を返す。</summary>
    public static WaapiSettings Load() => new();
}
