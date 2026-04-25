using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DocumentManager.Services
{
    /// <summary>
    /// Türkçe-aware metin normalizasyonu. Amaç: arama sırasında
    /// "Sözleşme", "sozlesme", "SÖZLEŞME" hepsinin aynı şeyi getirmesi.
    ///
    /// Strateji: lowercase + diacritics strip + non-alphanumeric'i boşluğa çevir.
    /// LocalDB'de FTS5 yok, bu yüzden normalize edilmiş alan üzerinde LIKE.
    /// </summary>
    public static class DocumentNormalizer
    {
        // Türkçe özel harfler için doğrudan map — Unicode FormD bazılarını
        // karşılamıyor (ı için özellikle).
        private static readonly (char from, char to)[] TurkishMap =
        {
            ('İ', 'i'), ('I', 'i'), ('ı', 'i'),
            ('Ş', 's'), ('ş', 's'),
            ('Ğ', 'g'), ('ğ', 'g'),
            ('Ü', 'u'), ('ü', 'u'),
            ('Ö', 'o'), ('ö', 'o'),
            ('Ç', 'c'), ('ç', 'c'),
        };

        private static readonly Regex NonAlphanumeric = new Regex(@"[^a-z0-9\s]", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                var mapped = ch;
                foreach (var (from, to) in TurkishMap)
                {
                    if (ch == from) { mapped = to; break; }
                }
                sb.Append(mapped);
            }

            // Kalan diakritikleri (FormD) çıkar
            var formD = sb.ToString().Normalize(NormalizationForm.FormD);
            var stripped = new string(formD.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

            stripped = stripped.ToLowerInvariant();
            stripped = NonAlphanumeric.Replace(stripped, " ");
            stripped = Whitespace.Replace(stripped, " ").Trim();
            return stripped;
        }

        /// <summary>Query'yi tokenlara böler (boşluk + min 2 karakter).</summary>
        public static string[] Tokenize(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized)) return Array.Empty<string>();
            return normalized
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 2)
                .Distinct()
                .ToArray();
        }
    }
}
