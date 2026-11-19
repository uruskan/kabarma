using System.Globalization;
using System.Text;

namespace Kabarma.Sim;

// denge (M6): basliksiz denge kosucusu. Veriden egrileri hesaplar; SayilarData/*.json'da, gerekce docs/DENGE.md.
// Testler (BalanceTests) egrilerin tekduze ve sicramasiz olmasini denetler; DumpText ayni tablolari yazdirir.

public sealed record KsRow(int Ks, float HpMult, float DamageMult, int Enemies, float TotalHp, float MayaMult, float PassRate1, float PassRate4);
public sealed record TierRow(int Tier, float Value, int Count);

public static class BalanceReport
{
    static readonly string[] BiomeOrder = { "altin_cayir", "sis_korusu", "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" };
    public static int BiomeTier(string biome) { int i = Array.IndexOf(BiomeOrder, biome); return i < 0 ? 1 : i + 1; }

    /// <summary>KS 1..maxKs: dalga toplam cani, dusman sayisi, Maya carpani, gecme orani (1 ve 4 oyuncu, runs tohum).</summary>
    public static List<KsRow> KsCurve(GameData d, int maxKs = 50, int runs = 40)
    {
        var rows = new List<KsRow>();
        var b = d.Kabarma.Full.Balance;
        int oldRuns = b.Runs; b.Runs = runs;
        try
        {
            for (int ks = 1; ks <= maxKs; ks++)
            {
                int tier = Math.Max(1, (int)MathF.Ceiling(ks / Math.Max(0.1f, b.KsPerOcakTier)));
                var spec = new KabarmaSpec { Kind = KabarmaKind.Full, Ks = ks, Players = 1, Biome = "altin_cayir", OcakTier = tier, Seed = 7,
                    PlantedFamilies = new() { ["root"] = 8, ["grain"] = 8, ["gourd"] = 4 } };
                var plan = Waves.Compose(d, spec);
                float total = 0; int n = 0;
                foreach (var w in plan.Waves) foreach (var s in w.Slots) { total += Waves.NewKabaran(d, plan, s, 0, w.Index, 0, 0).MaxHp; n++; }
                rows.Add(new KsRow(ks, Waves.HpMult(d.Kabarma, ks), Waves.DamageMult(d.Kabarma, ks), n, total, Waves.MayaKsMult(d.Kabarma, ks),
                    WaveBalance.PassRate(d, b, ks, 1), WaveBalance.PassRate(d, b, ks, 4)));
            }
        }
        finally { b.Runs = oldRuns; }
        return rows;
    }

    /// <summary>Yaratik can ve hasari: biyom basamagina gore ortalama.</summary>
    public static List<TierRow> CreatureHp(GameData d) => d.Creatures.Creatures.Where(c => c.Hp > 0).GroupBy(c => BiomeTier(c.Biome)).OrderBy(g => g.Key)
        .Select(g => new TierRow(g.Key, g.Average(c => c.Hp), g.Count())).ToList();
    public static List<TierRow> CreatureDamage(GameData d) => d.Creatures.Creatures.Where(c => c.Hp > 0).GroupBy(c => BiomeTier(c.Biome)).OrderBy(g => g.Key)
        .Select(g => new TierRow(g.Key, g.Average(c => c.Damage), g.Count())).ToList();

    /// <summary>Ata cani (dosya sirasi = basamak), Azman cani.</summary>
    public static List<(string id, float hp)> AtaHp(GameData d) => d.Atalar.Atalar.Select(a => (a.Id, a.Hp)).ToList();
    public static List<(string id, float hp)> AzmanHp(GameData d) => d.Azman.Azmanlar.Select(a => (a.Id, a.Hp)).ToList();

    /// <summary>Baslik saniye hasari (tek hedef): damage / fireInterval; kusam basliklari.</summary>
    public static List<(string id, int tier, float dps)> HeadDps(GameData d)
    {
        var l = new List<(string, int, float)>
        {
            (d.Tool.Weapon.Id, 1, d.Tool.Weapon.Damage / d.Tool.Weapon.FireInterval),
            (d.Tool.Nailgun.Id, 1, d.Tool.Nailgun.Damage / d.Tool.Nailgun.FireInterval),
        };
        foreach (var h in d.Tool.WeaponHeads)
        {
            float iv = h.FireInterval > 0 ? h.FireInterval : 1f;
            float dps = h.Kind == "beam" || h.Kind == "cone" || h.Kind == "seed" || h.Kind == "chain" ? h.Damage / iv : h.Damage / Math.Max(iv, h.ChargeSeconds + 0.5f);
            if (h.Kind == "chain") dps *= 1f + (h.ChainCount - 1) * 0.5f; // coklu hedef payi
            if (h.Splash > 0) dps *= 1.5f;
            l.Add((h.Id, h.Tier, dps));
        }
        return l;
    }

    /// <summary>Guc hasari / Sarj (ayni sarjda ne kadar hasar): yalniz hasar veren gucler.</summary>
    public static List<(string id, float damagePerCharge)> PowerEfficiency(GameData d) =>
        d.Abilities.Abilities.Where(a => !a.IsUlti && a.Damage > 0 && a.Charge > 0)
            .Select(a => (a.Id, (a.Duration > 0 && a.Id == "alev_cemberi" ? a.Damage * a.Duration : a.Damage) / a.Charge)).ToList();

    /// <summary>Uretim suresi: tarifin basamagi (unlockOn ocak:N, yoksa 1) basina ortalama sn.</summary>
    /// <summary>Basamak: unlockOn ocak:N ise N; yoksa arastirma dugumunun ya da istasyonunun dugum basamagi; yoksa 1.</summary>
    public static int RecipeTier(GameData d, RecipeDef r)
    {
        if (r.UnlockOn != null && r.UnlockOn.StartsWith("ocak:") && int.TryParse(r.UnlockOn[5..], out var t)) return t;
        var n = d.Research.NodeFor(r);
        if (n != null) return n.Tier;
        var sn = d.Research.Nodes.FirstOrDefault(x => x.Id == r.Station);
        return sn?.Tier ?? 1;
    }

    public static List<TierRow> CraftSeconds(GameData d) => d.Tool.Recipes.Where(r => r.Seconds > 0 && r.Category != "yemek")
        .GroupBy(r => RecipeTier(d, r)).OrderBy(g => g.Key)
        .Select(g => new TierRow(g.Key, g.Average(r => r.Seconds), g.Count())).ToList();

    /// <summary>Tohum paketi: ekin kredi getirisi (taban fiyat x verim) / tohum basina bedel.</summary>
    public static List<(string id, float ratio)> SeedPayback(GameData d)
    {
        var l = new List<(string, float)>();
        foreach (var e in d.Catalog.Entries.Where(e => e.Kind == "tohum" && e.Price > 0))
            foreach (var (item, n) in e.Items)
            {
                var c = d.Crops.Crops.FirstOrDefault(x => x.SeedId == item);
                if (c != null && n > 0) l.Add((e.Id, c.BasePrice * c.BaseYield / (e.Price / (float)n)));
            }
        return l;
    }

    /// <summary>Ekin Kredi/gun: basePrice x baseYield / days; biyom basamagina gore ortalama.</summary>
    public static List<TierRow> CropValuePerDay(GameData d) => d.Crops.Crops.Where(c => c.Days > 0)
        .GroupBy(c => BiomeTier(c.Band == "cayir" ? "altin_cayir" : c.Band == "koru" ? "sis_korusu" : c.Band == "sazlik" ? "isil_sazlik" : c.Band == "kanyon" ? "kizil_kanyon" : c.Band == "ayaz" ? "ayaz_yaylasi" : "gobek"))
        .OrderBy(g => g.Key).Select(g => new TierRow(g.Key, g.Average(c => c.BasePrice * c.BaseYield / c.Days), g.Count())).ToList();

    /// <summary>Tohum Carki beklenen degeri / bedel.</summary>
    public static float WheelEvRatio(GameData d)
    {
        float tw = d.Wheel.Rewards.Sum(r => r.Weight);
        float ev = d.Wheel.Rewards.Sum(r => r.Weight * Wheel.ValueOf(r)) / Math.Max(0.001f, tw);
        return ev / Math.Max(1f, d.Wheel.Cost);
    }

    public static string DumpText(GameData d)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("KS egrisi (KS, hpMult, dmgMult, dusman, toplamHp, mayaMult, gecme1, gecme4)");
        foreach (var r in KsCurve(d, 50, 30)) sb.AppendLine(string.Format(inv, "{0,2} {1,8:0.00} {2,6:0.00} {3,4} {4,10:0} {5,5:0.00} {6,5:0.00} {7,5:0.00}", r.Ks, r.HpMult, r.DamageMult, r.Enemies, r.TotalHp, r.MayaMult, r.PassRate1, r.PassRate4));
        void T(string name, IEnumerable<TierRow> rows) { sb.AppendLine(name); foreach (var r in rows) sb.AppendLine(string.Format(inv, "  T{0}: {1:0.0} (n={2})", r.Tier, r.Value, r.Count)); }
        T("Yaratik can (biyom basamagi)", CreatureHp(d)); T("Yaratik hasar", CreatureDamage(d));
        sb.AppendLine("Ata can"); foreach (var (id, hp) in AtaHp(d)) sb.AppendLine($"  {id}: {hp:0}");
        sb.AppendLine("Azman can"); foreach (var (id, hp) in AzmanHp(d)) sb.AppendLine($"  {id}: {hp:0}");
        sb.AppendLine("Baslik dps"); foreach (var (id, t, dps) in HeadDps(d)) sb.AppendLine(string.Format(inv, "  {0} T{1}: {2:0.0}", id, t, dps));
        sb.AppendLine("Guc hasar/sarj"); foreach (var (id, e) in PowerEfficiency(d)) sb.AppendLine(string.Format(inv, "  {0}: {1:0.00}", id, e));
        T("Uretim sn (basamak)", CraftSeconds(d)); T("Ekin Kredi/gun (biyom)", CropValuePerDay(d));
        sb.AppendLine("Tohum paketi getirisi (ekin geliri / tohum bedeli)"); foreach (var (id, r) in SeedPayback(d)) sb.AppendLine(string.Format(inv, "  {0}: {1:0.00}", id, r));
        sb.AppendLine(string.Format(inv, "Cark EV/bedel: {0:0.00} (hedef {1:0.00})", WheelEvRatio(d), d.Wheel.TargetEv));
        return sb.ToString();
    }
}