using Godot;

namespace Kabarma.Game;

/// <summary>
/// Koc Gidak'in tekmesi (PLAN §1.4: "Gidak'in midilli boyuna gelip birini citin ustunden tekmeliyor").
/// Zipzip kendinden disa dogru, citin ustunden yay cizerek ucar (Zipzip.Launch, Sim KickArc). Iki saldiri klibi sirayla:
/// bacak tekmesi ve kanat-gaga savurmasi.
/// </summary>
public partial class Gidak
{
    bool _kickAlt;

    /// <summary>Hemen tekmeler (sinama ve gorunus sahnesi; oyunda tekme _Process'te menzil ve araliga baglidir).</summary>
    public void KickNow(Zipzip z) => Kick(z);

    void Kick(Zipzip z)
    {
        var dir = z.GlobalPosition - GlobalPosition; dir.Y = 0;
        if (dir.LengthSquared() < 0.01f) dir = GlobalBasis.Z;
        Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        z.Launch(Def, dir, Def.Damage);
        _dir.OnAnimalHit();
        Fx.Burst(z.Center, Palette.Warning, 10, 5f, 0.1f, 0.4f, true);
        Sfx.Play("hit", GlobalPosition, 2f);
        _rig?.Play(_kickAlt ? "attack2" : "attack1", 0.05f, 1f, restart: true);
        _kickAlt = !_kickAlt;
        _kickAnimT = 0.6f;
        _sw?.HitJiggle();
        if (_rig == null) _visual.Rotation = new Vector3(-0.5f, _visual.Rotation.Y, 0);
        GD.Print($"GIDAK kick to={dir.Normalized().X:0.00},{dir.Normalized().Z:0.00}");
    }
}
