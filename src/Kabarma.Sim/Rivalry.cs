namespace Kabarma.Sim;

/// <summary>Enkaz plani: gecede yikilan parcanin hayaleti. Duz durum (kayit, ag).</summary>
public sealed class RuinPlan
{
    public int Id { get; set; }
    public string Team { get; set; } = "";
    public string Shape { get; set; } = "";
    public string Material { get; set; } = "wood";
    public int Units { get; set; } = 1;
    public float[] Pos { get; set; } = new float[3];
    public float Yaw { get; set; }
    public float CenterY { get; set; }
    public bool Grounded { get; set; }
    public bool Horizontal { get; set; }
}

/// <summary>Bir takim (GDD 22.2 kural 1): Ocak, oyuncular, gecenin sayaci, muhur.</summary>
public sealed class RivalryTeam
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float OcakX { get; set; }
    public float OcakZ { get; set; }
    public float PlotRadius { get; set; } = 60f;
    public List<string> Players { get; set; } = new();
    /// <summary>Bu gecenin (borc dusulmus) Maya'si; safakta sezon puanina ve Acik Silo'ya gider.</summary>
    public float NightMaya { get; set; }
    /// <summary>Sezon boyu toplanan Maya.</summary>
    public int CollectedMaya { get; set; }
    /// <summary>Sezon boyu ihraç edilen puan.</summary>
    public int Exports { get; set; }
    /// <summary>Kazan'i devrildi: gecesi bitti, tapusu mühürlü (safaga dek).</summary>
    public bool KazanToppled { get; set; }
    /// <summary>Cevrimdisi koruma (kural 7): ekipten kimse cevrimici degil.</summary>
    public bool Offline { get; set; }
    /// <summary>Ihracata hazir isaretli mallar (kural 5): Acik Silo'ya girer.</summary>
    public SortedDictionary<string, int> ExportReady { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Takimin kendi Acik Silo'su (kural 5): safakta dolar, yalniz rakipler yagmalar.</summary>
    public SortedDictionary<string, int> Silo { get; set; } = new(StringComparer.Ordinal);
    public int SiloMaya { get; set; }
    public int Score => CollectedMaya + Exports;
}

/// <summary>Sezon cetveli (kural 8).</summary>
public sealed class RivalrySeason
{
    /// <summary>0: sonsuz.</summary>
    public int Days { get; set; }
    public int StartDay { get; set; } = 1;
    public bool Frozen { get; set; }
    public string Winner { get; set; } = "";
}

/// <summary>Cekisme dunyasinin kalici durumu (kayit bolumu "rivalry", ag bolumu "rivalry").</summary>
public sealed class RivalryState
{
    public bool Enabled { get; set; }
    /// <summary>Kural 3: acik arazide oyuncu-oyuncu savasi (varsayilan kapali).</summary>
    public bool OpenPvp { get; set; }
    /// <summary>Kural 9: Holding kamplari.</summary>
    public bool HoldingCamps { get; set; }
    /// <summary>Kural 10: adanmis sunucu mu (aksi halde dinleme sunucusu).</summary>
    public bool Dedicated { get; set; }
    public RivalrySeason Season { get; set; } = new();
    public List<RivalryTeam> Teams { get; set; } = new();
    public List<RuinPlan> Ruins { get; set; } = new();
    public int NextRuin { get; set; } = 1;

    public RivalryTeam? Team(string id) => Teams.FirstOrDefault(t => t.Id == id);
    public RivalryTeam? TeamOf(string player) => Teams.FirstOrDefault(t => t.Players.Contains(player));
    public int SiloMayaTotal => Teams.Sum(t => t.SiloMaya);
}

public sealed partial class GameState
{
    /// <summary>Cekisme dunyasi: ayar, takimlar, enkaz, Acik Silo, sezon (GDD 22.2).</summary>
    public RivalryState Rivalry { get; private set; } = new();

    [SaveSection("rivalry", 1, Order = SaveOrder.State, Covers = new[] { nameof(Rivalry) })]
    RivalryState RivalrySection
    {
        get => Rivalry;
        set { Rivalry = value ?? new RivalryState(); }
    }

    /// <summary>Ag bolumu ve kurulum: Cekisme durumunu yerine koyar (olay yaymaz).</summary>
    public void ReplaceRivalry(RivalryState s) => Rivalry = s ?? new RivalryState();
}
