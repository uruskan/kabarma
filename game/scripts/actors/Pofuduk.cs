using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Pofuduk: bacakli yun yumagi (ICERIK §3.1). M0 denemesi #4: Tripo dort ayak iskeleti + hazir yurume klibi,
/// uretim hattindan gecmis model. Gunduz otlar, Kabarma'da sisip "Yastik" olur (simdilik yalnizca gorunus).
/// </summary>
public partial class Pofuduk : Node3D
{
    const string ModelPath = "res://assets/models/pofuduk/pofuduk.glb";
    Node3D _visual;
    AnimationPlayer _anim;
    string _walk;
    Director _d;
    Vector3 _home, _to;
    float _t, _wait, _scale = 1f, _targetScale = 1f;
    readonly float _speed = 1.1f;
    public bool Loaded { get; private set; }

    public Pofuduk(Director d, Vector3 home) { _d = d; _home = home; }

    public override void _Ready()
    {
        _visual = Assets.Load(ModelPath);
        if (_visual == null) { GD.PrintErr("POFUDUK model yok"); return; }
        AddChild(_visual);
        Loaded = true;
        _anim = _visual.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (_anim != null)
        {
            var names = _anim.GetAnimationList();
            _walk = names.FirstOrDefault(n => n.ToLower().Contains("walk")) ?? names.FirstOrDefault();
            if (_walk != null) { _anim.GetAnimation(_walk).LoopMode = Animation.LoopModeEnum.Linear; _anim.Play(_walk); }
            GD.Print($"POFUDUK anims: {string.Join(",", names)}");
        }
        _to = _home;
        GlobalPosition = _d.World.Ground(_home);
    }

    public void Swell(bool on) => _targetScale = on ? 2.4f : 1f;

    // Sinama (--animcheck): bacak kemiklerinin yurume sirasinda ne kadar dondugunu olcer.
    Skeleton3D _skel;
    readonly System.Collections.Generic.Dictionary<int, Quaternion> _first = new();
    float _maxDeg;
    void AnimCheck()
    {
        _skel ??= _visual.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (_skel == null) return;
        for (int b = 0; b < _skel.GetBoneCount(); b++)
        {
            var q = _skel.GetBonePoseRotation(b);
            if (!_first.ContainsKey(b)) { _first[b] = q; continue; }
            float deg = Mathf.RadToDeg(_first[b].AngleTo(q));
            if (deg > _maxDeg) _maxDeg = deg;
        }
        if (_t > 3f && _t - (float)GetProcessDeltaTime() <= 3f)
            GD.Print($"POFUDUK animcheck bones={_skel.GetBoneCount()} max_bone_rotation_deg={_maxDeg:0.0} playing={_anim?.IsPlaying()} anim={_anim?.CurrentAnimation} names={string.Join(",", Enumerable.Range(0, _skel.GetBoneCount()).Select(_skel.GetBoneName).Take(12))}");
    }

    public override void _Process(double delta)
    {
        if (!Loaded) return;
        float dt = (float)delta;
        _t += dt;
        if (Main.Args.ContainsKey("animcheck")) AnimCheck();
        _scale = Mathf.Lerp(_scale, _targetScale, Mathf.Min(1f, dt * 2f));
        float wob = _targetScale > 1f ? 1f + Mathf.Sin(_t * 6f) * 0.03f : 1f;
        _visual.Scale = new Vector3(_scale * wob, _scale / wob, _scale * wob);

        var d = _to - GlobalPosition; d.Y = 0;
        if (d.Length() < 0.3f)
        {
            _wait -= dt;
            if (_anim != null && _anim.IsPlaying()) _anim.Pause();
            if (_wait <= 0)
            {
                _wait = (float)GD.RandRange(1.5, 4.0);
                _to = _home + new Vector3((float)GD.RandRange(-6, 6), 0, (float)GD.RandRange(-6, 6));
            }
            return;
        }
        if (_anim != null && !_anim.IsPlaying() && _walk != null) _anim.Play(_walk);
        var step = d.Normalized() * Mathf.Min(d.Length(), _speed * dt);
        GlobalPosition = _d.World.Ground(GlobalPosition + step);
        float yaw = Mathf.Atan2(d.X, d.Z);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(1f, dt * 5f)), 0);
    }
}

/// <summary>
/// Tas Firin (ICERIK: makine). M0 denemesi #2: Tripo parcalara ayirdi, uretim hatti adlandirdi
/// (body, base, door, door_handle, chimney, panel). Kapak mentesesinden kodla acilir, bacadan duman cikar.
/// </summary>
public partial class TasFirin : Node3D
{
    const string ModelPath = "res://assets/models/tas_firin/tas_firin.glb";
    Node3D _doorPivot;
    Node3D _chimney;
    Vector3 _chimneyBase;
    GpuParticles3D _smoke;
    float _t;
    public bool Loaded { get; private set; }
    /// <summary>Pisiriyor mu: kapak acilip kapanir, baca tuter. Bos firin sessiz durur.</summary>
    public bool Working = true;
    public bool HasDoor => _doorPivot != null;

    public override void _Ready()
    {
        var model = Assets.Load(ModelPath);
        if (model == null) { GD.PrintErr("TAS_FIRIN model yok"); return; }
        AddChild(model);
        Loaded = true;
        var door = model.FindChild("door", true, false) as Node3D;
        var handle = model.FindChild("door_handle", true, false) as Node3D;
        if (door is MeshInstance3D dm)
        {
            // Mentese: kapagin bir yan kenari (Godot'ta model +X'e bakiyor; kapak YZ duzleminde).
            var box = dm.Transform * dm.GetAabb();
            var hinge = new Vector3(box.Position.X + box.Size.X * 0.5f, box.GetCenter().Y, box.Position.Z);
            _doorPivot = new Node3D { Name = "DoorHinge", Position = hinge };
            var parent = door.GetParent();
            parent.AddChild(_doorPivot);
            door.Reparent(_doorPivot, true);
            if (handle != null) handle.Reparent(_doorPivot, true);
        }
        var chimney = model.FindChild("chimney", true, false) as MeshInstance3D;
        _chimney = chimney;
        if (chimney != null) _chimneyBase = chimney.Scale;
        var top = chimney != null ? (chimney.Transform * chimney.GetAabb()) : new Aabb(new Vector3(0, 1.3f, 0), Vector3.Zero);
        _smoke = SmokeEmitter();
        AddChild(_smoke);
        _smoke.Position = new Vector3(top.GetCenter().X, top.End.Y + 0.05f, top.GetCenter().Z);
        foreach (var n in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (n.Name != "body" && n.Name != "base") continue;
            var body = new StaticBody3D();
            body.AddChild(new CollisionShape3D { Shape = n.Mesh.CreateConvexShape(), Transform = n.Transform });
            n.GetParent().AddChild(body);
        }
        GD.Print($"TAS_FIRIN parts: {string.Join(",", model.FindChildren("*", "MeshInstance3D", true, false).Select(n => n.Name))} door={HasDoor}");
    }

    static GpuParticles3D SmokeEmitter()
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up, Spread = 12f, InitialVelocityMin = 0.6f, InitialVelocityMax = 1.0f,
            Gravity = new Vector3(0.15f, 0.25f, 0), ScaleMin = 0.8f, ScaleMax = 1.6f,
            DampingMin = 0.2f, DampingMax = 0.5f,
        };
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 0.4f)); curve.AddPoint(new Vector2(1, 1.6f));
        pm.ScaleCurve = new CurveTexture { Curve = curve };
        var grad = new Gradient();
        grad.SetColor(0, new Color(Palette.OffWhite, 0.75f));
        grad.SetColor(1, new Color(Palette.Fog, 0f));
        pm.ColorRamp = new GradientTexture1D { Gradient = grad };
        return new GpuParticles3D
        {
            Amount = 18, Lifetime = 2.6f, ProcessMaterial = pm,
            DrawPass1 = new SphereMesh
            {
                Radius = 0.12f, Height = 0.24f, RadialSegments = 8, Rings = 4,
                Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
            },
        };
    }

    public override void _Process(double delta)
    {
        if (!Loaded) return;
        _t += (float)delta;
        if (_smoke != null) _smoke.Emitting = Working;
        if (!Working) { if (_doorPivot != null) _doorPivot.Rotation = _doorPivot.Rotation.Lerp(Vector3.Zero, 0.1f); return; }
        // Kapak 6 sn'de bir acilip kapanir (pisiriyor gibi): 0..1.6 rad.
        if (_doorPivot != null)
        {
            float phase = Mathf.PosMod(_t, 6f);
            float open = phase < 2.5f ? Mathf.SmoothStep(0f, 1f, phase / 0.6f) * Mathf.SmoothStep(1f, 0f, (phase - 1.9f) / 0.6f) : 0f;
            _doorPivot.Rotation = new Vector3(0, open * 1.6f, 0); // disa dogru (+X)
        }
        // Baca her duman atisinda kisa bir "puf" yapar (kodla hareket eden ikinci parca).
        if (_chimney != null)
        {
            float puff = Mathf.Max(0f, Mathf.Sin(_t * 3.2f)) * 0.06f;
            _chimney.Scale = _chimneyBase * new Vector3(1f + puff, 1f - puff * 0.6f, 1f + puff);
        }
    }
}
