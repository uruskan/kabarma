using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuncu gucleri (GDD 17, 17.1; ICERIK 7): girdi (ability_1, ability_2, ulti), Sarj ve olcer (Sim AbilityRules),
/// bes etkin gucun ve iki ultinin dunyadaki etkisi. Kadim Tas denemeleri KadimTasSystem, Fener ve Tatbikat
/// TatbikatSystem, ortak oyun AbilitySystem.Net.cs. Elektrik agi (PowerSystem) ile ilgisi yok.
/// Hasar: Kabarma dusmani (Zipzip.Hit), deneme ve Tatbikat hayaleti (TrialGhost.Hit). GECICI: savas akisinin Combat*
/// arayuzu gelince ortak hasar girisine baglanir.
/// </summary>
public partial class AbilitySystem : Node3D
{
    public readonly Director D;
    public AbilitiesFile F => D.Data.Abilities;
    public AbilityLoadout L => D.State.Abilities.Local;
    public readonly List<TrialGhost> Ghosts = new();
    public KadimTasSystem Stones { get; private set; }
    public TatbikatSystem Drill { get; private set; }

    /// <summary>Player.Hurt'un ilk satiri: Atilma ani, Kalkan Balonu, Dev Mod. Donen hasar 0 ise vurus yok sayilir.</summary>
    public static System.Func<Player, float, Vector3, float> HurtFilter;

    // Suren etkiler
    float _dashT; Vector3 _dashDir; float _dashSpeed;
    float _bubbleT; Vector3 _bubblePos; MeshInstance3D _bubble;
    float _decoyT, _decoyHp; Node3D _decoy; float _decoyPushT;
    float _fogT; Vector3 _fogPos; MeshInstance3D _fog;
    float _herdT; readonly List<(Node3D n, Vector3 from, Vector3 to)> _herd = new(); readonly HashSet<Node> _herdHit = new();
    float _giantTick; float _giantScale = 1f;
    float _lastAtaHp = -1;

    /// <summary>Son kullanimin kaydi (test ve gunluk icin).</summary>
    public string LastUsed { get; private set; } = "";
    public int Uses { get; private set; }

    public AbilitySystem(Director d) { D = d; Name = "Abilities"; }

    public override void _Ready()
    {
        AbilityRules.Normalize(F, L);
        AbilityRules.AutoEquip(F, D.State.Abilities, L, D.State.Atalar.Ultis);
        HurtFilter = Filter;
        Events.CreatureKilled += OnKilled;
        Stones = new KadimTasSystem(this);
        AddChild(Stones);
        Drill = new TatbikatSystem(this);
        AddChild(Drill);
        AddChild(new AbilityHud(this));
    }

    public override void _ExitTree()
    {
        Events.CreatureKilled -= OnKilled;
        if (HurtFilter == (System.Func<Player, float, Vector3, float>)Filter) HurtFilter = null;
    }

    /// <summary>Kayit yuklendikten sonra: yuva boyu ve otomatik takma (afterRestore).</summary>
    public void AfterRestore()
    {
        AbilityRules.Normalize(F, L);
        AbilityRules.AutoEquip(F, D.State.Abilities, L, D.State.Atalar.Ultis);
        Stones?.Rebuild();
    }

    void OnKilled(string id, bool sac)
    {
        // Olcer yalniz Kabarma'da dolar (GDD 17). Hayaletler bu olayi yaymaz.
        if (!D.NightActive) return;
        AbilityRules.OnKill(F, L, elite: false); // elit onekleri kabarma3 ile (KS 1-10): o zamana kadar hepsi siradan
    }

    float Filter(Player p, float dmg, Vector3 from)
    {
        if (p != D.Player) return dmg;
        if (_bubbleT > 0 && p.GlobalPosition.DistanceTo(_bubblePos) <= Def("kalkan_balonu").Radius
            && from.DistanceTo(_bubblePos) > Def("kalkan_balonu").Radius)
        {
            Fx.Burst(_bubblePos + (from - _bubblePos).Normalized() * Def("kalkan_balonu").Radius + Vector3.Up, Palette.MayaTeal, 8, 3f, 0.08f, 0.35f, true, -1f);
            return 0;
        }
        return AbilityRules.FilterDamage(F, L, dmg);
    }

    AbilityDef Def(string id) => F.Def(id);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        AbilityRules.Tick(F, L, dt);
        TickPeers(dt); // AbilitySystem.Net.cs: host'ta istemci aynalari
        KadimTasRules.TickRetry(D.State.KadimTas, dt);
        TickAtaMeter();
        ReadInput();
        TickDash(dt);
        TickBubble(dt);
        TickDecoy(dt);
        TickFog(dt);
        TickHerd(dt);
        TickGiant(dt);
        TickM4(dt);
    }

    void TickAtaMeter()
    {
        var f = D.State.Atalar.Active;
        if (f == null || f.MaxHp <= 0) { _lastAtaHp = -1; return; }
        if (_lastAtaHp >= 0 && f.Hp < _lastAtaHp) AbilityRules.OnAtaDamage(F, L, (_lastAtaHp - f.Hp) / f.MaxHp);
        _lastAtaHp = f.Hp;
    }

    void ReadInput()
    {
        var p = D.Player;
        if (p == null || p.Down || p.UiCapture || D.Hud.MenuOpen) return;
        if (D.Night?.Offer != null) return; // kart secimi kolun yon tuslarini kullanir
        if (Input.IsActionJustPressed("ability_1")) TryUse(0);
        if (Input.IsActionJustPressed("ability_2")) TryUse(1);
        if (Input.IsActionJustPressed("ulti")) TryUse(-1);
    }

    /// <summary>Yuvadaki gucu kullanir (slot -1: ulti). Kullanildiysa true; olmuyorsa ipucu.</summary>
    public bool TryUse(int slot)
    {
        AbilityRules.Normalize(F, L);
        if (slot >= F.ActiveSlots) { D.Hud.Hint("Bu yuva henüz kilitli (Kurbağa Ağa)", 1.5f); return false; }
        string id = slot < 0 ? L.Ulti : L.Slots[slot];
        if (string.IsNullOrEmpty(id)) { D.Hud.Hint(slot < 0 ? "Ulti yok: Atalar ulti verir" : "Güç yok: Kadim Taş denemesini geç", 1.5f); return false; }
        var why = AbilityRules.CanUse(F, L, id);
        if (why != null) { D.Hud.Hint($"{Def(id).Name}: {why}", 1.2f); Sfx.Play("hit", null, -12f); return false; }
        var aim = AimFlat();
        var at = D.Player.GlobalPosition;
        if (ForwardUse(id, at, aim)) { AbilityRules.Use(F, L, id); Uses++; LastUsed = id; return true; } // AbilitySystem.Net.cs: istemci
        var def = AbilityRules.Use(F, L, id);
        Apply(def, at, aim, local: true);
        Uses++;
        LastUsed = id;
        GD.Print($"ABILITY use {id} charge={L.Charge:0} meter={L.Meter:0}");
        return true;
    }

    /// <summary>Nisanin yatay yonu (yoksa oyuncunun bakisi).</summary>
    Vector3 AimFlat()
    {
        var p = D.Player;
        var f = -p.Cam.GlobalBasis.Z with { Y = 0 };
        if (p.AimOverride is Vector3 o) f = (o - p.GlobalPosition) with { Y = 0 };
        return f.LengthSquared() < 0.001f ? Vector3.Forward : f.Normalized();
    }

    /// <summary>Gucun dunyadaki etkisi. local: kullanan bu makinedeki oyuncu (Atilma ve Dev Mod yalniz onu oynatir).</summary>
    public void Apply(AbilityDef def, Vector3 at, Vector3 aim, bool local)
    {
        Vfx.Power(def.Id, at + Vector3.Up * 0.1f, aim); // fx/Vfx.cs: power.<kimlik> (yoksa power.default); kusam yeni gucler icin vfx.json'a satir ekler
        switch (def.Id)
        {
            case "atilma":
                if (!local) break;
                _dashT = def.Duration;
                _dashDir = MoveDir(aim);
                _dashSpeed = def.Distance / Mathf.Max(0.05f, def.Duration);
                Sfx.Play("pickup", at, -4f);
                Fx.Burst(at + Vector3.Up * 0.8f, Palette.MayaTeal, 14, 3f, 0.1f, 0.4f, true, -1f);
                break;
            case "kalkan_balonu":
                _bubbleT = def.Duration;
                _bubblePos = at;
                _bubble ??= Dome(def.Radius, Palette.MayaTeal, 0.22f);
                _bubble.GlobalPosition = at;
                _bubble.Visible = true;
                Sfx.Play("place", at, -2f);
                break;
            case "sok_dalgasi":
                Shock(def, at);
                break;
            case "yem_hologrami":
                _decoyT = def.Duration;
                _decoyHp = def.Hp;
                _decoy ??= Hologram();
                _decoy.GlobalPosition = D.World.Ground(at + aim * 2.5f);
                _decoy.Visible = true;
                Sfx.Play("place", at, -4f);
                break;
            case "sifa_sisi":
                _fogT = def.Duration;
                _fogPos = at;
                _fog ??= Dome(def.Radius, new Color(0.55f, 1f, 0.7f), 0.18f, flat: 0.35f);
                _fog.GlobalPosition = at;
                _fog.Visible = true;
                Sfx.Play("levelup", at, -8f);
                break;
            case "suru_cagrisi":
                Stampede(def, at, aim);
                break;
            case "dev_mod":
                if (!local) break;
                Hud().Big("DEV MOD", Palette.MayaViolet, 2f);
                Sfx.Play("levelup", at, -2f);
                break;
            default:
                ApplyM4(def, at, aim, local); // AbilitySystem.M4.cs: kusam gucleri
                break;
        }
    }

    Hud Hud() => D.Hud;

    Vector3 MoveDir(Vector3 aim)
    {
        var v = D.Player.Velocity with { Y = 0 };
        return v.Length() > 1f ? v.Normalized() : aim;
    }

    // ---------- Atilma ----------

    void TickDash(float dt)
    {
        if (_dashT <= 0) return;
        _dashT -= dt;
        var p = D.Player;
        p.MoveAndCollide(_dashDir * _dashSpeed * dt);
        float gy = D.World.HeightAt(p.GlobalPosition.X, p.GlobalPosition.Z);
        if (p.GlobalPosition.Y < gy) p.GlobalPosition = p.GlobalPosition with { Y = gy };
        Fx.Burst(p.GlobalPosition + Vector3.Up * 0.9f, Palette.MayaTeal, 2, 0.5f, 0.12f, 0.3f, true, 0f);
    }

    // ---------- Kalkan Balonu ----------

    void TickBubble(float dt)
    {
        if (_bubbleT <= 0) return;
        _bubbleT -= dt;
        float r = Def("kalkan_balonu").Radius;
        // Dusman iceri giremez: kubbenin kenarina itilir (mermi ve govde gecmez).
        foreach (var g in Ghosts) if (!g.Dead && g.GlobalPosition.DistanceTo(_bubblePos) < r) g.GlobalPosition = D.World.Ground(_bubblePos + ((g.GlobalPosition - _bubblePos) with { Y = 0 }).Normalized() * r);
        foreach (var z in Zipzip.All) if (!z.Dead && (z.GlobalPosition - _bubblePos).Length() < r) z.Push((z.GlobalPosition - _bubblePos) with { Y = 0 }, 4f);
        float k = _bubbleT < 0.5f ? _bubbleT / 0.5f : 1f;
        _bubble.Scale = Vector3.One * Mathf.Max(0.01f, k);
        if (_bubbleT <= 0) _bubble.Visible = false;
    }

    // ---------- Sok Dalgasi ----------

    void Shock(AbilityDef def, Vector3 at)
    {
        Sfx.Play("hit", at, 0f);
        Fx.Burst(at + Vector3.Up * 0.4f, Palette.MayaTeal, 40, def.Radius * 2.2f, 0.12f, 0.45f, true, 0f);
        Fx.Burst(at + Vector3.Up * 0.4f, Palette.OffWhite, 20, def.Radius * 1.6f, 0.08f, 0.35f, true, 0f);
        int hits = 0;
        foreach (var z in Zipzip.All.ToArray())
        {
            if (z.Dead || !AbilityRules.InRadius(at.X, at.Z, def.Radius, z.GlobalPosition.X, z.GlobalPosition.Z)) continue;
            var (kx, kz) = AbilityRules.Knock(def, at.X, at.Z, z.GlobalPosition.X, z.GlobalPosition.Z, z.Scale0);
            var k = new Vector3(kx, 0, kz);
            z.Hit(def.Damage, false, k, k.Length(), false);
            hits++;
        }
        foreach (var g in Ghosts.ToArray())
        {
            if (g.Dead || !AbilityRules.InRadius(at.X, at.Z, def.Radius, g.GlobalPosition.X, g.GlobalPosition.Z)) continue;
            var (kx, kz) = AbilityRules.Knock(def, at.X, at.Z, g.GlobalPosition.X, g.GlobalPosition.Z);
            g.Hit(def.Damage, new Vector3(kx, 0, kz));
            hits++;
        }
        // Gunduz yabanileri (yaratik2): kisa dikey dogruyla alan vurusu; ilk degeni vurur, tekrar ederek hepsine.
        var wildHit = new HashSet<Node3D>();
        for (int i = 0; i < 24 && D.Wildlife != null && D.Wildlife.ProjectileHit(at, at + Vector3.Up * 1.5f, def.Radius, def.Damage, wildHit); i++) hits++;
        GD.Print($"ABILITY shock hits={hits}");
    }

    // ---------- Yem Hologrami ----------

    /// <summary>Yakinda Yem Hologrami varsa onu dondurur (hayaletler ona saldirir).</summary>
    public Node3D DecoyNear(Vector3 pos) =>
        _decoyT > 0 && _decoy != null && _decoy.GlobalPosition.DistanceTo(pos) <= Def("yem_hologrami").Radius ? _decoy : null;

    public void HitDecoy(float dmg)
    {
        _decoyHp -= dmg;
        Fx.Burst(_decoy.GlobalPosition + Vector3.Up, Palette.MayaViolet, 6, 2f, 0.08f, 0.3f, true, -1f);
        if (_decoyHp <= 0) _decoyT = 0.01f;
    }

    void TickDecoy(float dt)
    {
        if (_decoyT <= 0) return;
        _decoyT -= dt;
        _decoy.RotateY(dt * 1.5f);
        // Kabarma dusmani: menzildekiler hologramin ustune itilir (Zipzip hedef secimi yaratik akisinda).
        _decoyPushT -= dt;
        if (_decoyPushT <= 0)
        {
            _decoyPushT = 0.35f;
            float r = Def("yem_hologrami").Radius;
            foreach (var z in Zipzip.All)
            {
                var to = (_decoy.GlobalPosition - z.GlobalPosition) with { Y = 0 };
                if (!z.Dead && to.Length() < r && to.Length() > 1f) z.Push(to.Normalized(), 3.5f);
            }
        }
        if (_decoyT <= 0) { _decoy.Visible = false; Fx.Burst(_decoy.GlobalPosition + Vector3.Up, Palette.MayaViolet, 14, 3f, 0.1f, 0.5f, true, -1f); }
    }

    // ---------- Sifa Sisi ----------

    void TickFog(float dt)
    {
        if (_fogT <= 0) return;
        _fogT -= dt;
        var def = Def("sifa_sisi");
        var p = D.Player;
        if (!p.Down && p.GlobalPosition.DistanceTo(_fogPos) <= def.Radius && p.Hp < p.MaxHp)
            p.Hp = Mathf.Min(p.MaxHp, p.Hp + def.Heal * dt);
        if (GD.Randf() < dt * 8f) Fx.Burst(_fogPos + new Vector3(GD.Randf() * 2 - 1, 0.3f, GD.Randf() * 2 - 1) * def.Radius * 0.7f, new Color(0.6f, 1f, 0.75f), 3, 0.6f, 0.2f, 1.2f, true, -0.5f);
        if (_fogT <= 0) _fog.Visible = false;
    }

    // ---------- Suru Cagrisi ----------

    void Stampede(AbilityDef def, Vector3 at, Vector3 aim)
    {
        // YER TUTUCU: evcil hayvanlar hayvan akisinin dugumlerinde; M3'te hayalet suru kosar (ICERIK 7 M3 sayilari).
        foreach (var (n, _, _) in _herd) n.QueueFree();
        _herd.Clear();
        _herdHit.Clear();
        var side = aim.Cross(Vector3.Up).Normalized();
        for (int i = 0; i < def.GhostAnimals; i++)
        {
            float off = (i - (def.GhostAnimals - 1) / 2f) * 1.6f;
            var from = D.World.Ground(at - aim * 2f + side * off);
            var to = D.World.Ground(from + aim * def.Distance);
            var n = GhostAnimal();
            AddChild(n);
            n.GlobalPosition = from;
            n.LookAt(to with { Y = from.Y }, Vector3.Up);
            _herd.Add((n, from, to));
        }
        _herdT = def.Duration;
        Sfx.Play("siren", at, -10f);
        Hud().Big("SÜRÜ ÇAĞRISI", Palette.Warning, 1.6f);
    }

    void TickHerd(float dt)
    {
        if (_herdT <= 0) return;
        var def = Def("suru_cagrisi");
        _herdT -= dt;
        float k = 1f - Mathf.Clamp(_herdT / def.Duration, 0, 1);
        foreach (var (n, from, to) in _herd)
        {
            var p = D.World.Ground(from.Lerp(to, k));
            n.GlobalPosition = p + Vector3.Up * Mathf.Abs(Mathf.Sin(k * 40f)) * 0.25f;
            foreach (var z in Zipzip.All.ToArray())
                if (!z.Dead && !_herdHit.Contains(z) && z.GlobalPosition.DistanceTo(p) < 1.4f + z.Radius)
                { _herdHit.Add(z); var dir = (to - from).Normalized(); z.Hit(def.Damage, false, dir, def.Knock, false); }
            foreach (var g in Ghosts.ToArray())
                if (!g.Dead && !_herdHit.Contains(g) && g.GlobalPosition.DistanceTo(p) < 1.4f + TrialGhost.Radius)
                { _herdHit.Add(g); g.Hit(def.Damage, (to - from).Normalized() * def.Knock); }
        }
        if (_herdT <= 0) { foreach (var (n, _, _) in _herd) { Fx.Burst(n.GlobalPosition + Vector3.Up * 0.5f, Palette.Warning, 8, 2f, 0.1f, 0.4f, true, -1f); n.QueueFree(); } _herd.Clear(); }
    }

    // ---------- Dev Mod ----------

    void TickGiant(float dt)
    {
        var def = Def("dev_mod");
        if (def == null) return;
        float want = L.GiantLeft > 0 ? def.Scale : 1f;
        _giantScale = Mathf.MoveToward(_giantScale, want, dt * 3f);
        var p = D.Player;
        if (p?.Visual != null) p.Visual.Scale = Vector3.One * _giantScale;
        if (L.GiantLeft <= 0) return;
        _giantTick -= dt;
        if (_giantTick > 0) return;
        _giantTick = 0.5f;
        float r = def.Radius * _giantScale;
        var at = p.GlobalPosition;
        foreach (var z in Zipzip.All.ToArray())
            if (!z.Dead && z.GlobalPosition.DistanceTo(at) < r + z.Radius) z.Hit(def.Damage * 0.5f, false, (z.GlobalPosition - at) with { Y = 0 }, 4f, false);
        foreach (var g in Ghosts.ToArray())
            if (!g.Dead && g.GlobalPosition.DistanceTo(at) < r + TrialGhost.Radius) g.Hit(def.Damage * 0.5f, ((g.GlobalPosition - at) with { Y = 0 }).Normalized() * 4f);
    }

    // ---------- Capa silahlari ----------

    /// <summary>Capa mermisi bu dogru parcasinda bir hayalete degdi mi (Capa.Weapons.cs satiri). Degdiyse vurur.</summary>
    public bool GhostHitSegment(Vector3 from, Vector3 to, float radius, float damage, ISet<Node3D> already = null)
    {
        var seg = to - from;
        foreach (var g in Ghosts.ToArray())
        {
            if (g.Dead || (already != null && already.Contains(g))) continue;
            var c = g.Center;
            float t = Mathf.Clamp((c - from).Dot(seg) / Mathf.Max(0.0001f, seg.LengthSquared()), 0, 1);
            if ((from + seg * t).DistanceTo(c) > TrialGhost.Radius + radius) continue;
            already?.Add(g);
            g.Hit(damage, seg.Normalized() * 2f);
            return true;
        }
        return false;
    }

    // ---------- Gorunus (koddan, YER TUTUCU) ----------

    public static StandardMaterial3D GhostMaterial(Color c, float alpha)
    {
        return new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            AlbedoColor = new Color(1, 1, 1, alpha),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = c,
            EmissionEnergyMultiplier = 0.8f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
    }

    MeshInstance3D Dome(float r, Color c, float alpha, float flat = 1f)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = r, Height = r * 2f * flat, RadialSegments = 32, Rings = 16 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = c with { A = alpha },
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 0.6f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                RimEnabled = true,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(mi);
        return mi;
    }

    Node3D Hologram()
    {
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.62f, 0.9f, 0.45f), MeshKit.At(0, 0.75f, 0), Palette.CoopBlue, 0.5f);
        kit.Sphere(0.4f, MeshKit.At(0, 1.45f, 0), Palette.OffWhite, 14, 7);
        kit.Pillow(new Vector3(0.5f, 0.3f, 0.2f), MeshKit.At(0, 1.45f, 0.3f), Palette.MayaTeal, 0.45f);
        var n = new Node3D { Visible = false };
        n.AddChild(kit.Instance(GhostMaterial(Palette.MayaViolet, 0.45f)));
        AddChild(n);
        return n;
    }

    Node3D GhostAnimal()
    {
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.7f, 0.6f, 0.9f), MeshKit.At(0, 0.5f, 0), Palette.OffWhite, 0.6f);
        kit.Sphere(0.24f, MeshKit.At(0, 0.95f, -0.38f), Palette.OffWhite, 10, 5);
        kit.Pillow(new Vector3(0.12f, 0.1f, 0.18f), MeshKit.At(0, 0.92f, -0.62f), Palette.Warning, 0.4f);
        var n = new Node3D();
        n.AddChild(kit.Instance(GhostMaterial(Palette.Warning, 0.6f)));
        return n;
    }
}
