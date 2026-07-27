using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// マウスドラッグで付与するマーカーのスナップ単位。
/// </summary>
internal enum MarkerGridOverrideMode
{
    /// <summary>タイムラインに表示中のグリッド単位へスナップする。</summary>
    Default,

    /// <summary>表示状態に関わらず常に小節単位。</summary>
    Bar,

    /// <summary>表示状態に関わらず常に拍単位。</summary>
    Beat,
}

/// <summary>
/// マーカー付与オプション（実行時モデル。[Project.*] に保存）。
/// </summary>
internal sealed class MarkerSettings
{
    /// <summary>ドラッグ付与時のスナップ単位。</summary>
    public MarkerGridOverrideMode GridOverride { get; set; } = MarkerGridOverrideMode.Bar;

    /// <summary>連番の桁数（幅・上限。0 で連番なし）。</summary>
    public int CommentDigits { get; set; } = 3;

    /// <summary>Digits で指定した桁数まで 0 埋めするか（桁数に依らず同じ）。</summary>
    public bool CommentZeroPad { get; set; } = true;

    public bool CommentPrefixEnabled { get; set; }

    public string CommentPrefix { get; set; } = string.Empty;

    public bool CommentSuffixEnabled { get; set; }

    public string CommentSuffix { get; set; } = string.Empty;

    /// <summary>接頭語・接尾語と連番を繋ぐ文字を使うか（入力があれば有効）。</summary>
    public bool CommentJoinerEnabled { get; set; }

    public string CommentJoiner { get; set; } = string.Empty;

    /// <summary>塊（書き出しパート）ごとに連番をリセットするか。</summary>
    public bool CommentResetPerPart { get; set; } = true;

    public const int CommentDigitsMin = 0;
    public const int CommentDigitsMax = 6;

    /// <summary>
    /// Enabled フラグを文字列の有無に同期する。
    /// </summary>
    public void SyncCommentOptionalEnabledFlags()
    {
        CommentPrefixEnabled = CommentPrefix.Length > 0;
        CommentSuffixEnabled = CommentSuffix.Length > 0;
        CommentJoinerEnabled = CommentJoiner.Length > 0;
    }

    /// <summary>コメント生成側（Wave 層）に渡す確定値へ変換する。</summary>
    public MarkerCommentRule ToCommentRule()
    {
        SyncCommentOptionalEnabledFlags();
        return new(
            Digits: Math.Clamp(CommentDigits, CommentDigitsMin, CommentDigitsMax),
            ZeroPad: CommentZeroPad,
            Prefix: CommentPrefix,
            Suffix: CommentSuffix,
            Joiner: CommentJoiner,
            ResetPerPart: CommentResetPerPart);
    }
}
