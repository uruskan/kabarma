namespace Kabarma.Sim;

// Agil, evcil hayvan ve agil yerlestirilebilirleri: pen.json (GDD 9.1, ICERIK 10.2, URETIM 6 M1 "Gidak ve agil").

/// <summary>Agilda tutulan tur: kapladigi alan, sevdigi yem, guven, uretim.</summary>
public sealed class PenAnimalDef
{
    public string Id { get; set; } = "";
    /// <summary>Tur basina gereken alan, m² (GDD 9.1: Gidak 4).</summary>
    public float Area { get; set; }
    public string Food { get; set; } = "wheat";
    /// <summary>Guven cubugunu dolduran yem sayisi (ogretici hayvan haric).</summary>
    public int FoodToTrust { get; set; } = 1;
    /// <summary>Yemlik'te yem varken iki urun arasi saniye (URETIM 6 M1: 3 dakika).</summary>
    public float LayInterval { get; set; }
    /// <summary>Her urunde Yemlik'ten alinan yem.</summary>
    public int FoodPerEgg { get; set; } = 1;
    public string Product { get; set; } = "egg";
}

public sealed class PenFile
{
    /// <summary>Bir hucrenin alani, m² (GDD 9.1: 1 hucre = 9 m²). Agil en az minCells hucre.</summary>
    public float CellArea { get; set; } = 9f;
    public int MinCells { get; set; } = 1;
    /// <summary>Parca uclarinin birlestigi uzaklik, m.</summary>
    public float Weld { get; set; } = 0.35f;
    /// <summary>Agil duvari sayilan yapi seti parcalari (GDD 9.1: cit, bahce kapisi, duvar, kapi parcalari).</summary>
    public List<string> WallPieces { get; set; } = new();
    /// <summary>Bunlarin kapi olanlari: acikken gecilir (kapi kasasi bos ya da Ahsap Kapi'si acikken).</summary>
    public List<string> GatePieces { get; set; } = new();
    public List<PenAnimalDef> Animals { get; set; } = new();
    /// <summary>Ogretici hayvan (ilk Gidak) bu kadar yemle guvenir (GDD 24).</summary>
    public int TutorialFoodToTrust { get; set; } = 1;
    /// <summary>Ogretici hayvan agila ilk girisinde hemen yumurtlar (GDD 24).</summary>
    public bool TutorialEggOnEntry { get; set; } = true;
    /// <summary>Oyuncu bu uzaklikta kosar ya da ates ederse dolmamis guven sifirlanir (GDD 9.1), m.</summary>
    public float ScareRadius { get; set; }
    /// <summary>Evcil hayvanin oyuncuyu izlerken durdugu uzaklik, m.</summary>
    public float FollowDistance { get; set; }
    /// <summary>Folluk yuvasi (ICERIK 10.2: 6).</summary>
    public int NestSlots { get; set; }
    /// <summary>Yemlik'in alabildigi yem.</summary>
    public int FeederCapacity { get; set; }
    /// <summary>Kapi ve kanat acilisi: aci (derece) ve sure (sn).</summary>
    public float GateOpenDegrees { get; set; }
    public float GateSwingSeconds { get; set; }
    /// <summary>E ile kapi, Yemlik, Folluk ve yerdeki yumurtaya uzanma, m.</summary>
    public float InteractRange { get; set; }
    /// <summary>Yapi parcasi caninin inebilecegi en dusuk oran (URETIM 6 M1: %10, yikilma M3).</summary>
    public float StructureHpFloor { get; set; }
    /// <summary>Zipzip cite vurdugunda bir sonraki vurusa kadar en az sure, sn.</summary>
    public float FenceHitCooldown { get; set; }
    /// <summary>Ahsap Kapi'nin Kapi Kasasi'na oturmak icin aranacagi uzaklik, m.</summary>
    public float DoorSnapRadius { get; set; }
}

public sealed partial class GameData
{
    [DataFile("pen.json")]
    public PenFile Pen { get; private set; } = new();
}
