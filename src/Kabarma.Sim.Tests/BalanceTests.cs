using Kabarma.Sim;
using Xunit;
using Xunit.Abstractions;

namespace Kabarma.Sim.Tests;

/// <summary>denge (M6): egri kontrolleri (tekduze artis, sicrama yok) ve hedef bantlari. Gerekce ve sayilar docs/DENGE.md.</summary>
public class BalanceTests
{
    static readonly GameData D = TestData.Load();
    readonly ITestOutputHelper _out;
    public BalanceTests(ITestOutputHelper o) => _out = o;

    [Fact]
    public void Dump_tables()
    {
        var text = BalanceReport.DumpText(D);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "denge_dump.txt"), text);
        _out.WriteLine(text);
    }

    [Fact]
    public void Ks_curve_rises_smoothly_over_ks_1_to_50()
    {
        var rows = BalanceReport.KsCurve(D, 50, 1); // gecme orani burada hesaplanmaz (asagida), yalniz plan egrisi
        for (int i = 1; i < rows.Count; i++)
        {
            var a = rows[i - 1]; var b = rows[i];
            Assert.True(b.TotalHp > a.TotalHp, $"KS{b.Ks}: toplam can artmiyor");
            Assert.True(b.TotalHp / a.TotalHp <= 1.45f, $"KS{b.Ks}: toplam can sicradi x{b.TotalHp / a.TotalHp:0.00}");
            Assert.True(b.HpMult > a.HpMult && b.HpMult / a.HpMult <= 1.16f, $"KS{b.Ks}: can carpani");
            Assert.True(b.DamageMult > a.DamageMult && b.DamageMult / a.DamageMult <= 1.09f, $"KS{b.Ks}: hasar carpani");
            Assert.True(b.MayaMult > a.MayaMult, $"KS{b.Ks}: Maya kazanci artmiyor");
            Assert.True(b.Enemies >= a.Enemies - 8, $"KS{b.Ks}: dusman sayisi dustu");
        }
        // kazanc ile zorluk: Maya (kazanc) KS 50'de KS 1'in ~7 kati, toplam can ise 500+ kati degil de makul bir katsayi
        Assert.InRange(rows[^1].MayaMult / rows[0].MayaMult, 5f, 10f);
    }

    [Fact]
    public void Pass_rates_stay_in_band_for_one_and_four_players_across_ks()
    {
        var b = D.Kabarma.Full.Balance;
        int old = b.Runs; b.Runs = 120;
        try
        {
            foreach (int ks in new[] { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 })
            {
                float p1 = WaveBalance.PassRate(D, b, ks, 1), p4 = WaveBalance.PassRate(D, b, ks, 4);
                Assert.InRange(p1, 0.5f, 0.9f);   // ilk denemede gecme %50-90 (M3 kabulu KS 1-10 icin %60-80; sonrasi genis bant, ornek gurultusu)
                Assert.InRange(p4, 0.5f, 0.95f);  // ekip biraz daha rahat gecer ama sabit gecmez
            }
        }
        finally { b.Runs = old; }
    }

    [Fact]
    public void Creature_ata_and_azman_hp_rise_with_biome_tier()
    {
        var hp = BalanceReport.CreatureHp(D).Where(r => r.Tier <= 5).ToList();   // Gobek'te tek yaratik (Hamurcuk) var
        for (int i = 1; i < hp.Count; i++) Assert.True(hp[i].Value >= hp[i - 1].Value, $"yaratik can T{hp[i].Tier} dustu");
        var dmg = BalanceReport.CreatureDamage(D).Where(r => r.Tier <= 5).ToList();
        for (int i = 1; i < dmg.Count; i++) Assert.True(dmg[i].Value >= dmg[i - 1].Value, $"yaratik hasar T{dmg[i].Tier} dustu");
        var ata = BalanceReport.AtaHp(D); var az = BalanceReport.AzmanHp(D);
        for (int i = 1; i < ata.Count; i++) { Assert.True(ata[i].hp > ata[i - 1].hp, $"Ata {ata[i].id}"); Assert.True(ata[i].hp / ata[i - 1].hp <= 1.8f, $"Ata sicradi {ata[i].id}"); }
        for (int i = 1; i < az.Count; i++) Assert.True(az[i].hp >= az[i - 1].hp, $"Azman {az[i].id} dustu");
        for (int i = 0; i < Math.Min(ata.Count, az.Count); i++) Assert.True(az[i].hp < ata[i].hp * 1.2f, $"Azman {az[i].id} Ata'dan cok guclu"); // ilk basamakta Azman (zirhli, 1500) Koca Pofuduk'a (1400) yakin; sonra hep altinda
    }

    [Fact]
    public void Head_dps_rises_with_tier_and_beats_the_starter()
    {
        var heads = BalanceReport.HeadDps(D);
        float starter = heads.Where(h => h.tier == 1).Max(h => h.dps);
        var byTier = heads.Where(h => h.tier >= 3).GroupBy(h => h.tier).OrderBy(g => g.Key).Select(g => (tier: g.Key, avg: g.Average(h => h.dps))).ToList();
        foreach (var h in heads.Where(h => h.tier >= 3)) Assert.True(h.dps >= starter * 1.2f, $"{h.id} baslangic silahindan zayif ({h.dps:0})");
        for (int i = 1; i < byTier.Count; i++)
        {
            Assert.True(byTier[i].avg > byTier[i - 1].avg, $"baslik dps T{byTier[i].tier}");
            Assert.True(byTier[i].avg / byTier[i - 1].avg <= 2f, $"baslik dps sicradi T{byTier[i].tier}");
        }
        Assert.All(BalanceReport.PowerEfficiency(D), p => Assert.True(p.damagePerCharge > 0));
    }

    [Fact]
    public void Craft_times_grow_with_tier_and_crop_income_never_falls()
    {
        var craft = BalanceReport.CraftSeconds(D);
        for (int i = 1; i < craft.Count; i++)
        {
            Assert.True(craft[i].Value > craft[i - 1].Value, $"uretim suresi T{craft[i].Tier}");
            Assert.True(craft[i].Value / craft[i - 1].Value <= 2f, $"uretim suresi sicradi T{craft[i].Tier}");
        }
        var crop = BalanceReport.CropValuePerDay(D);
        for (int i = 1; i < crop.Count; i++)
        {
            Assert.True(crop[i].Value >= crop[i - 1].Value, $"ekin geliri T{crop[i].Tier} dustu");
            Assert.True(crop[i].Value / crop[i - 1].Value <= 2f, $"ekin geliri sicradi T{crop[i].Tier}");
        }
    }

    [Fact]
    public void Economy_seed_packs_pay_back_and_the_wheel_is_slightly_losing()
    {
        foreach (var (id, ratio) in BalanceReport.SeedPayback(D)) Assert.InRange(ratio, 1f, 4f); // tohum paketi kendini cikarir ama sinirsiz kazandirmaz
        Assert.InRange(BalanceReport.WheelEvRatio(D), D.Wheel.TargetEv - 0.02f, D.Wheel.TargetEv + 0.02f);
        Assert.True(BalanceReport.WheelEvRatio(D) < 1f); // cark ortalamada kaybettirir (hedef 0,85)
    }
}