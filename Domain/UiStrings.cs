namespace MgaWwiseIMImporter.Domain;

/// <summary>
/// ユーザーに見えるすべての表示テキスト（Tips・ダイアログ・ログ・ラベル・ボタン・
/// アクセシビリティ名・開発者パネルなど）を一箇所に集約する。画面の固定ラベルも例外ではない。
/// 新しい表示テキストを追加するときは、必ずこのファイルへプロパティ／メソッドを追加してから参照する。
/// </summary>
internal static partial class UiStrings
{
    public static UiLanguage Language { get; private set; } = UiLanguage.Japanese;

    public static event EventHandler? LanguageChanged;

    public static bool IsJapanese => Language == UiLanguage.Japanese;

    public static void SetLanguage(UiLanguage language)
    {
        if (Language == language)
        {
            return;
        }

        Language = language;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static UiLanguage ParseLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UiLanguage.Japanese;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals("en", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("english", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(nameof(UiLanguage.English), StringComparison.OrdinalIgnoreCase))
        {
            return UiLanguage.English;
        }

        return UiLanguage.Japanese;
    }

    public static string ToStoredValue(UiLanguage language) =>
        language == UiLanguage.English ? "en" : "ja";

    public static string Get(string japanese, string english) =>
        IsJapanese ? japanese : english;

    public static string Format(string japaneseFormat, string englishFormat, params object[] args) =>
        string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Get(japaneseFormat, englishFormat),
            args);

}
