using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Capa, Sulama basligi: nisan noktasi cevresindeki kareleri islatir. Su deposu (dunya akisi; kural Sim'de WaterTank,
/// sayilar heads.json "sulama"): islanan her kare depodan su harcar, bos depo sulamaz; gol yuzeyine nisan alininca dolar.
/// </summary>
public partial class Capa
{
    readonly HashSet<(int, int)> _wateredPlanted = new();
    ulong _lastWaterUsec;

    // ---------- Sulama ----------

    public void WaterAt(Vector3 aim)
    {
        float dt = WaterCallSeconds();
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.WaterRange) return;
        var tank = _d.State.Tank;
        if (_d.Oyuk?.WaterAt(aim, tank) == true) return; // OyukSystem: Oyuk icinde su deliklere gider (oyuk, M3)
        // Gol: nisan noktasi gol tabanina duser (su yuzeyi carpismasiz), World.IsWater onu taniyor.
        if (_d.World.IsWater(aim)) { FillTank(tank, aim, dt); return; }
        if (!tank.CanWater) { _d.Hud.Hint("Su deposu boş: göle nişan al, dolsun", 0.5f); return; }
        _d.Azman?.Douse(aim, dt); // Azman Kizgin yangin lekeleri sonmeye baslar (ata4, M4)
        _d.Net?.ForwardWater(aim); // ortak: istemcide host'un tarlasi da islanir (depo kisisel)
        var farm = _d.State.Farm;
        bool any = false;
        foreach (var plot in farm.Plots)
        {
            var c = _d.FarmView.CellCenter(plot.X, plot.Y);
            if (new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > 1.1f) continue;
            if (plot.Moisture > 92f) continue;
            if (!tank.TryWaterCell()) break;
            farm.Water(plot.X, plot.Y);
            any = true;
            if (plot.Planted && _wateredPlanted.Add((plot.X, plot.Y)))
                _d.State.Scenario.SetProgress("water", _wateredPlanted.Count);
        }
        if (!any) return;
        _d.Hud.Hint(TankText(tank), 0.5f);
        if (GD.Randf() < 0.35f) Sfx.Play("water", aim, -8f);
    }

    void FillTank(WaterTank tank, Vector3 aim, float dt)
    {
        float got = tank.Fill(dt);
        _d.Hud.Hint(tank.Full ? $"Su deposu dolu: {tank.Capacity:0} L" : TankText(tank), 0.5f);
        if (got <= 0) return;
        if (GD.Randf() < 0.3f) Sfx.Play("water", aim, -14f);
        Fx.Burst(aim + Vector3.Up * 0.15f, Palette.SkyNoon, 5, 2.5f, 0.05f, 0.35f);
        Vfx.Play("farm_water", aim + Vector3.Up * 0.2f);
    }

    static string TankText(WaterTank t) => $"Su deposu: {t.Liters:0} / {t.Capacity:0} L";

    /// <summary>
    /// Iki WaterAt cagrisi arasindaki oyun suresi (Capa.cs bunu kisa araliklarla cagirir). Uzun ara (tetik birakilmis)
    /// bir cagri araligindan fazla sayilmasin diye kirpilir.
    /// </summary>
    float WaterCallSeconds()
    {
        ulong now = Time.GetTicksUsec();
        double real = _lastWaterUsec == 0 ? 0 : (now - _lastWaterUsec) / 1e6;
        _lastWaterUsec = now;
        return (float)(Mathf.Min(real, 0.25) * Engine.TimeScale);
    }
}
