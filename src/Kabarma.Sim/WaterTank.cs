namespace Kabarma.Sim;

/// <summary>Sulama basliginin su deposu: heads.json "sulama" (URETIM §6 M1 "Sulama deposu 100 L"; ana oturum karari 18 Kasim 2026).</summary>
public sealed class SulamaDef
{
    public float TankLiters { get; set; }
    /// <summary>Islanan her kare bu kadar su harcar.</summary>
    public float LitersPerCell { get; set; }
    /// <summary>Gol yuzeyine nisan alininca saniyede dolan litre.</summary>
    public float FillPerSecond { get; set; }
    public bool StartFull { get; set; }
}

/// <summary>
/// Sulama deposu kurali: dolu baslar, islanan her kare LitersPerCell harcar, gol yuzeyine nisan alininca FillPerSecond ile
/// dolar, bos depo sulamaz. Durum tek sayi (Liters): kayda hazir (PLAN §7.2, M1'de 5. kosul M2'de).
/// </summary>
public sealed class WaterTank
{
    readonly SulamaDef _def;

    public WaterTank(SulamaDef def)
    {
        _def = def;
        Liters = def.StartFull ? def.TankLiters : 0f;
    }

    public float Liters { get; private set; }
    public float Capacity => _def.TankLiters;
    public float PerCell => _def.LitersPerCell;
    public bool Full => Liters >= Capacity;
    /// <summary>Bir kareyi islatacak kadar su var mi?</summary>
    public bool CanWater => _def.LitersPerCell > 0 && Liters >= _def.LitersPerCell;

    /// <summary>Bir kare icin su harcar; yetmiyorsa harcamaz ve false doner.</summary>
    public bool TryWaterCell()
    {
        if (!CanWater) return false;
        Liters -= _def.LitersPerCell;
        return true;
    }

    /// <summary>Golden doldurur; giren litreyi doner (depo dolunca 0).</summary>
    public float Fill(float seconds)
    {
        if (seconds <= 0) return 0f;
        float before = Liters;
        Liters = Math.Min(Capacity, Liters + _def.FillPerSecond * seconds);
        return Liters - before;
    }

    /// <summary>Kayittan geri yukleme (M2).</summary>
    public void Restore(float liters) => Liters = Math.Clamp(liters, 0f, Capacity);
}
