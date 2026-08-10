namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Transport ---
    private static string WithKeyRepeat(string japanese, string english) =>
        Get(
            japanese + Environment.NewLine + "長押しでキーリピート",
            english + Environment.NewLine + "Hold for key repeat");

    public static string TipTransportPlayPause => Get(
        "[Space] 再生 / 一時停止"
        + Environment.NewLine
        + "[Alt+Enter] / Alt+クリック 直前の開始位置から再生し直し"
        + Environment.NewLine
        + "[Ctrl+Space] / Ctrl+クリック 3秒前から再生",
        "[Space] Play / Pause"
        + Environment.NewLine
        + "[Alt+Enter] / Alt+click Restart from last start"
        + Environment.NewLine
        + "[Ctrl+Space] / Ctrl+click Play from 3 seconds earlier");

    public static string TipTransportJumpToBar => Get(
        "[G] 小節番号を指定して移動",
        "[G] Jump to bar number");

    public static string TipTransportGoToStart => WithKeyRepeat(
        "[Ctrl+Home] 先頭へ移動",
        "[Ctrl+Home] Go to start");

    public static string TipTransportPreviousPage => WithKeyRepeat(
        "[Page Up] 前の表示ページ",
        "[Page Up] Previous view page");

    public static string TipTransportPreviousPlaylist => WithKeyRepeat(
        "[Ctrl+←] 前の Music Playlist 先頭、またはマーカーへ移動",
        "[Ctrl+←] Previous Music Playlist start or marker");

    public static string TipTransportPreviousMarker => WithKeyRepeat(
        "[Ctrl+Shift+←] 前のマーカーへ移動",
        "[Ctrl+Shift+←] Previous marker");

    public static string TipTransportPreviousBar => WithKeyRepeat(
        "[Home] 前の小節",
        "[Home] Previous bar");

    public static string TipTransportNextBar => WithKeyRepeat(
        "[End] 次の小節",
        "[End] Next bar");

    public static string TipTransportPreviousViewStep => WithKeyRepeat(
        "[Home] 表示の約 5% 前へ移動",
        "[Home] Move back about 5% of the view");

    public static string TipTransportNextViewStep => WithKeyRepeat(
        "[End] 表示の約 5% 先へ移動",
        "[End] Move forward about 5% of the view");

    public static string TipTransportNextPlaylist => WithKeyRepeat(
        "[Ctrl+→] 次の Music Playlist 先頭、またはマーカーへ移動",
        "[Ctrl+→] Next Music Playlist start or marker");

    public static string TipTransportNextMarker => WithKeyRepeat(
        "[Ctrl+Shift+→] 次のマーカーへ移動",
        "[Ctrl+Shift+→] Next marker");

    public static string TipTransportNextPage => WithKeyRepeat(
        "[Page Down] 次の表示ページ",
        "[Page Down] Next view page");

    public static string TipTransportGoToEnd => WithKeyRepeat(
        "[Ctrl+End] 末尾へ移動",
        "[Ctrl+End] Go to end");

    public static string TipTransportTimeZoomIn => WithKeyRepeat(
        "[↑] 時間軸を拡大",
        "[↑] Zoom in time");

    public static string TipTransportTimeZoomOut => WithKeyRepeat(
        "[↓] 時間軸を縮小",
        "[↓] Zoom out time");

    public static string TipTransportTimeZoomMax => WithKeyRepeat(
        "[Ctrl+↑] 時間軸を最大拡大",
        "[Ctrl+↑] Max time zoom");

    public static string TipTransportTimeZoomReset => WithKeyRepeat(
        "[Ctrl+↓] 時間軸を全体表示",
        "[Ctrl+↓] Fit time to view");

    public static string TipTransportAmpZoomIn => WithKeyRepeat(
        "[Shift+↑] 振幅を拡大",
        "[Shift+↑] Zoom in amplitude");

    public static string TipTransportAmpZoomOut => WithKeyRepeat(
        "[Shift+↓] 振幅を縮小",
        "[Shift+↓] Zoom out amplitude");

    public static string TipTransportAmpZoomMax => WithKeyRepeat(
        "[Ctrl+Shift+↑] 振幅を最大拡大",
        "[Ctrl+Shift+↑] Max amplitude zoom");

    public static string TipTransportAmpZoomReset => WithKeyRepeat(
        "[Ctrl+Shift+↓] 振幅を既定に戻す",
        "[Ctrl+Shift+↓] Reset amplitude zoom");

    public static string TipTransportCycleWaveformHeight => Get(
        "[Z] 波形表示エリアの高さを切替（1倍→2倍→3倍）",
        "[Z] Cycle waveform height (1×→2×→3×)");

    public static string TipTransportMetronome => Get(
        "[M] メトロノームのオン／オフ（テンポ／拍子があるとき）"
        + Environment.NewLine
        + "ホイール … 音量（最大〜10%、10% 刻み。既定 30%。アプリ設定に保存）",
        "[M] Toggle metronome (note or tempo; synced to waveform playback beats when tempo / time signature is available)"
        + Environment.NewLine
        + "Wheel … volume (max to 10%, 10% steps; default 30%; saved in app settings)");

    public static string TipMetronomeVolume(int percent) => Format(
        "メトロノーム音量 {0}%",
        "Metronome volume {0}%",
        percent);

    public static string TipForTransportCommand(
        TransportCommand command,
        bool waveOnlyViewStep = false,
        bool waveOnlyMarkerNav = false) => command switch
    {
        TransportCommand.TogglePlayback => TipTransportPlayPause,
        TransportCommand.JumpToBar => TipTransportJumpToBar,
        TransportCommand.GoToStart => TipTransportGoToStart,
        TransportCommand.PreviousPage => TipTransportPreviousPage,
        TransportCommand.PreviousPlaylist => waveOnlyMarkerNav
            ? TipTransportPreviousMarker
            : TipTransportPreviousPlaylist,
        TransportCommand.PreviousBar => waveOnlyViewStep
            ? TipTransportPreviousViewStep
            : TipTransportPreviousBar,
        TransportCommand.NextBar => waveOnlyViewStep
            ? TipTransportNextViewStep
            : TipTransportNextBar,
        TransportCommand.NextPlaylist => waveOnlyMarkerNav
            ? TipTransportNextMarker
            : TipTransportNextPlaylist,
        TransportCommand.NextPage => TipTransportNextPage,
        TransportCommand.GoToEnd => TipTransportGoToEnd,
        TransportCommand.TimeZoomIn => TipTransportTimeZoomIn,
        TransportCommand.TimeZoomOut => TipTransportTimeZoomOut,
        TransportCommand.TimeZoomMax => TipTransportTimeZoomMax,
        TransportCommand.TimeZoomReset => TipTransportTimeZoomReset,
        TransportCommand.AmpZoomIn => TipTransportAmpZoomIn,
        TransportCommand.AmpZoomOut => TipTransportAmpZoomOut,
        TransportCommand.AmpZoomMax => TipTransportAmpZoomMax,
        TransportCommand.AmpZoomReset => TipTransportAmpZoomReset,
        TransportCommand.CycleWaveformHeight => TipTransportCycleWaveformHeight,
        _ => string.Empty,
    };

}
