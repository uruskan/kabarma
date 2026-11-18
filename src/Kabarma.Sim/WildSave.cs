namespace Kabarma.Sim;

// Yabani yaratiklarin kayit bolumu "wildlife" (TEKNIK §9.1): Godot katmanindaki Wildlife dugumu kaydeder ve geri kurar
// (Director.Save, SaveOrder.Spawn). Butun alanlar duz; aktorler kimlikle ve konumla anilir.

public sealed class WildMemberSave
{
    public WildState State { get; set; } = new();
    public float Y { get; set; }
    public float Yaw { get; set; }
}

public sealed class WildGroupSave
{
    /// <summary>Dogma satirinin biyomu ve turu (yeniden dogma sayaci icin).</summary>
    public string Biome { get; set; } = "";
    public string Creature { get; set; } = "";
    public WildHerdState? Herd { get; set; }
    public List<WildMemberSave> Members { get; set; } = new();
    public SwarmState? Swarm { get; set; }
}

public sealed class WildlifeSave
{
    public int NextId { get; set; } = 1;
    public ulong Rand { get; set; }
    public List<WildGroupSave> Groups { get; set; } = new();
    /// <summary>"biyom:tur" -> yeni surunun gelmesine kalan sure (sn).</summary>
    public Dictionary<string, float> Respawn { get; set; } = new();
}
