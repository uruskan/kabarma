using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Azman Bozuk'un sokup firlattigi kutuk (yer tutucu: koddan silindir). Yay cizerek duser; dustugu yerde cevresine
/// hasar verir (yerel oyuncu, yapi parcalari). Sonra yerde kalir: Cekim'le tutulup ("cekim" grubu) Azman'a
/// firlatilabilir (azman.json uproot.logDamage, Darbe, zirhsiz). LogSeconds sonra cozulur.
/// </summary>
public partial class AzmanLog : Node3D, ICarryable
{
    readonly Director _d;
    readonly AzmanSystem _sys;
    readonly AzmanUprootDef _u;
    Vector3 _from, _to;
    float _flyT = -1, _flyDur, _life;
    bool _gone, _landed;

    public AzmanLog(Director d, AzmanSystem sys, AzmanUprootDef u) { _d = d; _sys = sys; _u = u; }

    public Node3D Body => this;
    public float Kg => 40f;
    public float Radius => 0.7f;
    public bool Held { get; set; }
    public bool Gone => _gone || IsQueuedForDeletion();

    public override void _Ready()
    {
        var kit = new MeshKit();
        kit.Add(new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.38f, Height = 2.6f, RadialSegments = 8 }, MeshKit.At(Vector3.Zero, new Vector3(0, 0, Mathf.Pi / 2)), Palette.Wood.Darkened(0.2f));
        kit.Sphere(0.6f, MeshKit.At(1.4f, 0.2f, 0), Palette.Plant.Darkened(0.3f), 8, 5);
        AddChild(kit.Instance());
    }

    public void Launch(Vector3 from, Vector3 to, float speed)
    {
        _from = from; _to = to + Vector3.Up * 0.4f;
        _flyDur = Mathf.Clamp(from.DistanceTo(to) / Mathf.Max(1f, speed), 0.5f, 2.2f);
        _flyT = 0;
        GlobalPosition = from;
        AddToGroup("azman_flying"); // sinama: havadaki kutuk (bot kacar)
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_gone) return;
        if (_flyT >= 0)
        {
            _flyT += dt;
            float k = Mathf.Clamp(_flyT / _flyDur, 0, 1);
            GlobalPosition = _from.Lerp(_to, k) + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * (3f + _flyDur * 2f);
            Rotation = new Vector3(k * 5f, k * 2f, 0);
            if (k >= 1) { _flyT = -1; Land(); }
            return;
        }
        if (!_landed) return;
        _life += dt;
        if (_life > _u.LogSeconds && !Held) { _gone = true; Fx.Burst(GlobalPosition, Palette.Wood, 14, 3f, 0.1f, 0.5f); QueueFree(); return; }
        // Firlatilmis (ThrowMotion cocugu varken ucuyor): bir Azman'a degerse buyuk hasar.
        bool flying = !Held && GetNodeOrNull(ThrowMotion.NodeName) != null;
        if (!flying) return;
        foreach (var a in _sys.Actors)
        {
            if (!a.Alive) continue;
            var c = a.Center;
            if (GlobalPosition.DistanceTo(c) > a.Radius + Radius + 0.5f) continue;
            _gone = true;
            _sys.LogHits++;
            var r = _d.Savas.Hit(a, new CombatHit(_u.LogDamage, DamageType.Darbe, CombatTeam.Player) { IgnoreArmor = true, Crit = true }, GlobalPosition);
            GD.Print($"AZMAN log_hit dmg={r.Damage:0}");
            Fx.Burst(GlobalPosition, Palette.Wood, 40, 7f, 0.2f, 0.9f);
            Sfx.Play("isabet_kritik", GlobalPosition, 6f, 0.6f);
            _d.HitStop(0.08f);
            QueueFree();
            return;
        }
    }

    void Land()
    {
        _landed = true;
        _sys.LogLandings++;
        RemoveFromGroup("azman_flying");
        GD.Print($"AZMAN log_land at=({GlobalPosition.X:0},{GlobalPosition.Z:0})");
        var at = GlobalPosition;
        Fx.Burst(at, Palette.Soil, 40, 7f, 0.22f, 0.9f);
        Sfx.Play("kabarik_gumleme", at, 4f, 0.8f);
        var p = _d.Player;
        if (p != null && !p.Down && p.GlobalPosition.DistanceTo(at) < _u.Radius + 0.5f)
        {
            p.Hurt(_u.Damage, at);
            p.Velocity += ((p.GlobalPosition - at) with { Y = 0 }).Normalized() * _u.Knockback + Vector3.Up * 3f;
        }
        var sys = _d.Build?.System;
        if (sys != null)
            foreach (var piece in sys.Pieces.Where(x => IsInstanceValid(x) && x.GlobalPosition.DistanceTo(at) < _u.Radius + 1f).ToList())
                _d.Build.Damage(piece, _u.StructureDamage);
        GlobalPosition = _d.World.Ground(at) + Vector3.Up * 0.35f;
        Rotation = new Vector3(0, Rotation.Y, 0);
        AddToGroup("cekim");
    }
}
