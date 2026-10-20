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

<!-- example: Fonts -->

`PdfFont.FromSystem` は、OS のフォントフォルダから名前でフォントを探します。「游ゴシック」のような日本語名でも探せます。フォントの読み込み方は [10.1](#101-フォントが見つからない) にもまとめています。

### 1.5 最初の PDF

自動レイアウトで、見出しと本文だけの PDF を作ります。

<!-- example: HelloDocument -->

![見出しと本文が 1 段落ある PDF](images/hello.png)

座標指定の API では、ページを追加して `PdfGraphics` に描きます。

<!-- example: HelloGraphics -->

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

<!-- example: Coordinates -->

![10 mm 方眼の上に、(40 mm, 30 mm) から始まる矩形を描いた図](images/coordinates.png)

文字の大きさもポイントで指定します。Word の「10.5 ポイント」と同じ大きさです。

---

## 3. 文字と段落

### 3.1 文字の装飾

`Paragraph.AddText` に `TextStyle` を渡すと、その部分だけ書式を変えられます。`TextStyle` のうち指定しなかった項目は、段落や文書の設定を引き継ぎます。

<!-- example: TextDecoration -->

![大きさ・太字・斜体・下線・色・マーカー・字間・フォント・リンクの例](images/text-decoration.png)

- 太字は、`PdfFont.BoldVariant` に太字のフォントを設定していればそのフォントで描きます。設定していなければ、文字の輪郭を太らせて描きます（疑似太字）。
- 斜体の日本語フォントはほとんどないので、通常は文字を傾けて描きます（疑似斜体）。
- `AddLink` で作ったリンクは、PDF ビューアーでクリックすると URL を開きます。

### 3.2 揃え

`SetAlignment` で、段落の揃えを指定します。

<!-- example: TextAlignments -->

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

<!-- example: LineSpacingAndIndent -->

![行送りの違いと、字下げ・左右のインデント・ぶら下げインデントの例](images/line-spacing.png)

`SetIndent(left, firstLine, right)` の `firstLine` は、1 行目だけに加える量です。負の値を指定すると、1 行目が左に出るぶら下げインデントになります。

### 3.4 折り返しと禁則処理

段落は本文の幅で自動的に折り返します。折り返すときは、次の規則に従います。

- 句読点・閉じかっこ・小書きの仮名（ゃ、っ など）・長音記号は、行の先頭に置きません。
- 開きかっこは、行の末尾に置きません。
- 英単語や数字は、単語の途中で改行しません。
- 規則を守れないときは、前の文字ごと次の行へ送ります（追い出し）。

<!-- example: LineBreaking -->

![1 行 7 文字のマス目に重ねた禁則処理の例と、英単語の折り返しの例](images/line-breaking.png)

左の例では、1 行目に 7 文字入りますが、8 文字目の「。」が行の先頭に来ないよう、「る」も 2 行目に送っています。中央の例では、「（」が行の末尾に来ないよう 1 行目を 6 文字で改行しています。3 行目は、「ピュー」の小書きの仮名と長音記号が行の先頭に来ないよう、5 文字で改行しています。

この例は座標指定の `DrawTextBox` で描いていますが、自動レイアウトの段落でも同じ規則で折り返します。

### 3.5 特殊な文字と代替フォント

サロゲートペアで表す漢字（𠮷、𩸽 など）や、丸数字などの記号も、フォントにあればそのまま描けます。フォントにない文字は、`FallbackFonts` に指定したフォントで描きます。

<!-- example: SpecialCharacters -->

![サロゲートペアの漢字、記号、代替フォントで描いたチェスの駒](images/special-characters.png)

代替フォントにもない文字は、フォントの「.notdef」グリフ（多くのフォントでは空白か四角）で描きます。

### 3.6 背景と枠

段落に背景色と枠線を付けられます。注意書きの囲みなどに使えます。

<!-- example: ParagraphBoxes -->

![背景色と枠線を付けた段落、背景色だけの段落、水平線](images/paragraph-boxes.png)

### 3.7 名前付きスタイル

同じ書式を何度も使うときは、名前付きスタイルに登録します。

<!-- example: NamedStyles -->

![見出し 1、見出し 2、本文、注記のスタイルを使った文書](images/named-styles.png)

書式は、次の順に引き継がれます。後に書いたものほど優先されます。

文書の既定値（`DefaultTextStyle`、`DefaultParagraphFormat`）→ 表 → 行 → セル → 名前付きスタイル → 段落 → 文字列（`AddText` のスタイル）

`TextStyle` と `ParagraphFormat` のプロパティは、null のままにしておくと上の階層の値を引き継ぎます。

---

## 4. ページの設定とページ送り

### 4.1 用紙と余白

用紙の大きさと余白は、セクションごとに `PageSetup` で指定します。既定値は A4 縦、余白は上下左右 20 mm です。

<!-- example: PageSetupSample -->

![A5 横の用紙に、本文の領域を破線で示した図](images/page-setup.png)

用意している用紙サイズは、`A3`、`A4`、`A5`、`A6`、`B4`、`B5`（JIS）、`Letter`、`Legal`、`Postcard`（はがき）、`EnvelopeChou3`（長形 3 号）です。`.Landscape` で横向きになります。その他の大きさは `PageSize.FromMm(幅, 高さ)` で作れます。

### 4.2 ヘッダーとフッター

`Section.Header` と `Section.Footer` には、本文と同じように段落や表を追加できます。`AddPageNumber()` はページ番号、`AddPageCount()` は総ページ数に置き換わります。

<!-- example: HeaderAndFooter -->

![ヘッダーに右寄せの文字と水平線、フッターに「1 / 2 ページ」を表示した 2 ページの文書](images/header-footer.png)

- ヘッダーは用紙の上端から `PageSetup.HeaderDistance`（既定 8 mm）、フッターは下端から `FooterDistance`（既定 8 mm）の位置に置きます。
- ヘッダーとフッターの高さによって、本文の位置は変わりません。上下の余白は、ヘッダーとフッターが収まる大きさにしてください。
- 各セクションの 1 ページ目だけヘッダーを出さないときは、`Header.ShowOnFirstPage = false` にします。

### 4.3 改ページ

`AddPageBreak()` を置いた位置、または `SetPageBreakBefore()` を指定した段落の前で改ページします。

<!-- example: PageBreaks -->

![第 1 章から第 3 章が 1 ページずつに分かれた 3 ページの文書](images/page-breaks.png)

ページの先頭にある改ページは無視します。白紙のページが続くのを防ぐためです。ただし、本文の最後に改ページを置くと、最後のページが白紙になります。

### 4.4 見出しを次の段落と同じページに置く

ページの最後に見出しだけが残ると読みにくくなります。見出しに `SetKeepWithNext()` を指定すると、次の段落と同じページに置きます。`SetKeepTogether()` を指定した段落は、途中で分割しません。

<!-- example: KeepWithNextSample -->

![1 ページ目の下に余白があるが、見出しと段落がまとめて 2 ページ目に送られた様子](images/keep-with-next.png)

1 ページ目の下には見出しが入る余白がありますが、続く段落が入りきらないため、見出しも 2 ページ目に送っています。

このほか、段落の 1 行目だけがページの最後に残ったり、最後の 1 行だけが次のページに送られたりしないよう、改ページの位置を調整します（`ParagraphFormat.WidowControl`、既定で有効）。

### 4.5 セクション

用紙の向きやヘッダーを途中で変えるときは、セクションを分けます。セクションは必ず新しいページから始まります。

<!-- example: Sections -->

![表紙、本文、横向きのページの 3 ページ](images/sections.png)

`FieldType.SectionPageNumber` を使うと、セクションの中でのページ番号（表紙を数えないページ番号など）を表示できます。

### 4.6 透かしとページの装飾

`PageBackground` は本文の下に、`PageForeground` は本文の上に、全ページ共通の図形を描きます。帳票の枠、ロゴ、透かしなどに使います。

<!-- example: Watermark -->

![上端に紺色の帯、中央に半透明の「社外秘」の透かしがあるページ](images/watermark.png)

2 つ目の引数 `PageContext` からは、ページ番号（`PageNumber`）、総ページ数（`PageCount`）、用紙の大きさ（`PageSize`）、本文の領域（`ContentArea`）を取得できます。

---

## 5. 表

### 5.1 表の基本

列を定義してから行を追加します。`AddHeaderRow` で追加した見出し行は、表が次のページに続くときに繰り返し表示されます。

<!-- example: TableBasic -->

![見出し行に背景色を付けた 4 列の表](images/table-basic.png)

セルに入りきらない文字列は、セルの中で折り返します。行の高さは、いちばん高いセルに合わせて広がります。

### 5.2 列幅と揃え

列幅は、固定幅（`AddColumn`）と比率（`AddRelativeColumn`、`AddColumns`）で指定します。固定幅の列を除いた残りの幅を、比率で分け合います。

<!-- example: TableColumns -->

![固定幅・比率・右揃え・列の背景色を使った表と、中央に配置した小さな表](images/table-columns.png)

### 5.3 セルの結合

`Cell.Merge(列数, 行数)` で、右と下のセルを結合します。結合で隠れるセルには何も入れず、`AddRow` では `null` を渡します。

<!-- example: TableMerge -->

![見出しを 2 行にし、地域の列を縦に結合した集計表](images/table-merge.png)

### 5.4 罫線と背景色

罫線は、表全体の既定値（`Table.Borders`）とセルの辺ごとの指定（`Cell.Borders`）で決まります。セルで指定した辺が優先されます。

<!-- example: TableBorders -->

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

<!-- example: TableLong -->

![70 行の表が 2 ページに分かれ、2 ページ目の先頭にも見出し行がある様子](images/table-long.png)

### 5.6 ページの終わりでの行の分割

ページの最後に行が入りきらないとき、既定では行ごと次のページへ送ります。`Row.AllowBreakAcrossPages = true` にすると、行をページの終わりで分割し、残りを次のページに続けます。

<!-- example: TableRowSplit -->

![2 行目の長い文章が 1 ページ目の終わりで分割され、2 ページ目に続いている様子](images/table-row-split.png)

1 ページに収まらないほど高い行は、この設定にかかわらず分割します。

### 5.7 セルの中身

セルには、段落・画像・図・表を入れられます。垂直方向の揃えは、表・行・セルのそれぞれで指定できます。

<!-- example: TableRichCells -->

![画像、複数の段落、入れ子の表を入れたセルと、上・中央・下に揃えたセル](images/table-rich-cells.png)

---

## 6. 画像

### 6.1 画像の配置

対応している形式は JPEG と PNG です。

- JPEG は、データを変換せずにそのまま埋め込みます。CMYK の JPEG も使えます。
- PNG は、全カラータイプ、16 bit、インターレース、透過に対応しています。

<!-- example: ImageBasic -->

![写真を幅 80 mm で配置し、ロゴを中央と右に配置した例](images/image-basic.png)

幅と高さを両方とも省略すると、画像に記録された解像度（記録がなければ 96 dpi）から大きさを決めます。本文の幅や残りの高さより大きい画像は、縦横比を保ったまま縮小します（`ImageBlock.ScaleToFit`、既定で有効）。

同じ `PdfImage` を何度配置しても、PDF に埋め込むデータは 1 つです。

### 6.2 透過 PNG

PNG の透明な部分は、そのまま透明になります。

<!-- example: ImageTransparency -->

![市松模様の上に透過 PNG のロゴと写真を重ねた例。ロゴの影が半透明になっている](images/image-transparency.png)

### 6.3 枠に合わせる

枠の大きさに合わせて画像を描くには、`DrawImage` に幅と高さを渡す方法と、`DrawImageFit` を使う方法があります。

<!-- example: ImageFit -->

![枠いっぱいに伸ばした画像と、縦横比を保って枠の中央に置いた画像](images/image-fit.png)

`DrawImage(image, x, y)` のように大きさを省略すると、画像の解像度どおりの大きさで描きます。

---

## 7. 図形とグラフ

### 7.1 基本の図形

`PdfGraphics` の図形メソッドは、線の指定（`PdfPen`）と塗りつぶしの色（`PdfColor`）を受け取ります。どちらかに `null` を渡すと、線だけ、または塗りだけになります。

<!-- example: Shapes -->

![直線、破線、矩形、角丸矩形、楕円、扇形、多角形、ベジェ曲線、点線の折れ線](images/shapes.png)

角度は度で指定します。0 度が右（3 時の方向）で、時計回りが正です。

### 7.2 色と透明度

<!-- example: Colors -->

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

<!-- example: Transforms -->

![45 度ずつ回転した文字、横に 2 倍にした文字、円の形に切り抜いた写真](images/transforms.png)

### 7.4 グラフ

自動レイアウトの中に図を入れるときは、`AddDrawing(高さ, 描画処理)` を使います。描画処理の中の座標は、図の左上が (0, 0) です。2 つ目の引数 `rect` から、図の幅と高さを取得できます。

<!-- example: Charts -->

![6 か月分の棒グラフと、4 項目の円グラフ](images/charts.png)

---

## 8. 座標を指定して描く帳票

### 8.1 1 行の文字

`DrawText(文字列, x, y, スタイル, 揃え)` は、折り返さずに 1 行で描きます（改行文字では改行します）。y は文字の上端です。揃えによって x の意味が変わります。

<!-- example: DrawTextSample -->

![赤い十字で示した基準点に対して、左揃え・中央揃え・右揃えで描いた文字](images/draw-text.png)

### 8.2 テキストボックス

`DrawTextBox` は、矩形の中で折り返して描きます。禁則処理も行います。

<!-- example: TextBoxes -->

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

<!-- example: ShrinkToFit -->

![縮小しない場合、縮小して 2 行に収めた場合、1 行に縮小した場合の比較](images/shrink-to-fit.png)

左の枠では 2 行目が入りきらないため、1 行目だけを描いて `Overflowed` が `True` になっています。

### 8.4 申込書の例

罫線・均等割り付けの項目名・縮小する記入欄・チェックボックスを組み合わせた例です。

<!-- example: FormSample -->

![氏名・フリガナ・住所・電話番号の欄とチェックボックスがある申込書](images/form.png)

### 8.5 自動レイアウトの結果に描き足す

`Document.Render()` は、自動レイアウトの結果を `PdfDocument` として返します。そのページの `Graphics` を使うと、座標を指定して描き足せます。

<!-- example: RenderThenDraw -->

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

<!-- example: FontLookup -->

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
