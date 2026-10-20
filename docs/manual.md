<!-- このファイルは docs/tools/build_docs.py で生成しています。編集は manual.template.md に行ってください。 -->

# ChohyoPdf 説明書

ChohyoPdf は、日本語の帳票や文書を PDF で出力するための C# ライブラリです。この説明書では、サンプルコードと出力結果の画像を並べて、主な機能の使い方を説明します。

掲載しているコードはすべて [docs/examples](examples/ChohyoPdf.DocExamples) にあり、そのまま実行できます。画像は、そのコードで出力した PDF を画像にしたものです。

## 目次

1. [はじめに](#1-はじめに)
2. [単位と座標](#2-単位と座標)
3. [文字と段落](#3-文字と段落)
4. [ページの設定とページ送り](#4-ページの設定とページ送り)
5. [表](#5-表)
6. [画像](#6-画像)
7. [図形とグラフ](#7-図形とグラフ)
8. [座標を指定して描く帳票](#8-座標を指定して描く帳票)
9. [まとまったサンプル](#9-まとまったサンプル)
10. [困ったときは](#10-困ったときは)
11. [API の一覧](#11-api-の一覧)
12. [この説明書の作り直し方](#12-この説明書の作り直し方)

---

## 1. はじめに

### 1.1 動作環境とライセンス

- 対応フレームワークは .NET Framework 4.6.2 以降、.NET Standard 2.0、.NET 8、.NET 10 です。.NET 9 では .NET 8 向けの DLL が、.NET Core 3.1 や .NET 5〜7 など .NET Standard 2.0 に対応した環境では .NET Standard 2.0 向けの DLL が使われます。
- ライセンスは MIT です。商用のアプリケーションにも組み込めます。
- 他のパッケージには依存しません。

### 1.2 プロジェクトへの追加

ソースから使う場合は、`src/ChohyoPdf/ChohyoPdf.csproj` をプロジェクト参照に追加します。

```xml
<ItemGroup>
  <ProjectReference Include="..\ChohyoPdf\src\ChohyoPdf\ChohyoPdf.csproj" />
</ItemGroup>
```

NuGet パッケージ（.nupkg）を作って参照する場合は、次のようにします。

```
dotnet pack src/ChohyoPdf -c Release -o artifacts
dotnet add package ChohyoPdf --source ./artifacts
```

コードの先頭には `using ChohyoPdf;` を書きます。

### 1.3 2 種類の API

ChohyoPdf には、用途の違う 2 種類の API があります。フォント・画像・色のクラスは、どちらの API でも共通です。

| API | 向いている用途 | 主なクラス |
| --- | --- | --- |
| 自動レイアウト | 報告書・請求書の明細など、量によってページ数が変わる文書 | `Document`、`Section`、`Paragraph`、`Table` |
| 座標指定 | 申込書・伝票・ラベルなど、決まった位置に印字する帳票 | `PdfDocument`、`PdfPage`、`PdfGraphics` |

自動レイアウトでは、段落や表を順に追加するだけで、折り返しと改ページをライブラリが行います。座標指定では、ページ上の位置を数値で指定して文字や図形を描きます。2 つを組み合わせることもできます（[8.5](#85-自動レイアウトの結果に描き足す)）。

### 1.4 フォントの準備

PDF に日本語を表示するには、フォントを読み込んで文書に埋め込みます。ChohyoPdf は使った文字の形だけを埋め込むので、ファイルサイズは数十 KB 程度に収まります。

この説明書のサンプルでは、次のコードで用意した `font`（游ゴシック）と `mincho`（游明朝）を使います。

```csharp
// 游ゴシック（Windows 標準）。太字は専用のフォントを対応付ける
var font = PdfFont.FromSystem("Yu Gothic Regular");
font.BoldVariant = PdfFont.FromSystem("Yu Gothic Bold");

// 游明朝
var mincho = PdfFont.FromSystem("Yu Mincho Regular");
```

`PdfFont.FromSystem` は、OS のフォントフォルダから名前でフォントを探します。「游ゴシック」のような日本語名でも探せます。フォントの読み込み方は [10.1](#101-フォントが見つからない) にもまとめています。

### 1.5 最初の PDF

自動レイアウトで、見出しと本文だけの PDF を作ります。

```csharp
var doc = new Document(font, 10.5);          // 既定のフォントと文字サイズ
var section = doc.AddSection();              // A4 縦・余白 20 mm

section.AddParagraph("はじめての PDF").SetFontSize(18).SetBold().SetSpaceAfter(6);
section.AddParagraph(
    "ChohyoPdf で作った文書です。長い文章は本文の幅に合わせて自動的に折り返され、" +
    "ページの終わりまで来ると続きが次のページに送られます。" +
    "句読点や閉じかっこが行の先頭に来ないように、禁則処理も行います。");

doc.Save("hello.pdf");
```

![見出しと本文が 1 段落ある PDF](images/hello.png)

座標指定の API では、ページを追加して `PdfGraphics` に描きます。

```csharp
var pdf = new PdfDocument();
var page = pdf.AddPage(PageSize.A6.Landscape);   // 148 × 105 mm
var g = page.Graphics;

g.DrawRectangle(new PdfPen(PdfColor.Navy, 1), Unit.Mm(8), Unit.Mm(8), Unit.Mm(132), Unit.Mm(89));
g.DrawText("座標を指定して描く", Unit.Mm(14), Unit.Mm(14), new TextStyle(font, 16));
g.DrawCircle(null, PdfColor.FromHex("#F4B183"), Unit.Mm(50), Unit.Mm(62), Unit.Mm(20));
g.DrawRectangle(null, PdfColor.FromHex("#9DC3E6"), Unit.Mm(80), Unit.Mm(42), Unit.Mm(45), Unit.Mm(40));

pdf.Save("hello-graphics.pdf");
```

![枠の中に文字と円と四角形を描いた PDF](images/hello-graphics.png)

---

## 2. 単位と座標

長さの単位は **ポイント**（1/72 インチ、約 0.353 mm）です。ミリメートルで指定するときは `Unit.Mm()` で変換します。

| メソッド | 変換 |
| --- | --- |
| `Unit.Mm(10)` | 10 mm → 28.35 pt |
| `Unit.Cm(1)` | 1 cm → 28.35 pt |
| `Unit.Inch(1)` | 1 インチ → 72 pt |
| `Unit.ToMm(72)` | 72 pt → 25.4 mm |

座標の原点はページの **左上** で、x は右に、y は下に向かって大きくなります。文字を描く位置は、文字の上端の座標で指定します。

```csharp
var pdf = new PdfDocument();
var page = pdf.AddPage(new PageSize(Unit.Mm(120), Unit.Mm(80)));
var g = page.Graphics;
var small = new TextStyle(font, 7) { Color = PdfColor.Gray };

// 10 mm ごとの方眼
for (int mm = 0; mm <= 120; mm += 10)
{
    g.DrawLine(new PdfPen(PdfColor.LightGray, 0.3), Unit.Mm(mm), 0, Unit.Mm(mm), page.Height);
    if (mm > 0) g.DrawText(mm.ToString(), Unit.Mm(mm) + 1, 1, small);
}
for (int mm = 10; mm <= 80; mm += 10)
{
    g.DrawLine(new PdfPen(PdfColor.LightGray, 0.3), 0, Unit.Mm(mm), page.Width, Unit.Mm(mm));
    g.DrawText(mm.ToString(), 1, Unit.Mm(mm) + 1, small);
}

var red = new TextStyle(font, 9) { Color = PdfColor.Red };
g.DrawText("(0, 0)", Unit.Mm(2), Unit.Mm(3), red);
g.DrawText("x →", Unit.Mm(12), Unit.Mm(6), red);
g.DrawText("y ↓", Unit.Mm(2), Unit.Mm(13), red);

// (40 mm, 30 mm) に幅 50 mm・高さ 25 mm の矩形
g.DrawRectangle(new PdfPen(PdfColor.Blue, 1), PdfColor.Blue.WithAlpha(0.1), Unit.Mm(40), Unit.Mm(30), Unit.Mm(50), Unit.Mm(25));
g.DrawCircle(null, PdfColor.Blue, Unit.Mm(40), Unit.Mm(30), 2);
g.DrawText("(Unit.Mm(40), Unit.Mm(30))", Unit.Mm(42), Unit.Mm(32), new TextStyle(font, 8) { Color = PdfColor.Blue });

pdf.Save("coordinates.pdf");
```

![10 mm 方眼の上に、(40 mm, 30 mm) から始まる矩形を描いた図](images/coordinates.png)

文字の大きさもポイントで指定します。Word の「10.5 ポイント」と同じ大きさです。

---

## 3. 文字と段落

### 3.1 文字の装飾

`Paragraph.AddText` に `TextStyle` を渡すと、その部分だけ書式を変えられます。`TextStyle` のうち指定しなかった項目は、段落や文書の設定を引き継ぎます。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

s.AddParagraph().AddText("大きさ　")
    .AddText("8pt ", new TextStyle { FontSize = 8 })
    .AddText("12pt ", new TextStyle { FontSize = 12 })
    .AddText("18pt", new TextStyle { FontSize = 18 });
s.AddParagraph().AddText("太字と斜体　")
    .AddBold("太字").AddText(" / ")
    .AddText("斜体", new TextStyle { Italic = true }).AddText(" / ")
    .AddText("太字の斜体", new TextStyle { Bold = true, Italic = true });
s.AddParagraph().AddText("線　")
    .AddText("下線", new TextStyle { Underline = true }).AddText(" / ")
    .AddText("取り消し線", new TextStyle { Strikeout = true });
s.AddParagraph().AddText("色　")
    .AddText("赤 ", new TextStyle { Color = PdfColor.Red })
    .AddText("#2E75B6 ", new TextStyle { Color = PdfColor.FromHex("#2E75B6") })
    .AddText("マーカー", new TextStyle { BackgroundColor = PdfColor.FromHex("#FFE699") });
s.AddParagraph().AddText("字間　")
    .AddText("ひろげる", new TextStyle { CharacterSpacing = 5 }).AddText(" / ")
    .AddText("つめる", new TextStyle { CharacterSpacing = -1.5 });
s.AddParagraph().AddText("フォント　")
    .AddText("游ゴシック / ").AddText("游明朝", new TextStyle { Font = mincho });
s.AddParagraph().AddText("リンク　").AddLink("example.com を開く", "https://example.com/");

// 段落全体にまとめて指定することもできる
s.AddParagraph("段落全体を紺色の 14pt にする").SetFontSize(14).SetColor(PdfColor.Navy).SetSpaceBefore(6);

doc.Save("text-decoration.pdf");
```

![大きさ・太字・斜体・下線・色・マーカー・字間・フォント・リンクの例](images/text-decoration.png)

- 太字は、`PdfFont.BoldVariant` に太字のフォントを設定していればそのフォントで描きます。設定していなければ、文字の輪郭を太らせて描きます（疑似太字）。
- 斜体の日本語フォントはほとんどないので、通常は文字を傾けて描きます（疑似斜体）。
- `AddLink` で作ったリンクは、PDF ビューアーでクリックすると URL を開きます。

### 3.2 揃え

`SetAlignment` で、段落の揃えを指定します。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
string text = "ChohyoPdf は、日本語の帳票を作るための PDF ライブラリです。" +
              "Visual Studio や Visual Studio Code から、NuGet パッケージとして参照できます。";
var frame = new PdfPen(PdfColor.LightGray, 0.5);

foreach (var align in new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right, TextAlignment.Justify })
{
    s.AddParagraph(align.ToString()).SetFontSize(8).SetColor(PdfColor.Gray);
    s.AddParagraph(text).SetAlignment(align).SetBorder(frame, new Thickness(4))
        .SetIndent(0, 0, Unit.Mm(50)).SetSpaceAfter(6);
}

// 均等割り付けは最終行も両端に揃える。短い項目名をそろえるときに使う
s.AddParagraph("Distribute").SetFontSize(8).SetColor(PdfColor.Gray);
foreach (string label in new[] { "氏名", "住所", "電話番号" })
    s.AddParagraph(label).SetAlignment(TextAlignment.Distribute).SetBorder(frame, new Thickness(4))
        .SetIndent(0, 0, Unit.Mm(145));

doc.Save("text-alignments.pdf");
```

![左揃え・中央揃え・右揃え・両端揃え・均等割り付けの比較](images/text-alignments.png)

| 値 | 動作 |
| --- | --- |
| `Left` | 左揃え（既定） |
| `Center` | 中央揃え |
| `Right` | 右揃え |
| `Justify` | 両端揃え。最終行以外の行の右端をそろえます |
| `Distribute` | 均等割り付け。最終行も含めて、文字の間隔を広げて両端にそろえます |

### 3.3 行送りとインデント

行送り（行の上端から次の行の上端までの距離）は、文字の大きさに対する倍率で指定します。既定値は 1.5 倍です。`LineSpacingRule.Exactly` を指定すると固定の高さに、`AtLeast` を指定すると最小の高さになります。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
string text = "行送りは文字の大きさに対する倍率で指定します。既定値は 1.5 倍です。" +
              "固定の高さ（ポイント）で指定することもできます。";
var frame = new PdfPen(PdfColor.LightGray, 0.5);

s.AddParagraph("行送り 1.0").SetFontSize(8).SetColor(PdfColor.Gray);
s.AddParagraph(text).SetLineSpacing(1.0).SetBorder(frame, new Thickness(4)).SetSpaceAfter(6);
s.AddParagraph("行送り 2.0").SetFontSize(8).SetColor(PdfColor.Gray);
s.AddParagraph(text).SetLineSpacing(2.0).SetBorder(frame, new Thickness(4)).SetSpaceAfter(6);
s.AddParagraph("行送り 固定 14pt").SetFontSize(8).SetColor(PdfColor.Gray);
s.AddParagraph(text).SetLineSpacing(14, LineSpacingRule.Exactly).SetBorder(frame, new Thickness(4)).SetSpaceAfter(12);

s.AddParagraph("字下げ（1 行目だけ 1 文字下げる）").SetFontSize(8).SetColor(PdfColor.Gray);
s.AddParagraph(text).SetIndent(0, firstLine: 10.5).SetBorder(frame, new Thickness(4)).SetSpaceAfter(6);
s.AddParagraph("左右のインデント（20 mm）").SetFontSize(8).SetColor(PdfColor.Gray);
s.AddParagraph(text).SetIndent(Unit.Mm(20), 0, Unit.Mm(20)).SetBorder(frame, new Thickness(4)).SetSpaceAfter(6);
s.AddParagraph("ぶら下げインデント（2 行目以降を下げる）").SetFontSize(8).SetColor(PdfColor.Gray);
s.AddParagraph("※　" + text).SetIndent(21, firstLine: -21).SetBorder(frame, new Thickness(4));

doc.Save("line-spacing.pdf");
```

![行送りの違いと、字下げ・左右のインデント・ぶら下げインデントの例](images/line-spacing.png)

`SetIndent(left, firstLine, right)` の `firstLine` は、1 行目だけに加える量です。負の値を指定すると、1 行目が左に出るぶら下げインデントになります。

### 3.4 折り返しと禁則処理

段落は本文の幅で自動的に折り返します。折り返すときは、次の規則に従います。

- 句読点・閉じかっこ・小書きの仮名（ゃ、っ など）・長音記号は、行の先頭に置きません。
- 開きかっこは、行の末尾に置きません。
- 英単語や数字は、単語の途中で改行しません。
- 規則を守れないときは、前の文字ごと次の行へ送ります（追い出し）。

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(48))).Graphics;
var style = new TextStyle(font, 12);
var options = new TextBoxOptions { LineSpacingRule = LineSpacingRule.Exactly, LineSpacing = 18 };
var grid = new PdfPen(PdfColor.FromHex("#BDD7EE"), 0.5);

// 1 行 7 文字のマス目に重ねて描き、行末が空く位置を見る
string[] texts =
{
    "吾輩は猫である。名前はまだ無い。",
    "長い文章です（注釈付き）。新しいコンピューターを買った。",
};
for (int i = 0; i < texts.Length; i++)
{
    double x = Unit.Mm(8 + i * 45), y = Unit.Mm(8);
    for (int row = 0; row < 5; row++)
        for (int col = 0; col < 7; col++)
            g.DrawRectangle(grid, x + col * 12, y + row * 18, 12, 18);
    g.DrawTextBox(texts[i], new PdfRect(x, y, 12 * 7 + 0.5, 18 * 5), style, options);
}

// 英単語は単語の途中で改行しない
var box = new PdfRect(Unit.Mm(100), Unit.Mm(8), Unit.Mm(60), 18 * 5);
g.DrawRectangle(grid, box.X, box.Y, box.Width, box.Height);
g.DrawTextBox("Long words such as typography are never split. 日本語と English が混ざっても同じです。",
    box, style, options);

pdf.Save("line-breaking.pdf");
```

![1 行 7 文字のマス目に重ねた禁則処理の例と、英単語の折り返しの例](images/line-breaking.png)

左の例では、1 行目に 7 文字入りますが、8 文字目の「。」が行の先頭に来ないよう、「る」も 2 行目に送っています。中央の例では、「（」が行の末尾に来ないよう 1 行目を 6 文字で改行しています。3 行目は、「ピュー」の小書きの仮名と長音記号が行の先頭に来ないよう、5 文字で改行しています。

この例は座標指定の `DrawTextBox` で描いていますが、自動レイアウトの段落でも同じ規則で折り返します。

### 3.5 特殊な文字と代替フォント

サロゲートペアで表す漢字（𠮷、𩸽 など）や、丸数字などの記号も、フォントにあればそのまま描けます。フォントにない文字は、`FallbackFonts` に指定したフォントで描きます。

```csharp
var doc = new Document(font, 12);
// 游ゴシックにない文字（チェスの駒など）は、代替フォントで描く
doc.DefaultTextStyle.FallbackFonts = new[] { PdfFont.FromSystem("Segoe UI Symbol") };
var s = doc.AddSection();

s.AddParagraph("サロゲートペアの漢字：𠮷野家の𠮷、𩸽（ほっけ）");
s.AddParagraph("記号：①②③ ㈱ ℡ ※ 〒 ♪ ← → ↑ ↓");
s.AddParagraph("代替フォント：♔ ♕ ♖ ♗ ♘ ♙");
s.AddParagraph("半角カナ・全角英数：ｱｲｳｴｵ ＡＢＣ１２３");

doc.Save("special-characters.pdf");
```

![サロゲートペアの漢字、記号、代替フォントで描いたチェスの駒](images/special-characters.png)

代替フォントにもない文字は、フォントの「.notdef」グリフ（多くのフォントでは空白か四角）で描きます。

### 3.6 背景と枠

段落に背景色と枠線を付けられます。注意書きの囲みなどに使えます。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

s.AddParagraph("ご注意").SetBold().SetColor(PdfColor.FromHex("#C55A11"));
s.AddParagraph("背景色と枠線を付けた段落です。内側の余白は Thickness で指定します。" +
               "段落がページをまたぐときも、背景と枠はページごとに描きます。")
    .SetBackground(PdfColor.FromHex("#FBE5D6"), new Thickness(8, 6))
    .SetBorder(new PdfPen(PdfColor.FromHex("#ED7D31"), 1))
    .SetSpaceAfter(12);

s.AddParagraph("背景色だけの段落。コードや補足の囲みに使えます。")
    .SetBackground(PdfColor.FromGray(0.94), new Thickness(8, 6))
    .SetSpaceAfter(12);

s.AddHorizontalLine(new PdfPen(PdfColor.Gray, 0.5));
s.AddParagraph("水平線（AddHorizontalLine）で区切ることもできます。");

doc.Save("paragraph-boxes.pdf");
```

![背景色と枠線を付けた段落、背景色だけの段落、水平線](images/paragraph-boxes.png)

### 3.7 名前付きスタイル

同じ書式を何度も使うときは、名前付きスタイルに登録します。

```csharp
var doc = new Document(mincho, 10.5);                 // 本文は游明朝
doc.DefaultParagraphFormat.Alignment = TextAlignment.Justify;
doc.DefaultParagraphFormat.FirstLineIndent = 10.5;    // 全段落を 1 文字字下げ
doc.DefaultParagraphFormat.SpaceAfter = 4;

doc.Styles["見出し1"] = new ParagraphStyle(
    new TextStyle { Font = font, FontSize = 15, Bold = true, Color = PdfColor.Navy },
    new ParagraphFormat { FirstLineIndent = 0, SpaceBefore = 10, SpaceAfter = 6, KeepWithNext = true });
doc.Styles["見出し2"] = new ParagraphStyle(
    new TextStyle { Font = font, FontSize = 12, Bold = true },
    new ParagraphFormat { FirstLineIndent = 0, SpaceBefore = 8, SpaceAfter = 4, KeepWithNext = true });
doc.Styles["注記"] = new ParagraphStyle(
    new TextStyle { Font = font, FontSize = 8.5, Color = PdfColor.DarkGray },
    new ParagraphFormat { FirstLineIndent = 0, LeftIndent = 10.5 });

var s = doc.AddSection();
s.AddParagraph("1. はじめに", "見出し1");
s.AddParagraph("名前付きスタイルを登録すると、段落ごとに書式を並べなくても、見出しや注記の見た目をそろえられます。" +
               "スタイルを書き換えれば、そのスタイルを使うすべての段落に反映されます。");
s.AddParagraph("1.1 本文の書式", "見出し2");
s.AddParagraph("本文は游明朝の両端揃えで、1 文字字下げしています。見出しと注記は游ゴシックにしています。");
s.AddParagraph("※ 注記スタイルの段落です。小さめの灰色の文字になります。", "注記");
s.AddParagraph("※ 段落で直接指定した値は、スタイルより優先されます。", "注記").SetColor(PdfColor.Red);

doc.Save("named-styles.pdf");
```

![見出し 1、見出し 2、本文、注記のスタイルを使った文書](images/named-styles.png)

書式は、次の順に引き継がれます。後に書いたものほど優先されます。

文書の既定値（`DefaultTextStyle`、`DefaultParagraphFormat`）→ 表 → 行 → セル → 名前付きスタイル → 段落 → 文字列（`AddText` のスタイル）

`TextStyle` と `ParagraphFormat` のプロパティは、null のままにしておくと上の階層の値を引き継ぎます。

---

## 4. ページの設定とページ送り

### 4.1 用紙と余白

用紙の大きさと余白は、セクションごとに `PageSetup` で指定します。既定値は A4 縦、余白は上下左右 20 mm です。

```csharp
var doc = new Document(font, 12);
var s = doc.AddSection(new PageSetup
{
    Size = PageSize.A5.Landscape,                    // A5 横
    Margins = Thickness.FromMm(15, 12, 15, 12),      // 左, 上, 右, 下
});

// 本文の領域（ContentArea）を破線で示す
s.PageBackground = (g, ctx) =>
{
    var area = ctx.ContentArea;
    g.DrawRectangle(PdfPen.Dashed(PdfColor.Gray, 0.5, 3, 2), area.X, area.Y, area.Width, area.Height);
};

s.AddParagraph("A5 横・余白は左右 15 mm、上下 12 mm").SetBold();
s.AddParagraph($"本文の幅 {Unit.ToMm(s.PageSetup.ContentWidth):0} mm × 高さ {Unit.ToMm(s.PageSetup.ContentHeight):0} mm");

doc.Save("page-setup.pdf");
```

![A5 横の用紙に、本文の領域を破線で示した図](images/page-setup.png)

用意している用紙サイズは、`A3`、`A4`、`A5`、`A6`、`B4`、`B5`（JIS）、`Letter`、`Legal`、`Postcard`（はがき）、`EnvelopeChou3`（長形 3 号）です。`.Landscape` で横向きになります。その他の大きさは `PageSize.FromMm(幅, 高さ)` で作れます。

### 4.2 ヘッダーとフッター

`Section.Header` と `Section.Footer` には、本文と同じように段落や表を追加できます。`AddPageNumber()` はページ番号、`AddPageCount()` は総ページ数に置き換わります。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
s.PageSetup.Margins = Thickness.FromMm(20, 25, 20, 22);   // ヘッダー/フッターの分だけ余白を広げる

s.Header.AddParagraph("社内報告書　2026 年 10 月").SetFontSize(8).SetColor(PdfColor.Gray)
    .SetAlignment(TextAlignment.Right);
s.Header.AddHorizontalLine(new PdfPen(PdfColor.Gray, 0.5));

s.Footer.AddParagraph().SetAlignment(TextAlignment.Center).SetFontSize(9)
    .AddPageNumber().AddText(" / ").AddPageCount().AddText(" ページ");

s.AddParagraph("ヘッダーとフッター").SetFontSize(16).SetBold().SetSpaceAfter(6);
for (int i = 0; i < 12; i++)
    s.AddParagraph(Filler(8)).SetSpaceAfter(6);

doc.Save("header-footer.pdf");
```

![ヘッダーに右寄せの文字と水平線、フッターに「1 / 2 ページ」を表示した 2 ページの文書](images/header-footer.png)

- ヘッダーは用紙の上端から `PageSetup.HeaderDistance`（既定 8 mm）、フッターは下端から `FooterDistance`（既定 8 mm）の位置に置きます。
- ヘッダーとフッターの高さによって、本文の位置は変わりません。上下の余白は、ヘッダーとフッターが収まる大きさにしてください。
- 各セクションの 1 ページ目だけヘッダーを出さないときは、`Header.ShowOnFirstPage = false` にします。

### 4.3 改ページ

`AddPageBreak()` を置いた位置、または `SetPageBreakBefore()` を指定した段落の前で改ページします。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

s.AddParagraph("第 1 章").SetFontSize(18).SetBold();
s.AddParagraph("1 ページ目の本文です。");
s.AddPageBreak();                                         // ここで改ページ

s.AddParagraph("第 2 章").SetFontSize(18).SetBold();
s.AddParagraph("2 ページ目の本文です。");

s.AddParagraph("第 3 章").SetFontSize(18).SetBold().SetPageBreakBefore();   // 段落の前で改ページ
s.AddParagraph("3 ページ目の本文です。");

doc.Save("page-breaks.pdf");
```

![第 1 章から第 3 章が 1 ページずつに分かれた 3 ページの文書](images/page-breaks.png)

ページの先頭にある改ページは無視します。白紙のページが続くのを防ぐためです。ただし、本文の最後に改ページを置くと、最後のページが白紙になります。

### 4.4 見出しを次の段落と同じページに置く

ページの最後に見出しだけが残ると読みにくくなります。見出しに `SetKeepWithNext()` を指定すると、次の段落と同じページに置きます。`SetKeepTogether()` を指定した段落は、途中で分割しません。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

// ページの下のほうまで本文が詰まっている状態を、灰色の箱で表す
s.AddDrawing(s.PageSetup.ContentHeight - 70, (g, r) =>
{
    g.FillRectangle(PdfColor.FromGray(0.93), 0, 0, r.Width, r.Height);
    g.DrawText("（ここまで本文があるとします）", r.Width / 2, r.Height / 2, new TextStyle(font, 14), TextAlignment.Center);
});

// 見出しだけがページの最後に残らないよう、次の段落と同じページに置く
s.AddParagraph("2. 次の章の見出し").SetFontSize(14).SetBold().SetKeepWithNext();
// 段落を途中で分割しない
s.AddParagraph(Filler(5)).SetKeepTogether();

doc.Save("keep-with-next.pdf");
```

![1 ページ目の下に余白があるが、見出しと段落がまとめて 2 ページ目に送られた様子](images/keep-with-next.png)

1 ページ目の下には見出しが入る余白がありますが、続く段落が入りきらないため、見出しも 2 ページ目に送っています。

このほか、段落の 1 行目だけがページの最後に残ったり、最後の 1 行だけが次のページに送られたりしないよう、改ページの位置を調整します（`ParagraphFormat.WidowControl`、既定で有効）。

### 4.5 セクション

用紙の向きやヘッダーを途中で変えるときは、セクションを分けます。セクションは必ず新しいページから始まります。

```csharp
var doc = new Document(font, 10.5);

// 表紙（フッターなし）
var cover = doc.AddSection();
cover.AddSpacer(Unit.Mm(90));
cover.AddParagraph("年次報告書").SetFontSize(28).SetBold().SetAlignment(TextAlignment.Center);
cover.AddParagraph("2026 年度").SetFontSize(14).SetAlignment(TextAlignment.Center);

// 本文（セクションごとのページ番号）
var body = doc.AddSection();
body.Footer.AddParagraph().SetAlignment(TextAlignment.Center)
    .AddText("- ").AddField(FieldType.SectionPageNumber).AddText(" -");
body.AddParagraph("本文").SetFontSize(16).SetBold();
body.AddParagraph(Filler(10));

// 横向きのページ（幅の広い表など）
var wide = doc.AddSection(new PageSetup { Size = PageSize.A4.Landscape });
wide.AddParagraph("横向きのセクション").SetFontSize(16).SetBold();
wide.AddParagraph("セクションごとに用紙の大きさ・向き・余白、ヘッダーとフッターを変えられます。");

doc.Save("sections.pdf");
```

![表紙、本文、横向きのページの 3 ページ](images/sections.png)

`FieldType.SectionPageNumber` を使うと、セクションの中でのページ番号（表紙を数えないページ番号など）を表示できます。

### 4.6 透かしとページの装飾

`PageBackground` は本文の下に、`PageForeground` は本文の上に、全ページ共通の図形を描きます。帳票の枠、ロゴ、透かしなどに使います。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

// 本文の下に描く：ページ上端の帯
s.PageBackground = (g, ctx) =>
{
    g.FillRectangle(PdfColor.FromHex("#1F4E79"), 0, 0, ctx.PageSize.Width, Unit.Mm(10));
    g.DrawText("ACME 株式会社", Unit.Mm(20), Unit.Mm(3), new TextStyle(font, 9) { Color = PdfColor.White });
};

// 本文の上に描く：半透明の透かし
s.PageForeground = (g, ctx) =>
{
    double cx = ctx.PageSize.Width / 2, cy = ctx.PageSize.Height / 2;
    g.SaveState();
    g.RotateAt(-30, cx, cy);                         // 反時計回りに 30 度
    g.DrawText("社外秘", cx, cy - 45, new TextStyle(font, 90) { Color = PdfColor.Red.WithAlpha(0.2) },
        TextAlignment.Center);
    g.RestoreState();
};

s.AddParagraph("透かしとページの装飾").SetFontSize(16).SetBold().SetSpaceAfter(6);
for (int i = 0; i < 6; i++) s.AddParagraph(Filler(6)).SetSpaceAfter(6);

doc.Save("watermark.pdf");
```

![上端に紺色の帯、中央に半透明の「社外秘」の透かしがあるページ](images/watermark.png)

2 つ目の引数 `PageContext` からは、ページ番号（`PageNumber`）、総ページ数（`PageCount`）、用紙の大きさ（`PageSize`）、本文の領域（`ContentArea`）を取得できます。

---

## 5. 表

### 5.1 表の基本

列を定義してから行を追加します。`AddHeaderRow` で追加した見出し行は、表が次のページに続くときに繰り返し表示されます。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

var t = s.AddTable();
t.AddColumns(1, 4, 1.2, 2);                          // 列幅の比率
t.HeaderBackground = PdfColor.FromHex("#DDEBF7");
t.HeaderStyle = new TextStyle { Bold = true };
t.AddHeaderRow("No.", "品名", "数量", "金額");
t.AddRow("1", "ノートパソコン 14 インチ", "2", "396,000");
t.AddRow("2", "液晶モニター 27 インチ（高さ調整スタンド付き、ケーブル類一式を含む）", "4", "168,000");
t.AddRow("3", "USB-C ドック", "4", "52,800");

doc.Save("table-basic.pdf");
```

![見出し行に背景色を付けた 4 列の表](images/table-basic.png)

セルに入りきらない文字列は、セルの中で折り返します。行の高さは、いちばん高いセルに合わせて広がります。

### 5.2 列幅と揃え

列幅は、固定幅（`AddColumn`）と比率（`AddRelativeColumn`、`AddColumns`）で指定します。固定幅の列を除いた残りの幅を、比率で分け合います。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

var t = s.AddTable();
t.AddColumn(Unit.Mm(15)).Alignment = TextAlignment.Center;     // 固定幅・中央揃え
t.AddRelativeColumn(3);                                         // 残りを 3 : 1 で分ける
var amount = t.AddRelativeColumn(1);
amount.Alignment = TextAlignment.Right;                         // 金額は右揃え
amount.Background = PdfColor.FromHex("#FFF2CC");                // 列の背景色
t.AddHeaderRow("コード", "内容", "金額");
t.AddRow("A01", "固定幅 15 mm の列", "1,200");
t.AddRow("A02", "比率 3 の列", "34,500");
t.AddRow("A03", "比率 1 の列は右揃え", "678,900");

s.AddSpacer(12);

// 列がすべて固定幅なら、表の幅は列幅の合計になる。Alignment で位置を決める
var small = s.AddTable();
small.Alignment = HorizontalAlignment.Center;
small.AddColumn(Unit.Mm(30));
small.AddColumn(Unit.Mm(30)).Alignment = TextAlignment.Right;
small.AddHeaderRow("項目", "値");
small.AddRow("中央に配置", "60 mm");

doc.Save("table-columns.pdf");
```

![固定幅・比率・右揃え・列の背景色を使った表と、中央に配置した小さな表](images/table-columns.png)

### 5.3 セルの結合

`Cell.Merge(列数, 行数)` で、右と下のセルを結合します。結合で隠れるセルには何も入れず、`AddRow` では `null` を渡します。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

var t = s.AddTable();
t.AddColumns(1.2, 1.5, 1, 1, 1, 1);
t.VerticalAlignment = VerticalAlignment.Middle;
t.HeaderBackground = PdfColor.FromHex("#E2EFDA");

// 見出し 2 行：結合されて隠れるセルには null を渡す
var h1 = t.AddHeaderRow("地域", "店舗", "上期", null!, "下期", null!);
h1[0].Merge(1, 2);                                   // 縦に 2 行
h1[1].Merge(1, 2);
h1[2].Merge(2);                                      // 横に 2 列
h1[4].Merge(2);
t.AddHeaderRow(null!, null!, "売上", "利益", "売上", "利益");
foreach (var row in t.Rows) foreach (var c in row.Cells) c.Alignment = TextAlignment.Center;

var r1 = t.AddRow("東日本", "札幌", "120", "18", "135", "21");
r1[0].Merge(1, 2);
t.AddRow(null!, "仙台", "98", "12", "101", "15");
var r3 = t.AddRow("西日本", "大阪", "210", "35", "198", "30");
r3[0].Merge(1, 3);
t.AddRow(null!, "広島", "75", "9", "80", "11");
t.AddRow(null!, "福岡", "130", "20", "142", "24");
var total = t.AddRow("合計", null!, "633", "94", "656", "101");
total[0].Merge(2).SetAlignment(TextAlignment.Center);
total.Background = PdfColor.FromGray(0.92);
total.Style = new TextStyle { Bold = true };

doc.Save("table-merge.pdf");
```

![見出しを 2 行にし、地域の列を縦に結合した集計表](images/table-merge.png)

### 5.4 罫線と背景色

罫線は、表全体の既定値（`Table.Borders`）とセルの辺ごとの指定（`Cell.Borders`）で決まります。セルで指定した辺が優先されます。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();

// 色付きの罫線と縞模様
var t = s.AddTable();
t.AddColumns(2, 1, 1);
t.Borders = new BorderLine(PdfColor.FromHex("#8EA9DB"), 0.5);
t.AlternateRowBackground = PdfColor.FromHex("#EEF3FB");
t.HeaderBackground = PdfColor.FromHex("#4472C4");
t.HeaderStyle = new TextStyle { Color = PdfColor.White, Bold = true };
t.CellPadding = new Thickness(6, 4);
t.AddHeaderRow("項目", "4 月", "5 月");
for (int i = 1; i <= 5; i++) t.AddRow("項目 " + i, (i * 120).ToString(), (i * 135).ToString());
var sum = t.AddRow("合計", "1,800", "2,025");
foreach (var c in sum.Cells) c.Borders.Top = new BorderLine(PdfColor.FromHex("#1F3864"), 1.5);

s.AddSpacer(16);

// 横線だけの表（縦の罫線なし）
var plain = s.AddTable();
plain.AddColumns(1, 3);
plain.Borders = null;                                // 既定の罫線をなくす
var head = plain.AddHeaderRow("日付", "内容");
foreach (var c in head.Cells) c.Borders.Bottom = BorderLine.Thick;
foreach (var (date, text) in new[] { ("10/1", "キックオフ"), ("10/8", "設計レビュー"), ("10/15", "結合テスト") })
{
    var row = plain.AddRow(date, text);
    foreach (var c in row.Cells) c.Borders.Bottom = BorderLine.Dashed(PdfColor.Gray);
}

doc.Save("table-borders.pdf");
```

![色付きの罫線と縞模様の表、縦の罫線がない横線だけの表](images/table-borders.png)

| 指定 | 内容 |
| --- | --- |
| `BorderLine.Thin` | 黒の細線（0.5 pt）。表の既定値 |
| `BorderLine.Thick` | 黒の太線（1.5 pt） |
| `BorderLine.Dashed(色, 太さ)` | 破線 |
| `BorderLine.None` | 罫線なし（その辺だけ消す） |
| `new BorderLine(色, 太さ)` | 任意の色と太さ |

背景色は、セル（`Cell.Background`）、行（`Row.Background`）、見出し行の背景色（`HeaderBackground`）または縞模様（`AlternateRowBackground`）、列（`Column.Background`）の順に優先されます。

### 5.5 複数ページにまたがる表

行が多い表は、自動的に次のページへ続きます。見出し行は各ページの先頭に繰り返します。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
s.Footer.AddParagraph().SetAlignment(TextAlignment.Center).AddPageNumber().AddText(" / ").AddPageCount();

var t = s.AddTable();
t.AddColumns(1, 4, 2);
t.HeaderBackground = PdfColor.FromHex("#DDEBF7");
t.AlternateRowBackground = PdfColor.FromGray(0.96);
t.AddHeaderRow("No.", "品名", "金額");                // 改ページ後も各ページの先頭に表示
for (int i = 1; i <= 70; i++)
    t.AddRow(i.ToString(), "商品 " + i, (i * 1280).ToString("#,0") + " 円").Cells[2].Alignment = TextAlignment.Right;

doc.Save("table-long.pdf");
```

![70 行の表が 2 ページに分かれ、2 ページ目の先頭にも見出し行がある様子](images/table-long.png)

### 5.6 ページの終わりでの行の分割

ページの最後に行が入りきらないとき、既定では行ごと次のページへ送ります。`Row.AllowBreakAcrossPages = true` にすると、行をページの終わりで分割し、残りを次のページに続けます。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
s.AddDrawing(s.PageSetup.ContentHeight - 110, (g, r) =>
    g.FillRectangle(PdfColor.FromGray(0.93), 0, 0, r.Width, r.Height));

var t = s.AddTable();
t.AddColumns(1, 5);
t.HeaderBackground = PdfColor.FromHex("#DDEBF7");
t.AddHeaderRow("No.", "内容");
t.AddRow("1", "短い行");
// 既定では、収まらない行は行ごと次のページへ送る。true にするとページ末尾で分割する
t.AddRow("2", Filler(12)).AllowBreakAcrossPages = true;

doc.Save("table-row-split.pdf");
```

![2 行目の長い文章が 1 ページ目の終わりで分割され、2 ページ目に続いている様子](images/table-row-split.png)

1 ページに収まらないほど高い行は、この設定にかかわらず分割します。

### 5.7 セルの中身

セルには、段落・画像・図・表を入れられます。垂直方向の揃えは、表・行・セルのそれぞれで指定できます。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
var logo = PdfImage.FromFile("logo.png");

var t = s.AddTable();
t.AddColumns(1, 2.2, 1.6);
t.AddHeaderRow("画像", "段落を複数", "表の入れ子");

var row = t.AddRow();
row.VerticalAlignment = VerticalAlignment.Middle;
row[0].AddImage(logo, width: Unit.Mm(22)).Alignment = HorizontalAlignment.Center;

row[1].AddParagraph("ChohyoPdf").SetBold().SetFontSize(13);
row[1].AddParagraph("セルには段落・画像・図・表を入れられます。").SetSpaceAfter(4);
row[1].AddParagraph().AddText("文字の").AddText("装飾", new TextStyle { Color = PdfColor.Red, Bold = true }).AddText("もそのまま使えます。");

var inner = row[2].AddTable();
inner.AddColumns(1, 1);
inner.Borders = new BorderLine(PdfColor.Gray, 0.3);
inner.AddRow("A", "1");
inner.AddRow("B", "2");

var r2 = t.AddRow("上", "中央", "下");
r2.Height = Unit.Mm(15);                             // 最小の高さ
r2[0].VerticalAlignment = VerticalAlignment.Top;
r2[1].VerticalAlignment = VerticalAlignment.Middle;
r2[2].VerticalAlignment = VerticalAlignment.Bottom;
r2[1].Background = PdfColor.FromHex("#FCE4D6");
r2[2].Padding = new Thickness(12, 2);                // セルごとの内側の余白

doc.Save("table-rich-cells.pdf");
```

![画像、複数の段落、入れ子の表を入れたセルと、上・中央・下に揃えたセル](images/table-rich-cells.png)

---

## 6. 画像

### 6.1 画像の配置

対応している形式は JPEG と PNG です。

- JPEG は、データを変換せずにそのまま埋め込みます。CMYK の JPEG も使えます。
- PNG は、全カラータイプ、16 bit、インターレース、透過に対応しています。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
var photo = PdfImage.FromFile("photo.jpg");          // JPEG
var logo = PdfImage.FromFile("logo.png");            // 透過 PNG

s.AddParagraph($"photo.jpg：{photo.PixelWidth} × {photo.PixelHeight} px").SetFontSize(9);
s.AddImage(photo, width: Unit.Mm(80));               // 幅を指定すると高さは縦横比から決まる
s.AddParagraph("中央・右寄せ").SetFontSize(9).SetSpaceBefore(8);
s.AddImage(logo, width: Unit.Mm(25)).Alignment = HorizontalAlignment.Center;
s.AddImage(logo, height: Unit.Mm(12)).Alignment = HorizontalAlignment.Right;

doc.Save("image-basic.pdf");
```

![写真を幅 80 mm で配置し、ロゴを中央と右に配置した例](images/image-basic.png)

幅と高さを両方とも省略すると、画像に記録された解像度（記録がなければ 96 dpi）から大きさを決めます。本文の幅や残りの高さより大きい画像は、縦横比を保ったまま縮小します（`ImageBlock.ScaleToFit`、既定で有効）。

同じ `PdfImage` を何度配置しても、PDF に埋め込むデータは 1 つです。

### 6.2 透過 PNG

PNG の透明な部分は、そのまま透明になります。

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(150), Unit.Mm(60))).Graphics;
var logo = PdfImage.FromFile("logo.png");
var photo = PdfImage.FromFile("photo.jpg");

// 市松模様の上に置くと、透明な部分と影の半透明が分かる
for (int y = 0; y < 12; y++)
    for (int x = 0; x < 30; x++)
        if ((x + y) % 2 == 0) g.FillRectangle(PdfColor.FromGray(0.85), x * Unit.Mm(5), y * Unit.Mm(5), Unit.Mm(5), Unit.Mm(5));

g.DrawImage(logo, Unit.Mm(10), Unit.Mm(10), Unit.Mm(40), Unit.Mm(40));
g.DrawImage(photo, Unit.Mm(60), Unit.Mm(10), Unit.Mm(64), Unit.Mm(40));
g.DrawImage(logo, Unit.Mm(105), Unit.Mm(25), Unit.Mm(30), Unit.Mm(30));   // 写真に重ねる

pdf.Save("image-transparency.pdf");
```

![市松模様の上に透過 PNG のロゴと写真を重ねた例。ロゴの影が半透明になっている](images/image-transparency.png)

### 6.3 枠に合わせる

枠の大きさに合わせて画像を描くには、`DrawImage` に幅と高さを渡す方法と、`DrawImageFit` を使う方法があります。

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(62))).Graphics;
var photo = PdfImage.FromFile("photo.jpg");
var pen = PdfPen.Dashed(PdfColor.Red, 0.5, 2, 2);
var label = new TextStyle(font, 8);
double w = Unit.Mm(45), h = Unit.Mm(40), top = Unit.Mm(14);

g.DrawText("DrawImage（枠いっぱいに伸ばす）", Unit.Mm(8), Unit.Mm(6), label);
g.DrawImage(photo, Unit.Mm(8), top, w, h);
g.DrawRectangle(pen, Unit.Mm(8), top, w, h);

g.DrawText("DrawImageFit（縦横比を保って中央に）", Unit.Mm(62), Unit.Mm(6), label);
g.DrawImageFit(photo, Unit.Mm(62), top, w, h);
g.DrawRectangle(pen, Unit.Mm(62), top, w, h);

g.DrawText("DrawImageFit（縦長の枠）", Unit.Mm(116), Unit.Mm(6), label);
g.DrawImageFit(photo, Unit.Mm(126), top, Unit.Mm(25), h);
g.DrawRectangle(pen, Unit.Mm(126), top, Unit.Mm(25), h);

pdf.Save("image-fit.pdf");
```

![枠いっぱいに伸ばした画像と、縦横比を保って枠の中央に置いた画像](images/image-fit.png)

`DrawImage(image, x, y)` のように大きさを省略すると、画像の解像度どおりの大きさで描きます。

---

## 7. 図形とグラフ

### 7.1 基本の図形

`PdfGraphics` の図形メソッドは、線の指定（`PdfPen`）と塗りつぶしの色（`PdfColor`）を受け取ります。どちらかに `null` を渡すと、線だけ、または塗りだけになります。

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(85))).Graphics;
var pen = new PdfPen(PdfColor.FromHex("#1F4E79"), 1.5);
var fill = PdfColor.FromHex("#BDD7EE");
var label = new TextStyle(font, 8);
double c = Unit.Mm(40);                              // 1 マスの大きさ

void Label(int col, int row, string text) =>
    g.DrawText(text, Unit.Mm(10) + col * c + Unit.Mm(15), Unit.Mm(5) + row * c + Unit.Mm(33), label, TextAlignment.Center);

double X(int col) => Unit.Mm(10) + col * c;
double Y(int row) => Unit.Mm(5) + row * c;

g.DrawLine(pen, X(0), Y(0) + Unit.Mm(25), X(0) + Unit.Mm(30), Y(0) + Unit.Mm(5));
g.DrawLine(PdfPen.Dashed(PdfColor.Red, 1, 4, 2), X(0), Y(0) + Unit.Mm(28), X(0) + Unit.Mm(30), Y(0) + Unit.Mm(28));
Label(0, 0, "DrawLine / 破線");
g.DrawRectangle(pen, fill, X(1), Y(0) + Unit.Mm(5), Unit.Mm(30), Unit.Mm(22));
Label(1, 0, "DrawRectangle");
g.DrawRoundedRectangle(pen, fill, X(2), Y(0) + Unit.Mm(5), Unit.Mm(30), Unit.Mm(22), Unit.Mm(5));
Label(2, 0, "DrawRoundedRectangle");
g.DrawEllipse(pen, fill, X(3), Y(0) + Unit.Mm(5), Unit.Mm(30), Unit.Mm(22));
Label(3, 0, "DrawEllipse");

g.DrawPie(pen, fill, X(0) + Unit.Mm(15), Y(1) + Unit.Mm(15), Unit.Mm(13), -90, 270);   // 真上から時計回りに 270 度
Label(0, 1, "DrawPie");
g.DrawPolygon(pen, fill, X(1) + Unit.Mm(15), Y(1) + Unit.Mm(2), X(1) + Unit.Mm(30), Y(1) + Unit.Mm(28), X(1), Y(1) + Unit.Mm(28));
Label(1, 1, "DrawPolygon");
var path = new PdfPath()
    .MoveTo(X(2), Y(1) + Unit.Mm(25))
    .CurveTo(X(2) + Unit.Mm(5), Y(1) - Unit.Mm(5), X(2) + Unit.Mm(25), Y(1) + Unit.Mm(40), X(2) + Unit.Mm(30), Y(1) + Unit.Mm(5));
g.DrawPath(path, pen, null);
Label(2, 1, "PdfPath（ベジェ曲線）");
g.DrawPolyline(PdfPen.Dotted(PdfColor.FromHex("#C55A11"), 2), X(3), Y(1) + Unit.Mm(25), X(3) + Unit.Mm(10), Y(1) + Unit.Mm(5),
    X(3) + Unit.Mm(20), Y(1) + Unit.Mm(20), X(3) + Unit.Mm(30), Y(1) + Unit.Mm(3));
Label(3, 1, "DrawPolyline / 点線");

pdf.Save("shapes.pdf");
```

![直線、破線、矩形、角丸矩形、楕円、扇形、多角形、ベジェ曲線、点線の折れ線](images/shapes.png)

角度は度で指定します。0 度が右（3 時の方向）で、時計回りが正です。

### 7.2 色と透明度

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(62))).Graphics;
var label = new TextStyle(font, 8);

// 半透明（WithAlpha）の円を重ねる
g.DrawCircle(null, PdfColor.FromRgb(255, 0, 0).WithAlpha(0.5), Unit.Mm(25), Unit.Mm(22), Unit.Mm(14));
g.DrawCircle(null, PdfColor.FromRgb(0, 176, 80).WithAlpha(0.5), Unit.Mm(40), Unit.Mm(22), Unit.Mm(14));
g.DrawCircle(null, PdfColor.FromRgb(0, 112, 192).WithAlpha(0.5), Unit.Mm(32.5), Unit.Mm(35), Unit.Mm(14));
g.DrawText("WithAlpha(0.5)", Unit.Mm(32.5), Unit.Mm(53), label, TextAlignment.Center);

// グレー・RGB・CMYK
for (int i = 0; i <= 10; i++)
    g.FillRectangle(PdfColor.FromGray(i / 10.0), Unit.Mm(70 + i * 8), Unit.Mm(8), Unit.Mm(8), Unit.Mm(10));
g.DrawText("FromGray(0.0) … FromGray(1.0)", Unit.Mm(70), Unit.Mm(19.5), label);

string[] hex = { "#C00000", "#ED7D31", "#FFC000", "#70AD47", "#4472C4", "#7030A0" };
for (int i = 0; i < hex.Length; i++)
    g.FillRectangle(PdfColor.FromHex(hex[i]), Unit.Mm(70 + i * 14), Unit.Mm(27), Unit.Mm(12), Unit.Mm(8));
g.DrawText("FromHex(\"#C00000\") など", Unit.Mm(70), Unit.Mm(36.5), label);

g.FillRectangle(PdfColor.FromCmyk(1, 0, 0, 0), Unit.Mm(70), Unit.Mm(44), Unit.Mm(12), Unit.Mm(8));
g.FillRectangle(PdfColor.FromCmyk(0, 1, 0, 0), Unit.Mm(84), Unit.Mm(44), Unit.Mm(12), Unit.Mm(8));
g.FillRectangle(PdfColor.FromCmyk(0, 0, 1, 0), Unit.Mm(98), Unit.Mm(44), Unit.Mm(12), Unit.Mm(8));
g.FillRectangle(PdfColor.FromCmyk(0, 0, 0, 1), Unit.Mm(112), Unit.Mm(44), Unit.Mm(12), Unit.Mm(8));
g.DrawText("FromCmyk（印刷向け）", Unit.Mm(70), Unit.Mm(53.5), label);

pdf.Save("colors.pdf");
```

![半透明の円の重なり、グレーの階調、RGB の色見本、CMYK の色見本](images/colors.png)

| 作り方 | 例 |
| --- | --- |
| RGB（0〜255） | `PdfColor.FromRgb(46, 117, 182)` |
| RGB（0〜1） | `PdfColor.FromRgb(0.2, 0.5, 0.7)` |
| 16 進数 | `PdfColor.FromHex("#2E75B6")`、`"#2E75B680"`（末尾 2 桁は透明度） |
| グレー（0 = 黒、1 = 白） | `PdfColor.FromGray(0.9)` |
| CMYK（0〜1） | `PdfColor.FromCmyk(0, 0.5, 1, 0)` |
| 透明度 | `PdfColor.Red.WithAlpha(0.3)` |
| 名前付きの色 | `Black`、`White`、`Gray`、`LightGray`、`DarkGray`、`Red`、`Green`、`Blue`、`Navy`、`Orange`、`Yellow`、`Transparent` |

### 7.3 回転・拡大縮小・クリップ

座標変換は、それ以降に描くすべての図形と文字に適用されます。変換の前に `SaveState()` を、描き終えたら `RestoreState()` を呼んで、元の状態に戻します。

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(70))).Graphics;
var style = new TextStyle(font, 10);

// 回転：点 (cx, cy) を中心に、時計回りを正とする角度で回す
double cx = Unit.Mm(35), cy = Unit.Mm(35);
for (int angle = 0; angle < 360; angle += 45)
{
    g.SaveState();
    g.RotateAt(angle, cx, cy);
    g.DrawText($"{angle}°", cx + Unit.Mm(6), cy - 5, style);
    g.RestoreState();
}
g.DrawCircle(null, PdfColor.Red, cx, cy, 2);

// 拡大縮小：Translate で原点を移してから Scale
g.SaveState();
g.Translate(Unit.Mm(75), Unit.Mm(12));
g.Scale(2, 1);                                       // 横に 2 倍
g.DrawText("横 2 倍", 0, 0, style);
g.RestoreState();

// クリップ：円の中だけに画像を描く
var photo = PdfImage.FromFile("photo.jpg");
g.SaveState();
g.SetClip(new PdfPath().AddEllipse(Unit.Mm(120), Unit.Mm(10), Unit.Mm(45), Unit.Mm(45)));
g.DrawImage(photo, Unit.Mm(106.5), Unit.Mm(10), Unit.Mm(72), Unit.Mm(45));
g.RestoreState();
g.DrawText("SetClip", Unit.Mm(142.5), Unit.Mm(58), style, TextAlignment.Center);

pdf.Save("transforms.pdf");
```

![45 度ずつ回転した文字、横に 2 倍にした文字、円の形に切り抜いた写真](images/transforms.png)

### 7.4 グラフ

自動レイアウトの中に図を入れるときは、`AddDrawing(高さ, 描画処理)` を使います。描画処理の中の座標は、図の左上が (0, 0) です。2 つ目の引数 `rect` から、図の幅と高さを取得できます。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
s.AddParagraph("月別売上と構成比").SetFontSize(14).SetBold().SetSpaceAfter(6);

string[] months = { "4月", "5月", "6月", "7月", "8月", "9月" };
double[] sales = { 120, 145, 98, 170, 160, 190 };
var labelStyle = new TextStyle(font, 8);

// 棒グラフ（AddDrawing の (0, 0) は図の左上）
s.AddDrawing(Unit.Mm(55), (g, r) =>
{
    double baseY = r.Height - 14, maxH = r.Height - 30, step = r.Width / months.Length;
    g.DrawLine(new PdfPen(PdfColor.Gray, 0.5), 0, baseY, r.Width, baseY);
    for (int i = 0; i < months.Length; i++)
    {
        double h = sales[i] / 200 * maxH, x = i * step + step * 0.25;
        g.FillRectangle(PdfColor.FromHex("#5B9BD5"), x, baseY - h, step * 0.5, h);
        g.DrawText(sales[i].ToString(), x + step * 0.25, baseY - h - 11, labelStyle, TextAlignment.Center);
        g.DrawText(months[i], x + step * 0.25, baseY + 3, labelStyle, TextAlignment.Center);
    }
});

// 円グラフ（DrawPie の角度は 0 度が右、時計回りが正）
string[] names = { "製品A", "製品B", "製品C", "その他" };
double[] share = { 45, 25, 20, 10 };
string[] colors = { "#5B9BD5", "#ED7D31", "#A5A5A5", "#FFC000" };
s.AddDrawing(Unit.Mm(55), (g, r) =>
{
    double cx = Unit.Mm(30), cy = r.Height / 2, radius = Unit.Mm(24), start = -90;
    for (int i = 0; i < share.Length; i++)
    {
        double sweep = share[i] / 100 * 360;
        g.DrawPie(new PdfPen(PdfColor.White, 1), PdfColor.FromHex(colors[i]), cx, cy, radius, start, sweep);
        double mid = (start + sweep / 2) * Math.PI / 180;
        g.DrawText($"{share[i]}%", cx + Math.Cos(mid) * radius * 0.65, cy + Math.Sin(mid) * radius * 0.65 - 4,
            labelStyle, TextAlignment.Center);
        g.FillRectangle(PdfColor.FromHex(colors[i]), Unit.Mm(65), Unit.Mm(12 + i * 8), Unit.Mm(4), Unit.Mm(4));
        g.DrawText(names[i], Unit.Mm(71), Unit.Mm(11.5 + i * 8), labelStyle);
        start += sweep;
    }
});

doc.Save("charts.pdf");
```

![6 か月分の棒グラフと、4 項目の円グラフ](images/charts.png)

---

## 8. 座標を指定して描く帳票

### 8.1 1 行の文字

`DrawText(文字列, x, y, スタイル, 揃え)` は、折り返さずに 1 行で描きます（改行文字では改行します）。y は文字の上端です。揃えによって x の意味が変わります。

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(160), Unit.Mm(45))).Graphics;
var style = new TextStyle(font, 12);
var mark = new PdfPen(PdfColor.Red, 0.5);
double y = Unit.Mm(10);

void Cross(double x) { g.DrawLine(mark, x - 4, y, x + 4, y); g.DrawLine(mark, x, y - 4, x, y + 4); }

// y は文字の上端。揃えによって x の意味が変わる（赤い十字が (x, y)）
Cross(Unit.Mm(10));
g.DrawText("Left：x が左端", Unit.Mm(10), y, style);
Cross(Unit.Mm(80));
g.DrawText("Center：x が中央", Unit.Mm(80), y, style, TextAlignment.Center);
Cross(Unit.Mm(150));
g.DrawText("Right：x が右端", Unit.Mm(150), y, style, TextAlignment.Right);

// 戻り値は描いた幅。続けて書くときに使う
double width = g.DrawText("金額：", Unit.Mm(10), Unit.Mm(25), style);
g.DrawText("12,345 円", Unit.Mm(10) + width, Unit.Mm(25), new TextStyle(font, 12) { Bold = true, Underline = true });

pdf.Save("draw-text.pdf");
```

![赤い十字で示した基準点に対して、左揃え・中央揃え・右揃えで描いた文字](images/draw-text.png)

### 8.2 テキストボックス

`DrawTextBox` は、矩形の中で折り返して描きます。禁則処理も行います。

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(48))).Graphics;
var style = new TextStyle(font, 10);
var frame = new PdfPen(PdfColor.Gray, 0.5);
string text = "DrawTextBox は矩形の中で折り返して描きます。";

var aligns = new[] { VerticalAlignment.Top, VerticalAlignment.Middle, VerticalAlignment.Bottom };
for (int i = 0; i < aligns.Length; i++)
{
    var rect = new PdfRect(Unit.Mm(8 + i * 54), Unit.Mm(10), Unit.Mm(48), Unit.Mm(30));
    g.DrawRectangle(frame, rect.X, rect.Y, rect.Width, rect.Height);
    g.DrawText(aligns[i].ToString(), rect.X, rect.Y - 11, new TextStyle(font, 8) { Color = PdfColor.Gray });
    g.DrawTextBox(text, rect.Deflate(3, 3, 3, 3), style, new TextBoxOptions
    {
        VerticalAlignment = aligns[i],
        Alignment = TextAlignment.Center,
    });
}

pdf.Save("text-boxes.pdf");
```

![上・中央・下に揃えたテキストボックス](images/text-boxes.png)

`TextBoxOptions` で指定できる項目は次のとおりです。

| プロパティ | 既定値 | 内容 |
| --- | --- | --- |
| `Alignment` | `Left` | 水平方向の揃え（均等割り付けも可） |
| `VerticalAlignment` | `Top` | 垂直方向の揃え |
| `LineSpacing` | 1.2 | 行送り（文字の大きさに対する倍率） |
| `LineSpacingRule` | `Multiple` | 行送りの指定方法 |
| `ShrinkToFit` | false | 入りきらないとき、文字を縮小して全体を収める |
| `MinFontSize` | 4 | 縮小するときの最小の文字の大きさ |
| `Wrap` | true | 折り返すか |
| `DrawOverflow` | false | 入りきらない行も、矩形の外にはみ出して描く |

### 8.3 縮小して収める

氏名や住所の欄のように枠の大きさが決まっているときは、`ShrinkToFit` で文字を縮小して収めます。戻り値の `TextBoxResult` で、実際の文字の大きさや、入りきらなかったかどうかを確認できます。

```csharp
var pdf = new PdfDocument();
var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(30))).Graphics;
var style = new TextStyle(font, 11);
var frame = new PdfPen(PdfColor.Gray, 0.5);
var note = new TextStyle(font, 7.5) { Color = PdfColor.Red };
string text = "株式会社サンプルホールディングス 営業企画部";

var options = new[]
{
    new TextBoxOptions(),                                        // そのまま（入りきらない行は描かない）
    new TextBoxOptions { ShrinkToFit = true },                   // 縮小して全体を収める
    new TextBoxOptions { ShrinkToFit = true, Wrap = false },     // 折り返さず 1 行に縮小
};
for (int i = 0; i < options.Length; i++)
{
    var rect = new PdfRect(Unit.Mm(8 + i * 54), Unit.Mm(8), Unit.Mm(48), Unit.Mm(10));
    g.DrawRectangle(frame, rect.X, rect.Y, rect.Width, rect.Height);
    options[i].VerticalAlignment = VerticalAlignment.Middle;
    TextBoxResult result = g.DrawTextBox(text, rect.Deflate(2, 1, 2, 1), style, options[i]);
    g.DrawText($"{result.FontSize:0.#}pt / {result.LinesDrawn}/{result.TotalLines} 行 / Overflowed={result.Overflowed}",
        rect.X, rect.Bottom + 3, note);
}

pdf.Save("shrink-to-fit.pdf");
```

![縮小しない場合、縮小して 2 行に収めた場合、1 行に縮小した場合の比較](images/shrink-to-fit.png)

左の枠では 2 行目が入りきらないため、1 行目だけを描いて `Overflowed` が `True` になっています。

### 8.4 申込書の例

罫線・均等割り付けの項目名・縮小する記入欄・チェックボックスを組み合わせた例です。

```csharp
var pdf = new PdfDocument();
var page = pdf.AddPage(PageSize.A5.Landscape);
var g = page.Graphics;
var label = new TextStyle(font, 10);
var value = new TextStyle(font, 12);
var thin = new PdfPen(PdfColor.Black, 0.5);
var gray = PdfColor.FromGray(0.92);

g.DrawText("申 込 書", page.Width / 2, Unit.Mm(12), new TextStyle(font, 20) { Bold = true }, TextAlignment.Center);
g.DrawText("2026 年 10 月 19 日", page.Width - Unit.Mm(15), Unit.Mm(24), label, TextAlignment.Right);

string[] labels = { "氏名", "フリガナ", "住所", "電話番号" };
string[] values = { "山田 太郎", "ヤマダ タロウ", "東京都千代田区千代田 1-1-1 サンプルマンション 1001 号室", "03-1234-5678" };
double x = Unit.Mm(15), y = Unit.Mm(32), labelW = Unit.Mm(30), w = page.Width - Unit.Mm(30), h = Unit.Mm(12);
for (int i = 0; i < labels.Length; i++)
{
    double top = y + i * h;
    g.DrawRectangle(thin, gray, x, top, labelW, h);
    g.DrawRectangle(thin, x + labelW, top, w - labelW, h);
    // 項目名は均等割り付け、記入欄は収まらなければ縮小
    g.DrawTextBox(labels[i], new PdfRect(x + Unit.Mm(4), top, labelW - Unit.Mm(8), h), label,
        new TextBoxOptions { Alignment = TextAlignment.Distribute, VerticalAlignment = VerticalAlignment.Middle });
    g.DrawTextBox(values[i], new PdfRect(x + labelW + Unit.Mm(3), top, w - labelW - Unit.Mm(6), h), value,
        new TextBoxOptions { VerticalAlignment = VerticalAlignment.Middle, ShrinkToFit = true, Wrap = false });
}

// チェックボックス
double cy = y + labels.Length * h + Unit.Mm(8);
string[] plans = { "ライト", "スタンダード", "プレミアム" };
for (int i = 0; i < plans.Length; i++)
{
    double bx = x + i * Unit.Mm(40);
    g.DrawRectangle(thin, bx, cy, Unit.Mm(4), Unit.Mm(4));
    if (i == 1) g.DrawPolyline(new PdfPen(PdfColor.Red, 1.5), bx + 2, cy + 6, bx + 5, cy + 10, bx + 11, cy + 1);
    g.DrawText(plans[i], bx + Unit.Mm(6), cy - 1, label);
}

pdf.Save("form.pdf");
```

![氏名・フリガナ・住所・電話番号の欄とチェックボックスがある申込書](images/form.png)

### 8.5 自動レイアウトの結果に描き足す

`Document.Render()` は、自動レイアウトの結果を `PdfDocument` として返します。そのページの `Graphics` を使うと、座標を指定して描き足せます。

```csharp
var doc = new Document(font, 10.5);
var s = doc.AddSection();
s.AddParagraph("稟議書").SetFontSize(20).SetBold().SetAlignment(TextAlignment.Center).SetSpaceAfter(10);
s.AddParagraph("件名：新しい会計システムの導入について");
s.AddParagraph(Filler(4));

// 自動レイアウトの結果を PdfDocument として受け取り、座標指定で描き足す
PdfDocument pdf = doc.Render();
var g = pdf.Pages[0].Graphics;
double cx = pdf.Pages[0].Width - Unit.Mm(35), cy = Unit.Mm(17);
var red = PdfColor.FromHex("#D00000");
g.DrawCircle(new PdfPen(red, 1.5), null, cx, cy, Unit.Mm(10));
g.DrawLine(new PdfPen(red, 0.8), cx - Unit.Mm(9.5), cy - 2, cx + Unit.Mm(9.5), cy - 2);
g.DrawText("承認", cx, cy - Unit.Mm(7), new TextStyle(font, 9) { Color = red }, TextAlignment.Center);
g.DrawText("10.19", cx, cy + 1, new TextStyle(font, 8) { Color = red }, TextAlignment.Center);
pdf.Save("render-then-draw.pdf");
```

![自動レイアウトした稟議書の右上に、座標指定で承認印を描いた例](images/render-then-draw.png)

---

## 9. まとまったサンプル

[samples/ChohyoPdf.Samples/Program.cs](../samples/ChohyoPdf.Samples/Program.cs) には、実務に近い 3 つのサンプルがあります。次のコマンドで PDF を出力できます。

```
dotnet run --project samples/ChohyoPdf.Samples -f net8.0 -- 出力先フォルダ
```

### 9.1 請求書

宛先と発行元を罫線のない表で並べ、36 行の明細を 2 ページに分けています。2 ページ目の先頭にも明細の見出しを表示し、小計・消費税・合計の行は同じページにまとめています。

![2 ページの請求書](images/sample-invoice.png)

### 9.2 レポート

名前付きスタイル、禁則処理と両端揃え、セルを結合した表、PNG 画像、棒グラフと円グラフ、長い表の自動改ページを使っています。

![4 ページのレポート](images/sample-report.png)

### 9.3 受領書

座標指定の API だけで作った A5 横の帳票です。

![受領書](images/sample-lowlevel.png)

---

## 10. 困ったときは

### 10.1 フォントが見つからない

`PdfFont.FromSystem` は、次のフォルダを探します。

- Windows：`C:\Windows\Fonts`、ユーザーごとのフォントフォルダ
- macOS：`/System/Library/Fonts`、`/Library/Fonts`、`~/Library/Fonts`
- Linux：`/usr/share/fonts`、`/usr/local/share/fonts`、`~/.fonts`、`~/.local/share/fonts`

見つからないときは、ファイルを直接指定します。TTC ファイル（複数のフォントを 1 つにまとめたファイル）では、番号か名前でフォントを選びます。

```csharp
// TTC ファイルの中のフォント名を調べ、名前で選んで読み込む
foreach (string name in PdfFont.GetFaceNames(@"C:\Windows\Fonts\msgothic.ttc"))
    Console.WriteLine(name);                     // MS Gothic、MS UI Gothic、MS PGothic
var pgothic = PdfFont.FromFile(@"C:\Windows\Fonts\msgothic.ttc", "MS PGothic");

// 候補を順に探す（サーバーなど、環境によってフォントが違うとき）
if (!PdfFont.TryFromSystem(out var gothic, "Yu Gothic Regular", "Meiryo", "Noto Sans JP", "IPAexGothic"))
    throw new InvalidOperationException("日本語フォントがありません");

// 文字があるかを調べる。サロゲートペアの文字はコードポイントにしてから渡す
Console.WriteLine(gothic!.HasGlyph('あ'));                    // True
Console.WriteLine(gothic.HasGlyph(char.ConvertToUtf32("𠮷", 0))); // True
Console.WriteLine(gothic.HasGlyph(0x265A));                   // False（♚ は游ゴシックにない）
```

アプリケーションにフォントファイルを同梱する場合は、`PdfFont.FromStream` や `PdfFont.FromBytes` でも読み込めます。

### 10.2 文字が細い・太さが違う

可変フォント（例: Windows 11 の `NotoSansJP-VF.ttf`）は、フォントの既定の太さで描かれます。NotoSansJP-VF の既定は最も細い Thin です。Regular や Bold など、太さごとに分かれたフォントファイルを使ってください。`PdfFont.IsVariableFont` で可変フォントかどうかを確認できます。

### 10.3 フォントを読み込めない

| 例外 | 原因と対処 |
| --- | --- |
| `NotSupportedException`（CFF） | .otf 形式のうち、CFF アウトラインのフォント（Source Han Sans、Noto Sans CJK の OTF 版など）には対応していません。同じフォントの TTF 版を使ってください |
| `NotSupportedException`（埋め込み禁止） | フォントのライセンス情報で埋め込みが禁止されています。別のフォントを使ってください |
| `FileNotFoundException` | `FromSystem` に渡した名前のフォントが見つかりません |

PDF にフォントを埋め込んで配布してよいかは、フォントごとのライセンスで決まります。利用するフォントのライセンスを確認してください。

### 10.4 文字が □ になる・表示されない

フォントにその文字がありません。`HasGlyph` で確認できます（[10.1](#101-フォントが見つからない) の例を参照）。代替フォントを指定すると、その文字だけ別のフォントで描けます（[3.5](#35-特殊な文字と代替フォント)）。

### 10.5 ヘッダーと本文が重なる

ヘッダーとフッターの高さは、本文の位置に影響しません。`PageSetup.Margins` の上下の値を大きくしてください（[4.2](#42-ヘッダーとフッター)）。

### 10.6 表がページからはみ出す

縦に結合したセルのまとまりと、高さを固定した行（`RowHeightRule.Exactly`）は、ページの途中で分割しません。1 ページより高くなると、ページからはみ出して描かれます。結合する行数を減らすか、行の高さの指定を `AtLeast` にしてください。

### 10.7 対応していない機能

縦書き、合字やカーニングなどの OpenType の機能、右から左に書く文字、JPEG の回転情報（EXIF）、暗号化、電子署名、PDF/A、タグ付き PDF、フォーム（入力欄）には対応していません。PDF の読み込みや編集もできません。

---

## 11. API の一覧

### 11.1 自動レイアウト

| クラス | 主なメンバー |
| --- | --- |
| `Document` | `AddSection()`、`Styles`、`DefaultTextStyle`、`DefaultParagraphFormat`、`Info`、`Save()`、`ToArray()`、`Render()` |
| `Section` | `PageSetup`、`Header`、`Footer`、`PageBackground`、`PageForeground`、`AddParagraph()`、`AddTable()`、`AddImage()`、`AddDrawing()`、`AddPageBreak()`、`AddSpacer()`、`AddHorizontalLine()` |
| `Paragraph` | `AddText()`、`AddBold()`、`AddLink()`、`AddLineBreak()`、`AddPageNumber()`、`AddPageCount()`、`AddField()`、`SetFontSize()`、`SetBold()`、`SetColor()`、`SetAlignment()`、`SetLineSpacing()`、`SetIndent()`、`SetSpaceBefore()`、`SetSpaceAfter()`、`SetKeepWithNext()`、`SetKeepTogether()`、`SetPageBreakBefore()`、`SetBackground()`、`SetBorder()` |
| `Table` | `AddColumn()`、`AddRelativeColumn()`、`AddColumns()`、`AddHeaderRow()`、`AddRow()`、`Borders`、`CellPadding`、`HeaderBackground`、`HeaderStyle`、`AlternateRowBackground`、`VerticalAlignment`、`Alignment` |
| `Row` | `Height`、`HeightRule`、`Background`、`Style`、`VerticalAlignment`、`AllowBreakAcrossPages`、`KeepWithNext` |
| `Cell` | `Merge()`、`SetText()`、`SetBackground()`、`SetAlignment()`、`Borders`、`Padding`、`Style`、および `Section` と同じ `Add〜` メソッド |

### 11.2 座標指定

| クラス | 主なメンバー |
| --- | --- |
| `PdfDocument` | `AddPage()`、`Pages`、`Info`、`Save()`、`ToArray()` |
| `PdfPage` | `Graphics`、`Width`、`Height`、`AddLink()` |
| `PdfGraphics` | `DrawText()`、`DrawTextBox()`、`MeasureText()`、`MeasureTextHeight()`、`DrawLine()`、`DrawPolyline()`、`DrawPolygon()`、`DrawRectangle()`、`FillRectangle()`、`DrawRoundedRectangle()`、`DrawEllipse()`、`DrawCircle()`、`DrawPie()`、`DrawPath()`、`DrawImage()`、`DrawImageFit()`、`SaveState()`、`RestoreState()`、`Translate()`、`Scale()`、`Rotate()`、`RotateAt()`、`SetClip()`、`AddLink()` |
| `PdfPath` | `MoveTo()`、`LineTo()`、`CurveTo()`、`QuadraticTo()`、`Arc()`、`Close()`、`AddRectangle()`、`AddRoundedRectangle()`、`AddEllipse()`、`AddPolygon()` |

### 11.3 共通

| クラス | 主なメンバー |
| --- | --- |
| `PdfFont` | `FromSystem()`、`TryFromSystem()`、`FromFile()`、`FromStream()`、`FromBytes()`、`GetFaceNames()`、`BoldVariant`、`ItalicVariant`、`HasGlyph()`、`MeasureText()`、`IsVariableFont` |
| `PdfImage` | `FromFile()`、`FromStream()`、`FromBytes()`、`FromRawPixels()`、`PixelWidth`、`PixelHeight`、`Width`、`Height`、`HasAlpha` |
| `TextStyle` | `Font`、`FontSize`、`Color`、`Bold`、`Italic`、`Underline`、`Strikeout`、`CharacterSpacing`、`BackgroundColor`、`FallbackFonts` |
| `PdfColor` | `FromRgb()`、`FromHex()`、`FromGray()`、`FromCmyk()`、`WithAlpha()` |
| `PdfPen` | `Color`、`Width`、`DashPattern`、`LineCap`、`LineJoin`、`Dashed()`、`Dotted()` |
| `Unit` | `Mm()`、`Cm()`、`Inch()`、`ToMm()` |

各メンバーの詳しい説明は、ソースコードの XML ドキュメントコメントにあります。Visual Studio などの IntelliSense でも表示されます。

---

## 12. この説明書の作り直し方

この説明書（`docs/manual.md`）と画像は、スクリプトで生成しています。文章を直すときは `docs/manual.template.md` を、サンプルを直すときは `docs/examples/ChohyoPdf.DocExamples` のコードを編集してから、次のコマンドを実行します。

```
pip install pillow
python docs/tools/build_docs.py
```

実行には、.NET SDK、Python 3 と Pillow、`pdftoppm` と `pdfinfo`（Poppler または xpdf）が必要です。

スクリプトは、サンプルを実行して PDF を出力し、`pdftoppm` で画像にしてから、テンプレートの `<!-- example: 名前 -->` をサンプルのコードに置き換えます。サンプルのコードのうち、`// #example 名前` から `// #end` までが説明書に載ります。
