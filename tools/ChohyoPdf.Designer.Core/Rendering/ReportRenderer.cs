using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Rendering
{
    /// <summary>描き方。</summary>
    public enum RenderMode
    {
        /// <summary>デザイン画面用。差し込み欄はそのまま表示し、デザインの 1 ページを PDF の 1 ページに描く。見つからないフォントや画像は飛ばす。</summary>
        Design,

        /// <summary>データを差し込んで PDF を作る。くり返し行の件数が多ければ、そのページを増やす。</summary>
        Data,

        /// <summary>
        /// デザイン画面で差し込んだ結果を表示する。データを差し込み、デザインの各ページの最初の 1 ページだけを描く。
        /// 表示するページの設定にかかわらずすべての部品を描き、見つからないフォントや画像は飛ばす。
        /// </summary>
        Preview,
    }

    /// <summary>デザイン画面で、編集していない方の部品を薄く描く（Word でヘッダーを編集しているときに本文を薄く表示するように）。</summary>
    public enum DesignLayer
    {
        /// <summary>どちらも普通に描く（ページの縮小画像など）。</summary>
        None,

        /// <summary>ページの部品を編集している。全ページ共通の部品を薄く描く。</summary>
        Page,

        /// <summary>全ページ共通の部品を編集している。ページの部品を薄く描き、共通の部品はページの設定にかかわらずすべて描く。</summary>
        Common,
    }

    /// <summary>
    /// デザインから PDF を作る。<see cref="CodeGen.CSharpGenerator"/> が出力するコードと同じ順序・同じ計算で描くので、
    /// 同じデータからは同じ PDF ができる（変更するときは両方を合わせること）。
    /// </summary>
    public sealed class ReportRenderer
    {
        // PdfFont はスレッドセーフではないため、描画は 1 つずつ行う
        private static readonly object Sync = new object();

        private readonly ReportDefinition _report;
        private readonly RenderMode _mode;
        private readonly ReportData _data;
        private readonly string? _baseDirectory;
        private readonly ICollection<string>? _warnings;
        private readonly Dictionary<string, PdfImage> _images = new Dictionary<string, PdfImage>(StringComparer.OrdinalIgnoreCase);
        private readonly int? _onlyPage;
        private readonly DesignLayer _layer;
        // 部品を動かしている間の画像（RenderLayer）で、描く部品を選ぶ。null ならすべて描く
        private Func<ReportElement, bool>? _filter;
        // 下の画像に重ねる画像では、薄くするための白を重ねない（背景を透明にしておく）
        private bool _noDim;
        private PdfGraphics _g = null!;
        // 全ページ共通の部品を描いている（共通の部品の表では、行をくり返さない）
        private bool _inCommon;
        // _pageIndex はデザインの 1 ページが増えた中での番号（0 から）。_pageNumber と _pageCount は文書全体のページ番号と総ページ数
        private int _pageIndex, _pageNumber, _pageCount;

        private ReportRenderer(ReportDefinition report, RenderMode mode, ReportData? data, string? baseDirectory, ICollection<string>? warnings, int? onlyPage, DesignLayer layer)
        {
            _layer = layer;
            _report = report;
            _mode = mode;
            _data = data ?? new ReportData();
            _baseDirectory = baseDirectory;
            _warnings = warnings;
            _onlyPage = onlyPage;
        }

        /// <summary>PDF を作る。</summary>
        /// <param name="baseDirectory">画像の相対パスの基準のフォルダ（デザインファイルのフォルダ）。</param>
        /// <param name="warnings">問題があれば、その説明を追加する。</param>
        /// <param name="pageIndex">
        /// デザイン画面と差し込み結果の表示（<see cref="RenderMode.Design"/>、<see cref="RenderMode.Preview"/>）で、このページ（0 から）だけを描く。
        /// null ならすべてのページを描く。<see cref="RenderMode.Data"/> では使わない。
        /// </param>
        /// <param name="layer">デザイン画面で、編集していない方の部品を薄く描く。<see cref="RenderMode.Data"/> では使わない。</param>
        public static PdfDocument Render(ReportDefinition report, RenderMode mode, ReportData? data = null, string? baseDirectory = null, ICollection<string>? warnings = null,
            int? pageIndex = null, DesignLayer layer = DesignLayer.None)
        {
            bool forPdf = mode == RenderMode.Data;
            lock (Sync) return new ReportRenderer(report, mode, data, baseDirectory, warnings, forPdf ? null : pageIndex, forPdf ? DesignLayer.None : layer).Render();
        }

        /// <summary>
        /// デザイン画面で部品を動かしている間に表示する画像の PDF を作る（ページ <paramref name="pageIndex"/> の、<paramref name="include"/> を満たす部品だけを描く）。
        /// 部品を、動かすもの・その奥・その手前に分けて描き、動かすものの画像をずらして重ねる。
        /// </summary>
        /// <param name="overlay">下の画像に重ねる。全ページ共通の部品やページの部品を薄くするための白を重ねない。</param>
        public static PdfDocument RenderLayer(ReportDefinition report, RenderMode mode, ReportData? data, string? baseDirectory, int pageIndex, DesignLayer layer,
            Func<ReportElement, bool> include, bool overlay)
        {
            if (mode == RenderMode.Data) throw new ArgumentException("デザイン画面の描き方を指定してください。", nameof(mode));
            lock (Sync)
            {
                var renderer = new ReportRenderer(report, mode, data, baseDirectory, null, pageIndex, layer) { _filter = include, _noDim = overlay };
                return renderer.Render();
            }
        }

        /// <summary>薄く描くために、上に重ねる白の不透明度。</summary>
        private const double DimAlpha = 0.5;

        /// <summary>サンプルの値を差し込んだ PDF を作る。</summary>
        /// <param name="now">{今日} などに使う日時。null なら今の日時。</param>
        /// <param name="runScripts">差し込み項目の C# の式を実行する（<see cref="ReportData.FromSamples"/>）。</param>
        public static PdfDocument RenderSample(ReportDefinition report, string? baseDirectory = null, ICollection<string>? warnings = null, DateTime? now = null, bool runScripts = false)
            => Render(report, RenderMode.Data, ReportData.FromSamples(report, warnings, now, runScripts), baseDirectory, warnings);

        private PdfDocument Render()
        {
            var pdf = new PdfDocument();
            if (_report.Name.Length > 0) pdf.Info.Title = _report.Name;
            // 総ページ数を先に求める（{総ページ数} は最初のページから使う）
            var counts = new List<int>();
            foreach (var page in _report.Pages) counts.Add(_mode == RenderMode.Design ? 1 : CountPages(page));
            _pageCount = 0;
            foreach (int count in counts) _pageCount += count;
            int first = 1;
            for (int p = 0; p < _report.Pages.Count; first += counts[p], p++)
            {
                if (_onlyPage != null && _onlyPage != p) continue;
                var page = _report.Pages[p];
                // 共通の部品を編集しているときは、文書の用紙に描く（共通の部品は文書の用紙で配置する）
                var size = GetPageSize(_report, _layer == DesignLayer.Common ? null : page);
                int drawCount = _mode == RenderMode.Data ? counts[p] : 1;
                for (_pageIndex = 0; _pageIndex < drawCount; _pageIndex++)
                {
                    _pageNumber = first + _pageIndex;
                    var pdfPage = pdf.AddPage(size);
                    _g = pdfPage.Graphics;
                    if (_layer == DesignLayer.Common)
                    {
                        DrawElements(page.Elements, null);
                        Dim(pdfPage);
                        DrawCommon(true);
                        continue;
                    }
                    if (!page.HideCommon && DrawCommon(false) && _layer == DesignLayer.Page) Dim(pdfPage);
                    DrawElements(page.Elements, counts[p]);
                }
            }
            return pdf;
        }

        /// <summary>全ページ共通の部品を描く。all なら、描くページの設定にかかわらずすべて描く。何か描いたら true。</summary>
        private bool DrawCommon(bool all)
        {
            var visible = _report.Common.Elements.Where(e => all || IsVisibleInDocument(e.Visibility, _pageNumber, _pageCount)).ToList();
            _inCommon = true;
            try
            {
                DrawElements(visible, null);
            }
            finally
            {
                _inCommon = false;
            }
            return visible.Count > 0;
        }

        /// <summary>部品を描く。groupCount はページが増えた数（PDF を作るときだけ、描くページの設定に使う。null なら設定を見ない）。</summary>
        private void DrawElements(IEnumerable<ReportElement> elements, int? groupCount)
        {
            foreach (var element in elements)
            {
                if (_filter != null && !_filter(element)) continue;
                if (_mode == RenderMode.Data && groupCount != null && !IsVisible(element.Visibility, _pageIndex, groupCount.Value)) continue;
                if (_mode == RenderMode.Data)
                {
                    DrawElement(element);
                    continue;
                }
                try
                {
                    DrawElement(element);
                }
                catch (Exception ex) when (ex is IOException || ex is FormatException || ex is NotSupportedException || ex is InvalidDataException || ex is ArgumentException || ex is InvalidOperationException)
                {
                    Warn(Label(element) + ": " + ex.Message);
                }
            }
        }

        /// <summary>ここまでに描いたものを、白を重ねて薄くする。</summary>
        private void Dim(PdfPage page)
        {
            if (!_noDim) _g.FillRectangle(PdfColor.White.WithAlpha(DimAlpha), 0, 0, page.Width, page.Height);
        }

        /// <summary>くり返し行の件数から、デザインの 1 ページが PDF の何ページになるかを求める。</summary>
        private int CountPages(ReportPage page)
        {
            int pageCount = 1;
            foreach (var element in page.Elements)
            {
                if (element is TableElement table && table.HasDetail && _report.FindList(table.DetailSource) != null)
                {
                    int count = GetItems(table.DetailSource!).Count;
                    pageCount = Math.Max(pageCount, (count + table.DetailRowCount - 1) / table.DetailRowCount);
                }
            }
            return pageCount;
        }

        internal static bool IsVisible(PageVisibility visibility, int pageIndex, int pageCount)
            => visibility == PageVisibility.AllPages
            || (visibility == PageVisibility.FirstPage && pageIndex == 0)
            || (visibility == PageVisibility.LastPage && pageIndex == pageCount - 1)
            || (visibility == PageVisibility.ExceptFirstPage && pageIndex != 0);

        /// <summary>全ページ共通の部品を、文書の pageNumber ページ目（1 から）に描くか。</summary>
        public static bool IsVisibleInDocument(PageVisibility visibility, int pageNumber, int pageCount)
            => IsVisible(visibility, pageNumber - 1, pageCount);

        /// <summary>文書の用紙の大きさ。定義済みの用紙は <see cref="PageSize"/> の値をそのまま使う。</summary>
        public static PageSize GetPageSize(ReportDefinition report) => GetPageSize(report, null);

        /// <summary>ページの用紙の大きさ（ページが用紙を指定していなければ、文書の用紙）。</summary>
        public static PageSize GetPageSize(ReportDefinition report, ReportPage? page)
        {
            var setting = page?.Paper;
            string name = setting?.PaperName ?? report.PaperName;
            bool landscape = setting?.Landscape ?? report.Landscape;
            var paper = PaperSizes.Find(name);
            if (paper == null) return PageSize.FromMm(report.PageWidthOf(page), report.PageHeightOf(page));
            var size = paper.PageSizeProperty switch
            {
                "A3" => PageSize.A3,
                "A4" => PageSize.A4,
                "A5" => PageSize.A5,
                "A6" => PageSize.A6,
                "B4" => PageSize.B4,
                "B5" => PageSize.B5,
                "Postcard" => PageSize.Postcard,
                "EnvelopeChou3" => PageSize.EnvelopeChou3,
                "Letter" => PageSize.Letter,
                "Legal" => PageSize.Legal,
                _ => PageSize.FromMm(paper.Width, paper.Height),
            };
            return landscape ? size.Landscape : size;
        }

        private void DrawElement(ReportElement element)
        {
            switch (element)
            {
                case TextElement text: DrawText(text); break;
                case LineElement line: DrawLine(line); break;
                case RectangleElement rect: DrawRectangle(rect); break;
                case EllipseElement ellipse: DrawEllipse(ellipse); break;
                case ImageElement image: DrawImage(image); break;
                case TableElement table: DrawTable(table); break;
            }
        }

        // ---- 部品ごとの描画（CSharpGenerator の出力と同じ処理） ----

        private void DrawText(TextElement e)
        {
            var pen = Styles.Pen(e.BorderColor, e.BorderWidth, e.BorderDash);
            PdfColor? fill = e.BackgroundColor != null ? Styles.Color(e.BackgroundColor) : (PdfColor?)null;
            if (pen != null || fill != null)
                _g.DrawRectangle(pen, fill, Unit.Mm(e.X), Unit.Mm(e.Y), Unit.Mm(e.Width), Unit.Mm(e.Height));

            // 差し込んだ結果が空でも DrawTextBox を呼ぶ（生成コードと同じ）。入力そのものが空なら呼ばない
            var box = Styles.TextBoxRect(e);
            if (e.Text.Length == 0 || box == null) return;
            var font = GetFont(e.FontName);
            if (font == null) return;
            // 箇条書きや、上付き・下付き・ルビの記法があるときは、段落・箇条書きとして描く
            if (e.IsRich)
            {
                _g.DrawBlocks(RichText.Build(e, t => Expand(t, null, null)), box.Value, Styles.TextStyle(e, font), Styles.TextBoxOptions(e));
                return;
            }
            _g.DrawTextBox(Expand(e.Text, null, null), box.Value, Styles.TextStyle(e, font), Styles.TextBoxOptions(e));
        }

        private void DrawLine(LineElement e)
        {
            var pen = Styles.Pen(e.StrokeColor, e.StrokeWidth, e.Dash);
            if (pen == null) return;
            _g.DrawLine(pen, Unit.Mm(e.X1), Unit.Mm(e.Y1), Unit.Mm(e.X2), Unit.Mm(e.Y2));
        }

        private void DrawRectangle(RectangleElement e)
        {
            var pen = Styles.Pen(e.StrokeColor, e.StrokeWidth, e.Dash);
            PdfColor? fill = e.FillColor != null ? Styles.Color(e.FillColor) : (PdfColor?)null;
            if (pen == null && fill == null) return;
            if (e.CornerRadius > 0)
                _g.DrawRoundedRectangle(pen, fill, Unit.Mm(e.X), Unit.Mm(e.Y), Unit.Mm(e.Width), Unit.Mm(e.Height), Unit.Mm(e.CornerRadius));
            else
                _g.DrawRectangle(pen, fill, Unit.Mm(e.X), Unit.Mm(e.Y), Unit.Mm(e.Width), Unit.Mm(e.Height));
        }

        private void DrawEllipse(EllipseElement e)
        {
            var pen = Styles.Pen(e.StrokeColor, e.StrokeWidth, e.Dash);
            PdfColor? fill = e.FillColor != null ? Styles.Color(e.FillColor) : (PdfColor?)null;
            if (pen == null && fill == null) return;
            _g.DrawEllipse(pen, fill, Unit.Mm(e.X), Unit.Mm(e.Y), Unit.Mm(e.Width), Unit.Mm(e.Height));
        }

        private void DrawImage(ImageElement e)
        {
            if (e.Path.Length == 0)
            {
                if (_mode != RenderMode.Data) DrawMissingImage(e, "画像なし");
                return;
            }
            string path = ResolvePath(e.Path, _baseDirectory);
            if (!_images.TryGetValue(path, out var image))
            {
                if (!File.Exists(path))
                {
                    if (_mode == RenderMode.Data) throw new FileNotFoundException("画像ファイルが見つかりません: " + path, path);
                    Warn(Label(e) + ": 画像ファイルが見つかりません（" + path + "）。");
                    DrawMissingImage(e, "画像が見つかりません");
                    return;
                }
                image = PdfImage.FromFile(path);
                _images[path] = image;
            }
            if (e.Fit == ImageFit.Stretch)
                _g.DrawImage(image, Unit.Mm(e.X), Unit.Mm(e.Y), Unit.Mm(e.Width), Unit.Mm(e.Height));
            else
                _g.DrawImageFit(image, Unit.Mm(e.X), Unit.Mm(e.Y), Unit.Mm(e.Width), Unit.Mm(e.Height));
        }

        /// <summary>画像の相対パスを、基準のフォルダからのパスにする（生成コードの Path.Combine と同じ）。</summary>
        public static string ResolvePath(string path, string? baseDirectory)
            => Path.Combine(baseDirectory ?? Directory.GetCurrentDirectory(), path);

        private void DrawMissingImage(ImageElement e, string label)
        {
            double x = Unit.Mm(e.X), y = Unit.Mm(e.Y), w = Unit.Mm(e.Width), h = Unit.Mm(e.Height);
            var pen = new PdfPen(PdfColor.Gray, 0.5);
            _g.DrawRectangle(pen, PdfColor.FromGray(0.95), x, y, w, h);
            _g.DrawLine(pen, x, y, x + w, y + h);
            _g.DrawLine(pen, x + w, y, x, y + h);
            var font = GetFont(null);
            if (font != null)
                _g.DrawTextBox(label, new PdfRect(x, y, w, h), new TextStyle(font, 8) { Color = PdfColor.DarkGray },
                    new TextBoxOptions { Alignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Middle, ShrinkToFit = true });
        }

        private void DrawTable(TableElement t)
        {
            var font = GetFont(t.FontName);
            if (font == null) return;
            var pen = Styles.Pen(t.BorderColor, t.BorderWidth, DashStyle.Solid);
            double padding = Unit.Mm(t.CellPadding);
            var layout = new TableLayout(t);
            var owners = t.GetOwnerMap();

            for (int r = 0; r < t.Rows.Count; r++)
            {
                for (int c = 0; c < t.Columns.Count; c++)
                {
                    if (owners[r, c] != (r, c)) continue;
                    var cell = t.Rows[r].Cells[c];
                    var rect = layout.GetFixedCell(r, c);
                    PdfColor? fill = cell.BackgroundColor != null ? Styles.Color(cell.BackgroundColor) : (PdfColor?)null;
                    DrawCell(_g, CellContent(cell.Text, null, null), Unit.Mm(rect.X), Unit.Mm(rect.Y), Unit.Mm(rect.Width), Unit.Mm(rect.Height),
                        Styles.CellStyle(t, cell, font), Styles.CellOptions(t, cell, c), pen, fill, padding);
                }
            }

            if (!t.HasDetail) return;
            // 全ページ共通の部品の表では、行をくり返さない（くり返し行は空の行として描く）
            var list = _inCommon ? null : _report.FindList(t.DetailSource);
            if (list == null && !_inCommon) Warn(Label(t) + ": くり返す一覧「" + t.DetailSource + "」がありません。");
            var items = list != null ? GetItems(list.Name) : new List<Dictionary<string, object?>>();
            int n = t.DetailRowCount;
            double h = t.DetailRow.Height;
            for (int row = 0; row < n; row++)
            {
                int index = _pageIndex * n + row;
                var item = index < items.Count ? items[index] : null;
                double y = Unit.Mm(layout.DetailTop + row * h);
                foreach (int c in layout.DetailColumns)
                {
                    var cell = t.DetailRow.Cells[c];
                    var span = layout.GetDetailCell(c);
                    PdfColor? fill = cell.BackgroundColor != null ? Styles.Color(cell.BackgroundColor)
                        : t.AlternateRowColor != null && row % 2 == 1 ? Styles.Color(t.AlternateRowColor) : (PdfColor?)null;
                    object content = _mode == RenderMode.Design ? (row == 0 ? CellContent(cell.Text, null, null) : "")
                        : item == null ? "" : CellContent(cell.Text, list, item);
                    DrawCell(_g, content, Unit.Mm(span.X), y, Unit.Mm(span.Width), Unit.Mm(h),
                        Styles.CellStyle(t, cell, font), Styles.CellOptions(t, cell, c), pen, fill, padding);
                }
            }
        }

        /// <summary>セルの中身。上付き・下付き・ルビの記法があれば段落（Paragraph）、なければ差し込んだ文字列。</summary>
        private object CellContent(string template, ListDefinition? list, Dictionary<string, object?>? item)
            => TextMarkup.HasMarkup(template) ? RichText.AddRuns(new Paragraph(), TextMarkup.Parse(template), t => Expand(t, list, item)) : (object)Expand(template, list, item);

        /// <summary>セルを描く。content は文字列か段落。生成コードの DrawCell と同じ処理。</summary>
        internal static void DrawCell(PdfGraphics g, object content, double x, double y, double width, double height,
            TextStyle style, TextBoxOptions options, PdfPen? pen, PdfColor? fill, double padding)
        {
            if (pen != null || fill != null) g.DrawRectangle(pen, fill, x, y, width, height);
            if (content is string text && text.Length == 0) return;
            double w = width - padding * 2, h = height - padding * 2;
            if (w <= 0 || h <= 0) return;
            var rect = new PdfRect(x + padding, y + padding, w, h);
            if (content is Paragraph paragraph) g.DrawBlocks(paragraph, rect, style, options);
            else g.DrawTextBox((string)content, rect, style, options);
        }

        // ---- 差し込み ----

        private List<Dictionary<string, object?>> GetItems(string listName)
            => _data.Lists.TryGetValue(listName, out var items) ? items : new List<Dictionary<string, object?>>();

        /// <summary>差し込み欄に値を入れる。デザイン画面では入力した文字列のまま。</summary>
        private string Expand(string template, ListDefinition? list, Dictionary<string, object?>? item)
        {
            if (_mode == RenderMode.Design) return template;
            var sb = new StringBuilder();
            foreach (var segment in TextTemplate.Parse(template))
            {
                if (!segment.IsField)
                {
                    sb.Append(segment.Text);
                    continue;
                }
                sb.Append(Resolve(segment, list, item));
            }
            return sb.ToString();
        }

        /// <summary>差し込み欄の値。一覧の列、項目、組み込みの差し込み欄（ページ番号・今日など）の順に名前を探す。</summary>
        private string Resolve(TemplateSegment segment, ListDefinition? list, Dictionary<string, object?>? item)
        {
            string name = segment.Text;
            var column = list?.FindColumn(name);
            if (column != null)
            {
                item!.TryGetValue(name, out object? value);
                return TextTemplate.FormatValue(value, column.Kind, TextTemplate.EffectiveFormat(column.Kind, segment.Format, column.Format));
            }
            var field = _report.FindField(name);
            if (field != null)
            {
                // 計算する項目で値がない（C# の式を実行していない・エラー）ときは、0 などを描かずに空にする
                if (!_data.Values.TryGetValue(name, out object? value) && field.IsComputed) return "";
                return TextTemplate.FormatValue(value, field.Kind, TextTemplate.EffectiveFormat(field.Kind, segment.Format, field.Format));
            }
            if (TextTemplate.FindBuiltIn(name) is BuiltInField builtIn) return TextTemplate.FormatBuiltIn(builtIn, segment.Format, _data.Now, _pageNumber, _pageCount);
            Warn("差し込み欄 {" + name + "} に対応する差し込み項目がありません。");
            return TextTemplate.ToDisplay(segment);
        }

        // ---- その他 ----

        private PdfFont? GetFont(string? fontName)
        {
            string name = fontName ?? _report.DefaultFontName;
            try
            {
                return FontProvider.Get(name, _report.GetBoldFontName(name));
            }
            catch (Exception ex) when (_mode != RenderMode.Data && (ex is IOException || ex is NotSupportedException || ex is InvalidDataException || ex is ArgumentException))
            {
                Warn(ex.Message);
                if (fontName != null) return GetFont(null);
                return null;
            }
        }

        private void Warn(string message)
        {
            if (_warnings != null && !_warnings.Contains(message)) _warnings.Add(message);
        }

        private static string Label(ReportElement e) => e.Name.Length > 0 ? e.Name : e.KindName;
    }

    /// <summary>表のセルの位置（mm）。生成コードと同じ順序で足し算する。</summary>
    internal sealed class TableLayout
    {
        private readonly TableElement _table;
        private readonly double[] _columnX;
        private readonly double[] _rowY;

        public TableLayout(TableElement table)
        {
            _table = table;
            _columnX = new double[table.Columns.Count + 1];
            _columnX[0] = table.X;
            for (int c = 0; c < table.Columns.Count; c++) _columnX[c + 1] = _columnX[c] + table.Columns[c].Width;
            _rowY = new double[table.Rows.Count + 1];
            _rowY[0] = table.Y;
            for (int r = 0; r < table.Rows.Count; r++) _rowY[r + 1] = _rowY[r] + table.Rows[r].Height;
            var columns = new List<int>();
            for (int c = 0; c < table.Columns.Count; c += table.ClampSpan(table.DetailRow.Cells[c].ColumnSpan, c)) columns.Add(c);
            DetailColumns = columns;
        }

        /// <summary>くり返し行の上端。</summary>
        public double DetailTop => _rowY[_table.Rows.Count];

        /// <summary>くり返し行で描くセルの列（結合で隠れる列を除く）。</summary>
        public IReadOnlyList<int> DetailColumns { get; }

        public double ColumnX(int column) => _columnX[column];

        public double RowY(int row) => _rowY[row];

        /// <summary>固定の行のセルの矩形（結合を含む）。</summary>
        public Rect GetFixedCell(int row, int column)
        {
            var cell = _table.Rows[row].Cells[column];
            int cs = _table.ClampSpan(cell.ColumnSpan, column);
            int rs = _table.ClampRowSpan(cell.RowSpan, row);
            return new Rect(_columnX[column], _rowY[row], _columnX[column + cs] - _columnX[column], _rowY[row + rs] - _rowY[row]);
        }

        /// <summary>くり返し行のセルの左端と幅（y と高さは使わない）。</summary>
        public Rect GetDetailCell(int column)
        {
            int cs = _table.ClampSpan(_table.DetailRow.Cells[column].ColumnSpan, column);
            return new Rect(_columnX[column], 0, _columnX[column + cs] - _columnX[column], 0);
        }
    }
}
