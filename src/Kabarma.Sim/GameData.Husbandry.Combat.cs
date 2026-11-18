namespace Kabarma.Sim;

// M3 hayvan savasi ve binek verisi: husbandry.json "combat", "forms", "mount" (GDD 9.5, ICERIK 3.1-3.2).
// Kurallar Husbandry.Combat.cs ve Mount.cs'te.

public sealed class AnimalCombatDef
{
    /// <summary>Savas Agili'ni belirleyen yerlestirilebilir (agilin icinde durur).</summary>
    public string Flag { get; set; } = "savas_agili_bayragi";
    public string Bell { get; set; } = "ahir_zili";
    public string Post { get; set; } = "baglama_diregi";
    /// <summary>Ahir Zili bu yaricaptaki evcil hayvanlari agila cagirir, m.</summary>
    public float BellRadius { get; set; } = 30;
    /// <summary>Binekten direge bu kadar yakin inilirse hayvan baglanir, m.</summary>
    public float PostRadius { get; set; } = 3;
    /// <summary>Bayilan hayvan bu kadar safak yorgun kalir.</summary>
    public int TiredDays { get; set; } = 1;
    /// <summary>Sert Ay: bayilan hayvan olur (dunya ayari M4).</summary>
    public bool HardMoonDies { get; set; }
    /// <summary>Savasan hayvan agilin merkezinden en cok bu kadar uzaklasir, m.</summary>
    public float GuardRadius { get; set; } = 16;
    /// <summary>Dusmani bu yaricapta gorur, m.</summary>
    public float SightRadius { get; set; } = 20;
    /// <summary>Ahir yikilinca hayvanlar bu kadar dagilir, m.</summary>
    public float ScatterRadius { get; set; } = 6;
    /// <summary>GECICI (kabarma3 Wave sozlesmesi gelene kadar): Zipzip'in hayvana temas uzanimi ve vurus araligi.</summary>
    public float EnemyReach { get; set; } = 1.4f;
    public float EnemyHitInterval { get; set; } = 1f;
}

/// <summary>Turun kabarmis hali (ICERIK 3, "Kabarmis hali").</summary>
public sealed class AnimalFormDef
{
    public string Name { get; set; } = "";
    public bool Fights { get; set; }
    public float Damage { get; set; }
    /// <summary>combat.json hasar turu kimligi.</summary>
    public string Type { get; set; } = "darbe";
    public float Interval { get; set; } = 1.5f;
    /// <summary>Hedefe vurma uzanimi, m.</summary>
    public float Reach { get; set; } = 1.6f;
    /// <summary>&gt; 0: hedefin cevresindeki bu yaricaptaki dusmanlara da vurur (alan), m.</summary>
    public float Radius { get; set; }
    public float Knock { get; set; }
    public float HpMult { get; set; } = 1f;
    /// <summary>Zirh puani (combat.json armorK).</summary>
    public float Armor { get; set; }
    /// <summary>Kabarikken binilir (Koc Gidak).</summary>
    public bool Rideable { get; set; }
    /// <summary>Kabarikken heybe yuvasi (Orman Arabasi).</summary>
    public int Pack { get; set; }
}

public sealed class MountDef
{
    /// <summary>Hayvana takilan esya.</summary>
    public string Item { get; set; } = "eyer";
    /// <summary>Eyeri takan yerlestirilebilir ve uzanimi.</summary>
    public string Rack { get; set; } = "eyerlik";
    public float RackRadius { get; set; } = 3;
    /// <summary>Surus ustaligi bu kadar metrede bir XP (ride_100m).</summary>
    public float XpMeters { get; set; } = 100;
    /// <summary>Turun binek hizi, m/sn (yalniz binilebilen turler).</summary>
    public Dictionary<string, float> Speeds { get; set; } = new();
}

public sealed partial class HusbandryFile
{
    public AnimalCombatDef Combat { get; set; } = new();
    public Dictionary<string, AnimalFormDef> Forms { get; set; } = new();
    public MountDef Mount { get; set; } = new();

    public AnimalFormDef? FormOf(string species) => Forms.GetValueOrDefault(species);
}
