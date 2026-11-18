using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Azman govdesi (ICERIK §4.2). Yer tutucu model: tabandaki yabani turun (azman.json "base": homur, bozuk) koddan
/// govdesi Def.Height boyuna olceklenir ve koddan suslenir (Homur: semer ve sirtta kucuk Homurlar; Bozuk: yosun ve dallar).
/// Kural ve sayilar Sim'de (AzmanFight, AzmanRules); bu dugum yurur, sezdirir, hucum eder (Homur: sur yikar), agac
/// sokup firlatir (Bozuk), oyuncuya degerse hasar verir. Istemcide kukladir: konumu durumdan alir, yalniz yerel oyuncuya
/// degmeyi olcer.
/// </summary>
public partial class AzmanActor : Node3D, IAtaTarget
{
    readonly Director _d;
    readonly AzmanSystem _sys;
    public AzmanDef Def { get; }
    public ulong FightId { get; }
    Node3D _visual, _frame, _decor, _held;
    CreatureRig _rig;
    MeshInstance3D _arrow;
    Label3D _bar;
    readonly List<Node3D> _riderMeshes = new();
    float _t, _contactCd, _arriveT = -1, _flash, _fxT;
    Vector3 _chargeDir;
    string _lastActing = "";
    readonly HashSet<BuildPiece> _chargeHitPieces = new();
    bool _chargeHitPlayer;
    Vector3 _treeAt;
    bool _hasTree;
    public bool Dying { get; private set; }

    public AzmanActor(Director d, AzmanSystem sys, AzmanDef def, ulong fightId) { _d = d; _sys = sys; Def = def; FightId = fightId; }

    AzmanFight Fight => _sys.FightOf(FightId);
    bool Puppet => _d.NetClient;
    public CombatBody Body => Fight?.Body;
    public Vector3 Center => GlobalPosition + Vector3.Up * Def.Height * 0.5f;
    public float Radius => Def.Radius;
    public bool Alive => !Dying && Fight != null && !Fight.Defeated;
    public bool IsWeakPoint => false;
    public string Acting => Fight?.Acting ?? "";
    public bool Telegraphing => Fight?.TelegraphT > 0;
    public string Pending => Fight?.Pending ?? "";

    public override void _Ready()
    {
        _visual = new Node3D { Name = "Visual" };
        AddChild(_visual);
        _frame = new Node3D { Name = "Frame" };
        _visual.AddChild(_frame);
        BuildModel();
        var aim = new Area3D { Name = "Nisan", CollisionLayer = 1, CollisionMask = 0, Monitoring = false };
        aim.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = Def.Radius, Height = Mathf.Max(Def.Height, Def.Radius * 2.1f) }, Position = Vector3.Up * Def.Height * 0.5f, RotationDegrees = new Vector3(90, 0, 0) });
        AddChild(aim);
        var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(Palette.HoldingRed, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _arrow = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(Def.Radius * 1.6f, 1f) }, MaterialOverride = mat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true };
        AddChild(_arrow);
        _bar = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 64, OutlineSize = 12, Modulate = Palette.OffWhite, Position = Vector3.Up * (Def.Height + 0.9f), NoDepthTest = true, PixelSize = 0.01f };
        AddChild(_bar);
        AddToGroup("azman");
    }

    // ---------- Model (yer tutucu) ----------

    void BuildModel()
    {
        var baseDef = _d.Data.Creature(Def.Base);
        _rig = baseDef != null ? CreatureRig.Load(baseDef, _d.Poses, _frame) : null;
        var box = new Aabb(new Vector3(-0.5f, 0, -0.7f), new Vector3(1f, 1f, 1.4f));
        if (_rig?.Model != null)
        {
            var meshes = _rig.Model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
            if (meshes.Count > 0) box = meshes.Select(m => PoseLibrary.RelTo(m, _frame) * m.GetAabb()).Aggregate((a, b) => a.Merge(b));
            _rig.Play("walk", 0f);
        }
        else
        {
            var kit = new MeshKit();
            kit.Pillow(new Vector3(1f, 0.8f, 1.4f), MeshKit.At(0, 0.5f, 0), Def.Base == "bozuk" ? Palette.Shadow : Palette.Wood, 0.5f);
            _frame.AddChild(kit.Instance());
        }
        float s = box.Size.Y > 0.05f ? Def.Height / box.Size.Y : Def.Scale;
        _frame.Scale = Vector3.One * s;
        _decor = new Node3D { Name = "Sus" };
        _frame.AddChild(_decor);
        float top = box.Position.Y + box.Size.Y * 0.8f, len = box.Size.Z, w = box.Size.X;
        var c = box.GetCenter();
        var kitD = new MeshKit();
        if (Def.Base == "homur")
        {
            // Semer ve sirttaki kucuk Homurlar (dusunce gizlenir).
            kitD.Pillow(new Vector3(w * 0.7f, w * 0.12f, len * 0.45f), MeshKit.At(c.X, top, c.Z), Palette.HoldingRed.Darkened(0.2f), 0.4f);
            _decor.AddChild(kitD.Instance());
            int n = Def.Riders?.Count ?? 0;
            for (int i = 0; i < n; i++)
            {
                var r = new MeshKit();
                float z = c.Z + Mathf.Lerp(-len * 0.2f, len * 0.2f, n == 1 ? 0.5f : i / (float)(n - 1));
                float x = c.X + (i % 2 == 0 ? -1 : 1) * w * 0.15f;
                r.Sphere(w * 0.12f, MeshKit.At(x, top + w * 0.14f, z), Palette.Wood.Lerp(Palette.Soil, 0.4f), 8, 6);
                r.Sphere(w * 0.05f, MeshKit.At(x, top + w * 0.22f, z + w * 0.09f), Palette.Shadow, 6, 4);
                var inst = r.Instance();
                _decor.AddChild(inst);
                _riderMeshes.Add(inst);
            }
        }
        else
        {
            // Bozuk: sirtta yosun tutamlari ve kirik dallar.
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f;
                kitD.Sphere(w * 0.13f, MeshKit.At(c.X + Mathf.Sin(a * 9f) * w * 0.25f, top, c.Z + Mathf.Lerp(-len * 0.3f, len * 0.3f, a)), Palette.Plant.Darkened(0.35f), 8, 5);
            }
            kitD.Add(new CylinderMesh { TopRadius = w * 0.02f, BottomRadius = w * 0.05f, Height = w * 0.6f, RadialSegments = 6 }, MeshKit.At(new Vector3(c.X - w * 0.2f, top + w * 0.25f, c.Z), new Vector3(0, 0, 0.5f)), Palette.Wood.Darkened(0.3f));
            kitD.Add(new CylinderMesh { TopRadius = w * 0.02f, BottomRadius = w * 0.05f, Height = w * 0.5f, RadialSegments = 6 }, MeshKit.At(new Vector3(c.X + w * 0.2f, top + w * 0.2f, c.Z - len * 0.15f), new Vector3(0.3f, 0, -0.6f)), Palette.Wood.Darkened(0.3f));
            _decor.AddChild(kitD.Instance());
        }
    }

    // ---------- Hasar ----------

    public CombatResult TakeHit(CombatHit hit, Vector3 from)
    {
        if (!Alive) return default;
        var r = _sys.Hit(this, hit, from);
        _flash = 1f;
        Squash.Kick(_visual, hit.Crit ? 0.1f : 0.05f, 0.25f, 2f);
        if (r.Damage > 0) Fx.DamageNumber(Center + Vector3.Up * Def.Height * 0.45f + new Vector3(GD.Randf() - 0.5f, 0, GD.Randf() - 0.5f), Mathf.Round(r.Damage), hit.Crit);
        else if (r.Blocked) Fx.Text3D(Center + Vector3.Up * Def.Height * 0.5f, "—", Palette.OffWhite, 48, 0.8f, 0.5f);
        return r;
    }

    // ---------- Sim komutlari (host) ----------

    public void Begin(bool arriving)
    {
        if (!arriving) return;
        _arriveT = 0;
        _visual.Position = Vector3.Up * 20f;
    }

    public void Telegraph(string kind)
    {
        Sfx.Play("sisme", Center, 0f, 0.5f);
        Squash.Kick(_visual, 0.08f, Def.Telegraph, 3f);
        if (kind == "charge") _chargeDir = ((NearestTarget(out _) - GlobalPosition) with { Y = 0 }).Normalized();
        if (kind == "uproot") _hasTree = FindTree(out _treeAt);
    }

    public void Attack(string kind)
    {
        _arrow.Visible = false;
        if (kind == "charge")
        {
            _chargeHitPieces.Clear();
            _chargeHitPlayer = false;
            Sfx.Play("kabarik_gumleme", Center, 4f, 0.6f);
        }
        if (kind == "uproot")
        {
            _sys.Uproots++;
            if (_hasTree)
            {
                foreach (var mesh in _d.World.TreeChunksAt(_treeAt)) Squash.KickInstance(mesh, mesh.ToLocal(_treeAt), 0.35f, Def.Uproot.Pull, 4);
                Fx.Burst(_treeAt + Vector3.Up, Palette.Wood, 20, 4f, 0.12f, 0.8f);
            }
            else Fx.Burst(GlobalPosition + Vector3.Up * 0.2f, Palette.Rock, 24, 4f, 0.14f, 0.8f);
            _held = MakeLog(!_hasTree);
            AddChild(_held);
            _held.Position = new Vector3(0, Def.Height * 0.85f, Def.Radius * 0.8f);
            Sfx.Play("parca_darbe", Center, 2f, 0.6f);
        }
    }

    public void DropRiders(int n)
    {
        var rd = Def.Riders;
        if (rd == null) return;
        for (int i = 0; i < n; i++)
        {
            var mesh = _riderMeshes.LastOrDefault(m => m.Visible);
            if (mesh != null) mesh.Visible = false;
            if (Puppet) continue;
            float a = GD.Randf() * Mathf.Tau;
            var to = _d.World.Ground(GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Def.Radius + 2f));
            var rider = new AzmanRider(_d, rd) { Name = "AzmanSirti" };
            GetParent().AddChild(rider);
            rider.GlobalPosition = to;
            rider.Pop(Center + Vector3.Up * Def.Height * 0.4f);
            _d.Savas.Add(rider);
        }
        GD.Print($"AZMAN riders {Def.Id} n={n}");
        Sfx.Play("zipzip_sicrama", Center, 2f, 0.7f);
    }

    public void OnDefeated()
    {
        Dying = true;
        _arrow.Visible = false;
        if (_held != null) { _held.QueueFree(); _held = null; }
        Fx.Burst(Center, Palette.MayaTeal, 70, 8f, 0.24f, 1.4f, true, -1f);
        Fx.Burst(Center, Palette.Soil, 40, 6f, 0.3f, 1.2f);
        Sfx.Play("kabarik_gumleme", Center, 6f, 0.5f);
        var tw = CreateTween();
        tw.TweenProperty(_visual, "scale", _visual.Scale * 1.2f, 0.3f).SetTrans(Tween.TransitionType.Back);
        tw.TweenProperty(_visual, "scale", Vector3.One * 0.01f, 0.6f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    // ---------- Kare ----------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        _contactCd -= dt;
        if (_flash > 0) _flash = Mathf.Max(0, _flash - dt * 5f);
        var f = Fight;
        if (Dying || f == null) return;
        UpdateBar(f);
        StatusFx(f, dt);

        if (_arriveT >= 0)
        {
            _arriveT += dt;
            float k = Mathf.Clamp(_arriveT / 1.2f, 0, 1);
            _visual.Position = Vector3.Up * 20f * (1f - k * k);
            if (k >= 1)
            {
                _arriveT = -1; _visual.Position = Vector3.Zero;
                Fx.Burst(GlobalPosition + Vector3.Up * 0.3f, Palette.Soil, 50, 8f, 0.25f, 1.0f);
                Sfx.Play("kabarik_gumleme", GlobalPosition, 6f, 0.5f);
                _d.Player.Shake(0.5f);
            }
            return;
        }

        if (Puppet) { PuppetTick(f, dt); LocalContact(f); return; }

        float speedMult = Status.SpeedMult(_d.Data.Combat, f.Body);
        bool acted = _lastActing != "" && f.Acting == "";
        if (acted && _lastActing == "uproot") Throw(f);
        if (acted && _lastActing == "charge") Squash.Kick(_visual, 0.15f, 0.4f, 2f);
        _lastActing = f.Acting;

        if (f.TelegraphT > 0) TelegraphTick(f, dt);
        else if (f.Acting == "charge") ChargeTick(f, dt * speedMult);
        else if (f.Acting == "uproot") { Face(((NearestTarget(out _) - GlobalPosition) with { Y = 0 }).Normalized(), dt * 3f); _rig?.Play("attack2", 0.2f); }
        else Walk(dt * speedMult);

        f.X = GlobalPosition.X; f.Z = GlobalPosition.Z; f.Yaw = Rotation.Y;
        LocalContact(f);
    }

    void TelegraphTick(AzmanFight f, float dt)
    {
        _rig?.Play("attack2", 0.2f);
        if (f.Pending == "charge")
        {
            var want = ((NearestTarget(out _) - GlobalPosition) with { Y = 0 }).Normalized();
            _chargeDir = _chargeDir.Slerp(want, Mathf.Min(1f, dt * 1.5f)).Normalized();
            Face(_chargeDir, dt * 8f);
            float len = Def.Charge.Speed * Def.Charge.Seconds;
            _arrow.Visible = true;
            _arrow.GlobalPosition = _d.World.Ground(GlobalPosition + _chargeDir * len * 0.5f) + Vector3.Up * 0.08f;
            _arrow.GlobalBasis = Basis.LookingAt(_chargeDir, Vector3.Up);
            _arrow.Scale = new Vector3(1, 1, len);
            if ((int)(_t * 6) != (int)((_t - dt) * 6)) Fx.Burst(GlobalPosition + Vector3.Up * 0.2f, Palette.Soil, 5, 3f, 0.12f, 0.4f);
        }
        else if (f.Pending == "uproot")
        {
            // Agaca don ve yuru (sokme yerinde olur); agac yoksa yeri kazar.
            var to = _hasTree ? _treeAt : GlobalPosition;
            var d = (to - GlobalPosition) with { Y = 0 };
            if (d.Length() > Def.Radius + 1.2f) { Step(d.Normalized() * Mathf.Min(d.Length(), Def.MoveSpeed * 1.6f * dt)); Face(d.Normalized(), dt * 4f); }
            if ((int)(_t * 4) != (int)((_t - dt) * 4)) Fx.Burst(GlobalPosition + Vector3.Up * 0.2f, Palette.Soil, 4, 3f, 0.12f, 0.4f);
        }
    }

    void ChargeTick(AzmanFight f, float dt)
    {
        var c = Def.Charge;
        _rig?.Play("run", 0.1f, 2f);
        Face(_chargeDir, dt * 10f);
        var next = GlobalPosition + _chargeDir * c.Speed * dt;
        if ((int)(_t * 10) != (int)((_t - dt) * 10)) Fx.Burst(GlobalPosition + Vector3.Up * 0.2f, Palette.Soil, 6, 4f, 0.15f, 0.5f);
        // Sur yikan hucum: yoldaki yapi parcalari hasar alir; ilk degen parcada Azman durur ve sersemler.
        var hitPieces = PiecesNear(next, c.StructureRadius + Def.Radius * 0.5f).Where(p => !_chargeHitPieces.Contains(p)).ToList();
        foreach (var p in hitPieces)
        {
            _chargeHitPieces.Add(p);
            _d.Build.Damage(p, c.StructureDamage);
            Fx.Burst(p.GlobalPosition + Vector3.Up, Palette.Wood, 24, 6f, 0.16f, 0.8f);
            Sfx.Play("parca_darbe", p.GlobalPosition, 4f, 0.6f);
            GD.Print($"AZMAN wall_hit {Def.Id} piece={p.Def.Id} hp={p.Hp:0}");
        }
        if (hitPieces.Count > 0)
        {
            _d.Player.Shake(0.4f);
            Squash.Kick(_visual, 0.3f, 0.5f, 3f);
            _sys.WallStun(this);
            return;
        }
        GlobalPosition = _d.World.Ground(next);
    }

    /// <summary>Yerel oyuncuya degme: govde degmesi ve hucum (her makinede yerel oyuncu icin; kukla da).</summary>
    void LocalContact(AzmanFight f)
    {
        var p = _d.Player;
        if (p == null || p.Down || f.Body.ShieldT > 0) return;
        float dist = p.GlobalPosition.DistanceTo(GlobalPosition);
        if (f.Acting == "charge" && Def.Charge != null)
        {
            if (!_chargeHitPlayer && dist < Def.Radius + 1f)
            {
                _chargeHitPlayer = true;
                p.Hurt(Def.Charge.Damage, GlobalPosition);
                var away = ((p.GlobalPosition - GlobalPosition) with { Y = 0 }).Normalized();
                p.Velocity += (away + _chargeDir).Normalized() * Def.Charge.Knockback + Vector3.Up * 4f;
            }
            return;
        }
        if (f.Acting == "") _chargeHitPlayer = false;
        if (_contactCd > 0 || dist > Def.Radius + 0.8f || !Status.CanAct(f.Body)) return;
        _contactCd = Def.ContactEvery;
        p.Hurt(Def.ContactDamage, GlobalPosition);
        Squash.Kick(_visual, 0.1f, 0.3f, 2f);
    }

    void Walk(float dt)
    {
        var target = NearestTarget(out bool any);
        var d = (target - GlobalPosition) with { Y = 0 };
        float stop = Def.Radius + 0.5f;
        if (any && d.Length() > stop)
        {
            Step(d.Normalized() * Mathf.Min(d.Length() - stop, Def.MoveSpeed * dt));
            Face(d.Normalized(), dt * 3f);
            _rig?.Play("walk", 0.2f, Mathf.Clamp(Def.MoveSpeed / 2.5f, 0.6f, 2f));
        }
        else _rig?.Play("idle", 0.25f);
    }

    void Step(Vector3 by) => GlobalPosition = _d.World.Ground(GlobalPosition + by);

    void PuppetTick(AzmanFight f, float dt)
    {
        var to = _d.World.Ground(new Vector3(f.X, 0, f.Z));
        bool moving = to.DistanceTo(GlobalPosition) > 0.05f;
        GlobalPosition = GlobalPosition.DistanceTo(to) > 12f ? to : GlobalPosition.Lerp(to, Mathf.Min(1f, dt * 10f));
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, f.Yaw, Mathf.Min(1f, dt * 10f)), 0);
        if (f.Acting == "charge") { _chargeDir = new Vector3(Mathf.Sin(f.Yaw), 0, Mathf.Cos(f.Yaw)); _rig?.Play("run", 0.1f, 2f); }
        else _rig?.Play(f.TelegraphT > 0 || f.Acting != "" ? "attack2" : moving ? "walk" : "idle", 0.2f);
        if (_riderMeshes.Count(m => m.Visible) > f.Riders) DropRiders(_riderMeshes.Count(m => m.Visible) - f.Riders);
    }

    /// <summary>En yakin canli oyuncu (ortak oyunda butun oyuncular; host yetkili).</summary>
    Vector3 NearestTarget(out bool any)
    {
        any = false;
        var best = GlobalPosition; float bd = float.MaxValue;
        if (_d.Net != null)
        {
            foreach (var (_, pos, down) in _d.Net.Players())
            {
                if (down) continue;
                float dd = pos.DistanceTo(GlobalPosition);
                if (dd < bd) { bd = dd; best = pos; any = true; }
            }
            if (any) return best;
        }
        var p = _d.Player;
        if (p != null && !p.Down) { any = true; return p.GlobalPosition; }
        return best;
    }

    IEnumerable<BuildPiece> PiecesNear(Vector3 at, float r)
    {
        var sys = _d.Build?.System;
        if (sys == null) return System.Array.Empty<BuildPiece>();
        return sys.Pieces.Where(p => IsInstanceValid(p) && new Vector2(p.GlobalPosition.X - at.X, p.GlobalPosition.Z - at.Z).Length() < r).ToList();
    }

    bool FindTree(out Vector3 at)
    {
        at = default;
        var u = Def.Uproot;
        if (u == null || _d.World?.TreePositions == null) return false;
        float bd = u.TreeReach;
        bool found = false;
        foreach (var t in _d.World.TreePositions)
        {
            if (_sys.UsedTrees.Contains(t)) continue;
            float dd = new Vector2(t.X - GlobalPosition.X, t.Z - GlobalPosition.Z).Length();
            if (dd < bd) { bd = dd; at = t; found = true; }
        }
        if (found) _sys.UsedTrees.Add(at);
        return found;
    }

    /// <summary>Sokme bitti: elindeki kutugu en yakin oyuncuya firlatir.</summary>
    void Throw(AzmanFight f)
    {
        if (_held == null) return;
        var from = _held.GlobalPosition;
        _held.QueueFree(); _held = null;
        var target = NearestTarget(out _);
        var log = new AzmanLog(_d, _sys, Def.Uproot) { Name = "AzmanKutuk" };
        GetParent().AddChild(log);
        log.Launch(from, _d.World.Ground(target), Def.Uproot.ThrowSpeed);
        Sfx.Play("firlatma", from, 4f, 0.5f);
        Squash.Kick(_visual, 0.15f, 0.35f, 2f);
    }

    Node3D MakeLog(bool rock)
    {
        var kit = new MeshKit();
        if (rock) kit.Sphere(0.7f, MeshKit.At(Vector3.Zero, Vector3.Zero, new Vector3(1.2f, 0.8f, 1f)), Palette.Rock, 8, 6);
        else
        {
            kit.Add(new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.38f, Height = 2.6f, RadialSegments = 8 }, MeshKit.At(Vector3.Zero, new Vector3(0, 0, Mathf.Pi / 2)), Palette.Wood.Darkened(0.2f));
            kit.Sphere(0.6f, MeshKit.At(1.4f, 0.2f, 0), Palette.Plant.Darkened(0.3f), 8, 5);
        }
        return kit.Instance();
    }

    void Face(Vector3 dir, float k)
    {
        if (dir.LengthSquared() < 0.001f) return;
        float yaw = Mathf.Atan2(dir.X, dir.Z);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(1f, k)), 0);
    }

    void UpdateBar(AzmanFight f)
    {
        int full = Mathf.RoundToInt(f.Body.Fraction * 10);
        string st = f.Body.Statuses.Count > 0 ? "  " + string.Join(" ", f.Body.Statuses.Select(s => s.Kind switch
        {
            StatusKind.Burn => "Yanık", StatusKind.Freeze => "Donma", StatusKind.Lock => "Kilit", StatusKind.Stun => "Sersem", _ => "Zehir",
        })) : "";
        _bar.Text = $"{Def.Name}\n{new string('█', full)}{new string('░', 10 - full)} {Mathf.CeilToInt(f.Body.Hp)}{st}";
        _bar.Modulate = CombatSystem.Tint(f.Body) * (f.Body.ShieldT > 0 ? new Color(0.7f, 0.7f, 0.7f) : Colors.White);
    }

    void StatusFx(AzmanFight f, float dt)
    {
        _fxT -= dt;
        if (_fxT > 0 || f.Body.Statuses.Count == 0) return;
        _fxT = 0.3f;
        if (Status.Has(f.Body, StatusKind.Burn)) Fx.Burst(Center + Vector3.Up * Def.Height * 0.3f, Palette.Accent, 5, 2f, 0.1f, 0.5f, true, -3f);
        if (Status.Has(f.Body, StatusKind.Freeze) || Status.Has(f.Body, StatusKind.Lock)) Fx.Burst(Center, new Color(0.7f, 0.85f, 1f), 4, 1.5f, 0.08f, 0.5f, true);
        if (Status.Has(f.Body, StatusKind.Stun)) Fx.Burst(Center + Vector3.Up * Def.Height * 0.55f, Palette.Warning, 3, 1.5f, 0.1f, 0.5f, true, -1f);
    }
}
