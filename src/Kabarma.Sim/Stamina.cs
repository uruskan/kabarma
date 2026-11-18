namespace Kabarma.Sim;

/// <summary>Dayaniklilik sayilari (GDD §3.1, §3.2): tool.json "player.stamina".</summary>
public sealed class StaminaDef
{
    /// <summary>Taban dayaniklilik (GDD §3.1: 100).</summary>
    public float Max { get; set; } = 100f;
    /// <summary>Depar suresince saniyede harcanan (GDD §3.2: 8).</summary>
    public float SprintDrain { get; set; } = 8f;
    /// <summary>Saniyede dolan (GDD §3.2: 15).</summary>
    public float Regen { get; set; } = 15f;
    /// <summary>Depar birakildiktan sonra dolmaya baslamadan once beklenen sure (GDD §3.2: 1 sn).</summary>
    public float RegenDelay { get; set; } = 1f;
    /// <summary>Sifira inince depar bu degere cikana kadar kapali kalir (GDD §3.2: 20).</summary>
    public float UnlockAt { get; set; } = 20f;
    /// <summary>Depar bu yatay hizin (m/sn) altindayken harcamaz: duvara dayanip tusa basmak yemez. GDD'de yok; oyuncu akisinin karari.</summary>
    public float MinSpeed { get; set; } = 0.5f;
}

/// <summary>
/// Oyuncunun dayanikliligi (GDD §3.2): depar saniyede SprintDrain harcar; depar birakildiktan RegenDelay sonra
/// saniyede Regen dolar; sifira inerse depar, dayaniklilik UnlockAt'e cikana kadar kapanir.
/// Motordan bagimsiz kural durumudur (GameState.Stamina); Godot katmani her karede Tick cagirir, pano okur.
/// </summary>
public sealed partial class Stamina
{
    public StaminaDef Def { get; }
    public float Value { get; private set; }
    /// <summary>Yemek ve Ustalik ekleri (GDD §3.1); M1'de baglanmadi, 0.</summary>
    public float BonusMax { get; set; }
    /// <summary>Yenilenme carpani (Dinlenmis +%25, GDD §3.4); M1'de baglanmadi, 1.</summary>
    public float RegenMult { get; set; } = 1f;
    /// <summary>Sifira indi; depar UnlockAt'e kadar kapali.</summary>
    public bool Exhausted { get; private set; }
    /// <summary>Son Tick'te depar atildi mi.</summary>
    public bool Sprinting { get; private set; }
    /// <summary>Son depardan bu yana gecen sure (sn).</summary>
    public float SinceSprint { get; private set; }

    public float Max => Def.Max + BonusMax;
    public float Fraction => Max > 0 ? Value / Max : 0f;

    public Stamina(StaminaDef def)
    {
        Def = def;
        Value = def.Max;
        SinceSprint = def.RegenDelay;
    }

    /// <summary>Bir kare ilerletir. wantSprint: oyuncu depar tusuna basiyor ve hareket ediyor. Donus: bu karede depar atiliyor mu.</summary>
    public bool Tick(float dt, bool wantSprint)
    {
        if (dt <= 0) return Sprinting;
        Sprinting = wantSprint && !Exhausted && Value > 0f;
        if (Sprinting)
        {
            Value = Math.Max(0f, Value - Def.SprintDrain * dt);
            SinceSprint = 0f;
            if (Value <= 0f) Exhausted = true;
        }
        else
        {
            float before = SinceSprint;
            SinceSprint += dt;
            // Bekleme bu karenin ortasinda bittiyse yalnizca kalan kisim kadar dolar.
            float fill = Math.Min(dt, SinceSprint - Def.RegenDelay);
            if (before < Def.RegenDelay && SinceSprint < Def.RegenDelay) fill = 0f;
            if (fill > 0f) Value = Math.Min(Max, Value + Def.Regen * RegenMult * fill);
        }
        if (Exhausted && Value >= Def.UnlockAt) Exhausted = false;
        return Sprinting;
    }
}
