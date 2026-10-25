using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;

namespace ChohyoPdf.Designer.CodeGen
{
    /// <summary>コード生成の設定。</summary>
    public sealed class CodeGenOptions
    {
        /// <summary>名前空間。</summary>
        public string Namespace { get; set; } = "MyApp.Reports";

        /// <summary>PDF を作るクラスの名前。空なら文書の名前 ＋ "Report"。</summary>
        public string? ClassName { get; set; }

        /// <summary>サンプルの値を入れたデータを作るメソッドを含める。</summary>
        public bool IncludeSampleData { get; set; } = true;

        /// <summary>見出しのコメントに書くデザインファイルの名前。</summary>
        public string? DesignFileName { get; set; }
    }

    /// <summary>生成するコードのデータのクラスの形。</summary>
    public sealed class DataShape
    {
        public DataShape(string dataClass, string reportClass)
        {
            DataClass = dataClass;
            ReportClass = reportClass;
        }

        /// <summary>データのクラスの名前（例: 請求書Data）。</summary>
        public string DataClass { get; }

        /// <summary>PDF を作るクラスの名前（例: 請求書Report）。</summary>
        public string ReportClass { get; }

        /// <summary>項目ごとのプロパティ。</summary>
        public Dictionary<FieldDefinition, DataMember> Fields { get; } = new Dictionary<FieldDefinition, DataMember>();

        /// <summary>一覧ごとのプロパティ。</summary>
        public Dictionary<ListDefinition, DataList> Lists { get; } = new Dictionary<ListDefinition, DataList>();
    }

    /// <summary>データのクラスのプロパティ。</summary>
    public sealed class DataMember
    {
        public DataMember(string property, string type, string exampleValue)
        {
            Property = property;
            Type = type;
            ExampleValue = exampleValue;
        }

        public string Property { get; }

        /// <summary>型（string / decimal / DateTime）。</summary>
        public string Type { get; }

        /// <summary>サンプルの値を C# のリテラルにしたもの。</summary>
        public string ExampleValue { get; }
    }

    /// <summary>一覧のプロパティ（List&lt;ItemClass&gt;）。</summary>
    public sealed class DataList
    {
        public DataList(string property, string itemClass, Dictionary<FieldDefinition, DataMember> columns)
        {
            Property = property;
            ItemClass = itemClass;
            Columns = columns;
        }

        public string Property { get; }

        /// <summary>1 件のクラスの名前（例: 明細Item）。</summary>
        public string ItemClass { get; }

        public Dictionary<FieldDefinition, DataMember> Columns { get; }
    }

    /// <summary>
    /// デザインから、ChohyoPdf だけを使って PDF を作る C# のコードを作る。
    /// 出力するコードは <see cref="ReportRenderer"/> と同じ順序・同じ計算で描く。
    /// C# 7.3 以降でコンパイルでき、null 許容参照型を有効にしても警告が出ないように書く。
    /// </summary>
    public sealed class CSharpGenerator
    {
        private readonly ReportDefinition _report;
        private readonly CodeGenOptions _options;
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indent;

        // 名前の対応
        private string _reportClass = "", _dataClass = "";
        private readonly Dictionary<FieldDefinition, string> _fieldNames = new Dictionary<FieldDefinition, string>();
        // C# の式で計算する差し込み項目と、計算するメソッドの名前
        private readonly Dictionary<FieldDefinition, string> _computeNames = new Dictionary<FieldDefinition, string>();
        // 式に #line を付けて、エラーの位置を項目の名前と行で分かるようにする（デザイナーの中で計算するときだけ）
        private bool _lineDirectives;
        private readonly Dictionary<ListDefinition, ListInfo> _lists = new Dictionary<ListDefinition, ListInfo>();
        private readonly List<FontInfo> _fonts = new List<FontInfo>();
        private readonly List<ImageInfo> _images = new List<ImageInfo>();
        private bool _usesCellWithPen, _usesCellWithoutPen;
        // 記法のあるセル（段落で描く DrawCell を書く）。差し込んだ値をルビにする（AddRubyText を書く）
        private bool _usesParagraphCellWithPen, _usesParagraphCellWithoutPen, _usesRubyText;

        // 書いているページの {ページ番号} {総ページ数} の式（C# の式。どのページも増えないなら数字）
        private string _pageNumberExpr = "1", _pageCountExpr = "1";
        private bool _pageNumbersAreConstant, _usesPageNumberVariable, _usesPageCountVariable;
        // 全ページ共通の部品を書いている（DrawCommon の中。ページ番号は引数、表は行をくり返さない）
        private bool _inCommon;
        // {今日} などで PDF を作る日時を使う（Create に now を渡す）。和暦の書式を使う（FormatJapaneseDate を書く）
        private bool _usesNow, _usesJapaneseDate;

        private sealed class ListInfo
        {
            public string Property = "", ItemClass = "", Local = "";
            public Dictionary<FieldDefinition, string> Columns = new Dictionary<FieldDefinition, string>();
        }

        private sealed class FontInfo
        {
            public string Name = "", Property = "";
            public string? Bold;
        }

        private sealed class ImageInfo
        {
            public string Path = "", Property = "";
        }

        private CSharpGenerator(ReportDefinition report, CodeGenOptions options)
        {
            _report = report;
            _options = options;
        }

        public static string Generate(ReportDefinition report, CodeGenOptions? options = null)
            => new CSharpGenerator(report, options ?? new CodeGenOptions()).Generate();

        /// <summary>生成するコードで、項目・一覧がどの名前のプロパティになるか（データの渡し方の説明に使う）。</summary>
        public static DataShape DescribeData(ReportDefinition report, CodeGenOptions? options = null)
        {
            var g = new CSharpGenerator(report, options ?? new CodeGenOptions());
            g.AssignNames();
            var shape = new DataShape(g._dataClass, g._reportClass);
            foreach (var pair in g._fieldNames) shape.Fields[pair.Key] = Member(pair.Key, pair.Value, pair.Key.SampleValue);
            foreach (var list in g.UsedLists())
            {
                var info = g._lists[list];
                var columns = new Dictionary<FieldDefinition, DataMember>();
                foreach (var pair in info.Columns) columns[pair.Key] = Member(pair.Key, pair.Value, null);
                shape.Lists[list] = new DataList(info.Property, info.ItemClass, columns);
            }
            return shape;
        }

        private static DataMember Member(FieldDefinition field, string property, string? sample)
        {
            string type = field.Kind == FieldKind.Number ? "decimal" : field.Kind == FieldKind.Date ? "DateTime" : "string";
            TextTemplate.TryParseSample(sample ?? "", field.Kind, out object value);
            return new DataMember(property, type, ValueLiteral(value, field.Kind));
        }

        /// <summary>
        /// データを作って PDF を保存するコードの例。項目はサンプルの値を入れ、一覧はサンプルの最初の行を 1 件追加する。
        /// </summary>
        public static string DataExample(ReportDefinition report)
        {
            var shape = DescribeData(report);
            var lines = new List<string> { "var data = new " + shape.DataClass + "();" };
            foreach (var pair in shape.Fields) lines.Add("data." + pair.Value.Property + " = " + pair.Value.ExampleValue + ";");
            foreach (var pair in shape.Lists)
            {
                var list = pair.Key;
                var first = list.SampleRows.FirstOrDefault();
                var init = new List<string>();
                for (int c = 0; c < list.Columns.Count; c++)
                {
                    if (!pair.Value.Columns.TryGetValue(list.Columns[c], out var member)) continue;
                    TextTemplate.TryParseSample(first != null && c < first.Count ? first[c] : "", list.Columns[c].Kind, out object value);
                    init.Add(member.Property + " = " + ValueLiteral(value, list.Columns[c].Kind));
                }
                lines.Add("// 「" + Comment(list.Name) + "」は 1 件ごとに Add する（1 件が表の 1 行になる）");
                lines.Add("data." + pair.Value.Property + ".Add(new " + pair.Value.ItemClass + (init.Count == 0 ? "()" : " { " + string.Join(", ", init) + " }") + ");");
            }
            lines.Add(shape.ReportClass + ".Save(data, " + Code.String((report.Name.Length > 0 ? report.Name : "文書") + ".pdf") + ");");
            return string.Join(Environment.NewLine, lines);
        }

        private string Generate()
        {
            AssignNames();
            // 計算する項目のメソッドは now を受け取る
            _usesNow = UsesNow() || _computeNames.Count > 0;
            string ns = _options.Namespace.Trim();
            bool hasNamespace = ns.Length > 0;
            // 本体を先に作り、使ったフォント・画像・補助メソッドを調べてから全体を組み立てる
            string createMethod = Capture(hasNamespace ? 2 : 1, WriteCreateMethod);
            string commonMethod = UsesCommon ? Capture(hasNamespace ? 2 : 1, WriteCommonMethod) : "";

            Line("// ChohyoPdf Designer で作成したコードです。" + (_options.DesignFileName != null ? "（デザイン: " + _options.DesignFileName + "）" : ""));
            Line("// NuGet パッケージ ChohyoPdf を参照して使います（dotnet add package ChohyoPdf）。");
            Line("using System;");
            Line("using System.Collections.Generic;");
            Line("using System.Globalization;");
            if (_images.Count > 0) Line("using System.IO;");
            if (_computeNames.Count > 0) Line("using System.Linq;");
            Line("using ChohyoPdf;");
            Line();
            if (hasNamespace)
            {
                Line("namespace " + ns);
                Open();
            }

            WriteDataClass();
            foreach (var list in UsedLists())
            {
                Line();
                WriteItemClass(list);
            }
            Line();
            WriteReportClass(createMethod, commonMethod);

            if (hasNamespace) Close();
            return _sb.ToString();
        }

        // ---- 名前 ----

        private void AssignNames()
        {
            string baseName = Identifiers.From(_report.Name, "Chohyo");
            var types = new NameScope();
            _reportClass = types.Reserve(string.IsNullOrWhiteSpace(_options.ClassName) ? baseName + "Report" : Identifiers.From(_options.ClassName!, baseName + "Report"));
            _dataClass = types.Reserve(baseName + "Data");

            var dataMembers = new NameScope(_dataClass.TrimStart('@'), "CreateSample");
            foreach (var field in _report.Fields)
            {
                if (_fieldNames.ContainsKey(field) || _computeNames.ContainsKey(field) || _report.FindField(field.Name) != field) continue;
                // 計算する項目は、データのクラスには入れない（値は渡さない）
                if (field.IsComputed) _computeNames[field] = UniqueMember("Compute" + Identifiers.From(field.Name, "Field").TrimStart('@'));
                else _fieldNames[field] = dataMembers.Reserve(Identifiers.From(field.Name, "Field"));
            }
            int n = 0;
            foreach (var list in UsedLists())
            {
                n++;
                var info = new ListInfo
                {
                    Property = dataMembers.Reserve(Identifiers.From(list.Name, "List")),
                    ItemClass = types.Reserve(Identifiers.From(list.Name, "List") + "Item"),
                    Local = "list" + n,
                };
                var members = new NameScope(info.ItemClass.TrimStart('@'));
                foreach (var column in list.Columns)
                {
                    if (list.FindColumn(column.Name) != column) continue;
                    info.Columns[column] = members.Reserve(Identifiers.From(column.Name, "Column"));
                }
                _lists[list] = info;
            }
        }

        /// <summary>一覧（同じ名前が複数あれば最初のもの）。</summary>
        private IEnumerable<ListDefinition> UsedLists()
            => _report.Lists.Where(l => _report.FindList(l.Name) == l);

        // ---- データのクラス ----

        private void WriteDataClass()
        {
            Line("/// <summary>「" + XmlText(_report.Name) + "」に差し込むデータ。</summary>");
            Line("public class " + _dataClass);
            Open();
            foreach (var pair in _fieldNames) WriteProperty(pair.Key, pair.Value);
            foreach (var list in UsedLists())
            {
                var info = _lists[list];
                Line("public List<" + info.ItemClass + "> " + info.Property + " { get; set; } = new List<" + info.ItemClass + ">();");
            }

            if (_options.IncludeSampleData)
            {
                Line();
                Line("/// <summary>デザイナーで入力したサンプルの値を入れたデータを作る。</summary>");
                Line("public static " + _dataClass + " CreateSample()");
                Open();
                Line("var data = new " + _dataClass + "();");
                foreach (var pair in _fieldNames)
                {
                    TextTemplate.TryParseSample(pair.Key.SampleValue, pair.Key.Kind, out object value);
                    Line("data." + pair.Value + " = " + ValueLiteral(value, pair.Key.Kind) + ";");
                }
                foreach (var list in UsedLists())
                {
                    var info = _lists[list];
                    foreach (var sample in list.SampleRows)
                    {
                        var init = new List<string>();
                        for (int c = 0; c < list.Columns.Count; c++)
                        {
                            var column = list.Columns[c];
                            if (!info.Columns.TryGetValue(column, out string? prop)) continue;
                            TextTemplate.TryParseSample(c < sample.Count ? sample[c] : "", column.Kind, out object value);
                            init.Add(prop + " = " + ValueLiteral(value, column.Kind));
                        }
                        Line("data." + info.Property + ".Add(new " + info.ItemClass + Code.Initializer(init) + (init.Count == 0 ? "()" : "") + ");");
                    }
                }
                Line("return data;");
                Close();
            }
            Close();
        }

        private void WriteItemClass(ListDefinition list)
        {
            var info = _lists[list];
            Line("/// <summary>一覧「" + XmlText(list.Name) + "」の 1 行。</summary>");
            Line("public class " + info.ItemClass);
            Open();
            foreach (var pair in info.Columns) WriteProperty(pair.Key, pair.Value);
            Close();
        }

        private void WriteProperty(FieldDefinition field, string name)
        {
            if (name.TrimStart('@') != field.Name) Line("/// <summary>" + XmlText(field.Name) + "</summary>");
            switch (field.Kind)
            {
                case FieldKind.Number: Line("public decimal " + name + " { get; set; }"); break;
                case FieldKind.Date: Line("public DateTime " + name + " { get; set; }"); break;
                default: Line("public string " + name + " { get; set; } = \"\";"); break;
            }
        }

        private static string ValueLiteral(object value, FieldKind kind)
        {
            switch (kind)
            {
                case FieldKind.Number:
                    return ((decimal)value).ToString(CultureInfo.InvariantCulture) + "m";
                case FieldKind.Date:
                    {
                        var d = (DateTime)value;
                        string s = "new DateTime(" + d.Year + ", " + d.Month + ", " + d.Day;
                        if (d.TimeOfDay != TimeSpan.Zero) s += ", " + d.Hour + ", " + d.Minute + ", " + d.Second;
                        return s + ")";
                    }
                default:
                    return Code.String((string)value);
            }
        }

        // ---- PDF を作るクラス ----

        private void WriteReportClass(string createMethod, string commonMethod)
        {
            Line("/// <summary>「" + XmlText(_report.Name) + "」の PDF を作る。</summary>");
            Line("public static class " + _reportClass);
            Open();
            Line("private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo(\"ja-JP\");");
            if (_usesJapaneseDate)
            {
                Line();
                Line("/// <summary>和暦（元号）で日付を書くカルチャ。</summary>");
                Line("private static readonly CultureInfo JapaneseCulture = CreateJapaneseCulture();");
            }
            if (_images.Count > 0)
            {
                Line();
                Line("/// <summary>画像ファイルを探すフォルダ。既定は実行ファイルのフォルダ。</summary>");
                Line("public static string ImageFolder { get; set; } = AppContext.BaseDirectory;");
            }
            Line();
            Line("/// <summary>PDF を作ってファイルに保存する。</summary>");
            Line("public static void Save(" + _dataClass + " data, string path)");
            Open();
            Line("Create(data).Save(path);");
            Close();
            Line();
            _sb.Append(createMethod);
            if (commonMethod.Length > 0)
            {
                Line();
                _sb.Append(commonMethod);
            }
            WriteComputeMethods(false);

            if (_usesJapaneseDate)
            {
                Line();
                Line("private static CultureInfo CreateJapaneseCulture()");
                Open();
                Line("var culture = (CultureInfo)CultureInfo.GetCultureInfo(\"ja-JP\").Clone();");
                Line("culture.DateTimeFormat.Calendar = new JapaneseCalendar();");
                Line("return culture;");
                Close();
                Line();
                Line("/// <summary>和暦（元号）で日付を書く。元号のない、明治より前の日付は空文字列にする。</summary>");
                Line("private static string FormatJapaneseDate(DateTime date, string format)");
                Open();
                Line("return date < JapaneseCulture.DateTimeFormat.Calendar.MinSupportedDateTime ? \"\" : date.ToString(format, JapaneseCulture);");
                Close();
            }

            if (_usesCellWithPen || _usesCellWithoutPen) Line();
            if (_usesCellWithPen)
            {
                Line("/// <summary>表のセルを描く（罫線・背景・文字）。</summary>");
                Line("private static void DrawCell(PdfGraphics g, string text, double x, double y, double width, double height,");
                Line("    TextStyle style, TextBoxOptions options, PdfPen pen, PdfColor? fill, double padding)");
                Open();
                Line("g.DrawRectangle(pen, fill, x, y, width, height);");
                Line("DrawCellText(g, text, x, y, width, height, style, options, padding);");
                Close();
            }
            if (_usesCellWithPen && _usesCellWithoutPen) Line();
            if (_usesCellWithoutPen)
            {
                Line("/// <summary>罫線のない表のセルを描く（背景・文字）。</summary>");
                Line("private static void DrawCell(PdfGraphics g, string text, double x, double y, double width, double height,");
                Line("    TextStyle style, TextBoxOptions options, PdfColor? fill, double padding)");
                Open();
                Line("if (fill != null) g.FillRectangle(fill.Value, x, y, width, height);");
                Line("DrawCellText(g, text, x, y, width, height, style, options, padding);");
                Close();
            }
            if (_usesCellWithPen || _usesCellWithoutPen)
            {
                Line();
                Line("private static void DrawCellText(PdfGraphics g, string text, double x, double y, double width, double height,");
                Line("    TextStyle style, TextBoxOptions options, double padding)");
                Open();
                Line("if (text.Length == 0) return;");
                Line("double w = width - padding * 2, h = height - padding * 2;");
                Line("if (w <= 0 || h <= 0) return;");
                Line("g.DrawTextBox(text, new PdfRect(x + padding, y + padding, w, h), style, options);");
                Close();
            }
            if (_usesParagraphCellWithPen)
            {
                Line();
                Line("/// <summary>上付き・下付き・ルビのあるセルを描く（罫線・背景・段落）。</summary>");
                Line("private static void DrawCell(PdfGraphics g, Paragraph paragraph, double x, double y, double width, double height,");
                Line("    TextStyle style, TextBoxOptions options, PdfPen pen, PdfColor? fill, double padding)");
                Open();
                Line("g.DrawRectangle(pen, fill, x, y, width, height);");
                Line("DrawCellText(g, paragraph, x, y, width, height, style, options, padding);");
                Close();
            }
            if (_usesParagraphCellWithoutPen)
            {
                Line();
                Line("/// <summary>罫線のない表の、上付き・下付き・ルビのあるセルを描く（背景・段落）。</summary>");
                Line("private static void DrawCell(PdfGraphics g, Paragraph paragraph, double x, double y, double width, double height,");
                Line("    TextStyle style, TextBoxOptions options, PdfColor? fill, double padding)");
                Open();
                Line("if (fill != null) g.FillRectangle(fill.Value, x, y, width, height);");
                Line("DrawCellText(g, paragraph, x, y, width, height, style, options, padding);");
                Close();
            }
            if (_usesParagraphCellWithPen || _usesParagraphCellWithoutPen)
            {
                Line();
                Line("private static void DrawCellText(PdfGraphics g, Paragraph paragraph, double x, double y, double width, double height,");
                Line("    TextStyle style, TextBoxOptions options, double padding)");
                Open();
                Line("double w = width - padding * 2, h = height - padding * 2;");
                Line("if (w <= 0 || h <= 0) return;");
                Line("g.DrawBlocks(paragraph, new PdfRect(x + padding, y + padding, w, h), style, options);");
                Close();
            }
            if (_usesRubyText)
            {
                Line();
                Line("/// <summary>差し込んだ値にルビを付ける。親文字が空なら何も足さず、改行とタブは空白にする（ルビには含められないため）。</summary>");
                Line("private static Paragraph AddRubyText(this Paragraph paragraph, string baseText, string rubyText)");
                Open();
                Line("baseText = (baseText ?? \"\").Replace('\\r', ' ').Replace('\\n', ' ').Replace('\\t', ' ');");
                Line("rubyText = (rubyText ?? \"\").Replace('\\r', ' ').Replace('\\n', ' ').Replace('\\t', ' ');");
                Line("return baseText.Length == 0 ? paragraph : paragraph.AddRuby(baseText, rubyText);");
                Close();
            }

            if (_fonts.Count > 0)
            {
                Line();
                Line("// ---- フォント（PdfFont は複数の PDF で共有できるので、一度だけ読み込む） ----");
                Line();
                foreach (var font in _fonts)
                {
                    string load = font.Bold != null
                        ? "LoadFont(" + Code.String(font.Name) + ", " + Code.String(font.Bold) + ")"
                        : "PdfFont.FromSystem(" + Code.String(font.Name) + ")";
                    Line("private static readonly Lazy<PdfFont> " + font.Property + " = new Lazy<PdfFont>(() => " + load + ");");
                }
                if (_fonts.Any(f => f.Bold != null))
                {
                    Line();
                    Line("/// <summary>フォントを読み込み、太字に使うフォントを対応付ける。</summary>");
                    Line("private static PdfFont LoadFont(string name, string boldName)");
                    Open();
                    Line("PdfFont font = PdfFont.FromSystem(name);");
                    Line("font.BoldVariant = PdfFont.FromSystem(boldName);");
                    Line("return font;");
                    Close();
                }
            }

            if (_images.Count > 0)
            {
                Line();
                Line("// ---- 画像 ----");
                Line();
                foreach (var image in _images)
                {
                    Line("private static readonly Lazy<PdfImage> " + image.Property + " = new Lazy<PdfImage>(() => PdfImage.FromFile(Path.Combine(ImageFolder, " + Code.String(image.Path) + ")));");
                }
            }
            Close();
        }

        private void WriteCreateMethod()
        {
            if (_usesNow)
            {
                Line("/// <summary>PDF を作る。{今日} などには、今の日時を使う。</summary>");
                Line("public static PdfDocument Create(" + _dataClass + " data)");
                Open();
                Line("return Create(data, DateTime.Now);");
                Close();
                Line();
                Line("/// <summary>PDF を作る。</summary>");
                Line("/// <param name=\"now\">{今日} {現在時刻} などに使う日時。</param>");
                Line("public static PdfDocument Create(" + _dataClass + " data, DateTime now)");
            }
            else
            {
                Line("/// <summary>PDF を作る。</summary>");
                Line("public static PdfDocument Create(" + _dataClass + " data)");
            }
            Open();
            Line("if (data == null) throw new ArgumentNullException(\"data\");");
            foreach (var list in UsedLists())
            {
                var info = _lists[list];
                Line("var " + info.Local + " = data." + info.Property + " ?? new List<" + info.ItemClass + ">();");
            }
            Line();
            Line("var pdf = new PdfDocument();");
            if (_report.Name.Length > 0) Line("pdf.Info.Title = " + Code.String(_report.Name) + ";");

            // くり返し行のある表を置いたページは、件数に応じて増える（ReportRenderer.CountPages と同じ計算）
            var pages = _report.Pages;
            bool single = pages.Count == 1;
            var countVars = new List<string?>();
            for (int i = 0; i < pages.Count; i++)
                countVars.Add(!DetailTables(pages[i]).Any() ? null : single ? "pageCount" : "pages" + (i + 1).ToString(CultureInfo.InvariantCulture));
            _pageNumbersAreConstant = countVars.All(v => v == null);

            // ページの中身を先に作り、ページ番号の変数を使うかを調べる
            var bodies = new List<string>();
            for (int i = 0; i < pages.Count; i++)
            {
                if (_pageNumbersAreConstant)
                {
                    _pageNumberExpr = (i + 1).ToString(CultureInfo.InvariantCulture);
                    _pageCountExpr = pages.Count.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    _pageNumberExpr = single ? "(pageIndex + 1)" : "pageNumber";
                    _pageCountExpr = "pageCount";
                }
                var page = pages[i];
                string? countVar = countVars[i];
                bodies.Add(Capture(0, () => WritePageElements(page, countVar)));
            }
            // 全ページ共通の部品には、ページ番号と総ページ数を渡す
            if (!_pageNumbersAreConstant && !single && pages.Any(CallsCommon))
            {
                _usesPageNumberVariable = true;
                _usesPageCountVariable = true;
            }

            if (!_pageNumbersAreConstant)
            {
                Line();
                Line("// くり返し行の件数からページ数を決める");
                for (int i = 0; i < pages.Count; i++)
                {
                    if (countVars[i] == null) continue;
                    Line("int " + countVars[i] + " = 1;");
                    foreach (var (table, list) in DetailTables(pages[i]))
                    {
                        int n = table.DetailRowCount;
                        Line(countVars[i] + " = Math.Max(" + countVars[i] + ", (" + _lists[list].Local + ".Count + " + (n - 1) + ") / " + n + "); // " + Comment(Label(table)) + "は 1 ページ " + n + " 行");
                    }
                }
                if (!single && _usesPageCountVariable)
                {
                    var terms = countVars.Where(v => v != null).Select(v => v!).ToList();
                    int fixedPages = countVars.Count(v => v == null);
                    if (fixedPages > 0) terms.Add(fixedPages.ToString(CultureInfo.InvariantCulture));
                    Line("int pageCount = " + string.Join(" + ", terms) + "; // 総ページ数");
                }
                if (!single && _usesPageNumberVariable) Line("int pageNumber = 0;");
            }

            for (int i = 0; i < pages.Count; i++)
            {
                Line();
                if (!single) Line("// ---- " + PageLabel(pages[i], i) + " ----");
                if (countVars[i] != null) Line("for (int pageIndex = 0; pageIndex < " + countVars[i] + "; pageIndex++)");
                // 複数のページでは、ページごとにブロックで囲んで変数 g の範囲を分ける
                bool block = countVars[i] != null || !single;
                if (block) Open();
                Line("PdfGraphics g = pdf.AddPage(" + PageSizeCode(pages[i]) + ").Graphics;");
                if (!single && _usesPageNumberVariable) Line("pageNumber++;");
                if (CallsCommon(pages[i]))
                {
                    string number = _pageNumbersAreConstant ? (i + 1).ToString(CultureInfo.InvariantCulture) : single ? "pageIndex + 1" : "pageNumber";
                    string count = _pageNumbersAreConstant ? pages.Count.ToString(CultureInfo.InvariantCulture) : "pageCount";
                    Line("DrawCommon(g, data, " + number + ", " + count + (_usesNow ? ", now" : "") + "); // 全ページ共通の部品");
                }
                _sb.Append(Indent(bodies[i], _indent));
                if (block) Close();
            }
            Line("return pdf;");
            Close();
        }

        /// <summary>ページにある、くり返し行のある表と、その一覧（一覧が見つからない表は除く）。</summary>
        private IEnumerable<(TableElement Table, ListDefinition List)> DetailTables(ReportPage page)
        {
            foreach (var element in page.Elements)
            {
                if (element is TableElement table && table.HasDetail && _report.FindList(table.DetailSource) is ListDefinition list) yield return (table, list);
            }
        }

        /// <summary>全ページ共通の部品があるか（描くページが 1 つもなければ、ないのと同じ）。</summary>
        private bool UsesCommon => _report.Pages.Any(CallsCommon);

        /// <summary>ページに全ページ共通の部品を描くか。</summary>
        private bool CallsCommon(ReportPage page) => !page.HideCommon && _report.Common.Elements.Count > 0;

        /// <summary>全ページ共通の部品を描くメソッド（ReportRenderer.DrawCommon と同じ処理）。</summary>
        private void WriteCommonMethod()
        {
            Line("/// <summary>全ページ共通の部品を描く。pageNumber は文書全体でのページ番号（1 から）。</summary>");
            Line("private static void DrawCommon(PdfGraphics g, " + _dataClass + " data, int pageNumber, int pageCount" + (_usesNow ? ", DateTime now" : "") + ")");
            Open();
            _inCommon = true;
            string body = Capture(0, () => WriteElements(_report.Common.Elements, v =>
                v == PageVisibility.FirstPage ? "if (pageNumber == 1)"
                : v == PageVisibility.LastPage ? "if (pageNumber == pageCount)"
                : v == PageVisibility.ExceptFirstPage ? "if (pageNumber != 1)"
                : null));
            _inCommon = false;
            // 先頭の空行は書かない
            _sb.Append(Indent(body.StartsWith("\r\n", StringComparison.Ordinal) ? body.Substring(2) : body, _indent));
            Close();
        }

        /// <summary>ページの部品を描くコード。countVar は、ページが増えるときのそのページの数の変数（増えなければ null）。</summary>
        private void WritePageElements(ReportPage page, string? countVar)
        {
            // ページが増えないなら、最初のページも最後のページも同じページなので、条件を付けない
            WriteElements(page.Elements, v => countVar == null ? null
                : v == PageVisibility.FirstPage ? "if (pageIndex == 0)"
                : v == PageVisibility.LastPage ? "if (pageIndex == " + countVar + " - 1)"
                : v == PageVisibility.ExceptFirstPage ? "if (pageIndex != 0)"
                : null);
        }

        /// <summary>部品を描くコード。condition は、描くページの設定から if の条件を作る（null なら条件を付けない）。</summary>
        private void WriteElements(IEnumerable<ReportElement> elements, Func<PageVisibility, string?> condition)
        {
            foreach (var element in elements)
            {
                string body = Capture(0, () => WriteElement(element));
                if (body.Length == 0) continue;
                Line();
                Line("// " + Comment(Label(element)));
                string? test = condition(element.Visibility);
                if (test == null)
                {
                    _sb.Append(Indent(body, _indent));
                }
                else
                {
                    Line(test);
                    if (element is TableElement)
                    {
                        // 表はもともとブロックで囲んであるので、そのまま if の本体にする
                        _sb.Append(Indent(body, _indent));
                    }
                    else
                    {
                        Open();
                        _sb.Append(Indent(body, _indent));
                        Close();
                    }
                }
            }
        }

        private static string PageLabel(ReportPage page, int index)
            => (index + 1).ToString(CultureInfo.InvariantCulture) + " ページ目" + (page.Name.Length > 0 ? "「" + Comment(page.Name) + "」" : "");

        /// <summary>ページの用紙の大きさ（ReportRenderer.GetPageSize と同じ値）。</summary>
        private string PageSizeCode(ReportPage page)
        {
            var paper = PaperSizes.Find(page.Paper?.PaperName ?? _report.PaperName);
            if (paper == null) return "PageSize.FromMm(" + Code.Number(_report.PageWidthOf(page)) + ", " + Code.Number(_report.PageHeightOf(page)) + ")";
            return "PageSize." + paper.PageSizeProperty + ((page.Paper?.Landscape ?? _report.Landscape) ? ".Landscape" : "");
        }

        // ---- 部品（ReportRenderer の DrawXxx と同じ処理） ----

        private void WriteElement(ReportElement element)
        {
            switch (element)
            {
                case TextElement text: WriteText(text); break;
                case LineElement line: WriteLine(line); break;
                case RectangleElement rect: WriteRectangle(rect); break;
                case EllipseElement ellipse: WriteEllipse(ellipse); break;
                case ImageElement image: WriteImage(image); break;
                case TableElement table: WriteTable(table); break;
            }
        }

        private void WriteText(TextElement e)
        {
            WriteBox(Styles.PenCode(e.BorderColor, e.BorderWidth, e.BorderDash), e.BackgroundColor, e.X, e.Y, e.Width, e.Height);
            string? rect = Styles.TextBoxRectCode(e);
            if (e.Text.Length == 0 || rect == null) return;
            string font = Font(e.FontName);
            if (e.IsRich)
            {
                WriteRichText(e, rect, font);
                return;
            }
            string text = TextExpression(e.Text, null, null);
            Line("g.DrawTextBox(" + text + ", " + rect + ",");
            Line("    " + Styles.TextStyleCode(e, font) + ",");
            Line("    " + Styles.TextBoxOptionsCode(e) + ");");
        }

        /// <summary>箇条書きや記法のあるテキスト（RichText.Build で作ったものを DrawBlocks で描くのと同じ）。</summary>
        private void WriteRichText(TextElement e, string rect, string font)
        {
            string style = Styles.TextStyleCode(e, font), options = Styles.TextBoxOptionsCode(e);
            if (e.ListStyle == TextListStyle.None)
            {
                Line("g.DrawBlocks(" + ParagraphExpression(TextMarkup.Parse(e.Text), null, null) + ", " + rect + ",");
                Line("    " + style + ",");
                Line("    " + options + ");");
                return;
            }
            // 入れ子のリストは、1 つ浅いリストの最後の項目に作る（RichText.Build と同じ順序）
            Line("{");
            _indent++;
            Line("var listBlock = " + RichText.NewListCode(e) + ";");
            var stack = new List<string> { "listBlock" };
            int lists = 1;
            foreach (var item in RichText.Items(e.Text))
            {
                if (item.NewSubList)
                {
                    string name = "listBlock" + (++lists).ToString(CultureInfo.InvariantCulture);
                    Line("var " + name + " = " + stack[item.Depth - 1] + ".AddSubList();");
                    stack.Add(name);
                }
                else if (stack.Count > item.Depth + 1)
                {
                    stack.RemoveRange(item.Depth + 1, stack.Count - item.Depth - 1);
                }
                var calls = RunCalls(item.Runs, null, null);
                string target = stack[item.Depth];
                if (calls.Count > 0 && calls[0].Method == "AddText") Line(target + ".AddItem(" + calls[0].Args + ")" + Chain(calls.Skip(1)) + ";");
                else Line(target + ".AddItem().AddParagraph()" + Chain(calls) + ";");
            }
            Line("g.DrawBlocks(listBlock, " + rect + ",");
            Line("    " + style + ",");
            Line("    " + options + ");");
            _indent--;
            Line("}");
        }

        /// <summary>記法のある文字列を、段落に足すメソッドの呼び出しの並びにする（RichText.AddRuns と同じ。中身が空の部分は飛ばす）。</summary>
        private List<(string Method, string Args)> RunCalls(IEnumerable<MarkupRun> runs, ListDefinition? list, string? itemVar)
        {
            var calls = new List<(string, string)>();
            foreach (var run in runs)
            {
                if (run.Text.Length == 0) continue;
                string text = TextExpression(run.Text, list, itemVar);
                switch (run.Kind)
                {
                    case MarkupKind.Superscript: calls.Add(("AddSuperscript", text)); break;
                    case MarkupKind.Subscript: calls.Add(("AddSubscript", text)); break;
                    case MarkupKind.Ruby:
                        {
                            // 差し込み欄のないルビは、記法を読んだときに空でないこと、改行やタブがないことを確かめてある
                            string ruby = TextExpression(run.Ruby, list, itemVar);
                            bool constant = TextTemplate.Parse(run.Text).All(s => !s.IsField) && TextTemplate.Parse(run.Ruby).All(s => !s.IsField);
                            if (!constant) _usesRubyText = true;
                            calls.Add((constant ? "AddRuby" : "AddRubyText", text + ", " + ruby));
                            break;
                        }
                    default: calls.Add(("AddText", text)); break;
                }
            }
            return calls;
        }

        /// <summary>段落を作る式（RichText.AddRuns(new Paragraph(), ...) と同じ段落になる）。</summary>
        private string ParagraphExpression(IEnumerable<MarkupRun> runs, ListDefinition? list, string? itemVar)
        {
            var calls = RunCalls(runs, list, itemVar);
            if (calls.Count > 0 && calls[0].Method == "AddText") return "new Paragraph(" + calls[0].Args + ")" + Chain(calls.Skip(1));
            return "new Paragraph()" + Chain(calls);
        }

        private static string Chain(IEnumerable<(string Method, string Args)> calls) => string.Concat(calls.Select(c => "." + c.Method + "(" + c.Args + ")"));

        /// <summary>枠線と背景を描く（ReportRenderer では DrawRectangle(pen, fill, ...) を 1 回呼ぶのと同じ）。</summary>
        private void WriteBox(string? pen, string? fill, double x, double y, double w, double h)
        {
            string xywh = Code.Mm(x) + ", " + Code.Mm(y) + ", " + Code.Mm(w) + ", " + Code.Mm(h);
            if (pen != null && fill != null) Line("g.DrawRectangle(" + pen + ", " + Styles.ColorCode(fill) + ", " + xywh + ");");
            else if (pen != null) Line("g.DrawRectangle(" + pen + ", " + xywh + ");");
            else if (fill != null) Line("g.FillRectangle(" + Styles.ColorCode(fill) + ", " + xywh + ");");
        }

        private void WriteLine(LineElement e)
        {
            string? pen = Styles.PenCode(e.StrokeColor, e.StrokeWidth, e.Dash);
            if (pen == null) return;
            Line("g.DrawLine(" + pen + ", " + Code.Mm(e.X1) + ", " + Code.Mm(e.Y1) + ", " + Code.Mm(e.X2) + ", " + Code.Mm(e.Y2) + ");");
        }

        private void WriteRectangle(RectangleElement e)
        {
            string? pen = Styles.PenCode(e.StrokeColor, e.StrokeWidth, e.Dash);
            if (pen == null && e.FillColor == null) return;
            if (e.CornerRadius > 0)
            {
                Line("g.DrawRoundedRectangle(" + (pen ?? "null") + ", " + (e.FillColor != null ? Styles.ColorCode(e.FillColor) : "null") + ", "
                    + Code.Mm(e.X) + ", " + Code.Mm(e.Y) + ", " + Code.Mm(e.Width) + ", " + Code.Mm(e.Height) + ", " + Code.Mm(e.CornerRadius) + ");");
            }
            else
            {
                WriteBox(pen, e.FillColor, e.X, e.Y, e.Width, e.Height);
            }
        }

        private void WriteEllipse(EllipseElement e)
        {
            string? pen = Styles.PenCode(e.StrokeColor, e.StrokeWidth, e.Dash);
            if (pen == null && e.FillColor == null) return;
            Line("g.DrawEllipse(" + (pen ?? "null") + ", " + (e.FillColor != null ? Styles.ColorCode(e.FillColor) : "null") + ", "
                + Code.Mm(e.X) + ", " + Code.Mm(e.Y) + ", " + Code.Mm(e.Width) + ", " + Code.Mm(e.Height) + ");");
        }

        private void WriteImage(ImageElement e)
        {
            if (e.Path.Length == 0) return;
            var image = _images.FirstOrDefault(i => string.Equals(i.Path, e.Path, StringComparison.OrdinalIgnoreCase));
            if (image == null)
            {
                string baseName = System.IO.Path.GetFileNameWithoutExtension(e.Path);
                image = new ImageInfo { Path = e.Path, Property = UniqueMember("Image" + Identifiers.PascalAscii(baseName, (_images.Count + 1).ToString(CultureInfo.InvariantCulture))) };
                _images.Add(image);
            }
            string method = e.Fit == ImageFit.Stretch ? "DrawImage" : "DrawImageFit";
            Line("g." + method + "(" + image.Property + ".Value, " + Code.Mm(e.X) + ", " + Code.Mm(e.Y) + ", " + Code.Mm(e.Width) + ", " + Code.Mm(e.Height) + ");");
        }

        private void WriteTable(TableElement t)
        {
            string font = Font(t.FontName);
            string? pen = Styles.PenCode(t.BorderColor, t.BorderWidth, DashStyle.Solid);
            if (pen != null) _usesCellWithPen = true;
            else _usesCellWithoutPen = true;

            var layout = new TableLayout(t);
            var owners = t.GetOwnerMap();
            // セルのスタイルは表の中で同じものをまとめて変数にする
            var styleVars = new Dictionary<string, string>();
            var body = new StringBuilder();
            string Var(string expr, string prefix)
            {
                if (!styleVars.TryGetValue(expr, out string? name))
                {
                    name = prefix + (styleVars.Count(p => p.Value.StartsWith(prefix, StringComparison.Ordinal)) + 1);
                    styleVars[expr] = name;
                }
                return name;
            }
            string Cell(string text, string x, string y, string w, string h, TableCell cell, int column, string fill)
            {
                string style = Var(Styles.CellStyleCode(t, cell, font), "style");
                string options = Var(Styles.CellOptionsCode(t, cell, column), "options");
                return "DrawCell(g, " + text + ", " + x + ", " + y + ", " + w + ", " + h + ", " + style + ", " + options + ", "
                    + (pen != null ? "pen, " : "") + fill + ", padding);";
            }
            // 上付き・下付き・ルビの記法があるセルは、段落で描く（ReportRenderer.CellContent と同じ）
            string? ParagraphCell(string template, ListDefinition? list, string? itemVar)
            {
                if (!TextMarkup.HasMarkup(template)) return null;
                if (pen != null) _usesParagraphCellWithPen = true;
                else _usesParagraphCellWithoutPen = true;
                return ParagraphExpression(TextMarkup.Parse(template), list, itemVar);
            }

            for (int r = 0; r < t.Rows.Count; r++)
            {
                for (int c = 0; c < t.Columns.Count; c++)
                {
                    if (owners[r, c] != (r, c)) continue;
                    var cell = t.Rows[r].Cells[c];
                    var rect = layout.GetFixedCell(r, c);
                    string fill = cell.BackgroundColor != null ? Styles.ColorCode(cell.BackgroundColor) : "null";
                    string text = ParagraphCell(cell.Text, null, null) ?? TextExpression(cell.Text, null, null);
                    body.AppendLine(Cell(text, Code.Mm(rect.X), Code.Mm(rect.Y), Code.Mm(rect.Width), Code.Mm(rect.Height), cell, c, fill));
                }
            }

            if (t.HasDetail)
            {
                // 全ページ共通の部品の表では、行をくり返さない（ReportRenderer と同じ）
                var list = _inCommon ? null : _report.FindList(t.DetailSource);
                int n = t.DetailRowCount;
                double h = t.DetailRow.Height;
                body.AppendLine("for (int row = 0; row < " + n + "; row++)");
                body.AppendLine("{");
                if (list != null)
                {
                    var info = _lists[list];
                    body.AppendLine("    int index = pageIndex * " + n + " + row;");
                    body.AppendLine("    var item = index < " + info.Local + ".Count ? " + info.Local + "[index] : null;");
                }
                body.AppendLine("    double y = " + "Unit.Mm(" + Code.Number(layout.DetailTop) + " + row * " + Code.Number(h) + ");");
                bool usesRowFill = t.AlternateRowColor != null && layout.DetailColumns.Any(c => t.DetailRow.Cells[c].BackgroundColor == null);
                if (usesRowFill)
                    body.AppendLine("    PdfColor? rowFill = row % 2 == 1 ? (PdfColor?)" + Styles.ColorCode(t.AlternateRowColor!) + " : null; // 1 行おきの背景色");
                foreach (int c in layout.DetailColumns)
                {
                    var cell = t.DetailRow.Cells[c];
                    var span = layout.GetDetailCell(c);
                    string fill = cell.BackgroundColor != null ? Styles.ColorCode(cell.BackgroundColor)
                        : t.AlternateRowColor != null ? "rowFill"
                        : "null";
                    string text = "\"\"";
                    if (list != null && ParagraphCell(cell.Text, list, "item") is string paragraph)
                    {
                        // データのない行は、文字のないセルとして描く
                        body.AppendLine("    if (item == null) " + Cell(text, Code.Mm(span.X), "y", Code.Mm(span.Width), Code.Mm(h), cell, c, fill));
                        body.AppendLine("    else " + Cell(paragraph, Code.Mm(span.X), "y", Code.Mm(span.Width), Code.Mm(h), cell, c, fill));
                        continue;
                    }
                    if (list != null)
                    {
                        string value = TextExpression(cell.Text, list, "item");
                        if (value != "\"\"") text = "item == null ? \"\" : " + value;
                    }
                    body.AppendLine("    " + Cell(text, Code.Mm(span.X), "y", Code.Mm(span.Width), Code.Mm(h), cell, c, fill));
                }
                body.AppendLine("}");
            }

            // 変数の宣言、セルの順に書く（ブロックで囲み、ほかの表と変数名が重ならないようにする）
            Line("{");
            _indent++;
            if (pen != null) Line("var pen = " + pen + ";");
            Line("double padding = " + Code.Mm(t.CellPadding) + ";");
            foreach (var pair in styleVars) Line("var " + pair.Value + " = " + pair.Key + ";");
            _sb.Append(Indent(body.ToString(), _indent));
            _indent--;
            Line("}");
        }

        // ---- 差し込み欄 ----

        /// <summary>差し込み欄を含む文字列を、C# の式にする（ReportRenderer.Expand と同じ規則）。</summary>
        private string TextExpression(string template, ListDefinition? list, string? itemVar)
        {
            var parts = new List<string>();
            bool singleNullableString = false;
            foreach (var segment in TextTemplate.Parse(template))
            {
                if (!segment.IsField)
                {
                    parts.Add(Code.String(segment.Text));
                    continue;
                }
                string name = segment.Text;
                var column = list?.FindColumn(name);
                if (column != null && itemVar != null)
                {
                    string prop = itemVar + "." + _lists[list!].Columns[column];
                    parts.Add(FormatExpression(prop, column.Kind, TextTemplate.EffectiveFormat(column.Kind, segment.Format, column.Format)));
                    singleNullableString = column.Kind == FieldKind.Text;
                    continue;
                }
                var field = _report.FindField(name);
                if (field != null && _computeNames.TryGetValue(field, out string? compute))
                {
                    parts.Add(FormatExpression(compute + "(data, now)", field.Kind, TextTemplate.EffectiveFormat(field.Kind, segment.Format, field.Format)));
                    continue;
                }
                if (field != null)
                {
                    string prop = "data." + _fieldNames[field];
                    parts.Add(FormatExpression(prop, field.Kind, TextTemplate.EffectiveFormat(field.Kind, segment.Format, field.Format)));
                    singleNullableString = field.Kind == FieldKind.Text;
                    continue;
                }
                if (TextTemplate.FindBuiltIn(name) is BuiltInField builtIn)
                {
                    parts.Add(BuiltInExpression(builtIn, segment.Format));
                    continue;
                }
                parts.Add(Code.String(TextTemplate.ToDisplay(segment)));
            }
            if (parts.Count == 0) return "\"\"";
            // 文字列の項目 1 つだけなら、null のときに空文字列にする（連結なら null は空文字列として扱われる）
            if (parts.Count == 1 && singleNullableString) return "(" + parts[0] + " ?? \"\")";
            return string.Join(" + ", parts);
        }

        // ---- C# の式で計算する差し込み項目 ----

        /// <summary>
        /// デザイナーの中で式を計算するためのコード（データのクラスと、計算する public なメソッドを持つ ScriptHost クラス）。
        /// methods は、項目の名前から ScriptHost のメソッドの名前を引く。
        /// </summary>
        public static (string Code, Dictionary<string, string> Methods) GenerateScriptHost(ReportDefinition report)
        {
            var g = new CSharpGenerator(report, new CodeGenOptions { Namespace = "", IncludeSampleData = true }) { _lineDirectives = true };
            g.AssignNames();
            g.Line("using System;");
            g.Line("using System.Collections.Generic;");
            g.Line("using System.Globalization;");
            g.Line("using System.Linq;");
            g.Line();
            g.WriteDataClass();
            foreach (var list in g.UsedLists())
            {
                g.Line();
                g.WriteItemClass(list);
            }
            g.Line();
            g.Line("public static class ScriptHost");
            g.Open();
            g.Line("private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo(\"ja-JP\");");
            g.WriteComputeMethods(true);
            g.Close();
            return (g._sb.ToString(), g._computeNames.ToDictionary(p => p.Key.Name, p => p.Value));
        }

        /// <summary>計算する差し込み項目ごとに、値を求めるメソッドを書く。</summary>
        private void WriteComputeMethods(bool isPublic)
        {
            foreach (var pair in _computeNames)
            {
                var field = pair.Key;
                string type = field.Kind == FieldKind.Number ? "decimal" : field.Kind == FieldKind.Date ? "DateTime" : "string";
                Line();
                Line("/// <summary>差し込み項目「" + XmlText(field.Name) + "」を計算する（デザイナーで入力した C# の式）。</summary>");
                Line((isPublic ? "public" : "private") + " static " + type + " " + pair.Value + "(" + _dataClass + " data, DateTime now)");
                Open();
                Line("object Value()");
                Open();
                string script = (field.Script ?? "").Replace("\r\n", "\n").TrimEnd();
                if (_lineDirectives) _sb.Append("#line 1 \"" + field.Name.Replace("\"", "") + "\"\r\n");
                if (script.Trim().Length == 0)
                {
                    Line(field.Kind == FieldKind.Number ? "return 0;" : field.Kind == FieldKind.Date ? "return default(DateTime);" : "return \"\";");
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(script, @"\breturn\b"))
                {
                    // return のある文は、そのまま書く
                    foreach (string line in script.Split('\n')) Line(line.TrimEnd());
                }
                else
                {
                    // 式は、その値を返す
                    string[] lines = script.TrimEnd(';', ' ', '\t', '\n').Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                        Line((i == 0 ? "return (" : "    ") + lines[i].TrimEnd() + (i == lines.Length - 1 ? ");" : ""));
                }
                if (_lineDirectives) _sb.Append("#line default\r\n");
                Close();
                Line(field.Kind == FieldKind.Number ? "return Convert.ToDecimal(Value(), Culture);"
                    : field.Kind == FieldKind.Date ? "return Convert.ToDateTime(Value(), Culture);"
                    : "return Convert.ToString(Value(), Culture) ?? \"\";");
                Close();
            }
        }

        /// <summary>組み込みの差し込み欄の式（TextTemplate.FormatBuiltIn と同じ規則）。</summary>
        private string BuiltInExpression(BuiltInField builtIn, string? segmentFormat)
        {
            string? format = string.IsNullOrEmpty(segmentFormat) ? builtIn.DefaultFormat : segmentFormat;
            if (builtIn.NowExpression is string nowExpr)
            {
                if (builtIn.Kind == FieldKind.Date) return FormatExpression(nowExpr, FieldKind.Date, format);
                return IntToString(nowExpr, format);
            }
            bool isNumber = builtIn.Source == BuiltInSource.PageNumber;
            string expr;
            if (_inCommon)
            {
                // DrawCommon の引数
                expr = isNumber ? "pageNumber" : "pageCount";
            }
            else if (_pageNumbersAreConstant)
            {
                // どのページも増えないなら、ページ番号は決まっている
                int n = int.Parse(isNumber ? _pageNumberExpr : _pageCountExpr, CultureInfo.InvariantCulture);
                return Code.String(TextTemplate.FormatBuiltIn(builtIn, segmentFormat, default, n, n));
            }
            else
            {
                expr = isNumber ? _pageNumberExpr : _pageCountExpr;
                if (isNumber && expr == "pageNumber") _usesPageNumberVariable = true;
                if (!isNumber) _usesPageCountVariable = true;
            }
            return IntToString(expr, format);
        }

        private static string IntToString(string expr, string? format)
        {
            // ローマ数字・漢数字などは、ChohyoPdf の PageNumberFormatter で文字列にする
            if (TextTemplate.NumberStyleOf(format) is PageNumberFormat style) return "PageNumberFormatter.Format(" + expr + ", PageNumberFormat." + style + ")";
            return expr + (format == null ? ".ToString(Culture)" : ".ToString(" + Code.String(format) + ", Culture)");
        }

        /// <summary>
        /// どこかの差し込み欄で、PDF を作る日時（{今日} など）を使うか。項目・一覧の列と同じ名前なら、そちらを使うので数えない。
        /// </summary>
        private bool UsesNow()
        {
            bool Check(string text, ListDefinition? list)
            {
                foreach (var segment in TextTemplate.Parse(text))
                {
                    if (!segment.IsField || list?.FindColumn(segment.Text) != null || _report.FindField(segment.Text) != null) continue;
                    if (TextTemplate.FindBuiltIn(segment.Text)?.UsesNow == true) return true;
                }
                return false;
            }
            foreach (var element in _report.AllElements())
            {
                switch (element)
                {
                    case TextElement text when Check(text.Text, null):
                        return true;
                    case TableElement table:
                        if (table.Rows.Any(r => r.Cells.Any(c => Check(c.Text, null)))) return true;
                        if (table.HasDetail && table.DetailRow.Cells.Any(c => Check(c.Text, _report.FindList(table.DetailSource)))) return true;
                        break;
                }
            }
            return false;
        }

        private string FormatExpression(string expr, FieldKind kind, string? format)
        {
            if (kind == FieldKind.Date && TextTemplate.IsJapaneseEraFormat(format))
            {
                _usesJapaneseDate = true;
                return "FormatJapaneseDate(" + expr + ", " + Code.String(format!) + ")";
            }
            return FormatExpressionCore(expr, kind, format);
        }

        private static string FormatExpressionCore(string expr, FieldKind kind, string? format)
            => kind == FieldKind.Text ? expr : expr + ".ToString(" + Code.String(format ?? "") + ", Culture)";

        // ---- フォント ----

        private string Font(string? fontName)
        {
            string name = fontName ?? _report.DefaultFontName;
            string? bold = _report.GetBoldFontName(name);
            var font = _fonts.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase) && f.Bold == bold);
            if (font == null)
            {
                font = new FontInfo { Name = name, Bold = bold, Property = UniqueMember("Font" + Identifiers.PascalAscii(name, (_fonts.Count + 1).ToString(CultureInfo.InvariantCulture))) };
                _fonts.Add(font);
            }
            return font.Property + ".Value";
        }

        private readonly NameScope _members = new NameScope("Culture", "JapaneseCulture", "CreateJapaneseCulture", "FormatJapaneseDate",
            "ImageFolder", "Save", "Create", "DrawCommon", "DrawCell", "DrawCellText", "LoadFont");

        private string UniqueMember(string name) => _members.Reserve(name);

        // ---- 出力の補助 ----

        private static string Label(ReportElement e) => e.KindName + "「" + (e.Name.Length > 0 ? e.Name : "名前なし") + "」";

        private static string Comment(string text) => text.Replace("\r", " ").Replace("\n", " ");

        private static string XmlText(string text) => Comment(text).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private void Line(string text = "")
        {
            if (text.Length > 0) _sb.Append(' ', _indent * 4).Append(text);
            _sb.Append("\r\n");
        }

        private void Open()
        {
            Line("{");
            _indent++;
        }

        private void Close()
        {
            _indent--;
            Line("}");
        }

        /// <summary>action が書いた内容を、指定のインデントで文字列として受け取る（本体には書かない）。</summary>
        private string Capture(int indent, Action action)
        {
            int start = _sb.Length, savedIndent = _indent;
            _indent = indent;
            action();
            string text = _sb.ToString(start, _sb.Length - start);
            _sb.Length = start;
            _indent = savedIndent;
            return text;
        }

        private static string Indent(string text, int indent)
        {
            if (indent == 0 || text.Length == 0) return NormalizeNewLines(text);
            var sb = new StringBuilder();
            foreach (string line in NormalizeNewLines(text).Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (sb.Length > 0) sb.Append("\r\n");
                if (line.Length > 0) sb.Append(' ', indent * 4);
                sb.Append(line);
            }
            return sb.ToString();
        }

        private static string NormalizeNewLines(string text) => text.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }
}
