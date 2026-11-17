using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Capa, Cekim basligi: kabarik urunu isinla tasima, Kargo Rampasi'na birakma.</summary>
public partial class Capa
{
    public SwollenCrop Held { get; private set; }

    // ---------- Cekim ----------

    public bool TryGrab(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.CarryRange + 2f) return false;
        // Yerde duran kabarik urun
        var loose = GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>()
            .Where(s => !s.Sold && s.GlobalPosition.DistanceTo(aim) < s.Radius + 1.2f)
            .OrderBy(s => s.GlobalPosition.DistanceTo(aim)).FirstOrDefault();
        if (loose != null) { Grab(loose); return true; }
        // Tarladaki kabarik urun
        var view = _d.FarmView.Views.Where(v => v.Plot.Swollen != null && v.Plot.Planted)
            .OrderBy(v => v.GlobalPosition.DistanceTo(aim)).FirstOrDefault();
        if (view != null && view.GlobalPosition.DistanceTo(aim) < 1.2f + view.CropScale * 0.4f)
        {
            var sc = view.Lift();
            if (sc != null) { Grab(sc); Fx.Burst(view.GlobalPosition, Palette.Soil, 18, 4f, 0.1f, 0.6f); return true; }
        }
        return false;
    }

    void Grab(SwollenCrop s)
    {
        Held = s;
        s.Held = true;
        Sfx.Play("place", s.GlobalPosition, -6f);
    }

    public bool OverPad(Vector3 p)
    {
        var pad = _d.Pod.PadCenter;
        return new Vector2(p.X - pad.X, p.Z - pad.Z).Length() < 2.4f;
    }

    public void Drop()
    {
        if (Held == null) return;
        var s = Held;
        Held = null;
        s.Held = false;
        // Rampanin ustunde ya da oyuncu rampanin dibindeyken birakilan yuk kargoya gider.
        var pad = _d.Pod.PadCenter;
        bool nearPad = new Vector2(_d.Player.GlobalPosition.X - pad.X, _d.Player.GlobalPosition.Z - pad.Z).Length() < 4.5f;
        if (_d.Pod.IsRamp && (OverPad(s.GlobalPosition) || nearPad)) { _d.SellCrop(s); return; }
        var g = _d.World.Ground(s.GlobalPosition);
        var tw = s.CreateTween();
        tw.TweenProperty(s, "global_position", g + Vector3.Up * s.Radius * 0.6f, 0.35f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        Sfx.Play("place", g, -4f);
    }
}
