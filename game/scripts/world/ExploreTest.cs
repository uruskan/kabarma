using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kesif sinamasi (--test=explore; dunya2): gercek oyun yolu (Director, Explorer, MapPanel) uzerinden GDD §21 ve
/// ICERIK §17 M2 kurallari. Oyuncu noktalar arasina tasinir (yuruyus botun isi degil; burada mekanik sinanir):
///  - yuruyerek harita acilir; Seyir Kulesi'ne tirmanmak haritayi acar, kule bulunur, iner;
///  - kapali durak firlatmaz; iki kule acilinca mancinik ucurur, parasutle hedef duragin yanina iner; yukle firlatmaz;
///  - Yuva bir Yumurta verir, bos Yuva vermez; Enkaz kazilinca Sim'de 5 gun sonraya yenilenir;
///  - M haritasi acilir ve kapanir, isaret konur.
/// Cikti: "EXPLORE RESULT ok|fail ..." ve cikis kodu.
/// </summary>
public partial class ExploreTest : Node
{
    readonly Director _d;
    readonly List<string> _fail = new();
    readonly List<string> _info = new();
    readonly IEnumerator<double> _steps;
    double _wait;

    public ExploreTest(Director d) { _d = d; Name = "ExploreTest"; _steps = Steps().GetEnumerator(); }

    void Check(bool ok, string what) { if (!ok) _fail.Add(what); GD.Print($"EXPLORE {(ok ? "ok" : "FAIL")} {what}"); }

    Explorer E => _d.Explorer;
    Exploration X => _d.State.Explore;
    World W => _d.World;

    public override void _Process(double delta)
    {
        if (_wait > 0) { _wait -= delta; return; }
        try
        {
            if (!_steps.MoveNext()) { Finish(); return; }
            _wait = _steps.Current;
        }
        catch (System.Exception e)
        {
            _fail.Add($"exception {e.GetType().Name}: {e.Message}");
            GD.PrintErr(e.ToString());
            Finish();
        }
    }

    void Put(Vector3 at)
    {
        _d.Player.GlobalPosition = W.Ground(at) + Vector3.Up * 0.3f;
        _d.Player.Velocity = Vector3.Zero;
    }

    IEnumerable<double> WaitIdle()
    {
        for (int i = 0; i < 400 && E.Busy; i++) yield return 0.05;
    }

    IEnumerable<double> Steps()
    {
        yield return 1.5;
        Check(E != null && _d.MapPanel != null, "Explorer ve MapPanel kuruldu");
        Check(X.Percent > 0.1f, $"yuruyus haritayi acar (%{X.Percent:0.00})");
        float walk = X.Percent;

        // Seyir Kulesi (Cayir): tirman, harita acilir, in.
        var cayirTower = W.Pois.First(p => p.Type == "seyir_kulesi" && p.Biome == "altin_cayir");
        var koruTower = W.Pois.First(p => p.Type == "seyir_kulesi" && p.Biome == "sis_korusu");
        Put(W.TowerLadder(cayirTower));
        yield return 0.4;
        Check(E.Prompt(_d.Player.GlobalPosition)?.verb == "Tırman", "kule dibinde istem: Tırman");
        Check(E.Interact(), "E kuleye tirmanir");
        foreach (var w in WaitIdle()) yield return w;
        yield return 0.3;
        Check(X.Climbed.Contains(cayirTower.Id), "kule tirmanildi");
        Check(X.Found.Contains(cayirTower.Id), "kule bulundu");
        Check(X.Percent > 25f && X.Percent > walk, $"kule 600 m acar (%{X.Percent:0}; 1800 m dunyada ~%35)"); // M4 ara adimi: 600 m yaricap 1800 m karenin ~%35'i
        Check(_d.Player.GlobalPosition.Y > W.PoiPos(cayirTower).Y + 12f, $"oyuncu kule tepesinde (y={_d.Player.GlobalPosition.Y:0.0})");
        Check(E.Prompt(_d.Player.GlobalPosition)?.verb == "İn", "tepede istem: İn");
        E.Interact();
        foreach (var w in WaitIdle()) yield return w;
        yield return 0.5;
        Check(_d.Player.GlobalPosition.Y < W.PoiPos(cayirTower).Y + 2f, "oyuncu indi");

        // Mancinik: Koru kulesi kapali.
        var stops = E.Stops();
        var from = stops.First(s => s.Id == cayirTower.Id);
        var to = stops.First(s => s.Id == koruTower.Id);
        Check(from.Open && !to.Open, "tirmanilmamis kulenin duragi kapali");
        Check(!E.Launch(from, to), "kapali duraga firlatmaz");
        // Koru kulesine tirman (yolu acmak icin oraya tasinir), in.
        Put(W.TowerLadder(koruTower));
        yield return 0.4;
        E.Interact();
        foreach (var w in WaitIdle()) yield return w;
        E.Interact();
        foreach (var w in WaitIdle()) yield return w;
        Check(X.Climbed.Contains(koruTower.Id), "Koru kulesi tirmanildi");
        // Yukle firlatmaz.
        stops = E.Stops();
        from = stops.First(s => s.Id == koruTower.Id);
        to = stops.First(s => s.Id == cayirTower.Id);
        Put(new Vector3(from.X, 0, from.Z));
        yield return 0.3;
        Check(E.Prompt(_d.Player.GlobalPosition)?.verb == "Fırlat", "durakta istem: Fırlat");
        _d.Player.Carrying = true;
        Check(!E.Launch(from, to), "yukle firlatmaz");
        _d.Player.Carrying = false;
        // Ucus: Koru -> Cayir.
        int flights = X.Flights;
        Check(E.Launch(from, to), "mancinik firlatir");
        float maxY = 0;
        for (int i = 0; i < 400 && E.Busy; i++) { maxY = Mathf.Max(maxY, _d.Player.GlobalPosition.Y); yield return 0.05; }
        yield return 0.5;
        var land = _d.Player.GlobalPosition;
        float off = new Vector2(land.X - to.X, land.Z - to.Z).Length();
        Check(X.Flights == flights + 1, "ucus sayildi");
        Check(off < W.Data.Biomes.World.Catapult.LandOffset + 2.5f, $"hedef duragin yanina indi ({off:0.0} m)");
        Check(Mathf.Abs(land.Y - W.HeightAt(land.X, land.Z)) < 1.5f, "yere indi");
        Check(maxY > Mathf.Max(from.Y, to.Y) + 20f, $"yay yuksekligi {maxY - Mathf.Max(from.Y, to.Y):0} m");
        _info.Add($"flight_land_off={off:0.0} apex={maxY - Mathf.Max(from.Y, to.Y):0}");

        // Yuva: bir Yumurta, sonra bos.
        var nest = W.Pois.First(p => p.Type == "yuva");
        Put(new Vector3(nest.X + 1f, 0, nest.Z));
        yield return 0.3;
        int eggs = _d.State.Backpack.Count("egg");
        Check(E.Prompt(_d.Player.GlobalPosition)?.verb == "Al", "yuvada istem: Al");
        E.Interact();
        Check(_d.State.Backpack.Count("egg") == eggs + 1, "yuva bir yumurta verdi");
        Check(E.Prompt(_d.Player.GlobalPosition) == null, "bos yuva istem vermez");

        // Enkaz: kayayi kaz (Kazma'nin cagirdigi Rock.Mine), Sim'de bos ve 5 gun sonra yenilenir.
        var wreck = W.Pois.First(p => p.Type == "enkaz");
        var rock = W.PoiRocks[wreck.Id];
        Check(rock.Item == "scrap" && rock.Stones == 12, "enkaz 12 Hurda");
        rock.Mine(100f);
        yield return 0.6;
        int day = _d.State.Calendar.Day;
        Check(X.Uses.TryGetValue(wreck.Id, out var u) && u.Left == 0 && u.RefillDay == day + 5, $"enkaz bos, gun {day + 5}'te yenilenir");

        // Harita: M paneli acilir, isaret konur, kapanir.
        _d.MapPanel.Toggle();
        yield return 0.2;
        Check(_d.MapPanel.Open, "harita acildi");
        int marks = X.Markers.Count;
        X.AddMarker(10, 10, "Sınama");
        Check(X.Markers.Count == marks + 1, "isaret kondu");
        _d.MapPanel.Toggle();
        Check(!_d.MapPanel.Open, "harita kapandi");
        _info.Add($"percent={X.Percent:0.0} found={X.Found.Count} flights={X.Flights}");
    }

    void Finish()
    {
        SetProcess(false);
        foreach (var i in _info) GD.Print("EXPLORE " + i);
        GD.Print(_fail.Count == 0 ? "EXPLORE RESULT ok" : $"EXPLORE RESULT fail {_fail.Count}: {string.Join("; ", _fail)}");
        GetTree().Quit(_fail.Count == 0 ? 0 : 1);
    }
}
