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

    /// <summary>Silahin arayacagi hedefler: Ata hedefleri ve savas kaydindaki IAtaTarget'lar (Azman ...).</summary>
    IEnumerable<IAtaTarget> WeaponTargets()
    {
        if (Atalar != null) foreach (var t in Atalar.Targets) yield return t;
        if (Savas != null) foreach (var t in Savas.Targets) if (t is IAtaTarget a) yield return a;
    }

    /// <summary>Bu kure icindeki canli Ata/Azman hedefleri (Tirpan hilali).</summary>
    public IEnumerable<IAtaTarget> AtaTargetsNear(Vector3 at, float reach) =>
        WeaponTargets().Where(t => t.Alive && t.Center.DistanceTo(at) <= t.Radius + reach).ToArray();

    /// <summary>Bu karede gecilen dogru parcasina ilk degen Ata/Azman hedefi (Civi). Zayif nokta govdeden once.</summary>
    public IAtaTarget AtaSegmentHit(Vector3 from, Vector3 to, float reach)
    {
        var seg = to - from;
        IAtaTarget best = null;
        float bestT = float.MaxValue;
        foreach (var t in WeaponTargets())
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

/// <summary>
/// Capa'nin mermi satirlarinin (Capa.Weapons.cs) aradigi patron hedefi: Ata govdesi, kovan, yardimci ve Azman.
/// Savas sozlesmesine gecti (M3): isabet ICombatTarget.TakeHit ile, hasar Combat.Apply'dan.
/// </summary>
public interface IAtaTarget : ICombatTarget { }
