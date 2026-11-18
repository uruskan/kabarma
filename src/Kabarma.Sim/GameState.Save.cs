namespace Kabarma.Sim;

// GameState'in kayit parcasi (TEKNIK 9.1). Bolumler [SaveSection] isaretli ozelliklerdir: get DTO uretir, set sessizce
// uygular. Baska akislar kendi bolumlerini kendi partial dosyalarinda ayni kalipla yazar; burada merkezi liste yok.

/// <summary>
/// Dunyadaki orneklerin kalici kimlikleri: hic yeniden kullanilmaz, kayitla saklanir, ag net_id'si ile aynidir (TEKNIK 4).
/// </summary>
public sealed class EntityIds
{
    /// <summary>Verilecek sonraki kimlik (0 "yok" demektir).</summary>
    public ulong Next { get; set; } = 1;
    public ulong New() => Next++;
    /// <summary>Kayittan gelen bir kimlik goruldu: sonraki kimlik onu gecsin.</summary>
    public void Seen(ulong id) { if (id >= Next) Next = id + 1; }
}

/// <summary>
/// Dunyadaki bir ornegin tanimlayicisi (TEKNIK 4): onu iki tarafta da ayni kurmaya yeter. Godot tipi tutmaz.
/// Kind: "station", "furniture", "piece", "pen_prop", "cart", "crate", "creature" ... Content: data/ kimligi.
/// </summary>
public sealed class EntityRecord
{
    public ulong Id { get; set; }
    public string Kind { get; set; } = "";
    public string Content { get; set; } = "";
    /// <summary>x, y, z (m).</summary>
    public float[] Pos { get; set; } = new float[3];
    /// <summary>Dunya donusu: x, y, z, w (dordey). Yalniz yaw gerekiyorsa da dordey yazilir.</summary>
    public float[] Rot { get; set; } = { 0, 0, 0, 1 };
    /// <summary>Ture ozgu ek degerler (can, acik/kapali, ...). Sirali: kararli bayt.</summary>
    public SortedDictionary<string, string> Params { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Bir yuva: esya kimligi, adet, kalite (yildiz 1-3).</summary>
public sealed class StackSave
{
    public string Id { get; set; } = "";
    public int Count { get; set; }
    public int Stars { get; set; } = Inventory.BaseStars;

    public static List<StackSave> Of(Inventory inv) => inv.Stacks.Select(s => new StackSave { Id = s.id, Count = s.count, Stars = s.stars }).ToList();
    public static void Into(Inventory inv, List<StackSave>? list) => inv.Load((list ?? new()).Select(s => (s.Id, s.Count, s.Stars)));
}

public sealed class SaleSave
{
    public string Crop { get; set; } = "";
    public string Size { get; set; } = "";
    public int Value { get; set; }
}

/// <summary>"core": oyunun ozu. Tohum bildirimde de durur; burada denetim icin.</summary>
public sealed class CoreSave
{
    public ulong Seed { get; set; }
    public ulong Rng { get; set; }
    public ulong NextId { get; set; }
    public int Credits { get; set; }
    public List<SaleSave> Sales { get; set; } = new();
    public List<string> Known { get; set; } = new();
}

/// <summary>Bir oyuncunun kisisel durumu (GDD 22.1: canta kisisel). Ortak oyunda oyuncu kimligine gore listede.</summary>
public sealed class PackSave
{
    /// <summary>Oyuncu kimligi: tek oyunculuda <see cref="GameState.LocalPlayer"/>.</summary>
    public string Player { get; set; } = GameState.LocalPlayer;
    public List<StackSave> Backpack { get; set; } = new();
    public List<string?> Belt { get; set; } = new();
    public int BeltSelected { get; set; } = -1;
    public float TankLiters { get; set; }
    public StaminaSave Stamina { get; set; } = new();
    public List<FoodSave> Food { get; set; } = new();
}

/// <summary>Dunya ayari (GDD 25). Etkileri (Kabarma sikligi, dusman hasari, hayvan olumu) ilgili sistemler Preset'e bakarak uygular.</summary>
public sealed class WorldSettings
{
    /// <summary>save.json worldPresets kimligi.</summary>
    public string Preset { get; set; } = "";
}

public sealed class PacksSave
{
    public List<PackSave> Players { get; set; } = new();
}

public sealed partial class GameState
{
    /// <summary>Tek oyunculuda oyuncunun kimligi (kisisel bolumlerin anahtari).</summary>
    public const string LocalPlayer = "p0";

    EntityIds? _ids;
    /// <summary>Dunyadaki orneklerin kalici kimlikleri (TEKNIK 9.1 kimlikle basvuru).</summary>
    public EntityIds Ids => _ids ??= new EntityIds();

    /// <summary>Bu GameState'in butun kayit bolumleri (yansimayla: [SaveSection] isaretli ozellikler).</summary>
    public List<ISaveSection> SaveSections() => SaveSection.FromAttributes(this);

    /// <summary>Bildirimin oyun parcasi: tohum ve oyun gunu.</summary>
    public SaveManifest Manifest(string name = "", string reason = "") => new()
    {
        Seed = Seed,
        Day = Calendar.Running ? Calendar.Day : 1,
        Name = name,
        Reason = reason,
        Crew = new List<string> { LocalPlayer },
        Preset = Settings.Preset,
    };

    WorldSettings? _settings;
    /// <summary>Dunya ayari (GDD 25): yeni oyunda secilir, dunyayla saklanir.</summary>
    public WorldSettings Settings => _settings ??= new WorldSettings { Preset = Data.Save.DefaultPreset };

    [SaveSection("world.settings", 1, Order = SaveOrder.Spawn - 50, Covers = new[] { nameof(Settings) })]
    WorldSettings SettingsSection
    {
        get => new() { Preset = Settings.Preset };
        set => Settings.Preset = string.IsNullOrEmpty(value.Preset) ? Data.Save.DefaultPreset : value.Preset;
    }

    [SaveSection("core", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Seed), nameof(Rng), nameof(Ids), nameof(Credits), nameof(Sales), nameof(Known) })]
    CoreSave CoreSection
    {
        get => new()
        {
            Seed = Seed,
            Rng = Rng.State,
            NextId = Ids.Next,
            Credits = Credits,
            Sales = Sales.Select(s => new SaleSave { Crop = s.crop, Size = s.size, Value = s.value }).ToList(),
            Known = Known.OrderBy(k => k, StringComparer.Ordinal).ToList(),
        };
        set
        {
            if (value.Seed != Seed) throw new SaveException($"kayit tohumu {value.Seed}, oyun {Seed} ile kuruldu (once ayni tohumla GameState kur)");
            Rng.State = value.Rng;
            Ids.Next = Math.Max(1, value.NextId);
            Credits = value.Credits;
            Sales.Clear();
            Sales.AddRange(value.Sales.Select(s => (s.Crop, s.Size, s.Value)));
            Known.Clear();
            foreach (var k in value.Known) Known.Add(k);
        }
    }

    [SaveSection("player.pack", 1, Order = SaveOrder.State, Covers = new[] { nameof(Backpack), nameof(Eggs), nameof(Belt), nameof(Tank), nameof(Stamina), nameof(Food) })]
    PacksSave PackSection
    {
        get => new()
        {
            Players = new()
            {
                new PackSave
                {
                    Player = LocalPlayer,
                    Backpack = StackSave.Of(Backpack),
                    Belt = Belt.Ids.ToList(),
                    BeltSelected = Belt.Selected,
                    TankLiters = Tank.Liters,
                    Stamina = Stamina.Save(),
                    Food = Food.Save(),
                },
            },
        };
        set
        {
            var p = value.Players.FirstOrDefault(x => x.Player == LocalPlayer) ?? value.Players.FirstOrDefault();
            if (p == null) return;
            StackSave.Into(Backpack, p.Backpack); // once canta: Changed yaymaz, kemer kendiliginden dolmaz
            Belt.Load(p.Belt, p.BeltSelected);
            Tank.Restore(p.TankLiters);
            Stamina.Load(p.Stamina ?? new StaminaSave { Value = Stamina.Max });
            Food.Load(p.Food ?? new(), id => Data.Tool.Foods.FirstOrDefault(f => f.Id == id));
        }
    }
}
