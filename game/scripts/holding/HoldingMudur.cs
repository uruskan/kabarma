using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Mudur'un silahla vurulabilen parcasi (kasa, motor kapagi, jenerator, hoparlor, balon, Mudur): IAtaTarget; hasar MudurEncounter.Hit'ten Sim'e.</summary>
public partial class MudurPart : Node3D, IAtaTarget
{
    readonly MudurEncounter _e;
    readonly string _part;
    readonly Func<bool> _alive;
    public float Radius { get; }
    public bool IsWeakPoint { get; }

    public MudurPart(MudurEncounter e, string part, float radius, Func<bool> alive, bool weak = false) { _e = e; _part = part; Radius = radius; _alive = alive; IsWeakPoint = weak; }

    public string Part => _part;
    public Vector3 Center => GlobalPosition;
    public bool Alive => _alive();
    public CombatBody Body => null;

    public CombatResult TakeHit(CombatHit hit, Vector3 from)
    {
        if (!Alive) return default;
        float done = _e.Hit(_part, hit.Amount);
        if (done > 0) Fx.DamageNumber(Center + Vector3.Up * 0.8f, Mathf.Round(done), IsWeakPoint);
        else Fx.Text3D(Center + Vector3.Up * 0.8f, "—", Palette.OffWhite, 48, 0.7f, 0.5f);
        return new CombatResult { Damage = done, Blocked = done <= 0 };
    }
}

/// <summary>
/// Mudur dovusu (ICERIK §4.4, GDD §19.5; holding4, M4). Durum Sim'de (HoldingState.Mudur, MudurRules); bu dugum asamaya gore
/// sahneyi kurar (avlu: zirhli kamyon; yonetim kati: jeneratorler, hoparlorler, yaya Mudur; cati: zeplin ve balonlar), yapay
/// zekayi host'ta yurutur, parcalari silah hedefi yapar (Director.Atalar.Targets). YER TUTUCU: koddan govdeler (kamyon
/// icin arac akisinin araci henuz yok: bu dosyanin BuildTruck'i gecici; arac gelince onun govdesi takilir).
/// Ortak oyun: yalniz host dovusur ve vurur; istemci durumu gorur (bilinen bosluk, rapor).
/// </summary>
public partial class MudurEncounter : Node3D
{
    readonly HoldingWorld _w;
    readonly List<Node3D> _stageNodes = new();
    readonly List<MudurPart> _parts = new();
    Node3D _truck, _boss, _zeppelin;
    int _builtStage = -1;
    float _t, _fireT, _meleeT, _ramT, _angle;
    Label3D _label;
    bool _won;
    Vector3 _center;

    public MudurEncounter(HoldingWorld w) { _w = w; Name = "Mudur"; }

    Director D => _w.D;
    HoldingSystem H => _w.H;
    HoldingFile Def => _w.Def;
    MudurFight F => H.State.Mudur;
    public int Stage => F?.Stage ?? -1;
    public int CampId => F?.CampId ?? 0;
    public Vector3 TruckPos => _truck != null && IsInstanceValid(_truck) ? _truck.GlobalPosition : _center;
    /// <summary>Sinama: sahnedeki parcalar.</summary>
    public IReadOnlyList<MudurPart> Parts => _parts;
    public Node3D Truck => _truck;
    public Node3D BossNode => _boss;
    /// <summary>Sinama ve rapor sayaclari.</summary>
    public int Shots, Rams, Calls;

    public override void _Ready()
    {
        _label = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 56, OutlineSize = 12, Modulate = Palette.HoldingRed.Lightened(0.5f), PixelSize = 0.01f, NoDepthTest = true };
        AddChild(_label);
    }

    // ---------------------------------------------------------------- sahne

    void ClearStage()
    {
        foreach (var p in _parts) { D.Atalar?.Targets.Remove(p); }
        _parts.Clear();
        foreach (var n in _stageNodes) if (IsInstanceValid(n)) n.QueueFree();
        _stageNodes.Clear();
        _truck = _boss = _zeppelin = null;
    }

    MudurPart AddPart(Node3D parent, string part, Vector3 local, float r, Func<bool> alive, bool weak = false)
    {
        var p = new MudurPart(this, part, r, alive, weak) { Name = part.Replace(':', '_') };
        parent.AddChild(p);
        p.Position = local;
        D.Atalar?.Targets.Add(p);
        _parts.Add(p);
        return p;
    }

    void Place(Node3D n, Vector3 flat) { AddChild(n); n.GlobalPosition = D.World.Ground(flat); _stageNodes.Add(n); }

    void BuildStage(int stage)
    {
        ClearStage();
        _builtStage = stage;
        var camp = H.Camp(F.CampId);
        _center = camp != null ? new Vector3(camp.X, 0, camp.Z) : GlobalPosition;
        float yard = Def.Mudur.YardRadius;
        switch (stage)
        {
            case 0:
                _truck = BuildTruck();
                Place(_truck, _center + new Vector3(yard, 0, 0));
                AddPart(_truck, "kasa", new Vector3(0, 1.5f, 0.6f), 2.3f, () => F is { Stage: 0 });
                AddPart(_truck, "motor", new Vector3(0, 1.4f, -3.3f), 1.0f, () => F is { Stage: 0 }, weak: true);
                if (_w.Host) for (int i = 0; i < Def.Mudur.Truck.Mechanics; i++) H.SpawnUnit(F.CampId, "mekanik");
                break;
            case 1:
                _boss = BuildMudur(1.15f, Palette.HoldingRed.Darkened(0.4f));
                Place(_boss, _center + new Vector3(-6, 0, 4));
                AddPart(_boss, "mudur", new Vector3(0, 1.8f, 0), 1.3f, () => F is { Stage: 1 } && MudurRules.GeneratorsDown(F));
                for (int i = 0; i < Def.Mudur.Boss.Generators; i++)
                {
                    float a = i * Mathf.Tau / Def.Mudur.Boss.Generators + 0.4f;
                    var g = BuildGenerator();
                    Place(g, _center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 13f);
                    int idx = i;
                    AddPart(g, $"jenerator:{i}", new Vector3(0, 1.4f, 0), 1.0f, () => F is { Stage: 1 } && idx < F.Generators.Count && F.Generators[idx] > 0);
                }
                for (int i = 0; i < Def.Mudur.Boss.Speakers; i++)
                {
                    float a = i * Mathf.Pi + 1.2f;
                    var sp = BuildSpeaker();
                    Place(sp, _center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 16f);
                    int idx = i;
                    AddPart(sp, $"hoparlor:{i}", new Vector3(0, 3.2f, 0), 0.9f, () => F is { Stage: 1 } && idx < F.Speakers.Count && F.Speakers[idx] > 0);
                }
                _fireT = 2f;
                D.Hud.Say(Loc.T("holding.mudur_out"));
                break;
            case 2:
                _zeppelin = BuildZeppelin(Def.Mudur.Roof.Balloons);
                Place(_zeppelin, _center + new Vector3(0, 0, 0));
                _zeppelin.GlobalPosition += Vector3.Up * Def.Mudur.Roof.Altitude;
                _boss = _zeppelin.GetNode<Node3D>("Mudur");
                AddPart(_boss, "mudur", new Vector3(0, 1.8f, 0), 1.3f, () => F is { Stage: 2 } && MudurRules.BalloonsLeft(F) == 0);
                for (int i = 0; i < Def.Mudur.Roof.Balloons; i++)
                {
                    int idx = i;
                    var b = _zeppelin.GetNode<Node3D>($"Balon{i}");
                    AddPart(b, $"balon:{i}", Vector3.Zero, 1.2f, () => F is { Stage: 2 } && idx < F.Balloons.Count && F.Balloons[idx] > 0, weak: true);
                }
                _fireT = 2.5f;
                D.Hud.Say(Loc.T("holding.mudur_zeppelin"));
                break;
        }
    }

    // ---------------------------------------------------------------- kare

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var f = F;
        if (f == null) return;
        _t += dt;
        if (_builtStage != f.Stage && f.Stage <= 2) BuildStage(f.Stage);
        if (f.Defeated) { if (!_won) Win(); return; }
        _label.GlobalPosition = (f.Stage == 0 ? TruckPos : _boss?.GlobalPosition ?? _center) + Vector3.Up * 5.5f;
        _label.Text = f.Stage switch
        {
            0 => Loc.T("holding.mudur_truck", Mathf.CeilToInt(f.TruckHp)),
            1 => Loc.T("holding.mudur_stage1", Mathf.CeilToInt(f.BossHp), f.Generators.Count(g => g > 0), f.Generators.Count, f.Speakers.Count(s => s > 0)),
            _ => Loc.T("holding.mudur_stage2", Mathf.CeilToInt(f.BossHp), MudurRules.BalloonsLeft(f), f.Balloons.Count),
        };
        if (!_w.Host) return;
        foreach (var c in H.MudurTick(dt, H.MechanicsAlive(f.CampId))) Dispatch(c);
        // Hedef: en yakin ayaktaki oyuncu (ortak oyunda butun oyuncular; HoldingWorld.NearestTarget)
        var (tpos, tpeer, has) = _w.NearestTarget(f.Stage == 0 ? TruckPos : (_boss?.GlobalPosition ?? _center));
        if (!has) return;
        var t = new Target(tpos, tpeer);
        switch (f.Stage)
        {
            case 0: TruckAi(dt, t); break;
            case 1: BossAi(dt, t, groundBoss: true); break;
            case 2: BossAi(dt, t, groundBoss: MudurRules.BalloonsLeft(f) == 0); break;
        }
    }

    void Dispatch(MudurCommand c)
    {
        switch (c.Cue)
        {
            case MudurCue.CallUstabasi:
                Calls++;
                for (int i = 0; i < c.Count; i++) H.SpawnUnit(F.CampId, "ustabasi");
                D.Hud.Say(Loc.T("holding.mudur_called"));
                GD.Print($"MUDUR call ustabasi x{c.Count}");
                break;
        }
    }

    readonly record struct Target(Vector3 Pos, byte Peer);

    void HurtTarget(Target t, float dmg, Vector3 from, Vector3? knock = null)
    {
        _w.HurtPeer(t.Peer, dmg, from);
        if (knock != null && (D.Net == null || (t.Peer == D.Net.LocalPeer && !D.Net.Dedicated))) D.Player.Velocity += knock.Value; // itme yalniz yerel oyuncuda
    }

    void TruckAi(float dt, Target p)
    {
        if (_truck == null) return;
        float yard = Def.Mudur.YardRadius, tr = Def.Mudur.Truck.Speed / yard;
        _angle += tr * dt;
        var at = _center + new Vector3(Mathf.Cos(_angle), 0, Mathf.Sin(_angle)) * yard;
        var next = D.World.Ground(at);
        var dir = new Vector3(next.X - _truck.GlobalPosition.X, 0, next.Z - _truck.GlobalPosition.Z);
        _truck.GlobalPosition = next;
        if (dir.LengthSquared() > 1e-6f) _truck.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        // Kuleden ates
        _fireT -= dt; _ramT -= dt;
        if (_fireT <= 0)
        {
            _fireT = Def.Mudur.Truck.CannonEvery;
            Shoot(_truck.GlobalPosition + Vector3.Up * 3.2f, p, Def.Mudur.Truck.CannonDamage);
        }
        if (_ramT <= 0 && p.Pos.DistanceTo(_truck.GlobalPosition) < 3.2f)
        {
            _ramT = 2f; Rams++;
            HurtTarget(p, Def.Mudur.Truck.RamDamage, _truck.GlobalPosition, ((p.Pos - _truck.GlobalPosition) with { Y = 0 }).Normalized() * 12f + Vector3.Up * 4f);
        }
    }

    void BossAi(float dt, Target p, bool groundBoss)
    {
        if (_boss == null) return;
        var b = Def.Mudur.Boss;
        if (groundBoss && _zeppelin != null)
        {
            // Balonlar patladi: zeplin iner (Mudur yerde vurulur)
            _zeppelin.GlobalPosition = _zeppelin.GlobalPosition.MoveToward(D.World.Ground(_zeppelin.GlobalPosition), 4f * dt);
        }
        if (groundBoss && _zeppelin == null)
        {
            var d = (p.Pos - _boss.GlobalPosition) with { Y = 0 };
            if (d.Length() > 2.6f) _boss.GlobalPosition = D.World.Ground(_boss.GlobalPosition + d.Normalized() * b.Speed * dt * MudurRules.UnitSpeedMult(Def, F));
            if (d.LengthSquared() > 0.01f) _boss.Rotation = new Vector3(0, Mathf.Atan2(d.X, d.Z), 0);
            _meleeT -= dt;
            if (_meleeT <= 0 && d.Length() < 3.2f) { _meleeT = 1.4f; HurtTarget(p, b.MeleeDamage, _boss.GlobalPosition); }
        }
        _fireT -= dt;
        if (_fireT <= 0)
        {
            _fireT = F.Stage == 2 ? 2.2f : 2.4f;
            Shoot(_boss.GlobalPosition + Vector3.Up * 2.4f, p, b.FireDamage);
        }
    }

    void Shoot(Vector3 from, Target p, float dmg)
    {
        var target = p.Pos + Vector3.Up * 1f;
        var dir = (target - from) with { Y = 0 };
        float dist = dir.Length();
        if (dist < 0.5f) return;
        var pr = new AtaProjectile(D, dmg, 5f, Palette.HoldingRed.Lightened(0.3f), 0.35f) { Name = "MudurMermisi" };
        pr.AddToGroup("ata_fx");
        AddChild(pr);
        pr.Launch(from, dir.Normalized(), 16f, Mathf.Min(dist + 6f, 60f));
        Shots++;
        Sfx.Play("firlatma", from, 0f, 0.7f);
    }

    // ---------------------------------------------------------------- vurus ve zafer

    /// <summary>Parcaya hasar (silah isabeti): Sim kurali; asama gecisi ve zafer komutlariyla.</summary>
    public float Hit(string part, float dmg)
    {
        if (!_w.Host || F == null) return 0;
        var cmds = new List<MudurCommand>();
        float done = H.MudurHit(part, dmg, cmds);
        foreach (var c in cmds)
        {
            if (c.Cue == MudurCue.StageChanged)
            {
                GD.Print($"MUDUR stage {c.Stage}");
                Fx.Burst(_boss?.GlobalPosition ?? TruckPos, Palette.HoldingRed, 60, 8f, 0.25f, 1.2f, true);
                Sfx.Play("kabarmis_patlama", TruckPos, 4f, 0.7f);
                BuildStage(c.Stage);
            }
        }
        if (done > 0 && part.StartsWith("balon")) { var b = _parts.FirstOrDefault(x => x.Part == part); if (b != null) { Fx.Burst(b.GlobalPosition, Palette.Warning, 30, 5f, 0.2f, 0.8f, true); (b.GetParent() as Node3D)!.Visible = false; } }
        return done;
    }

    void Win()
    {
        _won = true;
        var at = _boss?.GlobalPosition ?? TruckPos;
        Fx.Burst(at + Vector3.Up, Palette.HoldingRed, 100, 10f, 0.28f, 1.6f, true);
        Fx.Burst(at + Vector3.Up, Palette.Warning, 60, 7f, 0.2f, 1.4f, true);
        Sfx.Play("kabarmis_patlama", at, 6f, 0.5f);
        Sfx.Play("seviye_atlama", null, 0f);
        if (_w.Host) // ganimet, Depo, Kredi ve cip yalniz host'ta (istemcide Sim durumu host'tan gelir)
        {
            var loot = H.MudurReward(D.State.Backpack, out var left);
            foreach (var (id, n) in left) H.State.Depot[id] = H.State.Depot.GetValueOrDefault(id) + n;
            if (loot != null && loot.Credits > 0) D.State.ApplyLoot(new HoldingLoot("mudur", new Dictionary<string, int>(), loot.Credits, ""), D.State.Backpack);
            if (D.State.DropChip("holding:genel_merkez") != null) D.Hud.Say(Loc.T("holding.hq_chip"));
        }
        GD.Print($"MUDUR defeated t={_t:0.0} unlocks={string.Join(",", H.State.Unlocks)}");
        D.Hud.Big(Loc.T("holding.mudur_defeated"), Palette.HoldingRed, 3f);
        D.Hud.Say(Loc.T("holding.hq_yours"));
        ClearStage();
    }

    // ---------------------------------------------------------------- YER TUTUCU govdeler

    /// <summary>GECICI: Mudur'un zirhli kamyonu (arac akisinin araci gelince onun govdesi takilir). Koyu kirmizi kasa, kule, arka motor kapagi.</summary>
    static Node3D BuildTruck()
    {
        var root = new Node3D { Name = "MudurKamyonu" };
        var k = new MeshKit();
        var armor = Palette.HoldingRed.Darkened(0.5f);
        k.Pillow(new Vector3(3.6f, 1.8f, 7.2f), MeshKit.At(0, 1.5f, 0), armor, 0.2f);
        k.Pillow(new Vector3(2.8f, 1.1f, 2.6f), MeshKit.At(0, 2.9f, 0.8f), armor.Lightened(0.1f), 0.2f); // kule
        k.Cylinder(0.14f, 0.14f, 2.6f, MeshKit.At(new Vector3(0, 2.95f, 3.0f), new Vector3(Mathf.Pi / 2, 0, 0)), Palette.DarkGrey, 8); // top
        k.Pillow(new Vector3(2.2f, 0.9f, 0.6f), MeshKit.At(0, 1.5f, -3.6f), Palette.Warning, 0.15f); // motor kapagi (zayif nokta, sari)
        foreach (var (x, z) in new[] { (-1.9f, 2.4f), (1.9f, 2.4f), (-1.9f, -2.4f), (1.9f, -2.4f) })
            k.Cylinder(0.7f, 0.7f, 0.5f, MeshKit.At(new Vector3(x, 0.7f, z), new Vector3(0, 0, Mathf.Pi * 0.5f)), Palette.NightDeep);
        root.AddChild(k.Instance());
        return root;
    }

    static Node3D BuildMudur(float scale, Color suit)
    {
        var root = new Node3D { Name = "Mudur" };
        var k = new MeshKit();
        k.Pillow(new Vector3(1.4f, 1.8f, 0.8f), MeshKit.At(0, 1.7f, 0), suit, 0.4f);
        k.Sphere(0.45f, MeshKit.At(0, 3.0f, 0), new Color("E8C9A5"), 10, 6);
        k.Cylinder(0.55f, 0.55f, 0.2f, MeshKit.At(0, 3.35f, 0), Palette.Warning, 10); // kask
        foreach (float sx in new[] { -1f, 1f }) k.Cylinder(0.22f, 0.26f, 1.6f, MeshKit.At(sx * 0.4f, 0.6f, 0), suit.Darkened(0.2f), 8);
        k.Pillow(new Vector3(0.5f, 0.5f, 1.2f), MeshKit.At(0.9f, 1.9f, 0.5f), Palette.DarkGrey, 0.2f); // silah koluna bagli
        root.AddChild(k.Instance());
        root.Scale = Vector3.One * scale;
        return root;
    }

    static Node3D BuildGenerator()
    {
        var root = new Node3D { Name = "Jenerator" };
        var k = new MeshKit();
        k.Cylinder(0.9f, 1.0f, 2.2f, MeshKit.At(0, 1.1f, 0), Palette.DarkGrey, 10);
        k.Sphere(0.55f, MeshKit.At(0, 2.5f, 0), Palette.MayaTeal, 10, 6);
        root.AddChild(k.Instance());
        return root;
    }

    static Node3D BuildSpeaker()
    {
        var root = new Node3D { Name = "Hoparlor" };
        var k = new MeshKit();
        k.Cylinder(0.12f, 0.15f, 3.0f, MeshKit.At(0, 1.5f, 0), Palette.Wood, 8);
        k.Pillow(new Vector3(1.0f, 0.8f, 0.7f), MeshKit.At(0, 3.2f, 0), Palette.HoldingRed, 0.2f);
        root.AddChild(k.Instance());
        return root;
    }

    static Node3D BuildZeppelin(int balloons)
    {
        var root = new Node3D { Name = "Zeplin" };
        var k = new MeshKit();
        k.Pillow(new Vector3(10f, 0.6f, 5f), MeshKit.At(0, 0, 0), Palette.DarkGrey, 0.3f); // sepet/guverte
        root.AddChild(k.Instance());
        root.AddChild(BuildMudur(1.0f, Palette.HoldingRed.Darkened(0.4f)));
        root.GetNode<Node3D>("Mudur").Position = new Vector3(0, 0.3f, 0);
        for (int i = 0; i < balloons; i++)
        {
            float a = i * Mathf.Tau / balloons;
            var b = new Node3D { Name = $"Balon{i}", Position = new Vector3(Mathf.Cos(a) * 4f, 6f + (i % 2) * 1.2f, Mathf.Sin(a) * 2f) };
            b.AddChild(new MeshKit().Sphere(1.3f, MeshKit.At(0, 0, 0), i % 2 == 0 ? Palette.HoldingRed : Palette.Warning, 12, 8).Cylinder(0.03f, 0.03f, 5.5f, MeshKit.At(0, -2.9f, 0), Palette.OffWhite, 4).Instance());
            root.AddChild(b);
        }
        return root;
    }
}
