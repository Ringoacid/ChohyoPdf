using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>文書のデザイン全体。座標と大きさの単位はミリメートル、文字サイズと線の太さはポイント。</summary>
    public sealed class ReportDefinition : ObservableObject
    {
        /// <summary>保存形式のバージョン（2 からページを持つ）。</summary>
        public const int CurrentFormatVersion = 2;

        private string _name = "文書";
        private string _paperName = PaperSizes.Default.Name;
        private bool _landscape;
        private double _customWidth = 210;
        private double _customHeight = 297;
        private string _defaultFontName = "Yu Gothic Regular";

        public int FormatVersion { get; set; } = CurrentFormatVersion;

        /// <summary>文書の名前。PDF のタイトルと、生成するクラスの名前に使う。</summary>
        public string Name { get => _name; set => Set(ref _name, value ?? ""); }

        /// <summary>用紙の名前（<see cref="PaperSizes"/>）。"カスタム" のときは <see cref="CustomWidth"/> と <see cref="CustomHeight"/> を使う。</summary>
        public string PaperName
        {
            get => _paperName;
            set { if (Set(ref _paperName, value ?? PaperSizes.Default.Name)) OnPageSizeChanged(); }
        }

        /// <summary>横向き。</summary>
        public bool Landscape { get => _landscape; set { if (Set(ref _landscape, value)) OnPageSizeChanged(); } }

        public double CustomWidth { get => _customWidth; set { if (Set(ref _customWidth, value)) OnPageSizeChanged(); } }

        public double CustomHeight { get => _customHeight; set { if (Set(ref _customHeight, value)) OnPageSizeChanged(); } }

        /// <summary>フォントを指定していない部品が使うフォント（<see cref="ChohyoPdf.PdfFont.FromSystem"/> に渡す名前）。</summary>
        public string DefaultFontName { get => _defaultFontName; set => Set(ref _defaultFontName, value ?? ""); }

        /// <summary>フォントごとの太字の対応。</summary>
        public ObservableCollection<FontDefinition> Fonts { get; set; } = new ObservableCollection<FontDefinition>();

        /// <summary>差し込み項目（1 つの値）。</summary>
        public ObservableCollection<FieldDefinition> Fields { get; set; } = new ObservableCollection<FieldDefinition>();

        /// <summary>差し込む一覧（表のくり返し行になるデータ）。</summary>
        public ObservableCollection<ListDefinition> Lists { get; set; } = new ObservableCollection<ListDefinition>();

        /// <summary>ページ。少なくとも 1 ページある。</summary>
        public ObservableCollection<ReportPage> Pages { get; set; } = new ObservableCollection<ReportPage> { new ReportPage() };

        /// <summary>
        /// 全ページ共通の部品（Word のヘッダー・フッターのようなもの）。すべてのページで、ページの部品より先に（背面に）描く。
        /// 位置は用紙の左上からの距離で、用紙の大きさが違うページでも同じ位置に描く。
        /// </summary>
        public ReportPage Common { get; set; } = new ReportPage();

        /// <summary>形式のバージョン 1 のファイルの部品（ページを持たない形式）。読み込むときだけ使い、1 ページ目に移す。</summary>
        [JsonInclude]
        [JsonPropertyName("elements")]
        private List<ReportElement>? LegacyElements { get => null; set => _legacyElements = value; }

        private List<ReportElement>? _legacyElements;

        /// <summary>全ページ共通の部品と、すべてのページの部品（ページの順）。</summary>
        public IEnumerable<ReportElement> AllElements() => Common.Elements.Concat(Pages.SelectMany(p => p.Elements));

        /// <summary>ページの用紙の幅（mm）。ページが用紙を指定していなければ、文書の用紙。</summary>
        public double PageWidthOf(ReportPage? page) => page?.Paper?.PageWidth ?? PageWidth;

        /// <summary>ページの用紙の高さ（mm）。ページが用紙を指定していなければ、文書の用紙。</summary>
        public double PageHeightOf(ReportPage? page) => page?.Paper?.PageHeight ?? PageHeight;

        /// <summary>文書の用紙の設定を写した <see cref="PaperSetting"/>（ページの用紙を変えるときの最初の値）。</summary>
        public PaperSetting CopyPaper() => new PaperSetting { PaperName = PaperName, Landscape = Landscape, CustomWidth = CustomWidth, CustomHeight = CustomHeight };

        /// <summary>読み込んだあとの整理。古い形式の部品を 1 ページ目に移し、ページが 1 つもなければ空のページを足す。</summary>
        internal void Normalize()
        {
            if (Pages.Count == 0) Pages.Add(new ReportPage());
            // "common": null と書いたファイルでも、共通の部品の入れ物は作る
            if ((object?)Common == null) Common = new ReportPage();
            if (_legacyElements != null)
            {
                foreach (var e in _legacyElements) Pages[0].Elements.Add(e);
                _legacyElements = null;
            }
            FormatVersion = CurrentFormatVersion;
        }

        /// <summary>用紙の幅（mm）。横向きを反映した値。</summary>
        [JsonIgnore]
        public double PageWidth => PaperSizes.Width(PaperName, Landscape, CustomWidth, CustomHeight);

        /// <summary>用紙の高さ（mm）。横向きを反映した値。</summary>
        [JsonIgnore]
        public double PageHeight => PaperSizes.Height(PaperName, Landscape, CustomWidth, CustomHeight);

        private void OnPageSizeChanged()
        {
            OnPropertyChanged(nameof(PageWidth));
            OnPropertyChanged(nameof(PageHeight));
        }

        /// <summary>フォント名に対応する太字のフォント名。対応がなければ null（疑似太字になる）。</summary>
        public string? GetBoldFontName(string fontName)
        {
            var def = Fonts.FirstOrDefault(f => string.Equals(f.Name, fontName, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(def?.BoldName) ? null : def!.BoldName;
        }

        public ListDefinition? FindList(string? name)
            => name == null ? null : Lists.FirstOrDefault(l => l.Name == name);

        public FieldDefinition? FindField(string name) => Fields.FirstOrDefault(f => f.Name == name);
    }

    /// <summary>1 ページのデザイン。くり返し行のある表の件数が多ければ、PDF ではこのページが何ページかに増える。</summary>
    public sealed class ReportPage : ObservableObject
    {
        private string _name = "";

        /// <summary>ページの名前（例: 表紙）。一覧と生成コードのコメントに使う。</summary>
        public string Name { get => _name; set => Set(ref _name, value ?? ""); }

        private PaperSetting? _paper;
        private bool _hideCommon;

        /// <summary>部品。後ろのものほど手前に描く。</summary>
        public ObservableCollection<ReportElement> Elements { get; set; } = new ObservableCollection<ReportElement>();

        /// <summary>このページだけの用紙。null なら文書の用紙。</summary>
        public PaperSetting? Paper { get => _paper; set => Set(ref _paper, value); }

        /// <summary>このページには、全ページ共通の部品を描かない（全面の画像のページなど）。</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool HideCommon { get => _hideCommon; set => Set(ref _hideCommon, value); }
    }

    /// <summary>用紙の大きさと向き（ページごとの用紙に使う）。</summary>
    public sealed class PaperSetting : ObservableObject
    {
        private string _paperName = PaperSizes.Default.Name;
        private bool _landscape;
        private double _customWidth = 210;
        private double _customHeight = 297;

        /// <summary>用紙の名前（<see cref="PaperSizes"/>）。"カスタム" のときは <see cref="CustomWidth"/> と <see cref="CustomHeight"/> を使う。</summary>
        public string PaperName { get => _paperName; set { if (Set(ref _paperName, value ?? PaperSizes.Default.Name)) OnSizeChanged(); } }

        /// <summary>横向き。</summary>
        public bool Landscape { get => _landscape; set { if (Set(ref _landscape, value)) OnSizeChanged(); } }

        public double CustomWidth { get => _customWidth; set { if (Set(ref _customWidth, value)) OnSizeChanged(); } }

        public double CustomHeight { get => _customHeight; set { if (Set(ref _customHeight, value)) OnSizeChanged(); } }

        /// <summary>用紙の幅（mm）。横向きを反映した値。</summary>
        [JsonIgnore]
        public double PageWidth => PaperSizes.Width(PaperName, Landscape, CustomWidth, CustomHeight);

        /// <summary>用紙の高さ（mm）。横向きを反映した値。</summary>
        [JsonIgnore]
        public double PageHeight => PaperSizes.Height(PaperName, Landscape, CustomWidth, CustomHeight);

        private void OnSizeChanged()
        {
            OnPropertyChanged(nameof(PageWidth));
            OnPropertyChanged(nameof(PageHeight));
        }
    }

    /// <summary>用紙の大きさ（縦向き、mm）。</summary>
    public sealed class PaperSize
    {
        public PaperSize(string name, double width, double height, string? pageSizeProperty)
        {
            Name = name;
            Width = width;
            Height = height;
            PageSizeProperty = pageSizeProperty;
        }

        public string Name { get; }
        public double Width { get; }
        public double Height { get; }

        /// <summary>対応する <see cref="ChohyoPdf.PageSize"/> の静的プロパティ名（生成するコードで使う）。</summary>
        public string? PageSizeProperty { get; }

        public override string ToString() => Name;
    }

    public static class PaperSizes
    {
        public const string Custom = "カスタム";

        // 幅・高さは ChohyoPdf.PageSize の定義と同じ値にする（生成コードと PDF 出力を一致させるため）
        public static IReadOnlyList<PaperSize> All { get; } = new[]
        {
            new PaperSize("A3", 297, 420, "A3"),
            new PaperSize("A4", 210, 297, "A4"),
            new PaperSize("A5", 148, 210, "A5"),
            new PaperSize("A6", 105, 148, "A6"),
            new PaperSize("B4", 257, 364, "B4"),
            new PaperSize("B5", 182, 257, "B5"),
            new PaperSize("はがき", 100, 148, "Postcard"),
            new PaperSize("長形 3 号封筒", 120, 235, "EnvelopeChou3"),
            new PaperSize("Letter", 215.9, 279.4, "Letter"),
            new PaperSize("Legal", 215.9, 355.6, "Legal"),
        };

        public static PaperSize Default => All[1];

        /// <summary>名前から用紙を探す。カスタムや未知の名前なら null。</summary>
        public static PaperSize? Find(string? name) => All.FirstOrDefault(p => p.Name == name);

        /// <summary>用紙の幅（mm）。横向きを反映した値。</summary>
        public static double Width(string? name, bool landscape, double customWidth, double customHeight)
            => landscape ? Find(name)?.Height ?? customHeight : Find(name)?.Width ?? customWidth;

        /// <summary>用紙の高さ（mm）。横向きを反映した値。</summary>
        public static double Height(string? name, bool landscape, double customWidth, double customHeight)
            => landscape ? Find(name)?.Width ?? customWidth : Find(name)?.Height ?? customHeight;

        /// <summary>選択肢の一覧（カスタムを含む）。</summary>
        public static IReadOnlyList<string> Names { get; } = All.Select(p => p.Name).Concat(new[] { Custom }).ToArray();
    }

    /// <summary>フォントと、その太字のフォントの対応。</summary>
    public sealed class FontDefinition : ObservableObject
    {
        private string _name = "";
        private string? _boldName;

        public string Name { get => _name; set => Set(ref _name, value ?? ""); }

        /// <summary>太字に使うフォント。空なら疑似太字。</summary>
        public string? BoldName { get => _boldName; set => Set(ref _boldName, value); }
    }

    /// <summary>差し込む値の種類。</summary>
    public enum FieldKind
    {
        /// <summary>文字列（string）。</summary>
        Text,

        /// <summary>数値（decimal）。</summary>
        Number,

        /// <summary>日付（DateTime）。</summary>
        Date,
    }

    /// <summary>差し込み項目（一覧の列にも使う）。</summary>
    public sealed class FieldDefinition : ObservableObject
    {
        private string _name = "";
        private FieldKind _kind;
        private string? _format;
        private string _sampleValue = "";
        private string? _script;

        public string Name { get => _name; set => Set(ref _name, value ?? ""); }

        public FieldKind Kind { get => _kind; set => Set(ref _kind, value); }

        /// <summary>
        /// 値を計算する C# の式（または return を含む文）。null なら、PDF を作るときに値を渡す差し込み項目。
        /// 式では now（PDF を作る日時）と data（差し込むデータ）を使える。一覧の列には使わない。
        /// </summary>
        public string? Script { get => _script; set { if (Set(ref _script, value)) OnPropertyChanged(nameof(IsComputed)); } }

        /// <summary>値を C# の式で計算する。</summary>
        [JsonIgnore]
        public bool IsComputed
        {
            get => _script != null;
            set { if (value != IsComputed) Script = value ? "" : null; }
        }

        /// <summary>既定の書式（数値・日付）。差し込み欄で {項目:書式} と指定すればそちらを優先する。</summary>
        public string? Format { get => _format; set => Set(ref _format, value); }

        /// <summary>プレビューと PDF 出力に使うサンプルの値。</summary>
        public string SampleValue { get => _sampleValue; set => Set(ref _sampleValue, value ?? ""); }
    }

    /// <summary>差し込む一覧（表のくり返し行になるデータ）。</summary>
    public sealed class ListDefinition : ObservableObject
    {
        private string _name = "";
        private List<List<string>> _sampleRows = new List<List<string>>();

        public string Name { get => _name; set => Set(ref _name, value ?? ""); }

        /// <summary>1 行の中の項目。</summary>
        public ObservableCollection<FieldDefinition> Columns { get; set; } = new ObservableCollection<FieldDefinition>();

        /// <summary>サンプルの行。各行は <see cref="Columns"/> の順に値を並べたもの。</summary>
        public List<List<string>> SampleRows
        {
            get => _sampleRows;
            set => Set(ref _sampleRows, value ?? new List<List<string>>());
        }

        public FieldDefinition? FindColumn(string name) => Columns.FirstOrDefault(c => c.Name == name);
    }
}
