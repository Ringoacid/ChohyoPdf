# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 作業の約束

- 回答・コメント・ドキュメントは日本語で書く。
- コミットは任意のタイミングで行ってよい。**`git push`（タグの push を含む）と、NuGet への新しいバージョンの公開の前には、必ずユーザーの確認を取る。**
  - `v*` タグの push で nuget.org に公開され（`.github/workflows/release.yml`）、公開したバージョンは削除も上書きもできない。`designer-v*` タグの push ではデザイナーの ZIP が GitHub Releases に公開される。手順は `RELEASING.md`。
  - `main` への push で GitHub Pages のサイトも再公開される（`.github/workflows/pages.yml`）。

## プロジェクトの概要

日本語の帳票・文書を作る C# の PDF 生成ライブラリ（MIT、外部依存なし、生成専用）。開発者向けの詳しい説明は `CONTRIBUTING.md`、デザイナーは `tools/ChohyoPdf.Designer/README.md` にある。

- `src/ChohyoPdf`: ライブラリ本体。対象は `net462;netstandard2.0;net8.0;net10.0`。ソースに `#if` はなく、全フレームワークで同じコードをコンパイルするので、.NET Framework 4.6.2 / .NET Standard 2.0 にない API は使わない。
- `tools/ChohyoPdf.Designer.Core`（net10.0）: デザイナーのモデル・保存形式・PDF 出力・C# コード生成。`tools/ChohyoPdf.Designer` はその WPF の画面。
- `samples/ChohyoPdf.Samples`、`docs/examples/ChohyoPdf.DocExamples`: サンプル（後者は説明書のコードと画像の元）。

## コマンド

```
dotnet build ChohyoPdf.sln                     # WPF を含むので Windows で。ライブラリだけなら dotnet build src/ChohyoPdf
dotnet test tests/ChohyoPdf.Tests              # net8.0 / net10.0 / net48（net48 は net462 版のライブラリを使う）
dotnet test tests/ChohyoPdf.Tests -f net8.0 -p:LibraryTargetFramework=netstandard2.0   # netstandard2.0 版のテスト
dotnet test tests/ChohyoPdf.Designer.Tests     # デザイナーのテスト

# 1 つのテストだけ実行する（xUnit）
dotnet test tests/ChohyoPdf.Tests -f net10.0 --filter "FullyQualifiedName~ChohyoPdf.Tests.LayoutTests.メソッド名"

dotnet run --project samples/ChohyoPdf.Samples -f net8.0 -- 出力先フォルダ
dotnet run --project tools/ChohyoPdf.Designer
```

- CI（`.github/workflows/ci.yml`）は windows-latest で、Release 構成のビルドと上の 3 種類のテストを実行する。
- 日本語フォントを使うテストは `TestFonts.Japanese`（`tests/ChohyoPdf.Tests/TestHelpers.cs`）でフォントを探し、見つからない環境では検証を省略する。生成した PDF の構造は同ファイルの `PdfChecker` で検査する。
- 公開するクラスとメンバーにはドキュメントコメント（`///`）が必要（ないと CS1591 の警告）。API リファレンスもここから作られる。

## ドキュメントの生成

`docs/manual.md` は生成物なので直接編集しない。

- 文章は `docs/manual.template.md`、サンプルコードは `docs/examples/ChohyoPdf.DocExamples/*.cs` を直す。テンプレートの `<!-- example: 名前 -->` が、サンプルの `// #example 名前` 〜 `// #end` のコードに置き換わる。
- 生成: `python docs/tools/build_docs.py`（要 `pip install pillow markdown-it-py pygments`、`pdftoppm`/`pdfinfo`）。サンプルを実行して `docs/images` の画像を作り直し、`_site` にサイトを作る。文章だけなら `--skip-run`、API リファレンスを省くなら `--skip-api`。
- Pages のワークフローは `--skip-run` で動くので、サンプルを変えたら手元で生成した `docs/images` の画像もコミットする。
- README の「コードの例」は `Examples.QuickStart.cs` と同じでなければならない（違うとスクリプトがエラーにする）。README は NuGet のページにも出るので、画像・リンクは `https://` の絶対 URL で書く。
- `docs/designer.md` は直接編集する。

## アーキテクチャ

API は 2 層ある。

1. **低レベル層**（`PdfDocument` / `PdfPage` / `PdfGraphics` / `PdfPath`）: 座標を指定して文字・図形・画像を描く。`PdfDocument` はフォントと画像をリソースとして管理し、保存時にフォントを使った文字だけのサブセット（`Fonts/FontSubset.cs`）にして埋め込み、ToUnicode も作る。フォントは TrueType/TTC を自前で読む（`Fonts/`）。PNG の展開や zlib も `Images/`・`Internal/` に自前で実装している。
2. **自動レイアウト層**（`Layout/`）: `Document` → `Section` → `Block`（`Paragraph`・`Table`・`ImageBlock`・`ListBlock` など）を組み立て、`Document.Render()` で低レベル層の `PdfDocument` に描く。

自動レイアウトの流れ:

- 公開クラスの `Block` は `CreateNode(StyleScope)` で内部の `LayoutNode` を作る（`Layout/Blocks.cs` → `Layout/LayoutNodes.cs`、表は `TableNode.cs`）。
- `LayoutNode.Layout(ctx, width, available, cursor, atTop)` は、残りの高さに収まる分だけを `Slice`（高さ・描画処理 `DrawAction`・続きの位置 `Next`）として返す。ページをまたぐ分割はこのカーソルの受け渡しで行う。`atTop` が true なら、収まらなくても必ず何かを配置して処理を進める（無限ループ防止）。
- `LayoutNode.MeasureWidths` は表の列幅を内容に合わせるための計測で、副作用を持ってはいけない（予備のレイアウトで何度も呼ばれる）。
- `DocumentRenderer`（`Layout/DocumentRenderer.cs`）がページ割り付けを行う。ページ参照や目次があると、見出しのページが変わらなくなるまで描画しない予備のレイアウト（measuring パス）を最大 10 回繰り返してから本番を描く。ヘッダー/フッターや図形ブロックなどの利用者の描画処理は本番の 1 回だけ呼ぶ。見出し・アンカー・しおりの収集は `Layout/DocumentIndex.cs`。
- 段落とテキストボックスの改行・禁則処理・両端揃え・ルビ・タブは `Text/TextBlock.cs` に共通化されている。

デザイナー:

- `ReportRenderer`（デザインを PDF に描く）と `CSharpGenerator`（同じ PDF を作る C# コードを生成する）は、同じ順序・同じ計算で描くように作ってある。片方の描き方を変えたら必ずもう片方も変え、`tests/ChohyoPdf.Designer.Tests`（生成コードを Roslyn でコンパイル・実行して出力を比べる）で一致を確かめる。
- 配布は自己完結の win-x64 を ZIP にしたもの。`ScriptEvaluator` が実行時の DLL ファイルから Roslyn の参照を作るので、`PublishSingleFile` にはしない。
