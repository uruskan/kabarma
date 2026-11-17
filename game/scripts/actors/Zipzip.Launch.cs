using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Zipzip: Koc Gidak'in tekmesiyle havada ucus (PLAN §1.4: "birini citin ustunden tekmeliyor").
/// Yol Sim'deki KickArc'tan (tepe, menzil, sure veride; citi astigi CreatureTests'te sinanir). Ucarken takla atar,
/// inince yere carpar (hasar, toz, sersemleme), sonra dalgaya geri doner. Havadayken cit engellemez (Airborne).
/// </summary>
public partial class Zipzip
{
    KickArc _arc;
    float _launchT = -1f, _landStunT, _spinTurns, _landDamage;
    Vector3 _launchDir;

    /// <summary>Tekmeyle havada: cit (Zipzip.Fence.cs) bu sirada engellemez, Gidak yeniden hedeflemez.</summary>
    public bool Airborne => _launchT >= 0f;

    static System.Numerics.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);
    static Vector3 G(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>Tekme: dirXZ yonunde, tekmeleyenin verisindeki yayla ucar; hasar inince verilir.</summary>
    public void Launch(CreatureDef kicker, Vector3 dirXZ, float damage)
    {
        if (Dead || Airborne) return;
        var from = GlobalPosition;
        var flat = new Vector3(dirXZ.X, 0, dirXZ.Z);
        if (flat.LengthSquared() < 1e-4f) flat = Vector3.Forward;
        flat = flat.Normalized();
        float landY = _dir.World.Ground(from + flat * kicker.KickDistance).Y;
        _arc = KickArc.Toward(kicker, N(from), N(flat), landY);
        _launchDir = flat;
        _launchT = 0f;
        _spinTurns = kicker.KickSpinTurns;
        _landStunT = kicker.KickLandStun;
        _landDamage = damage;
        _hopT = 0; _knock = Vector3.Zero; _eatT = 0;
        if (_target != null) { _target.Claimed = false; _target = null; }
        _flash = 1f; _squash = 1f;
        _sw?.HitJiggle();
        _rig?.Play("hit", 0.05f, 1f, restart: true);
        _body.Rotation = new Vector3(0, Mathf.Atan2(-flat.X, -flat.Z), 0); // tekmeleyene bakarak geriye ucar
    }

    /// <summary>Ucus ve inis sersemlemesi; true donerse bu kare baska hareket yok.</summary>
    bool TickLaunch(float dt, float baseScale)
    {
        if (_launchT >= 0f)
        {
            _launchT += dt;
            float u = _arc.Progress(_launchT);
            GlobalPosition = G(_arc.At(u));
            float yaw = _body.Rotation.Y;
            _body.Rotation = new Vector3(-u * _spinTurns * Mathf.Tau, yaw, 0);
            TickLook(dt, baseScale, 0f);
            if (u >= 1f)
            {
                _launchT = -1f;
                _body.Rotation = new Vector3(0, yaw, 0);
                GlobalPosition = _dir.World.Ground(GlobalPosition);
                _squash = 1f;
                _sw?.HitJiggle();
                Fx.Burst(GlobalPosition + Vector3.Up * 0.2f, Palette.Soil.Lerp(Palette.Ground, 0.4f), 18, 4f, 0.12f, 0.5f);
                Sfx.Play("hit", GlobalPosition, -2f);
                Hit(_landDamage, false, Vector3.Zero, 0f, false);
            }
            return true;
        }
        if (_landStunT > 0f)
        {
            _landStunT -= dt;
            TickLook(dt, baseScale, 0f);
            return true;
        }
        return false;
    }
}
