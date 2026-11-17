using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>Pano: baslik carki (GDD 4: "sayi tuslari ya da cark").</summary>
public partial class Hud
{
    Control _wheel;
    readonly List<PanelContainer> _wheelSlots = new();
    Vector2 _wheelDir;
    int _wheelSel = -1;
    public bool WheelOpen => _wheel != null && _wheel.Visible;

    // ---------- Baslik carki ----------

    public void OpenWheel(int current)
    {
        _wheel.Visible = true;
        _wheelDir = Vector2.Zero;
        _wheelSel = current;
        PaintWheel();
    }

    /// <summary>Fare hareketi carkta yon secer (fare kilitli oldugu icin goreli hareket birikir).</summary>
    public void WheelMotion(Vector2 rel)
    {
        _wheelDir = (_wheelDir + rel * 0.02f).LimitLength(1f);
        if (_wheelDir.Length() > 0.35f)
        {
            int n = _wheelSlots.Count;
            float a = Mathf.PosMod(Mathf.Atan2(_wheelDir.X, -_wheelDir.Y), Mathf.Tau);
            _wheelSel = Mathf.PosMod((int)Mathf.Round(a / (Mathf.Tau / n)), n);
            PaintWheel();
        }
    }

    public int CloseWheel() { _wheel.Visible = false; return _wheelSel; }

    void PaintWheel()
    {
        for (int i = 0; i < _wheelSlots.Count; i++)
        {
            bool on = i == _wheelSel;
            _wheelSlots[i].AddThemeStyleboxOverride("panel", Box(on ? new Color(Capa.HeadColors[i].Darkened(0.3f), 0.95f) : new Color(Palette.NightDeep, 0.8f), 14, on ? Palette.Warning : new Color(Palette.OffWhite, 0.3f), on ? 3 : 2));
        }
    }

    void LayoutWheel(Vector2 vp)
    {
        int n = _wheelSlots.Count;
        var c = vp / 2f;
        float r = Mathf.Min(vp.X, vp.Y) * 0.24f;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n;
            var p = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * r;
            _wheelSlots[i].Position = p - _wheelSlots[i].Size / 2f;
        }
    }
}
