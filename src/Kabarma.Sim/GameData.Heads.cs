namespace Kabarma.Sim;

// Capa is basliklarinin ve silahlarinin M1 kurallari (URETIM 6 "M1 kapsami", Capa satiri; GDD 4; ICERIK 6).
// heads.json ve weapons.json'daki bu alanlar ToolFile'a ve WeaponDef'e parca olarak eklenir (GameData.Tool.cs).

/// <summary>Cekim: tutar, tasir, firlatir; 80 kg siniri (URETIM 6 M1, ICERIK 6.1).</summary>
public sealed class CarryDef
{
    public float MaxKg { get; set; }
    /// <summary>Nisan noktasi bu kadar uzaktaki yuk nesnesini tutar (yaricapina eklenir).</summary>
    public float GrabRadius { get; set; }
    /// <summary>Tutma menzili: carryRange'e eklenen pay.</summary>
    public float GrabReach { get; set; }
    /// <summary>Tarladaki kabarik urun: tutma yaricapina urunun olcegi basina eklenen.</summary>
    public float GrabPerScale { get; set; }
    /// <summary>Firlatmak icin yuklerken yuk bu kadar geri cekilir (m, tam yukte).</summary>
    public float ChargePullBack { get; set; }
    /// <summary>Bos ya da agir yuke basista gorunen kisa isin (sn): girdiye tepki.</summary>
    public float MissBeamSeconds { get; set; }
    /// <summary>Tutulan yuk namludan bu kadar ileride ve yukarida durur.</summary>
    public float HoldDistance { get; set; }
    public float HoldLift { get; set; }
    public float HoldLiftPerRadius { get; set; }
    public float Follow { get; set; }
    /// <summary>Tetik bu sureden kisa basilirsa yuk birakilir; uzun basilirsa yuklenip firlatilir.</summary>
    public float TapSeconds { get; set; }
    public float ChargeSeconds { get; set; }
    public float ThrowMinSpeed { get; set; }
    public float ThrowMaxSpeed { get; set; }
    public float ThrowUp { get; set; }
    public float Gravity { get; set; }
    public float Bounce { get; set; }
    public float DropSeconds { get; set; }
    /// <summary>Rampaya birakma: yuk pedin bu kadar yakininda ya da oyuncu rampanin bu kadar yakininda.</summary>
    public float RampPadRadius { get; set; }
    public float RampReach { get; set; }
}

/// <summary>Sulama kucuk dusmani iter (ICERIK 6.1, URETIM 6 M1).</summary>
public sealed class WaterPushDef
{
    /// <summary>Her sulama tikinde dusmana eklenen itme hizi (m/sn); dusman kendi surtunmesiyle yavaslar.</summary>
    public float Force { get; set; }
    /// <summary>Puskurtme cizgisine bu kadar yakin dusman itilir.</summary>
    public float Width { get; set; }
    /// <summary>"Kucuk": olcegi (Scale0) bunu asmayan dusman. Kabarmis Zipzip itilmez.</summary>
    public float MaxScale { get; set; }
}

public sealed partial class ToolFile
{
    public float MowInterval { get; set; }
    public float TillInterval { get; set; }
    /// <summary>Toprak: Ocak'in ve kapsulun bu kadar yakini surulmez.</summary>
    public float TillClearOcak { get; set; }
    public float TillClearPod { get; set; }
    /// <summary>Sulama tiki (sn): her tikte nisan cevresi islanir ve kucuk dusman itilir.</summary>
    public float WaterInterval { get; set; }
    /// <summary>Kazma: nisan noktasina bu kadar yakin kaya da kemirilir.</summary>
    public float MineAimSlack { get; set; }
    /// <summary>Bicme: agacin govdesi nisan noktasina bu kadar yakinsa vurulur.</summary>
    public float TreeRadius { get; set; }
    public CarryDef Carry { get; set; } = new();
    public WaterPushDef WaterPush { get; set; } = new();
}

public sealed partial class WeaponDef
{
    /// <summary>Yari otomatik: her tetik basisi bir atis (ICERIK 6.2 #2). Tam otomatikte basili tutmak ateslemeye devam eder.</summary>
    public bool SemiAuto { get; set; }
    /// <summary>Yari otomatikte bekleme bitmeden gelen basis bu sure icinde saklanir ve bekleme bitince atilir.</summary>
    public float InputBufferSeconds { get; set; }
    /// <summary>Baslangicta acik mi (CapaState). Tirpan baştan (B); Civi Tabancasi tarifle (ekonomi), main'de acik.</summary>
    public bool UnlockedAtStart { get; set; }
    /// <summary>Seken karti: sonraki hedef bu yaricapta aranir; sekince menzilden bu kadar geri verilir.</summary>
    public float BounceSearch { get; set; }
    public float BounceRefund { get; set; }
    /// <summary>Merminin isabet yaricapi (dusmanin yaricapina eklenir) ve zayif nokta yaricapi (dusman yaricapinin orani).</summary>
    public float HitRadius { get; set; }
    public float WeakRadius { get; set; }
    /// <summary>Nisanin dikey bileseni en fazla bu oranda (0: sinir yok). Tirpan yere paralel supurur.</summary>
    public float MaxPitch { get; set; }
    /// <summary>Mermi zeminin bu kadar ustunde kalir (0: zemine carpinca biter).</summary>
    public float GroundOffset { get; set; }
    /// <summary>Gorunus: donme hizi (rad/sn), sonmeye baslangic (menzil orani), isilti.</summary>
    public float Spin { get; set; }
    public float FadeStart { get; set; }
    public float Glow { get; set; }
}
