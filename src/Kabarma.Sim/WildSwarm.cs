using System.Numerics;

namespace Kabarma.Sim;

/// <summary>
/// Suru (TEKNIK "Sururler": Cekirge, Vizviz): tek varlik. Durum merkez, hedef, can havuzu ve tohumdur; uyelerin yeri
/// tohumdan ve zamandan hesaplanir (ag ve kayit yalniz bu duz durumu tasir, istemci uyeleri ayni tohumla cizer).
/// </summary>
public sealed class SwarmState
{
    public int Id { get; set; }
    public string Species { get; set; } = "";
    public ulong Seed { get; set; } = 1;
    public float X { get; set; }
    public float Z { get; set; }
    public float HomeX { get; set; }
    public float HomeZ { get; set; }
    public float GoalX { get; set; }
    public float GoalZ { get; set; }
    public float WanderT { get; set; }
    public float Hp { get; set; }
    public bool Swollen { get; set; }
    public float ScatterT { get; set; }
    public float ScatterFromX { get; set; }
    public float ScatterFromZ { get; set; }
    public float FeedT { get; set; }
    public float StingCd { get; set; }
    public ulong Rand { get; set; } = 0x2545F4914F6CDD1DUL;
    public float Next() { var r = Rand; var v = WildRandom.Next(ref r); Rand = r; return v; }
    public float Range(float min, float max) => min + Next() * (max - min);
}

public struct SwarmSense
{
    public float PlayerX, PlayerZ;
    public bool Night;
    public bool HasTarget;
    public float TargetX, TargetZ;
}

public struct SwarmStep
{
    public WildMode Mode;
    public WildEvent Event;
    public bool Sting;
}

public static class Swarms
{
    public static void Init(CreatureDef c, SwarmState s, float x, float z, ulong seed)
    {
        s.Species = c.Id; s.Seed = seed == 0 ? 1 : seed; s.Rand = s.Seed * 0x9E3779B97F4A7C15UL | 1;
        s.X = s.HomeX = s.GoalX = x; s.Z = s.HomeZ = s.GoalZ = z;
        s.Hp = c.Swarm!.HpPool;
    }

    public static int Count(SwarmDef d, bool swollen) => (int)MathF.Round(d.Members * (swollen ? d.SwollenMembers : 1f));

    public static float Radius(SwarmDef d, SwarmState s) =>
        d.Radius * (s.Swollen ? d.SwollenRadius : 1f) * (s.ScatterT > 0 ? 1f + (d.ScatterMult - 1f) * MathF.Min(1f, s.ScatterT / MathF.Max(0.01f, d.ScatterSeconds) * 2f) : 1f);

    /// <summary>i. uyenin merkeze gore yeri (m) t aninda. Durumsuz: ayni tohum, ayni sonuc.</summary>
    public static Vector3 Member(SwarmDef d, SwarmState s, int i, float t)
    {
        float a0 = WildRandom.Hash(s.Seed, i, 1) * MathF.Tau;
        float rf = 0.25f + 0.75f * MathF.Sqrt(WildRandom.Hash(s.Seed, i, 2));
        float hf = WildRandom.Hash(s.Seed, i, 3);
        float ph = WildRandom.Hash(s.Seed, i, 4) * MathF.Tau;
        float dir = WildRandom.Hash(s.Seed, i, 5) < 0.5f ? -1f : 1f;
        float spd = 0.6f + 0.8f * WildRandom.Hash(s.Seed, i, 6);
        float a = a0 + dir * t * d.Orbit * MathF.Tau * spd;
        float r = Radius(d, s) * rf * (0.85f + 0.15f * MathF.Sin(t * 1.3f + ph));
        float y = d.Height * (0.4f + 1.2f * hf) + MathF.Sin(t * 3.1f * spd + ph) * d.Bob;
        return new Vector3(MathF.Cos(a) * r, MathF.Max(0.05f, y), MathF.Sin(a) * r);
    }

    /// <summary>Can havuzundan hasar (GDD §14.3: suru tek can havuzu). Dagilir; havuz biterse true.</summary>
    public static bool Damage(CreatureDef c, SwarmState s, float dmg, float fromX, float fromZ)
    {
        s.Hp -= dmg;
        s.ScatterT = c.Swarm!.ScatterSeconds;
        s.ScatterFromX = fromX; s.ScatterFromZ = fromZ;
        return s.Hp <= 0;
    }

    public static SwarmStep Step(CreatureDef c, SwarmState s, SwarmSense sense, float dt)
    {
        var d = c.Swarm!;
        var w = c.Wild ?? new WildDef();
        s.StingCd = MathF.Max(0, s.StingCd - dt);
        var step = new SwarmStep { Mode = WildMode.Walk };
        float speed = c.Speed * w.WalkSpeedMult;
        if (!WildBrain.Awake(w, sense.Night))
        {
            // Uyku: Vizviz kovanina doner, Cekirge yere iner (merkez yerinde).
            s.GoalX = s.HomeX; s.GoalZ = s.HomeZ;
            step.Mode = WildMode.Sleep;
        }
        else if (s.ScatterT > 0)
        {
            s.ScatterT = MathF.Max(0, s.ScatterT - dt);
            float ax = s.X - s.ScatterFromX, az = s.Z - s.ScatterFromZ, l = MathF.Max(0.01f, MathF.Sqrt(ax * ax + az * az));
            s.GoalX = s.X + ax / l * 5f; s.GoalZ = s.Z + az / l * 5f;
            speed = c.Speed * w.RunSpeedMult;
            step.Mode = WildMode.Flee;
        }
        else if (sense.HasTarget && !d.Hive || sense.HasTarget && Dist(sense.TargetX, sense.TargetZ, s.HomeX, s.HomeZ) < d.HiveRange)
        {
            s.GoalX = sense.TargetX; s.GoalZ = sense.TargetZ;
            if (Dist(s.X, s.Z, s.GoalX, s.GoalZ) < 0.6f)
            {
                step.Mode = WildMode.Feed;
                s.FeedT += dt;
                if (s.FeedT >= w.TargetSeconds) { s.FeedT = 0; step.Event = WildEvent.Took; s.WanderT = 0; }
            }
            else step.Mode = WildMode.Approach;
        }
        else
        {
            s.WanderT -= dt;
            if (s.WanderT <= 0)
            {
                s.WanderT = s.Range(w.RetargetMin, w.RetargetMax);
                float range = d.Hive ? d.HiveRange : w.WanderRadius;
                float a = s.Next() * MathF.Tau, r = MathF.Sqrt(s.Next()) * range;
                s.GoalX = s.HomeX + MathF.Cos(a) * r; s.GoalZ = s.HomeZ + MathF.Sin(a) * r;
            }
        }
        // Kovan bekcisi: kovana yaklasan oyuncuya suru gider ve sokar (M2: kucuk hasar; tam savas M3).
        if (d.Hive && d.GuardRadius > 0 && WildBrain.Awake(w, sense.Night) && Dist(sense.PlayerX, sense.PlayerZ, s.HomeX, s.HomeZ) < d.GuardRadius && s.ScatterT <= 0)
        {
            s.GoalX = sense.PlayerX; s.GoalZ = sense.PlayerZ;
            speed = c.Speed * w.RunSpeedMult;
            step.Mode = WildMode.Charge;
            if (Dist(s.X, s.Z, sense.PlayerX, sense.PlayerZ) < d.Radius && s.StingCd <= 0)
            {
                s.StingCd = d.StingInterval;
                step.Sting = true; step.Event = WildEvent.Struck;
            }
        }
        float gx = s.GoalX - s.X, gz = s.GoalZ - s.Z, gl = MathF.Sqrt(gx * gx + gz * gz);
        if (gl > 0.01f)
        {
            float mv = MathF.Min(gl, speed * dt);
            s.X += gx / gl * mv; s.Z += gz / gl * mv;
        }
        return step;
    }

    static float Dist(float ax, float az, float bx, float bz) => MathF.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
}

/// <summary>Biyom dogma satirlari (biomes.json "spawns"): nokta secimi ve suru boyu.</summary>
public static class WildSpawns
{
    /// <summary>Satirin halkasinda (Ocak cevresi) ya da bolgesinde (Center, Radius) rastgele nokta [x, z].</summary>
    public static (float X, float Z) Point(SpawnDef row, float ocakX, float ocakZ, ref ulong rand)
    {
        float a = WildRandom.Next(ref rand) * MathF.Tau;
        if (row.Center.Count >= 2 && row.Radius > 0)
        {
            float r = MathF.Sqrt(WildRandom.Next(ref rand)) * row.Radius;
            return (row.Center[0] + MathF.Cos(a) * r, row.Center[1] + MathF.Sin(a) * r);
        }
        float d = WildRandom.Range(ref rand, row.MinDistance, row.MaxDistance);
        return (ocakX + MathF.Cos(a) * d, ocakZ + MathF.Sin(a) * d);
    }

    /// <summary>Satirin turu oyunda var mi (veride tanimli, yabani ya da suru tavri var).</summary>
    public static bool Spawnable(GameData data, SpawnDef row) =>
        data.Creatures.Creatures.Any(c => c.Id == row.Creature && (c.Wild != null || c.Swarm != null));
}
