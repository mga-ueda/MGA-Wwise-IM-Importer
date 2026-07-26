# MGA Wwise IMImporter

[English README](README.md)

Nuendo／Cubase の**マーカートラックをエクスポートした XML** と Wave を読み、波形プレビュー・分割 WAV 書き出し・WAAPI 経由の Wwise Interactive Music インポートを行う Windows 向けツール（開発中）です。Authoring での手組みより大幅に時短でき、**Wwise 上の実装をマスター波形として扱う**非破壊ワークフローを想定しています（ワンショット／ループを事前に別ファイルへ分けなくても組み立て可能）。複数波形の同時ドロップやマーカートラック XML により、何曲でもまとめて実装できます。

Nuendo／Cubase のマーカートラック XML があればテンポ・小節情報を最大限に活かし、マーカーを波形上に塗るように扱えます。Logic などマーカー付き WAV でも十分に使え、アプリ側のマーカー付与・セット移動により波形エディタが不要な場合も多いです。

**音量・フェード・マーカー類は全面的に非破壊です。** ソース／分割 WAV へ焼き込まず、EXPORT 時は Wwise 側プロパティ（MusicClip Fade／MusicFade／Make-Up Gain／Cue など）へ設定します。

## マニュアル

- **[日本語](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html)**／**[English](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.en.html)**
- 一覧: [https://mga-ueda.github.io/MGA-Wwise-IMImporter/](https://mga-ueda.github.io/MGA-Wwise-IMImporter/)
- アプリ内ではプロジェクトバー右の **マニュアル（`?`）**（歯車の左）から開けます。表示言語（JP／EN）に合わせて切り替わります

## はじめに

1. Wwise Authoring で WAAPI（HTTP）を有効にし、作成先オブジェクトを選択する
2. 本アプリで書き出し先を、接続中 Wwise プロジェクトの `Originals` 配下に指定する
3. 次のいずれかのパターンで読み込み、［EXPORT］を実行する
   - **Wave 単体** … 同名 XML 無しの `.wav` を 1 本ドロップ
   - **複数波形** … 同名 XML 無しの `.wav` を 2 本以上同時ドロップ
   - **WAV ＋ 同名 XML** … Nuendo／Cubase でマーカートラックをエクスポートしたペアをドロップ

詳細手順はマニュアルの「読み込みのパターン」を参照してください。

Windows x64 の自己完結パッケージは [Releases](https://github.com/mga-ueda/MGA-Wwise-IMImporter/releases) から入手できます。開発ビルドは .NET 8（`net8.0-windows`）向けです。

## 設定ファイルについて

作業データ（プロジェクト設定・追加マーカー・Keep Last Session など）は **exe と同じフォルダ** の `MgaWwiseIMImporter.ini` および `MgaWwiseIMImporter.lastwave.<プロジェクト名>.json` に保存されます。更新時は exe だけ差し替えるか、これらのファイルを退避してください。

## 商標・ライセンス

- **Wwise®**／**Audiokinetic®** は Audiokinetic Inc. の商標です。本ツールは非公式で、Audiokinetic ソフトウェアを同梱しません。WAAPI 利用には有効な Wwise ライセンスが必要です。
- **Nuendo®**／**Cubase®**／**Steinberg®** は Steinberg Media Technologies GmbH の商標です。本ツールは非公式で、マーカートラックをエクスポートした XML を読み取るのみです。
- 同梱: [NAudio](https://github.com/naudio/NAudio)（Ms-PL）、[UDEV Gothic](https://github.com/yuru7/udev-gothic)（SIL OFL 1.1。`Licenses/LICENSE-UDEV-GOTHIC.txt`）

アプリ下部表示:

> © 2026 MIYABI GAME AUDIO INC.  GitHub  
> Wwise® and Audiokinetic® are trademarks of Audiokinetic Inc.

## バージョン

csproj の `<Version>`（SemVer）を表示・更新チェック・GitHub タグ照合に共通利用します（例: `1.0.7-beta` ↔ タグ `v1.0.7-beta`）。
