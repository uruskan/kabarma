namespace Kabarma.Sim;

/// <summary>
/// GECICI ARAYUZ (M3 cip akisi): savas akisinin ortak hasar sozlesmesi (Combat*) main'e girene kadar cipler
/// CombatStats uzerinden ve vurus anindaki duzeltmeyle silaha baglanir. Combat* gelince bu sinif kalkar ve cipler
/// ChipBook.Stat(baslik, stat) ile onun modifier listesine yazilir (stat kimlikleri ayni).
/// </summary>
public static class ChipCombat
{
    /// <summary>Kart istatistiklerine (varsa) cip etkilerini ekleyerek yeni bir CombatStats uretir (atis basina).</summary>
    public static CombatStats Merge(CombatStats? cards, ChipBook chips, string head, Rng rng)
    {
        var s = new CombatStats();
        if (cards != null)
        {
            s.Projectiles = cards.Projectiles; s.Bounces = cards.Bounces; s.FireRateMult = cards.FireRateMult; s.SizeMult = cards.SizeMult;
            s.BurnChance = cards.BurnChance; s.FreezeChance = cards.FreezeChance; s.CritChance = cards.CritChance;
            s.KnockbackMult = cards.KnockbackMult; s.PickupRangeMult = cards.PickupRangeMult; s.MoveSpeedMult = cards.MoveSpeedMult;
        }
        if (!chips.On(head).Any()) return s;
        s.FireRateMult *= 1f + chips.Stat(head, "fire_rate");
        s.PickupRangeMult *= 1f + chips.Stat(head, "pickup_radius");
        s.KnockbackMult *= 1f + chips.Stat(head, "knockback");
        s.SizeMult *= 1f + chips.Stat(head, "area") * 0.5f;
        if (rng.Chance(chips.Stat(head, "extra_projectile_chance"))) s.Projectiles++;
        float bounce = chips.Stat(head, "bounce_chance");
        if (bounce > 0f && rng.Chance(bounce)) s.Bounces += 1 + (int)chips.Stat(head, "bounce_count");
        return s;
    }

    /// <summary>Vurus: hasar carpani ve cipin kritik sansi (kart kritigi RollHit'te zaten atildi).</summary>
    public static float AdjustHit(ChipBook chips, string head, float damage, ref bool crit, float critMult, Rng rng)
    {
        if (!chips.On(head).Any()) return damage;
        float d = damage * (1f + chips.Stat(head, "damage"));
        if (!crit && rng.Chance(chips.Stat(head, "crit_chance"))) { crit = true; d *= critMult; }
        if (crit) d *= 1f + chips.Stat(head, "crit_damage");
        return MathF.Round(d);
    }

    public static bool RollBurn(ChipBook chips, string head, Rng rng) => rng.Chance(chips.Stat(head, "burn_chance"));
    public static bool RollFreeze(ChipBook chips, string head, Rng rng) => rng.Chance(chips.Stat(head, "freeze_chance"));
}
