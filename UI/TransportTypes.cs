namespace MgaWwiseIMImporter.UI;

internal enum TransportCommand
{
    TogglePlayback,
    JumpToBar,
    GoToStart,
    PreviousPlaylist,
    PreviousBar,
    PreviousPage,
    NextPage,
    NextBar,
    NextPlaylist,
    GoToEnd,
    TimeZoomIn,
    TimeZoomOut,
    TimeZoomMax,
    TimeZoomReset,
    AmpZoomIn,
    AmpZoomOut,
    AmpZoomMax,
    AmpZoomReset,
    CycleWaveformHeight,
}

internal readonly record struct TransportPositionInfo(
    double Bpm,
    int Numerator,
    int Denominator,
    int Bar,
    int Beat,
    int Subdivision,
    TimeSpan Time,
    bool HasMusicalPosition = true);

internal enum TransportIcon
{
    PlayPause,
    JumpToBar,
    GoToStart,
    PreviousRegion,
    PreviousBar,
    PreviousPage,
    NextPage,
    NextBar,
    NextRegion,
    GoToEnd,
    TimeZoomIn,
    TimeZoomOut,
    TimeZoomMax,
    TimeZoomReset,
    AmpZoomIn,
    AmpZoomOut,
    AmpZoomMax,
    AmpZoomReset,
    WaveformHeight,
    Clear,
    Copy,
    Download,
    Folder,
    Delete,
    Lock,
    Unlock,
}
