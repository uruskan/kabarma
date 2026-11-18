namespace Kabarma.Sim;

// Durumlar (GDD §16.1): Yanik, Donma (ve yiginla Kilit), Sersem (Sok), Zehir. Kabarik govdedeki Swell sayisidir, Itme
// isabetin sonucudur (CombatResult.Knock). Sayilar combat.json. Butun durum duz ve serilestirilebilir (TEKNIK §9).

public enum StatusKind { Burn, Freeze, Lock, Stun, Poison }

/// <summary>Bir hedefin uzerindeki bir durum.</summary>
public sealed class StatusEffect
{
    public StatusKind Kind { get; set; }
    /// <summary>Kalan sure (sn).</summary>
    public float T { get; set; }
    /// <summary>Saniyedeki hasar (Yanik, Zehir).</summary>
    public float Dps { get; set; }
    /// <summary>Donma yigini.</summary>
    public int Stacks { get; set; }
    /// <summary>Surekli hasar ve sicrama sayaclari.</summary>
    public float TickT { get; set; }
    public float SpreadT { get; set; }
}

/// <summary>Durum kurallari: ekleme, sorgu. Zamanla islenme Combat.Tick'te (hasar govdeye orada islenir).</summary>
public static class Status
{
    public static StatusEffect? Get(CombatBody b, StatusKind k)
    {
        foreach (var s in b.Statuses) if (s.Kind == k) return s;
        return null;
    }

    public static bool Has(CombatBody b, StatusKind k) => Get(b, k) != null;

    static StatusEffect Ensure(CombatBody b, StatusKind k)
    {
        var s = Get(b, k);
        if (s == null) { s = new StatusEffect { Kind = k }; b.Statuses.Add(s); }
        return s;
    }

    /// <summary>Yanik: sure yenilenir, saniyelik hasar buyugu kalir.</summary>
    public static void AddBurn(CombatFile d, CombatBody b, float dps, float seconds = 0)
    {
        if (b.Dead || dps <= 0) return;
        var s = Ensure(b, StatusKind.Burn);
        s.T = Math.Max(s.T, seconds > 0 ? seconds : d.Burn.Seconds);
        s.Dps = Math.Max(s.Dps, Math.Max(d.Burn.MinDps, dps));
    }

    /// <summary>Donma: yavaslatir; LockStacks yiginda Kilit (hareket ve saldiri yok). Kilit verdiyse true.</summary>
    public static bool AddFreeze(CombatFile d, CombatBody b, float seconds = 0)
    {
        if (b.Dead) return false;
        if (Has(b, StatusKind.Lock)) return false; // kilitliyken yigin birikmez
        var s = Ensure(b, StatusKind.Freeze);
        s.T = Math.Max(s.T, seconds > 0 ? seconds : d.Freeze.Seconds);
        s.Stacks++;
        if (d.Freeze.LockStacks <= 0 || s.Stacks < d.Freeze.LockStacks) return false;
        b.Statuses.Remove(s);
        Ensure(b, StatusKind.Lock).T = d.Freeze.LockSeconds;
        return true;
    }

    /// <summary>Sersemleme: hareket ve saldiri yok. Sure buyugu kalir.</summary>
    public static void AddStun(CombatBody b, float seconds)
    {
        if (b.Dead || seconds <= 0) return;
        var s = Ensure(b, StatusKind.Stun);
        s.T = Math.Max(s.T, seconds);
    }

    /// <summary>Zehir: saniyelik hasar yigilir (en cok MaxDps), sure yenilenir; iyilesmeyi keser.</summary>
    public static void AddPoison(CombatFile d, CombatBody b, float dps, float seconds = 0)
    {
        if (b.Dead || dps <= 0) return;
        var s = Ensure(b, StatusKind.Poison);
        s.T = Math.Max(s.T, seconds > 0 ? seconds : d.Poison.Seconds);
        s.Dps = Math.Min(d.Poison.MaxDps, s.Dps + Math.Max(d.Poison.MinDps, dps));
    }

    /// <summary>Hareket hizi carpani: Kilit ve Sersem 0, Donma 1 − Slow.</summary>
    public static float SpeedMult(CombatFile d, CombatBody b)
    {
        if (Has(b, StatusKind.Lock) || Has(b, StatusKind.Stun)) return 0f;
        return Has(b, StatusKind.Freeze) ? 1f - d.Freeze.Slow : 1f;
    }

    /// <summary>Saldirabilir ve yetenek kullanabilir mi (Kilit ve Sersem yok).</summary>
    public static bool CanAct(CombatBody b) => !Has(b, StatusKind.Lock) && !Has(b, StatusKind.Stun);

    /// <summary>GDD §16.1: zehir iyilesmeyi keser.</summary>
    public static bool HealBlocked(CombatBody b) => Has(b, StatusKind.Poison);

    public static void Clear(CombatBody b) { b.Statuses.Clear(); b.Swell = 0; }
}
