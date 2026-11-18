using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Evcil hayvan davranisi, turden bagimsiz bilesen (GDD 9.1-9.4). Herhangi bir yaratik aktorune (Node3D) cocuk olarak
/// takilir:
/// - Yabaniyken: aktor kendi gezer; bilesen yalniz korkutmaya (kosma, ates) bakar.
/// - Evcilken: oyuncuyu izler, acik kapidan agila girince kayit olur (kovan turu Ari Kovani'na yerlesir).
/// - Kayitliyken komuta gore davranir: don (agilda gezin), izle, bekle, saldir. Bekci rolu gunduz zararlisini kovalar.
/// - Mutsuz kalirsa acik kapidan kacar.
/// Kurallar Sim'de (Ranch, HusbandryRules); burasi yalniz yurutur ve konumu Sim'e yazar.
/// Aktor IHusbandryHost uygularsa yurumeyi ona birakir; uygulamazsa konumu kendisi yazar (ProcessPriority 90: aktordan sonra).
/// </summary>
public partial class HusbandryAgent : Node
{
    public Node3D Host { get; }
    public PenAnimal Animal { get; }
    public SpeciesDef Species { get; }
    /// <summary>Yalniz kart, panel ve E icin: aktor kendi agil yolunu yurur (ilk gunun Gidak'i, Gidak.Pen.cs).</summary>
    public bool Passive { get; set; }
    /// <summary>Bilesen hayvani yurutuyor (evcil ya da tutulmus).</summary>
    public bool Driven { get; private set; }
    readonly Director _d;
    readonly RanchSystem _r;
    readonly IHusbandryHost _iface;
    Vector3 _pos, _goal;
    bool _posSet;
    float _routeT, _wanderT, _scareT, _bob, _t;
    PenShape _home;

    public HusbandryAgent(Director d, RanchSystem r, Node3D host, PenAnimal animal)
    {
        _d = d; _r = r; Host = host; Animal = animal;
        Species = r.Ranch.Rules.Species(animal.Species);
        _iface = host as IHusbandryHost;
        Name = "Husbandry";
        ProcessPriority = 90; // aktorden sonra, PenSystem'den (100) once
    }

    public float Radius => 0.3f * Mathf.Max(0.6f, Species.Size);
    public string Title => Species.Name + (Animal.Adult(_r.Ranch.Rules.Def.Breeding.AdultDays) ? "" : " (yavru)");
    public float HpFraction => _iface?.HpFraction ?? 1f;

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Host)) { QueueFree(); return; }
        float dt = (float)delta;
        _t += dt;
        var ps = _d.Pens;
        if (Passive) { Animal.X = Host.GlobalPosition.X; Animal.Z = Host.GlobalPosition.Z; return; }
        if (!_posSet || !Driven) { _pos = Host.GlobalPosition; _posSet = true; }
        // Duvar: bu karede gecilmez duvari kestiyse onunde durur (acik kapi gecilir).
        var now = Driven && _iface == null ? _pos : Host.GlobalPosition;
        Animal.X = now.X; Animal.Z = now.Z;

        if (!Animal.Tame && !Animal.Bound)
        {
            Release();
            ScareCheck(dt);
            return;
        }
        if (!Animal.Tame) { Steer(Host.GlobalPosition, 0f, dt, ps); return; } // tutulmus yirtici yerinde bekler

        Vector3 goal;
        float speed = Species.Speed;
        var p = _d.Player.GlobalPosition;
        if (Animal.Registered && Animal.HiveId != 0)
        {
            var h = _r.Ranch.Hives.FirstOrDefault(x => x.Id == Animal.HiveId);
            var c = h == null ? Host.GlobalPosition : new Vector3(h.X, 0, h.Z);
            goal = c + new Vector3(Mathf.Cos(_t * 0.8f), 0, Mathf.Sin(_t * 0.8f)) * 1.2f; // kovan cevresinde vizilti
        }
        else if (Animal.Registered && Animal.WantsOut) goal = EscapeGoal(ps) ?? Wander(ps, dt);
        else if (!Animal.Registered)
        {
            goal = Follow(ps, p, dt);
            TryHome(ps);
        }
        else
        {
            switch (Animal.Command)
            {
                case "izle": goal = Follow(ps, p, dt); break;
                case "bekle": goal = Host.GlobalPosition; break;
                case "saldir": goal = Chase(p, 14f) ?? Follow(ps, p, dt); speed *= 1.4f; break;
                default:
                    goal = Animal.Role == "bekci" && Chase(HomeCenter() ?? p, 16f) is Vector3 z ? z : Wander(ps, dt);
                    if (Animal.Role == "bekci") speed *= 1.3f;
                    break;
            }
        }
        Steer(goal, speed, dt, ps);
    }

    /// <summary>Kendi davranisina birak (yabani ya da kacmis).</summary>
    void Release()
    {
        if (!Driven) return;
        Driven = false;
        _iface?.HusbandrySteer(Host.GlobalPosition, 0f, false);
    }

    /// <summary>Kosmak ve ates etmek dolmamis guveni sifirlar (GDD 9.1).</summary>
    void ScareCheck(float dt)
    {
        _scareT -= dt;
        if (Animal.Trust <= 0 || _scareT > 0) return;
        var pl = _d.Player;
        if (pl.GlobalPosition.DistanceTo(Host.GlobalPosition) > _d.Data.Pen.ScareRadius) return;
        bool sprint = Input.IsActionPressed("sprint") && pl.MoveOverride == null && pl.Velocity.Length() > pl.WalkSpeed * 1.1f;
        bool fire = Capa.IsWeapon(_d.Capa.Current) && _d.Capa.UsingNow;
        if (!sprint && !fire) return;
        _r.Ranch.Rules.Scare(Animal);
        _scareT = 1f;
        Fx.Text3D(Host.GlobalPosition + Vector3.Up * (Species.Size + 0.4f), "!", Palette.Warning, 72, 0.8f, 1f);
    }

    Vector3 Follow(PenSystem ps, Vector3 p, float dt)
    {
        var target = p + (Host.GlobalPosition - p).Normalized() * (_d.Data.Pen.FollowDistance + Species.Size * 0.5f);
        if (ps == null) return target;
        if (ps.PenAt(p) != null || ps.FirstHit(p, target, Radius) != null) target = p;
        _routeT -= dt;
        if (_routeT <= 0 || Host.GlobalPosition.DistanceTo(_goal) < 0.5f)
        {
            _routeT = 0.25f;
            _goal = ps.Route(Host.GlobalPosition, target, Radius);
        }
        return _goal;
    }

    /// <summary>Evcil, kayitsiz: agila girdiyse kayit (GDD 9.1), kovan turu bos kovana yerlesir.</summary>
    void TryHome(PenSystem ps)
    {
        var r = _r.Ranch;
        if (Species.Home == "hive")
        {
            var h = r.Hives.Where(x => x.AnimalId == 0).OrderBy(x => new Vector2(x.X - Animal.X, x.Z - Animal.Z).Length()).FirstOrDefault();
            if (h != null && new Vector2(h.X - Animal.X, h.Z - Animal.Z).Length() < 2.5f && r.Settle(Animal, h)) Homed();
            return;
        }
        var pen = ps?.PenAt(Host.GlobalPosition);
        if (pen == null || !pen.HasGate) return;
        if (r.Register(Animal, pen)) { _home = pen; Animal.Command = "don"; Homed(); }
    }

    void Homed()
    {
        GD.Print($"RANCH registered {Animal.Species} id={Animal.Id} genes={Animal.Genes}");
        Fx.Text3D(Host.GlobalPosition + Vector3.Up * (Species.Size + 0.5f), "Kayıt", Palette.CoopBlue, 64, 1.2f, 1.4f);
        Sfx.Play("pickup", Host.GlobalPosition, -4f);
        Events.RaiseAnimalPenned(Animal.Species);
    }

    Vector3? HomeCenter()
    {
        if (_home == null) return null;
        return new Vector3(_home.Centroid.X, 0, _home.Centroid.Z);
    }

    /// <summary>Agilinda rastgele gezinir; agil disinda kaldiysa (izle komutundan donus) agila doner.</summary>
    Vector3 Wander(PenSystem ps, float dt)
    {
        var here = ps?.PenAt(Host.GlobalPosition);
        if (here != null) _home = here;
        else if (_home != null && ps != null && !ps.Pens.Contains(_home)) _home = ps.Pens.FirstOrDefault(x => x.Contains(new P2(Animal.X, Animal.Z)));
        if (_home == null) return Host.GlobalPosition;
        if (here == null)
        {
            _routeT -= dt;
            if (_routeT <= 0) { _routeT = 0.25f; _goal = ps.Route(Host.GlobalPosition, HomeCenter()!.Value, Radius); }
            return _goal;
        }
        _wanderT -= dt;
        if (_wanderT <= 0 || Host.GlobalPosition.DistanceTo(_goal) < 0.3f && _wanderT > 3f)
        {
            _wanderT = (float)GD.RandRange(2.5, 6.0);
            var c = _home.Centroid;
            for (int i = 0; i < 8; i++)
            {
                var q = new P2(c.X + (float)GD.RandRange(-3.0, 3.0), c.Z + (float)GD.RandRange(-3.0, 3.0));
                if (_home.Contains(q)) { _goal = new Vector3(q.X, 0, q.Z); break; }
            }
        }
        return _goal;
    }

    /// <summary>Kacma (GDD 9.1): agilinin acik kapisindan disari.</summary>
    Vector3? EscapeGoal(PenSystem ps)
    {
        if (ps == null || _home == null) return null;
        var gate = ps.Walls.Where(w => w.Gate && w.Open && _home.Walls.Contains(w.Id))
            .OrderBy(w => PenRules.DistanceToWall(w, new P2(Animal.X, Animal.Z))).FirstOrDefault();
        if (gate == null) return null;
        var mid = new Vector3(gate.Mid.X, 0, gate.Mid.Z);
        var c = HomeCenter()!.Value;
        var outside = mid + (mid - c).Normalized() * 4f;
        return ps.Route(Host.GlobalPosition, outside, Radius);
    }

    /// <summary>Yakindaki gunduz zararlisi (Zipzip): bekci ve saldir komutu (GDD 9.4).</summary>
    Vector3? Chase(Vector3 around, float radius)
    {
        var z = Zipzip.All.Where(e => IsInstanceValid(e) && !e.Dead && e.GlobalPosition.DistanceTo(around) < radius)
            .OrderBy(e => e.GlobalPosition.DistanceTo(Host.GlobalPosition)).FirstOrDefault();
        if (z == null) return null;
        if (z.GlobalPosition.DistanceTo(Host.GlobalPosition) < 1.6f && _scareT <= 0)
        {
            _scareT = 1.2f;
            Fx.Text3D(Host.GlobalPosition + Vector3.Up * (Species.Size + 0.4f), "!", Palette.Warning, 72, 0.6f, 1f);
        }
        _scareT -= (float)GetProcessDeltaTime();
        return z.GlobalPosition;
    }

    /// <summary>Hedefe yurut: arayuzlu aktor kendi yurur; arayuzsuz aktorun konumunu bilesen yazar (duvar dahil).</summary>
    void Steer(Vector3 goal, float speed, float dt, PenSystem ps)
    {
        if (!Driven) { Driven = true; _pos = Host.GlobalPosition; }
        if (_iface != null) { _iface.HusbandrySteer(goal, speed, true); return; }
        var d = goal - _pos; d.Y = 0;
        var from = _pos;
        if (d.Length() > 0.35f && speed > 0)
        {
            var step = d.Normalized() * Mathf.Min(d.Length(), speed * dt);
            _pos = _d.World.Ground(_pos + step);
            float yaw = Mathf.Atan2(d.X, d.Z);
            Host.Rotation = new Vector3(0, Mathf.LerpAngle(Host.Rotation.Y, yaw, Mathf.Min(1f, dt * 6f)), 0);
            _bob += dt;
        }
        if (ps != null && ps.FirstHit(from, _pos, Radius) is WallHit h) _pos = _d.World.Ground(new Vector3(h.Point.X, 0, h.Point.Z));
        Host.GlobalPosition = _pos;
    }
}
