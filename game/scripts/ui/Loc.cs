using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Arayuz dili (TEKNIK 12; M6 dil). Metinler <c>game/locale/*.loc</c> CSV dosyalarinda (Sim'deki <see cref="LocTable"/>);
/// <c>Loc.T("alan.ad", arg0, arg1)</c> secili dilde metni verir ({0}, {1} yer tutucular), anahtar yoksa Ingilizce, sonra
/// Turkce, sonra anahtarin kendisi. Dil <c>user://language.txt</c>'te saklanir; ilk acilista isletim sistemi diline bakilir,
/// desteklenmiyorsa Turkce. Tablo Godot TranslationServer'a da yazilir (Godot'un kendi dugumleri icin).
/// </summary>
public static class Loc
{
    public static LocTable Table { get; private set; }
    public static List<LocLanguage> Languages { get; private set; } = new();
    static string _lang = "tr";
    const string SavePath = "user://language.txt";

    public static string Language => _lang;
    public static event System.Action Changed;

    /// <summary>Ceviri dosyalarini okur (bir kez; T ilk cagrida kendiliginden cagirir).</summary>
    public static void Init()
    {
        if (Table != null) return;
        var t = new LocTable();
        foreach (var f in DirAccess.GetFilesAt("res://locale").OrderBy(x => x))
        {
            if (!f.EndsWith(".loc")) continue;
            using var fa = FileAccess.Open("res://locale/" + f, FileAccess.ModeFlags.Read);
            if (fa == null) continue;
            if (f == "languages.loc") Languages = LocTable.ParseLanguages(fa.GetAsText());
            else t.AddCsv(fa.GetAsText(), f);
        }
        foreach (var e in t.Errors) GD.PushWarning("LOC " + e);
        Table = t;
        _lang = Pick();
        RegisterWithServer();
        GD.Print($"LOC keys={t.Count} lang={_lang}");
    }

    static string Pick()
    {
        try
        {
            if (FileAccess.FileExists(SavePath))
            {
                using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                var s = f?.GetAsText().Trim();
                if (!string.IsNullOrEmpty(s) && LocTable.Codes.Contains(s)) return s;
            }
        }
        catch { }
        string os = OS.GetLocale(); // "tr_TR", "pt_BR", "zh_CN"...
        if (LocTable.Codes.Contains(os)) return os;
        var two = os.Split('_')[0];
        if (two == "pt") return "pt_BR";
        if (two == "zh") return "zh_CN";
        return LocTable.Codes.Contains(two) ? two : "tr";
    }

    static void RegisterWithServer()
    {
        foreach (var code in LocTable.Codes)
        {
            var tr = new Translation { Locale = code };
            foreach (var k in Table.Keys) tr.AddMessage(k, Table.Get(code, k));
            TranslationServer.AddTranslation(tr);
        }
        TranslationServer.SetLocale(_lang);
    }

    /// <summary>Dili degistirir ve saklar; Changed olayi acik panellerin yenilenmesi icin.</summary>
    public static void SetLanguage(string code)
    {
        Init();
        if (!LocTable.Codes.Contains(code) || code == _lang) return;
        _lang = code;
        TranslationServer.SetLocale(code);
        try { using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write); f?.StoreString(code); } catch { }
        Changed?.Invoke();
    }

    /// <summary>Bir sonraki dil (ayarlar satiri tiklaninca).</summary>
    public static void Next()
    {
        Init();
        int i = System.Array.IndexOf(LocTable.Codes, _lang);
        SetLanguage(LocTable.Codes[(i + 1) % LocTable.Codes.Length]);
    }

    public static bool Has(string key) { Init(); return Table.Has(key); }

    public static string T(string key)
    {
        Init();
        return Table.Get(_lang, key);
    }

    public static string T(string key, params object[] args)
    {
        Init();
        return LocTable.Format(Table.Get(_lang, key), args);
    }

    /// <summary>Dil satirinin etiketi: kendi adi, makine cevirisiyse isaret.</summary>
    public static string LanguageLabel(string code)
    {
        Init();
        var l = Languages.FirstOrDefault(x => x.Code == code);
        return l == null ? code : l.Name + (l.Machine ? " " + T("loc.machine") : "");
    }
}
