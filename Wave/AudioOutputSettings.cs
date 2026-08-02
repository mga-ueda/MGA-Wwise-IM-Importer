namespace MgaWwiseIMImporter.Wave;

/// <summary>再生出力の API 種別。</summary>
internal enum AudioOutputApi
{
    WaveOut,
    Wasapi,
    Asio,
}

/// <summary>
/// 出力 API とデバイス識別子。
/// <see cref="DeviceId"/> は API ごとに意味が異なる（空／既定値はシステム既定）。
/// </summary>
internal readonly record struct AudioOutputSettings(AudioOutputApi Api, string DeviceId)
{
    public static AudioOutputSettings Default { get; } = new(AudioOutputApi.WaveOut, string.Empty);

    public static AudioOutputApi ParseApi(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AudioOutputApi.WaveOut;
        }

        if (text.Equals("Wasapi", StringComparison.OrdinalIgnoreCase)
            || text.Equals("WASAPI", StringComparison.OrdinalIgnoreCase))
        {
            return AudioOutputApi.Wasapi;
        }

        if (text.Equals("Asio", StringComparison.OrdinalIgnoreCase)
            || text.Equals("ASIO", StringComparison.OrdinalIgnoreCase))
        {
            return AudioOutputApi.Asio;
        }

        return AudioOutputApi.WaveOut;
    }

    public static string ToStoredValue(AudioOutputApi api) => api switch
    {
        AudioOutputApi.Wasapi => "Wasapi",
        AudioOutputApi.Asio => "Asio",
        _ => "WaveOut",
    };
}

/// <summary>設定 UI 用のデバイス一覧項目。</summary>
internal readonly record struct AudioOutputDeviceInfo(string Id, string DisplayName);
