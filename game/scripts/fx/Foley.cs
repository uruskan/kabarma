using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Izleyerek calan sesler (audio.json "foley"): oyuncunun adimlari (zemine gore ot, toprak, tahta) ve inisi,
/// Gidak'in bekleme gidaklamasi, Ocak'in ates dongusu. Durumu okur; oyuncu, Gidak ve Ocak koduna dokunmaz.
/// Sahip akislar bu sesleri animasyon olayina baglarsa buradaki karsiligi kaldirilir.
/// </summary>
public partial class Foley : Node
{
    readonly Director _d;
    readonly FoleyDef _f;
    float _walked, _fallSpeed, _cluckT;
    bool _airborne;
    AudioStreamPlayer3D _fire;

    public Foley(Director d)
    {
        _d = d;
        _f = d.Data.Audio.Foley;
        _cluckT = _f.CluckMin;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Steps(dt);
        Gidak(dt);
        // Ocak kurulunca bir kez: ates dongusu (dosyasi yoksa bir daha denenmez).
        if (_d.Ocak != null && _d.Ocak != _fireFor && IsInstanceValid(_d.Ocak))
        {
            _fireFor = _d.Ocak;
            if (_fire != null && IsInstanceValid(_fire)) _fire.QueueFree();
            _fire = Sfx.Emitter("ates_citirti", _d.Ocak, _f.FireDistance);
        }
    }

    Node3D _fireFor;

    void Steps(float dt)
    {
        var p = _d.Player;
        if (p == null || !p.IsInsideTree() || p.Down) { _walked = 0; return; }
        var v = p.Velocity;
        if (!p.IsOnFloor())
        {
            _airborne = true;
            _fallSpeed = Mathf.Max(_fallSpeed, -v.Y);
            return;
        }
        if (_airborne && _fallSpeed >= _f.LandSpeed) Sfx.Play("inis", p.GlobalPosition);
        _airborne = false;
        _fallSpeed = 0;

        float speed = new Vector2(v.X, v.Z).Length();
        if (speed < _f.MinSpeed) { _walked = 0; return; }
        _walked += speed * dt;
        float stride = speed > p.WalkSpeed + 0.5f ? _f.SprintStride : _f.Stride;
        if (_walked < stride) return;
        _walked -= stride;
        Sfx.Play(Surface(p.GlobalPosition), p.GlobalPosition, _f.StepDb);
    }

    /// <summary>Ayagin altindaki zemin: yapi parcasi (arazinin ustunde), surulmus tarla karesi ya da cayir.</summary>
    string Surface(Vector3 at)
    {
        if (at.Y - _d.World.Ground(at).Y > _f.WoodHeight) return "adim_tahta";
        var (x, y) = FarmView.Cell(at);
        return _d.State.Farm.Get(x, y) != null ? "adim_toprak" : "adim_ot";
    }

    void Gidak(float dt)
    {
        var g = _d.Gidak;
        if (g == null || !g.Visible || !g.IsInsideTree()) return;
        _cluckT -= dt;
        if (_cluckT > 0) return;
        _cluckT = (float)GD.RandRange(_f.CluckMin, _f.CluckMax);
        Sfx.Play("gidak_gidaklama", g.GlobalPosition + Vector3.Up * 0.5f, 0f, g.Swollen ? 0.75f : 1f);
    }
}
