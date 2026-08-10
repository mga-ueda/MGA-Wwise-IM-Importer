namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// WAAPI object.set 用の @ プロパティ名と、
/// setProperty / setReference / WWU PropertyList 用の bare 名。
/// </summary>
internal static class WaapiPropertyNames
{
    public const string Arguments = "@Arguments";
    public const string AudioNode = "@AudioNode";
    public const string Cues = "@Cues";
    public const string CueType = "@CueType";
    public const string DefaultTransitionTime = "@DefaultTransitionTime";
    public const string DestinationContextObject = "@DestinationContextObject";
    public const string DestinationContextObjectId = "@DestinationContextObject.id";
    public const string DestinationContextObjectName = "@DestinationContextObject.name";
    public const string DestinationContextObjectPath = "@DestinationContextObject.path";
    public const string DestinationContextType = "@DestinationContextType";
    public const string DestinationJumpPositionPreset = "@DestinationJumpPositionPreset";
    public const string EnableDestinationFadeIn = "@EnableDestinationFadeIn";
    public const string EnableSourceFadeOut = "@EnableSourceFadeOut";
    public const string EndPosition = "@EndPosition";
    public const string Entries = "@Entries";
    public const string EntryPath = "@EntryPath";
    public const string ExitSourceAt = "@ExitSourceAt";
    public const string FadeInDuration = "@FadeInDuration";
    public const string FadeOutDuration = "@FadeOutDuration";
    public const string IsFolder = "@IsFolder";
    public const string IsStreamingEnabled = "@IsStreamingEnabled";
    public const string IsZeroLatency = "@IsZeroLatency";
    public const string LookAheadTime = "@LookAheadTime";
    public const string LoopCount = "@LoopCount";
    public const string MakeUpGain = "@MakeUpGain";
    public const string OverrideClockSettings = "@OverrideClockSettings";
    public const string PlayAt = "@PlayAt";
    public const string PlaylistItemType = "@PlaylistItemType";
    public const string PlaylistRoot = "@PlaylistRoot";
    public const string PlayMode = "@PlayMode";
    public const string PreFetchLength = "@PreFetchLength";
    public const string Segment = "@Segment";
    public const string SourceContextType = "@SourceContextType";
    public const string Tempo = "@Tempo";
    public const string TimeMs = "@TimeMs";
    public const string TimeSignatureLower = "@TimeSignatureLower";
    public const string TimeSignatureUpper = "@TimeSignatureUpper";
    public const string TransitionRoot = "@TransitionRoot";

    public static class Bare
    {
        public const string BeginTrimOffset = "BeginTrimOffset";
        public const string DestinationContextObject = "DestinationContextObject";
        public const string DestinationContextType = "DestinationContextType";
        public const string DestinationFadeIn = "DestinationFadeIn";
        public const string EnableDestinationFadeIn = "EnableDestinationFadeIn";
        public const string EnableSourceFadeOut = "EnableSourceFadeOut";
        public const string EndTrimOffset = "EndTrimOffset";
        public const string FadeInDuration = "FadeInDuration";
        public const string FadeInMode = "FadeInMode";
        public const string FadeInShape = "FadeInShape";
        public const string FadeOutDuration = "FadeOutDuration";
        public const string FadeOutMode = "FadeOutMode";
        public const string FadeOutShape = "FadeOutShape";
        public const string MusicSyncType = "MusicSyncType";
        public const string PlayAt = "PlayAt";
        public const string PlaySourcePostExit = "PlaySourcePostExit";
        public const string SourceContextType = "SourceContextType";
        public const string SourceFadeOut = "SourceFadeOut";
        public const string Volume = "Volume";
    }
}
