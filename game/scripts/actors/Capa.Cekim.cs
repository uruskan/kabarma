using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Cekim ile tutulabilen yuk nesnesi (GDD 5): kabarik urun; ileride kargo kasasi, hayvan. Yeni bir tur icin dugum bu
/// arayuzu uygular ve "cekim" grubuna girer.
/// </summary>
public interface ICarryable
{
    Node3D Body { get; }
    float Kg { get; }
    float Radius { get; }
    bool Held { get; set; }
    /// <summary>Satildi ya da yok oluyor: tutulamaz.</summary>
    bool Gone { get; }
}

/// <summary>
/// Capa, Cekim basligi (URETIM 6 M1, ICERIK 6.1): tutar, tasir, firlatir; carry.maxKg (80 kg) ustu tutulamaz.
/// Tetik: bos elde basis tutar. Yuk elde: kisa basis birakir (rampanin ustunde ya da dibinde kargoya yukler),
/// basili tutup birakmak yukleyip firlatir. Arkadas tutma M2.
/// </summary>
public partial class Capa
{
    public ICarryable Held { get; private set; }
    Tween _dropTween;
    bool _charging;
    float _chargeT, _missBeamT;

    /// <summary>Cekim karesi (Capa.Process). Isin gorunuyorsa true.</summary>
    bool TickCekim(float dt, bool fire, bool firePressed, Vector3 aim, Vector3 muzzle)
    {
        var c = _t.Carry;
        var p = _d.Player;
        if (Held != null && (Held.Gone || !IsInstanceValid(Held.Body))) { Held = null; _charging = false; }

        if (firePressed)
        {
            if (Held == null)
            {
                if (!TryGrab(aim)) _missBeamT = c.MissBeamSeconds; // bos cekim: kisa isin (tepki)
            }
            else
            {
                _charging = true; _chargeT = 0f;
                FeelSquash("cekim_birak", Held.Body);
            }
            _reacted = true;
        }
        else if (!fire && _charging)
        {
            // Birakildi: kisa basis birakir, uzun basis firlatir.
            _charging = false;
            if (_chargeT < c.TapSeconds) Drop();
            else Throw(p.AimDir, muzzle);
        }
        if (_charging && fire) _chargeT += dt;

        if (Held != null)
        {
            float k = _charging ? Mathf.Clamp(_chargeT / Mathf.Max(0.01f, c.ChargeSeconds), 0f, 1f) : 0f;
            var holdAt = muzzle + p.AimDir * (c.HoldDistance + Held.Radius - k * c.ChargePullBack) + Vector3.Up * (c.HoldLift + Held.Radius * c.HoldLiftPerRadius);
            var floor = _d.World.HeightAt(holdAt.X, holdAt.Z) + Held.Radius;
            if (holdAt.Y < floor) holdAt.Y = floor;
            Held.Body.GlobalPosition = Held.Body.GlobalPosition.Lerp(holdAt, Mathf.Min(1f, dt * c.Follow));
            ShowBeam(muzzle, Held.Body.GlobalPosition, HeadColors[(int)Head.Cekim], 0.5f + k);
            _d.Hud.Hint(AtRamp(Held.Body.GlobalPosition) ? "Bırak: kargoya yükle (tıkla) · basılı tut: fırlat" : "Kargo Rampası'na taşı · basılı tut, bırak: fırlat");
            return true;
        }
        if (_missBeamT > 0f)
        {
            _missBeamT -= dt;
            if (muzzle.DistanceTo(aim) <= _t.CarryRange + c.GrabReach) ShowBeam(muzzle, aim, HeadColors[(int)Head.Cekim], 0.15f);
            return true;
        }
        return false;
    }

    public bool TryGrab(Vector3 aim)
    {
        var c = _t.Carry;
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.CarryRange + c.GrabReach) return false;
        // Yerde duran yuk nesnesi (kabarik urun ya da "cekim" grubundaki herhangi bir tasinabilir)
        var loose = GetTree().GetNodesInGroup("swollen").OfType<ICarryable>()
            .Concat(GetTree().GetNodesInGroup("cekim").OfType<ICarryable>())
            .Where(s => !s.Gone && !s.Held && s.Body.GlobalPosition.DistanceTo(aim) < s.Radius + c.GrabRadius)
            .OrderBy(s => s.Body.GlobalPosition.DistanceTo(aim)).FirstOrDefault();
        if (loose != null) return TooHeavy(loose.Kg, loose.Body.GlobalPosition) ? false : Grab(loose, false);
        // Tarladaki kabarik urun: once agirlik, sonra sokulur.
        var view = _d.FarmView.Views.Where(v => v.Plot.Swollen != null && v.Plot.Planted)
            .OrderBy(v => v.GlobalPosition.DistanceTo(aim)).FirstOrDefault();
        if (view != null && view.GlobalPosition.DistanceTo(aim) < c.GrabRadius + view.CropScale * c.GrabPerScale)
        {
            if (TooHeavy(view.Plot.Swollen.Kg, view.GlobalPosition)) return false;
            var sc = view.Lift();
            if (sc != null) { FeelDebris("cekim_tut", view.GlobalPosition, Palette.Soil); return Grab(sc, true); }
        }
        return false;
    }

    bool TooHeavy(float kg, Vector3 at)
    {
        if (kg <= _t.Carry.MaxKg) return false;
        _d.Hud.Hint($"Çok ağır: {kg:0} kg (Çekim en çok {_t.Carry.MaxKg:0} kg taşır)", 1.5f);
        FeelEmpty("cekim_agir", at);
        _missBeamT = _t.Carry.MissBeamSeconds;
        return true;
    }

    bool Grab(ICarryable s, bool uprooted)
    {
        Held = s;
        s.Held = true;
        _charging = false;
        if (_dropTween != null && _dropTween.IsValid()) _dropTween.Kill();
        s.Body.GetNodeOrNull<ThrowMotion>(ThrowMotion.NodeName)?.Cancel();
        FeelFire("cekim_tut", s.Body.GlobalPosition, HeadColors[(int)Head.Cekim]);
        FeelSquash("cekim_tut", s.Body);
        return true;
    }

    public bool OverPad(Vector3 p)
    {
        var pad = _d.Pod.PadCenter;
        return new Vector2(p.X - pad.X, p.Z - pad.Z).Length() < _t.Carry.RampPadRadius;
    }

    /// <summary>Yuk buraya birakilirsa kargoya gider: pedin ustunde ya da oyuncu rampanin dibindeyken.</summary>
    bool AtRamp(Vector3 load)
    {
        if (!_d.Pod.IsRamp) return false;
        var pad = _d.Pod.PadCenter;
        var pp = _d.Player.GlobalPosition;
        return OverPad(load) || new Vector2(pp.X - pad.X, pp.Z - pad.Z).Length() < _t.Carry.RampReach;
    }

    public void Drop()
    {
        if (Held == null) return;
        var h = Held;
        Held = null;
        h.Held = false;
        _charging = false;
        var body = h.Body;
        if (h is SwollenCrop s && AtRamp(s.GlobalPosition)) { Squash.Stop(s); _d.SellCrop(s); return; }
        var g = _d.World.Ground(body.GlobalPosition);
        _dropTween = body.CreateTween();
        _dropTween.TweenProperty(body, "global_position", g + Vector3.Up * h.Radius * 0.6f, _t.Carry.DropSeconds).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        FeelHit("cekim_birak", g, Palette.Soil, squash: body);
    }

    void Throw(Vector3 aimDir, Vector3 muzzle)
    {
        if (Held == null) return;
        var c = _t.Carry;
        var h = Held;
        Held = null;
        h.Held = false;
        float k = Mathf.Clamp(_chargeT / Mathf.Max(0.01f, c.ChargeSeconds), 0f, 1f);
        var v = (aimDir + Vector3.Up * c.ThrowUp).Normalized() * Mathf.Lerp(c.ThrowMinSpeed, c.ThrowMaxSpeed, k);
        ThrowMotion.Start(this, h, v);
        FeelFire("cekim_firlat", muzzle, HeadColors[(int)Head.Cekim]);
    }

    /// <summary>Firlatilan yuk yere degdi (ThrowMotion). Rampanin pedine duserse kargoya gider.</summary>
    internal bool Landed(ICarryable h, Vector3 ground, bool first)
    {
        if (first) FeelHit("cekim_dus", ground, Palette.Soil, squash: h.Body);
        if (h is SwollenCrop s && _d.Pod.IsRamp && OverPad(s.GlobalPosition)) { Squash.Stop(s); _d.SellCrop(s); return true; }
        return false;
    }

    internal World World => _d.World;
    internal Kabarma.Sim.CarryDef CarryDef => _t.Carry;
}

/// <summary>Firlatilan yuk: yercekimiyle ucar, zemine carpinca bir kez seker ve durur (carry.gravity, carry.bounce).</summary>
public partial class ThrowMotion : Node
{
    public const string NodeName = "ThrowMotion";
    Capa _capa;
    ICarryable _h;
    Vector3 _v;
    bool _landed;

    public static void Start(Capa capa, ICarryable h, Vector3 velocity)
    {
        h.Body.GetNodeOrNull<ThrowMotion>(NodeName)?.Cancel();
        h.Body.AddChild(new ThrowMotion { Name = NodeName, _capa = capa, _h = h, _v = velocity });
    }

    public void Cancel() { Name = NodeName + "_bitti"; QueueFree(); }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var b = _h.Body;
        if (!IsInstanceValid(b) || _h.Held || _h.Gone) { Cancel(); return; }
        var carry = _capa.CarryDef;
        _v += Vector3.Down * carry.Gravity * dt;
        var pos = b.GlobalPosition + _v * dt;
        float rest = _capa.World.HeightAt(pos.X, pos.Z) + _h.Radius * 0.6f;
        if (pos.Y <= rest && _v.Y < 0f)
        {
            pos.Y = rest;
            bool sold = _capa.Landed(_h, pos - Vector3.Up * _h.Radius * 0.6f, !_landed);
            _landed = true;
            if (sold) { Cancel(); return; }
            _v = new Vector3(_v.X * carry.Bounce, -_v.Y * carry.Bounce, _v.Z * carry.Bounce);
            if (_v.Length() < 1f) { b.GlobalPosition = pos; Cancel(); return; }
        }
        b.GlobalPosition = pos;
    }
}
