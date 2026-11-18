using System;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Capa, Bicme basligi (URETIM 6 M1, ICERIK 6.1): alan etkili. Nisan noktasinin cevresindeki her hedef ayni vurusta:
/// olgun ekin hasat (GDD 8.2), agac her vurusta Odun (devrilmez; devrilme M2), ot ve cali Lif, yabani bugday Bugday
/// (geri gelmez). Ot/cali/bugday verimi dunya akisinin (World.Resources.cs, biomes.json "perHit").
/// </summary>
public partial class Capa
{
    // ---------- Bicme ----------

    /// <summary>Bicme vurusu. Hasat edilen kare sayisini verir.</summary>
    public int Mow(Vector3 aim)
    {
        var muzzle = _d.Player.Muzzle.GlobalPosition;
        if (muzzle.DistanceTo(aim) > _t.MowRange) return 0;
        FeelFire("bicme", muzzle, HeadColors[1]); // supurme: geri tepme, sarsinti

        // Olgun ekin
        int n = 0;
        foreach (var plot in _d.State.Farm.Plots.ToList())
        {
            var c = _d.FarmView.CellCenter(plot.X, plot.Y);
            if (new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > _t.MowRadius) continue;
            var h = _d.State.Farm.Harvest(plot.X, plot.Y, _d.State.Rng);
            if (h == null) continue;
            n++;
            var (yield, stars, seeds) = h.Value;
            FeelHit("bicme_ekin", c + Vector3.Up * 0.4f, Palette.Plant);
            Fx.Text3D(c + Vector3.Up * 1.0f, $"+{yield} Şalgam {new string('★', stars)}", Palette.Warning, 48, 1.2f, 1.3f);
            for (int i = 0; i < Math.Min(yield, 4); i++)
                Fx.FlyTo(c + Vector3.Up * 0.3f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.TurnipBody, 0.12f);
            _d.State.Backpack.Add("turnip", yield, stars); // kalite cantada tasinir (GDD 13: fiyat = taban × kalite)
            _d.State.Backpack.Add("turnip_seed", seeds);
        }
        if (n > 0) _d.OnHarvest(n);

        // Agaclar: her vurusta Odun, agac yerinde kalir (devrilme, kutuk, yenilenme M2).
        // Agaclar dunya akisinda parca parca MultiMesh (World.Decor.cs); esneme agacin kendi parcasinda.
        var trees = _d.World.TreePositions.Where(t => new Vector2(t.X - aim.X, t.Z - aim.Z).Length() < _t.TreeRadius).ToList();
        foreach (var tp in trees)
        {
            int w = _d.Data.Tool.WoodPerChop;
            string wood = _d.World.TreeWood(tp); // dunya2: Koru agaci Sert Odun verir (biomes.json decor "wood")
            _d.State.Backpack.Add(wood, w);
            var at = new Vector3(tp.X, aim.Y, tp.Z);
            FeelHit("bicme_agac", at, Palette.Plant);
            FeelDebris("bicme_odun", at, Palette.Wood);
            float a = F.Source("bicme_agac").Squash * F.Squash.Amount * F.Master.Squash;
            foreach (var treeMesh in _d.World.TreeChunksAt(tp)) // ornek bulunamayan parca sessizce gecer
                Squash.KickInstance(treeMesh, treeMesh.ToLocal(tp), a, F.Squash.Seconds, F.Squash.Wobbles);
            Fx.FlyTo(at, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Wood, 0.12f);
            Events.RaiseGathered(wood, w);
        }

        // Ot, cali ve yabani bugday (World.Resources.cs, dunya akisi): Lif ya da Bugday, geri gelmez; vurus basina biomes.json "perHit".
        foreach (var tuft in _d.World.Tufts.Where(t => t.Left > 0 && new Vector2(t.Position.X - aim.X, t.Position.Z - aim.Z).Length() < _t.MowRadius).ToList())
        {
            var visual = IsInstanceValid(tuft.Visual) ? tuft.Visual : null;
            int got = _d.World.TakeTuft(tuft, tuft.PerHit);
            if (got <= 0) continue;
            _d.State.Backpack.Add(tuft.Item, got);
            var color = tuft.Item == "wheat" ? Palette.Warning : Palette.Plant;
            FeelHit("bicme_tutam", tuft.Position + Vector3.Up * 0.4f, color, squash: tuft.Left > 0 ? visual : null);
            Fx.FlyTo(tuft.Position + Vector3.Up * 0.4f, () => _d.Player.BackpackPoint.GlobalPosition, color, 0.12f);
            Events.RaiseGathered(tuft.Item, got);
        }
        return n;
    }
}
