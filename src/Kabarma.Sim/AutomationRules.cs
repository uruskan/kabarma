namespace Kabarma.Sim;

/// <summary>
/// Otomasyon makinelerinin kurallari (GDD 11.4, ICERIK §11, §12; M4 tasima). Hepsi duz ve Godot'suz: girdi ve cikti tapu
/// icindeki Sandiklardan (Inventory listesi), dunya konumu (x, z) ve yaricap verilir. Makinenin zamanlayicisi ve gucu Godot
/// katmaninda (Station.Automation.cs); burada bir "tur"un ne yaptigi. automation.json machines.
/// </summary>
public static class AutomationRules
{
    static float Dist(float ax, float az, float bx, float bz) => MathF.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

    // ---------- Sandik listesi ----------

    public static int Count(IReadOnlyList<Inventory> chests, string id) => chests.Sum(c => c.Count(id));
    public static int Room(IReadOnlyList<Inventory> chests, string id) => chests.Sum(c => c.Room(id));

    /// <summary>Esyayi ilk yeri olan Sandiklara koyar. Donen: konan.</summary>
    public static int AddAll(IReadOnlyList<Inventory> chests, string id, int n, int stars = 0)
    {
        int put = 0;
        foreach (var c in chests)
        {
            if (put >= n) break;
            put += c.Add(id, n - put, stars);
        }
        return put;
    }

    // ---------- Hasat Robotu, Ekim Robotu ----------

    /// <summary>
    /// Hasat Robotu: yaricaptaki olgun (kabarik ve yabani olmayan) ekinleri hasat eder; urun ve tohum Sandiklara girer.
    /// Urune yer yoksa o kareye dokunmaz. Donen: hasat edilen kare. En cok <paramref name="maxPlots"/> kare.
    /// </summary>
    public static int Harvest(GameData d, Farm farm, Rng rng, float x, float z, float r, IReadOnlyList<Inventory> chests, int maxPlots = 4)
    {
        int n = 0;
        foreach (var p in farm.Plots.OrderBy(p => Dist(p.X + 0.5f, p.Y + 0.5f, x, z)).ToList())
        {
            if (n >= maxPlots) break;
            if (!p.Planted || p.Stage != CropStage.Mature || p.Swollen != null || Dist(p.X + 0.5f, p.Y + 0.5f, x, z) > r) continue;
            if (Room(chests, p.Crop!) < 1) continue; // urune yer yok: bekler
            var h = farm.HarvestFull(p.X, p.Y, rng);
            if (h == null) continue;
            AddAll(chests, h.Item, h.Yield, h.Stars);
            AddAll(chests, h.SeedItem, h.Seeds);
            n++;
        }
        return n;
    }

    /// <summary>
    /// Ekim Robotu: yaricaptaki bos (surulmus, ekilmemis ya da hasat edilmis) kareye Sandiktaki ilk tohumu eker (tohum turu
    /// sirayla; ayni turu tercih eder: kareden son hasat edilen ekin). Donen: ekilen kare.
    /// </summary>
    public static int Plant(GameData d, Farm farm, Rng rng, float x, float z, float r, IReadOnlyList<Inventory> chests, int maxPlots = 4)
    {
        int n = 0;
        foreach (var p in farm.Plots.OrderBy(p => Dist(p.X + 0.5f, p.Y + 0.5f, x, z)).ToList())
        {
            if (n >= maxPlots) break;
            if (p.Planted || p.Wild || Dist(p.X + 0.5f, p.Y + 0.5f, x, z) > r) continue;
            var crop = p.Crop != null && d.FindCrop(p.Crop) is { } last && Count(chests, last.SeedId) > 0 ? last : FirstSeedCrop(d, chests);
            if (crop == null) break; // tohum kalmadi
            var inv = chests.First(c => c.Count(crop.SeedId) > 0);
            if (farm.Plant(p.X, p.Y, crop.Id, inv, rng)) n++;
        }
        return n;
    }

    static CropDef? FirstSeedCrop(GameData d, IReadOnlyList<Inventory> chests) =>
        d.Crops.Crops.FirstOrDefault(c => Count(chests, c.SeedId) > 0);

    // ---------- Sera Kontrol Unitesi ----------

    /// <summary>
    /// Sera Kontrol Unitesi: beslenirken yaricaptaki tarla karelerini sera sayar (biyom bandindan bagimsiz sicaklik bandi,
    /// GDD 8); beslenmezken ya da yaricap disina cikinca birakir. Yalniz bu uniteninin actigi kareleri kapatir (<paramref name="mine"/>).
    /// </summary>
    public static int Greenhouse(Farm farm, float x, float z, float r, bool powered, HashSet<(int, int)> mine)
    {
        int n = 0;
        foreach (var p in farm.Plots)
        {
            bool inside = Dist(p.X + 0.5f, p.Y + 0.5f, x, z) <= r;
            if (powered && inside) { if (!p.Greenhouse) { farm.SetGreenhouse(p.X, p.Y, true); mine.Add((p.X, p.Y)); } n++; }
            else if (mine.Remove((p.X, p.Y))) farm.SetGreenhouse(p.X, p.Y, false);
        }
        return n;
    }

    // ---------- Otomatik Yemlik, Yumurta Toplayici, Sagim-Kirkim Kolu ----------

    /// <summary>Otomatik Yemlik: bos yeri olan Yemlik'leri Sandiktaki yemle doldurur (dolu Yemlik kendi yemini, bos olan istenen yemlerden ilkini alir). Donen: konan yem.</summary>
    public static int Feed(IEnumerable<Feeder> feeders, IReadOnlyList<Inventory> chests, IReadOnlyList<string> wanted)
    {
        int total = 0;
        foreach (var f in feeders)
        {
            if (f.Room <= 0) continue;
            string? food = f.Amount > 0 ? f.Food : wanted.FirstOrDefault(w => Count(chests, w) > 0);
            if (food == null) continue;
            foreach (var c in chests) { if (f.Room <= 0) break; total += HusbandryRules.Fill(f, c, food); }
        }
        return total;
    }

    /// <summary>Yumurta Toplayici: Folluk'lardaki yumurtalari Sandiklara tasir. Donen: tasinan.</summary>
    public static int CollectEggs(IEnumerable<Nest> nests, IReadOnlyList<Inventory> chests, string product)
    {
        int total = 0;
        foreach (var nest in nests)
            foreach (var c in chests)
            {
                if (nest.Eggs <= 0) break;
                total += CoopRules.Collect(nest, c, product);
            }
        return total;
    }

    /// <summary>Sagim-Kirkim Kolu: yaricaptaki evcil hayvanlarin ustundeki sut ve yun gibi urunleri (items) Sandiga alir. Donen: alinan.</summary>
    public static int Milk(IEnumerable<PenAnimal> animals, float x, float z, float r, IReadOnlyList<string> items, IReadOnlyList<Inventory> chests)
    {
        int total = 0;
        foreach (var a in animals)
        {
            if (!a.Tame || Dist(a.X, a.Z, x, z) > r) continue;
            foreach (var s in a.Pending.Where(s => items.Contains(s.Item)).ToList())
            {
                int put = 0;
                foreach (var c in chests) { if (put >= s.Count) break; put += c.Add(s.Item, s.Count - put, s.Stars); }
                total += put;
                s.Count -= put;
                if (s.Count <= 0) a.Pending.Remove(s);
            }
        }
        return total;
    }

    // ---------- Paketleyici ----------

    /// <summary>
    /// Paketleyici: Sandiklardaki satilabilir esyayi acik kasaya doldurur; kasa dolunca (ya da <paramref name="flush"/>) kapatip
    /// rampaya yukler. Donen: kasaya konan adet. Rampayi yollamak Godot'ta (Director.MachineLaunchRamp).
    /// </summary>
    public static int Pack(Cargo cargo, IReadOnlyList<Inventory> chests, bool flush, out bool crateReady)
    {
        int packed = 0;
        foreach (var c in chests)
            foreach (var (id, count) in c.Contents.ToList())
            {
                if (!cargo.Pricing.Sellable(id)) continue;
                packed += cargo.Pack(c, id, count);
            }
        // Kasa doldu mu: yeni yigin acilamiyorsa (tum yuvalar dolu) ya da boslugu bekletmeden gonderme istendi.
        crateReady = false;
        if (cargo.Open != null && (flush || cargo.Open.Items.UsedSlots >= cargo.Open.Items.Slots))
        {
            var closed = cargo.Close();
            if (closed != null && cargo.CanLoadCrate) { var crate = cargo.TakeClosed(); if (crate != null && cargo.LoadCrate(crate)) crateReady = true; }
        }
        return packed;
    }

    // ---------- Sondaj Kulesi ----------

    /// <summary>Sondaj Kulesi: Zengin Yatak'in bulundugu biyomun cevheri (automation.json ores); biyom bilinmiyorsa null.</summary>
    public static string? DrillOre(GameData d, string biome) =>
        d.Automation.Machines.TryGetValue("sondaj_kulesi", out var m) && m.Ores.TryGetValue(biome, out var ore) ? ore : null;
}

/// <summary>Montaj Makinesi (ICERIK §11): secilen tarifi kendi uretir. Secim duz durumdur ve kaydolur.</summary>
public sealed class AssemblerState
{
    /// <summary>Secili tarifin kimligi (urun kimligi); bos: secim yok.</summary>
    public string Recipe { get; set; } = "";

    /// <summary>Secilebilir tarifler: Tezgah'ta yapilan ve yuk olmayan, kilidi acik olanlar (alet, malzeme, yerlestirilebilir).</summary>
    public static IEnumerable<RecipeDef> Candidates(GameData d, ISet<string>? known) =>
        d.Tool.Recipes.Where(r => !r.Load && (r.Station == "tezgah" || r.Station == "") && (r.UnlockOn == null || known == null || known.Contains(r.UnlockOn)));

    /// <summary>Montaj Makinesi'nin kuyruguna girecek kopya: ayni girdi ve urun, istasyon montaj makinesi.</summary>
    public static RecipeDef Proxy(RecipeDef r) => new()
    {
        Id = r.Id, Name = r.Name, Station = "montaj_makinesi", Category = r.Category, In = new Dictionary<string, int>(r.In),
        Out = r.Out, Seconds = r.Seconds, Load = false, UnlockOn = null,
    };

    /// <summary>Secimi bir sonrakine ceviren (bos dahil, dongu).</summary>
    public string Next(GameData d, ISet<string>? known)
    {
        var ids = new List<string> { "" };
        ids.AddRange(Candidates(d, known).Select(r => r.Id));
        int i = ids.IndexOf(Recipe);
        Recipe = ids[(i + 1) % ids.Count];
        return Recipe;
    }
}

public sealed partial class GameState
{
    /// <summary>Montaj Makinesi'nin secili tarifi (kayit bolumu "assembler").</summary>
    public AssemblerState Assembler { get; private set; } = new();

    [SaveSection("assembler", 1, Order = SaveOrder.State, Covers = new[] { nameof(Assembler) })]
    AssemblerState AssemblerSection { get => Assembler; set => Assembler = value ?? new AssemblerState(); }
}
