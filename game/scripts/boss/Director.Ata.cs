using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Director: Atalar (boss akisi, GDD §18.1). AtaSystem arenalari, Ata Taslarini, dovusu ve Ata cubugunu kurar.
/// Silah mermileri (Capa.Weapons.cs) Ata hedeflerini buradan sorar; hedef listesi AtaSystem orneginde (statik yok, TEKNIK §9).
/// </summary>
public partial class Director
{
    public AtaSystem Atalar { get; private set; }

    void InitAtalar()
    {
        Atalar = new AtaSystem(this) { Name = "Atalar" };
        AddChild(Atalar);
    }

    static readonly IAtaTarget[] NoTargets = System.Array.Empty<IAtaTarget>();

    /// <summary>Bu kure icindeki canli Ata hedefleri (Tirpan hilali).</summary>
    public IEnumerable<IAtaTarget> AtaTargetsNear(Vector3 at, float reach)
    {
        if (Atalar == null || Atalar.Targets.Count == 0) return NoTargets;
        return Atalar.Targets.Where(t => t.Alive && t.Center.DistanceTo(at) <= t.Radius + reach).ToArray();
    }

    /// <summary>Bu karede gecilen dogru parcasina ilk degen Ata hedefi (Civi). Zayif nokta govdeden once.</summary>
    public IAtaTarget AtaSegmentHit(Vector3 from, Vector3 to, float reach)
    {
        if (Atalar == null || Atalar.Targets.Count == 0) return null;
        var seg = to - from;
        IAtaTarget best = null;
        float bestT = float.MaxValue;
        foreach (var t in Atalar.Targets)
        {
            if (!t.Alive) continue;
            float k = Mathf.Clamp((t.Center - from).Dot(seg) / Mathf.Max(0.0001f, seg.LengthSquared()), 0, 1);
            if ((from + seg * k).DistanceTo(t.Center) > t.Radius + reach) continue;
            float score = k - (t.IsWeakPoint ? 0.5f : 0f);
            if (score < bestT) { bestT = score; best = t; }
        }
        return best;
    }
}

/// <summary>Silahla vurulabilen Ata parcasi: govde, zayif nokta (kovan) ya da yardimci yaratik.</summary>
public interface IAtaTarget
{
    Vector3 Center { get; }
    float Radius { get; }
    bool Alive { get; }
    bool IsWeakPoint { get; }
    /// <summary>Silah isabeti: hasar Night.RollHit'ten (gece) ya da silahin hasari (gunduz).</summary>
    void TakeWeapon(float dmg, bool crit, Vector3 dir, float knock);
    /// <summary>Yanik (Kor karti, gece): Koca Pofuduk'un yununu yer.</summary>
    void Burn(float seconds) { }
}
