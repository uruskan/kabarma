using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Suru varligi (Cekirge Surusu, Vizviz; TEKNIK "Sururler"): tek can havuzu, tek dugum. Uyeler tek mesh'in
/// MultiMesh ornekleridir; yerleri Sim'deki tohumdan (Swarms.Member) her kare hesaplanir, fizik yok. Uye mesh'i ve
/// kovan bodies.json'dan koddan kurulur (yer tutucu). Kabarinca uye sayisi ve yaricap buyur, ortak sisme shader'iyla
/// isirlar (Cekirge: Bulut, Vizviz: Ofkeli Kovan). Dugum suru merkezindedir; kovan sabit (TopLevel).
/// </summary>
public partial class WildSwarm : Node3D
{
    public readonly CreatureDef Def;
    public readonly SwarmState State = new();
    readonly IWildWorld _w;
    readonly MultiMesh _mm;
    readonly MultiMeshInstance3D _mmi;
    readonly Swellable _sw;
    readonly Node3D _hive;
    readonly Swellable _hiveSw;
    readonly int _max;
    float _t, _senseT, _scale = 1f, _flash;
    bool _hasTarget;
    Vector3 _target;
    public SwarmStep Last;
    public float Travelled;
    public bool Display;
    public bool Dead => State.Hp <= 0;
    public int Visible => _mm.VisibleInstanceCount;
    public Swellable Look => _sw;
    public Node3D Hive => _hive;

    public WildSwarm(CreatureDef def, GameData data, IWildWorld world, int id, Vector3 at, ulong seed)
    {
        Def = def; _w = world;
        Name = $"Swarm_{def.Id}_{id}";
        State.Id = id;
        Swarms.Init(def, State, at.X, at.Z, seed);
        var body = data.Body(def.Id);
        var mesh = body != null ? CreatureBody.BuildStatic(body) : new ArrayMesh();
        if (body == null) GD.PrintErr($"SWARM {def.Id}: bodies.json'da uye govdesi yok");
        _sw = new Swellable(mesh, def.Swell);
        _max = Swarms.Count(def.Swarm!, true);
        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = _max, VisibleInstanceCount = Swarms.Count(def.Swarm!, false) };
        _mmi = new MultiMeshInstance3D { Name = "Members", Multimesh = _mm };
        AddChild(_mmi);
        var hb = def.Swarm!.Hive ? data.Body(def.Id + "_hive") : null;
        if (hb != null)
        {
            var root = new Node3D { Name = "Hive", TopLevel = true };
            root.AddChild(CreatureBody.Build(hb, "HiveBody"));
            AddChild(root);
            _hive = root;
            _hiveSw = new Swellable(root, def.Swell);
        }
        AddToGroup("wild");
    }

    public override void _Ready()
    {
        if (_hive != null) _hive.GlobalPosition = _w.Ground(new Vector3(State.HomeX, 0, State.HomeZ));
        Place(0f);
    }

    public void Swell(bool on)
    {
        State.Swollen = on;
        _sw.Swell(on); _hiveSw?.Swell(on);
    }

    public void SnapSwell(bool on)
    {
        State.Swollen = on;
        _sw.Snap(on); _hiveSw?.Snap(on);
    }

    /// <summary>Darbe can havuzuna gider (GDD §14.3); suru dagilir.</summary>
    public void Hit(float dmg, Vector3 from)
    {
        _flash = 1f;
        _sw.HitJiggle();
        if (Swarms.Damage(Def, State, dmg, from.X, from.Z)) QueueFree();
    }

    public override void _Process(double delta) => Tick((float)delta);

    public void Tick(float dt)
    {
        _t += dt;
        float amt = _sw.Tick(dt);
        _hiveSw?.Tick(dt);
        _scale = Mathf.Lerp(1f, Def.SwollenScale, amt);
        if (_flash > 0) { _flash = Mathf.Max(0, _flash - dt * 6f); _sw.Set(flash: _flash * 0.5f); }
        _mm.VisibleInstanceCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(Swarms.Count(Def.Swarm!, false), _max, Mathf.Clamp(amt, 0f, 1f))), 1, _max);
        if (!Display)
        {
            var w = Def.Wild;
            _senseT -= dt;
            if (_senseT <= 0 && w != null && !string.IsNullOrEmpty(w.Target))
            {
                _senseT = 0.5f;
                _hasTarget = _w.FindTarget(w.Target, GlobalPosition, w.TargetSense, out _target);
            }
            var p = _w.PlayerPos;
            float x0 = State.X, z0 = State.Z;
            Last = Swarms.Step(Def, State, new SwarmSense { PlayerX = p.X, PlayerZ = p.Z, Night = _w.Night, HasTarget = _hasTarget, TargetX = _target.X, TargetZ = _target.Z }, dt);
            Travelled += Mathf.Sqrt((State.X - x0) * (State.X - x0) + (State.Z - z0) * (State.Z - z0));
            if (Last.Event != WildEvent.None) _w.ReportSwarm(this, Last.Event, Last.Sting);
        }
        Place(dt);
    }

    /// <summary>Merkezi zemine oturtur, uyeleri tohumdan yerlestirir (yere inmis suru uyurken alcalir).</summary>
    void Place(float dt)
    {
        var center = _w.Ground(new Vector3(State.X, 0, State.Z));
        GlobalPosition = center;
        var d = Def.Swarm!;
        bool sleeping = Last.Mode == WildMode.Sleep;
        for (int i = 0; i < _mm.VisibleInstanceCount; i++)
        {
            var o = Swarms.Member(d, State, i, _t);
            var o2 = Swarms.Member(d, State, i, _t + 0.05f);
            var o3 = new Vector3(o.X, o.Y, o.Z);
            if (sleeping) o3 = new Vector3(o3.X * 0.6f, 0.04f, o3.Z * 0.6f);
            var fwd = new Vector3(o2.X - o.X, 0, o2.Z - o.Z);
            float yaw = fwd.LengthSquared() > 1e-6f ? Mathf.Atan2(fwd.X, fwd.Z) : 0f;
            float flap = sleeping ? 1f : 1f + 0.12f * Mathf.Sin(_t * 40f + i);
            var basis = Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(new Vector3(_scale, _scale * flap, _scale));
            _mm.SetInstanceTransform(i, new Transform3D(basis, o3 * new Vector3(1f, _scale > 1.2f ? 1.3f : 1f, 1f)));
        }
    }
}
