namespace Kabarma.Sim;

// Ortak hasar ve saglik (GDD §16.1). Sozlesme: her hasar alan sey (yaratik, Ata, Azman, Kabaran, oyuncu, kule, hayvan)
// kendi duz durumuna bir CombatBody gomer ve hasari Combat.Apply ile, zamani Combat.Tick ile isler. Govde kendi
// sahibinin kayit ve ag bolumuyle tasinir (yeni GameState ozelligi gerekmez). Sim komsu bilmez: alan etkileri (yanik
// sicramasi, sok zinciri, Kabarik patlamasi) CombatArea olarak doner; Godot katmani (game/scripts/combat/) konuma gore
// hedef bulur ve Combat.ApplyArea'yi cagirir. Sayilar combat.json.

public enum DamageType { Darbe, Kesme, Ates, Buz, Sok, Zehir, Maya }

/// <summary>Takim: dost atesi kapaliyken ayni takim birbirine hasar vermez (itme yine islenir).</summary>
public enum CombatTeam { Neutral, Player, Wild, Holding }

/// <summary>Isabetin kendi turundan bagimsiz ekledigi durumlar (kart sansi: Yanik, Donma ...).</summary>
[Flags]
public enum StatusProc { None = 0, Burn = 1, Freeze = 2, Shock = 4, Poison = 8 }

/// <summary>Bir hedefin saglik ve durum govdesi. Sahibi kendi durumuna gomer; kayit ve ag sahibinin bolumuyle.</summary>
public sealed class CombatBody
{
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    /// <summary>Zirh puani: azaltma = zirh / (zirh + armorK).</summary>
    public float Armor { get; set; }
    /// <summary>Kutle: itme hizi = itme / kutle (GDD §16.1 Itme).</summary>
    public float Mass { get; set; } = 1f;
    /// <summary>combat.json profiles anahtari (zayiflik ve direnc).</summary>
    public string Profile { get; set; } = "";
    public CombatTeam Team { get; set; }
    /// <summary>Kabarik: eklenen boy, 0..burstAt (1 = %200 boy; gorunus olcegi 1 + Swell).</summary>
    public float Swell { get; set; }
    /// <summary>Hasar almama suresi (inis, asama gecisi).</summary>
    public float ShieldT { get; set; }
    public List<StatusEffect> Statuses { get; set; } = new();

    public bool Dead => Hp <= 0;
    public float Fraction => MaxHp > 0 ? Math.Clamp(Hp / MaxHp, 0f, 1f) : 0f;

    public static CombatBody Of(float hp, CombatTeam team, string profile = "", float armor = 0, float mass = 1f) =>
        new() { Hp = hp, MaxHp = hp, Team = team, Profile = profile, Armor = armor, Mass = mass };
}

/// <summary>
/// Bir isabet. Amount onceden atilmis hasardir (kritik ve zayif nokta carpanlari Night.RollHit ya da Combat.Raw'da
/// uygulanmis); Crit ve Weak yalniz his icindir. Knock itme kuvvetidir (kutleye bolunur).
/// </summary>
public readonly record struct CombatHit(float Amount, DamageType Type, CombatTeam Team)
{
    public bool Crit { get; init; }
    public bool Weak { get; init; }
    public float Knock { get; init; }
    public StatusProc Procs { get; init; }
    public bool IgnoreArmor { get; init; }
    /// <summary>Sok zincirinden gelen isabet yeniden sicramaz.</summary>
    public bool Chained { get; init; }
}

public enum AreaKind { BurnSpread, ShockChain, SwellBurst }

/// <summary>
/// Govdenin cevresine yayilan etki: Godot katmani govdenin konumunda Radius icindeki en cok MaxTargets hedefe uygular
/// (kaynak govdenin kendisi haric). Team: zincir ve patlamada vuranin takimi; yanik sicramasinda yananin takimi.
/// </summary>
public readonly record struct CombatArea(AreaKind Kind, float Radius, int MaxTargets, float Amount, DamageType Type, CombatTeam Team);

public struct CombatResult
{
    /// <summary>Govdeye islenen hasar (azaltmadan sonra).</summary>
    public float Damage;
    /// <summary>Kalkan ya da dost atesi yuzunden hasar islenmedi.</summary>
    public bool Blocked;
    public bool Killed;
    /// <summary>Itme hizi (m/s); yon isabetin geldigi yondur (Godot katmani).</summary>
    public float Knock;
    /// <summary>Kabarik %200'e vardi ve patladi.</summary>
    public bool Burst;
    /// <summary>Donma yigini kilide vardi.</summary>
    public bool Locked;
}

public static class Combat
{
    static readonly string[] Ids = { "darbe", "kesme", "ates", "buz", "sok", "zehir", "maya" };

    public static string Id(DamageType t) => Ids[(int)t];

    public static DamageType Parse(string id)
    {
        int i = Array.IndexOf(Ids, id);
        if (i < 0) throw new InvalidDataException($"Bilinmeyen hasar turu: '{id}'");
        return (DamageType)i;
    }

    public static bool TryParse(string id, out DamageType t)
    {
        int i = Array.IndexOf(Ids, id);
        t = i < 0 ? DamageType.Darbe : (DamageType)i;
        return i >= 0;
    }

    /// <summary>GDD §16.1: zirhin azaltma payi.</summary>
    public static float ArmorReduction(CombatFile d, float armor) => armor <= 0 ? 0f : armor / (armor + d.ArmorK);

    /// <summary>Azaltma payindan zirh puani (payi 0..1 yazilmis eski veriler icin; 0.95 ustu kirpilir).</summary>
    public static float ArmorPointsFor(CombatFile d, float reduction)
    {
        float r = Math.Clamp(reduction, 0f, 0.95f);
        return r <= 0 ? 0f : d.ArmorK * r / (1f - r);
    }

    /// <summary>Kritik ve zayif nokta: onceden atilmis hasar (gece disi; gecede Night.RollHit ayni isi yapar).</summary>
    public static float Raw(float baseDamage, bool crit, float critMult, bool weak, float weakMult) =>
        baseDamage * (crit ? critMult : 1f) * (weak ? weakMult : 1f);

    /// <summary>
    /// Azaltma, sabit sira: tur carpani (profil) → zirh → kalkan ve dost atesi. Durum eklemez, govdeyi degistirmez.
    /// </summary>
    public static float Mitigate(CombatFile d, CombatBody b, in CombatHit h)
    {
        if (b.Dead || h.Amount <= 0) return 0;
        if (IsFriendly(d, b, h) || b.ShieldT > 0) return 0;
        float dmg = h.Amount * d.Mult(b.Profile, h.Type);
        if (!h.IgnoreArmor) dmg *= 1f - ArmorReduction(d, b.Armor);
        return Math.Max(0, dmg);
    }

    static bool IsFriendly(CombatFile d, CombatBody b, in CombatHit h) =>
        !d.FriendlyFire && h.Team != CombatTeam.Neutral && h.Team == b.Team;

    /// <summary>Itme hizi: kuvvet / kutle (en az minMass), en cok maxSpeed. Dost atesi kapali da olsa islenir.</summary>
    public static float Knock(CombatFile d, CombatBody b, float force) =>
        force <= 0 ? 0 : Math.Min(d.Knock.MaxSpeed, force / Math.Max(d.Knock.MinMass, b.Mass));

    /// <summary>
    /// Isabeti govdeye isler: azaltma, can, turun ve isabetin durumlari, Kabarik. Alan etkileri areas'a eklenir.
    /// </summary>
    public static CombatResult Apply(CombatFile d, CombatBody b, in CombatHit h, List<CombatArea>? areas = null)
    {
        var r = new CombatResult();
        if (b.Dead) return r;
        r.Knock = Knock(d, b, h.Knock);
        if (h.Amount <= 0) return r;
        if (IsFriendly(d, b, h) || b.ShieldT > 0) { r.Blocked = true; return r; }

        float dmg = Mitigate(d, b, h);
        b.Hp = Math.Max(0, b.Hp - dmg);
        r.Damage = dmg;
        if (b.Dead) { r.Killed = true; Status.Clear(b); return r; }

        var procs = h.Procs | ProcOf(h.Type);
        if ((procs & StatusProc.Burn) != 0) Status.AddBurn(d, b, dmg * d.Burn.DpsOfHit);
        if ((procs & StatusProc.Freeze) != 0) r.Locked = Status.AddFreeze(d, b);
        if ((procs & StatusProc.Poison) != 0) Status.AddPoison(d, b, dmg * d.Poison.DpsOfHit);
        if ((procs & StatusProc.Shock) != 0)
        {
            Status.AddStun(b, d.Shock.StunSeconds);
            if (!h.Chained && d.Shock.ChainTargets > 0)
                areas?.Add(new CombatArea(AreaKind.ShockChain, d.Shock.ChainRadius, d.Shock.ChainTargets, dmg * d.Shock.ChainFalloff, h.Type, h.Team));
        }
        if (h.Type == DamageType.Maya && b.MaxHp > 0)
        {
            b.Swell += dmg / b.MaxHp * d.Swell.PerMaxHp;
            if (b.Swell >= d.Swell.BurstAt)
            {
                b.Swell = 0;
                r.Burst = true;
                areas?.Add(new CombatArea(AreaKind.SwellBurst, d.Swell.BurstRadius, int.MaxValue, b.MaxHp * d.Swell.BurstOfMaxHp, DamageType.Darbe, h.Team));
                float self = b.MaxHp * d.Swell.SelfOfMaxHp;
                b.Hp = Math.Max(0, b.Hp - self);
                r.Damage += self;
                if (b.Dead) { r.Killed = true; Status.Clear(b); }
            }
        }
        return r;
    }

    static StatusProc ProcOf(DamageType t) => t switch
    {
        DamageType.Ates => StatusProc.Burn,
        DamageType.Buz => StatusProc.Freeze,
        DamageType.Sok => StatusProc.Shock,
        DamageType.Zehir => StatusProc.Poison,
        _ => StatusProc.None,
    };

    /// <summary>
    /// Alan etkisini komsu bir govdeye uygular: yanik sicramasi durum verir (hasarsiz), sok zinciri ve patlama isabettir.
    /// </summary>
    public static CombatResult ApplyArea(CombatFile d, CombatBody b, in CombatArea a, List<CombatArea>? areas = null)
    {
        switch (a.Kind)
        {
            case AreaKind.BurnSpread:
                // Sicrama yananin takimindaki komsulara gider (Team = yananin takimi; hedefi Godot katmani secer).
                if (b.Dead || Status.Has(b, StatusKind.Burn) || a.Team != b.Team) return default;
                Status.AddBurn(d, b, a.Amount);
                return default;
            case AreaKind.ShockChain:
                return Apply(d, b, new CombatHit(a.Amount, a.Type, a.Team) { Chained = true }, areas);
            default:
                return Apply(d, b, new CombatHit(a.Amount, a.Type, a.Team), areas);
        }
    }

    /// <summary>
    /// Zaman: kalkan azalir, durumlar islenir (yanik ve zehir hasari zirhsiz, tickSeconds araliklarla), yanik sicrar,
    /// suresi biten kalkar. Islenen surekli hasar doner.
    /// </summary>
    public static float Tick(CombatFile d, CombatBody b, float dt, List<CombatArea>? areas = null)
    {
        if (b.ShieldT > 0) b.ShieldT = Math.Max(0, b.ShieldT - dt);
        if (b.Dead || b.Statuses.Count == 0) return 0;
        float dealt = 0, step = Math.Max(0.05f, d.TickSeconds);
        for (int i = b.Statuses.Count - 1; i >= 0; i--)
        {
            var s = b.Statuses[i];
            float live = Math.Min(dt, Math.Max(0, s.T));
            s.T -= dt;
            if (s.Kind is StatusKind.Burn or StatusKind.Poison && s.Dps > 0)
            {
                s.TickT += live;
                while (s.TickT >= step - 1e-4f && !b.Dead)
                {
                    s.TickT -= step;
                    float hit = Math.Min(b.Hp, s.Dps * step);
                    b.Hp -= hit;
                    dealt += hit;
                }
                if (s.Kind == StatusKind.Burn && d.Burn.SpreadEvery > 0 && d.Burn.SpreadTargets > 0)
                {
                    s.SpreadT += live;
                    if (s.SpreadT >= d.Burn.SpreadEvery)
                    {
                        s.SpreadT -= d.Burn.SpreadEvery;
                        areas?.Add(new CombatArea(AreaKind.BurnSpread, d.Burn.SpreadRadius, d.Burn.SpreadTargets, s.Dps, DamageType.Ates, b.Team));
                    }
                }
            }
            if (b.Dead) { Status.Clear(b); return dealt; }
            if (s.T <= 0) b.Statuses.RemoveAt(i);
        }
        return dealt;
    }

    /// <summary>Iyilestirme: zehirliyken yok (GDD §16.1). Islenen miktar doner.</summary>
    public static float Heal(CombatBody b, float amount)
    {
        if (b.Dead || amount <= 0 || Status.HealBlocked(b)) return 0;
        float before = b.Hp;
        b.Hp = Math.Min(b.MaxHp, b.Hp + amount);
        return b.Hp - before;
    }
}
