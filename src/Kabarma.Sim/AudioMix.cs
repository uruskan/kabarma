using System.Text.Json;

namespace Kabarma.Sim;

/// <summary>
/// Ses ve muzik secimi (SANAT-SES §10): motordan bagimsiz, testli. Godot katmani (Sfx, MusicDirector, Hud.Voice)
/// bu kurallari cagirir; sayilar audio.json'da.
/// </summary>
public static class AudioMix
{
    /// <summary>
    /// Kabarma muziginin yogunlugu (0..1): dalga ilerlemesi ve Gosteri carpaninin agirlikli ortalamasi
    /// (SANAT-SES §10.2: "yogunluk dalga numarasina ve Gosteri carpanina gore"). Alarm evresinde dalga yok: 0.
    /// </summary>
    public static float Intensity(KabarmaMusicDef def, int waveIndex, int waveCount, float gosteri, float gosteriMin, float gosteriMax)
    {
        float wave = waveIndex < 0 ? 0f : waveCount <= 1 ? 1f : Math.Clamp(waveIndex / (float)(waveCount - 1), 0f, 1f);
        float show = gosteriMax > gosteriMin ? Math.Clamp((gosteri - gosteriMin) / (gosteriMax - gosteriMin), 0f, 1f) : 0f;
        float sum = def.WaveWeight + def.GosteriWeight;
        if (sum <= 0f) return 0f;
        return Math.Clamp((def.WaveWeight * wave + def.GosteriWeight * show) / sum, 0f, 1f);
    }

    /// <summary>Acik katman sayisi: 1 + gecilen esik sayisi, en fazla <paramref name="layers"/>.</summary>
    public static int LayersFor(float intensity, IReadOnlyList<float> thresholds, int layers) =>
        Math.Clamp(1 + thresholds.Count(t => intensity >= t), 1, Math.Max(1, layers));

    /// <summary>Bir olcunun suresi (sn). Tempo bilinmiyorsa 0: katman hemen gecer.</summary>
    public static double BarSeconds(float bpm, int beatsPerBar) => bpm <= 0f ? 0.0 : 60.0 / bpm * Math.Max(1, beatsPerBar);

    /// <summary>Calma suresi <paramref name="t"/> hangi olcude. Tempo bilinmiyorsa (olcu 0) saniyede dort karar noktasi.</summary>
    public static long BarIndex(double t, double barSeconds) => (long)Math.Floor(barSeconds <= 0 ? t * 4.0 : t / barSeconds);

    /// <summary>
    /// Gezme muzigi bittikten sonraki sessizlik: muzik zamanin (1 - pay) kadarinda calar (SANAT-SES §10.2:
    /// "gezme muzigi zamanin yarisinda susar"). Pay 1 ise hic calmaz (sonsuz).
    /// </summary>
    public static float SilenceAfter(float trackSeconds, float silenceShare)
    {
        if (silenceShare <= 0f) return 0f;
        if (silenceShare >= 1f) return float.PositiveInfinity;
        return trackSeconds * silenceShare / (1f - silenceShare);
    }

    /// <summary>
    /// Varyasyon secimi: ayni varyasyon art arda calmaz. <paramref name="roll"/> 0..(count-2) arasi rastgele sayi
    /// (count 1 ise yok sayilir). Donus 0..count-1.
    /// </summary>
    public static int PickVariant(int count, int last, int roll)
    {
        if (count <= 1) return 0;
        roll = Math.Clamp(roll, 0, count - 2);
        if (last < 0 || last >= count) return roll;
        return roll >= last ? roll + 1 : roll;
    }

    /// <summary>Rastgele ±yuzde perde ve ±dB duzey: <paramref name="u"/> ve <paramref name="v"/> 0..1 arasi.</summary>
    public static (float Pitch, float Db) Jitter(AudioVariationDef def, float u, float v) =>
        (1f + (u * 2f - 1f) * def.Pitch, (v * 2f - 1f) * def.VolumeDb);
}

/// <summary>
/// Kabarma katmanlari icin olcu sinirinda karar: artis bir sonraki olcude, azalis en az HoldBars olcu sonra.
/// </summary>
public sealed class LayerPlan
{
    public int Active { get; private set; } = 1;
    public int HoldBars { get; }
    /// <summary>Katman sayisinin son degistigi olcu.</summary>
    public long ChangedBar => _changedBar;
    long _changedBar;

    public LayerPlan(int holdBars) { HoldBars = Math.Max(0, holdBars); }

    public void Reset(int active, long bar) { Active = Math.Max(1, active); _changedBar = bar; }

    /// <summary>Yeni olcuye girildi. Katman sayisi degistiyse true.</summary>
    public bool OnBar(long bar, int desired)
    {
        desired = Math.Max(1, desired);
        if (desired == Active) return false;
        if (desired < Active && bar - _changedBar < HoldBars) return false;
        Active = desired;
        _changedBar = bar;
        return true;
    }
}

/// <summary>
/// Suna'nin replikleri (kimlik, metin), scenario.json'un ham metninden. Iki yapi okunur:
/// <list type="bullet">
/// <item>ilkgun yapisi: <c>"lines": { kimlik: { "tr": ..., "en": ... } }</c>. Kimlik acik, iki dil ayni dosyada
/// (SANAT-SES §10.3). Adimlar ve gece olaylari bu kimlikleri verir; burada yalniz "lines" okunur.</item>
/// <item>eski yapi (af67734, "lines" yok): kimlik turetilir (intro_N, {adim}_start, {adim}_done, night_{anahtar},
/// next_order); yalniz Turkce metin vardir.</item>
/// </list>
/// ScenarioFile DTO'suna dayanmaz: iki dal ayri ayri derlenir, birlesince "lines" yapisi kendiliginden secilir.
/// Ses dosyasi audio.json radio.path kalibiyla (voice/{dil}/{kimlik}.mp3). Ayni kural pipeline/eleven.py load_lines.
/// </summary>
public static class VoiceLines
{
    static readonly JsonDocumentOptions Doc = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>Bir dildeki replikler, dosyadaki sirayla. Metni bos olan replik atlanir. lang: "tr" ya da "en".</summary>
    public static List<(string Id, string Text)> FromJson(string json, string lang = "tr")
    {
        var list = new List<(string, string)>();
        using var doc = JsonDocument.Parse(json, Doc);
        var root = doc.RootElement;

        if (Prop(root, "lines") is { ValueKind: JsonValueKind.Object } lines)
        {
            foreach (var p in lines.EnumerateObject())
            {
                if (p.Name.StartsWith('_')) continue; // aciklama anahtari
                string? text = p.Value.ValueKind == JsonValueKind.String
                    ? (lang == "tr" ? p.Value.GetString() : null)
                    : Str(Prop(p.Value, lang));
                Add(list, p.Name, text);
            }
            return list;
        }

        if (lang != "tr") return list; // eski yapida yalniz Turkce metin var
        if (Prop(root, "intro") is { ValueKind: JsonValueKind.Array } intro)
        {
            int i = 0;
            foreach (var t in intro.EnumerateArray()) Add(list, $"intro_{++i}", Str(t));
        }
        if (Prop(root, "steps") is { ValueKind: JsonValueKind.Array } steps)
            foreach (var s in steps.EnumerateArray())
            {
                string? id = Str(Prop(s, "id"));
                if (id == null) continue;
                Add(list, $"{id}_start", Str(Prop(s, "start")));
                Add(list, $"{id}_done", Str(Prop(s, "done")));
            }
        if (Prop(root, "nightLines") is { ValueKind: JsonValueKind.Object } night)
            foreach (var p in night.EnumerateObject()) Add(list, $"night_{p.Name}", Str(p.Value));
        Add(list, "next_order", Str(Prop(root, "nextOrder")));
        return list;
    }

    /// <summary>Buyuk-kucuk harf duyarsiz alan (GameData de oyle okur).</summary>
    static JsonElement? Prop(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in e.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return null;
    }

    static string? Str(JsonElement? e) => e is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;

    static void Add(List<(string, string)> list, string id, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) list.Add((id, text.Trim()));
    }
}
