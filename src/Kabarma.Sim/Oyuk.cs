namespace Kabarma.Sim;

/// <summary>
/// Oyuk kurallari (M3, ICERIK §4.3, §17 "M3: Oyuklar"). Iki parca:
///  - OyukProgress: kalici gercekler (temizlenen Oyuk, acilan sandik); kayit bolumu "oyuk".
///  - MoleFight / BatKnotFight: dovus, yalniz oyuncu iceridiyken ve host'ta isler; kayda girmez (cikinca dovus bastan).
/// Godot katmani (game/scripts/world/Oyuk*.cs) bunlara olay verir (vurus, islatma, tikac, isik) ve durumu cizer.
/// </summary>
public static class OyukRules
{
    /// <summary>Noktanin kademesi: kimligin son parcasi (oyuk_altin_cayir_2 → 2).</summary>
    public static int TierOf(Poi p)
    {
        int i = p.Id.LastIndexOf('_');
        return i >= 0 && int.TryParse(p.Id[(i + 1)..], out int n) ? n : 0;
    }

    /// <summary>Biyomdaki Oyuk noktalari kimlik sirasiyla (oda dizini bu sira).</summary>
    public static List<Poi> Oyuks(IReadOnlyList<Poi> pois) => pois.Where(p => p.Type == "oyuk").OrderBy(p => p.Id, StringComparer.Ordinal).ToList();

    /// <summary>Odanin xz merkezi: oynanabilir karenin disinda, dizinle siralanmis.</summary>
    public static (float x, float z) RoomCenter(OyukInteriorDef d, int index) => (d.Origin[0] + index * d.Spacing, d.Origin[1]);

    /// <summary>Sandik basina ödül: kademenin toplami sandiklara bolunur (ilk sandik artani alir).</summary>
    public static Dictionary<string, int> ChestLoot(OyukGuardianDef g, int tier, int chest)
    {
        var loot = new Dictionary<string, int>();
        int n = Math.Max(1, g.Chests);
        foreach (var (item, list) in g.Rewards)
        {
            int total = OyukGuardianDef.Tier(list, tier);
            int share = total / n + (chest == 0 ? total % n : 0);
            if (share > 0) loot[item] = share;
        }
        return loot;
    }
}

/// <summary>Kalici Oyuk durumu (duz, kayitta).</summary>
public sealed class OyukRecord
{
    public bool Cleared { get; set; }
    /// <summary>Acilan sandiklarin dizinleri.</summary>
    public List<int> Looted { get; set; } = new();
    public int Entered { get; set; }
}

public sealed class OyukProgress
{
    public Dictionary<string, OyukRecord> Records { get; set; } = new();

    public OyukRecord Of(string id) => Records.TryGetValue(id, out var r) ? r : Records[id] = new OyukRecord();
    public bool IsCleared(string id) => Records.TryGetValue(id, out var r) && r.Cleared;
    public int ClearedCount => Records.Values.Count(r => r.Cleared);

    /// <summary>Sandigi acar: ilk aciliysa ödülü dondurur, yoksa bos. Koruyucu yenilmeden acilmaz.</summary>
    public Dictionary<string, int> OpenChest(string id, OyukGuardianDef g, int tier, int chest)
    {
        var r = Of(id);
        if (!r.Cleared || r.Looted.Contains(chest) || chest < 0 || chest >= g.Chests) return new();
        r.Looted.Add(chest);
        return OyukRules.ChestLoot(g, tier, chest);
    }

    /// <summary>Kayittan sessizce (yerinde; ortak oyunun kayit koprusu saniyede bir uygular, nesne degismez).</summary>
    public void LoadFrom(OyukProgress? other)
    {
        Records.Clear();
        if (other == null) return;
        foreach (var (k, v) in other.Records) Records[k] = new OyukRecord { Cleared = v.Cleared, Looted = v.Looted.ToList(), Entered = v.Entered };
    }
}

public sealed partial class GameState
{
    OyukProgress? _oyuk;
    public OyukProgress Oyuk => _oyuk ??= new OyukProgress();

    /// <summary>Kayit (TEKNIK 9.1): temizlenen Oyuklar ve acilan sandiklar. Dovus durumu kayda girmez.</summary>
    [SaveSection("oyuk", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Oyuk) })]
    OyukProgress OyukSection
    {
        get => Oyuk;
        set => Oyuk.LoadFrom(value);
    }
}

/// <summary>Koruyucu dovusunun ortak yuzu (Godot katmani bunu cizer ve vurur).</summary>
public interface IOyukFight
{
    float Hp { get; }
    float MaxHp { get; }
    bool Dead { get; }
    /// <summary>Simdi vurulabilir mi (saklanan kostebek, toplanmis yumak vurulamaz).</summary>
    bool Vulnerable { get; }
    /// <summary>Vurus: verilen hasar (savunmasizken carpanli). Vurulamiyorsa 0.</summary>
    float Hit(float dmg);
    /// <summary>Bu karede oyuncuya vurus varsa hasari (yoksa 0). Oyuncunun oda merkezine gore konumu verilir.</summary>
    float Tick(float dt, float px, float pz);
}

/// <summary>
/// Kostebek Bey: Hidden (saklı, vurulmaz) → Surfaced (acik bir delikten cikar; cikista yakindaki oyuncuya carpar) → Hidden.
/// Islak delikten cikar ya da ustteyken deligi islanirsa Stunned (hasar WeakMult). Tikanan delikten cikamaz. Butun delikler
/// tikaninca Trapped: ortada kalir, once sersemler, sonra aralikla hamle eder; artik dalamaz.
/// </summary>
public sealed class MoleFight : IOyukFight
{
    public enum Phase { Hidden, Surfaced, Stunned, Trapped }

    public sealed class Hole
    {
        public float X, Z;
        public bool Plugged;
        /// <summary>Kalan islaklik (sn) ve birikmis su (L).</summary>
        public float Wet, Liters;
    }

    readonly OyukGuardianDef _g;
    readonly int _tier;
    readonly Rng _rng;
    public readonly List<Hole> Holes = new();
    public Phase State { get; private set; } = Phase.Hidden;
    /// <summary>Ustteyse cikilan delik (-1: ortada, sikisik).</summary>
    public int At { get; private set; } = -1;
    public float Timer { get; private set; }
    public float Hp { get; private set; }
    public float MaxHp { get; }
    public bool Dead => Hp <= 0;
    public bool Vulnerable => !Dead && State != Phase.Hidden;
    public bool Weak => State == Phase.Stunned || (State == Phase.Trapped && _trapStun > 0);
    /// <summary>Kostebegin oda merkezine gore konumu (ustteyken).</summary>
    public (float x, float z) Pos => At >= 0 ? (Holes[At].X, Holes[At].Z) : (0f, 0f);
    public int Surfacings { get; private set; }
    float _trapStun, _lunge;

    public MoleFight(OyukGuardianDef g, int tier, Rng rng)
    {
        _g = g; _tier = tier; _rng = rng;
        MaxHp = Hp = OyukGuardianDef.Tier(g.Hp, tier);
        int n = Math.Max(1, OyukGuardianDef.Tier(g.Holes, tier));
        for (int i = 0; i < n; i++)
        {
            float a = i * MathF.Tau / n + 0.3f;
            Holes.Add(new Hole { X = MathF.Cos(a) * g.HoleRing, Z = MathF.Sin(a) * g.HoleRing });
        }
        Timer = OyukGuardianDef.Tier(g.HiddenSeconds, tier);
    }

    public bool AllPlugged => Holes.All(h => h.Plugged);

    /// <summary>Delige su: Liters birikir; FloodLiters'a ulasinca delik WetSeconds islak. Kostebek o delikteyse sersemler.</summary>
    public bool Water(int hole, float liters)
    {
        if (hole < 0 || hole >= Holes.Count || Dead) return false;
        var h = Holes[hole];
        if (h.Plugged) return false;
        h.Liters += liters;
        if (h.Liters < _g.FloodLiters) return false;
        h.Liters = 0;
        h.Wet = _g.WetSeconds;
        if (State == Phase.Surfaced && At == hole) Stun(_g.StunSeconds);
        return true;
    }

    /// <summary>Tikac kayasi delige birakildi: delik kalici kapanir. Kostebek o delikteyse orada kalir (dalamaz) ve sersemler.</summary>
    public bool Plug(int hole)
    {
        if (hole < 0 || hole >= Holes.Count || Holes[hole].Plugged) return false;
        Holes[hole].Plugged = true;
        Holes[hole].Wet = 0;
        if (AllPlugged) { Trap(); return true; }
        if ((State == Phase.Surfaced || State == Phase.Stunned) && At == hole) Stun(_g.StunSeconds);
        return true;
    }

    /// <summary>Konuma en yakin delik (yaricap icinde; yoksa -1).</summary>
    public int HoleNear(float x, float z, float radius)
    {
        int best = -1; float bd = radius * radius;
        for (int i = 0; i < Holes.Count; i++)
        {
            float dx = Holes[i].X - x, dz = Holes[i].Z - z, d = dx * dx + dz * dz;
            if (d <= bd) { bd = d; best = i; }
        }
        return best;
    }

    void Stun(float s) { State = Phase.Stunned; Timer = s; }

    void Trap()
    {
        State = Phase.Trapped;
        At = -1;
        _trapStun = _g.TrappedStunSeconds;
        _lunge = _g.TrappedLungeEvery;
    }

    public float Hit(float dmg)
    {
        if (!Vulnerable || dmg <= 0) return 0;
        float d = Weak ? dmg * _g.WeakMult : dmg;
        Hp = MathF.Max(0, Hp - d);
        return d;
    }

    public float Tick(float dt, float px, float pz)
    {
        if (Dead) return 0;
        foreach (var h in Holes) if (h.Wet > 0) h.Wet = MathF.Max(0, h.Wet - dt);
        float dmg = OyukGuardianDef.Tier(_g.Damage, _tier);
        switch (State)
        {
            case Phase.Trapped:
                if (_trapStun > 0) { _trapStun -= dt; return 0; }
                _lunge -= dt;
                if (_lunge > 0) return 0;
                _lunge = _g.TrappedLungeEvery;
                return Near(px, pz, 0, 0, _g.HitRadius * 1.5f) ? dmg : 0;
            case Phase.Hidden:
                Timer -= dt;
                if (Timer > 0) return 0;
                var open = Enumerable.Range(0, Holes.Count).Where(i => !Holes[i].Plugged).ToList();
                if (open.Count == 0) { Trap(); return 0; }
                At = open[_rng.Range(0, open.Count)];
                Surfacings++;
                if (Holes[At].Wet > 0) { Stun(_g.StunSeconds); return 0; }
                State = Phase.Surfaced;
                Timer = OyukGuardianDef.Tier(_g.SurfaceSeconds, _tier);
                // Cikis carpmasi: delikte duran oyuncu.
                return Near(px, pz, Holes[At].X, Holes[At].Z, _g.HitRadius) ? dmg : 0;
            default: // Surfaced, Stunned
                Timer -= dt;
                if (Timer > 0) return 0;
                // Dalar (ustundeki delik tikandiysa yandan kazar; o delik kapali kalir).
                State = Phase.Hidden;
                At = -1;
                Timer = OyukGuardianDef.Tier(_g.HiddenSeconds, _tier);
                return 0;
        }
    }

    static bool Near(float px, float pz, float x, float z, float r) => (px - x) * (px - x) + (pz - z) * (pz - z) <= r * r;
}

/// <summary>
/// Yarasa Yumagi: Gathered (tavanda toplu, vurulmaz; aralikla oyuncuya dalis) ↔ Scattered (yakinda isik var: ana yarasa
/// gorunur ve vurulur). Isik gidince RegroupSeconds sonra yeniden toplanir.
/// </summary>
public sealed class BatKnotFight : IOyukFight
{
    readonly OyukGuardianDef _g;
    readonly int _tier;
    public bool Scattered { get; private set; }
    public float Hp { get; private set; }
    public float MaxHp { get; }
    public bool Dead => Hp <= 0;
    public bool Vulnerable => !Dead && Scattered;
    public int Bats { get; }
    /// <summary>Isiksiz gecen sure (dagilmisken) ve dalis sayaci.</summary>
    public float Dark { get; private set; }
    float _dive;
    /// <summary>Yumagin oda merkezine gore konumu ve isik var mi (Godot her kare verir).</summary>
    public bool Lit;
    public int Scatters { get; private set; }

    public BatKnotFight(OyukGuardianDef g, int tier)
    {
        _g = g; _tier = tier;
        MaxHp = Hp = OyukGuardianDef.Tier(g.Hp, tier);
        Bats = OyukGuardianDef.Tier(g.Bats, tier);
        _dive = OyukGuardianDef.Tier(g.DiveEvery, tier);
    }

    /// <summary>Isik kaynagi yumaga LightRadius icinde mi (kaynak ile yumak tabandan olculur, yukseklik sayilmaz).</summary>
    public bool InLight(float lx, float lz, float kx, float kz) => (lx - kx) * (lx - kx) + (lz - kz) * (lz - kz) <= _g.LightRadius * _g.LightRadius;

    public float Hit(float dmg)
    {
        if (!Vulnerable || dmg <= 0) return 0;
        float d = dmg * _g.WeakMult;
        Hp = MathF.Max(0, Hp - d);
        return d;
    }

    public float Tick(float dt, float px, float pz)
    {
        if (Dead) return 0;
        if (Lit)
        {
            if (!Scattered) Scatters++;
            Scattered = true;
            Dark = 0;
            return 0;
        }
        if (Scattered)
        {
            Dark += dt;
            if (Dark >= OyukGuardianDef.Tier(_g.RegroupSeconds, _tier)) { Scattered = false; Dark = 0; _dive = OyukGuardianDef.Tier(_g.DiveEvery, _tier); }
            return 0;
        }
        _dive -= dt;
        if (_dive > 0) return 0;
        _dive = OyukGuardianDef.Tier(_g.DiveEvery, _tier);
        return OyukGuardianDef.Tier(_g.Damage, _tier); // karanlikta dalis: odadaki oyuncuya
    }
}
