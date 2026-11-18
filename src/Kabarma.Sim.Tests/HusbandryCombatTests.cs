using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>M3 (GDD 9.5): Savas Agili ve Ahir, kabarik hayvanin savasi, bayilma ve yorgunluk, ahirin yikilmasi, Ahir Zili, binek.</summary>
public class HusbandryCombatTests
{
    static PenShape Square(float x0, float z0, float size) => new()
    {
        Polygon = new List<P2> { new(x0, z0), new(x0 + size, z0), new(x0 + size, z0 + size), new(x0, z0 + size) },
        Walls = new List<int>(), Area = size * size, HasGate = true,
    };

    static (GameState s, Ranch r) Fresh(ulong seed = 7)
    {
        var s = new GameState(TestData.Load(), seed);
        return (s, s.Ranch);
    }

    static PenAnimal TameIn(Ranch r, string species, PenShape pen, float x, float z, string role)
    {
        var a = r.Wild(species, x, z);
        a.Trust = 1f;
        Assert.True(r.Register(a, pen));
        Assert.True(r.Rules.SetRole(a, role));
        return a;
    }

    static Func<string, float> Hp(GameState s) => id => s.Data.Creature(id).Hp;

    [Fact]
    public void Data_forms_cover_eight_species_and_mounts_are_binek_species()
    {
        var h = TestData.Load().Husbandry;
        Assert.Equal(h.Species.Select(x => x.Id).OrderBy(x => x), h.Forms.Keys.OrderBy(x => x));
        var combat = TestData.Load().Combat;
        foreach (var (id, f) in h.Forms.Where(kv => kv.Value.Fights))
        {
            Assert.True(f.Damage > 0 && f.Interval > 0 && f.HpMult > 0, id);
            Assert.True(Combat.TryParse(f.Type, out _), $"{id}: {f.Type}");
        }
        foreach (var id in h.Mount.Speeds.Keys) Assert.Contains("binek", h.SpeciesOf(id)!.Roles);
        // Savasci rolu alabilenlerin formu dovusur (ICERIK 3: Pofuduk Yastik, Bozuk Dag)
        foreach (var s in h.Species.Where(x => x.Roles.Contains("savasci"))) Assert.True(h.Forms[s.Id].Fights, s.Id);
    }

    [Fact]
    public void War_pen_savasci_swells_others_hide_and_tutorial_is_untouched()
    {
        var (s, r) = Fresh();
        var war = Square(0, 0, 6);
        var barn = Square(20, 0, 6);
        r.AddFlag(1, 1);
        var fighter = TameIn(r, "pofuduk", war, 3, 3, "savasci");
        var worker = TameIn(r, "pofuduk", war, 4, 4, "uretici");
        var stabled = TameIn(r, "pofuduk", barn, 23, 3, "savasci");
        var tut = new PenAnimal { Species = "gidak", Tutorial = true, Trust = 1, Registered = true, X = 2, Z = 2 };
        r.Animals.Add(tut);
        var pens = new[] { war, barn };

        Assert.True(r.IsWarPen(war));
        Assert.False(r.IsWarPen(barn));
        var ev = r.KabarmaBegin(pens, Hp(s));

        Assert.NotNull(fighter.War);
        float expected = s.Data.Creature("pofuduk").Hp * r.Rules.HpMult(fighter) * r.Rules.Def.Forms["pofuduk"].HpMult;
        Assert.Equal(expected, fighter.War!.MaxHp, 3);
        Assert.Equal(CombatTeam.Player, fighter.War.Team);
        Assert.Null(worker.War);
        Assert.True(worker.Hidden);
        Assert.Null(stabled.War);  // bayraksiz agil: Ahir
        Assert.True(stabled.Hidden);
        Assert.False(tut.Hidden);  // ilk gunun Gidak'i kendi yolunda (Director.Night)
        Assert.Null(tut.War);
        Assert.Contains(ev, e => e.Kind == WarEventKind.Swelled && e.AnimalId == fighter.Id);
    }

    [Fact]
    public void Faint_wakes_home_tired_one_day_and_tired_does_not_produce_or_fight()
    {
        var (s, r) = Fresh();
        var war = Square(0, 0, 6);
        r.AddFlag(1, 1);
        var a = TameIn(r, "pofuduk", war, 3, 3, "savasci");
        var pens = new[] { war };
        r.KabarmaBegin(pens, Hp(s));
        a.X = 15; a.Z = 15; // dusmani kovaladi, agildan cikti

        var ev = new List<WarEvent>();
        var res = r.Hit(a, s.Data.Combat, new CombatHit(99999, DamageType.Darbe, CombatTeam.Wild), ev);
        Assert.True(res.Killed);
        Assert.True(a.Fainted);
        Assert.Contains(ev, e => e.Kind == WarEventKind.Fainted);
        Assert.Contains(a, r.Animals); // kalici olmez (GDD 9.5)

        r.Dawn();
        Assert.False(a.Fainted);
        Assert.Equal(1, a.TiredDawns);
        Assert.Equal(war.Centroid.X, a.X, 3); // ahirinda uyanir
        Assert.Null(a.War);
        Assert.True(r.Exhausted(a));
        Assert.False(r.Fights(a, pens));

        // yorgun: uretici olsa bile urun vermez
        r.Rules.SetRole(a, "uretici");
        var feeder = r.AddFeeder(2, 2); s.Backpack.Add("turnip", 20); HusbandryRules.Fill(feeder, s.Backpack, "turnip");
        r.Tick(r.Rules.DaySeconds * 0.5f, pens);
        Assert.Empty(a.Pending);

        r.Dawn(); // ertesi safak: dinlendi
        Assert.Equal(0, a.TiredDawns);
        r.Tick(r.Rules.DaySeconds * 0.5f, pens);
        Assert.NotEmpty(a.Pending);
    }

    [Fact]
    public void Friendly_hits_are_blocked_and_armor_absorbs()
    {
        var (s, r) = Fresh();
        var war = Square(0, 0, 6);
        r.AddFlag(1, 1);
        var a = TameIn(r, "pofuduk", war, 3, 3, "savasci");
        r.KabarmaBegin(new[] { war }, Hp(s));
        float hp = a.War!.Hp;
        r.Hit(a, s.Data.Combat, new CombatHit(10, DamageType.Darbe, CombatTeam.Player));
        Assert.Equal(hp, a.War.Hp); // oyuncu atesi evcil hayvana islemez (dost atesi kapali)
        var res = r.Hit(a, s.Data.Combat, new CombatHit(10, DamageType.Darbe, CombatTeam.Wild));
        Assert.True(res.Damage < 10f); // Yastik zirhi
    }

    [Fact]
    public void Hard_moon_kills_fainted_animal()
    {
        var (s, r) = Fresh();
        r.Rules.Def.Combat.HardMoonDies = true;
        try
        {
            var war = Square(0, 0, 6);
            r.AddFlag(1, 1);
            var a = TameIn(r, "pofuduk", war, 3, 3, "savasci");
            r.KabarmaBegin(new[] { war }, Hp(s));
            var ev = new List<WarEvent>();
            r.Hit(a, s.Data.Combat, new CombatHit(99999, DamageType.Darbe, CombatTeam.Wild), ev);
            Assert.DoesNotContain(a, r.Animals);
            Assert.Contains(ev, e => e.Kind == WarEventKind.Died);
        }
        finally { r.Rules.Def.Combat.HardMoonDies = false; }
    }

    [Fact]
    public void Barn_destroyed_at_night_scatters_and_halves_happiness_at_dawn()
    {
        var (s, r) = Fresh();
        var barn = Square(0, 0, 6);
        var a = TameIn(r, "pofuduk", barn, 3, 3, "uretici");
        a.Happiness = 80;
        r.KabarmaBegin(new[] { barn }, Hp(s));
        Assert.True(a.Hidden);
        Assert.Empty(r.WarTick(new[] { barn })); // ahir duruyor
        var ev = r.WarTick(Array.Empty<PenShape>()); // duvar dustu: agil yok
        Assert.Contains(ev, e => e.Kind == WarEventKind.Scattered);
        Assert.False(a.Registered);
        Assert.False(a.Tame);
        r.Dawn();
        Assert.Equal(40f, a.Happiness, 3);
    }

    [Fact]
    public void Bell_calls_followers_and_escaped_animals_home()
    {
        var (_, r) = Fresh();
        var follower = r.Wild("pofuduk", 10, 0); follower.Trust = 1; follower.Happiness = 60;
        var escaped = r.Wild("pofuduk", 12, 0); escaped.Trust = 0.5f; escaped.Happiness = 50;
        var far = r.Wild("pofuduk", 200, 0); far.Trust = 1;
        var wild = r.Wild("pofuduk", 11, 0); wild.Trust = 0.1f;
        var ev = r.Bell(0, 0, 3, 3);
        Assert.True(follower.Called);
        Assert.True(escaped.Called);
        Assert.True(escaped.Tame);
        Assert.False(far.Called);
        Assert.False(wild.Called);
        Assert.Equal(2, ev.Count);
    }

    [Fact]
    public void Mount_needs_saddle_role_and_rest_speed_scales_with_B_and_mastery()
    {
        var (s, r) = Fresh();
        var pen = Square(0, 0, 6);
        var g = TameIn(r, "gidak", pen, 3, 3, "binek");
        var m = r.Mount;
        Assert.False(m.CanRide(g)); // eyersiz
        var bag = s.Backpack;
        Assert.False(m.Saddle(g, bag)); // cantada eyer yok
        bag.Add("eyer", 1);
        Assert.True(m.Saddle(g, bag));
        Assert.Equal(0, bag.Count("eyer"));
        Assert.True(m.CanRide(g));

        float v1 = m.Speed(g);
        Assert.Equal(r.Rules.Def.Mount.Speeds["gidak"] * (1f + (g.Genes.B - 1) * r.Rules.Def.Genes.BSpeedPerPoint), v1, 3);
        g.Genes = new AnimalGenes { B = 5, U = g.Genes.U, V = g.Genes.V, K = g.Genes.K };
        Assert.True(m.Speed(g) > v1);
        Assert.Equal(m.Speed(g) * 1.15f, m.Speed(g, 0.15f), 3);

        Assert.True(m.Mount(g, 1));
        Assert.False(g.Registered);
        Assert.False(m.Mount(r.Wild("homur", 0, 0), 1)); // bir binicinin bir bineği

        g.TiredDawns = 1;
        Assert.False(m.CanRide(g));
    }

    [Fact]
    public void Dismount_near_post_ties_animal_and_it_hides_at_kabarma()
    {
        var (s, r) = Fresh();
        var g = r.Wild("gidak", 0, 0); g.Trust = 1; r.Rules.SetRole(g, "binek"); g.Saddled = true;
        var post = r.AddPost(10, 10);
        Assert.True(r.Mount.Mount(g, 7));
        var tied = r.Mount.Dismount(g, 11, 10);
        Assert.Same(post, tied);
        Assert.Equal(g.Id, post.AnimalId);
        Assert.Equal("bekle", g.Command);
        r.KabarmaBegin(Array.Empty<PenShape>(), Hp(s));
        Assert.True(g.Hidden);
        // bir direge bir hayvan
        var h = r.Wild("gidak", 0, 0); h.Trust = 1; r.Rules.SetRole(h, "binek"); h.Saddled = true;
        r.Dawn();
        Assert.True(r.Mount.Mount(h, 8));
        Assert.Null(r.Mount.Dismount(h, 10, 10));
        // binince direkten cozulur
        Assert.True(r.Mount.Mount(g, 9));
        Assert.Equal(0, post.AnimalId);
    }

    [Fact]
    public void Travel_counts_whole_meters_and_xp_chunks()
    {
        var (_, r) = Fresh();
        var m = r.Mount;
        var (a, x1) = m.Travel(60.6f);
        var (b, x2) = m.Travel(60.6f);
        Assert.Equal(60, a);
        Assert.Equal(61, b);
        Assert.Equal(0, x1);
        Assert.Equal(1, x2);
    }

    [Fact]
    public void Combat_state_round_trips_through_ranch_save()
    {
        var (s, r) = Fresh();
        var war = Square(0, 0, 6);
        r.AddFlag(1, 1);
        var a = TameIn(r, "pofuduk", war, 3, 3, "savasci");
        a.Saddled = true;
        r.KabarmaBegin(new[] { war }, Hp(s));
        r.Hit(a, s.Data.Combat, new CombatHit(5, DamageType.Ates, CombatTeam.Wild)); // yanik durumu
        var game = new SaveRegistry().AddRange(s.SaveSections()).Capture(SaveScope.World);
        var t = new GameState(TestData.Load(), 7);
        new SaveRegistry().AddRange(t.SaveSections()).Restore(game);
        var b = t.Ranch.ById(a.Id)!;
        Assert.True(t.Ranch.AtWar);
        Assert.NotNull(b.War);
        Assert.Equal(a.War!.Hp, b.War!.Hp, 3);
        Assert.Equal(a.War.Statuses.Count, b.War.Statuses.Count);
        Assert.True(b.Saddled);
        Assert.Equal(0, b.Rider);
    }
}
