using System.Text;

namespace Kabarma.Sim;

/// <summary>Bir arayuz dili (game/locale/languages.loc): kod, kendi adi, makine ceviri mi.</summary>
public sealed record LocLanguage(string Code, string Name, bool Machine);

/// <summary>
/// Ceviri tablosu (TEKNIK 12; M6 dil). Dosya bicimi: virgullu CSV, UTF-8, ".loc" uzantili (Godot .csv'yi kendi ceviri
/// icerigi olarak iceri aktarir ve disa aktarmada ham dosyayi dusurur). Ilk satir baslik: key, sonra dil kodlari
/// (tr, en, de, fr, es, pt_BR, ru, zh_CN, ja, ko). Anahtar "alan.ad" bicimindedir; metinde {0}, {1} yer tutucular,
/// {act:...} glif belirtecleri ve satir sonu icin \n. Bir anahtar birden cok dosyada olamaz. Godot'tan bagimsizdir
/// (Sim testleri tabloyu ayni sekilde okur).
/// </summary>
public sealed class LocTable
{
    /// <summary>Planin 10 dili, sirasi dosya sutunlariyla ayni.</summary>
    public static readonly string[] Codes = { "tr", "en", "de", "fr", "es", "pt_BR", "ru", "zh_CN", "ja", "ko" };

    readonly Dictionary<string, string[]> _rows = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _source = new(StringComparer.Ordinal);
    public List<string> Errors { get; } = new();
    public IReadOnlyCollection<string> Keys => _rows.Keys;
    public int Count => _rows.Count;
    public string SourceOf(string key) => _source.GetValueOrDefault(key, "");

    /// <summary>Bir dosyanin icerigini ekler. Hata Errors'a yazilir (yinelenen anahtar, bozuk satir).</summary>
    public void AddCsv(string text, string source)
    {
        var rows = ParseCsv(text);
        if (rows.Count == 0) return;
        var head = rows[0];
        if (head.Count < 2 || head[0] != "key") { Errors.Add($"{source}: baslik 'key,...' olmali"); return; }
        var cols = new int[Codes.Length];
        for (int i = 0; i < Codes.Length; i++)
        {
            cols[i] = head.IndexOf(Codes[i]);
            if (cols[i] < 0) { Errors.Add($"{source}: '{Codes[i]}' sutunu yok"); return; }
        }
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 0 || (row.Count == 1 && row[0].Length == 0)) continue;
            string key = row[0];
            if (key.Length == 0) { Errors.Add($"{source}:{r + 1}: bos anahtar"); continue; }
            var vals = new string[Codes.Length];
            for (int i = 0; i < Codes.Length; i++) vals[i] = cols[i] < row.Count ? row[cols[i]].Replace("\\n", "\n") : "";
            if (!_rows.TryAdd(key, vals)) { Errors.Add($"{source}:{r + 1}: yinelenen anahtar '{key}' (ilk: {_source[key]})"); continue; }
            _source[key] = source;
        }
    }

    public bool Has(string key) => _rows.ContainsKey(key);

    /// <summary>Metin: istenen dil, bossa Ingilizce, sonra Turkce, sonra anahtarin kendisi.</summary>
    public string Get(string lang, string key)
    {
        if (!_rows.TryGetValue(key, out var v)) return key;
        int i = Array.IndexOf(Codes, lang);
        if (i >= 0 && v[i].Length > 0) return v[i];
        if (v[1].Length > 0) return v[1];
        return v[0].Length > 0 ? v[0] : key;
    }

    /// <summary>Yer tutuculari doldurur: {0}, {1}... Tanimsiz indeks yazildigi gibi kalir.</summary>
    public static string Format(string template, params object?[] args)
    {
        if (args.Length == 0 || template.IndexOf('{') < 0) return template;
        var sb = new StringBuilder(template.Length + 16);
        for (int i = 0; i < template.Length; i++)
        {
            char c = template[i];
            if (c == '{')
            {
                int j = i + 1;
                int n = 0;
                while (j < template.Length && char.IsAsciiDigit(template[j])) { n = n * 10 + (template[j] - '0'); j++; }
                if (j > i + 1 && j < template.Length && template[j] == '}' && n < args.Length) { sb.Append(args[n]); i = j; continue; }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Metindeki sayisal yer tutucular ({0}, {1}), sirali ve tekil.</summary>
    public static List<int> Placeholders(string text)
    {
        var set = new SortedSet<int>();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '{') continue;
            int j = i + 1, n = 0;
            while (j < text.Length && char.IsAsciiDigit(text[j])) { n = n * 10 + (text[j] - '0'); j++; }
            if (j > i + 1 && j < text.Length && text[j] == '}') { set.Add(n); i = j; }
        }
        return set.ToList();
    }

    /// <summary>Metindeki {act:...} glif belirteclerinin kumesi (cevirinin bozmamasi gerekir).</summary>
    public static List<string> ActTokens(string text)
    {
        var list = new List<string>();
        int i = 0;
        while ((i = text.IndexOf("{act:", i, StringComparison.Ordinal)) >= 0)
        {
            int e = text.IndexOf('}', i);
            if (e < 0) break;
            list.Add(text.Substring(i, e - i + 1));
            i = e + 1;
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>Verilen dilde bos (Ingilizce ya da Turkceye dusen) anahtarlar.</summary>
    public List<string> Missing(string lang)
    {
        int i = Array.IndexOf(Codes, lang);
        return _rows.Where(kv => kv.Value[i].Trim().Length == 0).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    /// <summary>Tek dilin ham metni (bos olabilir; geri dusme yok). Sinama icin.</summary>
    public string Raw(string lang, string key) => _rows.TryGetValue(key, out var v) ? v[Array.IndexOf(Codes, lang)] : "";

    /// <summary>languages.loc: code,name,machine.</summary>
    public static List<LocLanguage> ParseLanguages(string text)
    {
        var res = new List<LocLanguage>();
        var rows = ParseCsv(text);
        for (int r = 1; r < rows.Count; r++)
            if (rows[r].Count >= 3 && rows[r][0].Length > 0) res.Add(new LocLanguage(rows[r][0], rows[r][1], rows[r][2] == "1" || rows[r][2].Equals("true", StringComparison.OrdinalIgnoreCase)));
        return res;
    }

    /// <summary>RFC 4180 CSV: tirnakli alanlar, "" kacisi, tirnak icinde virgul ve satir sonu.</summary>
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var sb = new StringBuilder();
        bool q = false, any = false;
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; } else q = false; }
                else sb.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': q = true; any = true; break;
                case ',': row.Add(sb.ToString()); sb.Clear(); any = true; break;
                case '\r': break;
                case '\n':
                    if (any || sb.Length > 0) { row.Add(sb.ToString()); rows.Add(row); }
                    row = new List<string>(); sb.Clear(); any = false;
                    break;
                default: sb.Append(c); any = true; break;
            }
        }
        if (any || sb.Length > 0) { row.Add(sb.ToString()); rows.Add(row); }
        return rows;
    }

    /// <summary>Bir alani CSV'ye yazar (gerekirse tirnaklar).</summary>
    public static string CsvField(string s)
    {
        s = s.Replace("\r", "").Replace("\n", "\\n");
        return s.IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
