# ChohyoPdf

日本語の帳票・文書を作るための、C# 製 PDF 生成ライブラリです。

- **MIT ライセンス**のみで提供します（デュアルライセンスではなく、GPL / AGPL でもありません）。商用利用もできます。
- PDFsharp / MigraDoc を含め、既存ライブラリのコードは使わずにゼロから実装しています。
- 外部パッケージへの依存はありません。
- 対応フレームワークは .NET Framework 4.6.2 以降、.NET Standard 2.0、.NET 8、.NET 10 です。.NET 9 では net8.0 版が、.NET Core 3.1 や .NET 5〜7 など .NET Standard 2.0 に対応した環境では netstandard2.0 版が使われます。

サンプルコードと出力結果の画像を載せた [説明書](https://github.com/Ringoacid/ChohyoPdf/blob/main/docs/manual.md) に、機能ごとの使い方をまとめています。

## できること

| 分類 | 機能 |
| --- | --- |
| 日本語テキスト | TrueType / TrueType Collection（.ttf / .ttc）フォントの埋め込み（使った文字だけのサブセット）、サロゲートペア（𠮷 など）、テキスト抽出・検索用の ToUnicode、代替フォント（フォールバック） |
| 組版 | 自動折り返し、禁則処理（行頭・行末）、欧文の単語単位の改行、左／中央／右揃え、両端揃え、均等割り付け、行送り、インデント、段落前後の間隔 |
| 文字装飾 | 太字・斜体（専用フォントがなければ疑似太字・疑似斜体）、下線、取り消し線、文字色、マーカー、字間、リンク |
| ページ送り | 自動改ページ、段落の分割（改ページ位置の調整あり）、「次の段落と分離しない」「段落を分割しない」、改ページ、セクションごとの用紙設定 |
| 表 | 固定幅・比率指定の列、横方向・縦方向のセル結合、罫線（太さ・色・破線）、背景色、縞模様、見出し行の繰り返し、ページをまたぐ行の分割、セル内への段落・画像・表の配置 |
| 図 | JPEG（CMYK を含む）と PNG（全カラータイプ・16 bit・インターレース・透過）、直線・矩形・角丸矩形・楕円・扇形・多角形・ベジェ曲線、破線、半透明、回転・拡大縮小、クリップ |
| ページ要素 | ヘッダー、フッター、ページ番号・総ページ数、背景や前景の描画（透かし・帳票の枠など） |
| 座標指定の描画 | 帳票向けに、座標を指定してテキストボックス（折り返し・縮小して全体を表示）や図形を描ける低レベル API |

PDF の読み込み・編集はできません（生成専用です）。

## インストール

```
dotnet add package ChohyoPdf
```

## 使い方

### 自動レイアウト（Document）

```csharp
using ChohyoPdf;

var font = PdfFont.FromSystem("Yu Gothic Regular");      // 游ゴシック
var doc = new Document(font, 10.5);
doc.Info.Title = "請求書";

var section = doc.AddSection();                           // 既定は A4 縦・余白 20 mm
section.Footer.AddParagraph().SetAlignment(TextAlignment.Center)
    .AddPageNumber().AddText(" / ").AddPageCount();

section.AddParagraph("請求書").SetFontSize(20).SetBold().SetAlignment(TextAlignment.Center);
section.AddParagraph("長い文章は本文の幅で自動的に折り返され、ページの終わりに来ると次のページへ送られます。");

var table = section.AddTable();
table.AddColumn(Unit.Mm(15));                             // 固定幅
table.AddRelativeColumn(3);                               // 残りの幅を 3:1 で分ける
table.AddRelativeColumn(1).Alignment = TextAlignment.Right;
table.HeaderBackground = PdfColor.FromHex("#DCE6F2");
table.AddHeaderRow("No.", "品名", "金額");                // 改ページ後も繰り返される
for (int i = 1; i <= 100; i++)
    table.AddRow(i.ToString(), "商品 " + i, (i * 1000).ToString("#,0"));

section.AddImage(PdfImage.FromFile("logo.png"), width: Unit.Mm(40));
section.AddDrawing(Unit.Mm(30), (g, rect) =>
{
    // (0, 0) が図の左上。単位はポイント
    g.DrawRectangle(new PdfPen(PdfColor.Blue, 1), PdfColor.FromHex("#EEF"), 0, 0, rect.Width, rect.Height);
});

doc.Save("invoice.pdf");
```

### 座標指定の描画（PdfDocument / PdfGraphics）

座標は**左上が原点、y は下向き、単位はポイント**（1/72 インチ）です。`Unit.Mm()` でミリメートルから変換できます。

```csharp
var pdf = new PdfDocument();
var page = pdf.AddPage(PageSize.A4);
var g = page.Graphics;
var style = new TextStyle(font, 11);

g.DrawText("左上が (x, y) の 1 行テキスト", Unit.Mm(20), Unit.Mm(20), style);

var box = new PdfRect(Unit.Mm(20), Unit.Mm(30), Unit.Mm(60), Unit.Mm(12));
g.DrawRectangle(new PdfPen(PdfColor.Black, 0.5), box.X, box.Y, box.Width, box.Height);
g.DrawTextBox("枠に収まらない長い文字列は縮小して表示します", box, style,
    new TextBoxOptions { ShrinkToFit = true, VerticalAlignment = VerticalAlignment.Middle });

g.DrawTextBox("氏名", new PdfRect(Unit.Mm(20), Unit.Mm(45), Unit.Mm(30), Unit.Mm(8)), style,
    new TextBoxOptions { Alignment = TextAlignment.Distribute });   // 均等割り付け

pdf.Save("form.pdf");
```

`Document.Render()` で、自動レイアウトした結果を `PdfDocument` として受け取り、さらに座標指定で描き足すこともできます。`Section.PageBackground` / `PageForeground` を使うと、全ページに枠や透かしを描けます。

### スタイル

`TextStyle` と `ParagraphFormat` のプロパティは、null のままにすると親の値を引き継ぎます。引き継ぎの順序は次のとおりです。

文書の既定値 → 表 → 行 → セル → 名前付きスタイル → 段落 → 文字列

```csharp
doc.DefaultTextStyle.FallbackFonts = new[] { PdfFont.FromSystem("Segoe UI Symbol") }; // グリフがない文字の代替
doc.DefaultParagraphFormat.Alignment = TextAlignment.Justify;
doc.DefaultParagraphFormat.LineSpacing = 1.6;             // 文字サイズ × 1.6 の行送り

doc.Styles["見出し"] = new ParagraphStyle(
    new TextStyle { FontSize = 14, Bold = true },
    new ParagraphFormat { SpaceBefore = 12, SpaceAfter = 6, KeepWithNext = true });
section.AddParagraph("1. 概要", "見出し");

var p = section.AddParagraph("通常の文字、");
p.AddText("赤い太字", new TextStyle { Color = PdfColor.Red, Bold = true });
p.AddLink("リンク", "https://example.com/");
```

表のセルとヘッダー/フッターの中では、本文用の字下げ・段落前後の間隔・揃えを引き継ぎません（行送りと文字スタイルは引き継ぎます）。

## フォント

- **TrueType アウトラインのフォント（.ttf / .ttc）に対応**しています。Windows の游ゴシック・游明朝・メイリオ・MS ゴシック・MS 明朝、Google Fonts の Noto Sans JP / Noto Serif JP（TTF 版）、IPAex フォントなどが使えます。
- 可変フォント（例: Windows 11 の `NotoSansJP-VF.ttf`）は、フォントの既定インスタンスの形で描画されます。既定インスタンスは Regular とは限りません（NotoSansJP-VF は Thin）。静的なフォント（Regular / Bold などウェイトごとのファイル）を使ってください。`PdfFont.IsVariableFont` で判定でき、`PdfFont.FromSystem` は同名の静的フォントを優先します。
- CFF アウトラインの OpenType（.otf。Source Han Sans や Noto Sans CJK の OTF 版など）には対応していません。読み込むと `NotSupportedException` になります。
- `PdfFont.FromFile(path, faceIndex)` または `PdfFont.FromFile(path, "MS PGothic")` のように指定して読み込みます。TTC の中のフォント名は `PdfFont.GetFaceNames(path)` で確認できます。
- `PdfFont.FromSystem("游ゴシック")` を使うと、OS のフォントフォルダを英語名・日本語名で検索します。
- 太字のフォントがあれば `font.BoldVariant = boldFont` のように対応付けます。対応付けがなければ疑似太字（輪郭を太らせる描画）になります。
- 埋め込むのは使った文字のグリフだけなので、ファイルサイズは小さく抑えられます。
- 1 つの `PdfFont` / `PdfImage` は複数の文書で共有できます。毎回読み込み直す必要はありません。

> **フォントのライセンスについて**
> PDF にフォントを埋め込んで配布してよいかどうかは、フォントごとのライセンスで決まります。利用するフォントのライセンスは、利用者自身で確認してください。
> OS/2 テーブルで埋め込みが禁止されているフォント（Restricted License）は、既定では読み込み時に例外になります。

## 制限事項

- 縦書き、合字・カーニングなどの OpenType レイアウト機能（GSUB / GPOS）、アラビア文字などの複雑な文字体系、右から左に書く文字には対応していません。
- 異体字セレクタ（IVS）は無視し、基底の文字のグリフで描画します。
- ヘッダー/フッターの高さに応じて本文の位置は変わりません。上下の余白（`PageSetup.Margins`）は、ヘッダー/フッターが収まる大きさにしてください。
- 改ページ（`AddPageBreak`）はページの先頭では無視されます。本文の最後に置くと、最後に白紙のページができます。
- 表の行は、ページ末尾に収まらなければ行ごと次のページへ送ります。`Row.AllowBreakAcrossPages = true` にすると、行を分割してページ末尾の余白を使います。1 ページに収まらない行は、設定にかかわらず分割します。
- JPEG の EXIF の回転情報は無視します（スマートフォンの写真が横向きのまま表示されることがあります）。
- 縦に結合したセルのグループ、および高さを固定（`RowHeightRule.Exactly`）した行は、ページをまたいで分割しません。1 ページに収まらない場合は、ページからはみ出して描画されます。
- 暗号化、電子署名、PDF/A、タグ付き PDF、フォーム（AcroForm）には対応していません。

## ビルドとテスト

```
dotnet build ChohyoPdf.sln
dotnet test tests/ChohyoPdf.Tests
dotnet test tests/ChohyoPdf.Tests -f net8.0 -p:LibraryTargetFramework=netstandard2.0   # netstandard2.0 版のテスト
dotnet run --project samples/ChohyoPdf.Samples -f net8.0 -- 出力先フォルダ
dotnet pack src/ChohyoPdf -c Release -o artifacts
python docs/tools/build_docs.py      # 説明書と画像を作り直す
```

テストは net8.0 / net10.0 / net48 で実行します（net48 では net462 版のライブラリを使います）。netstandard2.0 版は、上のように `LibraryTargetFramework` を指定して net8.0 で実行します。日本語フォントを使うテストのうち、フォントが見つからない環境では検証を省略するものがあります。

## ライセンス

[MIT License](https://github.com/Ringoacid/ChohyoPdf/blob/main/LICENSE)
