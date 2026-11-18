using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M1 Sim sistemlerinin gidis-donusu (PLAN 7.2 kosul 5): dolu bir oyun kaydedilir, taze bir GameState'e yuklenir;
/// bolumler bayt bayt ayni, ayni komutlarla ikisi ayni yere varir.
/// </summary>
public class SaveRoundTripTests
{
    static SaveRegistry Reg(GameState s) => new SaveRegistry().AddRange(s.SaveSections());

    /// <summary>Her M1 sistemine dokunan bir oyun.</summary>
    static GameState Played()
    {
        var s = new GameState(TestData.Load());
        var d = s.Data;
        s.Backpack.Add("wood", 60); s.Backpack.Add("stone", 30); s.Backpack.Add("turnip", 6, 2); s.Backpack.Add("copper_ore", 6);
        s.Backpack.Add("turnip_seed", 4); s.Backpack.Add("wheat", 3);
        // tarla: sur, ek, sula, buyut
        for (int x = 0; x < 3; x++) { s.Farm.Till(x, 0); s.Farm.Plant(x, 0, "turnip", s.Backpack); s.Farm.Water(x, 0); }
        s.Farm.Till(5, 5);
        s.Farm.Tick(20f, true);
        // agil
        s.Coop.Animals.Add(new PenAnimal { Species = "gidak", Tutorial = true, Trust = 0.6f, Registered = true, LayTimer = 12.5f, Laid = 2 });
        // istasyon kuyrugu ve el uretimi
        var ingot = d.Tool.Recipes.First(r => r.Id == "copper_ingot");
        Assert.True(s.Stations["tas_firin"].Enqueue(ingot, s.Backpack));
        s.Stations["tas_firin"].Tick(1.5f, s.Backpack);
        var mesale = d.Tool.Recipes.First(r => r.Id == "mesale");
        Assert.True(s.Craft.Enqueue(mesale, 3, s.Backpack));
        s.Craft.Tick(0.4f, s.Backpack);
        // kargo: ilk satis aninda, ikincisi bekler
        s.Cargo.Pack(s.Backpack, "turnip", 2);
        var c = s.Cargo.Close()!;
        s.Cargo.LoadCrate(s.Cargo.TakeClosed()!);
        s.Cargo.LaunchCrates();
        s.Cargo.Pack(s.Backpack, "turnip", 1);
        s.Cargo.LoadCrate(s.Cargo.Close()!);
        s.Cargo.LaunchCrates();
        s.Cargo.Pack(s.Backpack, "wheat", 1); // acik kasa
        s.Cargo.Tick(5f);
        // takvim, Defter, senaryo, yemek, dayaniklilik, Capa
        s.Calendar.Start(d.Calendar.FirstDay);
        s.Calendar.Tick(33f);
        s.Orders.OpenDefter();
        s.Orders.Report("planted", 2);
        s.Scenario.Report(s.Scenario.Current!.Id, 1);
        s.Food.Eat(d.Tool.Foods[0]);
        s.Food.Tick(2f);
        s.Stamina.Tick(1.2f, wantSprint: true);
        s.Capa.Unlock("civi_tabancasi");
        s.Ids.New();
        _ = c;
        return s;
    }

    [Fact]
    public void Played_game_round_trips_byte_identical()
    {
        var a = Played();
        var g = SaveCodec.Decode(SaveCodec.Encode(Reg(a).Capture(SaveScope.World, a.Manifest("tam"))));
        var b = new GameState(TestData.Load());
        var rep = Reg(b).Restore(g);
        Assert.Empty(rep.Missing);
        Assert.Empty(rep.Unknown);
        var g2 = Reg(b).Capture(SaveScope.World);
        Assert.Equal(g.Sections.Keys, g2.Sections.Keys);
        foreach (var (id, blob) in g.Sections)
            Assert.True(blob.Data.AsSpan().SequenceEqual(g2.Sections[id].Data), $"'{id}' bolumu gidis-donuste degisti");
        Assert.Equal(a.Calendar.Day, g.Manifest.Day);
    }

    [Fact]
    public void Restored_game_runs_on_identically()
    {
        var a = Played();
        var b = new GameState(TestData.Load());
        Reg(b).Restore(Reg(a).Capture(SaveScope.World));
        foreach (var s in new[] { a, b })
        {
            for (int i = 0; i < 40; i++)
            {
                s.Farm.Tick(1f, true);
                foreach (var q in s.Stations.Values) q.Tick(1f, s.Backpack);
                s.Craft.Tick(1f, s.Backpack);
                s.Cargo.Tick(1f);
                s.Calendar.Tick(1f);
                s.Food.Tick(1f);
                s.Stamina.Tick(1f, wantSprint: i % 7 == 0);
            }
            s.Orders.Report("planted");
            s.Farm.Harvest(0, 0, s.Rng);
            s.Scenario.Report(s.Scenario.Current?.Id ?? "", 1);
        }
        var ga = Reg(a).Capture(SaveScope.World);
        var gb = Reg(b).Capture(SaveScope.World);
        foreach (var (id, blob) in ga.Sections)
            Assert.True(blob.Data.AsSpan().SequenceEqual(gb.Sections[id].Data), $"'{id}' yuklemeden sonra ayrisdi");
        Assert.Equal(a.Credits, b.Credits);
        Assert.Equal(a.Farm.MatureCount, b.Farm.MatureCount);
    }

    [Fact]
    public void Restore_does_not_advance_progress_or_fire_events()
    {
        var a = Played();
        var b = new GameState(TestData.Load());
        int events = 0;
        b.Farm.Planted += _ => events++;
        b.Farm.Tilled += _ => events++;
        b.Scenario.StepChanged += (_, _) => events++;
        b.Orders.DefterOpened += () => events++;
        b.Calendar.Morning += _ => events++;
        b.Cargo.Paid += (_, _) => events++;
        b.Backpack.Changed += (_, _) => events++;
        Reg(b).Restore(Reg(a).Capture(SaveScope.World));
        Assert.Equal(0, events);
        Assert.Equal(a.Orders.Chains.Sum(c => c.Count("planted")), b.Orders.Chains.Sum(c => c.Count("planted")));
        Assert.Equal(a.Scenario.Progress, b.Scenario.Progress);
        Assert.Equal(a.Scenario.StepIndex, b.Scenario.StepIndex);
        Assert.Equal(a.Credits, b.Credits);
        Assert.Equal(a.Known.Count, b.Known.Count);
    }

    [Fact]
    public void Cargo_adopt_keeps_one_crate_object()
    {
        var a = Played();
        a.Cargo.Pack(a.Backpack, "turnip", 1);
        a.Cargo.LoadCrate(a.Cargo.Close()!);
        int id = a.Cargo.Pad[0].Id;
        var b = new GameState(TestData.Load());
        Reg(b).Restore(Reg(a).Capture(SaveScope.World));
        var node = new CargoCrate(b.Data, id); // dunyadaki kasa dugumunun kendi nesnesi
        b.Cargo.Adopt(node);
        Assert.Same(node, b.Cargo.FindCrate(id));
    }
}
