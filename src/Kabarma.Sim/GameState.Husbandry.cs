namespace Kabarma.Sim;

/// <summary>
/// Hayvancilik kaydi (TEKNIK 9.1): saat, kimlik sayaci, RNG durumu, gubre yiginlari ve her hayvanin M2 alanlari.
/// Hayvanlarin M1 alanlari (tur, guven, kayit) kayit akisinin "pen.animals" bolumunde; bu bolum ondan sonra (Progress)
/// okunur ve ayni siradaki hayvanlara M2 alanlarini yazar. Suluk, Firca, Kovan, Kulucka icerikleri Yemlik ve Folluk
/// gibi sahibi yerlestirilebilirin kaydinda (Godot "pen.props").
/// </summary>
public sealed class RanchSave
{
    public double Clock { get; set; }
    public int NextId { get; set; } = 1;
    public ulong Rng { get; set; }
    public List<AnimalExtraSave> Animals { get; set; } = new();
    public List<ManurePile> Manure { get; set; } = new();
}

public sealed class AnimalExtraSave
{
    public string Species { get; set; } = "";
    public int Id { get; set; }
    public AnimalGenes Genes { get; set; } = new();
    public float Happiness { get; set; }
    public float AgeDays { get; set; }
    public string Role { get; set; } = "";
    public string Command { get; set; } = "";
    public SortedDictionary<string, float> Timers { get; set; } = new(StringComparer.Ordinal);
    public List<ProductStack> Pending { get; set; } = new();
    public int LastPetDay { get; set; }
    public float LowSeconds { get; set; }
    public bool WantsOut { get; set; }
    public float BreedTimer { get; set; }
    public int MateId { get; set; }
    public int MotherId { get; set; }
    public int FatherId { get; set; }
    public bool Bound { get; set; }
    public int HiveId { get; set; }
    public SortedDictionary<string, int> Pack { get; set; } = new(StringComparer.Ordinal);
    public float X { get; set; }
    public float Z { get; set; }
}

public sealed partial class GameState
{
    Ranch? _ranch;

    /// <summary>
    /// Hayvancilik (GDD 9.1-9.4): Coop'un hayvanlari, Yemlik ve Folluk'u uzerine ihtiyac, mutluluk, gen, ureme, rol,
    /// kacma; Suluk, Kasinma Fircasi, Ari Kovani, Kulucka. Kurucuya dokunmamak icin ilk eriste kurulur.
    /// </summary>
    public Ranch Ranch => _ranch ??= new Ranch(Coop, Data.Pen, Data.Husbandry, Data.Calendar, Seed);

    [SaveSection("ranch", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Ranch) })]
    RanchSave RanchSection
    {
        get => new()
        {
            Clock = Ranch.Clock,
            NextId = Ranch.NextId,
            Rng = Ranch.Rng.State,
            Manure = Ranch.Manure.Select(m => new ManurePile { Id = m.Id, Count = m.Count, X = m.X, Z = m.Z }).ToList(),
            Animals = Coop.Animals.Select(a => new AnimalExtraSave
            {
                Species = a.Species, Id = a.Id, Genes = new AnimalGenes { B = a.Genes.B, U = a.Genes.U, V = a.Genes.V, K = a.Genes.K },
                Happiness = a.Happiness, AgeDays = a.AgeDays, Role = a.Role, Command = a.Command,
                Timers = new SortedDictionary<string, float>(a.Timers, StringComparer.Ordinal),
                Pending = a.Pending.Select(p => new ProductStack { Item = p.Item, Count = p.Count, Stars = p.Stars }).ToList(),
                LastPetDay = a.LastPetDay, LowSeconds = a.LowSeconds, WantsOut = a.WantsOut, BreedTimer = a.BreedTimer,
                MateId = a.MateId, MotherId = a.MotherId, FatherId = a.FatherId, Bound = a.Bound, HiveId = a.HiveId,
                Pack = new SortedDictionary<string, int>(a.Pack, StringComparer.Ordinal), X = a.X, Z = a.Z,
            }).ToList(),
        };
        set
        {
            var r = Ranch;
            r.Clock = value.Clock;
            r.NextId = Math.Max(1, value.NextId);
            if (value.Rng != 0) r.Rng.State = value.Rng;
            r.Manure.Clear();
            r.Manure.AddRange(value.Manure ?? new());
            var list = value.Animals ?? new();
            // "pen.animals" ayni sirayla geri kurdu; kayitta fazlasi varsa (M2 yavrulari) M1 alanlariyla eklenir.
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                PenAnimal a;
                if (i < Coop.Animals.Count && Coop.Animals[i].Species == e.Species) a = Coop.Animals[i];
                else { a = new PenAnimal { Species = e.Species, Trust = 1f, Registered = true }; Coop.Animals.Add(a); }
                a.Id = e.Id; a.Genes = e.Genes ?? new(); a.Happiness = e.Happiness; a.AgeDays = e.AgeDays; a.Role = e.Role ?? "";
                a.Command = e.Command ?? "izle"; a.Timers = new Dictionary<string, float>(e.Timers ?? new());
                a.Pending = e.Pending ?? new(); a.LastPetDay = e.LastPetDay; a.LowSeconds = e.LowSeconds; a.WantsOut = e.WantsOut;
                a.BreedTimer = e.BreedTimer; a.MateId = e.MateId; a.MotherId = e.MotherId; a.FatherId = e.FatherId; a.Bound = e.Bound;
                a.HiveId = e.HiveId; a.Pack = new Dictionary<string, int>(e.Pack ?? new()); a.X = e.X; a.Z = e.Z;
            }
        }
    }
}
