using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Capa, Sulama basliginin itmesi (ICERIK 6.1 "kucuk dusmani iter", URETIM 6 M1): puskurtme cizgisine yakin, olcegi
/// kucuk (kabarmamis) dusman su yonunde itilir. Hasar yok. Depo, dolum ve su yuzeyi Capa.Sulama.cs'de (dunya akisi).
/// </summary>
public partial class Capa
{
    /// <summary>Bir sulama tikinde itilen dusman sayisi.</summary>
    public int PushSmallEnemies(Vector3 muzzle, Vector3 aim)
    {
        var wp = _t.WaterPush;
        var seg = aim - muzzle;
        float len = seg.Length();
        if (len < 0.01f || len > _t.WaterRange || wp.Force <= 0f) return 0;
        int n = 0;
        foreach (var z in Zipzip.All.ToArray())
        {
            if (z.Dead || z.Scale0 > wp.MaxScale) continue;
            var c = z.Center;
            float t = Mathf.Clamp((c - muzzle).Dot(seg) / (len * len), 0f, 1f);
            if ((muzzle + seg * t).DistanceTo(c) > wp.Width + z.Radius) continue;
            var dir = (c - muzzle) with { Y = 0 };
            z.Push(dir, wp.Force);
            FeelHit("sulama_itme", c, new Color("BFE6F5"));
            n++;
        }
        return n;
    }
}
