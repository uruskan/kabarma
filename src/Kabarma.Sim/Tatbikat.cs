namespace Kabarma.Sim;

/// <summary>
/// Yerel ya da hayalet Kabarma istegi: Maya Feneri (GDD 14.6) ve Tatbikat (GDD 14.7). Gece akisini kabarma3
/// akisi kurar; bu istek onun baslatma girdisidir.
/// </summary>
public sealed class LocalKabarmaRequest
{
    /// <summary>fener ya da tatbikat.</summary>
    public string Kind { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    /// <summary>Kabaran alanin yaricapi (0: butun tapu).</summary>
    public float Radius { get; set; }
    /// <summary>Sure (0: tam gece).</summary>
    public float Seconds { get; set; }
    public int Ks { get; set; }
    /// <summary>Verilebilecek en fazla kart seviyesi (0: sinirsiz).</summary>
    public int MaxCardLevels { get; set; }
    /// <summary>Dusmanlar hayalet: ganimet, Oz, Maya yok.</summary>
    public bool Ghost { get; set; }
    /// <summary>Gece sayaci, is emirleri, kart, Maya ve ganimet ilerler mi.</summary>
    public bool Rewards { get; set; }
    /// <summary>Alandaki olgun ekinler ve evcil hayvanlar kabarir mi.</summary>
    public bool Swell { get; set; }
}

/// <summary>
/// GECICI SOZLESME (gucler, 18 Kasim 2026): kabarma3 akisinin Wave* arayuzu main'e girene kadar yerel Kabarma
/// baslatma kapisi. kabarma3 sozlesmesi gelince bu arayuz onunla degistirilir ya da ona uyarlanir.
/// </summary>
public interface IKabarmaStarter
{
    /// <summary>Acilmis en yuksek KS (KS merdiveni kabarma3'te).</summary>
    int HighestKs { get; }
    /// <summary>Su an bir Kabarma suruyor mu.</summary>
    bool Active { get; }
    /// <summary>Istegi baslatir; olmuyorsa Turkce neden.</summary>
    string? Start(LocalKabarmaRequest r);
}

/// <summary>Maya Feneri durumu: hangi fener hangi oyun gununde yakildi (fener kimligi -> gun).</summary>
public sealed class FenerProgress
{
    public Dictionary<string, int> LitDay { get; set; } = new();
    public Dictionary<string, int> LitCount { get; set; } = new();
}

/// <summary>Tatbikat kaydi: baslangicta yapi canlari (bitince bedava onarim). Kayitta yok.</summary>
public sealed class TatbikatRun
{
    public int Ks { get; set; }
    public Dictionary<string, float> Hp { get; set; } = new();
}

public static class TatbikatRules
{
    /// <summary>Fener KS'si: acilmis en yuksek KS'nin yarisi, asagi, en az 1.</summary>
    public static int FenerKs(AbilitiesFile f, int highestKs) =>
        Math.Max(1, (int)MathF.Floor(Math.Max(1, highestKs) * f.Fener.KsFactor));

    /// <summary>Feneri yakar: o gun yakilmadiysa istegi kurar ve gunu yazar. Olmuyorsa null ve neden.</summary>
    public static LocalKabarmaRequest? LightFener(AbilitiesFile f, FenerProgress p, string fenerId, int day, int highestKs, bool kabarmaActive, float x, float z, out string reason)
    {
        reason = "";
        if (kabarmaActive) { reason = "Bir Kabarma zaten sürüyor"; return null; }
        int used = p.LitDay.TryGetValue(fenerId, out var d) && d == day ? p.LitCount.GetValueOrDefault(fenerId) : 0;
        if (used >= f.Fener.UsesPerDay) { reason = "Fener bugün yakıldı; yarın yine yanar"; return null; }
        p.LitDay[fenerId] = day;
        p.LitCount[fenerId] = used + 1;
        return new LocalKabarmaRequest
        {
            Kind = "fener", X = x, Z = z, Radius = f.Fener.Radius, Seconds = f.Fener.Seconds,
            Ks = FenerKs(f, highestKs), MaxCardLevels = f.Fener.MaxCardLevels, Ghost = false, Rewards = true, Swell = true,
        };
    }

    /// <summary>Kaldirilan fenerin kaydini siler.</summary>
    public static void Forget(FenerProgress p, string fenerId) { p.LitDay.Remove(fenerId); p.LitCount.Remove(fenerId); }

    /// <summary>Tatbikat istegi: acilmis KS'lerden biri, hayalet, odulsuz, kabartmaz. Olmuyorsa null ve neden.</summary>
    public static LocalKabarmaRequest? Tatbikat(AbilitiesFile f, int ks, int highestKs, bool kabarmaActive, out string reason)
    {
        reason = "";
        if (kabarmaActive) { reason = "Bir Kabarma zaten sürüyor"; return null; }
        if (ks < 1 || ks > Math.Max(1, highestKs)) { reason = $"KS {ks} açılmadı"; return null; }
        return new LocalKabarmaRequest { Kind = "tatbikat", Ks = ks, Ghost = true, Rewards = f.Tatbikat.Rewards, Swell = false };
    }

    /// <summary>Tatbikat bitince onarilacak yapilar: baslangictakinden az cani olanlar (kimlik -> baslangic cani).</summary>
    public static Dictionary<string, float> Repairs(AbilitiesFile f, TatbikatRun run, IReadOnlyDictionary<string, float> now)
    {
        var r = new Dictionary<string, float>();
        if (!f.Tatbikat.RepairFree) return r;
        foreach (var (id, hp) in run.Hp)
            if (now.TryGetValue(id, out var cur) && cur < hp) r[id] = hp;
        return r;
    }
}
