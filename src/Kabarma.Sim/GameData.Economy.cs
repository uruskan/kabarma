namespace Kabarma.Sim;

// economy.json: kargo kasasi ve rampa (GDD §13), El Arabasi (ICERIK §14), insa kurallari (GDD §7; URETIM §6 M1 "Yapi").

public sealed class CrateDef
{
    /// <summary>Kasa kac yigin alir (GDD §13: 20).</summary>
    public int Slots { get; set; } = 20;
    /// <summary>Kasanin yuk nesnesi agirligi, kg (GDD §13: 25).</summary>
    public float Kg { get; set; } = 25f;
}

public sealed class RampDef
{
    /// <summary>Bir firlatmada en fazla kasa (GDD §13: 1-6); kabarik urun tek basina gider.</summary>
    public int MaxCrates { get; set; } = 6;
    /// <summary>Firlatmadan sonra odeme gecikmesi, sn (GDD §13: 60).</summary>
    public float PayoutSeconds { get; set; } = 60f;
    /// <summary>Ilk satisin odemesi aninda gelir (GDD §24 ogretici kurali).</summary>
    public bool FirstSaleInstant { get; set; } = true;
}

public sealed class CartDef
{
    /// <summary>Iterken hiz, yurume hizinin carpani (ICERIK §14 / URETIM §6: %80).</summary>
    public float PushSpeedMult { get; set; } = 0.8f;
    /// <summary>Tek yuk nesnesinin azami agirligi, kg (200).</summary>
    public float MaxLoadKg { get; set; } = 200f;
    /// <summary>Yuk nesnesi yerine cantadaki gibi esya yuvasi (6).</summary>
    public int Slots { get; set; } = 6;
    /// <summary>Bu acidan fazla yatan araba devrilmis sayilir.</summary>
    public float TipDegrees { get; set; } = 55f;
    /// <summary>Iterken bundan dik yokusta araba devrilir.</summary>
    public float MaxSlopeDegrees { get; set; } = 32f;
}

public sealed class BuildRulesDef
{
    /// <summary>Tapu icinde sokum iadesi (GDD §7: %100). Tapu disi M2.</summary>
    public float RefundInClaim { get; set; } = 1f;
    /// <summary>Onarim: hasar oraninda malzemenin bu kadari (GDD §7: %25).</summary>
    public float RepairFraction { get; set; } = 0.25f;
    /// <summary>M1'de hasarla yikilma yok: parca cani bu oranin altina inmez (URETIM §6 M1).</summary>
    public float HpFloor { get; set; } = 0.1f;
}

public sealed class EconomyFile
{
    public CrateDef Crate { get; set; } = new();
    public RampDef Ramp { get; set; } = new();
    public CartDef Cart { get; set; } = new();
    public BuildRulesDef Build { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("economy.json")]
    public EconomyFile Economy { get; private set; } = new();
}
