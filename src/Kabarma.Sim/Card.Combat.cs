namespace Kabarma.Sim;

// Kartlarin savas kurallari (ICERIK 8.1-8.2). GECICI ARAYUZ: savas akisinin Combat*/Status* sozlesmesi main'e girene
// kadar atis, isabet, oldurme ve alinan hasar burada duz degerlerle anlatilir; Godot katmani (Capa, Director.Night)
// bu sonuclari uygular. Sozlesme gelince HitResult'in durumlari onun Status turlerine baglanir.

/// <summary>Bir tetik cekisinin plani.</summary>
public readonly record struct ShotPlan(
    int Projectiles, int Bounces, int Pierce, float RangeMult,
    bool Explodes, float ExplodeDamage, float ExplodeRadius,
    float TwinPower, float EchoPower, float EchoDelay, bool ForkOnBounce, float FreezeArea);

/// <summary>Bir isabetin sonucu.</summary>
public readonly record struct HitResult(
    float Damage, bool Crit, bool Burn, bool Freeze, bool Shock, bool Poison, float Swell, float Heal, bool UnlimitedChain);

/// <summary>Bir oldurmenin kart etkileri.</summary>
public readonly record struct KillResult(float BlastDamage, float BlastDelay, float BlastRadius, bool ChainSwell);

/// <summary>Oyuncuya gelen hasarin kart etkilerinden gecmis hali.</summary>
public readonly record struct GuardResult(float Taken, float Absorbed, float Reflected, bool Dodged);

/// <summary>Yorunge silahinin bir atesi.</summary>
public readonly record struct OrbitalFire(string Card, int Count, float Damage, float Radius, CardDef Def);

public sealed partial class CardHand
{
    int _shots;
    /// <summary>Altin Tetik: bu gecenin oldurme yigini.</summary>
    public int KillStacks { get; private set; }
    /// <summary>Godot her kare yazar: oyuncunun can orani (Son Kursun) ve havada mi (Hokkabaz).</summary>
    public float PlayerHpFrac { get; set; } = 1f;
    public bool Airborne { get; set; }
    public float Shield { get; private set; }
    public int RevivesUsed { get; private set; }
    public float FestivalLeft { get; private set; }
    float _shieldTimer;
    bool _shieldOn;
    readonly List<(CardDef def, float timer)> _orbitals = new();

    // ---- Atis ----

    public ShotPlan NextShot()
    {
        _shots++;
        var patlar = _c.Find("patlar_uc");
        int every = (int)(patlar?.Param("every", 5) ?? 5);
        float explode = Mod("explodeDamage");
        bool explodes = explode > 0 && _shots % every == 0;
        var yanki = _c.Find("yanki");
        return new ShotPlan(Stats.Projectiles, Stats.Bounces, (int)MathF.Round(Mod("pierce")), 1f + Mod("range"),
            explodes, explodes ? explode : 0f, patlar?.Param("radius", 3) ?? 3,
            Mod("twinHead"), Mod("echo"), yanki?.Param("delay", 0.5f) ?? 0.5f, Has("forkOnBounce"), Mod("freezeArea"));
    }

    // ---- Isabet ----

    /// <summary>Kartlardan gelen hasar carpani (Iri Tane disinda): Cam Top, Kabarik Beden, Altin Tetik, Son Kursun, Hokkabaz, Hasat Senligi.</summary>
    public float DamageMult()
    {
        float m = 1f + Mod("damageMult");
        m *= 1f + KillStacks * Mod("killDamageStack");
        float th = _c.Find("son_kursun")?.Param("threshold", 0.25f) ?? 0.25f;
        if (PlayerHpFrac < th) m *= 1f + Mod("lowHpDamage");
        if (Airborne) m *= 1f + Mod("airDamage");
        if (FestivalLeft > 0) m *= 1f + Mod("festival");
        return m;
    }

    public HitResult RollHit(WeaponDef w, bool weakPoint, float weakMult)
    {
        float dmg = Stats.Damage(w.Damage) * DamageMult();
        bool crit = _rng.Chance(Stats.Crit(w.CritChance));
        if (crit) dmg *= w.CritMult;
        if (weakPoint) dmg *= weakMult;
        dmg = MathF.Round(dmg);
        return new HitResult(dmg, crit,
            _rng.Chance(Stats.BurnChance), _rng.Chance(Stats.FreezeChance),
            _rng.Chance(Mod("shockChance")), _rng.Chance(Mod("poisonChance")),
            Mod("swellOnHit"), dmg * Mod("lifesteal"), Has("unlimitedChain"));
    }

    // ---- Oldurme ----

    public KillResult OnKill()
    {
        if (Has("killDamageStack")) KillStacks++;
        var g = _c.Find("geri_sayim");
        return new KillResult(Mod("deathBlast"), g?.Param("delay", 1) ?? 1, g?.Param("radius", 3) ?? 3, Has("chainSwell"));
    }

    // ---- Beden ----

    /// <summary>Sağlam, Kabarik Beden, Cam Top: (taban + duz) x (1 + carpan).</summary>
    public float MaxHp(float baseHp) => MathF.Max(1f, (baseHp + Mod("maxHp")) * (1f + Mod("maxHpMult")));
    public float BodyScale => MathF.Max(0.3f, 1f + Mod("bodySize"));
    public int ExtraJumps => (int)MathF.Round(Mod("jumps"));
    public bool UnlimitedJumps => Has("unlimitedJumps");
    public float RegenPerSecond => Mod("regen");
    public float ShieldMax => Mod("shield");

    /// <summary>Alinan hasar: Ufak Tefek kacinmasi, Kabuk kalkani, Dikenli yansitmasi.</summary>
    public GuardResult Guard(float amount)
    {
        if (amount <= 0) return new GuardResult(0, 0, 0, false);
        if (_rng.Chance(Mod("dodge"))) return new GuardResult(0, 0, 0, true);
        float absorbed = MathF.Min(Shield, amount);
        Shield -= absorbed;
        if (absorbed > 0) _shieldTimer = 0;
        return new GuardResult(amount - absorbed, absorbed, amount * Mod("thorns"), false);
    }

    /// <summary>Ikinci Nefes: gecede rutbesi kadar kendi kendine kalkma.</summary>
    public bool TryRevive()
    {
        if (RevivesUsed >= (int)MathF.Round(Mod("revives"))) return false;
        RevivesUsed++;
        return true;
    }

    /// <summary>Hasat Senligi: kabaran her ekin ekibe 5 sn hiz ve hasar verir.</summary>
    public void OnCropSwell()
    {
        if (Has("festival")) FestivalLeft = _c.Find("hasat_senligi")?.Param("seconds", 5) ?? 5;
    }

    public float SpeedMult => Stats.MoveSpeedMult * (FestivalLeft > 0 ? 1f + Mod("festival") : 1f);

    // ---- Zaman ----

    void RebuildOrbitals()
    {
        var keep = _orbitals.ToDictionary(o => o.def.Id, o => o.timer);
        _orbitals.Clear();
        foreach (var c in _c.Cards)
            if (c.Stat == "orbital" && Rank(c.Id) > 0) _orbitals.Add((c, keep.GetValueOrDefault(c.Id)));
        if (ShieldMax <= 0) { Shield = 0; _shieldOn = false; }
        else if (!_shieldOn) { Shield = ShieldMax; _shieldOn = true; }
        else Shield = MathF.Min(Shield, ShieldMax);
    }

    /// <summary>Yorunge silahinin bu rutbedeki atesi: hasar = taban x (1 + buyume x (rutbe-1)) x Nisan.</summary>
    public OrbitalFire OrbitalAt(CardDef c)
    {
        int r = Math.Max(1, Rank(c.Id));
        float dmg = c.Param("damage") * (1f + c.Param("growth") * (r - 1)) * NisanBonus(c.Id);
        float radius = c.Param("radius");
        if (c.Id == "simsek_bulutu") radius *= 1f + Mod("cloudRadius");
        return new OrbitalFire(c.Id, (int)c.Param("count", 1), dmg, radius, c);
    }

    /// <summary>Kalkan dolumu, Hasat Senligi suresi ve yorunge silahlari. Donus: bu karedeki yorunge atesleri.</summary>
    public List<OrbitalFire> Tick(float dt)
    {
        var fires = new List<OrbitalFire>();
        if (Faded) return fires;
        FestivalLeft = MathF.Max(0, FestivalLeft - dt);
        if (ShieldMax > 0 && Shield < ShieldMax)
        {
            _shieldTimer += dt;
            float refill = _c.Find("kabuk")?.Param("refill", 10) ?? 10;
            if (_shieldTimer >= refill) { Shield = ShieldMax; _shieldTimer = 0; }
        }
        for (int i = 0; i < _orbitals.Count; i++)
        {
            var (def, t) = _orbitals[i];
            float interval = MathF.Max(0.05f, def.Param("interval", 1));
            t += dt;
            while (t >= interval) { t -= interval; fires.Add(OrbitalAt(def)); }
            _orbitals[i] = (def, t);
        }
        return fires;
    }

    /// <summary>Gece akisinin okudugu carpanlar.</summary>
    public float OzMult => 1f + Mod("ozGain");
    public float MayaMult => 1f + Mod("mayaMult");
    public float EnemyHpMult => 1f + Mod("enemyHp");
    public float GosteriCapAdd => Mod("gosteriCap");
    public float GosteriDecayMult => MathF.Max(0f, 1f + Mod("gosteriDecay"));
    public bool Bait => Has("baitTarget");
    public float ShadowPower => Mod("shadow");
}
