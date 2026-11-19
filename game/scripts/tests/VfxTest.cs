using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Efekt kitapligi sinamasi (--test=vfx; M4 efekt akisi). 1) vfx.json'daki her efekt kimligi calinir (hata yok, havuz sinirli).
/// 2) Savas hedefine yanik, donma, sok, zehir ve kabarik verilince durum aurasi kurulur, durum bitince birakilir.
/// 3) Savas isabeti kitapligi cagirir (CombatSystem.Hit -> hit_*). 4) Capa basligi ve guc kancalari tarifli/tarifsiz kimlikle calisir.
/// Pencereli kosuda (basliksiz degil) goruntuleri progress/m4/efekt/ altina yazar: vurus, durumlar, kule, tuzak, telgraf, Kabarma.
/// </summary>
public partial class VfxTest : Node
{
    sealed partial class Dummy : Node3D, ICombatTarget
    {
        public CombatBody B = new() { Hp = 400, MaxHp = 400, Team = CombatTeam.Wild, Mass = 3 };
        public Vector3 Center => GlobalPosition + Vector3.Up * 0.9f;
        public float Radius => 0.7f;
        public bool Alive => B.Hp > 0;
        public bool IsWeakPoint => false;
        public CombatBody Body => B;
        public CombatResult TakeHit(CombatHit hit, Vector3 from) => Combat.Apply(((Director)GetTree().Root.FindChild("Director", true, false)).Data.Combat, B, hit);
    }

    static readonly Vector3 Site = new(-400f, 220f, 400f);
    Director _d;
    readonly List<string> _fails = new();
    int _checks, _shots;
    bool _window;
    string _dir;
    Camera3D _cam;
    Vector3 _focus;

    public override void _Ready() { _window = DisplayServer.GetName() != "headless"; CallDeferred(nameof(Begin)); }

    async void Begin()
    {
        try { await Run(); } catch (Exception e) { _fails.Add("istisna: " + e.Message); GD.Print(e.ToString()); }
        GD.Print(_fails.Count == 0 ? $"RESULT ok vfx checks={_checks} shots={_shots} effects={Vfx.Data.Effects.Count} played={Vfx.PlayedIds.Count}" : $"RESULT fail vfx ({_fails.Count}/{_checks}) {string.Join("; ", _fails)}");
        GetTree().Quit(_fails.Count == 0 ? 0 : 1);
    }

    void Check(bool ok, string what) { _checks++; if (!ok) { _fails.Add(what); GD.Print("VFX FAIL " + what); } }
    async Task Seconds(float s) { int n = Mathf.CeilToInt(s * Engine.PhysicsTicksPerSecond); for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override void _Process(double delta)
    {
        if (_cam == null) return;
        _cam.GlobalPosition = _focus + new Vector3(0, 2.6f, 8.5f);
        _cam.LookAt(_focus + Vector3.Up * 1f, Vector3.Up);
        if (!_cam.Current) _cam.MakeCurrent();
    }

    async Task Shot(string name, float wait = 0.22f)
    {
        await Seconds(wait);
        if (!_window) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{name}.png");
        _shots++;
        GD.Print("VFX SHOT " + name);
    }

    async Task Run()
    {
        while ((_d = GetParent().GetNodeOrNull<Director>("Director")) == null || _d.Player == null || _d.VfxSys == null) await Seconds(0.05f);
        while (!(_d.Pod?.IntroDone ?? true)) await Seconds(0.05f);
        await Seconds(0.2f);
        _dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/m4/efekt";
        DirAccess.MakeDirRecursiveAbsolute(_dir);

        // 1) her efekt
        var floor = new StaticBody3D();
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(160, 1, 160) } });
        AddChild(floor);
        floor.GlobalPosition = Site + Vector3.Down * 0.5f;
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0), LightEnergy = 1.1f });
        _cam = new Camera3D { Fov = 50, Far = 400 };
        AddChild(_cam);
        _d.Player.GlobalPosition = Site + new Vector3(-3.5f, 0.05f, 2.5f);
        _focus = Site;
        foreach (var id in Vfx.Data.Effects.Keys.ToList()) Vfx.Play(id, Site + Vector3.Up * 0.3f);
        await Seconds(0.3f);
        Check(Vfx.PlayedIds.Count == Vfx.Data.Effects.Count, $"calinan efekt {Vfx.PlayedIds.Count}/{Vfx.Data.Effects.Count}");
        Vfx.Play("yok_boyle_bir_efekt", Site); // bilinmeyen kimlik sessiz
        Vfx.Head("tarifsiz_baslik", "muzzle", Site);
        Vfx.Power("tarifsiz_guc", Site);
        Check(Vfx.PlayedIds.Contains("head.default.muzzle") && Vfx.PlayedIds.Contains("power.default"), "tarifsiz baslik ve guc varsayilana dusmedi");
        await Seconds(1.2f);

        // 2) durum aurasi
        var dummies = new Dictionary<string, Dummy>();
        float x = -4f;
        foreach (var k in new[] { "burn", "freeze", "shock", "poison", "swell" })
        {
            var dm = new Dummy { Name = "VfxDummy_" + k };
            var mesh = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.45f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) };
            mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.Plant };
            dm.AddChild(mesh);
            AddChild(dm);
            dm.GlobalPosition = Site + new Vector3(x, 0.05f, 0);
            x += 2f;
            _d.Savas.Add(dm);
            dummies[k] = dm;
        }
        var cd = _d.Data.Combat;
        Status.AddBurn(cd, dummies["burn"].B, 2f, 6f);
        Status.AddFreeze(cd, dummies["freeze"].B, 6f);
        Status.AddStun(dummies["shock"].B, 6f);
        Status.AddPoison(cd, dummies["poison"].B, 2f, 6f);
        dummies["swell"].B.Swell = 0.6f;
        _focus = Site;
        await Seconds(0.8f);
        foreach (var k in new[] { "status_burn", "status_freeze", "status_shock", "status_poison", "status_swell" })
            Check(_d.VfxSys.AuraCounts.GetValueOrDefault(k) >= 1, $"{k} aurasi kurulmadi");
        await Shot("04_durumlar_yanik_donma_sok_zehir_kabarik", 0.3f);
        // durum bitince aura birakilir
        int before = _d.VfxSys.ActiveAuras;
        dummies["burn"].B.Statuses.Clear(); dummies["swell"].B.Swell = 0f;
        await Seconds(0.6f);
        Check(_d.VfxSys.ActiveAuras < before, "bitmis durumun aurasi birakilmadi");

        // 3) vurus ve patlama
        var tgt = dummies["poison"];
        _focus = tgt.GlobalPosition;
        var from = tgt.GlobalPosition + new Vector3(2f, 1.2f, 3f);
        int p0 = Vfx.Played;
        _d.Savas.Hit(tgt, new CombatHit(20, DamageType.Kesme, CombatTeam.Player) { Crit = true }, from);
        Check(Vfx.Played > p0, "savas isabeti efekt cagirmadi");
        await Shot("01_vurus_kritik", 0.12f);
        _d.Savas.Hit(dummies["freeze"], new CombatHit(15, DamageType.Ates, CombatTeam.Player), from);
        _focus = dummies["freeze"].GlobalPosition;
        await Shot("02_vurus_ates", 0.1f);
        Vfx.Play("swell_burst", dummies["swell"].GlobalPosition + Vector3.Up);
        _focus = dummies["swell"].GlobalPosition;
        await Shot("03_kabarik_patlamasi", 0.18f);

        // 4) kule, tuzak, telgraf, guc
        _focus = Site + new Vector3(0, 0, -8);
        var tp = _focus + Vector3.Up * 1.6f;
        Vfx.Play("tower_sacma_shot", tp, Vector3.Forward);
        Vfx.Play("tower_civi_shot", tp + Vector3.Left * 2f, Vector3.Forward);
        Vfx.Play("tower_sapan_shot", tp + Vector3.Right * 2f, Vector3.Up);
        await Shot("05_kule_atislari", 0.12f);
        Vfx.Play("trap_diken", _focus + Vector3.Left * 2f); Vfx.Play("trap_firlatma", _focus); Vfx.Play("trap_cukur", _focus + Vector3.Right * 2f);
        await Shot("06_tuzaklar", 0.2f);
        Vfx.Telegraph(_focus, 3f, 1.2f, true);
        await Seconds(0.7f);
        await Shot("07_telgraf_tehlike", 0.05f);
        Vfx.Power("sok_dalgasi", _focus);
        await Shot("08_guc_sok_dalgasi", 0.2f);

        // 5) Kabarma gokyuzu dalgasi ve zemin damarlari
        _focus = _d.Player.GlobalPosition;
        _d.VfxSys.SkyWave();
        for (int i = 0; i < 14; i++) Vfx.Play("kabarma_vein", _focus + new Vector3(GD.Randf() * 16 - 8, 0.05f, GD.Randf() * 10 - 14));
        await Shot("09_kabarma_gokyuzu_dalgasi_damarlar", 1.4f);
        Vfx.Play("kabarma_dawn", _focus + Vector3.Up * 0.3f);
        await Shot("10_safakta_sonme", 0.7f);
        foreach (var dm in dummies.Values) { _d.Savas.Remove(dm); dm.QueueFree(); }
    }
}
