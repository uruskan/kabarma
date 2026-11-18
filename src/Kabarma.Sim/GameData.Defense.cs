namespace Kabarma.Sim;

// defense.json: kuleler ve tuzaklar (GDD §16.4 M3 kurallari, ICERIK §13).

public sealed class TowerDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; } = 1;
    /// <summary>single: tek hedef, one nisanli · arc: kavis, alan · cone: koni, sacma.</summary>
    public string Kind { get; set; } = "single";
    /// <summary>Kabul edilen cephane esyalari (sirayla).</summary>
    public List<string> Ammo { get; set; } = new();
    public int Magazine { get; set; } = 50;
    public float Range { get; set; } = 10f;
    public float MinRange { get; set; }
    public float Interval { get; set; } = 1f;
    public float Damage { get; set; } = 5f;
    /// <summary>Hasar turu (GDD §16.1); savas akisinin Combat sozlesmesi gelene dek bilgi icin.</summary>
    public string DamageType { get; set; } = "darbe";
    /// <summary>single: mermi hizi (m/sn), one nisan bunu kullanir.</summary>
    public float Speed { get; set; } = 40f;
    /// <summary>arc: dusus alaninin yaricapi.</summary>
    public float Splash { get; set; }
    public float FlightSeconds { get; set; } = 1f;
    public float Gravity { get; set; } = 14f;
    /// <summary>cone: sacma tanesi ve koni acisi (tam aci).</summary>
    public int Pellets { get; set; } = 1;
    public float ConeDegrees { get; set; } = 30f;
    public float Hp { get; set; } = 300f;
    public float WearPerShot { get; set; } = 1f;
    /// <summary>Namlunun yerden yuksekligi (atis noktasi).</summary>
    public float HeadHeight { get; set; } = 2f;
}

public sealed class TrapDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; } = 1;
    /// <summary>spikes · sticky · launch · pit.</summary>
    public string Kind { get; set; } = "spikes";
    /// <summary>Kare etki alaninin yari kenari (yerel, donusuyle).</summary>
    public float HalfSize { get; set; } = 1f;
    public float Dps { get; set; }
    /// <summary>spikes: hiz carpani (0.5 = yari hiz) ve suresi.</summary>
    public float Slow { get; set; } = 1f;
    public float SlowSeconds { get; set; }
    public float TriggerEvery { get; set; } = 0.5f;
    public float HoldSeconds { get; set; }
    public float ImmuneSeconds { get; set; }
    public float Distance { get; set; }
    public float Apex { get; set; }
    public float FlightSeconds { get; set; } = 1f;
    public float Damage { get; set; }
    public float RearmSeconds { get; set; } = 1f;
    public bool AffectsPlayers { get; set; }
    public float PlayerUpSpeed { get; set; }
    public float PlayerForwardSpeed { get; set; }
    public int Capacity { get; set; } = 1;
    public float Depth { get; set; } = 1f;
    public float Hp { get; set; } = 300f;
    public float WearPerTrigger { get; set; } = 1f;
}

public sealed class DefenseFile
{
    /// <summary>Oncelik sirasi; "off" kapali (E basili ile dongu).</summary>
    public List<string> Priorities { get; set; } = new() { "nearest", "strongest", "flying", "thief", "off" };
    public float RepairFraction { get; set; } = 0.25f;
    public float InteractHoldSeconds { get; set; } = 0.45f;
    public List<TowerDef> Towers { get; set; } = new();
    public List<TrapDef> Traps { get; set; } = new();

    public TowerDef? Tower(string id) => Towers.FirstOrDefault(t => t.Id == id);
    public TrapDef? Trap(string id) => Traps.FirstOrDefault(t => t.Id == id);
    public bool Handles(string id) => Tower(id) != null || Trap(id) != null;
    public float MaxHp(string id) => Tower(id)?.Hp ?? Trap(id)?.Hp ?? 0f;
}

public sealed partial class GameData
{
    [DataFile("defense.json")]
    public DefenseFile Defense { get; private set; } = new();
}
