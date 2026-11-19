using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dolap sinamasi (--test=dolap; M4 dolap). 1) Uye pasifleri oyuna baglanir (hiz, can, depar, suzulme, XP, gelir, katalog,
/// mutasyon, uretim). 2) Her kozmetik slotu modele uygulanir (kask dugumu, canta susu, tulum golgelendiricisi, Capa kaplamasi). 3) Jest
/// klibi oynar. 4) Dolap menusu acilir. Pencereli kosuda goruntuler progress/m4/dolap/ altina yazilir (kask, yuz, tulum, canta susu, Capa).
/// </summary>
public partial class DolapTest : Node
{
    static readonly Vector3 Site = new(-400f, 220f, 400f);
    Director _d;
    Player P => _d.Player;
    Wardrobe W => _d.State.Wardrobe;
    readonly List<string> _fails = new();
    int _checks, _shots;
    bool _window;
    string _dir;
    Camera3D _cam;
    Vector3 _off = new(0f, 1.0f, 3.2f);
    float _lookY = 0.85f;

    public override void _Ready() { _window = DisplayServer.GetName() != "headless"; CallDeferred(nameof(Begin)); }

    async void Begin()
    {
        try { await Run(); } catch (Exception e) { _fails.Add("istisna: " + e.Message); GD.Print(e.ToString()); }
        GD.Print(_fails.Count == 0 ? $"RESULT ok dolap checks={_checks} shots={_shots}" : $"RESULT fail dolap ({_fails.Count}/{_checks}) {string.Join("; ", _fails)}");
        GetTree().Quit(_fails.Count == 0 ? 0 : 1);
    }

    void Check(bool ok, string what) { _checks++; if (!ok) { _fails.Add(what); GD.Print("DOLAP FAIL " + what); } }
    async Task Seconds(float s) { int n = Mathf.CeilToInt(s * Engine.PhysicsTicksPerSecond); for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override void _Process(double delta)
    {
        if (_cam == null || _d?.Player == null) return;
        var basis = P.Visual.GlobalBasis.Orthonormalized();
        _cam.GlobalPosition = P.GlobalPosition + basis * _off;
        _cam.LookAt(P.GlobalPosition + Vector3.Up * _lookY, Vector3.Up);
        if (!_cam.Current) _cam.MakeCurrent();
    }

    async Task Shot(string name)
    {
        await Seconds(0.25f);
        if (!_window) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{name}.png");
        _shots++;
    }

    void Wear(params string[] ids)
    {
        foreach (var id in ids) { W.Unlock("cosmetic", id); W.Equip(id); }
    }

    async Task Run()
    {
        while ((_d = GetParent().GetNodeOrNull<Director>("Director")) == null || _d.Player == null || _d.Capa == null) await Seconds(0.05f);
        while (!(_d.Pod?.IntroDone ?? true)) await Seconds(0.05f);
        await Seconds(0.3f);
        _dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/m4/dolap";
        DirAccess.MakeDirRecursiveAbsolute(_dir);

        var floor = new StaticBody3D();
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(120, 1, 120) } });
        AddChild(floor);
        floor.GlobalPosition = Site + Vector3.Down * 0.5f;
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0), LightEnergy = 1.2f });
        _cam = new Camera3D { Fov = 40f, Far = 200f };
        AddChild(_cam);
        P.GlobalPosition = Site + Vector3.Up * 0.05f;
        P.Velocity = Vector3.Zero;
        P.Yaw.Rotation = Vector3.Zero;
        P.Pitch.Rotation = Vector3.Zero;
        await Seconds(0.8f);

        // ---- 1) pasifler ----
        Check(W.Active == "cirak" && Math.Abs((_d.State.Mastery.XpScale?.Invoke() ?? 0) - 1.1) < 1e-4, "Cirak: XP carpani 1,10");
        W.Unlock("member", "kacak"); W.SetMember("kacak");
        Check(Mathf.IsEqualApprox(P.MemberSpeed, 0.08f), $"Kacak: hiz eki {P.MemberSpeed}");
        Check(Mathf.IsZeroApprox(_d.State.Stamina.DrainMult), "Kacak: depar harcamaz");
        W.Unlock("member", "pehlivan"); W.SetMember("pehlivan");
        Check(Mathf.IsEqualApprox(P.MaxHp, 125f), $"Pehlivan: can 125 ({P.MaxHp})");
        Check(Mathf.IsEqualApprox(P.MemberSpeed, 0f) && _d.State.Stamina.DrainMult > 0.5f, "Pehlivan: Kacak etkisi kalkti");
        W.Unlock("member", "muhtar"); W.SetMember("muhtar");
        var entry = _d.Data.Catalog.Entries.FirstOrDefault(e => e.Price > 0);
        if (entry != null)
        {
            int disc = _d.State.Catalog.Price(entry);
            Check(disc == (int)Math.Round(entry.Price * 0.9, MidpointRounding.AwayFromZero), $"Muhtar: katalog -%10 ({entry.Price}->{disc})");
        }
        Check(Mathf.IsEqualApprox(_d.State.Cargo.IncomeMult?.Invoke() ?? 0, 1.1f), "Muhtar: gelir x1,10");
        W.Unlock("member", "arici"); W.SetMember("arici");
        Check(Mathf.IsEqualApprox(_d.Data.Crops.Genetics.MutationMult, 1.5f), "Arici: mutasyon x1,5");
        W.Unlock("member", "gidak_kostumlu"); W.SetMember("gidak_kostumlu");
        Check(P.Glide && Mathf.IsEqualApprox(_d.Data.Crops.Genetics.MutationMult, 1f), "Gidak Kostumlu: suzulme, Arici etkisi kalkti");
        W.SetMember("cirak");
        Check(!P.Glide, "Cirak: suzulme yok");

        // ---- 2) kozmetik ----
        var skel = P.Skeleton;
        Check(skel != null, "iskelet yok");
        foreach (var c in _d.Data.Cosmetics.Cosmetics.Where(c => c.Slot == "kask" && c.Look.GetValueOrDefault("kind") != "none"))
        {
            Wear(c.Id);
            await Seconds(0.05f);
            var h = skel.FindChild("CosHelmet", false, false);
            Check(h != null && h.GetChildCount() > 0 && h.GetChild(0).GetChildCount() > 0, $"kask gorunmuyor: {c.Id}");
        }
        Wear("kask_standart");
        await Seconds(0.05f);
        Check(skel.FindChild("CosHelmet", false, false) == null, "standart kask: ek mesh kalmadi");
        foreach (var c in _d.Data.Cosmetics.Cosmetics.Where(c => c.Slot == "canta"))
        {
            Wear(c.Id);
            await Seconds(0.05f);
            var h = skel.FindChild("CosCharm", false, false);
            Check(h != null && h.GetChild(0).GetChildCount() > 0, $"canta susu gorunmuyor: {c.Id}");
        }
        Wear("canta_bayrak"); await Seconds(0.1f);
        { var ch = (Node3D)skel.FindChild("CosCharm", false, false).GetChild(0); var bag = skel.GetParent().FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault(m => m.Name.ToString().Contains("Backpack")); var bb = bag != null ? bag.GlobalTransform * bag.GetAabb() : default; int bi = skel.FindBone("Backpack"); var gp = skel.GlobalTransform * skel.GetBoneGlobalPose(bi); GD.Print($"BONE origin={gp.Origin} X={gp.Basis.X} Y={gp.Basis.Y} Z={gp.Basis.Z} skelScale={skel.GlobalTransform.Basis.Scale} parent={skel.GetBoneName(skel.GetBoneParent(bi))}"); GD.Print($"CHARM pos={ch.GlobalPosition} bagAabb={bb.Position}..{bb.End} player={P.GlobalPosition}"); }
        W.Unequip("canta");
        await Seconds(0.05f);
        Check(skel.FindChild("CosCharm", false, false) == null, "canta susu cikarilinca kalmadi");
        var body = skel.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => m.Skin != null).OrderByDescending(m => m.Mesh.GetFaces().Length).First();
        Wear("tulum_ekose");
        await Seconds(0.05f);
        Check(body.GetSurfaceOverrideMaterial(0) is ShaderMaterial, "tulum deseni: govde golgelendirici yok");
        Wear("tulum_kooperatif_mavisi");
        await Seconds(0.05f);
        Check(body.GetSurfaceOverrideMaterial(0) == null, "varsayilan tulum: orijinal malzeme");
        Wear("capa_kristal");
        await Seconds(0.05f);
        Check(P.CapaNode.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Any(m => m.MaterialOverlay != null), "Capa kaplamasi uygulanmadi");
        Wear("capa_fabrika");
        Wear("vizor_kalp");
        await Seconds(0.05f);
        Check(P.Visor != null, "vizor yok");

        // ---- 3) jest ----
        Wear("jest_alkis");
        _d.PlayGesture();
        await Seconds(0.2f);
        Check(P.CurrentClip == "cheer", $"jest klibi oynamiyor ({P.CurrentClip})");

        // ---- 4) menu ----
        _d.OpenWardrobe();
        Check(_d.Hud.MenuOpen && _d.Hud.MenuTitleText == "Dolap" && _d.Hud.MenuRows == 8, $"dolap menusu ({_d.Hud.MenuTitleText}, {_d.Hud.MenuRows})");
        _d.Hud.HideMenu();

        // ---- goruntuler ----
        if (!_window) return;
        await Seconds(3.5f); // jest klibi bitsin (kol vizoru kapatmasin)
        P.Visual.Rotation = Vector3.Zero;
        W.Unlock("member", "cirak");
        async Task Face(string id, string name) { Wear(id); _off = new Vector3(0.1f, 1.55f, 1.5f); _lookY = 1.45f; await Shot(name); }
        await Face("vizor_nokta_goz", "01_vizor_nokta_goz");
        foreach (var (id, n) in new[] { ("vizor_kalp", "kalp"), ("vizor_yildiz", "yildiz"), ("vizor_kizgin", "kizgin"), ("vizor_gozluklu", "gozluklu"), ("vizor_biyikli", "biyikli"), ("vizor_piksel", "piksel"), ("vizor_bos_ekran", "bos_ekran"), ("vizor_aglamakli", "aglamakli") })
            await Face(id, $"02_vizor_{n}");
        Wear("vizor_nokta_goz");
        _off = new Vector3(1.2f, 1.7f, 1.6f); _lookY = 1.35f;
        foreach (var k in new[] { "hasir", "dalgic", "boynuzlu", "aurora", "kaynakci", "director" })
        {
            string id = k == "director" ? "kask_holding_muduru" : $"kask_{k}";
            Wear(id); await Shot($"03_kask_{k}");
        }
        Wear("kask_standart");
        _off = new Vector3(0f, 1.0f, 3.6f); _lookY = 0.85f;
        foreach (var k in new[] { "ekose", "kamuflaj", "altin", "aurora", "cizgili", "holding_kirmizisi" }) { Wear($"tulum_{k}"); await Shot($"04_tulum_{k}"); }
        Wear("tulum_kooperatif_mavisi");
        _off = new Vector3(0.9f, 1.0f, -2.8f); _lookY = 0.95f; // arkadan (canta susu)
        foreach (var k in new[] { "bayrak", "fener", "firildak", "gidak_pelusu", "tencere", "caydanlik" }) { Wear($"canta_{k}"); await Shot($"05_canta_susu_{k}"); }
        W.Unequip("canta");
        P.Visual.Rotation = Vector3.Zero;
        _off = new Vector3(-2.2f, 1.1f, 2.2f); _lookY = 0.9f;
        P.Yaw.GlobalRotation = new Vector3(0, P.Visual.GlobalRotation.Y + Mathf.Pi, 0);
        foreach (var k in new[] { "krom", "altin", "kristal", "cicekli" }) { Wear($"capa_{k}"); await Shot($"06_capa_{k}"); }
    }
}
