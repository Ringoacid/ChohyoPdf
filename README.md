# ChohyoPdf

[![NuGet](https://img.shields.io/nuget/v/ChohyoPdf.svg)](https://www.nuget.org/packages/ChohyoPdf)
[![CI](https://github.com/Ringoacid/ChohyoPdf/actions/workflows/ci.yml/badge.svg)](https://github.com/Ringoacid/ChohyoPdf/actions/workflows/ci.yml)

日本語の帳票・文書を作るための、C# 製 PDF 生成ライブラリです。

- **MIT ライセンス**のみで提供します（デュアルライセンスではなく、GPL / AGPL でもありません）。商用利用もできます。
- 外部パッケージへの依存はありません。
- 対応フレームワークは .NET Framework 4.6.2 以降、.NET Standard 2.0、.NET 8、.NET 10 です（.NET 9 では net8.0 版、.NET Core 3.1 や .NET 5〜7 では netstandard2.0 版が使われます）。

![ChohyoPdf で作った 2 ページの請求書。明細の表が 2 ページ目に続き、見出し行が繰り返されている](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/sample-invoice.png)

## インストール

```
dotnet add package ChohyoPdf
```

## コードの例

段落や表を順に追加するだけで、折り返し・禁則処理・改ページはライブラリが行います。

```csharp
using ChohyoPdf;

var font = PdfFont.FromSystem("Yu Gothic Regular");      // 游ゴシック
font.BoldVariant = PdfFont.FromSystem("Yu Gothic Bold");

var doc = new Document(font, 10.5);
var section = doc.AddSection();                          // A4 縦・余白 20 mm

section.AddParagraph("御見積書").SetFontSize(18).SetBold().SetAlignment(TextAlignment.Center);
section.AddParagraph("株式会社サンプル 御中").SetFontSize(12).SetSpaceBefore(12);
section.AddParagraph("下記のとおり御見積り申し上げます。").SetSpaceAfter(8);

var table = section.AddTable();
table.AddColumns(1, 5, 1.2, 2);                          // 列幅の比率
table.Columns[2].Alignment = TextAlignment.Right;
table.Columns[3].Alignment = TextAlignment.Right;
table.HeaderBackground = PdfColor.FromHex("#DDEBF7");
table.AddHeaderRow("No.", "品名", "数量", "金額");     // 次のページにも繰り返す
table.AddRow("1", "ノートパソコン 14 インチ", "2", "396,000");
table.AddRow("2", "液晶モニター 27 インチ（高さ調整スタンド付き、ケーブル類一式を含む）", "4", "168,000");
table.AddRow("3", "USB-C ドック", "4", "52,800");

doc.Save("quick-start.pdf");
```

出力される PDF は次のとおりです。

![上のコードで作った見積書。中央揃えの表題、宛名、4 列の表](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/quick-start.png)

申込書や伝票のように決まった位置に印字する帳票は、座標を指定して文字や図形を描く API で作れます。2 つの API は組み合わせて使えます。

## 作例

どの画像も、ChohyoPdf で出力した PDF です。画像をクリックすると、作り方を説明した説明書の節を開きます。

**レポート**：名前付きスタイル、禁則処理と両端揃え、セルを結合した表、画像、グラフ、長い表の自動改ページ

[![4 ページのレポート](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/sample-report.png)](https://ringoacid.github.io/ChohyoPdf/manual.html#92-レポート)

**目次**：見出しから自動で作り、ページ番号も自動で確定します。前付けをローマ数字にするなど、ページ番号の書式も選べます

[![ローマ数字のページに置いた目次と、本文のページ](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/table-of-contents.png)](https://ringoacid.github.io/ChohyoPdf/manual.html#411-目次)

| 座標指定の帳票（均等割り付け、枠に合わせた縮小） | グラフ（図形の描画） |
| --- | --- |
| [![申込書](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/form.png)](https://ringoacid.github.io/ChohyoPdf/manual.html#84-申込書の例) | [![棒グラフと円グラフ](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/charts.png)](https://ringoacid.github.io/ChohyoPdf/manual.html#74-グラフ) |

| ルビ（振り仮名） | 表のセル結合 |
| --- | --- |
| [![ルビを付けた段落](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/ruby.png)](https://ringoacid.github.io/ChohyoPdf/manual.html#311-ルビ振り仮名) | [![見出しを 2 行にし、縦にセルを結合した集計表](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/table-merge.png)](https://ringoacid.github.io/ChohyoPdf/manual.html#53-セルの結合) |

このほか、段組み・脚注・箇条書き・ヘッダーとフッター・透かし・しおり・文書内リンク・透過 PNG・写真の向き（EXIF）の反映などに対応しています。機能の一覧は [説明書の目次](https://ringoacid.github.io/ChohyoPdf/manual.html) を見てください。PDF の読み込みと編集はできません（生成専用です）。

## GUI デザイナー

画面上で部品を並べて文書や帳票をデザインし、PDF や、同じ PDF を作る C# のコードとして出力できる Windows アプリ（ChohyoPdf Designer）もあります。宛名などをあとから入れる差し込み欄（`{顧客名}`）や、データの件数だけ行をくり返す表も作れます。

[ZIP ファイルをダウンロード](https://github.com/Ringoacid/ChohyoPdf/releases/latest/download/ChohyoPdfDesigner-win-x64.zip)（Windows 10 / 11 の x64、約 70 MB）し、展開して `ChohyoPdfDesigner.exe` を起動します。.NET のインストールは不要です。使い方は [ChohyoPdf Designer 説明書](https://ringoacid.github.io/ChohyoPdf/designer.html) を見てください。

![ChohyoPdf Designer の画面。ページの一覧、編集中のページ、表の書式設定が並んでいる](https://raw.githubusercontent.com/Ringoacid/ChohyoPdf/main/docs/images/designer/overview.png)

## ドキュメント

ドキュメントは [ChohyoPdf のサイト](https://ringoacid.github.io/ChohyoPdf/) にまとめています。

- [説明書](https://ringoacid.github.io/ChohyoPdf/manual.html)：機能ごとの使い方を、サンプルコードと出力結果の画像で説明しています。フォントの選び方、困ったときの対処、対応していない機能もここにあります（GitHub では [docs/manual.md](https://github.com/Ringoacid/ChohyoPdf/blob/main/docs/manual.md)）。
- [API リファレンス](https://ringoacid.github.io/ChohyoPdf/api/)：すべての公開クラスとメンバーの説明です。
- [ChohyoPdf Designer 説明書](https://ringoacid.github.io/ChohyoPdf/designer.html)：GUI デザイナーの使い方です（GitHub では [docs/designer.md](https://github.com/Ringoacid/ChohyoPdf/blob/main/docs/designer.md)）。
- [開発者向けの説明](https://github.com/Ringoacid/ChohyoPdf/blob/main/CONTRIBUTING.md)：ライブラリやデザイナーを改造する人向けに、ビルド・テスト・ドキュメントの作り直し方をまとめています。

## ライセンス

[MIT License](https://github.com/Ringoacid/ChohyoPdf/blob/main/LICENSE)
