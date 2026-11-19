using System.Numerics;

namespace Kabarma.Sim;

/// <summary>Kurulu bir kule (GDD §16.4). Duz ve serilestirilebilir.</summary>
public sealed class TowerState
{
    public int Id { get; set; }
    public string Def { get; set; } = "";
    public float[] Pos { get; set; } = new float[3];
    public float Yaw { get; set; }
    /// <summary>Depodaki cephane (esya -> adet).</summary>
    public Dictionary<string, int> Mag { get; set; } = new();
    public float Hp { get; set; }
    /// <summary>defense.json priorities'ten biri; "off" kapali.</summary>
    public string Priority { get; set; } = "nearest";
    public float Cooldown { get; set; }
    public int Shots { get; set; }

    public Vector3 At => new(Pos[0], Pos[1], Pos[2]);
    public int Loaded => Mag.Values.Sum();
}

public enum TowerStatus { Ready, Empty, Off, Broken }

/// <summary>Kule kurallari: hedef secimi, one nisan, kavis, koni, cephane, asinma, onarim (GDD §16.4 M3).</summary>
public static class TowerRules
{
    static float Flat(Vector3 a, Vector3 b) { var d = a - b; d.Y = 0; return d.Length(); }

    public static TowerStatus Status(TowerState t, TowerDef? d = null) =>
        t.Hp <= 0 ? TowerStatus.Broken : t.Priority == "off" ? TowerStatus.Off
        : t.Loaded <= 0 && (d == null || (d.Ammo.Count > 0 && !d.AmmoOptional)) ? TowerStatus.Empty : TowerStatus.Ready; // M4: cephanesiz (guc) ve istege bagli cephaneli kule hep hazir

    /// <summary>rangeMult: Tuzuk Ters Ruzgar (mermi menzili carpani, gece4); en az menzil degismez.</summary>
    public static bool InRange(TowerDef d, Vector3 at, DefenseTarget x, float rangeMult = 1f)
    {
        float r = Flat(at, x.Pos);
        return r >= d.MinRange && r <= d.Range * rangeMult + x.Radius;
    }

    /// <summary>Oncelige gore hedef (listedeki sira); yoksa -1. Aranan tur yoksa en yakina doner; esitlikte kucuk Id.</summary>
    public static int Pick(TowerDef d, Vector3 at, IReadOnlyList<DefenseTarget> ts, string priority, float rangeMult = 1f)
    {
        if (priority == "off") return -1;
        int best = -1;
        bool Better(int i, int j)
        {
            var a = ts[i]; var b = ts[j];
            float da = Flat(at, a.Pos), db = Flat(at, b.Pos);
            if (priority == "strongest" && Math.Abs(a.Hp - b.Hp) > 1e-3f) return a.Hp > b.Hp;
            if (priority == "flying" && a.Flying != b.Flying) return a.Flying;
            if (priority == "thief" && a.Thief != b.Thief) return a.Thief;
            if (Math.Abs(da - db) > 1e-4f) return da < db;
            return a.Id < b.Id;
        }
        for (int i = 0; i < ts.Count; i++)
        {
            var x = ts[i];
            if (x.IsPlayer || x.Hp <= 0 || !InRange(d, at, x, rangeMult)) continue;
            if (best < 0 || Better(i, best)) best = i;
        }
        return best;
    }

    /// <summary>One nisan: mermi hizi s ile hareketli hedefe carpisma noktasi (cozum yoksa hedefin kendisi).</summary>
    public static Vector3 Lead(Vector3 from, Vector3 pos, Vector3 vel, float speed)
    {
        var r = pos - from;
        float a = Vector3.Dot(vel, vel) - speed * speed, b = 2f * Vector3.Dot(r, vel), c = Vector3.Dot(r, r);
        float t;
        if (Math.Abs(a) < 1e-5f) t = b < 0 ? -c / b : -1f;
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0) return pos;
            float s = MathF.Sqrt(disc), t1 = (-b - s) / (2f * a), t2 = (-b + s) / (2f * a);
            t = t1 > 0 && t2 > 0 ? Math.Min(t1, t2) : Math.Max(t1, t2);
        }
        return t > 0 ? pos + vel * t : pos;
    }

    /// <summary>Kavis: from'dan to'ya t saniyede dusen merminin ilk hizi (yercekimi g asagi).</summary>
    public static Vector3 ArcVelocity(Vector3 from, Vector3 to, float t, float g) =>
        (to - from) / t + new Vector3(0, 0.5f * g * t, 0);

    public static Vector3 ArcAt(Vector3 from, Vector3 v0, float g, float time) =>
        from + v0 * time + new Vector3(0, -0.5f * g * time * time, 0);

    /// <summary>Alan: merkezden r icindeki hedefler (oyuncu haric).</summary>
    public static List<int> Splash(Vector3 at, float r, IReadOnlyList<DefenseTarget> ts)
    {
        var hit = new List<int>();
        for (int i = 0; i < ts.Count; i++)
            if (!ts[i].IsPlayer && ts[i].Hp > 0 && Flat(at, ts[i].Pos) <= r + ts[i].Radius) hit.Add(i);
        return hit;
    }

    /// <summary>Koni: yatayda esit acili yelpaze (deterministik).</summary>
    public static List<Vector3> ConeDirs(Vector3 dir, int pellets, float degrees)
    {
        var flat = new Vector3(dir.X, 0, dir.Z);
        flat = flat.LengthSquared() < 1e-6f ? -Vector3.UnitZ : Vector3.Normalize(flat);
        var l = new List<Vector3>();
        float half = degrees * MathF.PI / 360f;
        for (int i = 0; i < pellets; i++)
        {
            float a = pellets == 1 ? 0 : -half + 2f * half * i / (pellets - 1);
            float c = MathF.Cos(a), s = MathF.Sin(a);
            l.Add(new Vector3(flat.X * c - flat.Z * s, 0, flat.X * s + flat.Z * c));
        }
        return l;
    }

    /// <summary>Koni isabetleri: her tane menzil icindeki ilk hedefe (isin hedef yaricapindan gecerse). Hedef sirasi -> tane sayisi.</summary>
    public static Dictionary<int, int> ConeHits(TowerDef d, Vector3 from, Vector3 dir, IReadOnlyList<DefenseTarget> ts)
    {
        var hits = new Dictionary<int, int>();
        foreach (var u in ConeDirs(dir, Math.Max(1, d.Pellets), d.ConeDegrees))
        {
            int best = -1; float bestT = float.MaxValue;
            for (int i = 0; i < ts.Count; i++)
            {
                var x = ts[i];
                if (x.IsPlayer || x.Hp <= 0) continue;
                var r = x.Pos - from; r.Y = 0;
                float along = Vector3.Dot(r, u);
                if (along <= 0 || along > d.Range + x.Radius) continue;
                float off = (r - u * along).Length();
                if (off <= x.Radius && along < bestT) { bestT = along; best = i; }
            }
            if (best >= 0) hits[best] = hits.GetValueOrDefault(best) + 1;
        }
        return hits;
    }

    /// <summary>
    /// Ark Bobini: ilk hedeften yakindaki hedeflere zincirleme Sok (hedef, hasar carpani); her atlamada carpan ChainFalloff
    /// kadar azalir, en fazla ChainTargets atlama, atlama ChainRadius icinde (hedef yaricapi dahil).
    /// </summary>
    public static List<(int index, float mult)> Chain(TowerDef d, int first, IReadOnlyList<DefenseTarget> ts)
    {
        var res = new List<(int, float)> { (first, 1f) };
        var used = new HashSet<int> { first };
        int cur = first; float m = 1f;
        for (int k = 0; k < d.ChainTargets; k++)
        {
            m *= d.ChainFalloff;
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < ts.Count; i++)
            {
                if (used.Contains(i) || ts[i].IsPlayer || ts[i].Hp <= 0) continue;
                float dd = Flat(ts[cur].Pos, ts[i].Pos);
                if (dd <= d.ChainRadius + ts[i].Radius && dd < bd) { bd = dd; best = i; }
            }
            if (best < 0) break;
            res.Add((best, m)); used.Add(best); cur = best;
        }
        return res;
    }

    /// <summary>Lazer: hasar x (guclendirici cephane varsa BoostMult) x (hedef ucansa FlyingMult).</summary>
    public static float BeamDamage(TowerDef d, DefenseTarget x, bool boosted) =>
        d.Damage * (boosted ? d.BoostMult : 1f) * (x.Flying ? d.FlyingMult : 1f);

    /// <summary>Cantadan depoya cephane aktarir (tanimdaki sirayla, depo dolana dek). Aktarilan adet.</summary>
    public static int Load(TowerState t, TowerDef d, Inventory bag)
    {
        int moved = 0;
        foreach (var item in d.Ammo)
        {
            int room = d.Magazine - t.Loaded;
            if (room <= 0) break;
            int n = Math.Min(room, bag.Count(item));
            if (n <= 0 || !bag.Remove(item, n)) continue;
            t.Mag[item] = t.Mag.GetValueOrDefault(item) + n;
            moved += n;
        }
        return moved;
    }

    /// <summary>Bekleme suresini ilerletir; ates edebiliyorsa true.</summary>
    public static bool Tick(TowerState t, float dt, TowerDef? d = null)
    {
        t.Cooldown = Math.Max(0f, t.Cooldown - dt);
        return t.Cooldown <= 0f && Status(t, d) == TowerStatus.Ready;
    }

    /// <summary>Atis: bir cephane (tanim sirasindaki ilk dolu) duser, bekleme ve asinma yazilir. Harcanan esya (yoksa null).</summary>
    public static string? Fire(TowerState t, TowerDef d)
    {
        if (Status(t, d) != TowerStatus.Ready || t.Cooldown > 0) return null;
        string? item = d.Ammo.FirstOrDefault(a => t.Mag.GetValueOrDefault(a) > 0);
        if (item == null && (d.Ammo.Count == 0 || d.AmmoOptional)) item = ""; // guc kulesi: cephanesiz atar ("" = cephane harcanmadi)
        if (item == null) return null;
        if (item.Length > 0 && --t.Mag[item] <= 0) t.Mag.Remove(item);
        t.Cooldown = d.Interval;
        t.Hp = Math.Max(0f, t.Hp - d.WearPerShot);
        t.Shots++;
        return item;
    }

    /// <summary>Onceligi siradakine gecirir (sonu "off", sonra basa).</summary>
    public static string NextPriority(DefenseFile f, string cur)
    {
        int i = f.Priorities.IndexOf(cur);
        return f.Priorities[(i + 1) % f.Priorities.Count];
    }

    /// <summary>Onarim bedeli: tarifin repairFraction'i, eksik can oraninda (yukari yuvarlanir). Can tamsa bos.</summary>
    public static Dictionary<string, int> RepairCost(IReadOnlyDictionary<string, int> recipe, float hp, float max, float fraction)
    {
        var c = new Dictionary<string, int>();
        if (max <= 0 || hp >= max - 1e-3f) return c;
        float miss = Math.Clamp(1f - hp / max, 0f, 1f);
        foreach (var (k, n) in recipe)
        {
            int need = (int)MathF.Ceiling(n * fraction * miss - 1e-4f);
            if (need > 0) c[k] = need;
        }
        if (c.Count == 0 && recipe.Count > 0) c[recipe.First().Key] = 1;
        return c;
    }
}
