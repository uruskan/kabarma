namespace Kabarma.Sim;

// Yabani yaratiklarin gunduz tavri (GDD §16.2 "Gunduz tavri", ICERIK §3.8): veriyle surulen durum makinesi.
// Butun durum duz alanlardir (kayda ve aga hazir, TEKNIK §9): Godot tipi, dugum basvurusu yok; suru kimlikle baglanir.
// Etkiler (ekin kaybi, yumurta, hasar) sahibi olan sisteme olay olarak doner (WildEvent); burada uygulanmaz.

public enum WildMode { Idle, Graze, Rest, Walk, Alert, Flee, Approach, Feed, Warn, Charge, Attack, Sleep }

public enum WildEvent { None, Alerted, Fled, Warned, Struck, Took, Woke, Slept }

/// <summary>Bir yabani surunun ortak durumu: ev, dolasma noktasi, kacis.</summary>
public sealed class WildHerdState
{
    public int Id { get; set; }
    public string Species { get; set; } = "";
    public float HomeX { get; set; }
    public float HomeZ { get; set; }
    public float WanderX { get; set; }
    public float WanderZ { get; set; }
    public float WanderT { get; set; }
    public float FleeT { get; set; }
    public float FleeFromX { get; set; }
    public float FleeFromZ { get; set; }
    public ulong Rand { get; set; } = 0x2545F4914F6CDD1DUL;
    public float Next() { var r = Rand; var v = WildRandom.Next(ref r); Rand = r; return v; }
    public float Range(float min, float max) => min + Next() * (max - min);
}

/// <summary>Bir yabani yaratigin durumu.</summary>
public sealed class WildState
{
    public int Id { get; set; }
    public int Herd { get; set; }
    public string Species { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    /// <summary>Surudeki yeri (merkeze gore ofset).</summary>
    public float SlotX { get; set; }
    public float SlotZ { get; set; }
    public WildMode Mode { get; set; } = WildMode.Idle;
    /// <summary>Durustaki kalan bekleme ya da moddaki gecen sure.</summary>
    public float ModeT { get; set; }
    public float AttackCd { get; set; }
    public float TargetT { get; set; }
    /// <summary>Durusun (bekleme, otlanma, dinlenme) suresi.</summary>
    public float Pause { get; set; }
    /// <summary>Yirtici: bu yaklasimda uyardi mi.</summary>
    public bool Warned { get; set; }
    public float Hp { get; set; }
    public bool Swollen { get; set; }
    /// <summary>Evcillesti: davranisi hayvancilik akisi surer (GDD §9).</summary>
    public bool Tamed { get; set; }
    public ulong Rand { get; set; } = 0x9E3779B97F4A7C15UL;
    public float Next() { var r = Rand; var v = WildRandom.Next(ref r); Rand = r; return v; }
    public float Range(float min, float max) => min + Next() * (max - min);
}

/// <summary>Yaratigin her adimda algiladigi dunya: oyuncu, gece, darbe, en yakin hedef.</summary>
public struct WildSense
{
    public float PlayerX, PlayerZ;
    public bool Night;
    /// <summary>Bu adimda vuruldu ya da yakininda ates edildi.</summary>
    public bool Hit;
    public bool HasTarget;
    public float TargetX, TargetZ;
}

/// <summary>Adimin sonucu: gidilecek nokta, hiz, klip yuvasi ve olay.</summary>
public struct WildStep
{
    public WildMode Mode;
    public float GoalX, GoalZ;
    public float Speed;
    public string Clip;
    public WildEvent Event;
    /// <summary>Hedefe donup bakar (uyari, saldiri, bakma).</summary>
    public bool Face;
    public float FaceX, FaceZ;
}

/// <summary>Durumdaki tohumla calisan kucuk rastgele (xorshift); durum kayda girer, sonuc tekrarlanir.</summary>
public static class WildRandom
{
    public static float Next(ref ulong s)
    {
        if (s == 0) s = 0x9E3779B97F4A7C15UL;
        s ^= s << 13; s ^= s >> 7; s ^= s << 17;
        return (s >> 40) / (float)(1UL << 24);
    }

    public static float Range(ref ulong s, float min, float max) => min + Next(ref s) * (max - min);

    /// <summary>Tohum ve sayidan karisik deger (suru uyeleri icin; durum tutmaz).</summary>
    public static float Hash(ulong seed, int i, int salt)
    {
        ulong x = seed ^ ((ulong)(uint)i * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)salt * 0xC2B2AE3D27D4EB4FUL);
        x ^= x >> 33; x *= 0xFF51AFD7ED558CCDUL; x ^= x >> 33; x *= 0xC4CEB9FE1A85EC53UL; x ^= x >> 33;
        return (x >> 40) / (float)(1UL << 24);
    }
}

public static class WildBrain
{
    /// <summary>Tur bu saatte uyanik mi ("day", "night", "always").</summary>
    public static bool Awake(WildDef w, bool night) => w.Active switch
    {
        "always" => true,
        "night" => night,
        _ => !night,
    };

    /// <summary>Kabarma gecesi yabaniler sahneden cekilir; dalga olarak gelirler (GDD §14.2).</summary>
    public static bool Present(bool kabarmaNight) => !kabarmaNight;


    static float Dist(float ax, float az, float bx, float bz) => MathF.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

    /// <summary>Surunun ortak saati: dolasma noktasi secimi, kacis sayaci.</summary>
    public static void StepHerd(WildDef w, WildHerdState h, float dt)
    {
        if (h.FleeT > 0) h.FleeT = MathF.Max(0, h.FleeT - dt);
        h.WanderT -= dt;
        if (h.WanderT > 0) return;
        h.WanderT = h.Range(w.RetargetMin, w.RetargetMax);
        float a = h.Next() * MathF.Tau, r = MathF.Sqrt(h.Next()) * w.WanderRadius;
        h.WanderX = h.HomeX + MathF.Cos(a) * r;
        h.WanderZ = h.HomeZ + MathF.Sin(a) * r;
    }

    /// <summary>Surunun tamami kacar (biri urkunce hepsi).</summary>
    public static void Startle(WildDef w, WildHerdState h, float fromX, float fromZ)
    {
        h.FleeT = w.FleeSeconds;
        h.FleeFromX = fromX; h.FleeFromZ = fromZ;
    }

    /// <summary>Bir yaratigin adimi: tavrina gore mod, hedef nokta, hiz, klip ve olay.</summary>
    public static WildStep Step(CreatureDef c, WildHerdState h, WildState s, WildSense sense, float dt)
    {
        var w = c.Wild ?? new WildDef();
        float walk = c.Speed * w.WalkSpeedMult, run = c.Speed * w.RunSpeedMult;
        float pd = Dist(s.X, s.Z, sense.PlayerX, sense.PlayerZ);
        s.AttackCd = MathF.Max(0, s.AttackCd - dt);
        var prev = s.Mode;
        WildStep Out(WildMode m, float gx, float gz, float speed, string clip, WildEvent e = WildEvent.None)
        {
            if (m != s.Mode) { s.Mode = m; s.ModeT = 0; }
            else s.ModeT += dt;
            return new WildStep { Mode = m, GoalX = gx, GoalZ = gz, Speed = speed, Clip = clip, Event = e };
        }
        WildStep Facing(WildStep st, float fx, float fz) { st.Face = true; st.FaceX = fx; st.FaceZ = fz; return st; }

        // Uyku: tur bu saatte uyanik degil (gececi gunduz, gunduzcu normal gece).
        if (!Awake(w, sense.Night))
        {
            s.Warned = false;
            var st = Out(WildMode.Sleep, s.X, s.Z, 0, "rest", prev != WildMode.Sleep ? WildEvent.Slept : WildEvent.None);
            // Uyuyan da vurulunca uyanir ve kacar.
            if (!sense.Hit) return st;
            Startle(w, h, sense.PlayerX, sense.PlayerZ);
        }

        bool predator = w.Stance == WildStance.Predator;

        // Darbe: yirtici ofkelenir, digerleri suruyle kacar.
        if (sense.Hit)
        {
            if (predator) { s.Warned = true; }
            else if (w.FleeOnHit) Startle(w, h, sense.PlayerX, sense.PlayerZ);
        }

        // Kacis
        if (h.FleeT > 0 && !predator)
        {
            float ax = s.X - h.FleeFromX, az = s.Z - h.FleeFromZ;
            float l = MathF.Max(0.001f, MathF.Sqrt(ax * ax + az * az));
            s.TargetT = 0;
            return Out(WildMode.Flee, s.X + ax / l * 6f, s.Z + az / l * 6f, run, "run", prev != WildMode.Flee ? WildEvent.Fled : WildEvent.None);
        }

        // Oyuncu cok yakin: urkek, sakin, yagmaci ve gececi kacar.
        if (!predator && w.FleeRadius > 0 && pd < w.FleeRadius)
        {
            Startle(w, h, sense.PlayerX, sense.PlayerZ);
            float ax = s.X - sense.PlayerX, az = s.Z - sense.PlayerZ;
            float l = MathF.Max(0.001f, pd);
            return Out(WildMode.Flee, s.X + ax / l * 6f, s.Z + az / l * 6f, run, "run", WildEvent.Fled);
        }

        // Yirtici: uyari -> hucum -> pence.
        if (predator)
        {
            if (pd > w.GiveUpRadius) s.Warned = false;
            if (pd < w.AlertRadius && !s.Warned)
            {
                var st = Out(WildMode.Warn, s.X, s.Z, 0, "attack2", prev != WildMode.Warn ? WildEvent.Warned : WildEvent.None);
                if (s.ModeT >= w.WarnSeconds) s.Warned = true;
                return Facing(st, sense.PlayerX, sense.PlayerZ);
            }
            bool angry = s.Warned && pd < w.GiveUpRadius && (pd < w.ChargeRadius || s.Mode is WildMode.Charge or WildMode.Attack || sense.Hit);
            if (angry)
            {
                if (pd <= w.AttackRange)
                {
                    if (s.AttackCd <= 0)
                    {
                        s.AttackCd = w.AttackInterval;
                        return Facing(Out(WildMode.Attack, s.X, s.Z, 0, "attack1", WildEvent.Struck), sense.PlayerX, sense.PlayerZ);
                    }
                    var hold = s.Mode == WildMode.Attack && s.ModeT < 0.5f ? "attack1" : "idle";
                    return Facing(Out(s.Mode == WildMode.Attack ? WildMode.Attack : WildMode.Charge, s.X, s.Z, 0, hold), sense.PlayerX, sense.PlayerZ);
                }
                return Facing(Out(WildMode.Charge, sense.PlayerX, sense.PlayerZ, run, "run"), sense.PlayerX, sense.PlayerZ);
            }
        }

        // Yagmaci ve gececi: hedefe gider, yaninda durur, sure dolunca alir ve kacar.
        if (!string.IsNullOrEmpty(w.Target) && sense.HasTarget)
        {
            float td = Dist(s.X, s.Z, sense.TargetX, sense.TargetZ);
            if (td <= w.TargetStop + 0.25f)
            {
                s.TargetT += dt;
                if (s.TargetT >= w.TargetSeconds)
                {
                    s.TargetT = 0;
                    Startle(w, h, sense.TargetX, sense.TargetZ);
                    return Out(WildMode.Flee, s.X, s.Z, run, "run", WildEvent.Took);
                }
                return Facing(Out(WildMode.Feed, s.X, s.Z, 0, "eat"), sense.TargetX, sense.TargetZ);
            }
            float tx = sense.TargetX - s.X, tz = sense.TargetZ - s.Z, tl = MathF.Max(0.001f, td);
            float gx = sense.TargetX - tx / tl * w.TargetStop, gz = sense.TargetZ - tz / tl * w.TargetStop;
            return Out(WildMode.Approach, gx, gz, td > 8f ? run : walk, td > 8f ? "run" : "walk");
        }

        // Urkek: yakindaki oyuncuya basini kaldirip bakar.
        if (w.AlertRadius > 0 && pd < w.AlertRadius && w.Stance is WildStance.Shy or WildStance.Raider)
            return Facing(Out(WildMode.Alert, s.X, s.Z, 0, "idle", prev != WildMode.Alert ? WildEvent.Alerted : WildEvent.None), sense.PlayerX, sense.PlayerZ);

        // Dolasma: suruyle yurur, varinca bekler / otlar / dinlenir.
        float wx = h.WanderX + s.SlotX, wz = h.WanderZ + s.SlotZ;
        float wd = Dist(s.X, s.Z, wx, wz);
        bool paused = s.Mode is WildMode.Idle or WildMode.Graze or WildMode.Rest;
        if (paused && s.ModeT < PauseOf(s) && wd < w.WanderRadius) return Out(s.Mode, s.X, s.Z, 0, ClipOf(s.Mode));
        if (wd > 0.4f && !(paused && s.ModeT < PauseOf(s))) return Out(WildMode.Walk, wx, wz, walk, "walk");
        // Varildi: yeni durus secimi.
        float r = s.Next();
        var m = r < w.IdleShare ? WildMode.Idle : r < w.IdleShare + w.GrazeShare ? WildMode.Graze : WildMode.Rest;
        s.Pause = s.Range(w.PauseMin, w.PauseMax);
        s.Mode = m; s.ModeT = 0;
        return new WildStep { Mode = m, GoalX = s.X, GoalZ = s.Z, Speed = 0, Clip = ClipOf(m) };
    }

    static float PauseOf(WildState s) => s.Pause;

    public static string ClipOf(WildMode m) => m switch
    {
        WildMode.Graze => "eat",
        WildMode.Rest or WildMode.Sleep => "rest",
        WildMode.Walk or WildMode.Approach => "walk",
        WildMode.Flee or WildMode.Charge => "run",
        WildMode.Feed => "eat",
        WildMode.Warn => "attack2",
        WildMode.Attack => "attack1",
        _ => "idle",
    };
}
