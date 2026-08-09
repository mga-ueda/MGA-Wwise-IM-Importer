using System.Windows;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// WinForms 版と同じレイアウト設計 DPI（150% = 144）基準値を、WPF DIP（96dpi 論理）へ換算する。
/// WPF は DIP レイアウトを OS DPI で拡縮するため、ここで物理 px を再乗算してはならない。
/// </summary>
public static class DesignMetrics
{
    public const double DesignDpi = 144d;
    public const double DipDpi = 96d;

    /// <summary>144dpi 設計 px → WPF DIP。</summary>
    public static double Dip(double designPxAt144) => designPxAt144 * DipDpi / DesignDpi;

    /// <summary>旧 96dpi 基準の論理 px（Designer 値）。そのまま DIP。</summary>
    public static double From96(double value96) => value96;

    // --- 名前付き寸法（DIP）。XAML の x:Static からも参照する。 ---

    /// <summary>Designer 固定。projectBar.Height = 30。</summary>
    public static double ProjectBarHeight => From96(30);

    /// <summary>Designer 固定。actionBar.Height = 44。</summary>
    public static double ActionBarHeight => From96(44);

    /// <summary>
    /// 著作権 3 行ブロックの最小高さ。Designer copyrightLinkLabel.Height=30@96。
    /// </summary>
    public static double CopyrightBlockMinHeight => From96(30);

    /// <summary>
    /// Form1 AutoScaleMode.Font の設計高さ（AutoScaleDimensions.Height）。
    /// 150% 実測の CurrentAutoScale.Height=25 との比 25/15 を、DPI 比 1.5（=22.5/15）で割った
    /// ブースト 25/22.5 を Designer 値に掛けると、WinForms 実行時 device 高さと一致する。
    /// </summary>
    private const double WinFormsFontAutoScaleBoost = 25d / 22.5d;

    /// <summary>
    /// 横スクロールバー。Designer 15 → Form1 実測 @150% で device 25 → DIP ≈ 16.67。
    /// </summary>
    public static double WaveformScrollBarHeight => From96(15) * WinFormsFontAutoScaleBoost;

    /// <summary>
    /// 波形ホスト。Designer 220 → Form1 実測 @150% で device 367 → DIP ≈ 244.67。
    /// From96(220) だけだと device 330 にしかならず、情報行（GetHeight@144）に食い込まれて
    /// 波形レーンが約 15〜20% 短くなる。ホストを Form1 実行時高さに合わせるのが正解。
    /// </summary>
    public static double WaveformHostHeight => From96(220) * WinFormsFontAutoScaleBoost;

    /// <summary>ホストからバーを除いたビュー高さ（DIP）。</summary>
    public static double WaveformViewHeight => WaveformHostHeight - WaveformScrollBarHeight;

    /// <summary>RowDefinition 用（Height は GridLength）。</summary>
    public static GridLength WaveformScrollBarHeightGrid => new(WaveformScrollBarHeight);

    /// <summary>DesignMetrics.Px(30) → DIP 20。</summary>
    public static double WaapiBarHeight => Dip(30);

    /// <summary>Transport ButtonSideDesign 45 → DIP 30。</summary>
    public static double TransportButtonSide => Dip(45);

    /// <summary>Transport BarHeightDesign 54 → DIP 36。</summary>
    public static double TransportBarHeight => Dip(54);

    /// <summary>Transport ButtonPitchDesign 47 → DIP。</summary>
    public static double TransportButtonPitch => Dip(47);

    /// <summary>Transport GroupGapDesign 6 → DIP。</summary>
    public static double TransportGroupGap => Dip(6);

    /// <summary>Transport ButtonGapDesign 2 → DIP。</summary>
    public static double TransportButtonGap => Dip(2);

    /// <summary>Transport PadXDesign 12 → DIP。</summary>
    public static double TransportPadX => Dip(12);

    /// <summary>Transport PadYDesign 5 → DIP。</summary>
    public static double TransportPadY => Dip(5);

    /// <summary>Position display WidthDesign 315 → DIP。</summary>
    public static double TransportPositionWidth => Dip(315);

    /// <summary>Position / button HeightDesign 45 → DIP。</summary>
    public static double TransportPositionHeight => Dip(45);

    /// <summary>MetronomeHitWidthDesign 57 → DIP。</summary>
    public static double TransportMetronomeHitWidth => Dip(57);

    /// <summary>FlatOption RowHeightDesign 30 → DIP 20。</summary>
    public static double FlatOptionRowHeight => Dip(30);

    /// <summary>MarkerOptions HeaderHeightDesign 39 → DIP 26。</summary>
    public static double MarkerHeaderHeight => Dip(39);

    /// <summary>MarkerOptions RowPitchDesign 32 → DIP。</summary>
    public static double MarkerRowPitch => Dip(32);

    /// <summary>RowDefinition 用（Height は GridLength）。</summary>
    public static GridLength MarkerRowPitchGrid => new(MarkerRowPitch);

    /// <summary>
    /// Section header（Fade/Playlist 等）。Designer 26@96 は AutoScale 150% で 39px 相当 → Dip(39)。
    /// MarkerHeaderHeight と同じ。
    /// </summary>
    public static double SectionHeaderHeight => Dip(39);

    /// <summary>
    /// Tips/Log も他セクションと同じ帯高さに揃える（旧 Compact 22 は細く見えた）。
    /// </summary>
    public static double CompactSectionHeaderHeight => SectionHeaderHeight;

    /// <summary>
    /// Music Playlist 行の左インデント。Form1 PlaylistItemIndentDesign=15 → Dip。
    /// 帯（BarMarginLeft From96(3)）より内側にグループ枠を置く。
    /// </summary>
    public static double PlaylistItemIndent => Dip(15);

    /// <summary>
    /// 選択肢パネルの外側余白（Designer FlowLayoutPanel.Padding 9,0,4,4 @96）。
    /// WPF の StackPanel に Padding が無いため Margin で同等のインデントにする。
    /// 見出しテキスト（Padding.Left=10）よりわずかに右へ来る（+ Control.Margin 3）。
    /// </summary>
    public static Thickness FlatOptionPanelPadding =>
        new(From96(9), 0, From96(4), From96(4));

    /// <summary>FlatOption コントロール Margin（Designer 3,1,3,1 @96 → DIP）。</summary>
    public static Thickness FlatOptionControlMargin =>
        new(From96(3), From96(1), From96(3), From96(1));

    /// <summary>
    /// MarkerOptions 見出し Padding。WinForms CreateHeader の S(10),0,S(4),0
    /// （S = From96。Dip ではない）。
    /// </summary>
    public static Thickness MarkerOptionsHeaderPadding =>
        new(From96(10), 0, From96(4), 0);

    /// <summary>
    /// MarkerOptions 本文左インデント。WinForms streamPadL / grid の S(12),0,S(8),0。
    /// 帯開始（BarMarginLeft From96(3)）より右、見出し文字（From96(10)）よりわずかに右。
    /// </summary>
    public static Thickness MarkerOptionsContentMargin =>
        new(From96(12), 0, From96(8), 0);

    /// <summary>Stream 数値行など、上に少し空けた本文インデント。</summary>
    public static Thickness MarkerOptionsContentMarginTop4 =>
        new(From96(12), From96(4), From96(8), 0);

    /// <summary>Toolbar / project icon 正方形。Designer 24。</summary>
    public static double ToolbarButtonSide => From96(24);

    /// <summary>AudioSettings fieldWidth D(450)。</summary>
    public static double AudioFieldWidth => Dip(450);

    /// <summary>AudioSettings expected box D(120)。</summary>
    public static double AudioExpectedBoxWidth => Dip(120);

    /// <summary>ColumnDefinition 用（Width は GridLength）。</summary>
    public static GridLength AudioExpectedBoxWidthGrid => new(AudioExpectedBoxWidth);

    /// <summary>AudioSettings OK/Cancel D(162)。</summary>
    public static double AudioDialogButtonWidth => Dip(162);

    /// <summary>AudioSettings OK/Cancel D(48)（Designer 32@96）。</summary>
    public static double AudioDialogButtonHeight => Dip(48);

    /// <summary>
    /// 設定ダイアログの Combo / 数値欄の高さ。
    /// FlatOptionRowHeight(20) だと Yu Gothic + Padding で文字が欠けるため 28@96 を使う。
    /// </summary>
    public static double AudioInputHeight => From96(28);

    public static GridLength AudioInputHeightGrid => new(AudioInputHeight);

    /// <summary>AudioSettings 外周 Padding D(18)。</summary>
    public static Thickness AudioPad => new(Dip(18));

    /// <summary>AudioSettings ヘッダー左右はみ出し D(10)、下 D(3)。</summary>
    public static Thickness AudioHeaderMargin => new(-Dip(10), 0, -Dip(10), Dip(3));

    /// <summary>Action bar CLEAR/EXPORT 等 Designer 32。</summary>
    public static double ActionButtonHeight => From96(32);

    /// <summary>Position SignatureLeftDesign 67 − MetronomeHit 57。</summary>
    public static double TransportSignatureLeftInText => Dip(67 - 57);

    /// <summary>Position MusicalLeftDesign 110 − MetronomeHit 57。</summary>
    public static double TransportMusicalLeftInText => Dip(110 - 57);

    /// <summary>Position TimeLeftDesign 189 − MetronomeHit 57。</summary>
    public static double TransportTimeLeftInText => Dip(189 - 57);

    /// <summary>Yu Gothic など視覚上の下寄り補正（旧 VisualTextNudgeY D(1.5)）。</summary>
    public static double VisualTextNudgeY => Dip(1.5);

    /// <summary>単行 TextBox 用。幾何中央よりわずかに下へ（最低 1 DIP）。</summary>
    public static double TextBoxOpticalNudgeY => Math.Max(1d, Math.Floor(Dip(1)));
}
