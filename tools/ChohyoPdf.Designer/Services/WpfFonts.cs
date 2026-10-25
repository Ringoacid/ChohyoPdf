using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ChohyoPdf.Designer.Services
{
    /// <summary>画面に表示するときの書体（WPF のフォントファミリーと太さ・スタイル）。</summary>
    public sealed class WpfTypeface
    {
        public WpfTypeface(FontFamily family, FontWeight weight, FontStyle style)
        {
            Family = family;
            Weight = weight;
            Style = style;
        }

        public FontFamily Family { get; }
        public FontWeight Weight { get; }
        public FontStyle Style { get; }
    }

    /// <summary>
    /// ChohyoPdf のフォント名（例: "Yu Gothic Regular"、"BIZ UDGothic"、"游ゴシック"）を、WPF の書体に対応付ける。
    /// フォント名は「ファミリー名」か「ファミリー名 ＋ スタイル名」なので、OS のフォントのファミリー名とスタイル名から探す。
    /// </summary>
    public static class WpfFonts
    {
        private static readonly Dictionary<string, WpfTypeface?> Cache = new Dictionary<string, WpfTypeface?>(StringComparer.OrdinalIgnoreCase);
        private static readonly Lazy<List<FontFamily>> Families = new Lazy<List<FontFamily>>(() => System.Windows.Media.Fonts.SystemFontFamilies.ToList());

        /// <summary>フォント名に対応する書体。見つからなければ null。</summary>
        public static WpfTypeface? Resolve(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name!.Trim();
            lock (Cache)
            {
                if (Cache.TryGetValue(name, out var cached)) return cached;
                var result = Find(name);
                Cache[name] = result;
                return result;
            }
        }

        private static WpfTypeface? Find(string name)
        {
            WpfTypeface? best = null;
            int bestLength = -1;
            foreach (var family in Families.Value)
            {
                foreach (string familyName in FamilyNames(family))
                {
                    if (familyName.Length <= bestLength) continue;
                    if (string.Equals(name, familyName, StringComparison.OrdinalIgnoreCase))
                    {
                        best = new WpfTypeface(family, FontWeights.Normal, FontStyles.Normal);
                        bestLength = familyName.Length;
                    }
                    else if (name.StartsWith(familyName + " ", StringComparison.OrdinalIgnoreCase)
                        && FindFace(family, name.Substring(familyName.Length + 1)) is WpfTypeface face)
                    {
                        best = face;
                        bestLength = familyName.Length;
                    }
                }
            }
            return best;
        }

        private static IEnumerable<string> FamilyNames(FontFamily family)
        {
            yield return family.Source;
            foreach (string n in family.FamilyNames.Values) yield return n;
        }

        /// <summary>ファミリーの中から、スタイル名（"Regular"、"Bold"、"Medium" など）の書体を探す。</summary>
        private static WpfTypeface? FindFace(FontFamily family, string faceName)
        {
            foreach (var typeface in family.FamilyTypefaces)
            {
                if (typeface.AdjustedFaceNames.Values.Any(n => string.Equals(n, faceName, StringComparison.OrdinalIgnoreCase)))
                    return new WpfTypeface(family, typeface.Weight, typeface.Style);
            }
            // 合成フォント（游ゴシックなど）はスタイル名を持たないことがあるので、太さの名前として読む
            var weight = ParseWeight(faceName);
            return weight != null ? new WpfTypeface(family, weight.Value, FontStyles.Normal) : null;
        }

        private static FontWeight? ParseWeight(string faceName)
        {
            switch (faceName.Replace(" ", "").ToLowerInvariant())
            {
                case "regular": case "normal": case "r": return FontWeights.Normal;
                case "thin": return FontWeights.Thin;
                case "extralight": case "ultralight": return FontWeights.ExtraLight;
                case "light": case "l": return FontWeights.Light;
                case "medium": case "m": return FontWeights.Medium;
                case "demibold": case "semibold": return FontWeights.SemiBold;
                case "bold": case "b": return FontWeights.Bold;
                case "extrabold": case "ultrabold": return FontWeights.ExtraBold;
                case "black": case "heavy": return FontWeights.Black;
                default: return null;
            }
        }
    }
}
