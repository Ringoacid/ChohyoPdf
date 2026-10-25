using System;
using System.Collections.Generic;
using ChohyoPdf.Layout;

namespace ChohyoPdf
{
    /// <summary>罫線の指定。</summary>
    public sealed class BorderLine
    {
        /// <summary>色と線幅（ポイント）を指定して作る。</summary>
        public BorderLine(PdfColor color, double width = 0.5)
        {
            Color = color;
            Width = width;
        }

        /// <summary>線の色。</summary>
        public PdfColor Color { get; set; }

        /// <summary>線幅（ポイント）。</summary>
        public double Width { get; set; }

        /// <summary>破線パターン。null なら実線。</summary>
        public double[]? DashPattern { get; set; }

        /// <summary>罫線なし。</summary>
        public static BorderLine None => new BorderLine(PdfColor.Transparent, 0);

        /// <summary>黒の細線（0.5pt）。</summary>
        public static BorderLine Thin => new BorderLine(PdfColor.Black, 0.5);

        /// <summary>黒の太線（1.5pt）。</summary>
        public static BorderLine Thick => new BorderLine(PdfColor.Black, 1.5);

        /// <summary>破線を作る。</summary>
        public static BorderLine Dashed(PdfColor color, double width = 0.5) => new BorderLine(color, width) { DashPattern = new[] { 3.0, 2.0 } };

        internal bool IsVisible => Width > 0 && Color.Alpha > 0;

        internal PdfPen ToPen() => new PdfPen(Color, Width) { DashPattern = DashPattern, LineCap = LineCap.Square };
    }

    /// <summary>セルの上下左右の罫線。null の辺は表の既定値を使う。</summary>
    public sealed class CellBorders
    {
        /// <summary>上の罫線。</summary>
        public BorderLine? Top { get; set; }
        /// <summary>下の罫線。</summary>
        public BorderLine? Bottom { get; set; }
        /// <summary>左の罫線。</summary>
        public BorderLine? Left { get; set; }
        /// <summary>右の罫線。</summary>
        public BorderLine? Right { get; set; }

        /// <summary>全辺を同じ罫線にする。</summary>
        public CellBorders All(BorderLine? line)
        {
            Top = Bottom = Left = Right = line;
            return this;
        }
    }

    /// <summary>列の幅の決め方。</summary>
    public enum ColumnWidthMode
    {
        /// <summary>固定幅（<see cref="Column.Width"/>）。</summary>
        Fixed,

        /// <summary>比率（<see cref="Column.RelativeWidth"/>）で、固定幅と内容に合わせる列の残りの幅を分ける。</summary>
        Relative,

        /// <summary>セルの内容に合わせる（<see cref="Column.AutoFit"/>）。</summary>
        Auto,
    }

    /// <summary>表の列。</summary>
    public sealed class Column
    {
        internal Column()
        {
        }

        /// <summary>固定幅（ポイント）。null なら <see cref="RelativeWidth"/> の比率で残りの幅を分ける。<see cref="AutoFit"/> が true の間は使われない。</summary>
        public double? Width { get; set; }

        /// <summary>相対幅（固定幅でない列で残りの幅を分け合う比率）。<see cref="AutoFit"/> が true の間は使われない。</summary>
        public double RelativeWidth { get; set; } = 1;

        /// <summary>
        /// true なら、列の幅をセルの内容に合わせる（<see cref="Width"/> と <see cref="RelativeWidth"/> は使わない）。
        /// 各列のセルの内容の最小幅（折り返せない最長の単位）と最大幅（折り返さないときの幅）を調べ、HTML の自動表レイアウトに近い方法で配分する。
        /// 既定は false。<see cref="Table.AddAutoColumn"/> で追加した列は true になる。
        /// </summary>
        public bool AutoFit { get; set; }

        /// <summary>
        /// 幅の決め方（<see cref="AutoFit"/> が true なら Auto、そうでなくて <see cref="Width"/> があれば Fixed、なければ Relative）。
        /// 表全体の <see cref="Table.AutoFitColumns"/> は反映しない。
        /// </summary>
        public ColumnWidthMode WidthMode => AutoFit ? ColumnWidthMode.Auto : Width.HasValue ? ColumnWidthMode.Fixed : ColumnWidthMode.Relative;

        /// <summary>この列のセルの既定の揃え。</summary>
        public TextAlignment? Alignment { get; set; }

        /// <summary>この列のセルの既定の背景色。</summary>
        public PdfColor? Background { get; set; }
    }

    /// <summary>行の高さの指定方法。</summary>
    public enum RowHeightRule
    {
        /// <summary>内容に合わせて広がる（<see cref="Row.Height"/> は最小値）。</summary>
        AtLeast,

        /// <summary>固定（はみ出した内容は切り取る）。</summary>
        Exactly,
    }

    /// <summary>表の行。</summary>
    public sealed class Row
    {
        internal Row(Table table, int columns)
        {
            Table = table;
            for (int i = 0; i < columns; i++) Cells.Add(new Cell(this));
        }

        /// <summary>この行がある表。</summary>
        public Table Table { get; }

        /// <summary>セル（列の数だけある）。</summary>
        public List<Cell> Cells { get; } = new List<Cell>();

        /// <summary>列番号でセルを得る。</summary>
        public Cell this[int column] => Cells[column];

        /// <summary>行の高さ（<see cref="HeightRule"/> に従う）。</summary>
        public double? Height { get; set; }

        /// <summary><see cref="Height"/> の扱い（既定 <see cref="RowHeightRule.AtLeast"/>）。</summary>
        public RowHeightRule HeightRule { get; set; } = RowHeightRule.AtLeast;

        /// <summary>行の背景色。</summary>
        public PdfColor? Background { get; set; }

        /// <summary>行の文字スタイル。</summary>
        public TextStyle? Style { get; set; }

        /// <summary>行内のセルの垂直方向の揃え。</summary>
        public VerticalAlignment? VerticalAlignment { get; set; }

        /// <summary>
        /// ページ末尾に収まらないとき、行を分割して残りの高さを使うか（既定 false: 行ごと次のページへ送る）。
        /// false でも、行が 1 ページに収まらない場合は分割する。
        /// </summary>
        public bool AllowBreakAcrossPages { get; set; }

        /// <summary>次の行と同じページに置く。</summary>
        public bool KeepWithNext { get; set; }
    }

    /// <summary>表のセル。段落・画像・表などを入れられる。</summary>
    public sealed class Cell : BlockContainer
    {
        internal Cell(Row row)
        {
            Row = row;
        }

        /// <summary>このセルがある行。</summary>
        public Row Row { get; }

        /// <summary>右方向に結合する列数（1 = 結合しない）。</summary>
        public int ColumnSpan { get; set; } = 1;

        /// <summary>下方向に結合する行数（1 = 結合しない）。</summary>
        public int RowSpan { get; set; } = 1;

        /// <summary>背景色。</summary>
        public PdfColor? Background { get; set; }

        /// <summary>内側の余白。null なら表の既定値。</summary>
        public Thickness? Padding { get; set; }

        /// <summary>垂直方向の揃え。</summary>
        public VerticalAlignment? VerticalAlignment { get; set; }

        /// <summary>セル内の段落の既定の揃え。</summary>
        public TextAlignment? Alignment { get; set; }

        /// <summary>セルの文字スタイル。</summary>
        public TextStyle? Style { get; set; }

        /// <summary>罫線（null の辺は表の既定値）。</summary>
        public CellBorders Borders { get; } = new CellBorders();

        /// <summary>セルのテキストを設定する（既存の内容は置き換える）。</summary>
        public Cell SetText(string text, TextStyle? style = null)
        {
            Blocks.Clear();
            AddParagraph(text, style);
            return this;
        }

        /// <summary>結合を設定する。</summary>
        public Cell Merge(int columnSpan, int rowSpan = 1)
        {
            if (columnSpan < 1 || rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(columnSpan));
            ColumnSpan = columnSpan;
            RowSpan = rowSpan;
            return this;
        }

        /// <summary>背景色を指定する。</summary>
        public Cell SetBackground(PdfColor color)
        {
            Background = color;
            return this;
        }

        /// <summary>水平方向の揃えと、垂直方向の揃えを指定する。</summary>
        public Cell SetAlignment(TextAlignment alignment, VerticalAlignment? vertical = null)
        {
            Alignment = alignment;
            if (vertical.HasValue) VerticalAlignment = vertical;
            return this;
        }
    }

    /// <summary>
    /// 表。列幅（固定・比率・内容に合わせる）、セル結合、罫線、背景色、見出し行の繰り返し、ページをまたぐ行の分割に対応する。
    /// </summary>
    public sealed class Table : BoxBlock
    {
        /// <summary>列。</summary>
        public List<Column> Columns { get; } = new List<Column>();

        /// <summary>行（見出し行を含む）。</summary>
        public List<Row> Rows { get; } = new List<Row>();

        /// <summary>見出し行の数。改ページ後の各ページの先頭に繰り返し表示される。</summary>
        public int HeaderRowCount { get; set; }

        /// <summary>見出し行を改ページ後にも繰り返すか（既定 true）。</summary>
        public bool RepeatHeaderRows { get; set; } = true;

        /// <summary>罫線の既定値（全セルの全辺）。null なら罫線なし。</summary>
        public BorderLine? Borders { get; set; } = BorderLine.Thin;

        /// <summary>セルの内側の余白の既定値。</summary>
        public Thickness CellPadding { get; set; } = new Thickness(3, 2, 3, 2);

        /// <summary>表全体の文字スタイル。</summary>
        public TextStyle? Style { get; set; }

        /// <summary>見出し行の背景色。</summary>
        public PdfColor? HeaderBackground { get; set; }

        /// <summary>見出し行の文字スタイル。</summary>
        public TextStyle? HeaderStyle { get; set; }

        /// <summary>見出し以外の行で 1 行おきに付ける背景色（縞模様）。</summary>
        public PdfColor? AlternateRowBackground { get; set; }

        /// <summary>セルの垂直方向の揃えの既定値。</summary>
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

        /// <summary>表の水平位置（列幅の合計が利用できる幅より狭い場合）。</summary>
        public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;

        /// <summary>
        /// true なら、固定幅（<see cref="Column.Width"/>）を指定していない列をすべて内容に合わせる（比率の列も内容に合わせる）。
        /// 固定幅の列はそのまま。既定は false。<see cref="Column.AutoFit"/> を個別に指定した列と同じ扱い。
        /// </summary>
        public bool AutoFitColumns { get; set; }

        /// <summary>固定幅の列を追加する。</summary>
        public Column AddColumn(double width)
        {
            var c = new Column { Width = width };
            AddColumnInternal(c);
            return c;
        }

        /// <summary>比率で幅を決める列を追加する。</summary>
        public Column AddRelativeColumn(double relativeWidth = 1)
        {
            var c = new Column { RelativeWidth = relativeWidth };
            AddColumnInternal(c);
            return c;
        }

        /// <summary>セルの内容に合わせて幅を決める列を追加する（<see cref="Column.AutoFit"/> が true の列）。</summary>
        public Column AddAutoColumn()
        {
            var c = new Column { AutoFit = true };
            AddColumnInternal(c);
            return c;
        }

        /// <summary>セルの内容に合わせて幅を決める列を、<paramref name="count"/> 個まとめて追加する。</summary>
        public Table AddAutoColumns(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++) AddAutoColumn();
            return this;
        }

        /// <summary>比率で幅を決める列をまとめて追加する（例: AddColumns(1, 2, 1)）。</summary>
        public Table AddColumns(params double[] relativeWidths)
        {
            foreach (double w in relativeWidths) AddRelativeColumn(w);
            return this;
        }

        private void AddColumnInternal(Column c)
        {
            if (Rows.Count > 0) throw new InvalidOperationException("列は行を追加する前に定義してください。");
            Columns.Add(c);
        }

        /// <summary>行を追加する。<paramref name="texts"/> を渡すと先頭のセルから順にテキストを入れる。</summary>
        public Row AddRow(params string[] texts)
        {
            if (Columns.Count == 0) throw new InvalidOperationException("先に列を追加してください。");
            var row = new Row(this, Columns.Count);
            for (int i = 0; i < texts.Length && i < Columns.Count; i++)
            {
                if (texts[i] != null) row.Cells[i].AddParagraph(texts[i]);
            }
            Rows.Add(row);
            return row;
        }

        /// <summary>見出し行を追加する（<see cref="HeaderRowCount"/> も増やす）。</summary>
        public Row AddHeaderRow(params string[] texts)
        {
            if (HeaderRowCount != Rows.Count) throw new InvalidOperationException("見出し行は本文の行より前に追加してください。");
            var row = AddRow(texts);
            HeaderRowCount++;
            return row;
        }

        internal override LayoutNode CreateNode(StyleScope scope) => new TableNode(this, scope);
    }
}
