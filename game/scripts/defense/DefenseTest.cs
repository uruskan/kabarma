using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=savunma (GDD §16.4 M3): uc kule ve dort tuzak oyunun icinde, dalga Zipzip'lerine karsi. Kule: doldurma, atis,
/// oldurme, oncelik dongusu, asinmadan bozulma ve onarim, sokunce cephane iadesi. Tuzak: Diken vurur ve yavaslatir,
/// Yapiskan tutar, Pedi Zipzip'i ve oyuncuyu firlatir, Cukur acikken dusurur, kapaninca birakir. Sinama malzemesi
/// cantaya dogrudan konur (bot degil, sistem sinamasi). Basliksiz degilse goruntu progress/m3/savunma/'ya.
/// </summary>
public partial class DefenseTest : Node
{
    readonly Director _d;
    int _ok, _fail;
    string _out;

    public DefenseTest(Director d) { _d = d; Name = "DefenseTest"; }

    public override async void _Ready()
    {
        _out = Main.Args.TryGetValue("m3_out", out var o) ? o.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m3/savunma/";
        DirAccess.MakeDirRecursiveAbsolute(_out);
        await Wait(1.5f);
        try { await Run(); }
        catch (System.Exception e) { GD.PrintErr("DEFENSE test hata: " + e); _fail++; }
        GD.Print($"DEFENSE RESULT ok={_ok} fail={_fail} shots={_d.Defense.Shots} kills={_d.Defense.Kills} trapHits={_d.Defense.TrapHits}");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    void Check(bool ok, string what) { if (ok) _ok++; else _fail++; GD.Print($"DEFENSE {(ok ? "PASS" : "FAIL")} {what}"); }

    async Task Wait(float s) => await ToSignal(GetTree().CreateTimer(s, false, true), SceneTreeTimer.SignalName.Timeout);

    PlaceableDef Def(string id) => _d.Data.Tool.Placeables.First(p => p.Id == id);
    Vector3 G(float x, float z) => _d.World.Ground(_base + new Vector3(x, 0, z));
    Vector3 _base;

    Zipzip Spawn(Vector3 at, float hp = 1f)
    {
        var z = new Zipzip(_d.Data.Creature("zipzip"), _d, hp, 1f, swollen: true) { Name = "TestZipzip" };
        _d.AddChild(z);
        z.GlobalPosition = at;
        return z;
    }

    async Task Run()
    {
        var bag = _d.State.Backpack;
        var sys = _d.Defense;
        // Ciftlikten uzakta duz bir yer; oyuncu disarida bekler (Zipzip'ler ona dalmasin).
        _base = _d.World.Ground(_d.OcakPos + new Vector3(-26, 0, 18));
        _d.Player.GlobalPosition = G(0, 14);

        // ---------------- kuleler
        var civi = sys.Place(Def("civi_kulesi"), G(0, 0), 0);
        var sapan = sys.Place(Def("sapan_kulesi"), G(9, 0), 0);
        var sacma = sys.Place(Def("sacma_kulesi"), G(-9, 0), 0);
        await Wait(0.3f);
        Check(civi.TowerState != null && sapan.TowerState != null && sacma.TowerState != null, "uc kule kuruldu, Sim durumu var");
        bag.Add("civi", 60); bag.Add("stone", 20); bag.Add("sacma", 40);
        _d.Player.GlobalPosition = civi.GlobalPosition + Vector3.Back * 1.2f;
        Check(sys.Apply(DefenseSystem.Op.Load, civi.StateId, bag, true) && civi.TowerState.Loaded == 60, $"Civi Kulesi doldu ({civi.TowerState.Loaded})");
        sys.Apply(DefenseSystem.Op.Load, sapan.StateId, bag, true);
        sys.Apply(DefenseSystem.Op.Load, sacma.StateId, bag, true);
        Check(bag.Count("civi") == 0 && sapan.TowerState.Loaded == 20 && sacma.TowerState.Loaded == 40, "cephane cantadan depoya");
        _d.Player.GlobalPosition = G(0, 16);

        var zs = new List<Zipzip> { Spawn(G(2, -8)), Spawn(G(11, -10)), Spawn(G(-8, -4)), Spawn(G(-10, -5)) };
        await Wait(1.0f);
        Shot("savunma_kuleler_atis");
        await Wait(7f);
        int dead = zs.Count(z => !IsInstanceValid(z) || z.Dead);
        Check(sys.Shots > 5, $"kuleler atti ({sys.Shots})");
        Check(dead >= 3, $"kuleler dalgayi indirdi ({dead}/4)");
        Check(civi.TowerState.Loaded < 60 && sacma.TowerState.Loaded < 40, $"cephane harcandi civi={civi.TowerState.Loaded} sacma={sacma.TowerState.Loaded}");
        Check(civi.TowerState.Hp < civi.Tower.Hp, "atis asinma yazar");

        // oncelik dongusu ve kapali kule
        var t = civi.TowerState;
        foreach (var _ in Enumerable.Range(0, 4)) sys.Apply(DefenseSystem.Op.Priority, civi.StateId, bag, false);
        Check(t.Priority == "off", $"oncelik dongusu sonu kapali ({t.Priority})");
        int before = sys.Shots, ammo = t.Loaded;
        var probe = Spawn(G(1, -5));
        await Wait(1.5f);
        Check(t.Loaded == ammo, "kapali Civi Kulesi atmaz");
        sys.Apply(DefenseSystem.Op.Priority, civi.StateId, bag, false);
        await Wait(3f);
        Check(t.Loaded < ammo, "yeniden acilinca atar");

        // bakim: can biter, kule durur, onarilir
        t.Hp = 0;
        Check(TowerRules.Status(t) == TowerStatus.Broken, "can bitince bakim ister");
        bag.Add("wood", 10); bag.Add("copper_ingot", 2); bag.Add("civi", 10);
        Check(sys.Apply(DefenseSystem.Op.Repair, civi.StateId, bag, true) && t.Hp == civi.Tower.Hp, "onarim: tarifin %25'i, can tam");

        // ---------------- tuzaklar
        var spikes = sys.Place(Def("diken_tarlasi"), G(0, 30), 0);
        var sticky = sys.Place(Def("yapiskan_zemin"), G(5, 30), 0);
        var pad = sys.Place(Def("firlatma_pedi"), G(10, 30), Mathf.Pi / 2f);
        var pit = sys.Place(Def("cukur_kapagi"), G(15, 30), 0);
        _d.Player.GlobalPosition = G(7, 42);
        await Wait(0.3f);
        int hits0 = sys.TrapHits;
        var zSp = Spawn(G(0, 30), 3f);
        await Wait(1.2f);
        Check(spikes.TrapState.Triggers > 0 && (!IsInstanceValid(zSp) || zSp.Hp < zSp.MaxHp), $"Diken Tarlasi vurdu ({spikes.TrapState.Triggers})");
        var zSt = Spawn(G(5, 30), 3f);
        await Wait(0.4f);
        var p0 = IsInstanceValid(zSt) ? zSt.GlobalPosition : Vector3.Zero;
        await Wait(1.2f);
        Check(sticky.TrapState.Triggers > 0 && IsInstanceValid(zSt) && zSt.GlobalPosition.DistanceTo(p0) < 0.3f, "Yapiskan Zemin tuttu");
        var zPd = Spawn(G(10, 30), 3f);
        await Wait(0.3f);
        Check(pad.TrapState.Triggers > 0 && IsInstanceValid(zPd) && zPd.Airborne, "Firlatma Pedi Zipzip'i firlatti");
        Shot("savunma_tuzaklar");
        await Wait(2f);
        Check(!IsInstanceValid(zPd) || zPd.GlobalPosition.DistanceTo(pad.GlobalPosition) > 3f, "pedin yonunde uzaga dustu");
        // oyuncu
        _d.Player.GlobalPosition = pad.GlobalPosition + Vector3.Up * 0.2f;
        float y0 = pad.GlobalPosition.Y, peak = y0;
        for (int i = 0; i < 30; i++) { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); peak = Mathf.Max(peak, _d.Player.GlobalPosition.Y); }
        Check(peak > y0 + 1.2f, $"Firlatma Pedi oyuncuyu firlatti (+{peak - y0:0.0} m)");
        _d.Player.GlobalPosition = G(7, 42);

        // Cukur: kapaliyken dusmez, acikken duser ve tutulur, kapaninca cikar
        var zP1 = Spawn(G(15, 30), 3f);
        await Wait(0.5f);
        Check(pit.TrapState.Held.Count == 0, "kapali cukura dusulmez");
        if (IsInstanceValid(zP1)) zP1.QueueFree();
        sys.Apply(DefenseSystem.Op.Toggle, pit.StateId, bag, true);
        await Wait(0.8f);
        var zP2 = Spawn(G(15, 30), 3f);
        await Wait(0.6f);
        Check(pit.TrapState.Held.Count == 1, $"acik cukura dustu ({pit.TrapState.Held.Count})");
        await Wait(1.5f);
        Check(IsInstanceValid(zP2) && zP2.GlobalPosition.Y < pit.GlobalPosition.Y - 0.4f, "cukurda tutuluyor");
        Shot("savunma_cukur");
        sys.Apply(DefenseSystem.Op.Toggle, pit.StateId, bag, true);
        await Wait(0.3f);
        Check(pit.TrapState.Held.Count == 0 && (!IsInstanceValid(zP2) || zP2.GlobalPosition.Y > pit.GlobalPosition.Y - 0.3f), "kapak kapaninca birakti");
        Check(sys.TrapHits > hits0, "tuzak etkileri uygulandi");

        // ---------------- sokme: durum gider, cephane cantaya
        int sbefore = bag.Count("stone");
        int sapanId = sapan.StateId, left = sapan.TowerState.Loaded;
        _d.Furniture.Remove(sapan);
        sapan.QueueFree();
        await Wait(0.2f);
        Check(_d.State.Defense.Tower(sapanId) == null && bag.Count("stone") == sbefore + left, $"sokulen kulenin cephanesi cantaya (+{left})");
        foreach (var z in Zipzip.All.Where(z => z.Name.ToString().StartsWith("TestZipzip")).ToList()) z.QueueFree();
    }

    void Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        var img = GetViewport().GetTexture().GetImage();
        img?.SavePng(_out + name + ".png");
        GD.Print($"DEFENSE shot {_out}{name}.png");
    }
}
