namespace Kabarma.Sim;

// Oyuk koruyuculari, M4 (ata4): Camur Devi (Isil Sazlik), Kuvars Akrebi (Kizil Kanyon), Buz Aynasi (Ayaz Yaylasi).
// ICERIK §4.3. Ayni Oyuk.cs sozlesmesi: IOyukFight, dovus yalniz iceride ve host'ta isler, kayda girmez. Koordinatlar oda merkezine gore.

/// <summary>
/// Camur Devi: islakken hasar almaz ve canlanir. Ates (Alev, Kizgin yangini) kurutur; kuruyunca yalniz Darbe etkili
/// hasar verir (diger turler %15). Kuruyan dev goluyne yurur; girince yeniden islanir ya da DryHold bitince kendi islanir.
/// </summary>
public sealed class MudFight : IOyukFight
{
    readonly OyukGuardianDef _g;
    readonly int _tier;
    public float Hp { get; private set; }
    public float MaxHp { get; }
    public bool Dead => Hp <= 0;
    /// <summary>Hedeflenebilir (islakken vurus 0 doner: Godot "—" gosterir).</summary>
    public bool Vulnerable => !Dead;
    public float Dryness { get; private set; }
    public float DryT { get; private set; }
    public bool Dry => DryT > 0;
    public float X { get; private set; }
    public float Z { get; private set; } = 4f;
    public int Dryings { get; private set; }
    public int Rewets { get; private set; }
    float _slam;

    public MudFight(OyukGuardianDef g, int tier)
    {
        _g = g; _tier = tier;
        MaxHp = Hp = OyukGuardianDef.Tier(g.Hp, tier);
        _slam = OyukGuardianDef.Tier(g.SlamEvery, tier);
    }

    /// <summary>Ates ya da yanma hasari (puan): kuruluk birikir; DryNeeded dolunca kurur.</summary>
    public bool Heat(float amount)
    {
        if (Dead || Dry || amount <= 0) return false;
        Dryness += amount * _g.DryPerHeat;
        if (Dryness < _g.DryNeeded) return false;
        Dryness = 0; DryT = _g.DryHold; Dryings++;
        return true;
    }

    public float Hit(float dmg) => Hit(dmg, DamageType.Kesme);

    public float Hit(float dmg, DamageType type)
    {
        if (Dead || dmg <= 0 || !Dry) return 0;
        float d = type == DamageType.Darbe ? dmg * _g.WeakMult : dmg * 0.15f;
        Hp = MathF.Max(0, Hp - d);
        return d;
    }

    public float Tick(float dt, float px, float pz)
    {
        if (Dead) return 0;
        float speed = OyukGuardianDef.Tier(_g.MoveSpeed, _tier);
        if (Dry)
        {
            DryT -= dt;
            float gx = _g.Pool.Count > 0 ? _g.Pool[0] : 0f, gz = _g.Pool.Count > 1 ? _g.Pool[1] : -8f;
            Step(gx, gz, speed * 1.5f, dt, 0f);
            bool inPool = (X - gx) * (X - gx) + (Z - gz) * (Z - gz) <= _g.PoolRadius * _g.PoolRadius;
            if (inPool || DryT <= 0) { DryT = 0; Dryness = 0; Rewets++; }
        }
        else
        {
            Hp = MathF.Min(MaxHp, Hp + OyukGuardianDef.Tier(_g.RegenPerSecond, _tier) * dt); // islakken kendini onarir
            Dryness = MathF.Max(0, Dryness - 0.5f * dt);
            Step(px, pz, speed, dt, 1.4f);
        }
        _slam -= dt;
        if (_slam > 0) return 0;
        _slam = OyukGuardianDef.Tier(_g.SlamEvery, _tier);
        float d = (X - px) * (X - px) + (Z - pz) * (Z - pz);
        return d <= _g.HitRadius * _g.HitRadius ? OyukGuardianDef.Tier(_g.Damage, _tier) : 0f;
    }

    void Step(float tx, float tz, float speed, float dt, float stop)
    {
        float dx = tx - X, dz = tz - Z, len = MathF.Sqrt(dx * dx + dz * dz);
        if (len <= stop || len < 0.001f) return;
        float m = MathF.Min(len - stop, speed * dt);
        X += dx / len * m; Z += dz / len * m;
    }
}

/// <summary>
/// Kuvars Akrebi: zirh mermiyi sektirir (vurus 0). Agir bir Darbe (Havan: CrackThreshold ustu) ya da ustune dusen sarkit zirhi
/// CrackSeconds catlatir; catlakta her hasar WeakMult. Kuyruk TailEvery'de menzildeki oyuncuya zehir atar.
/// </summary>
public sealed class ScorpionFight : IOyukFight
{
    public sealed class Stalactite
    {
        public float X, Z;
        public bool Fallen;
        /// <summary>Dusme geri sayimi (sn); &lt;0 = dusmuyor.</summary>
        public float Fall = -1f;
    }

    readonly OyukGuardianDef _g;
    readonly int _tier;
    public readonly List<Stalactite> Stalactites = new();
    public float Hp { get; private set; }
    public float MaxHp { get; }
    public bool Dead => Hp <= 0;
    public bool Vulnerable => !Dead;
    public float CrackT { get; private set; }
    public bool Cracked => CrackT > 0;
    public float X { get; private set; }
    public float Z { get; private set; } = 3f;
    public int Cracks { get; private set; }
    public int Bounces { get; private set; }
    float _tail, _regen;

    public ScorpionFight(OyukGuardianDef g, int tier)
    {
        _g = g; _tier = tier;
        MaxHp = Hp = OyukGuardianDef.Tier(g.Hp, tier);
        _tail = OyukGuardianDef.Tier(g.TailEvery, tier);
        Reset();
    }

    void Reset()
    {
        Stalactites.Clear();
        int n = Math.Max(1, _g.Stalactites);
        for (int i = 0; i < n; i++)
        {
            float a = i * MathF.Tau / n + 0.5f;
            Stalactites.Add(new Stalactite { X = MathF.Cos(a) * _g.StalactiteRing, Z = MathF.Sin(a) * _g.StalactiteRing });
        }
        _regen = 0;
    }

    void Crack() { CrackT = _g.CrackSeconds; Cracks++; }

    /// <summary>Sarkit vuruldu: FallSeconds sonra duser; dusunce Akrep etki yaricapindaysa zirh catlar.</summary>
    public bool DropStalactite(int i)
    {
        if (i < 0 || i >= Stalactites.Count || Stalactites[i].Fallen || Stalactites[i].Fall >= 0) return false;
        Stalactites[i].Fall = _g.FallSeconds;
        return true;
    }

    public int StalactiteNear(float x, float z, float radius)
    {
        int best = -1; float bd = radius * radius;
        for (int i = 0; i < Stalactites.Count; i++)
        {
            float dx = Stalactites[i].X - x, dz = Stalactites[i].Z - z, d = dx * dx + dz * dz;
            if (!Stalactites[i].Fallen && d <= bd) { bd = d; best = i; }
        }
        return best;
    }

    public float Hit(float dmg) => Hit(dmg, DamageType.Kesme);

    public float Hit(float dmg, DamageType type)
    {
        if (Dead || dmg <= 0) return 0;
        if (!Cracked)
        {
            if (type is DamageType.Darbe or DamageType.Ates && dmg >= _g.CrackThreshold) Crack(); // Havan
            else Bounces++;
            return 0;
        }
        float d = dmg * _g.WeakMult;
        Hp = MathF.Max(0, Hp - d);
        return d;
    }

    public float Tick(float dt, float px, float pz)
    {
        if (Dead) return 0;
        if (CrackT > 0) CrackT = MathF.Max(0, CrackT - dt);
        foreach (var s in Stalactites)
        {
            if (s.Fall < 0) continue;
            s.Fall -= dt;
            if (s.Fall > 0) continue;
            s.Fall = -1; s.Fallen = true;
            if ((s.X - X) * (s.X - X) + (s.Z - Z) * (s.Z - Z) <= _g.StalactiteRadius * _g.StalactiteRadius) Crack();
        }
        if (Stalactites.All(s => s.Fallen)) { _regen += dt; if (_regen >= 6f) Reset(); }
        // yurur: oyuncuya yaklasir (sarkit altina cekilebilir)
        float dx = px - X, dz = pz - Z, len = MathF.Sqrt(dx * dx + dz * dz);
        if (len > 3.5f) { float m = MathF.Min(len - 3.5f, 2.2f * dt); X += dx / len * m; Z += dz / len * m; }
        _tail -= dt;
        if (_tail > 0) return 0;
        _tail = OyukGuardianDef.Tier(_g.TailEvery, _tier);
        return len <= _g.TailRange ? OyukGuardianDef.Tier(_g.Damage, _tier) : 0f;
    }
}

/// <summary>
/// Buz Aynasi: duvardaki ayna asil hedeftir ama kopyalar varken vurulmaz. Her CopyEvery'de oyuncunun buzdan kopyasi cikar
/// (en cok CopyMax); kopya oyuncuya yurur ve vurur. Kopya Ates ve Sok ile hemen erir; digerleri %25 islerHasar.
/// </summary>
public sealed class MirrorFight : IOyukFight
{
    public sealed class Copy
    {
        public float X, Z, Hp, Atk;
    }

    readonly OyukGuardianDef _g;
    readonly int _tier;
    public readonly List<Copy> Copies = new();
    public float Hp { get; private set; }
    public float MaxHp { get; }
    public bool Dead => Hp <= 0;
    /// <summary>Ayna kopya yokken vurulur.</summary>
    public bool Vulnerable => !Dead && Copies.Count == 0;
    public float MirrorX { get; } = 0f;
    public float MirrorZ { get; }
    public int Melted { get; private set; }
    public int Made { get; private set; }
    float _spawn;

    public MirrorFight(OyukGuardianDef g, int tier, float roomRadius)
    {
        _g = g; _tier = tier;
        MaxHp = Hp = OyukGuardianDef.Tier(g.Hp, tier);
        MirrorZ = -(roomRadius - 1.6f);
        _spawn = OyukGuardianDef.Tier(g.CopyEvery, tier) * 0.5f;
    }

    public float Hit(float dmg) => Hit(dmg, DamageType.Kesme);

    public float Hit(float dmg, DamageType type)
    {
        if (!Vulnerable || dmg <= 0) return 0;
        float d = dmg * _g.WeakMult;
        Hp = MathF.Max(0, Hp - d);
        return d;
    }

    /// <summary>Kopyaya vurus: Ates/Sok eritir (true doner); digerleri kopyanin canini %25 etkiyle dusurur.</summary>
    public bool HitCopy(int i, float dmg, DamageType type)
    {
        if (i < 0 || i >= Copies.Count || dmg <= 0) return false;
        var c = Copies[i];
        if (type is DamageType.Ates or DamageType.Sok) c.Hp = 0; else c.Hp -= dmg * 0.25f;
        if (c.Hp > 0) return false;
        Copies.RemoveAt(i);
        Melted++;
        return true;
    }

    public float Tick(float dt, float px, float pz)
    {
        if (Dead) return 0;
        if (Copies.Count < OyukGuardianDef.Tier(_g.CopyMax, _tier))
        {
            _spawn -= dt;
            if (_spawn <= 0)
            {
                _spawn = OyukGuardianDef.Tier(_g.CopyEvery, _tier);
                float a = Made * 2.4f;
                Copies.Add(new Copy { X = MirrorX + MathF.Cos(a) * 2f, Z = MirrorZ + 2.2f + MathF.Sin(a) * 0.6f, Hp = OyukGuardianDef.Tier(_g.CopyHp, _tier), Atk = _g.CopyAttackEvery });
                Made++;
            }
        }
        float dmg = 0f;
        foreach (var c in Copies)
        {
            float dx = px - c.X, dz = pz - c.Z, len = MathF.Sqrt(dx * dx + dz * dz);
            if (len > _g.CopyReach * 0.8f) { float m = MathF.Min(len, _g.CopySpeed * dt); c.X += dx / len * m; c.Z += dz / len * m; }
            c.Atk -= dt;
            if (c.Atk > 0 || len > _g.CopyReach) continue;
            c.Atk = _g.CopyAttackEvery;
            dmg += OyukGuardianDef.Tier(_g.Damage, _tier);
        }
        return dmg;
    }
}
