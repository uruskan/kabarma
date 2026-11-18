namespace Kabarma.Sim;

/// <summary>Suren Kadim Tas denemesi (GDD 17.1, ICERIK 7). Duz; kayitta yoktur (yuklemede deneme bastan baslar).</summary>
public sealed class TrialRun
{
    public string StoneId { get; set; } = "";
    public float Time { get; set; }
    /// <summary>parkur: siradaki halka.</summary>
    public int Ring { get; set; }
    /// <summary>koru: korunan Gidak'in cani.</summary>
    public float GuardHp { get; set; }
    /// <summary>koru: siradaki hayalet grubuna kalan sure.</summary>
    public float NextSpawn { get; set; }
    /// <summary>dalga: suren dalga (0'dan) ve dalganin baslatilip baslatilmadigi.</summary>
    public int Wave { get; set; } = -1;
    /// <summary>gizlen: kesintisiz gorulme suresi.</summary>
    public float SeenT { get; set; }
    public bool Won { get; set; }
    public bool Lost { get; set; }
    public string Reason { get; set; } = "";
    public bool Over => Won || Lost;
}

/// <summary>Denemenin Godot'tan okudugu durum (o karedeki).</summary>
public struct TrialSense
{
    /// <summary>Deneyenin tasa goreli konumu (dx, dz) ve yerden yuksekligi (gogus).</summary>
    public float Dx, Dz, Height;
    /// <summary>Deneyen (ya da NoDown denemesinde ekipten biri) yere dustu.</summary>
    public bool Down;
    /// <summary>Sahnedeki canli deneme hayaleti.</summary>
    public int Alive;
    /// <summary>Bu karede korunan Gidak'a gelen hasar.</summary>
    public float GuardDamage;
}

/// <summary>Bir karede denemenin istedigi: dogacak hayalet sayisi, bitis.</summary>
public struct TrialStep
{
    public int Spawn;
    public bool Won, Lost;
    public int RingReached;
}

/// <summary>Dunyanin Kadim Tas ilerlemesi: tamamlanan taslar ve yeniden deneme beklemeleri.</summary>
public sealed class KadimTasProgress
{
    public List<string> Done { get; set; } = new();
    public Dictionary<string, float> Retry { get; set; } = new();
}

/// <summary>Kadim Tas denemelerinin kurali: baslat, kare kare ilerlet, kazaninca gucu ac.</summary>
public static class KadimTasRules
{
    /// <summary>Denemeyi baslatir. Olmuyorsa null ve Turkce neden.</summary>
    public static TrialRun? Start(AbilitiesFile f, KadimTasProgress p, string stoneId, out string reason)
    {
        var s = f.Stone(stoneId);
        reason = "";
        if (s == null) { reason = "Bilinmeyen taş"; return null; }
        if (p.Retry.TryGetValue(stoneId, out var r) && r > 0) { reason = $"Taş dinleniyor: {MathF.Ceiling(r)} sn"; return null; }
        var run = new TrialRun { StoneId = stoneId, GuardHp = s.GuardHp, NextSpawn = 0f };
        return run;
    }

    /// <summary>Taslarin yeniden deneme beklemesini indirir.</summary>
    public static void TickRetry(KadimTasProgress p, float dt)
    {
        foreach (var k in p.Retry.Keys.ToList())
        {
            float v = p.Retry[k] - dt;
            if (v <= 0) p.Retry.Remove(k); else p.Retry[k] = v;
        }
    }

    /// <summary>Gizlen denemesi: deneyen o an bir gozcunun gorus konisinde mi (tasa goreli dx, dz).</summary>
    public static bool Seen(KadimTasDef s, float dx, float dz)
    {
        foreach (var w in s.Watchers)
        {
            float vx = dx - w[0], vz = dz - w[1];
            float d = MathF.Sqrt(vx * vx + vz * vz);
            if (d > s.Sight) continue;
            if (d < 0.5f) return true;
            float yaw = w[2] * MathF.PI / 180f;
            float fx = MathF.Sin(yaw), fz = MathF.Cos(yaw);
            float cos = (vx * fx + vz * fz) / d;
            if (cos >= MathF.Cos(s.Fov * 0.5f * MathF.PI / 180f)) return true;
        }
        return false;
    }

    /// <summary>Bir kare ilerletir. Bitince sonucu run'a yazar; kayipta tas dinlenmeye gecer, kazancta ilerlemeye yazilir.</summary>
    public static TrialStep Tick(AbilitiesFile f, KadimTasProgress p, TrialRun run, TrialSense sense, float dt)
    {
        var step = new TrialStep { RingReached = -1 };
        if (run.Over) return step;
        var s = f.Stone(run.StoneId)!;
        run.Time += dt;

        float far = MathF.Sqrt(sense.Dx * sense.Dx + sense.Dz * sense.Dz);
        if (far > f.Trial.LeaveRadius) return Finish(f, p, run, false, "Taştan uzaklaştın", ref step);
        if (sense.Down) return Finish(f, p, run, false, s.NoDown ? "Ekipten biri yere düştü" : "Yere düştün", ref step);

        switch (s.Trial)
        {
            case "parkur":
            {
                if (run.Ring < s.Rings.Count)
                {
                    var g = s.Rings[run.Ring];
                    float rx = sense.Dx - g[0], rz = sense.Dz - g[1], ry = sense.Height - (g.Length > 2 ? g[2] : 1f);
                    if (rx * rx + ry * ry + rz * rz <= s.RingRadius * s.RingRadius) { step.RingReached = run.Ring; run.Ring++; }
                }
                if (run.Ring >= s.Rings.Count) return Finish(f, p, run, true, "", ref step);
                if (run.Time > s.Seconds) return Finish(f, p, run, false, "Süre doldu", ref step);
                break;
            }
            case "koru":
            {
                run.GuardHp -= MathF.Max(0, sense.GuardDamage);
                if (run.GuardHp <= 0) return Finish(f, p, run, false, "Gıdak düştü", ref step);
                if (run.NextSpawn <= 0) { run.NextSpawn += s.Every; step.Spawn = s.PerWave; }
                run.NextSpawn -= dt;
                if (run.Time >= s.Seconds) return Finish(f, p, run, true, "", ref step);
                break;
            }
            case "dalga":
            {
                if (run.Wave < 0 || sense.Alive == 0)
                {
                    if (run.Wave >= 0 && run.Wave >= s.Waves.Count - 1) return Finish(f, p, run, true, "", ref step);
                    run.Wave++;
                    step.Spawn = s.Waves[run.Wave];
                }
                break;
            }
            case "gizlen":
            {
                run.SeenT = Seen(s, sense.Dx, sense.Dz) ? run.SeenT + dt : 0f;
                if (run.SeenT > s.SeenLimit) return Finish(f, p, run, false, "Görüldün", ref step);
                float gx = sense.Dx - s.Goal[0], gz = sense.Dz - s.Goal[1];
                if (gx * gx + gz * gz <= s.GoalRadius * s.GoalRadius) return Finish(f, p, run, true, "", ref step);
                if (run.Time > s.Seconds) return Finish(f, p, run, false, "Süre doldu", ref step);
                break;
            }
            default:
                return Finish(f, p, run, false, $"Bilinmeyen deneme '{s.Trial}'", ref step);
        }
        return step;
    }

    /// <summary>Denemeyi deneyen vazgecince ya da baglanti kopunca bitirir (kayip).</summary>
    public static void Abort(AbilitiesFile f, KadimTasProgress p, TrialRun run, string reason)
    {
        var step = new TrialStep();
        if (!run.Over) Finish(f, p, run, false, reason, ref step);
    }

    static TrialStep Finish(AbilitiesFile f, KadimTasProgress p, TrialRun run, bool won, string reason, ref TrialStep step)
    {
        run.Won = won;
        run.Lost = !won;
        run.Reason = reason;
        step.Won = won;
        step.Lost = !won;
        if (won) { if (!p.Done.Contains(run.StoneId)) p.Done.Add(run.StoneId); }
        else p.Retry[run.StoneId] = f.Trial.RetrySeconds;
        return step;
    }
}
