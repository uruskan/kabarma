namespace Kabarma.Sim;

/// <summary>Baygin oyuncunun durumu (GDD 3.6). Duz ve serilestirilebilir.</summary>
public sealed class DownedState
{
    /// <summary>Baygin mi (ortak oyunda can sifirlandi, kalkmayi bekliyor).</summary>
    public bool Down { get; set; }
    /// <summary>Olume kalan sure, sn.</summary>
    public float Left { get; set; }
    /// <summary>Sok tokadinin birikmis basili tutma suresi, sn.</summary>
    public float Hold { get; set; }
    /// <summary>Tek oyuncu: bu gece ikinci nefes kullanildi mi.</summary>
    public bool SecondWindUsed { get; set; }
}

/// <summary>
/// Bayilma, sok tokadi ve ikinci nefes (GDD 3.6):
/// ortak oyunda can sifirlaninca 30 sn baygin; bir arkadas sok tokadiyla (E basili, 1,5 sn) %30 canla kaldirir; sure
/// dolarsa olur. Tek oyuncuda gecede bir kez ikinci nefes: %40 canla kalkilir.
/// </summary>
public static class DownedRules
{
    public enum Outcome
    {
        /// <summary>Ortak oyun: baygin, tokat bekleniyor.</summary>
        Downed,
        /// <summary>Tek oyuncu: ikinci nefesle %40 canla kalkar.</summary>
        SecondWind,
        /// <summary>Olur: canta emanet kasasi, yatakta dogma (GDD 3.6 "Olum").</summary>
        Dead,
    }

    public static Outcome OnZeroHp(DownedState s, bool coop, NetDownedDef def)
    {
        if (coop)
        {
            s.Down = true;
            s.Left = def.Seconds;
            s.Hold = 0;
            return Outcome.Downed;
        }
        if (!s.SecondWindUsed) { s.SecondWindUsed = true; return Outcome.SecondWind; }
        return Outcome.Dead;
    }

    /// <summary>Gece basinda ikinci nefes yenilenir ("gecede bir kez").</summary>
    public static void OnNightStarted(DownedState s) => s.SecondWindUsed = false;

    /// <summary>Baygin sure akar. true: sure doldu, oyuncu olur.</summary>
    public static bool Tick(DownedState s, float dt)
    {
        if (!s.Down) return false;
        s.Left -= dt;
        if (s.Left > 0) return false;
        s.Down = false;
        s.Hold = 0;
        return true;
    }

    /// <summary>
    /// Bir arkadas E'yi basili tutuyor (<paramref name="holding"/>) ve yakinda (<paramref name="inRange"/>). Birakinca ya da
    /// uzaklasinca birikim sifirlanir. true: tokat tamam, oyuncu kalkar.
    /// </summary>
    public static bool Revive(DownedState s, float dt, bool holding, bool inRange, NetDownedDef def)
    {
        if (!s.Down) return false;
        if (!holding || !inRange) { s.Hold = 0; return false; }
        s.Hold += dt;
        if (s.Hold + 1e-4f < def.ReviveHold) return false;
        s.Down = false;
        s.Hold = 0;
        return true;
    }

    public static float ReviveHp(float maxHp, NetDownedDef def) => maxHp * def.ReviveHpFraction;
    public static float SecondWindHp(float maxHp, NetDownedDef def) => maxHp * def.SecondWindFraction;
}
