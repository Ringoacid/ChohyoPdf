using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>
    /// 部品を描くページ。ページの部品では、くり返し行のある表の件数が多く、デザインの 1 ページが PDF の何ページかに増えたときの、
    /// 増えたページの中での位置（ページが増えなければ、どれでもそのページに描く）。
    /// 全ページ共通の部品では、文書全体での位置。
    /// </summary>
    public enum PageVisibility
    {
        /// <summary>増えたすべてのページ。</summary>
        AllPages,

        /// <summary>最初のページだけ（宛名など）。</summary>
        FirstPage,

        /// <summary>最後のページだけ（合計欄など）。</summary>
        LastPage,

        /// <summary>最初のページ以外（「（続き）」の見出しや、表紙に描かないページ番号など）。</summary>
        ExceptFirstPage,
    }

    /// <summary>線の種類。</summary>
    public enum DashStyle
    {
        Solid,
        Dash,
        Dot,
    }

    /// <summary>画像の収め方。</summary>
    public enum ImageFit
    {
        /// <summary>縦横比を保って枠に収める（中央寄せ）。</summary>
        Uniform,

        /// <summary>枠いっぱいに引き伸ばす。</summary>
        Stretch,
    }

    /// <summary>テキストを箇条書きにするか。</summary>
    public enum TextListStyle
    {
        /// <summary>箇条書きにしない。</summary>
        None,

        /// <summary>記号の箇条書き（・ など）。</summary>
        Bullet,

        /// <summary>番号付きの箇条書き（1. など）。</summary>
        Numbered,
    }

    /// <summary>番号付きの箇条書きの番号の形。</summary>
    public enum ListNumberStyle
    {
        /// <summary>入れ子の深さごとの既定（1. → (1) → ①）。</summary>
        Default,

        /// <summary>1) 2) 3)</summary>
        ArabicParenthesis,

        /// <summary>(1) (2) (3)</summary>
        Parenthesized,

        /// <summary>① ② ③</summary>
        Circled,

        /// <summary>一、 二、 三、</summary>
        Kanji,

        /// <summary>（一） （二） （三）</summary>
        KanjiParenthesized,

        /// <summary>a. b. c.</summary>
        LowerLetter,

        /// <summary>A. B. C.</summary>
        UpperLetter,

        /// <summary>i. ii. iii.</summary>
        LowerRoman,

        /// <summary>I. II. III.</summary>
        UpperRoman,
    }

    /// <summary>用紙に置く部品。</summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(TextElement), "text")]
    [JsonDerivedType(typeof(LineElement), "line")]
    [JsonDerivedType(typeof(RectangleElement), "rectangle")]
    [JsonDerivedType(typeof(EllipseElement), "ellipse")]
    [JsonDerivedType(typeof(ImageElement), "image")]
    [JsonDerivedType(typeof(TableElement), "table")]
    public abstract class ReportElement : ObservableObject
    {
        private string _name = "";
        private PageVisibility _visibility;

        /// <summary>部品の名前（一覧と生成コードのコメントに使う）。</summary>
        public string Name { get => _name; set => Set(ref _name, value ?? ""); }

        /// <summary>ページが増えたとき、どのページに描くか。</summary>
        public PageVisibility Visibility { get => _visibility; set => Set(ref _visibility, value); }

        /// <summary>部品の種類の表示名。</summary>
        [JsonIgnore]
        public abstract string KindName { get; }

        /// <summary>部品を囲む矩形（mm）。</summary>
        [JsonIgnore]
        public abstract Rect Bounds { get; }

        /// <summary>部品を平行移動する。</summary>
        public abstract void MoveBy(double dx, double dy);
    }

    /// <summary>位置と大きさを持つ部品。</summary>
    public abstract class BoxElement : ReportElement
    {
        private double _x, _y, _width = 40, _height = 10;

        public double X { get => _x; set { if (Set(ref _x, value)) OnPropertyChanged(nameof(Bounds)); } }

        public double Y { get => _y; set { if (Set(ref _y, value)) OnPropertyChanged(nameof(Bounds)); } }

        public virtual double Width { get => _width; set { if (Set(ref _width, Math.Max(0, value))) OnPropertyChanged(nameof(Bounds)); } }

        public virtual double Height { get => _height; set { if (Set(ref _height, Math.Max(0, value))) OnPropertyChanged(nameof(Bounds)); } }

        public override Rect Bounds => new Rect(X, Y, Width, Height);

        public override void MoveBy(double dx, double dy)
        {
            X += dx;
            Y += dy;
        }
    }

    /// <summary>テキストボックス。{項目名} で値を差し込める。</summary>
    public sealed class TextElement : BoxElement
    {
        private string _text = "テキスト";
        private string? _fontName;
        private double _fontSize = 10.5;
        private bool _bold, _italic, _underline, _strikeout;
        private string _color = "#000000";
        private string? _backgroundColor;
        private TextAlignment _alignment = TextAlignment.Left;
        private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
        private bool _wrap = true;
        private bool _shrinkToFit;
        private double _lineSpacing = 1.2;
        private double _characterSpacing;
        private string? _borderColor;
        private double _borderWidth = 0.5;
        private DashStyle _borderDash;
        private double _padding;
        private TextListStyle _listStyle;
        private string? _bullet;
        private ListNumberStyle _numberStyle;

        public override string KindName => "テキスト";

        public string Text { get => _text; set => Set(ref _text, value ?? ""); }

        /// <summary>フォント。null なら文書の既定のフォント。</summary>
        public string? FontName { get => _fontName; set => Set(ref _fontName, string.IsNullOrWhiteSpace(value) ? null : value); }

        public double FontSize { get => _fontSize; set => Set(ref _fontSize, value); }

        public bool Bold { get => _bold; set => Set(ref _bold, value); }

        public bool Italic { get => _italic; set => Set(ref _italic, value); }

        public bool Underline { get => _underline; set => Set(ref _underline, value); }

        public bool Strikeout { get => _strikeout; set => Set(ref _strikeout, value); }

        /// <summary>文字色（#RRGGBB）。</summary>
        public string Color { get => _color; set => Set(ref _color, value ?? "#000000"); }

        /// <summary>背景色（#RRGGBB）。null なら塗らない。</summary>
        public string? BackgroundColor { get => _backgroundColor; set => Set(ref _backgroundColor, NullIfEmpty(value)); }

        public TextAlignment Alignment { get => _alignment; set => Set(ref _alignment, value); }

        public VerticalAlignment VerticalAlignment { get => _verticalAlignment; set => Set(ref _verticalAlignment, value); }

        /// <summary>枠の幅で折り返す。</summary>
        public bool Wrap { get => _wrap; set => Set(ref _wrap, value); }

        /// <summary>枠に収まらなければ文字を小さくする。</summary>
        public bool ShrinkToFit { get => _shrinkToFit; set => Set(ref _shrinkToFit, value); }

        /// <summary>行送り（文字サイズの倍数）。</summary>
        public double LineSpacing { get => _lineSpacing; set => Set(ref _lineSpacing, value); }

        /// <summary>字間（ポイント）。</summary>
        public double CharacterSpacing { get => _characterSpacing; set => Set(ref _characterSpacing, value); }

        /// <summary>枠線の色。null なら枠線なし。</summary>
        public string? BorderColor { get => _borderColor; set => Set(ref _borderColor, NullIfEmpty(value)); }

        /// <summary>枠線の太さ（ポイント）。</summary>
        public double BorderWidth { get => _borderWidth; set => Set(ref _borderWidth, value); }

        public DashStyle BorderDash { get => _borderDash; set => Set(ref _borderDash, value); }

        /// <summary>枠と文字の間隔（mm）。</summary>
        public double Padding { get => _padding; set => Set(ref _padding, value); }

        /// <summary>箇条書きにする（1 行が 1 項目。行頭のタブの数だけ入れ子にする）。</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TextListStyle ListStyle { get => _listStyle; set => Set(ref _listStyle, value); }

        /// <summary>箇条書きの記号（いちばん外側の項目）。null なら既定の「・」（入れ子の項目は、深さごとの既定）。</summary>
        public string? Bullet { get => _bullet; set => Set(ref _bullet, string.IsNullOrEmpty(value) ? null : value); }

        /// <summary>番号付きの箇条書きの番号の形（いちばん外側の項目。入れ子の項目は、深さごとの既定）。</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ListNumberStyle NumberStyle { get => _numberStyle; set => Set(ref _numberStyle, value); }

        /// <summary>
        /// 1 つの書式ではまとめて描けない（箇条書きにするか、上付き・下付き・ルビの記法がある。<see cref="TextMarkup"/>）。
        /// このときは、段落や箇条書きとして描く（PdfGraphics.DrawBlocks）。
        /// </summary>
        [JsonIgnore]
        public bool IsRich => ListStyle != TextListStyle.None || TextMarkup.HasMarkup(Text);

        internal static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>直線。</summary>
    public sealed class LineElement : ReportElement
    {
        private double _x1, _y1, _x2 = 50, _y2;
        private string _strokeColor = "#000000";
        private double _strokeWidth = 0.5;
        private DashStyle _dash;

        public override string KindName => "直線";

        public double X1 { get => _x1; set { if (Set(ref _x1, value)) OnPropertyChanged(nameof(Bounds)); } }

        public double Y1 { get => _y1; set { if (Set(ref _y1, value)) OnPropertyChanged(nameof(Bounds)); } }

        public double X2 { get => _x2; set { if (Set(ref _x2, value)) OnPropertyChanged(nameof(Bounds)); } }

        public double Y2 { get => _y2; set { if (Set(ref _y2, value)) OnPropertyChanged(nameof(Bounds)); } }

        public string StrokeColor { get => _strokeColor; set => Set(ref _strokeColor, value ?? "#000000"); }

        /// <summary>線の太さ（ポイント）。</summary>
        public double StrokeWidth { get => _strokeWidth; set => Set(ref _strokeWidth, value); }

        public DashStyle Dash { get => _dash; set => Set(ref _dash, value); }

        public override Rect Bounds
            => new Rect(Math.Min(X1, X2), Math.Min(Y1, Y2), Math.Abs(X2 - X1), Math.Abs(Y2 - Y1));

        public override void MoveBy(double dx, double dy)
        {
            X1 += dx;
            Y1 += dy;
            X2 += dx;
            Y2 += dy;
        }
    }

    /// <summary>線と塗りつぶしを持つ図形。</summary>
    public abstract class ShapeElement : BoxElement
    {
        private string? _strokeColor = "#000000";
        private double _strokeWidth = 0.5;
        private DashStyle _dash;
        private string? _fillColor;

        /// <summary>線の色。null なら線なし（既定値が null ではないので、null も保存する）。</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? StrokeColor { get => _strokeColor; set => Set(ref _strokeColor, TextElement.NullIfEmpty(value)); }

        /// <summary>線の太さ（ポイント）。</summary>
        public double StrokeWidth { get => _strokeWidth; set => Set(ref _strokeWidth, value); }

        public DashStyle Dash { get => _dash; set => Set(ref _dash, value); }

        /// <summary>塗りつぶしの色。null なら塗らない。</summary>
        public string? FillColor { get => _fillColor; set => Set(ref _fillColor, TextElement.NullIfEmpty(value)); }
    }

    /// <summary>矩形・角丸矩形。</summary>
    public sealed class RectangleElement : ShapeElement
    {
        private double _cornerRadius;

        public override string KindName => "矩形";

        /// <summary>角の半径（mm）。0 なら角丸にしない。</summary>
        public double CornerRadius { get => _cornerRadius; set => Set(ref _cornerRadius, Math.Max(0, value)); }
    }

    /// <summary>楕円・円。</summary>
    public sealed class EllipseElement : ShapeElement
    {
        public override string KindName => "楕円";
    }

    /// <summary>画像（PNG / JPEG）。</summary>
    public sealed class ImageElement : BoxElement
    {
        private string _path = "";
        private ImageFit _fit;

        public override string KindName => "画像";

        /// <summary>画像ファイルのパス。相対パスはデザインファイルのフォルダから見たもの。</summary>
        public string Path { get => _path; set => Set(ref _path, value ?? ""); }

        public ImageFit Fit { get => _fit; set => Set(ref _fit, value); }
    }

    /// <summary>表の列。</summary>
    public sealed class TableColumn : ObservableObject
    {
        private double _width = 20;
        private TextAlignment _alignment = TextAlignment.Left;

        /// <summary>列の幅（mm）。</summary>
        public double Width { get => _width; set => Set(ref _width, Math.Max(1, value)); }

        /// <summary>列の文字の揃え（セルで指定がなければこれを使う）。</summary>
        public TextAlignment Alignment { get => _alignment; set => Set(ref _alignment, value); }
    }

    /// <summary>表の行。</summary>
    public sealed class TableRow : ObservableObject
    {
        private double _height = 7;

        /// <summary>行の高さ（mm）。</summary>
        public double Height { get => _height; set => Set(ref _height, Math.Max(1, value)); }

        /// <summary>セル。列と同じ数だけ並べる。</summary>
        public ObservableCollection<TableCell> Cells { get; set; } = new ObservableCollection<TableCell>();
    }

    /// <summary>表のセル。スタイルのプロパティは null なら表・列の設定を引き継ぐ。</summary>
    public sealed class TableCell : ObservableObject
    {
        private string _text = "";
        private int _columnSpan = 1, _rowSpan = 1;
        private TextAlignment? _alignment;
        private VerticalAlignment? _verticalAlignment;
        private bool? _bold;
        private double? _fontSize;
        private string? _color;
        private string? _backgroundColor;

        public string Text { get => _text; set => Set(ref _text, value ?? ""); }

        /// <summary>横方向に結合するセルの数。</summary>
        public int ColumnSpan { get => _columnSpan; set => Set(ref _columnSpan, Math.Max(1, value)); }

        /// <summary>縦方向に結合するセルの数（くり返し行では使えない）。</summary>
        public int RowSpan { get => _rowSpan; set => Set(ref _rowSpan, Math.Max(1, value)); }

        public TextAlignment? Alignment { get => _alignment; set => Set(ref _alignment, value); }

        public VerticalAlignment? VerticalAlignment { get => _verticalAlignment; set => Set(ref _verticalAlignment, value); }

        public bool? Bold { get => _bold; set => Set(ref _bold, value); }

        public double? FontSize { get => _fontSize; set => Set(ref _fontSize, value); }

        public string? Color { get => _color; set => Set(ref _color, TextElement.NullIfEmpty(value)); }

        public string? BackgroundColor { get => _backgroundColor; set => Set(ref _backgroundColor, TextElement.NullIfEmpty(value)); }
    }

    /// <summary>
    /// 表。<see cref="Rows"/>（見出しなどの固定の行）の下に、一覧のデータを 1 件 1 行で並べるくり返し行を置ける。
    /// 一覧の件数が 1 ページの行数を超えると、ページを追加して続きを描く。
    /// </summary>
    public sealed class TableElement : BoxElement
    {
        private string? _fontName;
        private double _fontSize = 9;
        private string _color = "#000000";
        private string? _borderColor = "#000000";
        private double _borderWidth = 0.5;
        private double _cellPadding = 1;
        private VerticalAlignment _verticalAlignment = VerticalAlignment.Middle;
        private bool _shrinkToFit = true;
        private bool _wrap;
        private string? _detailSource;
        private int _detailRowCount = 10;
        private TableRow _detailRow = new TableRow();
        private string? _alternateRowColor;

        public override string KindName => "表";

        public string? FontName { get => _fontName; set => Set(ref _fontName, string.IsNullOrWhiteSpace(value) ? null : value); }

        public double FontSize { get => _fontSize; set => Set(ref _fontSize, value); }

        public string Color { get => _color; set => Set(ref _color, value ?? "#000000"); }

        /// <summary>罫線の色。null なら罫線なし（既定値が null ではないので、null も保存する）。</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? BorderColor { get => _borderColor; set => Set(ref _borderColor, TextElement.NullIfEmpty(value)); }

        /// <summary>罫線の太さ（ポイント）。</summary>
        public double BorderWidth { get => _borderWidth; set => Set(ref _borderWidth, value); }

        /// <summary>セルの内側の余白（mm）。</summary>
        public double CellPadding { get => _cellPadding; set => Set(ref _cellPadding, Math.Max(0, value)); }

        public VerticalAlignment VerticalAlignment { get => _verticalAlignment; set => Set(ref _verticalAlignment, value); }

        /// <summary>セルに収まらない文字を小さくする。</summary>
        public bool ShrinkToFit { get => _shrinkToFit; set => Set(ref _shrinkToFit, value); }

        /// <summary>セルの幅で折り返す。false なら 1 行で描く（縮小と組み合わせると、1 行に収まるまで小さくする）。</summary>
        public bool Wrap { get => _wrap; set => Set(ref _wrap, value); }

        public ObservableCollection<TableColumn> Columns { get; set; } = new ObservableCollection<TableColumn>();

        /// <summary>固定の行（見出しなど）。</summary>
        public ObservableCollection<TableRow> Rows { get; set; } = new ObservableCollection<TableRow>();

        /// <summary>くり返し行に並べる一覧の名前。null ならくり返し行なし。</summary>
        public string? DetailSource
        {
            get => _detailSource;
            set
            {
                if (!Set(ref _detailSource, TextElement.NullIfEmpty(value))) return;
                // くり返し行をやめたときに固定の行がなければ、1 行足す（行のない表にしない）
                if (_detailSource == null && Rows.Count == 0 && Columns.Count > 0) Rows.Add(NewRow(DetailRow.Height));
                OnSizeChanged();
            }
        }

        /// <summary>1 ページに並べるくり返し行の数。データが少なくても、この数だけ空の行を描く。</summary>
        public int DetailRowCount
        {
            get => _detailRowCount;
            set { if (Set(ref _detailRowCount, Math.Max(1, value))) OnSizeChanged(); }
        }

        /// <summary>くり返し行の見本（セルに {列名} を書く）。</summary>
        public TableRow DetailRow
        {
            get => _detailRow;
            set { if (Set(ref _detailRow, value ?? new TableRow())) OnSizeChanged(); }
        }

        /// <summary>くり返し行の 1 行おきの背景色。null なら塗らない。</summary>
        public string? AlternateRowColor { get => _alternateRowColor; set => Set(ref _alternateRowColor, TextElement.NullIfEmpty(value)); }

        [JsonIgnore]
        public bool HasDetail => DetailSource != null;

        /// <summary>表の幅（列の幅の合計）。設定すると列の幅を同じ比率で変える。</summary>
        [JsonIgnore]
        public override double Width
        {
            get => Columns.Sum(c => c.Width);
            set
            {
                double current = Width;
                if (current <= 0 || value <= 0 || Math.Abs(current - value) < 1e-9) return;
                foreach (var c in Columns) c.Width = c.Width * value / current;
                OnSizeChanged();
            }
        }

        /// <summary>表の高さ（固定の行と、1 ページ分のくり返し行の合計）。設定すると行の高さを同じ比率で変える。</summary>
        [JsonIgnore]
        public override double Height
        {
            get => Rows.Sum(r => r.Height) + (HasDetail ? DetailRow.Height * DetailRowCount : 0);
            set
            {
                double current = Height;
                if (current <= 0 || value <= 0 || Math.Abs(current - value) < 1e-9) return;
                foreach (var r in Rows) r.Height = r.Height * value / current;
                DetailRow.Height = DetailRow.Height * value / current;
                OnSizeChanged();
            }
        }

        /// <summary>行や列の大きさを変えたあとに呼び、<see cref="Width"/> などの変更を通知する。</summary>
        public void OnSizeChanged()
        {
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(Bounds));
            OnPropertyChanged(nameof(HasDetail));
        }

        /// <summary>行・列の数を指定して、空の表を作る。</summary>
        public static TableElement Create(int columns, int rows, double columnWidth = 25, double rowHeight = 7)
        {
            var table = new TableElement();
            for (int c = 0; c < columns; c++) table.Columns.Add(new TableColumn { Width = columnWidth });
            for (int r = 0; r < rows; r++) table.Rows.Add(table.NewRow(rowHeight));
            table.DetailRow = table.NewRow(rowHeight);
            return table;
        }

        /// <summary>列の数だけセルを持つ行を作る。</summary>
        public TableRow NewRow(double height)
        {
            var row = new TableRow { Height = height };
            for (int c = 0; c < Columns.Count; c++) row.Cells.Add(new TableCell());
            return row;
        }

        /// <summary>
        /// 表の中の行。0 〜 Rows.Count - 1 は固定の行、Rows.Count はくり返し行の見本。
        /// </summary>
        public TableRow? GetRow(int index)
            => index >= 0 && index < Rows.Count ? Rows[index] : index == Rows.Count && HasDetail ? DetailRow : null;

        /// <summary>編集できる行の数（固定の行 ＋ くり返し行の見本）。</summary>
        [JsonIgnore]
        public int EditableRowCount => Rows.Count + (HasDetail ? 1 : 0);

        /// <summary>
        /// 各セルを描くセル（結合の左上のセル）の位置。結合で隠れていないセルは自分自身の位置になる。
        /// 添字は [行, 列]。行は 0 〜 Rows.Count - 1 が固定の行、Rows.Count がくり返し行。
        /// </summary>
        public (int Row, int Column)[,] GetOwnerMap()
        {
            int rowCount = Rows.Count + 1;
            var map = new (int Row, int Column)[rowCount, Columns.Count];
            var assigned = new bool[rowCount, Columns.Count];
            for (int r = 0; r < rowCount; r++)
            {
                var row = r < Rows.Count ? Rows[r] : DetailRow;
                for (int c = 0; c < Columns.Count; c++)
                {
                    if (assigned[r, c]) continue;
                    var cell = c < row.Cells.Count ? row.Cells[c] : null;
                    int rs = cell == null ? 1 : ClampRowSpan(cell.RowSpan, r);
                    int cs = cell == null ? 1 : ClampSpan(cell.ColumnSpan, c);
                    for (int rr = r; rr < r + rs; rr++)
                    {
                        for (int cc = c; cc < c + cs; cc++)
                        {
                            if (assigned[rr, cc]) continue;
                            assigned[rr, cc] = true;
                            map[rr, cc] = (r, c);
                        }
                    }
                }
            }
            return map;
        }

        /// <summary>結合で隠れているセルなら、隠しているセルの位置を返す。隠れていなければ自分自身の位置を返す。</summary>
        public (int Row, int Column) GetOwner(int row, int column) => GetOwnerMap()[row, column];

        /// <summary>ほかのセルの結合で隠れているか。</summary>
        public bool IsCovered(int row, int column) => GetOwner(row, column) != (row, column);

        /// <summary>表の範囲を超えない横方向の結合数。</summary>
        public int ClampSpan(int span, int column) => Math.Max(1, Math.Min(span, Columns.Count - column));

        /// <summary>固定の行の範囲を超えない縦方向の結合数。</summary>
        public int ClampRowSpan(int span, int row) => row >= Rows.Count ? 1 : Math.Max(1, Math.Min(span, Rows.Count - row));

        /// <summary>行・列の数に合わせて、各行のセルの数を揃える。</summary>
        public void NormalizeCells()
        {
            if (Rows.Count == 0 && !HasDetail) Rows.Add(new TableRow { Height = DetailRow.Height });
            foreach (var row in Rows.Concat(new[] { DetailRow }))
            {
                while (row.Cells.Count < Columns.Count) row.Cells.Add(new TableCell());
                while (row.Cells.Count > Columns.Count) row.Cells.RemoveAt(row.Cells.Count - 1);
            }
        }
    }

    /// <summary>矩形（mm）。</summary>
    public readonly struct Rect : IEquatable<Rect>
    {
        public Rect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;

        public bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;

        public bool IntersectsWith(Rect other)
            => other.X <= Right && other.Right >= X && other.Y <= Bottom && other.Bottom >= Y;

        public static Rect FromPoints(double x1, double y1, double x2, double y2)
            => new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));

        public bool Equals(Rect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) => obj is Rect r && Equals(r);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    }
}
