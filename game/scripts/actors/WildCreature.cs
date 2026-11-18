using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Yabani yaratigin gordugu dunya (Director ya da sinama sahnesi). Dar tutulur: test tam Director'suz kosar.</summary>
public interface IWildWorld
{
    Vector3 Ground(Vector3 p);
    Vector3 PlayerPos { get; }
    /// <summary>Takvimin normal gecesi (Kabarma gecesinde yabaniler sahnede degildir).</summary>
    bool Night { get; }
    /// <summary>Hedef ara: "crop" olgun ekin, "grain" olgun tahil, "seed" ekili tohum, "coop" kumes/Gidak, "flower" cicek.</summary>
    bool FindTarget(string kind, Vector3 from, float range, out Vector3 at);
    /// <summary>Davranis olayi: etkiyi sahibi olan sistem uygular (ICERIK §3.8); hasar M2'de kucuk.</summary>
    void Report(WildCreature c, WildEvent e);
    /// <summary>Suru olayi; sting: oyuncuyu soktu (M2: kucuk hasar).</summary>
    void ReportSwarm(WildSwarm s, WildEvent e, bool sting);
}

/// <summary>
/// Yabani yaratik aktoru (ICERIK §3.8): veriyle surulen tek sinif, butun iskeletli turler icin. Gorunus CreatureRig
/// (Tripo modeli ya da koddan govde) + PoseLibrary'nin 8 klibi + ortak sisme shader'i (Swellable) ve Maya kesesi.
/// Kararlar Sim'de (WildBrain.Step); durum WildState'te (duz, kayda hazir). Statik yok: listeler Wildlife dugumunde.
/// Evcil hal (hayvan akisi): TameSteer partial'i uygulanir ve handled = true donerse beyin atlanir.
/// </summary>
public partial class WildCreature : Node3D
{
    public readonly CreatureDef Def;
    public readonly WildState State;
    public WildHerdState Herd;
    /// <summary>Surunun ortak saatini bu uye ilerletir (Wildlife atar).</summary>
    public bool LeadsHerd;
    readonly IWildWorld _w;
    readonly Node3D _visual;
    public CreatureRig Rig { get; }
    public Swellable Look { get; }
    MeshInstance3D _sac;
    float _scale = 1f, _flash, _lock, _stepAcc, _senseT, _yaw, _flyY;
    bool _hit, _hasTarget;
    Vector3 _target;
    public WildStep Last;
    /// <summary>Toplam yuruyus (m): sinama "hareket ediyor mu".</summary>
    public float Travelled;
    public bool Dead => State.Hp <= 0;
    /// <summary>Sinama: yerinde durur, DisplaySlot klibini oynatir.</summary>
    public bool Display;
    public string DisplaySlot = "idle";
    TrailMarks _trail;

    public WildCreature(CreatureDef def, PoseLibrary poses, IWildWorld world, WildHerdState herd, WildState state)
    {
        Def = def; _w = world; Herd = herd; State = state;
        Name = $"Wild_{def.Id}_{state.Id}";
        State.Species = def.Id;
        if (State.Hp <= 0) State.Hp = def.Hp;
        _visual = new Node3D { Name = "Visual" };
        AddChild(_visual);
        Rig = CreatureRig.Load(def, poses, _visual);
        if (Rig?.Model == null) { GD.PrintErr($"WILD {def.Id}: model yok ({def.Rig?.Model})"); return; }
        if (Rig.Body == null) FitHeight(Rig.Model, def.Rig?.Height ?? 0.75f);
        Look = new Swellable(Rig.Model, def.Swell);
        if (Rig.Body != null) _sac = CreatureBody.Sac(Rig.Body, Rig.Skeleton);
        Rig.Play("idle", 0f);
        _yaw = Rotation.Y;
        AddToGroup("wild");
    }

    public bool Loaded => Rig != null;

    /// <summary>Tripo modelini rig boyuna olcekler ve tabanini yere oturtur (Gidak ile ayni).</summary>
    static void FitHeight(Node3D model, float height)
    {
        Aabb box = new(); bool first = true;
        foreach (var mi in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var b = PoseLibrary.RelTo(mi, model) * mi.GetAabb();
            box = first ? b : box.Merge(b); first = false;
        }
        if (first || box.Size.Y < 0.01f) return;
        float s = height / box.Size.Y;
        model.Scale = Vector3.One * s;
        model.Position = new Vector3(-box.GetCenter().X * s, -box.Position.Y * s, -box.GetCenter().Z * s);
    }

    public void Swell(bool on) { State.Swollen = on; Look?.Swell(on); }
    public void SnapSwell(bool on) { State.Swollen = on; Look?.Snap(on); }

    /// <summary>Darbe: can duser, darbe klibi ve esneme; sonraki adimda beyin "vuruldu" gorur (kacar ya da ofkelenir).</summary>
    public void Hit(float dmg)
    {
        State.Hp -= dmg;
        _hit = true;
        _flash = 1f;
        Look?.HitJiggle();
        Rig?.Play("hit", 0.05f, 1f, restart: true);
        _lock = 0.4f;
    }

    /// <summary>Yakinda ates edildi ya da korkutuldu: vurulmus gibi tepki (hasarsiz).</summary>
    public void Startle() => _hit = true;

    /// <summary>
    /// Evcil hal (hayvan akisi, GDD §9): her kare beyinden once. handled = true ise yaratik goal'e speed ile gider,
    /// clip yuvasini oynatir; yabani beyin o kare calismaz.
    /// </summary>
    partial void TameSteer(float dt, ref Vector3 goal, ref float speed, ref string clip, ref bool handled);

    public override void _Process(double delta) => Tick((float)delta);

    public void Tick(float dt)
    {
        if (!Loaded) return;
        _scale = Look != null ? Mathf.Lerp(1f, Def.SwollenScale, Look.Tick(dt)) : 1f;
        _visual.Scale = Vector3.One * _scale;
        if (_sac != null) _sac.Visible = (Look?.Amount ?? 0f) > 0.5f;
        if (_flash > 0) { _flash = Mathf.Max(0, _flash - dt * 8f); Look?.Set(flash: _flash * 0.6f); }
        if (Display) { Rig.PlayRepeat(DisplaySlot); return; }

        var pos = GlobalPosition;
        State.X = pos.X; State.Z = pos.Z;
        _lock -= dt;

        Vector3 goal = pos; float speed = 0; string clip = "idle"; bool handled = false;
        TameSteer(dt, ref goal, ref speed, ref clip, ref handled);
        bool face = false; Vector3 faceAt = default;
        if (!handled)
        {
            var w = Def.Wild;
            if (LeadsHerd && Herd != null && w != null) WildBrain.StepHerd(w, Herd, dt);
            _senseT -= dt;
            if (_senseT <= 0 && w != null && !string.IsNullOrEmpty(w.Target))
            {
                _senseT = 0.5f;
                _hasTarget = _w.FindTarget(w.Target, pos, w.TargetSense, out _target);
            }
            var p = _w.PlayerPos;
            var sense = new WildSense { PlayerX = p.X, PlayerZ = p.Z, Night = _w.Night, Hit = _hit, HasTarget = _hasTarget, TargetX = _target.X, TargetZ = _target.Z };
            _hit = false;
            Last = WildBrain.Step(Def, Herd ?? new WildHerdState { WanderX = pos.X, WanderZ = pos.Z, HomeX = pos.X, HomeZ = pos.Z }, State, sense, dt);
            if (Last.Event != WildEvent.None) _w.Report(this, Last.Event);
            goal = new Vector3(Last.GoalX, pos.Y, Last.GoalZ);
            speed = Last.Speed; clip = Last.Clip;
            face = Last.Face; faceAt = new Vector3(Last.FaceX, 0, Last.FaceZ);
            if (Last.Event is WildEvent.Struck or WildEvent.Warned) { _lock = 0.5f; Rig.Play(clip, 0.08f, 1f, restart: true); }
        }

        var d = goal - pos; d.Y = 0;
        bool moving = speed > 0.01f && d.Length() > 0.15f;
        if (moving)
        {
            var step = d.Normalized() * Mathf.Min(d.Length(), speed * dt);
            GlobalPosition = _w.Ground(pos + step);
            Travelled += step.Length();
            _yaw = Mathf.Atan2(d.X, d.Z);
            _stepAcc += step.Length();
            if (_stepAcc > 0.35f * _scale) { _stepAcc = 0; Look?.StepJiggle(); _trail?.Drop(GlobalPosition, _yaw); }
        }
        else if (face)
        {
            var f = faceAt - pos; f.Y = 0;
            if (f.LengthSquared() > 0.01f) _yaw = Mathf.Atan2(f.X, f.Z);
        }
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _yaw, Mathf.Min(1f, dt * 6f)), 0);
        if (Def.Wild?.TrailSeconds > 0 && _trail == null && IsInsideTree()) { _trail = new TrailMarks(Def.Wild.TrailSeconds) { Name = "Trail" }; GetParent().AddChild(_trail); }

        // Suzulen tur (Hu): uyanikken yururken yerden yukselir, kanat cirpar (kus planinin kosu klibi).
        float fly = Def.Wild?.FlyHeight ?? 0f;
        bool flying = fly > 0 && moving && WildBrain.Awake(Def.Wild!, _w.Night);
        _flyY = Mathf.Lerp(_flyY, flying ? fly : 0f, Mathf.Min(1f, dt * 2f));
        _visual.Position = new Vector3(0, _flyY, 0);
        if (flying) clip = "run";

        if (_lock > 0) return;
        // Adim hizi yurunen hiza gore: klibin dogal hizi tur hizinin yuruyus/kosu carpanina denk.
        float rate = 1f;
        if (moving && Def.Wild != null)
        {
            float baseSpeed = Def.Speed * (clip == "run" ? Def.Wild.RunSpeedMult : Def.Wild.WalkSpeedMult);
            rate = Mathf.Clamp(speed / Mathf.Max(0.01f, baseSpeed) / Mathf.Max(1f, _scale * 0.6f), 0.5f, 1.6f);
        }
        Rig.Play(moving ? (clip is "run" ? "run" : "walk") : clip, 0.2f, rate);
    }

    public override void _ExitTree() { if (IsInstanceValid(_trail)) _trail.QueueFree(); }
}

/// <summary>Vicik'in birakdigi iz (ICERIK §3.8): yere yatik, zamanla solan lekeler. Gorunus; yavaslatma M3 (Kaydirak).</summary>
public partial class TrailMarks : Node3D
{
    readonly float _life;
    readonly System.Collections.Generic.List<(MeshInstance3D m, float t)> _marks = new();
    readonly StandardMaterial3D _mat;

    public TrailMarks(float life)
    {
        _life = life;
        _mat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.85f, 0.7f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.15f, Metallic = 0.1f };
    }

    public void Drop(Vector3 at, float yaw)
    {
        var m = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 0.01f, RadialSegments = 10 }, MaterialOverride = _mat };
        AddChild(m);
        m.GlobalPosition = at + Vector3.Up * 0.015f;
        m.Rotation = new Vector3(0, yaw, 0);
        m.Scale = new Vector3(1f, 1f, 1.6f);
        _marks.Add((m, 0f));
    }

    public override void _Process(double delta)
    {
        for (int i = _marks.Count - 1; i >= 0; i--)
        {
            var (m, t) = _marks[i];
            t += (float)delta;
            if (t >= _life) { m.QueueFree(); _marks.RemoveAt(i); continue; }
            m.Transparency = t / _life;
            _marks[i] = (m, t);
        }
    }
}
