# MGA Wwise IMImporter

[English](README.en.md)

Interactive Music を、Authoring の手組みから **「聴いて決めて渡す」** へ。

Nuendo／Cubase の**マーカートラック XML** と Wave を波形上でプレビューし、EXPORT 一発で Wwise Interactive Music 構造を WAAPI 生成する Windows 向けツール（開発中）です。Music Playlist／Switch／Segment／Track／State Group／トランジションを手で積むより、**大幅に時短**できます。

### ここが魅力

- **Wwise 実装がマスター** — ワンショットとループを事前に別ファイルへ分けず、1 本のマスター上で非破壊に組み立て。ゲイン／フェードは WAV に焼き込まない
- **マーカートラック XML** — テンポ・小節を最大限に活かし、マーカーを波形上に塗るように扱える
- **それらの DAW がなくても** — Logic などのマーカー付き WAV や、アプリ内のマーカー付与・セット移動で、**波形エディタが不要なことも多い**
- **何曲でもまとめて** — 複数波形の同時ドロップやマーカートラック XML でまとめて実装
- **出荷前に聴ける** — Playlist 遷移・縦レイヤー・ストリーミング・Keep Layer Balance（Make-Up Gain）をプレビューしてから EXPORT

音量・フェード・Cue は Wwise プロパティ（MusicClip Fade／MusicFade／Make-Up Gain／Custom Cue など）へ載せます。

## マニュアル・ダウンロード

- マニュアル: [日本語](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html) · [English](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.en.html) · [一覧](https://mga-ueda.github.io/MGA-Wwise-IMImporter/)
- アプリ内: プロジェクトバーの **マニュアル（`?`）**（歯車の左。JP／EN 追従）
- 配布: [Releases](https://github.com/mga-ueda/MGA-Wwise-IMImporter/releases)

Wwise®／Audiokinetic®、Nuendo®／Cubase®／Steinberg® は各権利者の商標です。本ツールは非公式です。
