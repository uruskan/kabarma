namespace Kabarma.Sim;

// Mudur (Genel Merkez boss'u; ICERIK §4.4, GDD §19.5). Duz, serilestirilebilir durum (TEKNIK §9) ve saf kurallar; konum,
// hareket ve gorunus Godot katmaninda (game/scripts/holding/). Asamalar: 0 avlu (zirhli kamyon), 1 yonetim kati
// (kalkan jeneratorleri, hoparlorler), 2 cati (zeplin, balonlar), 3 yenildi.

public sealed class MudurFight
{
    public int CampId { get; set; }
    /// <summary>0 avlu, 1 yonetim kati, 2 cati, 3 yenildi.</summary>
    public int Stage { get; set; }
    public int TeamSize { get; set; } = 1;
    public float TruckHp { get; set; }
    public float TruckMax { get; set; }
    public float BossHp { get; set; }
    public float BossMax { get; set; }
    /// <summary>Kalan can: jeneratorler, hoparlorler, balonlar (0: yikildi).</summary>
    public List<float> Generators { get; set; } = new();
    public List<float> Speakers { get; set; } = new();
    public List<float> Balloons { get; set; } = new();
    public float CallT { get; set; }
    public float Time { get; set; }
    /// <summary>Asama gecisinde kisa hasar almama (sn).</summary>
    public float ShieldT { get; set; }
    public int Calls { get; set; }
    public bool Defeated { get; set; }
    public Dictionary<string, float> DamageByPart { get; set; } = new();
}

public enum MudurCue { StageChanged, CallUstabasi, Defeated }

public readonly record struct MudurCommand(MudurCue Cue, int Stage = 0, int Count = 0);

public static class MudurRules
{
    public static float TeamMult(HoldingMudurDef d, int team) => 1f + d.TeamHpPerExtra * (Math.Max(1, team) - 1);

    /// <summary>Genel Merkez'in Cekirdegi dusunce: avlu asamasi baslar (kamyon tam can).</summary>
    public static MudurFight Start(HoldingFile h, int campId, int team)
    {
        var d = h.Mudur;
        float m = TeamMult(d, team);
        return new MudurFight
        {
            CampId = campId, TeamSize = Math.Max(1, team), Stage = 0,
            TruckMax = d.Truck.Hp * m, TruckHp = d.Truck.Hp * m,
            BossMax = d.Boss.Hp * m, BossHp = d.Boss.Hp * m,
            ShieldT = 1.5f,
        };
    }

    public static bool GeneratorsDown(MudurFight f) => f.Generators.All(g => g <= 0);
    public static bool SpeakersAlive(MudurFight f) => f.Speakers.Any(s => s > 0);
    public static int BalloonsLeft(MudurFight f) => f.Balloons.Count(b => b > 0);

    /// <summary>Hoparlor calisirken Bekci ve oteki birimler hizlanir (ICERIK §4.4).</summary>
    public static float UnitSpeedMult(HoldingFile h, MudurFight? f) => f != null && f.Stage == 1 && SpeakersAlive(f) ? h.Mudur.Boss.SpeechSpeedMult : 1f;

    /// <summary>
    /// Parcaya vurus; verilen (azaltilmis) hasar doner. Parca: "kasa" (kamyon govdesi, zirhli), "motor" (arkadaki kapak: zirhsiz,
    /// MotorMult kat), "jenerator:i", "hoparlor:i", "balon:i", "mudur" (yaya Mudur: jeneratorler kapanmadan ve balonlar inmeden hasar yok).
    /// Asama bitince komutlar cmds'e eklenir.
    /// </summary>
    public static float Hit(MudurFight f, HoldingFile h, string part, float dmg, List<MudurCommand>? cmds = null)
    {
        if (f.Defeated || dmg <= 0 || f.ShieldT > 0) return 0;
        var d = h.Mudur;
        float done = 0;
        int i = Index(part, out string kind);
        switch (kind)
        {
            case "kasa" when f.Stage == 0: done = Take(ref f, nameof(f.TruckHp), dmg * (1f - Math.Clamp(d.Truck.BodyArmor, 0f, 0.95f))); break;
            case "motor" when f.Stage == 0: done = Take(ref f, nameof(f.TruckHp), dmg * d.Truck.MotorMult); break;
            case "jenerator" when f.Stage == 1 && i >= 0 && i < f.Generators.Count: done = TakeAt(f.Generators, i, dmg); break;
            case "hoparlor" when f.Stage == 1 && i >= 0 && i < f.Speakers.Count: done = TakeAt(f.Speakers, i, dmg); break;
            case "balon" when f.Stage == 2 && i >= 0 && i < f.Balloons.Count: done = TakeAt(f.Balloons, i, dmg); break;
            case "mudur" when f.Stage == 1 && GeneratorsDown(f): done = Take(ref f, nameof(f.BossHp), dmg); break;
            case "mudur" when f.Stage == 2 && BalloonsLeft(f) == 0: done = Take(ref f, nameof(f.BossHp), dmg); break;
        }
        if (done <= 0) return 0;
        f.DamageByPart[kind] = f.DamageByPart.GetValueOrDefault(kind) + done;
        Advance(f, h, cmds);
        return done;
    }

    static int Index(string part, out string kind)
    {
        int c = part.IndexOf(':');
        if (c < 0) { kind = part; return -1; }
        kind = part[..c];
        return int.TryParse(part[(c + 1)..], out int n) ? n : -1;
    }

    static float Take(ref MudurFight f, string field, float amount)
    {
        if (field == nameof(f.TruckHp)) { float d = Math.Min(f.TruckHp, amount); f.TruckHp -= d; return d; }
        float b = Math.Min(f.BossHp, amount); f.BossHp -= b; return b;
    }

    static float TakeAt(List<float> l, int i, float amount)
    {
        if (l[i] <= 0) return 0;
        float d = Math.Min(l[i], amount);
        l[i] -= d;
        return d;
    }

    static void Advance(MudurFight f, HoldingFile h, List<MudurCommand>? cmds)
    {
        var d = h.Mudur;
        if (f.Stage == 0 && f.TruckHp <= 0)
        {
            f.Stage = 1; f.ShieldT = 2f; f.CallT = d.Boss.CallEvery * 0.5f;
            f.Generators = Enumerable.Repeat(d.Boss.GeneratorHp, d.Boss.Generators).ToList();
            f.Speakers = Enumerable.Repeat(d.Boss.SpeakerHp, d.Boss.Speakers).ToList();
            cmds?.Add(new MudurCommand(MudurCue.StageChanged, 1));
        }
        else if (f.Stage == 1 && f.BossHp <= f.BossMax * d.Boss.RoofAt)
        {
            f.Stage = 2; f.ShieldT = 2f;
            f.Balloons = Enumerable.Repeat(h.Mudur.Roof.BalloonHp, d.Roof.Balloons).ToList();
            f.Generators.Clear(); f.Speakers.Clear();
            cmds?.Add(new MudurCommand(MudurCue.StageChanged, 2));
        }
        else if (f.Stage == 2 && f.BossHp <= 0)
        {
            f.Stage = 3; f.Defeated = true;
            cmds?.Add(new MudurCommand(MudurCue.Defeated, 3));
        }
    }

    /// <summary>
    /// Zaman: kalkan; 1. asamada Mekanikler kamyonu onarir (mekanik adedi × RepairPerMechanic / sn, tam cana dek); 2. asamada
    /// her CallEvery'de Ustabasi cagirir (hoparlor calisirken).
    /// </summary>
    public static List<MudurCommand> Tick(MudurFight f, HoldingFile h, float dt, int mechanicsAlive)
    {
        var cmds = new List<MudurCommand>();
        if (f.Defeated) return cmds;
        f.Time += dt;
        if (f.ShieldT > 0) f.ShieldT = Math.Max(0, f.ShieldT - dt);
        var d = h.Mudur;
        if (f.Stage == 0 && f.TruckHp > 0 && mechanicsAlive > 0)
            f.TruckHp = Math.Min(f.TruckMax, f.TruckHp + mechanicsAlive * d.Truck.RepairPerMechanic * dt);
        if (f.Stage == 1)
        {
            f.CallT -= dt;
            if (f.CallT <= 0)
            {
                f.CallT = d.Boss.CallEvery;
                f.Calls++;
                cmds.Add(new MudurCommand(MudurCue.CallUstabasi, 1, d.Boss.CallCount));
            }
        }
        return cmds;
    }
}

/// <summary>
/// Kamp yeri cozumu (GDD §19.1): sitenin Anchor'u verildiyse o; yoksa biyomun sinirlarinda merkeze en yakin, biyom agirligi
/// en az MinWeight olan nokta (20 m izgara). Durumsuz: ayni veri ayni yeri verir.
/// </summary>
public static class HoldingSites
{
    public const float MinWeight = 0.9f;

    public static (float x, float z) Resolve(HoldingFile h, HoldingSiteDef s, BiomeMap map, float edge)
    {
        if (s.Anchor.Count >= 2) return (s.Anchor[0], s.Anchor[1]);
        var (x0, z0, x1, z1) = map.Bounds(s.Biome);
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        (float x, float z) best = (cx, cz);
        float bestD = float.MaxValue;
        for (float z = Math.Max(z0, -edge); z <= Math.Min(z1, edge); z += 20f)
            for (float x = Math.Max(x0, -edge); x <= Math.Min(x1, edge); x += 20f)
            {
                if (map.Weight(s.Biome, x, z) < MinWeight) continue;
                float d = (x - cx) * (x - cx) + (z - cz) * (z - cz);
                if (d < bestD) { bestD = d; best = (x, z); }
            }
        return best;
    }
}
