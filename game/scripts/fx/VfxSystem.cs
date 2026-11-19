using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Efekt kitapliginin oyun baglantisi (Director.Vfx.cs kurar): savas hedeflerinin durum aurasi (yanik, donma, sok, zehir,
/// kabarik), oyun olaylarina bagli efektler (tarim, hayvan, yapi, yabani yaratik telgrafi) ve Kabarma gecesinin
/// gokyuzu dalgasi, zemin damarlari, Maya akisi ve safakta sonme. Sistemlerin kendi dosyalarina yalniz tek satir Vfx.* cagrisi girer.
/// </summary>
public partial class VfxSystem : Node
{
    readonly Director _d;
    readonly Dictionary<ICombatTarget, Dictionary<string, GpuParticles3D>> _auras = new();
    float _scanT, _veinT, _waveT, _flowT;
    bool _night;

    public int ActiveAuras => _auras.Sum(kv => kv.Value.Count);
    /// <summary>Sinama: durum olarak kurulan aura sayisi (tur basina).</summary>
    public readonly Dictionary<string, int> AuraCounts = new();

    public VfxSystem(Director d) { _d = d; }

    public override void _EnterTree()
    {
        Events.WildActed += OnWild;
        Events.NightStarted += OnNight;
        Events.Dawn += OnDawn;
        Events.Harvested += OnHarvest;
        Events.AnimalTamed += OnTamed;
        Events.AnimalCollected += OnCollected;
        Events.Laid += OnLaid;
        Events.PiecePlaced += OnPlaced;
        Events.StationPlaced += OnPlaced;
        Events.PieceUpgraded += OnUpgraded;
        Events.Deconstructed += OnDecon;
        Events.Produced += OnProduced;
    }

    public override void _ExitTree()
    {
        Events.WildActed -= OnWild;
        Events.NightStarted -= OnNight;
        Events.Dawn -= OnDawn;
        Events.Harvested -= OnHarvest;
        Events.AnimalTamed -= OnTamed;
        Events.AnimalCollected -= OnCollected;
        Events.Laid -= OnLaid;
        Events.PiecePlaced -= OnPlaced;
        Events.StationPlaced -= OnPlaced;
        Events.PieceUpgraded -= OnUpgraded;
        Events.Deconstructed -= OnDecon;
        Events.Produced -= OnProduced;
    }

    Vector3 PlayerPos => _d.Player != null ? _d.Player.GlobalPosition : Vector3.Zero;
    Vector3 Ahead => _d.Player != null ? _d.Player.GlobalPosition + _d.Player.Visual.GlobalBasis.Z.Normalized() * 1.6f : Vector3.Zero;

    // ---------------- olaylar ----------------

    Vector3 Ground(float x, float z) => new(x, _d.World?.HeightAt(x, z) ?? PlayerPos.Y, z);

    void OnWild(string species, string what, float x, float z)
    {
        var p = Ground(x, z);
        switch (what)
        {
            case "Warned": Vfx.Telegraph(p, 2.6f, 0.9f, false); Vfx.Play("alert_mark", p + Vector3.Up * 2f); break;
            case "Alerted": Vfx.Play("alert_mark", p + Vector3.Up * 2f); break;
            case "Struck": Vfx.Play("hit_spark", p + Vector3.Up * 0.8f); Vfx.Telegraph(p, 1.6f, 0.25f, true); break;
            case "Down": Vfx.Play("hit_dust", p + Vector3.Up * 0.4f); break;
        }
    }

    void OnHarvest(int n) => Vfx.Play("farm_harvest", Ahead + Vector3.Up * 0.5f);
    void OnTamed(string sp) { Vfx.Play("animal_heart", Ahead + Vector3.Up * 1.4f); Vfx.Play("animal_trust", Ahead); }
    void OnCollected(string item, int n) => Vfx.Play("animal_product", Ahead + Vector3.Up * 0.5f);
    void OnLaid(string product, string where) => Vfx.Play("animal_product", Ahead + Vector3.Up * 0.3f);
    void OnPlaced(string id) => Vfx.Play("build_dust", Ahead);
    void OnUpgraded(string id, string mat) => Vfx.Play("build_upgrade", Ahead);
    void OnDecon(string id) => Vfx.Play("build_collapse", Ahead + Vector3.Up * 0.3f);
    void OnProduced(string item, int n) { if (n > 0) Vfx.Play("eng_steam", Ahead + Vector3.Up * 1.2f); }

    void OnNight()
    {
        _night = true; _waveT = 0; _veinT = 0;
        SkyWave();
    }

    void OnDawn()
    {
        _night = false;
        Vfx.Play("kabarma_dawn", PlayerPos + Vector3.Up * 0.3f);
    }

    public void SkyWave()
    {
        var p = PlayerPos;
        Vfx.Play("kabarma_sky_wave", p);                                  // yerde yayilan dalga
        Vfx.Ring(p + Vector3.Up * 30f, 110f, 4.5f, Palette.MayaViolet, 5f); // gokyuzunde yayilan dalga
    }

    // ---------------- zaman ----------------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _scanT += dt;
        if (_scanT >= Vfx.Data.StatusScanSeconds) { _scanT = 0; ScanStatuses(); }
        if (!_night || _d.Player == null) return;
        var f = Vfx.Data;
        _veinT += dt; _waveT += dt; _flowT += dt;
        if (_veinT >= f.VeinEvery)
        {
            _veinT = 0;
            var a = GD.Randf() * Mathf.Tau; var r = 6f + GD.Randf() * f.VeinRadius;
            var pos = PlayerPos + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Vfx.Play("kabarma_vein", Ground(pos.X, pos.Z) + Vector3.Up * 0.05f);
        }
        if (_waveT >= f.SkyWaveEvery) { _waveT = 0; SkyWave(); }
        if (_flowT >= 0.35f)
        {
            _flowT = 0;
            var k = _d.Night?.Kazan;
            if (k != null) Vfx.Play("kabarma_maya_flow", PlayerPos + Vector3.Up * 0.9f);
        }
    }

    void ScanStatuses()
    {
        if (_d.Savas == null) return;
        var seen = new HashSet<ICombatTarget>();
        foreach (var t in _d.Savas.All())
        {
            if (t is not Node3D node || t.Body == null) continue;
            var want = new HashSet<string>();
            foreach (var s in t.Body.Statuses)
                want.Add(s.Kind switch
                {
                    StatusKind.Burn => "status_burn",
                    StatusKind.Freeze or StatusKind.Lock => "status_freeze",
                    StatusKind.Stun => "status_shock",
                    StatusKind.Poison => "status_poison",
                    _ => null,
                });
            if (t.Body.Swell > 0.15f) want.Add("status_swell");
            want.Remove(null);
            if (want.Count == 0 && !_auras.ContainsKey(t)) continue;
            seen.Add(t);
            if (!_auras.TryGetValue(t, out var have)) _auras[t] = have = new Dictionary<string, GpuParticles3D>();
            foreach (var id in want.Where(w => !have.ContainsKey(w)).ToList())
            {
                var p = Vfx.Loop(id, node, node.ToLocal(t.Center), t.Radius);
                if (p == null) continue;
                have[id] = p;
                AuraCounts[id] = AuraCounts.GetValueOrDefault(id) + 1;
            }
            foreach (var id in have.Keys.Where(k => !want.Contains(k)).ToList()) { Vfx.Stop(have[id]); have.Remove(id); }
        }
        // hedefi gitmis (olmus, serbest birakilmis) auralari birak
        foreach (var t in _auras.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            foreach (var p in _auras[t].Values) Vfx.Stop(p);
            _auras.Remove(t);
        }
    }
}
