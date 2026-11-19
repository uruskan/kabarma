using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Oyuncu gucleri, Kadim Tas denemeleri, Maya Feneri ve Tatbikat (GDD 14.6, 14.7, 17.1; ICERIK 7).</summary>
public class AbilityTests
{
    static AbilitiesFile F() => TestData.Load().Abilities;

    static (AbilityProgress p, AbilityLoadout l) Unlocked(AbilitiesFile f, params string[] ids)
    {
        var p = new AbilityProgress();
        foreach (var id in ids) AbilityRules.Unlock(f, p, id);
        return (p, p.Local);
    }

    [Fact]
    public void Data_has_five_powers_two_ultis_five_stones_each_with_a_power()
    {
        var f = F();
        Assert.Equal(12, f.Abilities.Count(a => !a.IsUlti)); // M4 kusam: PLAN 7.1 Guc 12 (5 M3 + 7 M4)
        Assert.Equal(4, f.Abilities.Count(a => a.IsUlti));   // Ulti 4
        Assert.Equal(12, f.Stones.Count);                    // 5 M3 + 7 M4 Kadim Tas (her gucun bir tasi)
        foreach (var s in f.Stones) Assert.False(f.Def(s.Ability)!.IsUlti);
        Assert.Equal(f.Abilities.Where(a => !a.IsUlti).Select(a => a.Id).OrderBy(x => x), f.Stones.Select(s => s.Ability).OrderBy(x => x));
        // GDD 17: Sarj 25-50, bekleme 8-30 sn
        foreach (var a in f.Abilities.Where(a => !a.IsUlti))
        {
            Assert.InRange(a.Charge, 25, 50);
            Assert.InRange(a.Cooldown, 8, 30);
        }
        // Ultiler Ata odulu: atalar.json'daki ultiler burada tanimli
        foreach (var ata in TestData.Load().Atalar.Atalar.Where(a => !string.IsNullOrEmpty(a.Reward.Ulti)))
            Assert.True(f.Def(ata.Reward.Ulti)?.IsUlti == true, ata.Reward.Ulti);
    }

    [Fact]
    public void Unlock_auto_equips_into_the_one_open_slot_second_slot_is_locked()
    {
        var f = F();
        var (p, l) = Unlocked(f, "atilma", "sok_dalgasi");
        Assert.Equal("atilma", l.Slots[0]);
        Assert.Equal("", l.Slots[1]);
        Assert.Equal("Bu yuva henüz kilitli", AbilityRules.Equip(f, p, Array.Empty<string>(), l, 1, "sok_dalgasi"));
        Assert.Null(AbilityRules.Equip(f, p, Array.Empty<string>(), l, 0, "sok_dalgasi"));
        Assert.Equal("sok_dalgasi", l.Slots[0]);
        Assert.Equal("Bu güç henüz açılmadı", AbilityRules.Equip(f, p, Array.Empty<string>(), l, 0, "sifa_sisi"));
    }

    [Fact]
    public void Use_spends_charge_starts_cooldown_and_charge_regenerates_after_delay()
    {
        var f = F();
        var (_, l) = Unlocked(f, "atilma");
        var def = AbilityRules.Use(f, l, "atilma");
        Assert.NotNull(def);
        Assert.Equal(75f, l.Charge);
        Assert.Equal("Bekleniyor", AbilityRules.CanUse(f, l, "atilma"));
        AbilityRules.Tick(f, l, 1.0f);
        Assert.Equal(75f, l.Charge); // gecikme 1,5 sn
        AbilityRules.Tick(f, l, 1.0f);
        Assert.InRange(l.Charge, 77f, 78f);
        AbilityRules.Tick(f, l, 6.0f);
        Assert.Null(AbilityRules.CanUse(f, l, "atilma")); // 8 sn bekleme bitti
        AbilityRules.Tick(f, l, 30f);
        Assert.Equal(100f, l.Charge);
    }

    [Fact]
    public void Not_enough_charge_blocks_use()
    {
        var f = F();
        var (_, l) = Unlocked(f, "sifa_sisi");
        l.Charge = 40;
        Assert.Equal("Şarj yetmiyor", AbilityRules.CanUse(f, l, "sifa_sisi"));
        Assert.Null(AbilityRules.Use(f, l, "sifa_sisi"));
        Assert.Equal(40, l.Charge);
    }

    [Fact]
    public void Dash_gives_iframes_then_damage_returns()
    {
        var f = F();
        var (_, l) = Unlocked(f, "atilma");
        AbilityRules.Use(f, l, "atilma");
        Assert.Equal(0f, AbilityRules.FilterDamage(f, l, 10));
        AbilityRules.Tick(f, l, 0.4f);
        Assert.Equal(10f, AbilityRules.FilterDamage(f, l, 10));
    }

    [Fact]
    public void Ulti_meter_fills_from_kills_and_ata_damage_then_giant_mode_halves_damage()
    {
        var f = F();
        var l = new AbilityLoadout();
        var p = new AbilityProgress();
        Assert.Equal("Bu ulti henüz açılmadı", AbilityRules.Equip(f, p, Array.Empty<string>(), l, -1, "dev_mod"));
        Assert.Null(AbilityRules.Equip(f, p, new[] { "dev_mod" }, l, -1, "dev_mod"));
        Assert.Equal("Maya ölçeri dolmadı", AbilityRules.CanUse(f, l, "dev_mod"));
        for (int i = 0; i < 20; i++) AbilityRules.OnKill(f, l, false); // 40
        AbilityRules.OnKill(f, l, true);                               // 46
        AbilityRules.OnAtaDamage(f, l, 0.27f);                         // +54 = 100
        Assert.Equal(100f, l.Meter, 3);
        Assert.NotNull(AbilityRules.Use(f, l, "dev_mod"));
        Assert.Equal(0f, l.Meter);
        Assert.Equal(5f, AbilityRules.FilterDamage(f, l, 10));
        AbilityRules.Tick(f, l, 20.1f);
        Assert.Equal(10f, AbilityRules.FilterDamage(f, l, 10));
    }

    [Fact]
    public void Shock_wave_pushes_outward_lighter_further()
    {
        var def = F().Def("sok_dalgasi")!;
        var (x, z) = AbilityRules.Knock(def, 0, 0, 3, 0);
        Assert.Equal(def.Knock, x, 3);
        Assert.Equal(0f, z, 3);
        var (hx, _) = AbilityRules.Knock(def, 0, 0, 3, 0, mass: 4f);
        Assert.True(hx < x);
        Assert.True(AbilityRules.InRadius(0, 0, def.Radius, 4, 4));
        Assert.False(AbilityRules.InRadius(0, 0, def.Radius, 5, 5));
    }

    // ---------- Kadim Tas ----------

    static TrialSense At(float dx, float dz, float h = 1f) => new() { Dx = dx, Dz = dz, Height = h };

    [Fact]
    public void Parkour_rings_in_order_win_unlocks_stone()
    {
        var f = F();
        var p = new KadimTasProgress();
        var s = f.Stone("kadim_atilma")!;
        var run = KadimTasRules.Start(f, p, s.Id, out _)!;
        // Sira disi halka sayilmaz
        var last = s.Rings[^1];
        KadimTasRules.Tick(f, p, run, At(last[0], last[1], last[2]), 0.1f);
        Assert.Equal(0, run.Ring);
        foreach (var g in s.Rings) KadimTasRules.Tick(f, p, run, At(g[0], g[1], g[2]), 1f);
        Assert.True(run.Won);
        Assert.Contains(s.Id, p.Done);
    }

    [Fact]
    public void Parkour_timeout_loses_and_stone_rests_before_retry()
    {
        var f = F();
        var p = new KadimTasProgress();
        var s = f.Stone("kadim_atilma")!;
        var run = KadimTasRules.Start(f, p, s.Id, out _)!;
        var step = KadimTasRules.Tick(f, p, run, At(0, 0), s.Seconds + 1);
        Assert.True(step.Lost);
        Assert.Equal("Süre doldu", run.Reason);
        Assert.Null(KadimTasRules.Start(f, p, s.Id, out var why));
        Assert.StartsWith("Taş dinleniyor", why);
        KadimTasRules.TickRetry(p, f.Trial.RetrySeconds + 0.1f);
        Assert.NotNull(KadimTasRules.Start(f, p, s.Id, out _));
    }

    [Fact]
    public void Guard_trial_spawns_every_n_seconds_and_fails_when_gidak_falls()
    {
        var f = F();
        var p = new KadimTasProgress();
        var s = f.Stone("kadim_kalkan")!;
        var run = KadimTasRules.Start(f, p, s.Id, out _)!;
        int spawned = 0;
        for (int i = 0; i < 30; i++) spawned += KadimTasRules.Tick(f, p, run, At(1, 1), 1f).Spawn;
        Assert.Equal(3 * s.PerWave, spawned); // 0, 10, 20. sn
        var sense = At(1, 1); sense.GuardDamage = s.GuardHp;
        Assert.True(KadimTasRules.Tick(f, p, run, sense, 0.1f).Lost);
        Assert.Equal("Gıdak düştü", run.Reason);

        var run2 = new TrialRun { StoneId = s.Id, GuardHp = s.GuardHp };
        for (float t = 0; t <= s.Seconds + 1; t += 1f) KadimTasRules.Tick(f, p, run2, At(1, 1), 1f);
        Assert.True(run2.Won);
    }

    [Fact]
    public void Wave_trial_advances_when_cleared_and_down_loses()
    {
        var f = F();
        var p = new KadimTasProgress();
        var s = f.Stone("kadim_sok")!;
        var run = KadimTasRules.Start(f, p, s.Id, out _)!;
        var spawns = new List<int>();
        for (int i = 0; i < s.Waves.Count; i++)
        {
            var st = KadimTasRules.Tick(f, p, run, At(0, 0), 0.1f); // alive 0: siradaki dalga
            spawns.Add(st.Spawn);
            var busy = At(0, 0); busy.Alive = st.Spawn;
            Assert.Equal(0, KadimTasRules.Tick(f, p, run, busy, 1f).Spawn);
        }
        Assert.Equal(s.Waves, spawns);
        Assert.True(KadimTasRules.Tick(f, p, run, At(0, 0), 0.1f).Won);

        var heal = f.Stone("kadim_sifa")!;
        var r2 = KadimTasRules.Start(f, p, heal.Id, out _)!;
        var down = At(0, 0); down.Down = true;
        Assert.True(KadimTasRules.Tick(f, p, r2, down, 0.1f).Lost);
        Assert.Equal("Ekipten biri yere düştü", r2.Reason);
    }

    [Fact]
    public void Stealth_trial_watchers_see_in_cone_only()
    {
        var f = F();
        var s = f.Stone("kadim_yem")!;
        var w = s.Watchers[0]; // [-6, 12, 90]: +X'e bakar
        Assert.True(KadimTasRules.Seen(s, w[0] + 5, w[1]));
        Assert.False(KadimTasRules.Seen(s, w[0] - 5, w[1]));            // arkasinda
        Assert.False(KadimTasRules.Seen(s, w[0] + s.Sight + 1, w[1]));  // gorus menzili disinda

        var p = new KadimTasProgress();
        var run = KadimTasRules.Start(f, p, s.Id, out _)!;
        KadimTasRules.Tick(f, p, run, At(w[0] + 3, w[1]), 0.3f);
        Assert.False(run.Over);
        KadimTasRules.Tick(f, p, run, At(w[0] + 3, w[1]), 0.3f);
        Assert.True(run.Lost);
        Assert.Equal("Görüldün", run.Reason);
    }

    [Fact]
    public void Stealth_trial_has_an_unseen_path_to_the_goal()
    {
        // Tasarim denetimi: tas (0,0) ile hedef arasinda gorulmeden gecilen bir yol var (izgara uzerinde arama).
        var s = F().Stone("kadim_yem")!;
        const float step = 0.5f;
        int n = (int)(60 / step);
        var seen = new bool[n, n];
        (int, int) Cell(float x, float z) => ((int)((x + 30) / step), (int)((z + 10) / step));
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) seen[i, j] = KadimTasRules.Seen(s, -30 + i * step, -10 + j * step);
        var start = Cell(0, 0); var goal = Cell(s.Goal[0], s.Goal[1]);
        var q = new Queue<(int, int)>(); var vis = new bool[n, n];
        q.Enqueue(start); vis[start.Item1, start.Item2] = true;
        bool found = false;
        while (q.Count > 0 && !found)
        {
            var (i, j) = q.Dequeue();
            if (Math.Abs(i - goal.Item1) + Math.Abs(j - goal.Item2) <= (int)(s.GoalRadius / step)) found = true;
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = i + di, b = j + dj;
                if (a < 0 || b < 0 || a >= n || b >= n || vis[a, b] || seen[a, b]) continue;
                vis[a, b] = true; q.Enqueue((a, b));
            }
        }
        Assert.True(found, "gizlen denemesinde gorulmeden hedefe yol yok");
    }

    [Fact]
    public void Leaving_the_stone_loses()
    {
        var f = F();
        var p = new KadimTasProgress();
        var run = KadimTasRules.Start(f, p, "kadim_sok", out _)!;
        Assert.True(KadimTasRules.Tick(f, p, run, At(f.Trial.LeaveRadius + 1, 0), 0.1f).Lost);
    }

    // ---------- Fener ve Tatbikat ----------

    [Fact]
    public void Fener_once_per_day_half_ks_capped_cards()
    {
        var f = F();
        var p = new FenerProgress();
        var r = TatbikatRules.LightFener(f, p, "e1", day: 3, highestKs: 7, kabarmaActive: false, 10, 20, out _)!;
        Assert.Equal(3, r.Ks);
        Assert.Equal(40f, r.Radius);
        Assert.Equal(240f, r.Seconds);
        Assert.Equal(3, r.MaxCardLevels);
        Assert.True(r.Swell && r.Rewards && !r.Ghost);
        Assert.Null(TatbikatRules.LightFener(f, p, "e1", 3, 7, false, 10, 20, out var why));
        Assert.StartsWith("Fener bugün yakıldı", why);
        Assert.NotNull(TatbikatRules.LightFener(f, p, "e2", 3, 7, false, 0, 0, out _)); // baska fener
        Assert.NotNull(TatbikatRules.LightFener(f, p, "e1", 4, 7, false, 10, 20, out _)); // ertesi gun
        Assert.Equal(1, TatbikatRules.FenerKs(f, 1));
        Assert.Null(TatbikatRules.LightFener(f, new FenerProgress(), "e3", 1, 1, kabarmaActive: true, 0, 0, out _));
    }

    [Fact]
    public void Tatbikat_is_ghost_rewardless_and_repairs_damaged_pieces()
    {
        var f = F();
        Assert.Null(TatbikatRules.Tatbikat(f, 5, highestKs: 3, false, out var why));
        Assert.Equal("KS 5 açılmadı", why);
        var r = TatbikatRules.Tatbikat(f, 2, 3, false, out _)!;
        Assert.True(r.Ghost);
        Assert.False(r.Rewards);
        Assert.False(r.Swell);
        var run = new TatbikatRun { Ks = 2, Hp = new() { ["a"] = 400, ["b"] = 300, ["c"] = 100 } };
        var now = new Dictionary<string, float> { ["a"] = 400, ["b"] = 120 }; // c yikildi
        var fix = TatbikatRules.Repairs(f, run, now);
        Assert.Equal(new Dictionary<string, float> { ["b"] = 300 }, fix);
    }

    [Fact]
    public void Abilities_save_section_round_trips()
    {
        var f = F();
        var s = new GameState(TestData.Load());
        AbilityRules.Unlock(f, s.Abilities, "atilma");
        s.Abilities.Local.Meter = 42;
        s.KadimTas.Done.Add("kadim_atilma");
        s.Fener.LitDay["e1"] = 2;
        var json = JsonSerializer.Serialize(new AbilitiesSave { Abilities = s.Abilities, KadimTas = s.KadimTas, Fener = s.Fener });
        var back = JsonSerializer.Deserialize<AbilitiesSave>(json)!;
        Assert.Equal(new[] { "atilma" }, back.Abilities.Unlocked);
        Assert.Equal("atilma", back.Abilities.Local.Slots[0]);
        Assert.Equal(42f, back.Abilities.Local.Meter);
        Assert.Equal(new[] { "kadim_atilma" }, back.KadimTas.Done);
        Assert.Equal(2, back.Fener.LitDay["e1"]);
    }
    [Fact]
    public void Every_m4_power_unlocks_through_its_kadim_tas_trial()
    {
        var d = TestData.Load();
        var f = d.Abilities;
        var biomes = new HashSet<string>(new[] { "altin_cayir", "sis_korusu", "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" });
        var ids = new[] { "kadim_bal", "kadim_kuyu", "kadim_roket", "kadim_alev", "kadim_isin", "kadim_duvar", "kadim_pelerin" };
        var powers = new[] { "zaman_bali", "cekim_kuyusu", "roket_ziplama", "alev_cemberi", "isinlan", "buz_duvari", "gorunmez_pelerin" };
        for (int i = 0; i < ids.Length; i++)
        {
            var s = f.Stone(ids[i])!;
            Assert.Equal(powers[i], s.Ability);
            Assert.Contains(s.Biome, biomes);
            Assert.NotEqual("altin_cayir", s.Biome); Assert.NotEqual("sis_korusu", s.Biome); // yeni biyomlar
            var prog = new KadimTasProgress();
            var run = KadimTasRules.Start(f, prog, s.Id, out _);
            Assert.NotNull(run);
            TrialStep step = default;
            for (int t = 0; t < 4000 && !run!.Over; t++)
            {
                var sense = new TrialSense { Height = 1f };
                switch (s.Trial)
                {
                    case "parkur": if (run.Ring < s.Rings.Count) { var g = s.Rings[run.Ring]; sense.Dx = g[0]; sense.Dz = g[1]; sense.Height = g.Length > 2 ? g[2] : 1f; } break;
                    case "gizlen": sense.Dx = s.Goal[0]; sense.Dz = s.Goal[1]; break;
                    case "dalga": sense.Alive = 0; break;
                    case "koru": break;
                }
                step = KadimTasRules.Tick(f, prog, run, sense, 0.25f);
            }
            Assert.True(run!.Won, $"{s.Id} ({s.Trial}) kazanilamadi: {run.Reason}");
            // kazanan gucu acar (Godot: AbilitySystem tasi bitirince AbilityRules.Unlock)
            var ap = new AbilityProgress();
            Assert.True(AbilityRules.Unlock(f, ap, s.Ability));
            Assert.Contains(s.Ability, ap.Unlocked);
        }
    }
}