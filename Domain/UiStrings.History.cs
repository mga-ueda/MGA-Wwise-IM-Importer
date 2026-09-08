namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    public static string EditHistoryTitle => Get("編集履歴", "Edit History");

    public static string EditHistoryOrigin => Get("初期状態", "Initial state");

    public static string EditHistoryHint => Get(
        "↑↓ 移動　Enter 確定　Esc キャンセル",
        "↑↓ move  Enter apply  Esc cancel");

    public const string EditHistoryNameAddMarker = "Add Marker";
    public const string EditHistoryNameDeleteMarkers = "Delete Markers";
    public const string EditHistoryNameMoveMarker = "Move Marker";
    public const string EditHistoryNameMarkerComment = "Marker Comment";
    public const string EditHistoryNameEditMarkers = "Edit Markers";
    public const string EditHistoryNameRegionEdgeFade = "Region Edge Fade";

    public static string EditHistoryName(string name) => name switch
    {
        EditHistoryNameAddMarker => Get("マーカー追加", "Add marker"),
        EditHistoryNameDeleteMarkers => Get("マーカー削除", "Delete marker"),
        EditHistoryNameMoveMarker => Get("マーカー移動", "Move marker"),
        EditHistoryNameMarkerComment => Get("マーカー名", "Marker name"),
        EditHistoryNameEditMarkers => Get("マーカー編集", "Edit markers"),
        EditHistoryNameRegionEdgeFade => Get("リージョン端フェード", "Region-edge fade"),
        _ => EditHistoryOrigin,
    };

    public static string EditHistoryQuote(string? text)
    {
        text = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (text.Length == 0)
        {
            return Get("（空）", "(empty)");
        }

        const int max = 18;
        return text.Length > max ? text[..max] + "…" : text;
    }

    public static string FormatHistoryTimecode(long frame, int sampleRate)
    {
        var rate = Math.Max(1, sampleRate);
        var seconds = frame / (double)rate;
        if (double.IsNaN(seconds) || seconds < 0)
        {
            return "00:00.000";
        }

        var minutes = (int)(seconds / 60d);
        var rest = seconds - minutes * 60d;
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{minutes:00}:{rest:00.000}");
    }

    public static string EditHistoryPoint(string verb, int sampleRate, long frame, string? extra = null)
    {
        var at = FormatHistoryTimecode(frame, sampleRate);
        return string.IsNullOrEmpty(extra) ? $"{verb}  {at}" : $"{verb}  {at}  {extra}";
    }

    public static string EditHistoryShift(
        string verb,
        int sampleRate,
        long fromFrame,
        long toFrame,
        string? extra = null)
    {
        var shift =
            $"{FormatHistoryTimecode(fromFrame, sampleRate)}→{FormatHistoryTimecode(toFrame, sampleRate)}";
        return string.IsNullOrEmpty(extra) ? $"{verb}  {shift}" : $"{verb}  {shift}  {extra}";
    }
}
