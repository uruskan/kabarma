namespace Kabarma.Sim;

// Otomasyon makineleri ve istasyon islevleri: automation.json (GDD 11.4, ICERIK §11, §12; M4 tasima).

public sealed class MachineDef
{
    /// <summary>Makinenin etrafindaki calisma yaricapi (m); 0: yaricap yok.</summary>
    public float Radius { get; set; }
    /// <summary>Makinenin girdi ve cikti icin kullandigi Sandiklarin uzakligi (m).</summary>
    public float ChestRadius { get; set; } = 10f;
    /// <summary>Bir calisma turu (sn).</summary>
    public float Interval { get; set; } = 2f;
    /// <summary>Sagim-Kirkim Kolu: toplanan urunler.</summary>
    public List<string> Items { get; set; } = new();
    /// <summary>Paketleyici: kasa dolu degilse ve girdi yoksa kac sn sonra rampadakini yollar.</summary>
    public float IdleLaunchSeconds { get; set; }
    /// <summary>Sondaj Kulesi: ustunde durulacak keşif noktası türü ve biyomun cevheri.</summary>
    public string? PoiType { get; set; }
    public Dictionary<string, string> Ores { get; set; } = new();
}

public sealed class AutomationFile
{
    public Dictionary<string, MachineDef> Machines { get; set; } = new();
    /// <summary>Istasyon kimligi -> islev: recipes, machine, hand, special, next:&lt;akis&gt;.</summary>
    public Dictionary<string, string> Functions { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("automation.json")]
    public AutomationFile Automation { get; private set; } = new();
}
