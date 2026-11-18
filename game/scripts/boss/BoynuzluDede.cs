using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Boynuzlu Dede (ICERIK §4.1, GDD §18.1). Yer tutucu: koddan parca model (govde, bacaklar, boyun, bas, uzun sakal,
/// dalli boynuz; boynuzda fidanlar ve iki isiyan kovan). Asama 1 dallardan koruyucu duser ve sis coker; asama 2 kok
/// dalgasi, kovanlar zayif nokta; asama 3 boynuz kirilir, parcalari Cekim'le alinip ona firlatilir.
/// </summary>
public partial class BoynuzluDede : AtaActor
{
    Node3D _antlers, _head;
    readonly List<Vector3> _branchTips = new();
    readonly List<AtaHive> _hives = new();
    readonly List<MeshInstance3D> _fog = new();
    StandardMaterial3D _fogMat;
    float _walkT;
    Node3D[] _legs;

    protected override void BuildModel()
    {
        var hide = Palette.Shadow.Lerp(Palette.Wood, 0.45f);
        var dark = hide.Darkened(0.35f);
        var beard = Palette.OffWhite.Lerp(Palette.Fog, 0.4f);
        var moss = Palette.Plant.Lerp(Palette.Shadow, 0.35f);

        var kit = new MeshKit();
        kit.Pillow(new Vector3(1.7f, 1.5f, 3.0f), MeshKit.At(0, 2.1f, 0), hide, 0.55f, 16, 8);
        kit.Pillow(new Vector3(1.2f, 0.35f, 1.8f), MeshKit.At(0, 2.85f, -0.1f), moss, 0.6f, 12, 4); // sirtta yosun
        kit.Sphere(0.25f, MeshKit.At(0.45f, 2.95f, 0.5f), Palette.Accent.Lerp(Palette.Wood, 0.4f), 8, 5); // mantar
        kit.Sphere(0.18f, MeshKit.At(-0.4f, 2.9f, -0.6f), Palette.Accent.Lerp(Palette.Wood, 0.4f), 8, 5);
        kit.Sphere(0.35f, MeshKit.At(0, 1.9f, -1.5f), dark, 8, 5); // kuyruk
        Visual.AddChild(kit.Instance());

        // Bacaklar ayri dugum: yururken sallanir.
        _legs = new Node3D[4];
        int li = 0;
        foreach (var (x, z) in new[] { (-0.55f, 1.0f), (0.55f, 1.0f), (-0.55f, -1.0f), (0.55f, -1.0f) })
        {
            var hip = new Node3D { Position = new Vector3(x, 1.7f, z) };
            var lk = new MeshKit();
            lk.Cylinder(0.14f, 0.2f, 1.7f, MeshKit.At(0, -0.85f, 0), dark, 8);
            lk.Cylinder(0.2f, 0.16f, 0.2f, MeshKit.At(0, -1.65f, 0.04f), Palette.NightDeep.Lerp(dark, 0.5f), 8);
            hip.AddChild(lk.Instance());
            Visual.AddChild(hip);
            _legs[li++] = hip;
        }

        // Boyun, bas, sakal
        _head = new Node3D { Name = "Head", Position = new Vector3(0, 3.0f, 1.75f) };
        Visual.AddChild(_head);
        var hk = new MeshKit();
        hk.Cylinder(0.38f, 0.5f, 1.0f, MeshKit.At(new Vector3(0, -0.25f, -0.35f), new Vector3(-0.8f, 0, 0)), hide, 10);
        hk.Pillow(new Vector3(0.65f, 0.6f, 1.0f), MeshKit.At(0, 0.15f, 0.2f), hide, 0.5f, 12, 6);
        hk.Pillow(new Vector3(0.4f, 0.32f, 0.35f), MeshKit.At(0, 0.02f, 0.72f), dark, 0.5f, 10, 4); // burun
        hk.Cylinder(0.3f, 0.04f, 1.3f, MeshKit.At(new Vector3(0, -0.65f, 0.35f), new Vector3(0.15f, 0, 0)), beard, 10); // sakal
        hk.Pillow(new Vector3(0.14f, 0.35f, 0.08f), MeshKit.At(new Vector3(-0.42f, 0.35f, 0.05f), new Vector3(0, 0, 0.9f)), hide, 0.5f, 8, 4); // kulak
        hk.Pillow(new Vector3(0.14f, 0.35f, 0.08f), MeshKit.At(new Vector3(0.42f, 0.35f, 0.05f), new Vector3(0, 0, -0.9f)), hide, 0.5f, 8, 4);
        _head.AddChild(hk.Instance());
        var eyeMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Palette.MayaTeal, EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 3f };
        foreach (float sx in new[] { -1f, 1f })
            _head.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f }, MaterialOverride = eyeMat, Position = new Vector3(sx * 0.25f, 0.28f, 0.55f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        // Dalli boynuz ve ustundeki koru
        _antlers = new Node3D { Name = "Antlers", Position = new Vector3(0, 0.45f, 0.05f) };
        _head.AddChild(_antlers);
        var ak = new MeshKit();
        var bone = Palette.OffWhite.Lerp(Palette.Wood, 0.45f);
        foreach (float s in new[] { -1f, 1f })
        {
            // Ana kol: disa ve yukari; her kolda uc catal, her catalin ucunda fidan.
            var a0 = Vector3.Zero + new Vector3(s * 0.25f, 0, 0);
            var a1 = a0 + new Vector3(s * 0.75f, 0.6f, -0.1f);
            var a2 = a1 + new Vector3(s * 0.55f, 0.75f, -0.15f);
            Branch(ak, a0, a1, 0.11f, bone);
            Branch(ak, a1, a2, 0.08f, bone);
            foreach (var (from, off) in new[] { (a1, new Vector3(s * 0.05f, 0.75f, 0.25f)), (a2, new Vector3(s * 0.45f, 0.5f, 0)), (a2, new Vector3(-s * 0.1f, 0.65f, -0.2f)) })
            {
                var tip = from + off;
                Branch(ak, from, tip, 0.05f, bone);
                ak.Cylinder(0f, 0.32f, 0.7f, MeshKit.At(tip + Vector3.Up * 0.35f), Palette.Plant.Lerp(Palette.Shadow, 0.2f), 8); // fidan
                ak.Cylinder(0f, 0.24f, 0.5f, MeshKit.At(tip + Vector3.Up * 0.75f), Palette.Plant, 8);
                _branchTips.Add(tip);
            }
            // Kovan: alt catalda (Tirpan'in erisebilecegi yukseklik), asama 2'de isir.
            var hive = new AtaHive(this, _hives.Count) { Name = "Kovan", Position = a1 + new Vector3(s * 0.05f, -0.15f, 0.2f) };
            _antlers.AddChild(hive);
            _hives.Add(hive);
        }
        _antlers.AddChild(ak.Instance());
        Visual.Scale = Vector3.One * Def.Scale;

        // Sis katmanlari (asama 1): arenayi saran yari saydam perdeler.
        var soft = new GradientTexture2D { Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64, Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0) }, Offsets = new[] { 0f, 1f } } };
        _fogMat = new StandardMaterial3D { AlbedoTexture = soft, DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(Palette.Fog.Lerp(Palette.SkyNoon, 0.4f), 0f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var center = Sys.ArenaCenter(Def);
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.Tau;
            float r = Def.Arena.Radius * (0.55f + 0.4f * (i % 2));
            var m = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(16f, 7f) }, MaterialOverride = _fogMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            m.AddToGroup("ata_fx");
            Sys.AddChild(m);
            m.GlobalPosition = D.World.Ground(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r) + Vector3.Up * 2f;
            _fog.Add(m);
        }
    }

    static void Branch(MeshKit k, Vector3 a, Vector3 b, float r, Color c)
    {
        var d = b - a;
        var basis = Basis.LookingAt(d.Normalized(), Mathf.Abs(d.Normalized().Dot(Vector3.Up)) > 0.95f ? Vector3.Forward : Vector3.Up) * Basis.FromEuler(new Vector3(-Mathf.Pi / 2, 0, 0));
        k.Add(new CylinderMesh { TopRadius = r * 0.75f, BottomRadius = r, Height = d.Length(), RadialSegments = 7, Rings = 1 }, new Transform3D(basis, (a + b) * 0.5f), c);
    }

    public IEnumerable<AtaHive> Hives => _hives;

    protected override Color VolleyColor => Palette.Wood.Darkened(0.2f);
    protected override float VolleySize => 0.35f;

    public override void OnPhase(int phase, bool quiet = false)
    {
        base.OnPhase(phase, quiet);
        float fog = Phase.Fog;
        var tw = CreateTween();
        tw.TweenProperty(_fogMat, "albedo_color:a", Mathf.Clamp((fog - 1f) * 0.22f, 0f, 0.6f), 2.0f);
        foreach (var h in _hives) h.SetActive(phase == 1 && Fight != null && h.Index < Fight.WeakPoints.Count && Fight.WeakPoints[h.Index] > 0);
        if (phase >= 2 && _antlers.Visible)
        {
            _antlers.Visible = false;
            if (!quiet)
            {
                Sfx.Play("parca_darbe", Center, 8f, 0.4f);
                Fx.Burst(_antlers.GlobalPosition, Palette.OffWhite.Lerp(Palette.Wood, 0.45f), 60, 8f, 0.25f, 1.2f);
                D.Hud.Say("Boynuz kırıldı: parçaları Çekim'le al, ona fırlat!");
            }
            for (int i = 0; i < Phase.Shards; i++)
            {
                float a = i / (float)Mathf.Max(1, Phase.Shards) * Mathf.Tau + 0.4f;
                var shard = new AntlerShard(D, Sys, this) { Name = "BoynuzParcasi" };
                shard.AddToGroup("ata_fx");
                Sys.AddChild(shard);
                var at = D.World.Ground(GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Def.Radius + 3f));
                shard.Drop(_antlers.GlobalPosition, at);
            }
        }
        if (!quiet && phase == 0) D.Hud.Say("Sis çöküyor; dallardan koruyucular düşüyor.");
        if (!quiet && phase == 1) D.Hud.Say("Kök dalgası: zıpla! Işıyan kovanları vur.");
    }

    protected override void SpecialAttack(string kind, int count)
    {
        if (kind != "rootwave") return;
        var w = new RootWave(D, Phase.WaveSpeed, Phase.WaveRadius, Phase.WaveWidth, Phase.AttackDamage, Phase.Knockback) { Name = "KokDalgasi" };
        w.AddToGroup("ata_fx");
        w.AddToGroup("ata_rootwave");
        Sys.AddChild(w);
        w.GlobalPosition = D.World.Ground(GlobalPosition) + Vector3.Up * 0.15f;
        Sfx.Play("kaya_kemirme", GlobalPosition, 6f, 0.4f);
        Squash.Kick(Visual, 0.2f, 0.4f, 2f);
        D.Player.Shake(0.4f);
    }

    public override Vector3 SpawnPoint(int i)
    {
        if (_branchTips.Count == 0 || !_antlers.Visible) return base.SpawnPoint(i);
        return _antlers.ToGlobal(_branchTips[(i * 3 + (int)(GD.Randi() % 2)) % _branchTips.Count]);
    }

    public override void OnWeakPointBroken(int i)
    {
        if (i < 0 || i >= _hives.Count) return;
        _hives[i].Break();
    }

    public override void OnStunned()
    {
        base.OnStunned();
        var tw = CreateTween();
        tw.TweenProperty(_head, "rotation:x", 0.5f, 0.3f);
        tw.TweenInterval(Mathf.Max(0.2f, (Fight?.StunT ?? 1f) - 0.6f));
        tw.TweenProperty(_head, "rotation:x", 0f, 0.3f);
    }

    public override void OnDefeated()
    {
        base.OnDefeated();
        var tw = _fogMat != null ? CreateTween() : null;
        tw?.TweenProperty(_fogMat, "albedo_color:a", 0f, 1.5f);
        foreach (var h in _hives) h.SetActive(false);
    }

    protected override void WalkAnim(float dt, float speed)
    {
        _walkT += dt * speed * 1.4f;
        for (int i = 0; i < 4; i++) _legs[i].Rotation = new Vector3(Mathf.Sin(_walkT + (i is 0 or 3 ? 0 : Mathf.Pi)) * 0.35f, 0, 0);
        Visual.Position = Vector3.Up * Mathf.Abs(Mathf.Sin(_walkT)) * 0.06f;
        _head.Rotation = new Vector3(Mathf.Sin(_walkT * 0.5f) * 0.05f, 0, 0);
    }

    protected override void IdleAnim(float dt, bool tense)
    {
        _walkT += dt;
        foreach (var l in _legs) l.Rotation = l.Rotation.Lerp(Vector3.Zero, Mathf.Min(1, dt * 5));
        if (!Stunned) _head.Rotation = new Vector3(tense ? -0.25f : Mathf.Sin(_walkT * 0.7f) * 0.06f, Mathf.Sin(_walkT * 0.4f) * 0.15f, 0);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        foreach (var f in _fog) if (IsInstanceValid(f)) f.QueueFree();
    }
}

/// <summary>Boynuzdaki isiyan kovan: Dede asama 2'nin zayif noktasi (ICERIK §4.1). Kirilinca Dede sersemler.</summary>
public partial class AtaHive : Node3D, IAtaTarget
{
    readonly BoynuzluDede _dede;
    public int Index { get; }
    MeshInstance3D _mesh;
    StandardMaterial3D _mat;
    bool _active, _broken;
    float _t;

    public AtaHive(BoynuzluDede dede, int index) { _dede = dede; Index = index; }

    public Vector3 Center => GlobalPosition;
    public float Radius => 0.45f;
    /// <summary>Vurulabilir: asama 2, kirilmamis ve pencere acik (kok dalgasindan sonra isir, GDD §18.1).</summary>
    public bool Alive => _active && !_broken && IsVisibleInTree() && Open;
    bool Open { get { var s = FindSys(); return s?.Fight != null && AtaRules.WeakOpen(s.Fight, _dede.Def); } }
    public bool IsWeakPoint => true;

    public override void _Ready()
    {
        _mat = new StandardMaterial3D { AlbedoColor = Palette.Warning.Darkened(0.3f), EmissionEnabled = true, Emission = Palette.Warning, EmissionEnergyMultiplier = 0.2f };
        _mesh = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.3f, Height = 0.75f, RadialSegments = 10, Rings = 4 }, MaterialOverride = _mat };
        AddChild(_mesh);
        var bands = new MeshKit();
        for (int i = 0; i < 3; i++) bands.Add(new TorusMesh { InnerRadius = 0.27f, OuterRadius = 0.33f, Rings = 14, RingSegments = 4 }, MeshKit.At(0, -0.2f + i * 0.2f, 0), Palette.Wood.Darkened(0.3f));
        AddChild(bands.Instance());
    }

    public void SetActive(bool on)
    {
        _active = on && !_broken;
        var sys = GetParentOrNull<Node>() == null ? null : FindSys();
        if (sys == null) return;
        if (_active) { if (!sys.Targets.Contains(this)) sys.Targets.Add(this); }
        else sys.Remove(this);
    }

    AtaSystem FindSys()
    {
        Node n = _dede;
        while (n != null && n is not AtaSystem) n = n.GetParent();
        return n as AtaSystem;
    }

    public void TakeWeapon(float dmg, bool crit, Vector3 dir, float knock)
    {
        if (!Alive) return;
        var sys = FindSys();
        sys?.DamageWeakPoint(Index, dmg);
        Fx.DamageNumber(Center + Vector3.Up * 0.5f, dmg, true);
        Fx.Burst(Center, Palette.Warning, 8, 3f, 0.1f, 0.4f, true);
        Squash.Kick(this, 0.25f, 0.25f, 2f);
    }

    public void Break()
    {
        if (_broken) return;
        _broken = true;
        Fx.Burst(Center, Palette.Warning, 40, 7f, 0.16f, 1.0f, true, -2f);
        Sfx.Play("isabet_kritik", Center, 6f, 0.7f);
        Sfx.Play("parca_sacilma", Center, 2f);
        _mesh.Visible = false;
        SetActive(false);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        bool lit = _active && !_broken && Open;
        if (_mat != null) _mat.EmissionEnergyMultiplier = lit ? 3f + Mathf.Sin(_t * 8f) * 1.5f : _active ? 0.5f : 0.2f;
    }
}

/// <summary>
/// Kirik boynuz parcasi (Dede asama 3): yere duser, Cekim'le tutulur ("cekim" grubu) ve firlatilir. Firlatilmisken
/// Dede'ye degerse buyuk hasar (atalar.json shardDamage) ve parca dagilir.
/// </summary>
public partial class AntlerShard : Node3D, ICarryable
{
    readonly Director _d;
    readonly AtaSystem _sys;
    readonly BoynuzluDede _dede;
    bool _gone;
    Vector3 _dropFrom, _dropTo;
    float _dropT = -1;

    public AntlerShard(Director d, AtaSystem sys, BoynuzluDede dede) { _d = d; _sys = sys; _dede = dede; }

    public Node3D Body => this;
    public float Kg => 30f;
    public float Radius => 0.55f;
    public bool Held { get; set; }
    public bool Gone => _gone || IsQueuedForDeletion();

    public override void _Ready()
    {
        AddToGroup("cekim");
        var kit = new MeshKit();
        var bone = Palette.OffWhite.Lerp(Palette.Wood, 0.45f);
        kit.Add(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.12f, Height = 1.2f, RadialSegments = 7 }, MeshKit.At(new Vector3(0, 0, 0), new Vector3(0, 0, 1.3f)), bone);
        kit.Add(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.07f, Height = 0.6f, RadialSegments = 6 }, MeshKit.At(new Vector3(0.2f, 0.25f, 0), new Vector3(0, 0, 0.3f)), bone);
        kit.Cylinder(0f, 0.25f, 0.5f, MeshKit.At(0.32f, 0.6f, 0), Palette.Plant, 7);
        AddChild(kit.Instance());
        var glow = new OmniLight3D { LightColor = Palette.MayaTeal, LightEnergy = 0.6f, OmniRange = 2.5f };
        AddChild(glow);
    }

    public void Drop(Vector3 from, Vector3 to) { _dropFrom = from; _dropTo = to + Vector3.Up * 0.3f; _dropT = 0; GlobalPosition = from; }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_gone) return;
        if (_dropT >= 0)
        {
            _dropT += dt;
            float k = Mathf.Clamp(_dropT / 0.9f, 0, 1);
            GlobalPosition = _dropFrom.Lerp(_dropTo, k) + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * 2.5f;
            Rotation = new Vector3(k * 6f, k * 2f, 0);
            if (k >= 1) { _dropT = -1; Fx.Burst(GlobalPosition, Palette.Soil, 10, 3f, 0.12f, 0.5f); Sfx.Play("kabarik_gumleme", GlobalPosition, 0f, 1.2f); }
            return;
        }
        // Firlatma: ThrowMotion cocugu varken ucuyor.
        bool flying = !Held && GetNodeOrNull(ThrowMotion.NodeName) != null;
        if (flying && _dede != null && IsInstanceValid(_dede) && _dede.Alive)
        {
            var c = _dede.Center;
            if (GlobalPosition.DistanceTo(c) < _dede.Radius + Radius + 0.4f || new Vector2(GlobalPosition.X - c.X, GlobalPosition.Z - c.Z).Length() < _dede.Radius && GlobalPosition.Y < c.Y + _dede.Def.Height * 0.5f)
            {
                _gone = true;
                var ph = _sys.Fight != null ? AtaRules.PhaseOf(_sys.Fight, _dede.Def) : null;
                float done = _sys.Damage(ph?.ShardDamage ?? 0, AtaHitKind.Thrown);
                _sys.ShardHits++;
                GD.Print($"ATA shard_hit dmg={done:0}");
                Fx.DamageNumber(c + Vector3.Up * 1.5f, done, true);
                Fx.Burst(GlobalPosition, Palette.OffWhite.Lerp(Palette.Wood, 0.45f), 40, 7f, 0.2f, 0.9f);
                Sfx.Play("isabet_kritik", GlobalPosition, 6f, 0.6f);
                Sfx.Play("parca_darbe", GlobalPosition, 4f, 0.8f);
                Squash.Kick(_dede.Visual, 0.25f, 0.4f, 3f);
                _d.HitStop(0.08f);
                QueueFree();
                return;
            }
        }
    }
}
