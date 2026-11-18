using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Tam Kabarma sozlesmesi (kabarma3, GDD 14.1-14.6): dalga kurallari, bilesim, kuyruk, Kazan, ilk gecis, kayit.</summary>
public class WaveTests
{
    static readonly GameData D = TestData.Load();
    static FullKabarmaDef F => D.Kabarma.Full;

    static KabarmaSpec Spec(int ks, int players = 1, Dictionary<string, int>? fam = null, KabarmaKind kind = KabarmaKind.Full, ulong seed = 7) =>
        new() { Kind = kind, Ks = ks, Players = players, Biome = "altin_cayir", PlantedFamilies = fam ?? new() { ["grain"] = 10, ["root"] = 10 }, Seed = seed };

    static int WeightOf(IEnumerable<WaveSlot> s) => s.Where(x => !x.Azman).Sum(x => D.Creature(x.Species).Weight);

    [Fact]
    public void Count_power_and_elite_formulas_match_gdd_14_3()
    {
        Assert.Equal(38, Waves.Count(F, 1, 1));
        Assert.Equal(1f + 0.6f * MathF.Pow(3, 0.85f), Waves.PlayerMult(F, 4), 4);
        Assert.Equal((int)MathF.Round(110 * Waves.PlayerMult(F, 4)), Waves.Count(F, 10, 4));
        Assert.Equal(0.02f, Waves.EliteRate(F, 1), 4);
        Assert.Equal(0.4f, Waves.EliteRate(F, 30), 4);
        Assert.Equal(MathF.Pow(1.15f, 9), Waves.HpMult(D.Kabarma, 10), 3);
        Assert.Equal(MathF.Pow(1.08f, 9), Waves.DamageMult(D.Kabarma, 10), 3);
    }

    [Fact]
    public void Plan_has_eight_waves_and_doruk_filled_by_weight_same_seed_same_plan()
    {
        var a = Waves.Compose(D, Spec(3));
        var b = Waves.Compose(D, Spec(3));
        Assert.Equal(9, a.Waves.Count);
        Assert.True(a.Waves[^1].Doruk);
        Assert.All(a.Waves.Take(8), w => Assert.InRange(WeightOf(w.Slots), Waves.Count(F, 3, 1) - 1, Waves.Count(F, 3, 1) + 7));
        Assert.Equal(JsonSerializer.Serialize(a.Waves), JsonSerializer.Serialize(b.Waves));
        Assert.Equal(120f, a.AlarmSeconds);
        Assert.Equal(75f, a.Waves[0].Seconds);
        Assert.Equal(15f, a.Waves[0].GapAfter);
        Assert.Equal(120f, a.Waves[^1].Seconds);
    }

    [Fact]
    public void What_you_plant_is_what_you_draw_half_from_planted_families()
    {
        var plan = Waves.Compose(D, Spec(4, fam: new() { ["grain"] = 20 }));
        var grain = F.Families["grain"].ToHashSet();
        var slots = plan.Waves.Take(8).SelectMany(w => w.Slots).ToList();
        float grainW = WeightOf(slots.Where(s => grain.Contains(s.Species)));
        float allW = WeightOf(slots);
        // %50 ekili + yerli payindan gelen ayni turler (gidak, cekirge yerli de): en az yari.
        Assert.True(grainW / allW >= 0.5f, $"tahil payi {grainW / allW:0.00}");
        Assert.Contains(slots, s => F.Natives["sis_korusu"].Contains(s.Species)); // komsu biyom %20
    }

    [Fact]
    public void No_crops_all_natives_count_times_point_six_half_maya()
    {
        var plan = Waves.Compose(D, Spec(2, fam: new()));
        Assert.True(plan.NoCrops);
        Assert.Equal(F.NoCropMayaMult, plan.MayaMult);
        var natives = F.Natives["altin_cayir"].ToHashSet();
        Assert.All(plan.Waves.SelectMany(w => w.Slots).Where(s => !s.Azman), s => Assert.Contains(s.Species, natives));
        int full = Waves.Count(F, 2, 1);
        Assert.InRange(WeightOf(plan.Waves[0].Slots), (int)(full * 0.6f) - 1, (int)(full * 0.6f) + 6);
    }

    [Fact]
    public void Family_without_species_goes_to_natives()
    {
        var plan = Waves.Compose(D, Spec(2, fam: new() { ["fiber"] = 10 }));
        var allowed = F.Natives["altin_cayir"].Concat(F.Natives["sis_korusu"]).ToHashSet();
        Assert.All(plan.Waves.SelectMany(w => w.Slots), s => Assert.Contains(s.Species, allowed));
        var nat = F.Natives["altin_cayir"].ToHashSet();
        var slots = plan.Waves.Take(8).SelectMany(w => w.Slots).ToList();
        Assert.True(WeightOf(slots.Where(s => nat.Contains(s.Species))) / (float)WeightOf(slots) > 0.6f);
    }

    [Fact]
    public void Doruk_is_azman_every_fifth_ks_else_all_elite_and_swarms_never_elite()
    {
        var k5 = Waves.Compose(D, Spec(5));
        Assert.Contains(k5.Waves[^1].Slots, s => s.Azman);
        var k4 = Waves.Compose(D, Spec(4));
        Assert.DoesNotContain(k4.Waves[^1].Slots, s => s.Azman);
        Assert.All(k4.Waves[^1].Slots.Where(s => !s.Swarm), s => Assert.False(string.IsNullOrEmpty(s.Elite)));
        Assert.All(k4.Waves.SelectMany(w => w.Slots).Where(s => s.Swarm), s => Assert.Equal("", s.Elite));
        var k30 = Waves.Compose(D, Spec(30, 8));
        var plain = k30.Waves.Take(8).SelectMany(w => w.Slots).Where(s => !s.Swarm).ToList();
        float rate = plain.Count(s => s.Elite != "") / (float)plain.Count;
        Assert.InRange(rate, 0.33f, 0.47f);
    }

    [Fact]
    public void Kabaran_state_carries_ks_and_elite_multipliers()
    {
        var plan = Waves.Compose(D, Spec(10));
        var dev = new WaveSlot { Species = "homur", Elite = "dev" };
        var k = Waves.NewKabaran(D, plan, dev, 1, 0, 3, 4);
        var homur = D.Creature("homur");
        Assert.Equal(homur.Hp * plan.HpMult * 2.5f, k.MaxHp, 2);
        Assert.Equal(homur.Damage * plan.DamageMult * 1.5f, k.Damage, 2);
        Assert.Equal(1.6f, k.Scale);
        Assert.Equal(50f, Waves.AfterArmor(100f, 100f), 3);
    }

    static KabarmaNight Run(KabarmaNight n, float seconds, int active = 0, List<(WaveSlot, int)>? got = null)
    {
        for (float t = 0; t < seconds && n.Phase != NightPhase.Dawn; t += 0.5f)
        {
            n.Tick(0.5f);
            var due = n.TakeDue(active);
            got?.AddRange(due);
        }
        return n;
    }

    [Fact]
    public void Queue_respects_220_and_overflow_carries_then_drops_at_dawn()
    {
        var plan = Waves.Compose(D, Spec(30, 8));
        var n = new KabarmaNight(D, plan, 3);
        var got = new List<(WaveSlot, int)>();
        Run(n, plan.AlarmSeconds + 1f);
        Assert.Equal(NightPhase.Waves, n.Phase);
        // Sahne dolu: hic giris yok.
        Assert.Empty(n.TakeDue(plan.MaxActive));
        n.Tick(2f); // yeni girisler vadesine gelsin
        // 219 dolu: tek giris.
        Assert.Single(n.TakeDue(plan.MaxActive - 1));
        // Ilk dalga suresince sahne bos sayilirsa planin tamami girer; dolu kalirsa tasar.
        Run(n, 60f, active: plan.MaxActive, got);
        Assert.Empty(got);
        Assert.True(n.PendingSpawns > 0, "tasma kuyrukta");
        int before = n.PendingSpawns;
        Run(n, 30f, active: plan.MaxActive); // ikinci dalgaya gecti: tasanlar onde
        Assert.True(n.WaveIndex >= 1);
        Assert.True(n.PendingSpawns >= before);
        Run(n, 2000f, active: 220);
        Assert.Equal(NightPhase.Dawn, n.Phase);
        Assert.Equal(0, n.PendingSpawns);
        Assert.Equal(KabarmaEnd.Dawn, n.EndReason);
    }

    [Fact]
    public void Kazan_topple_ends_the_night_and_halves_maya_rout_costs_a_quarter()
    {
        var plan = Waves.Compose(D, Spec(1));
        var n = new KabarmaNight(D, plan, 3);
        Run(n, plan.AlarmSeconds + 1f);
        for (int i = 0; i < 10; i++) n.OnKill(D.Creature("homur"));
        float maya = n.Maya;
        Assert.True(maya > 0);
        float max = n.Kazan!.MaxHp;
        Assert.Equal(Waves.KazanHp(F, 1), max);
        Assert.False(n.Rout());
        Assert.Equal(max * 0.75f, n.Kazan.Hp, 2);
        KabarmaEnd? ended = null;
        n.Ended += e => ended = e;
        Assert.True(n.DamageKazan(max));
        Assert.Equal(NightPhase.Dawn, n.Phase);
        Assert.Equal(KabarmaEnd.KazanToppled, ended);
        Assert.Equal(maya * 0.5f, n.Maya, 2);
    }

    [Fact]
    public void Gosteri_events_raise_and_going_down_resets()
    {
        var n = new KabarmaNight(D, Waves.Compose(D, Spec(1)), 3);
        n.ReportGosteri(GosteriEvent.EnvironmentKill);
        Assert.Equal(1f + D.Kabarma.Gosteri.Events["EnvironmentKill"], n.Gosteri, 3);
        n.RecordPlayerDown();
        Assert.Equal(D.Kabarma.Gosteri.Min, n.Gosteri);
    }

    [Fact]
    public void First_clear_unlocks_next_ks_and_gives_token_and_credits_once()
    {
        var s = new GameState(D);
        Assert.Equal(1, s.UnlockedKs);
        int credits = s.Credits;
        var n = s.StartKabarma(s.KabarmaSpecFor(KabarmaKind.Full, 1, 1, 0, 0));
        Assert.Equal(KabarmaKind.Full, n.Kind);
        Run(n, 5000f);
        Assert.Equal(NightPhase.Dawn, n.Phase);
        Assert.Equal(1, s.Kabarma.HighestCleared);
        Assert.Equal(2, s.UnlockedKs);
        Assert.Equal(1, s.Kabarma.CardTokens);
        Assert.Equal(credits + F.FirstClearCredits, s.Credits);
        var again = s.StartKabarma(s.KabarmaSpecFor(KabarmaKind.Full, 1, 1, 0, 0));
        Run(again, 5000f);
        Assert.Equal(1, s.Kabarma.CardTokens);
        var t = s.StartKabarma(s.KabarmaSpecFor(KabarmaKind.Full, 2, 1, 0, 0));
        Run(t, 200f);
        t.DamageKazan(t.Kazan!.MaxHp);
        Assert.Equal(1, s.Kabarma.HighestCleared);
        Assert.Equal(1, s.Kabarma.Toppled);
    }

    [Fact]
    public void Small_kabarma_halves_ks_three_card_levels_no_kazan_drill_gives_no_maya()
    {
        var small = Waves.Compose(D, Spec(6, kind: KabarmaKind.Small));
        Assert.Equal(3, small.Ks);
        Assert.Equal(F.Small.WaveCount, small.Waves.Count);
        Assert.Equal(3, small.MaxCardLevels);
        Assert.True(small.Waves.Sum(w => w.Seconds + w.GapAfter) <= F.Small.Seconds + 1f);
        Assert.Null(new KabarmaNight(D, small, 1).Kazan);
        var drill = Waves.Compose(D, Spec(3, kind: KabarmaKind.Drill));
        Assert.Equal(0f, drill.MayaMult);
        var n = new KabarmaNight(D, drill, 1);
        n.OnKill(D.Creature("homur"));
        Assert.Equal(0f, n.Maya);
    }

    [Fact]
    public void Kabarma_progress_round_trips_through_its_save_section()
    {
        var s = new GameState(D);
        var n = s.StartKabarma(s.KabarmaSpecFor(KabarmaKind.Full, 1, 1, 0, 0));
        Run(n, 5000f);
        var json = JsonSerializer.Serialize(s.Kabarma);
        var back = JsonSerializer.Deserialize<KabarmaProgress>(json)!;
        Assert.Equal(1, back.HighestCleared);
        Assert.Equal(1, back.CardTokens);
    }
}
