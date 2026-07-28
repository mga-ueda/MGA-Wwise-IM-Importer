# MGA Wwise IMImporter

[English](README.en.md)

Interactive Music を、手作業の積み上げから「聴いて決めて渡す」流れへ。

## このアプリの魅力

**DAW で設計した音楽の構造を、そのまま Wwise の Interactive Music へ運ぶ**ためのツールです。

Authoring 上で Music Playlist Container／Music Switch Container／Music Segment／Music Track／State Group／トランジション／Cue／フェードを一つずつ手で組む作業は、時間がかかります。本アプリなら、波形上でプレビューし、遷移やレイヤーを聴きながら決めた内容を EXPORT 一発で Wwise へ揃えられます。**手作業での実装より大幅に時短**できます。

**それらの DAW（Nuendo／Cubase）を使っていなくても大丈夫です。** Logic などマーカーを付けられる DAW で書き出した WAV（埋め込みマーカー）でも十分な手間軽減になります。さらに本アプリ自体にマーカーの付与やループ用マーカーのペア移動機能があり、**波形エディタすら必須ではありません**——これも大きな魅力の一つです。

**Nuendo／Cubase があると非常に強力です。** マーカートラックをエクスポートした XML があれば、テンポ・小節・拍子情報を最大限に活かせます。マーカーやサイクルも波形上に「塗る」ように打つことができ、Entry／Exit／ループ設計を視覚的に進められます。

**Wwise に実装したデータをマスター波形として扱う**想定です。ワンショット部とループ部をあらかじめ別ファイルにセパレートせず、1 本のマスター上で構造を非破壊に組み立て、EXPORT では切り出しと Wwise プロパティ（MusicClip Fade／MusicFade／Make-Up Gain／Cue など）へ載せる仕様です。ソース WAV へゲインやフェードは焼き込みません。

- Music Playlist の遷移プレビュー（Exit Source At／Fade In／Fade Out／Play -E）
- グループ化による縦レイヤー（Alt 上乗せ／Additive Layer、Group Fade、Change Occurs At）
- ストリーミング（Prefetch Length／Look-ahead Time）や Keep Layer Balance（Make-Up Gain）
- WAAPI 経由で Music 構造を自動生成
- **複数波形の同時ドロップやマーカートラック XML の利用により、何曲でもまとめて実装可能**

## マニュアル・ダウンロード

- マニュアル: [日本語](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html) · [English](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.en.html) · [一覧](https://mga-ueda.github.io/MGA-Wwise-IMImporter/)
- 初めての方: WAV 1 本だけで試せる [クイックスタート](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html#quickstart) と、XML などの [素材の準備と方式の選び方](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html#prepare) をどうぞ
- アプリ内: プロジェクトバーの **マニュアル（`?`）**（歯車の左。JP／EN 追従）
- 配布: [Releases](https://github.com/mga-ueda/MGA-Wwise-IMImporter/releases)

Wwise®／Audiokinetic®、Nuendo®／Cubase®／Steinberg® は各権利者の商標です。本ツールは非公式です。
