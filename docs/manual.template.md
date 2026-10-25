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

---

## 1. はじめに

### 1.1 動作環境とライセンス

- 対応フレームワークは .NET Framework 4.6.2 以降、.NET Standard 2.0、.NET 8、.NET 10 です。.NET 9 では .NET 8 向けの DLL が、.NET Core 3.1 や .NET 5〜7 など .NET Standard 2.0 に対応した環境では .NET Standard 2.0 向けの DLL が使われます。
- ライセンスは MIT です。商用のアプリケーションにも組み込めます。
- 他のパッケージには依存しません。

### 1.2 プロジェクトへの追加

[NuGet](https://www.nuget.org/packages/ChohyoPdf) からプロジェクトに追加します。

```
dotnet add package ChohyoPdf
```

Visual Studio では、［NuGet パッケージの管理］で「ChohyoPdf」を検索してもインストールできます。

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

- 使えるのは、TrueType 形式のフォント（.ttf と、複数のフォントをまとめた .ttc）です。Windows の游ゴシック・游明朝・メイリオ・MS ゴシック・MS 明朝、Google Fonts の Noto Sans JP / Noto Serif JP（TTF 版）、IPAex フォントなどが使えます。
- .otf のうち、CFF 形式のフォント（Source Han Sans、Noto Sans CJK の OTF 版など）は読み込めません（[10.3](#103-フォントを読み込めない)）。
- 可変フォントは、フォントの既定の太さで描かれます（[10.2](#102-文字が細い太さが違う)）。`FromSystem` は、同じ名前の可変フォントと太さごとのフォントがあれば、太さごとのフォントを使います。
- 太字のフォントは `BoldVariant` に対応付けます。対応付けていなければ、文字の輪郭を太らせて描きます（疑似太字）。
- 1 つの `PdfFont` は、複数の文書で使い回せます。文書ごとに読み込み直す必要はありません。`PdfImage` も同じです。

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
- 上付き・下付きは [3.8](#38-上付き下付き) を参照してください。

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

異体字セレクタ（IVS。漢字のあとに付けて、細かい字形の違いを指定する文字）は無視し、基底の文字の字形で描きます。

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

### 3.8 上付き・下付き

`AddSuperscript` と `AddSubscript` で、上付き文字と下付き文字を追加できます。冪（べき）の指数、化学式、注釈の番号などに使います。

<!-- example: SuperscriptSubscript -->

![上付き・下付きの文字を使った数式と化学式、赤い上付きと下線付きの下付き、座標指定で描いた x の 2 乗](images/superscript-subscript.png)

- 文字の大きさを 0.65 倍にして、ベースラインを上付きは文字サイズの約 1/3 上へ、下付きは約 1/8 下へずらして描きます。
- 行の高さと、行の中のベースラインの位置は、上付き・下付きを含まない行と同じです。上付きや下付きを入れても、行の間隔は変わりません。
- 下線と取り消し線は、縮小した文字とずらしたベースラインに合わせて引きます。
- `TextStyle.BaselineShift`（`BaselineShift.Superscript` / `Subscript` / `None`）で指定します。`AddText` に渡すスタイルや段落全体のスタイルに設定できます。親が上付きのとき、`None` を指定すると通常の位置に戻ります。
- `PdfGraphics.DrawText` や `DrawTextBox` に渡す `TextStyle` にも指定できます。文字の一部だけを上付き・下付きにした段落を座標を指定して描くときは、`DrawBlocks`（[8.6](#86-段落と箇条書きを描く)）を使います。
- フォントの上付き・下付き専用のグリフ（OpenType の `sups` / `subs`）は使いません。通常のグリフを縮小して描きます。

### 3.9 タブ位置とリーダー

文字列中のタブ文字（`\t`）や `AddTab()` は、次のタブ位置まで進みます。右揃えのタブ位置と点線のリーダー（導線）を組み合わせると、目次のような「項目………… 12」の並びを作れます。

<!-- example: TabStops -->

![点線のリーダーでつないだ目次、小数点をそろえた見積の明細、既定の間隔のタブ](images/tab-stops.png)

タブ位置は `AddTabStop(位置, 揃え, リーダー)` で追加します。`Paragraph` のほか、段落の書式（`ParagraphFormat`）や文書の既定値（`DefaultParagraphFormat`）、名前付きスタイルにも指定できます。

| 揃え（`TabAlignment`） | 動作 |
| --- | --- |
| `Left` | タブ位置から右へ文字を並べます（既定） |
| `Center` | タブ位置が文字列の中心になります |
| `Right` | タブ位置が文字列の右端になります |
| `Decimal` | 小数点（`.` または `．`）がタブ位置になります。小数点がなければ `Right` と同じです |

| リーダー（`TabLeader`） | 動作 |
| --- | --- |
| `None` | 何も描きません（既定） |
| `Dots` | 点線（丸い点） |
| `Dashes` | 破線 |
| `Line` | 実線 |

動作の細かい規則は次のとおりです。

- タブ位置は、段落の本文領域の左端（左インデントの位置）からの距離をポイントで指定します。本文の右端に置きたいときは、`PageSetup.ContentWidth` から左右のインデントを引いた値を指定します。
- タブ文字は、現在位置より右にある最初のタブ位置まで進みます。すでにタブ位置を通り過ぎているときは、その次のタブ位置へ進みます。
- 最後のタブ位置より右では、既定のタブ間隔（`DefaultTabStop`、指定しなければ 36 ポイント）の整数倍の位置まで進みます。`DefaultTabStop` だけを指定して、タブ位置を決めない使い方もできます。
- 1 行目をぶら下げインデントにして左に出しているときは、左インデントの位置にもタブ位置があるものとして扱います。「1.」のあとのタブで本文の左端にそろえるときに使えます。
- 右揃え・中央揃え・小数点揃えのタブ位置は、タブの後ろから次のタブ（または改行）までの文字列の幅で、タブの幅を決めます。文字列がタブ位置の手前に収まらないときは、タブの幅は 0 になり、文字はそのまま続けて並びます。
- タブ位置が行の右端を超えるとき、右揃えのタブ位置は行の右端に寄せます。左揃えなどのタブ位置は、タブの後ろの文字を次の行へ送ります。
- タブ位置に従うタブを含む行は、両端揃え・均等割り付けにしません（左揃えで描きます）。
- `TabStops` または `DefaultTabStop` を指定していない段落では、タブ文字は空白 4 文字分の幅になります。
- リーダーは、タブの文字に指定した文字色で描きます。点線と破線の位置は、タブ位置を基準に決めます。段落や行が違っても、同じタブ位置なら点の位置が縦にそろいます。
- `TabStops` を指定すると、親（文書の既定値や名前付きスタイル）のタブ位置を置き換えます。追加はされません。
- 表のセルの中では、セルの余白の内側の左端が基準になります。

座標指定の `DrawTextBox` では、`TextBoxOptions.TabStops` と `DefaultTabStop` で同じように指定できます（位置は矩形の左端から）。`DrawText` はタブ位置に対応していないので、折り返さないときは `Wrap = false` の `DrawTextBox` を使ってください。

### 3.10 箇条書きと番号付きリスト

`AddBulletList()` で箇条書き、`AddNumberedList()` で番号付きリストを追加し、`AddItem("文字列")` で項目を足します。`Section`・表のセル・ヘッダー/フッターなど、ブロックを入れられる場所ならどこにでも置けます。座標を指定して描くときは `DrawBlocks`（[8.6](#86-段落と箇条書きを描く)）を使います。項目は 1 行目に記号が付き、2 行目以降の行頭は本文の開始位置にそろいます（ぶら下げインデント）。

<!-- example: BulletLists -->

![入れ子の箇条書き、赤い丸の記号、複数の段落を持つ項目](images/bullet-lists.png)

<!-- example: NumberedLists -->

![入れ子の番号付きリスト、漢数字の条項、右揃えの番号](images/numbered-lists.png)

| 設定（`ListBlock`） | 動作 |
| --- | --- |
| `Bullet` | 箇条書きの記号（任意の文字列）。`null` なら入れ子のレベルごとの既定（・ → ○ → ■） |
| `NumberFormat` / `Prefix` / `Suffix` | 番号の書式（`PageNumberFormat` と同じ）と、番号の前後に付ける文字列。`null` なら、レベルごとの既定（1. → (1) → ①） |
| `StartNumber` / `ListBlockItem.Number` | 最初の項目の番号と、項目ごとの番号の指定（指定した項目の次からは、その番号に続く） |
| `MarkerWidth` / `MarkerGap` | 記号の列の幅と、記号と本文の間隔（ポイント）。`MarkerWidth` が `null` なら、リストの中で一番幅の広い記号に合わせる |
| `MarkerAlignment` | 記号の列の中での位置（既定は左揃え）。`Right` にすると、9. → 10. でも右端がそろう |
| `MarkerStyle` | 記号の文字スタイル（色・フォント・太字・文字サイズ） |
| `Indent` / `ItemSpacing` | リスト全体の左の字下げと、項目の間隔（ポイント） |
| `Style` / `Format` | リスト全体の文字スタイルと段落書式（`ListBlockItem.Style` / `Format` で項目ごとに上書き） |
| `SpaceBefore` / `SpaceAfter` / `KeepWithNext` / `PageBreakBefore` | リスト全体の前後の間隔や改ページ（表や画像と同じ） |

- 項目の中に子リストを入れると入れ子になります。最後の項目に入れるなら `list.AddSubList()`、そうでなければ `ListBlockItem.AddList()` / `AddBulletList()` / `AddNumberedList()` を使います。記号を指定しなければレベルごとの既定が使われ、4 レベル目以降は 1 レベル目に戻ります。
- 項目に段落を 1 つだけ入れるなら `AddItem("文字列")`（段落を返します）、複数のブロック（段落・表・画像・子リストなど）を入れるなら `AddItem()`（`ListBlockItem` を返します。`Section` と同じ `Add〜` メソッドが使えます）。記号は項目の最初のブロックが段落ならその 1 行目に、そうでなければ項目の上端に付きます。空の項目にも記号が付きます。
- 番号の書式は `PageNumberFormat`（アラビア数字・ローマ数字・英字・全角数字・丸数字・漢数字）をそのまま使います。書式を指定したとき、番号の後ろは `.` が既定です（丸数字は何も付けません）。「一、」「（一）」「(a)」のような形は `Prefix` / `Suffix` で作ります。
- 項目は段落と同じように折り返され、ページをまたいで分割されます。記号は項目の 1 行目にだけ付き、番号はページをまたいでも連続します。
- 記号の文字スタイルは、項目の最初の段落の文字スタイル（文字サイズ・色・太字など）に `MarkerStyle` を重ねたものです。下線・取り消し線・マーカー・上付き下付きは、`MarkerStyle` で指定しない限り記号には付きません。記号と本文のベースラインはそろいます。
- スタイルは、文書の既定値 → 表 → 行 → セル → リスト（`Style` / `Format`） → 項目 → 名前付きスタイル → 段落の順に上書きされます。項目の位置はリストが決めるので、`DefaultParagraphFormat` の `FirstLineIndent` と `LeftIndent`、および段落前の改ページは、リストの中では引き継ぎません（そのほかの揃え・行送り・段落間隔は引き継ぎます）。
- リストの中の改ページ（`AddPageBreak()`）と、段落の `PageBreakBefore` は無視されます。リスト全体の前で改ページするときは、`ListBlock.PageBreakBefore` を使います。
- 見出し（`SetOutline`）、アンカー、文書内リンク、ページ参照は、項目の中の段落にも使えます。

### 3.11 ルビ（振り仮名）

`AddRuby("親文字", "ルビ")` で、ルビ（振り仮名）付きの文字列を追加できます。ルビは親文字の中央にそろえて、親文字の半分の大きさで描きます。

<!-- example: Ruby -->

![ルビを付けた段落。モノルビ、記法による指定、長いルビ、色や装飾、両端揃えの長い段落、固定値の行送り](images/ruby.png)

| 書き方 | 内容 |
| --- | --- |
| `AddRuby("東京", "とうきょう")` | 親文字全体に 1 つのルビを付ける（グループルビ）。第 3・第 4 引数で、親文字のスタイルとルビのスタイルを指定できる |
| `AddMonoRuby("東京", new[] { "とう", "きょう" })` | 親文字を 1 文字ずつ区切り、同じ順番のルビを付ける（モノルビ）。ルビを付けない文字は空文字列にする |
| `AddRuby(new[] { "東京", "下町" }, new[] { "とうきょう", "したまち" })` | 親文字とルビの組を、配列で順番に追加する |
| `AddRubyMarkup("｜東京《とうきょう》")` | 「｜親文字《ルビ》」の記法（青空文庫と同じ）で、普通の文字列とルビ付きの文字列をまとめて追加する |

記法は次のとおりです。「《」「》」「｜」「|」は記号として扱うので、これらの文字そのものを入れたいときは `AddText` で追加します。形式が正しくないとき（「》」がない、親文字がない、モノルビの数が合わないなど）は `ArgumentException` になります。

- `｜親文字《ルビ》`: 「｜」（全角または半角の縦線）から「《」までが親文字。親文字には漢字以外も指定できます（`｜Tokyo《とうきょう》`）。
- `東京《とうきょう》`: 「｜」がなければ、「《」の直前に続く漢字（々・〆・〇を含む）が親文字になります。
- `東京《とう|きょう》`: ルビの中を「|」で区切り、区切りの数が親文字の文字数と同じなら、モノルビになります。

#### ルビの配置と大きさ

- ルビの文字サイズは、親文字の半分です。ルビのスタイル（色・フォント・文字サイズ・太字など）は、親文字のスタイルを引き継いだうえで、`Paragraph.RubyStyle`（`SetRubyStyle()`）→ `AddRuby` の `rubyStyle` の順に上書きします。下線・取り消し線・背景色・上付き下付き・字間は引き継がず、ルビには付きません。
- ルビが親文字より短いときは、ルビを親文字の幅の中央にそろえます（ルビの文字の間は広げません）。
- ルビが親文字より長いときは、親文字の文字の間と前後を同じ量だけ空けて、親文字の幅をルビの幅に合わせます。前後の文字にルビをはみ出させることはありません。
- 親文字とルビの組は、途中で改行しません。禁則処理は、組を 1 つの単位として行います。1 行に収まらないほど長い組は、1 行にそのまま置きます。
- 両端揃え・均等割り付けでは、組の中の文字の間は空けず、組と組の間だけを広げます。タブ・太字・下線・上付きなどと組み合わせられます。下線・取り消し線・背景色は、親文字（広げた幅を含む）だけに引きます。
- ルビがある行は、行の上端からベースラインまでの距離がルビの高さに足りないときだけ、足りない分だけ行が高くなります。行間に余裕があるとき（行送りが大きいときなど）は、行の高さは変わりません。ルビがない行の高さも変わりません。行送りが「最小値」（`AtLeast`）のときも同じです。「固定値」（`Exactly`）では行の高さを変えず、ルビは行の上にはみ出して描くので、ルビが入る余白を行送りで確保してください。
- 見出し（`SetOutline`）に使うと、しおりと目次には親文字だけが入ります。
- PDF のテキスト抽出・検索では、ルビを本文に含めません。ルビの描画に空の `ActualText` を付けているので、`ActualText` に対応するソフト（MuPDF で確認）では親文字だけが抽出されます。対応しないソフト（poppler・pypdf で確認）では、各組の親文字の直後にルビが続く順（「東京とうきょう」）で抽出されます。
- ルビは段落（`Paragraph`）だけで使えます。`PdfGraphics.DrawText` / `DrawTextBox` には、ルビを付ける方法はありません。座標を指定して描くときは、段落を `DrawBlocks`（[8.6](#86-段落と箇条書きを描く)）で描きます。

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
- 奇数・偶数ページや 1 ページ目で内容を変えるときは、[4.7](#47-奇数偶数ページと先頭ページのヘッダーフッター) を見てください。ページ番号の書式や開始番号は [4.8](#48-ページ番号の書式と開始番号) で指定します。

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

2 つ目の引数 `PageContext` からは、通しページ番号（`PageNumber`）、表示用のページ番号（`DisplayPageNumber`、文字列は `PageNumberText`）、奇数・偶数（`IsOddPage` / `IsEvenPage`）、総ページ数（`PageCount`）、用紙の大きさ（`PageSize`）、本文の領域（`ContentArea`）を取得できます。

### 4.7 奇数・偶数ページと先頭ページのヘッダー/フッター

両面印刷の冊子のように、ページの左右でヘッダーの位置や内容を変えるときは `Section.DifferentOddEven = true` にして、偶数ページ用の `EvenHeader` / `EvenFooter` を使います。奇数ページには、これまでどおり `Header` / `Footer` が使われます。セクションの 1 ページ目だけ別にするときは `DifferentFirstPage = true` にして、`FirstPageHeader` / `FirstPageFooter` を使います。

<!-- example: OddEvenHeaders -->

![1 ページ目に表題、奇数ページに右寄せ、偶数ページに左寄せのヘッダーとフッターを表示した 3 ページの文書](images/odd-even-headers.png)

- 1 ページ目用（`DifferentFirstPage`）は、奇数・偶数の切り替えより優先されます。
- 奇数か偶数かは、文書全体の通しページ番号（`PageContext.PageNumber`）で決まります。セクションをまたいでも続けて数え、`PageNumberStart` で表示用の番号を変えても影響しません（用紙の表と裏は物理的なページの順番で決まるためです）。
- 切り替えを有効にしたのに、使うヘッダー/フッターに何も追加していないページには、何も表示しません（通常のヘッダー/フッターには切り替わりません）。
- `Header.ShowOnFirstPage = false` の動作は変わりません。これは `Header` / `EvenHeader` などのセクションの 1 ページ目に表示するかの指定で、`FirstPageHeader` / `FirstPageFooter` では使われません。

### 4.8 ページ番号の書式と開始番号

`Section.PageNumberFormat` で書式、`Section.PageNumberStart` で、そのセクションの最初のページの番号を指定します。どちらも指定しない（`null`）と、前のセクションから引き継ぎます（最初のセクションはアラビア数字の 1 から）。前付けをローマ数字、本文を 1 から、という使い方ができます。

<!-- example: PageNumberFormats -->

![ローマ数字の前付け、1 から数える本文、丸数字の付録の 4 ページ](images/page-number-formats.png)

| 書式（`PageNumberFormat`） | 表示 | 範囲外のとき |
| --- | --- | --- |
| `Arabic` | 1, 2, 3 | 常にアラビア数字 |
| `LowerRoman` / `UpperRoman` | i, ii, iii / I, II, III | 1〜3999 以外はアラビア数字 |
| `LowerLetter` / `UpperLetter` | a, b, …, z, aa, bb, … / A, B, … | 1 未満はアラビア数字（27 以降は同じ文字を繰り返す） |
| `FullWidthArabic` | １, ２, ３ | なし |
| `CircledNumber` | ①〜⑳ | 1〜20 以外はアラビア数字 |
| `Kanji` | 一, 二, …, 十, 十一, 二十, 百二十三 | 0〜9999 以外はアラビア数字（0 は「〇」） |

- `AddPageNumber()` と `FieldType.PageNumber` は、表示用のページ番号をセクションの書式で表示します。`AddPageNumber(PageNumberFormat.Kanji)` や `AddField(FieldType.PageCount, PageNumberFormat.UpperRoman)` のように、フィールドごとに書式を指定することもできます。
- `FieldType.SectionPageNumber` と `SectionPageCount` もセクションの書式に従います。`FieldType.PageCount`（総ページ数）は、書式を指定しない限りアラビア数字で、開始番号の指定に関係なく物理的なページ数を表示します。
- 開始番号に関係なく、文書全体での物理的なページの順番を表示するときは `FieldType.PhysicalPageNumber` を使います。
- `PageBackground` / `PageForeground` の `PageContext` では、`DisplayPageNumber`、`PageNumberText`（書式を適用した文字列）、`PageNumberFormat` で表示用のページ番号を取得できます。

### 4.9 しおり（アウトライン）

段落に見出しレベルを付けると、PDF のしおり（アウトライン）が作られます。見出しレベルは、段落の書式の `ParagraphFormat.OutlineLevel`（1〜9。1 が最上位）で指定します。名前付きスタイルに指定しておくと、スタイルを使うだけで見出しになります。`Paragraph.SetOutline(レベル)` で段落ごとに指定することもできます。

<!-- example: Bookmarks -->

- しおりの入れ子は、見出しレベルの大小で決まります。レベルが飛んでも（1 の次に 3）、直前の浅い見出しの子になります。
- しおりの文字列は、段落の文字列です（フィールドは含めず、改行とタブは空白 1 つにまとめます）。`Paragraph.OutlineTitle` で別の文字列にできます。
- `Document.CreateBookmarks`（既定 `true`）を `false` にするとしおりを作りません。見出しレベルを指定した段落がなければ、しおりは作られません。
- `Document.ShowBookmarkPane = true` にすると、PDF を開いたときにしおりの欄を表示します（`/PageMode /UseOutlines`）。しおりが 1 つもなければ何も変わりません。
- しおりの文字列は UTF-16BE の PDF テキスト文字列として保存します。移動先は、見出しの段落の上端です。
- 見出しとして数えるのは、セクションの本文（表のセルの中を含む）の段落だけです。ヘッダー/フッターの段落は数えません。

座標指定の `PdfDocument` / `PdfPage` からも、ページと位置を指定してしおりを追加できます。

<!-- example: BookmarksLowLevel -->

- `PdfDocument.AddBookmark(題, ページ, y)` は最上位のしおりを、`PdfBookmark.AddChild()` は子のしおりを追加します。`PdfPage.AddBookmark(題, y)` は、そのページへ移動するしおりを追加します。`y` はページ上端からの距離（ポイント）です。
- `PdfBookmark.IsOpen`（既定 `true`）で、子を開いた状態で表示するかを指定します。
- `PdfPage.AddLink(矩形, 移動先のページ, y)` と `PdfGraphics.AddLink(矩形, 移動先のページ, y)` は、矩形をクリックしたときに別のページの指定位置へ移動する文書内リンクを追加します。

### 4.10 文書内リンクとページ参照

段落にアンカー（名前）を付けると、そこへ移動するリンクと、そのページの番号を表示するフィールドを置けます。

<!-- example: InternalLinks -->

![先頭ページのリンクとページ参照、第 3 章のアンカーとリンクの 3 ページ](images/internal-links.png)

| メソッド | 動作 |
| --- | --- |
| `Paragraph.SetAnchor(名前)` | 段落の先頭（複数のページにまたがるときは最初のページの先頭）にアンカーを付けます |
| `Paragraph.AddAnchor(名前)` | 段落の途中の位置に、文字を描かないアンカーを置きます（`AnchorRun`） |
| `Paragraph.AddInternalLink(文字列, アンカー名)` | アンカーへ移動するリンク付きのテキスト（既定は青い下線）。`TextRun.LinkAnchor` と同じです |
| `Paragraph.AddPageRef(アンカー名)` | アンカーがあるページの番号。`FieldType.PageRef` と `FieldRun.Anchor` で指定することもできます |

- 移動先は、アンカーの位置が画面の上端に来るように指定します（PDF の `/Dest [ページ /XYZ …]`）。後ろのページのアンカー（前方参照）も指せます。
- ページ参照の表示は、そのアンカーがあるページが属するセクションの書式です（前付けがローマ数字なら、ローマ数字）。`AddPageRef(アンカー名, PageNumberFormat.Kanji)` のように、書式だけ変えることもできます。
- アンカー名は、セクションの本文内で一意にします。重複した名前や、存在しないアンカー名への参照は、`Render()`（`Save()` や `ToArray()` を含む）で `InvalidOperationException` になります。ヘッダー/フッターの段落に付けたアンカーは、飛び先になりません。
- ページ参照を使うと、後ろのページにあるアンカーのページ番号が分かるまでレイアウトを繰り返します（[4.11](#411-目次)）。

### 4.11 目次

`AddTableOfContents()` は、見出しレベルを持つ段落（[4.9](#49-しおりアウトライン)）から目次を自動で作ります。各行は、見出しの文字列、点線のリーダー、ページ番号で、行全体が見出しへの文書内リンクになります。

<!-- example: TableOfContents -->

![ローマ数字のページに置いた目次、第 1 章〜第 8 章の見出しと節のページ番号](images/table-of-contents.png)

| 設定（`TableOfContents`） | 動作 |
| --- | --- |
| `Title` / `TitleStyleName` / `TitleStyle` / `TitleFormat` | 目次の見出しの文字列とスタイル（`Title` が `null` なら見出しなし）。`TitleOutlineLevel` を指定すると、目次の見出しもしおりに入れます |
| `MinLevel` / `MaxLevel` | 目次に入れる見出しレベルの範囲（既定 1〜3） |
| `IndentPerLevel` | レベルが 1 つ深いごとの左インデント（既定 14 ポイント。`null` ならスタイルに従う） |
| `Leader` | 見出しとページ番号の間のリーダー（既定は点線） |
| `ShowPageNumbers` / `Hyperlinks` | ページ番号の表示、行のリンク（どちらも既定 `true`） |
| `PageNumberFormat` | ページ番号の書式。`null`（既定）なら、見出しのあるページのセクションの書式 |
| `EntryStyleName` / `LevelStyleNames` / `EntryStyle` / `EntryFormat` | 目次の各行のスタイル。名前付きスタイルを全体またはレベルごとに指定します |

- 目次は、目次より前にある見出しも後ろにある見出しも載せます。目次自体が複数ページになっても、ページ番号は見出しのページと一致します。
- ページ番号は、前付けをローマ数字、本文を 1 から数え直す構成でも、見出しのページに表示される番号になります。
- ページ番号を確定するために、見出しのページが変わらなくなるまでレイアウトを繰り返します（上限 10 回。確定しなければ `InvalidOperationException`）。そのため、目次やページ参照を使う文書は、レイアウトに通常の数倍の時間がかかります。繰り返しの間は、`PageBackground` / `PageForeground` と `AddDrawing` の描画処理を呼ばず、最後の 1 回だけ呼びます。
- 見出しを数えるのは、セクションの本文（表のセルの中を含む）の段落だけです。ヘッダー/フッターの段落は数えません。

### 4.12 段組み

`PageSetup.ColumnCount` に 2 以上を指定すると、セクションの本文を段組み（2 段組みなど）で組みます。本文は 1 段目の下端まで流れたら 2 段目の上へ、最後の段の下端まで来たら次のページの 1 段目へ進みます。

<!-- example: Columns -->

![1 段の表題のページと、2 段組みの本文のページ。段の間に境界線がある](images/columns.png)

| 設定・メソッド | 動作 |
| --- | --- |
| `PageSetup.ColumnCount` | 段数（既定 1 = 段組みなし。1 未満は `ArgumentOutOfRangeException`） |
| `PageSetup.ColumnSpacing` | 段と段の間隔（既定 8 mm） |
| `PageSetup.ColumnSeparator` | 段の間に引く境界線（`PdfPen`。既定 `null` で引かない）。段の間隔の中央に、本文領域の高さいっぱいに引きます。内容のある段の間だけに引き、最後のページで内容のない段との間には引きません |
| `PageSetup.ColumnWidth` | 1 つの段の幅（読み取り専用）。段の幅は等分で、`(本文の幅 − 段間 × (段数 − 1)) / 段数` です |
| `AddColumnBreak()` | 段区切り。これ以降を次の段の上から始めます（最後の段なら次のページの 1 段目）。段組みでないセクションでは改ページと同じで、段の先頭にある段区切りは無視します |

- 改ページ（`AddPageBreak()` と、段落などの `PageBreakBefore`）は、段ではなく次のページへ進みます。2 段目の先頭にあるときも、1 段目に内容があれば次のページへ進みます。
- 段落は段の下端で分割され、2 段目に続きます（段落の分割位置の調整、`KeepWithNext`、`KeepTogether` は、段の下端でもページの下端と同じように働きます）。表は段の幅で組まれ、段をまたいで行が続くときは見出し行が各段の先頭で繰り返されます。画像、リスト、目次（`AddTableOfContents()`）、ルビ、アンカーや文書内リンクも、段の幅で組まれます。
- **段の幅に収まらない内容**: 画像は `ScaleToFit`（既定 `true`）なら段の幅に縮めます（`false` にすると指定したサイズのまま隣の段にはみ出します）。固定幅（`AddColumn(幅)`）の列の合計が段の幅を超える表は、固定幅の比率を保ったまま段の幅に収まるよう縮めます（段組みでないセクションでは、指定した幅のままはみ出します）。`AddDrawing()` の図は、幅が段の幅を超えると段の幅に縮めます（描画処理に渡される矩形の幅も段の幅になります）。
- ヘッダー/フッター、`PageBackground` / `PageForeground` は、段組みの影響を受けず、本文領域全体の幅のままです。`PageContext.ContentArea` の意味も変わらず、すべての段をまとめた領域です。段ごとの領域（ページ座標）は `PageContext.ColumnAreas`（左の段から順）、段数は `PageContext.ColumnCount` で取得できます。

<!-- example: ColumnAreas -->

![3 段組みで、段ごとの領域を破線で示した図](images/column-areas.png)

- 段数を変えられるのはセクションの区切りだけです。1 段の表題のあとに 2 段の本文を置くときは、セクションを分けます（セクションは新しいページから始まります）。同じページの途中で段数を切り替えることはできません。
- 最後のページの段の高さをそろえる（バランスさせる）機能はありません。最後のページは、1 段目から順に下端まで詰めます。
- しおりの位置、アンカー、文書内リンクの飛び先、目次のページ番号は、見出しが実際に描かれた段の位置（ページと、ページ上端からの縦位置）が使われます。PDF の移動先は縦位置だけを指定するので、2 段目の見出しへ移動すると、2 段目の見出しの高さが表示の上端に来ます。

### 4.13 脚注

`Paragraph.AddFootnote("脚注の文字列")` で、その位置に脚注を付けます。本文には上付きの番号が入り、同じ番号の脚注は、参照のあるページの本文領域の下端（短い区切り線の下）に置かれます。脚注の分だけ、本文の高さは自動で減ります。

<!-- example: Footnotes -->

![脚注のあるページ。本文に上付きの番号が入り、ページの下に区切り線と脚注が並ぶ](images/footnotes.png)

| 設定・メソッド | 動作 |
| --- | --- |
| `Paragraph.AddFootnote(text, style)` | 文字列だけの脚注を付ける（段落を返すので、続けて `AddText` などを呼べます） |
| `Paragraph.AddFootnote()` | 脚注の本文（`Footnote`）を返す。`Section` と同じ `AddParagraph()`、`AddBulletList()` などで、複数の段落やリストを入れられます |
| `Document.FootnoteStyle` / `FootnoteFormat` | 脚注の本文の文字スタイルと段落書式。文書全体に指定します。既定は本文の 0.85 倍の文字サイズ（0.5 ポイント単位）、行送り 1.2 倍、字下げと段落間隔なしで、表のセルやリストの設定は引き継ぎません。脚注の段落に `StyleName` や `Format` を指定すると、それが優先されます |
| `Document.FootnoteNumberFormat` | 番号の書式（`PageNumberFormat`。既定はアラビア数字） |
| `Document.FootnoteSymbols` | 番号の代わりに使う記号の列（例: `"*"`, `"†"`, `"‡"`）。使い切ると `"**"`, `"††"` のように重ねます |
| `Section.FootnoteNumbering` | `Continuous`（既定）は前のセクションからの続き番号、`Restart` はこのセクションの最初の脚注を 1 に戻します |
| `Document.FootnoteSeparator` / `FootnoteSeparatorLength` / `FootnoteSpacing` | 区切り線のペン（既定は黒の 0.5 ポイント。`null` なら線を引かない）、その長さ（既定は本文の幅の 3 分の 1）、脚注と脚注の間の空き（既定 2 ポイント） |

<!-- example: FootnoteNumbers -->

- 参照記号と脚注の番号は、文書の並びの順（表は行ごとに左から右）に自動で振られます。レイアウトの前に決まるので、目次によるレイアウトの繰り返しやページ数の変化で変わることはありません。
- 参照のある行がそのページに入るなら、その脚注も同じページに入ります。入らないときは、参照を含む行ごと次のページへ送ります。段落が複数ページにまたがるときは、参照のある行があるページに脚注を置きます。
- 段組みのセクションでは、脚注はページの全幅（全段をまたぐ）の下端に置き、脚注の分だけ、すべての段の高さが減ります。段の境界線も脚注の上までになります。1 段目が下端まで埋まっているときは、2 段目に脚注のある行を置けないので、その行を次のページへ送ります。
- 表のセルの脚注は、行ごとに数えます。脚注のある行が入らなければ、行ごと次のページへ送ります（`AllowBreakAcrossPages` の行は、入る分だけを分割します）。見出し行（`RepeatHeaderRows`）にある脚注は、最初の 1 回だけ置きます。
- 脚注の本文の段落は、見出しとしては数えません（しおりと目次に入りません）。ページ参照（`AddPageRef`）や文書内リンクは使えます。見出しの段落に付けた脚注の番号は、しおりと目次の文字列に含まれません。
- **使えない場所**: ヘッダー/フッターの段落と、脚注の本文の中（脚注の入れ子）で `AddFootnote` を使うと、レイアウト時に `InvalidOperationException` になります。`PageBackground` / `PageForeground`、`PdfGraphics.DrawText` / `DrawTextBox`、目次の行、ルビには、脚注を付ける方法がありません。文末脚注（後注）には対応していません。
- 1 ページに収まらないほど長い脚注は、次のページの脚注領域へ続けず、ページの下にはみ出して描きます。

---

## 5. 表

### 5.1 表の基本

列を定義してから行を追加します。`AddHeaderRow` で追加した見出し行は、表が次のページに続くときに繰り返し表示されます。

<!-- example: TableBasic -->

![見出し行に背景色を付けた 4 列の表](images/table-basic.png)

セルに入りきらない文字列は、セルの中で折り返します。行の高さは、いちばん高いセルに合わせて広がります。

### 5.2 列幅と揃え

列幅は、固定幅（`AddColumn`）と比率（`AddRelativeColumn`、`AddColumns`）で指定します。固定幅の列を除いた残りの幅を、比率で分け合います。セルの内容に合わせる列（`AddAutoColumn`）もあります（[5.8 列の幅を内容に合わせる](#58-列の幅を内容に合わせる)）。

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

### 5.8 列の幅を内容に合わせる

`AddAutoColumn()` で追加した列（`Column.AutoFit` が `true` の列）は、セルの内容に合わせて幅が決まります。表全体を内容に合わせるときは `Table.AutoFitColumns = true` にします（幅を指定していない列がすべて内容に合わせる列になり、固定幅の列はそのままです）。

<!-- example: TableAutoWidth -->

![内容に合わせて幅が決まる表、収まらず折り返す表、固定幅・比率の列と組み合わせた表、表全体を内容に合わせた表](images/table-auto-width.png)

幅は、HTML の自動表レイアウトに近い方法で決めます。

1. 列ごとに、すべてのセルの内容から **最小幅**（折り返せない最長の単位。欧文の単語や、禁則処理で分けられない文字のまとまり）と **最大幅**（折り返さないときの幅）を調べます。セルの内側の余白と、左右の罫線の太さの半分も加えます。
2. 固定幅の列の幅を除いた残りの幅に最大幅の合計が収まれば、各列を最大幅にします。表の幅は内容の幅になり、本文の幅より狭ければ `Table.Alignment` に従って配置します。
3. 収まらなければ、最大幅に比例して縮めます。最小幅を下回る列は最小幅にして、残りを配り直します。最小幅の合計も収まらないときは、最小幅を同じ比率で縮めて表を本文（段組みでは段）の幅に収めます。このとき、長い欧文の単語は文字の途中で折り返されます。
4. 比率の列（`AddRelativeColumn`）と混ぜると、内容に合わせる列が最大幅を先に取り、残りを比率の列が比率で分けます。比率の列は、その列の最小幅より狭くなりません。残りが足りないときは、比率の列を最小幅にして、残りを内容に合わせる列に配ります。
5. 横に結合したセル（`Cell.Merge`）は、結合した列の合計が足りない分を、内容に合わせる列に最大幅の比で振り分けます（結合の小さいセルから順に処理します）。

計測の対象は、行の数にかかわらず表のすべての行です（ページをまたぐ表でも、列の幅はページごとに変わりません）。セルの中の入れ子の表・リスト・画像・ルビも計測します。幅を指定していない図形（`AddDrawing`）は幅 0、タブ位置は反映せず、タブは既定の幅（空白 4 文字分）として数えます。ページ番号などのフィールドは、最初に計測したページでの文字列で数えます。

`Column.AutoFit` が `true` の列では、`Width` と `RelativeWidth` の値は使いません。

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

JPEG に EXIF の向き（Orientation）があるときの扱いは、[6.4 写真の向き（EXIF）](#64-写真の向きexif)を参照してください。

### 6.2 透過 PNG

PNG の透明な部分は、そのまま透明になります。

<!-- example: ImageTransparency -->

![市松模様の上に透過 PNG のロゴと写真を重ねた例。ロゴの影が半透明になっている](images/image-transparency.png)

### 6.3 枠に合わせる

枠の大きさに合わせて画像を描くには、`DrawImage` に幅と高さを渡す方法と、`DrawImageFit` を使う方法があります。

<!-- example: ImageFit -->

![枠いっぱいに伸ばした画像と、縦横比を保って枠の中央に置いた画像](images/image-fit.png)

`DrawImage(image, x, y)` のように大きさを省略すると、画像の解像度どおりの大きさで描きます。

### 6.4 写真の向き（EXIF）

スマートフォンやデジタルカメラの写真は、画素を横向きのまま保存し、「回転して表示する」という向き（EXIF の Orientation。1〜8）を記録していることがあります。`PdfImage` は、JPEG の APP1（Exif）からこの向きを読み取り、**既定で反映します**。TIFF ヘッダーのバイト順は、リトルエンディアン（`II`）とビッグエンディアン（`MM`）のどちらにも対応します。

<!-- example: ImageOrientation -->

![向きを反映して横長に表示した写真と、向きを無視して保存どおりの縦長（横倒し）に表示した写真](images/image-orientation.png)

- 画素のデータ（DCTDecode）は再エンコードせず、そのまま埋め込みます。回転と反転は、描画時の変換行列で行います（向き 1〜8 のすべてに対応）。
- 向きを反映した画像は、`PdfImage.Width` / `Height` / `PixelWidth` / `PixelHeight` / `DpiX` / `DpiY` が、向きを直した後の画像のものになります（向き 5〜8 では縦と横が入れ替わります）。`AddImage` の大きさの計算や `DrawImageFit` も、この向きの縦横比で行います。
- 向きを無視するには、`PdfImage.FromFile(path, false)`、`PdfImage.FromStream(stream, false)`、`PdfImage.FromBytes(data, false)` のように、第 2 引数に `false` を渡します。保存されている画素の向きのまま描画します。
- 記録されていた向きは `PdfImage.ExifOrientation`（なければ 1）、描画に反映しているかは `PdfImage.OrientationApplied` で分かります。
- EXIF がない JPEG、PNG、`FromRawPixels` で作った画像、向きの値や EXIF の構造が不正なときは、向きを 1（そのまま）として扱います。
- EXIF 以外の向きの情報（XMP や PNG の eXIf チャンク）は読み取りません。

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

この章の帳票は、画面の上で部品を並べてデザインし、同じ PDF を作る C# のコードとして出力することもできます（[ChohyoPdf Designer](designer.md)）。

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
| `TabStops` | なし | タブ位置（[3.9](#39-タブ位置とリーダー)）。矩形の左端からの位置 |
| `DefaultTabStop` | なし | 既定のタブ間隔 |

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

### 8.6 段落と箇条書きを描く

`DrawTextBox` は 1 つの文字列を 1 つの書式で描きます。文字の一部だけを上付き・下付きにしたり、ルビを付けたりした段落（[3.8](#38-上付き下付き)、[3.11](#311-ルビ振り仮名)）や、箇条書き（[3.10](#310-箇条書きと番号付きリスト)）を座標を指定して描くときは、`DrawBlocks(ブロック, 矩形, スタイル, オプション)` を使います。自動レイアウトと同じ `Paragraph`・`ListBlock`・`Table` などを、矩形の中に上から順に並べて描きます。

<!-- example: BlockBoxes -->

![上付き・下付き・ルビのある段落と、入れ子の番号付きリストを、それぞれ枠の中に描いた例](images/block-boxes.png)

- 3 番目の引数のスタイルは、既定の文字スタイル（`Document.DefaultTextStyle` にあたる）です。`TextBoxOptions` の `Alignment`・`LineSpacingRule`・`LineSpacing`・`TabStops`・`DefaultTabStop` は既定の段落書式になり、段落やリストの書式で上書きできます。行送りの既定値は `DrawTextBox` と同じ 1.2 倍です。
- `VerticalAlignment`・`Wrap`・`DrawOverflow` は `DrawTextBox` と同じ動作です。`DrawOverflow` が false なら、矩形に入るところまでを描きます。
- `ShrinkToFit` では、内容全体を同じ倍率で縮めて矩形に収めます（文字の大きさ・行送り・記号の幅などがそろって縮みます）。`MinFontSize` は、既定の文字スタイルの文字サイズに対する下限です。
- 戻り値の `BlockBoxResult` で、描いた高さ（`Height`）、全体の高さ（`TotalHeight`）、収まりきらなかったか（`Overflowed`）、縮めた倍率（`Scale`）が分かります。
- 改ページと段区切りは無視します。脚注・目次・ページ参照・名前付きスタイルは使えません（`InvalidOperationException` になります）。
- アンカー（`SetAnchor` / `AddAnchor`）は `PdfDocument` の飛び先になり、同じ `PdfDocument` に描いた文書内リンク（`AddInternalLink`）から飛べます。別のページの `DrawBlocks` で描いたアンカーにも飛べます。
- ページ番号のフィールドは、そのページが `PdfDocument` の何ページ目かを表示します。総ページ数は、描いた時点のページ数です。

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

次の機能には対応していません。

- 縦書き
- 合字やカーニングなどの OpenType のレイアウト機能（GSUB / GPOS）、アラビア文字などの複雑な文字体系、右から左に書く文字
- 異体字セレクタ（IVS）。無視して、基底の文字の字形で描きます
- CFF 形式の OpenType フォント（[10.3](#103-フォントを読み込めない)）
- 文末脚注（後注）
- 段組みの最後のページで、段の高さをそろえること
- 暗号化、電子署名、PDF/A、タグ付き PDF、フォーム（入力欄）
- PDF の読み込みと編集（PDF を作るためのライブラリです）

各機能の細かい動作と制限は、それぞれの節にまとめています。

---

## 11. API の一覧

主なクラスとメンバーを、用途ごとにまとめています。すべての型とメンバーの説明は [API リファレンス](https://ringoacid.github.io/ChohyoPdf/api/) にあります。

### 11.1 自動レイアウト

| クラス | 主なメンバー |
| --- | --- |
| `Document` | `AddSection()`、`FootnoteStyle`、`FootnoteFormat`、`FootnoteNumberFormat`、`FootnoteSymbols`、`FootnoteSeparator`、`FootnoteSeparatorLength`、`FootnoteSpacing`、`CreateBookmarks`、`ShowBookmarkPane`、`Styles`、`DefaultTextStyle`、`DefaultParagraphFormat`、`Info`、`Save()`、`ToArray()`、`Render()` |
| `PageSetup` / `PageContext` | `PageSetup.Size`、`Margins`、`HeaderDistance`、`FooterDistance`、`ColumnCount`、`ColumnSpacing`、`ColumnSeparator`、`ColumnWidth`、`ContentWidth`、`ContentHeight`、`PageContext.ContentArea`、`ColumnCount`、`ColumnAreas` |
| `Section` | `PageSetup`、`Header`、`Footer`、`DifferentOddEven`、`EvenHeader`、`EvenFooter`、`DifferentFirstPage`、`FirstPageHeader`、`FirstPageFooter`、`PageNumberStart`、`PageNumberFormat`、`FootnoteNumbering`、`PageBackground`、`PageForeground`、`AddParagraph()`、`AddTable()`、`AddImage()`、`AddDrawing()`、`AddPageBreak()`、`AddColumnBreak()`、`AddSpacer()`、`AddHorizontalLine()`、`AddTableOfContents()`、`AddList()`、`AddBulletList()`、`AddNumberedList()` |
| `Paragraph` | `AddText()`、`AddBold()`、`AddLink()`、`AddLineBreak()`、`AddSuperscript()`、`AddSubscript()`、`AddTab()`、`AddTabStop()`、`AddRuby()`、`AddMonoRuby()`、`AddRubyMarkup()`、`RubyStyle`、`SetRubyStyle()`、`AddPageNumber()`、`AddPageCount()`、`AddField()`、`SetAnchor()`、`AddAnchor()`、`AddInternalLink()`、`AddPageRef()`、`AddFootnote()`、`SetOutline()`、`OutlineTitle`、`SetFontSize()`、`SetBold()`、`SetColor()`、`SetAlignment()`、`SetLineSpacing()`、`SetIndent()`、`SetSpaceBefore()`、`SetSpaceAfter()`、`SetDefaultTabStop()`、`SetKeepWithNext()`、`SetKeepTogether()`、`SetPageBreakBefore()`、`SetBackground()`、`SetBorder()` |
| `ParagraphFormat` | `Alignment`、`LineSpacing`、`LineSpacingRule`、`FirstLineIndent`、`LeftIndent`、`RightIndent`、`SpaceBefore`、`SpaceAfter`、`KeepTogether`、`KeepWithNext`、`PageBreakBefore`、`WidowControl`、`Background`、`Border`、`Padding`、`TabStops`、`DefaultTabStop`、`AddTabStop()`、`OutlineLevel` |
| `ListBlock` / `ListBlockItem` | `Kind`（`ListKind`）、`Bullet`、`NumberFormat`、`Prefix`、`Suffix`、`StartNumber`、`MarkerWidth`、`MarkerGap`、`MarkerAlignment`、`MarkerStyle`、`Indent`、`ItemSpacing`、`Style`、`Format`、`AddItem()`、`AddSubList()`、`ListBlockItem.Number`、および `ListBlockItem` は `Section` と同じ `Add〜` メソッド |
| `TabStop` | `Position`、`Alignment`（`TabAlignment`）、`Leader`（`TabLeader`） |
| `TableOfContents` | `Title`、`TitleStyleName`、`TitleStyle`、`TitleFormat`、`TitleOutlineLevel`、`MinLevel`、`MaxLevel`、`IndentPerLevel`、`Leader`、`ShowPageNumbers`、`Hyperlinks`、`PageNumberFormat`、`EntryStyleName`、`LevelStyleNames`、`EntryStyle`、`EntryFormat` |
| `Footnote` / `FootnoteRun` | `Footnote`（`Section` と同じ `Add〜` メソッド）、`FootnoteRun.Footnote`、`FootnoteNumbering`（`Continuous` / `Restart`） |
| `TextRun` / `RubyRun` / `AnchorRun` / `FieldRun` | `RubyRun.BaseText`、`RubyRun.RubyText`、`RubyRun.RubyStyle`、`TextRun.Link`、`TextRun.LinkAnchor`、`AnchorRun.Name`、`FieldRun.Field`（`FieldType.PageRef` など）、`FieldRun.Anchor`、`FieldRun.Format` |
| `Table` | `AddColumn()`、`AddRelativeColumn()`、`AddAutoColumn()`、`AddAutoColumns()`、`AutoFitColumns`、`AddColumns()`、`AddHeaderRow()`、`AddRow()`、`Borders`、`CellPadding`、`HeaderBackground`、`HeaderStyle`、`AlternateRowBackground`、`VerticalAlignment`、`Alignment` |
| `Column` | `Width`、`RelativeWidth`、`AutoFit`、`WidthMode`（`ColumnWidthMode`）、`Alignment`、`Background` |
| `Row` | `Height`、`HeightRule`、`Background`、`Style`、`VerticalAlignment`、`AllowBreakAcrossPages`、`KeepWithNext` |
| `Cell` | `Merge()`、`SetText()`、`SetBackground()`、`SetAlignment()`、`Borders`、`Padding`、`Style`、および `Section` と同じ `Add〜` メソッド |

### 11.2 座標指定

| クラス | 主なメンバー |
| --- | --- |
| `PdfDocument` | `AddPage()`、`Pages`、`Info`、`AddBookmark()`、`Bookmarks`、`ShowBookmarkPane`、`Save()`、`ToArray()` |
| `PdfBookmark` | `Title`、`Page`、`Y`、`IsOpen`、`Children`、`AddChild()` |
| `PdfPage` | `Graphics`、`Width`、`Height`、`AddLink()`（URL または別のページへ）、`AddBookmark()` |
| `PdfGraphics` | `DrawText()`、`DrawTextBox()`、`DrawBlocks()`、`MeasureText()`、`MeasureTextHeight()`、`DrawLine()`、`DrawPolyline()`、`DrawPolygon()`、`DrawRectangle()`、`FillRectangle()`、`DrawRoundedRectangle()`、`DrawEllipse()`、`DrawCircle()`、`DrawPie()`、`DrawPath()`、`DrawImage()`、`DrawImageFit()`、`SaveState()`、`RestoreState()`、`Translate()`、`Scale()`、`Rotate()`、`RotateAt()`、`SetClip()`、`AddLink()`（URL または別のページへ） |
| `PdfPath` | `MoveTo()`、`LineTo()`、`CurveTo()`、`QuadraticTo()`、`Arc()`、`Close()`、`AddRectangle()`、`AddRoundedRectangle()`、`AddEllipse()`、`AddPolygon()` |

### 11.3 共通

| クラス | 主なメンバー |
| --- | --- |
| `PdfFont` | `FromSystem()`、`TryFromSystem()`、`FromFile()`、`FromStream()`、`FromBytes()`、`GetFaceNames()`、`BoldVariant`、`ItalicVariant`、`HasGlyph()`、`MeasureText()`、`IsVariableFont` |
| `PdfImage` | `FromFile()`、`FromStream()`、`FromBytes()`（いずれも、JPEG の EXIF の向きを反映するかを指定する `bool` の引数を持つオーバーロードあり）、`FromRawPixels()`、`PixelWidth`、`PixelHeight`、`Width`、`Height`、`HasAlpha`、`ExifOrientation`、`OrientationApplied` |
| `TextStyle` | `Font`、`FontSize`、`Color`、`Bold`、`Italic`、`Underline`、`Strikeout`、`CharacterSpacing`、`BackgroundColor`、`BaselineShift`、`FallbackFonts` |
| `PdfColor` | `FromRgb()`、`FromHex()`、`FromGray()`、`FromCmyk()`、`WithAlpha()` |
| `PdfPen` | `Color`、`Width`、`DashPattern`、`LineCap`、`LineJoin`、`Dashed()`、`Dotted()` |
| `Unit` | `Mm()`、`Cm()`、`Inch()`、`ToMm()` |

各メンバーの詳しい説明は、ソースコードの XML ドキュメントコメントにあります。Visual Studio などの IntelliSense でも表示されます。
