using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=kale (M4 kale; ICERIK §10, §13): bes yeni kule ve dort yeni tuzak oyunun icinde, dalga Zipzip'lerine karsi.
/// Alev (Biyoyakit, Yanik), Havan (uzun menzil), Ark / Lazer / Maya Topu (gercek kabloyla Maya Reaktoru'nden beslenir;
/// kablosuz Ark atmaz), Elektrikli Cit (guc, Sok), Mayin (patlar, bozulur), Yag Hendegi (E tutusturur), Dondurucu
/// (guc, Donma); efektler VfxSystem'den (Vfx.PlayedIds); Cam seti Insa'dan kurulur (cam malzeme, saydam); Silo, Ambar,
/// yatak, isik ve hizmet parcalari. Sonuc: "KALE RESULT ok=N fail=M". DURUST: gevsetilenler "GEVSETILDI" (Relax).
/// </summary>
public partial class KaleTest : Node
{
    readonly Director _d;
    int _ok, _fail;
    string _out;
    Vector3 _base;
    readonly List<string> _relaxed = new();

    public KaleTest(Director d) { _d = d; Name = "KaleTest"; }

    GameState S => _d.State;
    DefenseSystem Sys => _d.Defense;
    PowerNet Net => S.Power;
    PowerFile F => _d.Data.Power;
    void Check(bool ok, string what) { if (ok) _ok++; else _fail++; GD.Print($"KALE {(ok ? "PASS" : "FAIL")} {what}"); }
    void Relax(string what) { if (!_relaxed.Contains(what)) { _relaxed.Add(what); GD.Print("KALE GEVSETILDI " + what); } }
    async Task Wait(float s) => await ToSignal(GetTree().CreateTimer(s, false, true), SceneTreeTimer.SignalName.Timeout);
    PlaceableDef Def(string id) => _d.Data.Tool.Placeables.First(p => p.Id == id);
    Vector3 G(float x, float z) => _d.World.Ground(_base + new Vector3(x, 0, z));

    Zipzip Spawn(Vector3 at, float hp = 4f)
    {
        var z = new Zipzip(_d.Data.Creature("zipzip"), _d, hp, 1f, swollen: true) { Name = "TestZipzip" };
        _d.AddChild(z);
        z.GlobalPosition = at;
        return z;
    }

    public override async void _Ready()
    {
        _out = Main.Args.TryGetValue("test_out", out var o) ? o.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m4/kale/";
        DirAccess.MakeDirRecursiveAbsolute(_out);
        GetTree().CreateTimer(480, true, false, true).Timeout += () => { GD.Print("KALE ZAMAN ASIMI"); _fail++; Finish(); };
        await Wait(1.5f);
        try
        {
            _base = _d.World.Ground(_d.OcakPos + new Vector3(-26, 0, 18));
            _d.Player.GlobalPosition = G(0, 60);
            await Alev();
            await Havan();
            await PowerTowers();
            await Traps();
            await Glass();
            await Props();
            Effects();
        }
        catch (System.Exception e) { GD.Print("KALE HATA " + e); _fail++; }
        Finish();
    }

    /// <summary>Hedef sayisini tutar: <paramref name="seconds"/> boyunca her 0.7 sn'de eksik Zipzip'i yeniden doğurur.</summary>
    async Task Feed(Vector3 at, float seconds, int count = 2, float hp = 6f)
    {
        for (float t = 0; t < seconds; t += 0.7f)
        {
            int alive = Zipzip.All.Count(z => IsInstanceValid(z) && !z.Dead && z.Name.ToString().StartsWith("TestZipzip") && z.GlobalPosition.DistanceTo(at) < 6f);
            for (int i = alive; i < count; i++) Spawn(at + new Vector3((i - 0.5f) * 1.2f, 0, 0), hp);
            await Wait(0.7f);
        }
    }

    void Clear() { foreach (var z in Zipzip.All.Where(z => z.Name.ToString().StartsWith("TestZipzip")).ToList()) if (IsInstanceValid(z)) z.QueueFree(); }

    async Task Alev()
    {
        var bag = S.Backpack;
        var t = Sys.Place(Def("alev_kulesi"), G(20, 0), 0);
        await Wait(0.3f);
        bag.Add("biyoyakit", 12);
        Relax("Biyoyakit cantaya verildi (Damitici ve Bugday zinciri tasima/zincir testlerinde)");
        Check(Sys.Apply(DefenseSystem.Op.Load, t.StateId, bag, true) && t.TowerState.Loaded == 12, "Alev Kulesi Biyoyakit ile doldu");
        int burn0 = Sys.BurnTicks;
        await Feed(G(20, -5), 3.5f, 2, 8f);
        Check(t.TowerState.Shots > 0 && t.TowerState.Loaded < 12, $"Alev Kulesi atti ({t.TowerState.Shots}), cephane harcandi");
        Check(Sys.BurnTicks > burn0, $"Yanik hasar verdi ({Sys.BurnTicks - burn0} adim)");
        Check(Zipzip.All.Any(z => IsInstanceValid(z) && z.Name.ToString().StartsWith("TestZipzip") && z.Hp < z.MaxHp) || Sys.Kills > 0, "hedef hasar aldi ya da oldu");
        Clear();
    }

    async Task Havan()
    {
        var bag = S.Backpack;
        var t = Sys.Place(Def("havan_kulesi"), G(-20, 0), 0);
        bag.Add("havan_mermisi", 8);
        Sys.Apply(DefenseSystem.Op.Load, t.StateId, bag, true);
        await Wait(0.3f);
        await Feed(G(-20, -20), 7f, 2, 6f);
        Check(t.TowerState.Shots > 0, $"Havan Kulesi 20 m'deki hedefe atti ({t.TowerState.Shots})");
        // minimum menzil: yanindaki hedefe atmaz
        int shots = t.TowerState.Shots;
        Clear();
        var near = Spawn(G(-20, -4), 6f);
        await Wait(1.2f);
        Check(t.TowerState.Shots == shots, "Havan Kulesi 4 m'deki (menzil alti) hedefe atmaz");
        Clear();
    }

    PowerDevice Dev(string id, float x, float z) => _d.Power.Place(Def(id), G(x, z), 0);

    bool Wire(int a, int b)
    {
        var l = Net.Connect(F, a, b, false);
        if (l == null) GD.Print($"KALE kablo cekilemedi {Net.Get(a)?.Def} -> {Net.Get(b)?.Def}: {Net.CanConnect(F, a, b, false)}");
        return l != null;
    }

    async Task PowerTowers()
    {
        var bag = S.Backpack;
        Relax("kablo Sim'den cekildi (Kablo esyasi harcanmadi; kablo cekme Capa'da sinanir)");
        var reactor = Dev("maya_reaktoru", 0, -3);
        var hub = Dev("direk", 0, 1);
        var ark = Sys.Place(Def("ark_bobini"), G(-5, 2), 0);
        var lazer = Sys.Place(Def("lazer_kulesi"), G(5, 2), 0);
        var maya = Sys.Place(Def("maya_topu_kulesi"), G(0, 6), 0);
        var ark2 = Sys.Place(Def("ark_bobini"), G(-8, 4), 0); // kablosuz
        await Wait(0.4f);
        Check(ark.Port != null && lazer.Port != null && maya.Port != null, "guc isteyen kuleler guc ucu acti (Ark 120 W, Lazer 200 W, Maya Topu 300 W)");
        bool wired = Wire(reactor.NodeId, hub.NodeId) && Wire(hub.NodeId, ark.Port.NodeId) && Wire(hub.NodeId, lazer.Port.NodeId) && Wire(hub.NodeId, maya.Port.NodeId);
        Check(wired, "Maya Reaktoru -> Direk -> 3 kule kablolandi");
        bag.Add("maya", 20); bag.Add("ayaz_kristali", 3);
        Relax("Maya (Kazan) ve Ayaz Kristali cantaya verildi");
        Sys.Apply(DefenseSystem.Op.Load, maya.StateId, bag, true);
        Sys.Apply(DefenseSystem.Op.Load, lazer.StateId, bag, true);
        Check(maya.TowerState.Loaded == 20 && lazer.TowerState.Loaded == 3, "Maya Topu Maya ile, Lazer Ayaz Kristali ile doldu");
        Check(lazer.TowerState.Priority == "flying", "Lazer Kulesi ucanlara oncelikli kuruldu");
        await Feed(G(-5, -6), 7f, 3, 12f);
        Check(ark.Port.Powered && lazer.Port.Powered, "kuleler beslendi (hedef varken guc ceker)");
        Check(ark.TowerState.Shots > 0, $"Ark Bobini atti ({ark.TowerState.Shots}), cephane harcamadi: {ark.TowerState.Loaded}");
        Check(lazer.TowerState.Shots > 0, $"Lazer Kulesi atti ({lazer.TowerState.Shots})");
        Check(maya.TowerState.Shots > 0 && maya.TowerState.Loaded < 20, $"Maya Topu atti ({maya.TowerState.Shots}), Maya harcandi");
        Check(ark2.TowerState.Shots == 0, "kablosuz Ark Bobini atmaz (guc yok)");
        Check(Sys.Kills > 0 || Zipzip.All.Any(z => IsInstanceValid(z) && z.Name.ToString().StartsWith("TestZipzip") && z.Hp < z.MaxHp), "guc kuleleri hedefe hasar verdi");
        await Shot("kale_kuleler_atis", G(0, 16) + Vector3.Up * 9f, G(0, -4) + Vector3.Up * 1f);
        GD.Print($"KALE swellBursts={Sys.SwellBursts} kills={Sys.Kills}");
        Clear();
    }

    async Task Traps()
    {
        var bag = S.Backpack;
        var sys = Sys;
        var reactor = Dev("maya_reaktoru", 0, 33);
        var hub = Dev("direk", 0, 37);
        var fence = sys.Place(Def("elektrikli_cit"), G(0, 30), 0);
        var fence2 = sys.Place(Def("elektrikli_cit"), G(10, 30), 0); // kablosuz
        var mine = sys.Place(Def("mayin"), G(20, 30), 0);
        var oil = sys.Place(Def("yag_hendegi"), G(30, 30), 0);
        var freezer = sys.Place(Def("dondurucu_puskurtucu"), G(6, 40), 0);
        await Wait(0.4f);
        Check(fence.Port != null && freezer.Port != null && mine.Port == null, "Elektrikli Cit ve Dondurucu guc ucu acti; Mayin ve Yag Hendegi istemez");
        Check(Wire(reactor.NodeId, hub.NodeId) && Wire(hub.NodeId, fence.Port.NodeId) && Wire(hub.NodeId, freezer.Port.NodeId), "Reaktor -> Direk -> Cit ve Dondurucu");
        _d.Player.GlobalPosition = G(20, 48);
        // Elektrikli Cit
        var zf = Spawn(G(0, 30), 6f);
        var zf2 = Spawn(G(10, 30), 6f);
        await Wait(1.6f);
        Check(fence.TrapState.Triggers > 0 && (!IsInstanceValid(zf) || zf.Hp < zf.MaxHp), $"Elektrikli Cit vurdu ({fence.TrapState.Triggers})");
        Check(fence2.TrapState.Triggers == 0, "kablosuz Elektrikli Cit calismaz");
        Clear();
        // Mayin
        var zm = Spawn(G(20.3f, 30), 3f); var zm2 = Spawn(G(22, 30), 3f);
        await Wait(0.6f);
        Check(TrapRules.Broken(mine.TrapState) && mine.TrapState.Triggers == 1, "Mayin patladi ve bozuldu (tek kullanimlik)");
        Check((!IsInstanceValid(zm) || zm.Hp < zm.MaxHp) && (!IsInstanceValid(zm2) || zm2.Hp < zm2.MaxHp), "patlama alan hasari verdi");
        bag.Add("celik", 3); bag.Add("barut", 4); bag.Add("kaucuk", 1);
        Relax("Mayin onarimi icin celik, barut, kaucuk cantaya verildi");
        Check(sys.Apply(DefenseSystem.Op.Repair, mine.StateId, bag, true) && !TrapRules.Broken(mine.TrapState), "Mayin onarimla yeniden kuruldu");
        Clear();
        // Yag Hendegi
        var zo0 = Spawn(G(30, 30), 8f);
        await Wait(0.4f);
        Check(oil.TrapState.Triggers == 0 && !oil.TrapState.Open, "tutusturulmadan Yag Hendegi yalniz kaydirir");
        Clear();
        sys.Apply(DefenseSystem.Op.Toggle, oil.StateId, bag, true);
        var zo = Spawn(G(30, 30), 8f);
        int ticks = sys.BurnTicks;
        await Wait(2.0f);
        Check(oil.TrapState.Open && (sys.BurningCount > 0 || sys.BurnTicks > ticks), "E ile tutusunca alev duvari yakar (Yanik)");
        Check(!IsInstanceValid(zo) || zo.Hp < zo.MaxHp, "alevdeki hedef hasar aldi");
        await Shot("kale_tuzaklar", G(25, 40) + Vector3.Up * 7f, G(25, 30));
        Clear();
        // Dondurucu
        var zd = Spawn(G(6.5f, 40), 6f);
        await Wait(1.5f);
        Check(freezer.TrapState.Triggers > 0 && (!IsInstanceValid(zd) || zd.FreezeT > 0 || zd.Hp < zd.MaxHp), $"Dondurucu Puskurtucu dondurdu ({freezer.TrapState.Triggers})");
        Clear();
    }

    async Task Glass()
    {
        var bs = _d.Build.System;
        Check(bs.Kit["cam_duvar"] != null && bs.Kit["cam_cati"] != null && bs.Kit["cam_zemin"] != null && bs.Kit["cam_kapi"] != null, "Cam seti dort parca kitte (temel parcalarin geometrisiyle)");
        Check(bs.Kit["cam_duvar"].Sockets.Length == bs.Kit["duvar"].Sockets.Length, "Cam Duvar ahsap duvarin yuvalarini tasir");
        var origin = G(60, 0);
        var f = await PlaceGround("kare_temel", origin);
        Check(f != null && f.Material == "wood", "temel (ahsap) kuruldu");
        if (f == null) { bs.SetActive(false); return; }
        // Insa Plani gibi: once on kose sutunlari (yuvalar), sonra kenar ortasina duvar (BuildTest ile ayni yol).
        float y0 = f.GlobalPosition.Y; var fc = f.GlobalPosition;
        foreach (var sx in new[] { -1f, 1f })
        {
            var corner = new Vector3(fc.X + sx * 1.5f, y0, fc.Z + 1.5f) + new Vector3(-sx * 0.25f, 0, -0.25f);
            await AimPlace("sutun", corner, corner + new Vector3(0, 6, 1), null);
        }
        var wall = await PlaceWall("cam_duvar", fc + new Vector3(0, 0, 1.5f), Vector3.Back, y0);
        Check(wall != null && wall.Material == "cam", $"Cam Duvar Cam malzemesiyle kuruldu ({wall?.Material})");
        if (wall != null)
        {
            var d = _d.Data.Build.Material("cam");
            Check(wall.Hp == d.Hp && wall.Hp < _d.Data.Build.Material("wood").Hp, $"cam kirilgan: {wall.Hp} can");
            Check(wall.GetChildren().OfType<Node3D>().SelectMany(n => n.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()).Any(m => m.GetSurfaceOverrideMaterial(0) is BaseMaterial3D { Transparency: BaseMaterial3D.TransparencyEnum.Alpha }), "Cam Duvar saydam");
            Check(StructureCosts.Upgrade(_d.Data.Build, "cam", 4, true) == null, "Cam yukseltme zincirinde degil");
        }
        bs.SetActive(false);
        await Shot("kale_cam_duvar", f.GlobalPosition + new Vector3(4, 3, 6), f.GlobalPosition + new Vector3(0, 1.2f, 1.5f));
    }

    async Task<BuildPiece> PlaceGround(string id, Vector3 at)
    {
        var bs = _d.Build.System;
        for (int i = 0; i < 10; i++)
        {
            var t = new Vector3(at.X + i * 5f, _d.World.HeightAt(at.X + i * 5f, at.Z), at.Z);
            bs.SetActive(true);
            bs.Select(id);
            bs.UpdateAim(t + new Vector3(0, 9, 5), new Vector3(0, -9, -5));
            if (bs.Current is { Valid: true, Mode: "ground" }) { var p = bs.TryPlace(); await Wait(0.2f); return p; }
        }
        return null;
    }

    async Task<BuildPiece> PlaceWall(string id, Vector3 mid, Vector3 outward, float level)
    {
        var target = new Vector3(mid.X, level, mid.Z) - outward * 0.45f;
        return await AimPlace(id, target, target + Vector3.Up * 6f - outward * 2f, xf => xf.Basis.Z.Normalized().Dot(outward) > 0.9f);
    }

    /// <summary>Oyuncu gibi: parcayi sec, hedefe nisan al, gerekirse dondur, yerlestir (BuildTest.Place ile ayni).</summary>
    async Task<BuildPiece> AimPlace(string id, Vector3 target, Vector3 from, System.Func<Transform3D, bool> want)
    {
        var bs = _d.Build.System;
        bs.SetActive(true); // Director.Build her karede kapatabilir: ayni karede ac, sec, nisan al, yerlestir
        bs.Select(id);
        for (int i = 0; i < 8; i++)
        {
            bs.UpdateAim(from, target - from);
            var c = bs.Current;
            if (c != null && c.Valid && (want == null || want(c.Xf))) break;
            bs.Rotate();
        }
        var cur = bs.Current;
        if (cur == null || !cur.Valid) { GD.Print($"KALE {id} hayaleti gecersiz: mode={cur?.Mode} reason='{cur?.Reason}'"); return null; }
        var p = bs.TryPlace();
        await Wait(0.2f);
        return p;
    }

    async Task Props()
    {
        var ambar = _d.PlaceFurniture(Def("ambar"), G(-30, 50), 0);
        var silo = _d.PlaceFurniture(Def("silo"), G(-34, 50), 0);
        var yatak = _d.PlaceFurniture(Def("yatak"), G(-38, 50), 0);
        var fener = _d.PlaceFurniture(Def("fener"), G(-42, 50), 0);
        var borsa = _d.PlaceFurniture(Def("borsa_tahtasi"), G(-46, 50), 0);
        await Wait(0.3f);
        Check(ambar.Storage != null && ambar.Storage.Slots == 60, "Ambar 60 yuvalik depo");
        silo.Storage.Add("wheat", 2600);
        Check(silo.Storage.Count("wheat") == 2000 && silo.Storage.Slots == 1, $"Silo tek turden 2.000 ({silo.Storage.Count("wheat")})");
        Check(yatak.Def.Spawn, "Yatak dogma noktasi ve uyku yeri");
        Check(fener.FindChildren("*", "OmniLight3D", true, false).Count > 0, "Fener gece isigi tasir");
        _d.Player.GlobalPosition = borsa.GlobalPosition + Vector3.Back * 1.0f;
        Check(_d.InteractProp(borsa), "Borsa Tahtasi E ile Borsa'yi acar (rampa terminaliyle ayni)");
        _d.Hud.HideMenu();
        int placed = _d.Data.Tool.Placeables.Count(p => p.Station == null && !_d.Data.Power.IsDevice(p.Id) && !_d.Data.Defense.Handles(p.Id) && p.Id != "ocak");
        GD.Print($"KALE placeables(istasyon/guc/savunma haric)={placed}");
    }

    void Effects()
    {
        var want = new[] { "tower_alev_shot", "tower_havan_shot", "tower_ark_shot", "tower_lazer_shot", "tower_maya_topu_shot", "trap_elektrik", "trap_mayin", "trap_yag", "trap_dondurucu" };
        var missing = want.Where(w => !Vfx.PlayedIds.Contains(w)).ToList();
        Check(missing.Count == 0, "efektler VfxSystem'den oynadi" + (missing.Count > 0 ? ": oynamayan " + string.Join(",", missing) : ""));
        Check(want.All(Vfx.Has), "dokuz yeni efekt vfx.json'da tanimli");
    }

    async Task Shot(string name, Vector3 eye, Vector3 at)
    {
        if (DisplayServer.GetName() == "headless") return;
        var prev = GetViewport().GetCamera3D();
        var cam = new Camera3D { Fov = 55f, Far = 400f };
        _d.AddChild(cam);
        cam.GlobalPosition = eye;
        cam.LookAt(at);
        cam.MakeCurrent();
        _d.Hud.Visible = false;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage()?.SavePng(_out + name + ".png");
        GD.Print($"KALE shot {_out}{name}.png");
        _d.Hud.Visible = true;
        prev?.MakeCurrent();
        cam.QueueFree();
    }

    bool _fin;
    void Finish()
    {
        if (_fin) return;
        _fin = true;
        GD.Print($"KALE GEVSETILEN ADIMLAR ({_relaxed.Count}): " + string.Join("; ", _relaxed));
        GD.Print($"KALE RESULT ok={_ok} fail={_fail} shots={Sys.Shots} kills={Sys.Kills} trapHits={Sys.TrapHits} burn={Sys.BurnTicks}");
        GetTree().Quit(_fail == 0 && _ok > 0 ? 0 : 1);
    }
}
