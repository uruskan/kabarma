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

    public Vector3 At => new(Pos[0], Pos[1], Pos[2]);
}

public enum TrapFx { Damage, Slow, Hold, Launch, Fall, Release }

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
    public static List<TrapEffect> Step(TrapState s, TrapDef d, IReadOnlyList<DefenseTarget> ts, float dt)
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

    /// <summary>Cukur Kapagi ac/kapa (E). Kapaninca tutulanlar bir sonraki adimda birakilir.</summary>
    public static void Toggle(TrapState s) => s.Open = !s.Open;
}
