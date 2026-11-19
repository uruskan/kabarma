using System.Numerics;

namespace Kabarma.Sim;

/// <summary>Kurulu bir tuzak (GDD §16.4). Duz ve serilestirilebilir.</summary>
public sealed class TrapState
{
    public int Id { get; set; }
    public string Def { get; set; } = "";
    public float[] Pos { get; set; } = new float[3];
    public float Yaw { get; set; }
    public float Hp { get; set; }
    /// <summary>Cukur Kapagi acik mi.</summary>
    public bool Open { get; set; }
    /// <summary>spikes: sonraki vurusa; launch: yeniden kurulmaya kalan sure.</summary>
    public float Cooldown { get; set; }
    /// <summary>Cukurda tutulan hedefler (hedef kimligi).</summary>
    public List<int> Held { get; set; } = new();
    /// <summary>Yapiskan Zemin: yeniden tutulamayacagi sure (hedef kimligi -> sn).</summary>
    public Dictionary<int, float> Immune { get; set; } = new();
    public int Triggers { get; set; }
    /// <summary>Yag Hendegi: alev duvarinin kalan suresi (Open iken).</summary>
    public float Fuel { get; set; }

    public Vector3 At => new(Pos[0], Pos[1], Pos[2]);
}

public enum TrapFx { Damage, Slow, Hold, Launch, Fall, Release, Shock, Burn, Blast, Freeze }

/// <summary>Tuzagin bir hedefe etkisi; Godot katmani uygular. Dir: fırlatma yonu (yatay, birim).</summary>
public readonly record struct TrapEffect(int Target, TrapFx Kind, float Value, float Seconds, Vector3 Dir);

/// <summary>Tuzak kurallari (GDD §16.4 M3): Diken Tarlasi, Yapiskan Zemin, Firlatma Pedi, Cukur Kapagi.</summary>
public static class TrapRules
{
    /// <summary>Tuzagin yerel -Z yonu (Godot ileri), yaw'a gore.</summary>
    public static Vector3 Forward(float yaw) => new(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));

    /// <summary>Nokta tuzagin kare alaninda mi (yerel eksenlerde, yari kenar + hedef yaricapinin yarisi).</summary>
    public static bool Inside(TrapState s, TrapDef d, Vector3 p, float radius = 0f)
    {
        var r = p - s.At;
        float c = MathF.Cos(s.Yaw), sn = MathF.Sin(s.Yaw);
        // Godot Basis(Up, yaw) tersi: yerel x = c*rx - s*rz, yerel z = s*rx + c*rz
        float lx = c * r.X - sn * r.Z, lz = sn * r.X + c * r.Z;
        float h = d.HalfSize + radius * 0.5f;
        return MathF.Abs(lx) <= h && MathF.Abs(lz) <= h && MathF.Abs(r.Y) < 2.5f;
    }

    public static bool Broken(TrapState s) => s.Hp <= 0;

    static void Wear(TrapState s, TrapDef d) { s.Hp = Math.Max(0f, s.Hp - d.WearPerTrigger); s.Triggers++; }

    /// <summary>Bir adim: alandaki hedeflere etkiler. Oyuncular yalniz Firlatma Pedi'nde (affectsPlayers).</summary>
    public static List<TrapEffect> Step(TrapState s, TrapDef d, IReadOnlyList<DefenseTarget> ts, float dt, bool powered = true, IReadOnlySet<int>? burning = null)
    {
        var fx = new List<TrapEffect>();
        var alive = new HashSet<int>(ts.Where(t => t.Hp > 0).Select(t => t.Id));
        foreach (var k in s.Immune.Keys.ToList()) if ((s.Immune[k] -= dt) <= 0 || !alive.Contains(k)) s.Immune.Remove(k);
        s.Held.RemoveAll(id => !alive.Contains(id));
        s.Cooldown = Math.Max(0f, s.Cooldown - dt);

        if (d.Kind == "pit")
        {
            if (!s.Open)
            {
                foreach (var id in s.Held) fx.Add(new TrapEffect(id, TrapFx.Release, 0, 0, Vector3.Zero));
                s.Held.Clear();
                return fx;
            }
            foreach (var id in s.Held) fx.Add(new TrapEffect(id, TrapFx.Hold, 0, 0.25f, Vector3.Zero)); // tutmaya devam
        }
        if (Broken(s)) return fx;
        if (d.Powered && !powered) return fx; // M4: guc ister (Elektrikli Cit, Dondurucu Puskurtucu)

        var inside = ts.Where(t => t.Hp > 0 && (!t.IsPlayer || (d.Kind == "launch" && d.AffectsPlayers)) && Inside(s, d, t.Pos, t.Radius)).ToList();
        switch (d.Kind)
        {
            case "spikes":
                if (s.Cooldown > 0 || inside.Count == 0) break;
                foreach (var t in inside)
                {
                    fx.Add(new TrapEffect(t.Id, TrapFx.Damage, d.Dps * d.TriggerEvery, 0, Vector3.Zero));
                    fx.Add(new TrapEffect(t.Id, TrapFx.Slow, d.Slow, d.SlowSeconds, Vector3.Zero));
                }
                s.Cooldown = d.TriggerEvery;
                Wear(s, d);
                break;
            case "sticky":
                foreach (var t in inside)
                {
                    if (s.Immune.ContainsKey(t.Id)) continue;
                    fx.Add(new TrapEffect(t.Id, TrapFx.Hold, 0, d.HoldSeconds, Vector3.Zero));
                    s.Immune[t.Id] = d.HoldSeconds + d.ImmuneSeconds;
                    Wear(s, d);
                }
                break;
            case "launch":
                if (s.Cooldown > 0 || inside.Count == 0) break;
                var dir = Forward(s.Yaw);
                foreach (var t in inside) fx.Add(new TrapEffect(t.Id, TrapFx.Launch, t.IsPlayer ? 0 : d.Damage, d.FlightSeconds, dir));
                s.Cooldown = d.RearmSeconds;
                Wear(s, d);
                break;
            case "fence": // Elektrikli Cit: dokunana Sok (durdurur) + hasar, yakindakilere zincir
                if (s.Cooldown > 0 || inside.Count == 0) break;
                foreach (var t in inside)
                {
                    fx.Add(new TrapEffect(t.Id, TrapFx.Damage, d.Dps * d.TriggerEvery, 0, Vector3.Zero));
                    fx.Add(new TrapEffect(t.Id, TrapFx.Shock, d.ChainTargets, d.StunSeconds, Vector3.Zero));
                }
                s.Cooldown = d.TriggerEvery;
                Wear(s, d);
                break;
            case "mine": // Mayin: ilk basan patlatir; alan hasari mesafeyle azalir; tek kullanimlik (can 0: onarimla kurulur)
                if (inside.Count == 0) break;
                foreach (var t in ts.Where(t => t.Hp > 0 && !t.IsPlayer))
                {
                    var r = t.Pos - s.At; r.Y = 0;
                    float dist = r.Length();
                    if (dist > d.Radius + t.Radius) continue;
                    var away = dist < 1e-3f ? Vector3.UnitZ : r / dist;
                    fx.Add(new TrapEffect(t.Id, TrapFx.Blast, d.Damage * (1f - 0.5f * Math.Clamp(dist / Math.Max(0.1f, d.Radius), 0f, 1f)), 0.4f, away));
                }
                s.Triggers++;
                s.Hp = 0;
                break;
            case "oil": // Yag Hendegi: icindekiler kayar (yavaslar); tutusunca alev duvari
                if (!s.Open && burning != null && inside.Any(t => burning.Contains(t.Id))) Ignite(s, d); // yanan hedef yagi tutusturur
                if (s.Open)
                {
                    s.Fuel -= dt;
                    if (s.Fuel <= 0f) { s.Open = false; s.Hp = 0; s.Triggers++; break; } // yag bitti: onarimla (yeni yag) kurulur
                    if (s.Cooldown <= 0f)
                    {
                        foreach (var t in inside)
                        {
                            fx.Add(new TrapEffect(t.Id, TrapFx.Damage, d.Dps * d.TriggerEvery, 0, Vector3.Zero));
                            fx.Add(new TrapEffect(t.Id, TrapFx.Burn, d.BurnDps, d.BurnSeconds, Vector3.Zero));
                        }
                        s.Cooldown = d.TriggerEvery;
                    }
                }
                else if (s.Cooldown <= 0f && inside.Count > 0)
                {
                    foreach (var t in inside) fx.Add(new TrapEffect(t.Id, TrapFx.Slow, d.Slow, d.SlowSeconds, Vector3.Zero));
                    s.Cooldown = 0.25f;
                }
                break;
            case "freezer": // Dondurucu Puskurtucu: alan Donma
                if (s.Cooldown > 0 || inside.Count == 0) break;
                foreach (var t in inside)
                {
                    fx.Add(new TrapEffect(t.Id, TrapFx.Damage, d.Dps * d.TriggerEvery, 0, Vector3.Zero));
                    fx.Add(new TrapEffect(t.Id, TrapFx.Freeze, d.Slow, d.FreezeSeconds, Vector3.Zero));
                }
                s.Cooldown = d.TriggerEvery;
                Wear(s, d);
                break;
            case "pit":
                foreach (var t in inside)
                {
                    if (t.IsPlayer || s.Held.Contains(t.Id) || s.Held.Count >= d.Capacity) continue;
                    s.Held.Add(t.Id);
                    fx.Add(new TrapEffect(t.Id, TrapFx.Fall, d.Depth, 0.25f, Vector3.Zero));
                    Wear(s, d);
                }
                break;
        }
        return fx;
    }

    /// <summary>Cukur Kapagi ac/kapa (E). Kapaninca tutulanlar bir sonraki adimda birakilir. Yag Hendegi: E tutusturur.</summary>
    public static void Toggle(TrapState s, TrapDef? d = null)
    {
        if (d is { Kind: "oil" }) { if (!s.Open && !Broken(s)) Ignite(s, d); return; }
        s.Open = !s.Open;
    }

    /// <summary>Yag Hendegi tutusur: alev duvari IgniteSeconds surer.</summary>
    public static void Ignite(TrapState s, TrapDef d)
    {
        if (Broken(s)) return;
        s.Open = true;
        s.Fuel = d.IgniteSeconds;
        s.Cooldown = 0f;
    }

    /// <summary>Guc ya da cephane gerektiren tuzak/kule kimligi icin kurulu durum etiketi (Hud).</summary>
    public static bool NeedsPower(TrapDef d) => d.Powered;
}
