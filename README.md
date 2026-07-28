# MGA Wwise IMImporter

[English](README.en.md)

Interactive Music を、手作業の積み上げから「聴いて決めて渡す」流れへ。

<p align="center">
  <img src="docs/images/screenshot-transition-preview.png" width="49%" alt="遷移プレビューとフェードカーブ選択">
  <img src="docs/images/screenshot-multi-part.png" width="49%" alt="長尺マスター波形の複数パート分割">
</p>

## このアプリの魅力

**DAW で設計した音楽の構造を、そのまま Wwise の Interactive Music へ運ぶ**ためのツールです。

Wwise の Interactive Music は、コンテナ・トランジション・Cue・フェードなど組み立てる要素が多く、一つずつ手で実装すると時間がかかります。本アプリなら、波形上で遷移やレイヤーを聴きながら内容を決め、EXPORT 一発で Wwise へ揃えられます。**手作業での実装より大幅に時短**できます。

**Nuendo／Cubase を使っていなくても大丈夫です。** Logic などマーカーを付けられる DAW で書き出した WAV（埋め込みマーカー）でも十分な手間軽減になります。さらに本アプリ自体にマーカーの付与やループ用マーカーのペア移動機能があり、**波形エディタすら必須ではありません**。レイヤーミュージックでは、何レイヤーあっても、ループポイントの処理は 1 レイヤーに対して作業するだけで、自動的に他のレイヤーへ反映されます。

**Nuendo／Cubase があると非常に強力です。** マーカートラックをエクスポートした XML があれば、テンポ・小節・拍子情報を最大限に活かせます。マーカーやサイクルも波形上に「塗る」ように打つことができ、Entry／Exit／ループ設計を視覚的に進められます。

**横方向と縦方向の推移を、混在したまま扱えます。** インタラクティブミュージックの推移には、曲から曲へ切り替えるクロスフェードタイプ（横方向）と、レイヤーを重ねるタイプ（縦方向）があります。さらに縦方向には、再生位置を保ったままクロスフェードで曲を差し替えるレイヤー切替タイプと、鳴っている曲にパートを重ねていくアッドレイヤータイプの 2 種類があります。これらのアセットが 1 つのプロジェクトに混じっていても、分け隔てなく同時に推移をプレビューでき、そのまままとめて Wwise へ実装できます。

**Wwise に実装したデータをマスター波形として扱う**想定です。ワンショット部とループ部をあらかじめ別ファイルにセパレートせず、1 本のマスター上で構造を非破壊に組み立て、EXPORT では切り出しと Wwise プロパティ（MusicClip Fade／MusicFade／Make-Up Gain／Cue など）へ載せる仕様です。ソース WAV へゲインやフェードは焼き込みません。

**実装は WAAPI ＋ 独自処理の組み合わせです。** Wwise の遠隔操作（WAAPI）に加えて、WAV の切り出し・ラウドネス計測・フェードのプロパティ変換などをアプリ側の独自処理で補っているため、WAAPI を呼ぶだけの自動実装ツールでは実現できない実装まで踏み込めます。具体的には次のようなことができます。

- Music Playlist 間の遷移を、Exit Source At／Fade In／Fade Out／Play -E を切り替えながら実際に聴いてプレビューできます
- Playlist をグループ化して縦レイヤーを組み、上乗せ再生（Additive Layer）・Group Fade・Change Occurs At によるレイヤー切替を確認できます
- **グループの音量バランスを維持したまま実装できます**（Loudness Normalization で崩れがちなレイヤー間バランスを Make-Up Gain で自動補正。利用時に推奨）
- 設定を忘れがちなストリーミング設定（Prefetch Length／Look-ahead Time）も UI から指定して書き出せます
- 聴いて決めた内容は、WAAPI 経由で Wwise 上に Music 構造として自動生成されます
- **複数波形の同時ドロップやマーカートラック XML の利用により、何曲でもまとめて実装できます**

## マニュアル・ダウンロード

- マニュアル: [日本語](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html) · [English](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.en.html) · [一覧](https://mga-ueda.github.io/MGA-Wwise-IMImporter/)
- 初めての方: WAV 1 本だけで試せる [クイックスタート](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html#quickstart) と、XML などの [素材の準備と方式の選び方](https://mga-ueda.github.io/MGA-Wwise-IMImporter/manual.ja.html#prepare) をどうぞ
- アプリ内: プロジェクトバーの **マニュアル（`?`）**（歯車の左。JP／EN 追従）
- 配布: [Releases](https://github.com/mga-ueda/MGA-Wwise-IMImporter/releases)

Wwise®／Audiokinetic®、Nuendo®／Cubase®／Steinberg® は各権利者の商標です。本ツールは非公式です。
