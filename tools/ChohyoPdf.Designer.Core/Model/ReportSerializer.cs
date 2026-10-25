using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>デザインを JSON で保存・読み込みする。</summary>
    public static class ReportSerializer
    {
        /// <summary>デザインファイルの拡張子。</summary>
        public const string FileExtension = ".chohyo";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            // 日本語をエスケープせずに書き、ファイルを読みやすくする
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        public static string ToJson(ReportDefinition report) => JsonSerializer.Serialize(report, Options);

        public static ReportDefinition FromJson(string json)
        {
            var report = JsonSerializer.Deserialize<ReportDefinition>(json, Options)
                ?? throw new InvalidDataException("デザインファイルの内容が空です。");
            if (report.FormatVersion > ReportDefinition.CurrentFormatVersion)
                throw new InvalidDataException("新しい版のデザイナーで作られたファイルです（形式のバージョン " + report.FormatVersion + "）。");
            report.Normalize();
            foreach (var element in report.AllElements())
            {
                if (element is TableElement table) table.NormalizeCells();
            }
            return report;
        }

        public static void Save(ReportDefinition report, string path) => File.WriteAllText(path, ToJson(report));

        public static ReportDefinition Load(string path) => FromJson(File.ReadAllText(path));

        /// <summary>デザインを複製する（JSON を経由するので、ほかのスレッドで安全に使える）。</summary>
        public static ReportDefinition Clone(ReportDefinition report) => FromJson(ToJson(report));

        /// <summary>部品を複製する。</summary>
        public static T CloneElement<T>(T element) where T : ReportElement
        {
            string json = JsonSerializer.Serialize<ReportElement>(element, Options);
            var clone = (T)(JsonSerializer.Deserialize<ReportElement>(json, Options) ?? throw new InvalidOperationException());
            if (clone is TableElement table) table.NormalizeCells();
            return clone;
        }

        /// <summary>部品の一覧を JSON にする（コピー・貼り付け用）。</summary>
        public static string ElementsToJson(IEnumerable<ReportElement> elements)
            => JsonSerializer.Serialize(new List<ReportElement>(elements), Options);

        public static List<ReportElement> ElementsFromJson(string json)
        {
            var list = JsonSerializer.Deserialize<List<ReportElement>>(json, Options)
                ?? new List<ReportElement>();
            foreach (var e in list)
            {
                if (e is TableElement table) table.NormalizeCells();
            }
            return list;
        }
    }
}
