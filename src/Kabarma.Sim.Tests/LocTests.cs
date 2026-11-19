using System.Text.RegularExpressions;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>M6 dil (TEKNIK 12, URETIM 2.2 K): 10 dilde anahtar eksigi 0; yer tutucular ve glif belirtecleri her dilde ayni; kodda kullanilan her anahtar tabloda.</summary>
public class LocTests
{
    static string LocDir => Path.Combine(AppContext.BaseDirectory, "locale");

    static LocTable Load()
    {
        var t = new LocTable();
        foreach (var f in Directory.GetFiles(LocDir, "*.loc").OrderBy(x => x))
            if (Path.GetFileName(f) != "languages.loc") t.AddCsv(File.ReadAllText(f), Path.GetFileName(f));
        return t;
    }

    static string? RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "game", "scripts"))) d = d.Parent;
        return d?.FullName;
    }

    [Fact]
    public void Tables_parse_without_errors_and_have_keys()
    {
        var t = Load();
        Assert.Empty(t.Errors);
        Assert.True(t.Count > 400, $"yalniz {t.Count} anahtar");
    }

    [Fact]
    public void Every_key_is_filled_in_all_ten_languages()
    {
        var t = Load();
        var bad = new List<string>();
        foreach (var lang in LocTable.Codes)
            foreach (var k in t.Missing(lang)) bad.Add($"{lang}:{k}");
        Assert.True(bad.Count == 0, $"{bad.Count} bos hucre, ilk 20: {string.Join(", ", bad.Take(20))}");
    }

    [Fact]
    public void Placeholders_and_glyph_tokens_match_turkish_in_every_language()
    {
        var t = Load();
        var bad = new List<string>();
        foreach (var k in t.Keys)
        {
            var tr = t.Raw("tr", k);
            var ph = LocTable.Placeholders(tr);
            var act = LocTable.ActTokens(tr);
            foreach (var lang in LocTable.Codes.Skip(1))
            {
                var s = t.Raw(lang, k);
                if (!ph.SequenceEqual(LocTable.Placeholders(s))) bad.Add($"{lang}:{k} yer tutucu");
                if (!act.SequenceEqual(LocTable.ActTokens(s))) bad.Add($"{lang}:{k} glif");
            }
        }
        Assert.True(bad.Count == 0, $"{bad.Count} uyumsuz, ilk 20: {string.Join(", ", bad.Take(20))}");
    }

    [Fact]
    public void Languages_file_lists_ten_languages_and_marks_machine_translations()
    {
        var langs = LocTable.ParseLanguages(File.ReadAllText(Path.Combine(LocDir, "languages.loc")));
        Assert.Equal(LocTable.Codes, langs.Select(l => l.Code).ToArray());
        Assert.All(langs.Where(l => l.Code is "tr" or "en"), l => Assert.False(l.Machine));
        Assert.All(langs.Where(l => l.Code is not ("tr" or "en")), l => Assert.True(l.Machine));
    }

    [Fact]
    public void Every_loc_key_used_in_code_exists_in_the_table()
    {
        var root = RepoRoot();
        if (root == null) return; // paketlenmis ortam: kaynak yok
        var t = Load();
        var rx = new Regex("""Loc\.T\(\s*"([a-z0-9_]+\.[a-zA-Z0-9_.]+)"|ItemNames\.Of\(\s*"([a-z0-9_]+)"|Loc\.Has\(\s*"([a-z0-9_.]+)"|LocKey\(\s*"([a-z0-9_.]+)"|L\(\s*"([a-z0-9_]+\.[a-zA-Z0-9_.]+)"\s*[,)]""");
        var missing = new List<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "game", "scripts"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.EndsWith("Loc.cs")) continue;
            var src = File.ReadAllText(f);
            foreach (Match m in rx.Matches(src))
            {
                string? key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Success ? m.Groups[4].Value : m.Groups[5].Success ? m.Groups[5].Value : null;
                if (m.Groups[2].Success) key = "item." + m.Groups[2].Value;
                if (key != null && !t.Has(key)) missing.Add($"{Path.GetFileName(f)}: {key}");
            }
        }
        Assert.True(missing.Count == 0, $"tabloda olmayan {missing.Count} anahtar: {string.Join("; ", missing.Take(15))}");
    }

    [Fact]
    public void Item_ids_with_a_turkish_name_before_the_move_all_have_item_keys()
    {
        var d = TestData.Load();
        var t = Load();
        var ids = d.Tool.Recipes.Select(r => r.Id).Concat(d.Tool.Recipes.SelectMany(r => r.In.Keys)).Distinct().ToList();
        var missing = ids.Where(i => !t.Has("item." + i)).ToList();
        Assert.True(missing.Count <= 40, $"adsiz {missing.Count} esya (ilk 20): {string.Join(", ", missing.Take(20))}");
    }

    [Fact]
    public void Format_fills_placeholders_and_keeps_unknown_ones()
    {
        Assert.Equal("3/5 Odun", LocTable.Format("{0}/{1} Odun", 3, 5));
        Assert.Equal("1 {2}", LocTable.Format("{0} {2}", 1));
        Assert.Equal("{act:scan} 7", LocTable.Format("{act:scan} {0}", 7));
    }

    [Fact]
    public void Csv_parser_handles_quotes_commas_and_newlines()
    {
        var rows = LocTable.ParseCsv("key,tr,en\r\na.b,\"x, y\",\"he said \"\"hi\"\"\"\r\nc.d,,z\r\n");
        Assert.Equal(3, rows.Count);
        Assert.Equal("x, y", rows[1][1]);
        Assert.Equal("he said \"hi\"", rows[1][2]);
        Assert.Equal("", rows[2][1]);
    }

    [Fact]
    public void Get_falls_back_to_english_then_turkish_then_key()
    {
        var t = new LocTable();
        t.AddCsv("key,tr,en,de,fr,es,pt_BR,ru,zh_CN,ja,ko\nk.a,tr1,en1,,,,,,,,\nk.b,tr2,,,,,,,,,\n", "mem");
        Assert.Equal("en1", t.Get("de", "k.a"));
        Assert.Equal("tr2", t.Get("de", "k.b"));
        Assert.Equal("k.z", t.Get("de", "k.z"));
    }
}
