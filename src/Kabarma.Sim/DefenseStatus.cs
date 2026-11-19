using System.Numerics;

namespace Kabarma.Sim;

/// <summary>
/// Kule ve tuzak durumlari (M4 kale; GDD §16.1, ICERIK §13): Yanik (Alev Kulesi, Yag Hendegi), Kabarik (Maya Topu Kulesi).
/// Hedefler Zipzip'ler olduğu ve Combat sozlesmesi onlara baglanmadigi icin (DefenseTarget GECICI) durum burada, hedef
/// kimligiyle tutulur; sayilar combat.json (burn, swell). Sok ve Donma anlik etkidir (TrapEffect, Godot yaratiga uygular).
/// Duz ve serilestirilebilir degil: gecici (gece boyu) durumdur, kayda girmez.
/// </summary>
public sealed class DefenseStatus
{
    readonly Dictionary<int, (float left, float dps)> _burn = new();
    readonly Dictionary<int, float> _swell = new();
    float _tick;

    public bool IsBurning(int id) => _burn.ContainsKey(id);
    public IEnumerable<int> Burning => _burn.Keys;
    public float SwellOf(int id) => _swell.GetValueOrDefault(id);

    /// <summary>Yanik ekler: sure uzar, saniyedeki hasar buyuk olan kalir.</summary>
    public void AddBurn(int id, float dps, float seconds)
    {
        _burn[id] = _burn.TryGetValue(id, out var b) ? (Math.Max(b.left, seconds), Math.Max(b.dps, dps)) : (seconds, dps);
    }

    /// <summary>
    /// Yanigi <paramref name="dt"/> ilerletir; her <paramref name="tickSeconds"/>'de (hasar hedef, hasar). Suresi bitenler ve
    /// <paramref name="alive"/>'da olmayanlar silinir.
    /// </summary>
    public List<(int id, float damage)> StepBurn(float dt, float tickSeconds, IReadOnlySet<int> alive)
    {
        var dmg = new List<(int, float)>();
        _tick += dt;
        bool tick = _tick >= tickSeconds;
        float step = tick ? _tick : 0f;
        if (tick) _tick = 0f;
        foreach (var id in _burn.Keys.ToList())
        {
            var (left, dps) = _burn[id];
            left -= dt;
            if (left <= 0f || !alive.Contains(id)) { _burn.Remove(id); continue; }
            _burn[id] = (left, dps);
            if (tick) dmg.Add((id, dps * step));
        }
        return dmg;
    }

    /// <summary>Yanan hedeften yakindaki yanmayana yayilma (combat.json burn.spreadRadius): yayilacak hedefler.</summary>
    public List<int> Spread(BurnDef b, IReadOnlyList<DefenseTarget> ts)
    {
        var fresh = new List<int>();
        foreach (var src in ts.Where(t => IsBurning(t.Id)))
        {
            int n = 0;
            foreach (var t in ts.OrderBy(t => Vector3.Distance(t.Pos, src.Pos)))
            {
                if (n >= b.SpreadTargets) break;
                if (t.Id == src.Id || t.IsPlayer || t.Hp <= 0 || IsBurning(t.Id) || fresh.Contains(t.Id)) continue;
                if (Vector3.Distance(t.Pos, src.Pos) > b.SpreadRadius) continue;
                fresh.Add(t.Id); n++;
            }
        }
        return fresh;
    }

    /// <summary>
    /// Maya hasari sisirir (combat.json swell): eklenen boy = hasar / (azami can × perMaxHp). BurstAt'e varinca patlar:
    /// (true, cevreye hasar, kendine hasar, yaricap); patlayan kayittan silinir.
    /// </summary>
    public (bool burst, float aoe, float self, float radius) AddSwell(int id, float mayaDamage, float maxHp, SwellRuleDef r)
    {
        float add = mayaDamage / Math.Max(1f, maxHp * r.PerMaxHp);
        float s = _swell.GetValueOrDefault(id) + add;
        if (s < r.BurstAt) { _swell[id] = s; return (false, 0, 0, 0); }
        _swell.Remove(id);
        return (true, maxHp * r.BurstOfMaxHp, maxHp * r.SelfOfMaxHp, r.BurstRadius);
    }

    public void Prune(IReadOnlySet<int> alive)
    {
        foreach (var k in _swell.Keys.Where(k => !alive.Contains(k)).ToList()) _swell.Remove(k);
        foreach (var k in _burn.Keys.Where(k => !alive.Contains(k)).ToList()) _burn.Remove(k);
    }
}
