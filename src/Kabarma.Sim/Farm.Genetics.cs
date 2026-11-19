namespace Kabarma.Sim;

/// <summary>Duzlemde bir nokta (tarla karesi biriminde, m): Korkuluk, kovan. Duz ve serilestirilebilir.</summary>
public sealed class FarmPoint
{
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>Hasat sonucu (GDD 8.2-8.3): urun, kalite, tohumlar ve soylari.</summary>
public sealed class HarvestResult
{
    public string Crop { get; init; } = "";
    /// <summary>Cantaya giren esya kimligi (Alaca ise "turnip_altin").</summary>
    public string Item { get; init; } = "";
    public int Yield { get; init; }
    public int Stars { get; init; }
    public int Seeds { get; init; }
    public string SeedItem { get; init; } = "";
    public string? Alaca { get; init; }
    public bool Crossed { get; init; }
    public bool Wild { get; init; }
    public List<SeedLot> SeedLots { get; init; } = new();
}

/// <summary>Tarla: genler, Tohum Kaydi, caprazlama, Alaca, gunduz zararlisi, Korkuluk, yabani bitki (GDD 8.2-8.3).</summary>
public sealed partial class Farm
{
    /// <summary>Tapunun Tohum Kaydi (genler, Alaca).</summary>
    public SeedLedger Seeds { get; set; } = new();
    /// <summary>Defter'in Islah notlari.</summary>
    public BreedingLog Log { get; set; } = new();
    /// <summary>Tarlanin isleyen saati (sn); notlar icin.</summary>
    public float Time { get; set; }
    /// <summary>Kurulu Korkuluklar (Godot katmani her saniye yazar).</summary>
    public List<FarmPoint> Scarecrows { get; set; } = new();
    /// <summary>Tozlayicilar (Vizviz kovani; hayvan akisi yazar). Caprazlama icin.</summary>
    public List<FarmPoint> Pollinators { get; set; } = new();
    /// <summary>Toprak basliginda secili tohum (ekin kimligi).</summary>
    public string Selected { get; set; } = "turnip";

    /// <summary>Gunduz zararlisi isirdi (Zarar artti, ekin yenmedi).</summary>
    public event Action<Plot>? Bitten;
    /// <summary>Yabani bitki kuruldu ya da yeniden bitti.</summary>
    public event Action<Plot>? WildGrown;

    GeneticsRules G => _data.Crops.Genetics;

    /// <summary>Nem carpani, D ile: esikler 0,5 x (D-1) iner; su seven ekin 80 ustunde x1 (GDD 8.2, 8.3).</summary>
    public float MoistureFactor(float moisture, int d, bool wetLoving)
    {
        var m = _data.Crops.Moisture;
        float shift = G.DMoistureShift * Math.Max(0, d - 1);
        if (moisture < m.StopBelow - shift) return 0f;
        if (moisture < m.HalfBelow - shift) return 0.5f;
        if (moisture > m.WetAbove) return wetLoving ? 1f : m.WetFactor;
        return 1f;
    }

    /// <summary>Sicaklik: biyom bandinda x1, disinda 0,25 + 0,03 x (D-1) (GDD 8.2, 8.3).</summary>
    public float BandFactor(CropDef? def, int d) => BandFactor(def, d, false);

    /// <summary>Seradaki kare bandi duzeltir (ICERIK 5): greenhouse.bandFactor.</summary>
    public float BandFactor(CropDef? def, int d, bool greenhouse)
    {
        if (def == null) return 1f;
        if (greenhouse) return G.Greenhouse.BandFactor;
        string band = G.Bands.TryGetValue(def.Band, out var b) ? b : def.Band;
        if (string.IsNullOrEmpty(G.FarmBand) || band == G.FarmBand) return 1f;
        return Math.Min(1f, G.OutOfBand + G.DBandBonus * Math.Max(0, d - 1));
    }

    /// <summary>Su basmis ekin sig su karesinde x1, baska yerde greenhouse.floodedOutOfWater.</summary>
    public float FloodFactor(CropDef? def, Plot p) => def?.Flooded == true && !p.Flooded ? G.Greenhouse.FloodedOutOfWater : 1f;

    /// <summary>Biyom bandinda mi (ekilen yer ekinin bandinda; sera ya da ana tapu bandi)?</summary>
    public bool InBand(CropDef def, bool greenhouse) => BandFactor(def, 1, greenhouse) >= 1f;

    public bool SetGreenhouse(int x, int y, bool on) { var p = Get(x, y); if (p == null) return false; p.Greenhouse = on; return true; }
    public bool SetFlooded(int x, int y, bool on) { var p = Get(x, y); if (p == null) return false; p.Flooded = on; if (on) p.Moisture = 100f; return true; }

    public float FertilityLoss(Plot p)
    {
        var def = p.Crop != null ? _data.FindCrop(p.Crop) : null;
        return def?.FertilityLoss ?? _data.Crops.HarvestFertilityLoss;
    }

    public void SetScarecrows(IEnumerable<(float x, float z)> pts) { Scarecrows.Clear(); foreach (var (x, z) in pts) Scarecrows.Add(new FarmPoint { X = x, Z = z }); }
    public void SetPollinators(IEnumerable<(float x, float z)> pts) { Pollinators.Clear(); foreach (var (x, z) in pts) Pollinators.Add(new FarmPoint { X = x, Z = z }); }

    static float Dist(Plot p, FarmPoint q) { float dx = p.X + 0.5f - q.X, dz = p.Y + 0.5f - q.Z; return MathF.Sqrt(dx * dx + dz * dz); }

    /// <summary>Korkuluk (10 m) ve Sarimsak (kendi karesi + 8 komsu) korumasi.</summary>
    public bool IsProtected(Plot p)
    {
        foreach (var s in Scarecrows) if (Dist(p, s) <= G.ScarecrowRadius) return true;
        int w = G.WardCells;
        for (int dx = -w; dx <= w; dx++)
            for (int dy = -w; dy <= w; dy++)
                if (_plots.TryGetValue((p.X + dx, p.Y + dy), out var q) && q.Planted && !q.Wild && q.Crop != null && _data.FindCrop(q.Crop)?.WardsPests == true) return true;
        return false;
    }

    float _protT = float.MaxValue;
    /// <summary>Koruma bayragi saniyede bir yenilenir (Korkuluk/Sarimsak yavas degisir; PestBite her isirikta yeniden bakar).</summary>
    void UpdateProtection(float dt)
    {
        _protT += dt;
        if (_protT < 1f) return;
        _protT = 0f;
        bool garlic = false;
        foreach (var p in _plots.Values) if (p.Planted && p.Crop != null && _data.FindCrop(p.Crop)?.WardsPests == true) { garlic = true; break; }
        foreach (var p in _plots.Values) p.Protected = (Scarecrows.Count > 0 || garlic) && IsProtected(p);
    }

    /// <summary>Gunduz zararlisinin bir isirigi (GDD 8.2). Korunan ekine islemez. Ekin yendiyse true.</summary>
    public bool PestBite(int x, int y)
    {
        var p = Get(x, y);
        if (p == null || !p.Planted || p.Swollen != null) return false;
        if (IsProtected(p)) { p.Protected = true; return false; }
        float dmg = Math.Max(0f, G.BiteDamage * (1f - G.DBiteReduce * p.Genes.D));
        p.PestDamage = Math.Min(1f, p.PestDamage + dmg);
        if (p.PestDamage >= 1f) return Eat(x, y);
        Bitten?.Invoke(p);
        return false;
    }

    /// <summary>Tapu disina yabani bitki koyar (olgun, genleri 1-3; Alaca zari atilir). Kare doluysa null.</summary>
    public Plot? SpawnWild(int x, int y, string cropId, Rng rng)
    {
        if (_plots.ContainsKey((x, y)) || _data.FindCrop(cropId) == null) return null;
        var p = new Plot { X = x, Y = y, Wild = true, Crop = cropId, Growth = 1f, Fertility = _data.Crops.StartFertility, Moisture = 60f };
        p.Genes = Genetics.Wild(G, rng);
        p.Alaca = Genetics.RollAlaca(G, null, rng);
        _plots[(x, y)] = p;
        WildGrown?.Invoke(p);
        return p;
    }

    void TickWild(Plot p, float dt, float day)
    {
        if (p.Planted) return;
        p.RegrowT += dt;
        if (p.RegrowT < G.Wild.RegrowDays * day) return;
        p.RegrowT = 0; p.Harvested = false; p.Eaten = false; p.Growth = 1f; p.PestDamage = 0f; p.NeverDry = true;
        WildGrown?.Invoke(p);
    }

    /// <summary>Caprazlama esi: ayni aileden, crossRadius icinde ekili bitki ve pollinatorRadius icinde tozlayici (GDD 8.3).</summary>
    public Plot? CrossPartner(Plot p)
    {
        if (p.Wild || p.Crop == null || Pollinators.Count == 0) return null;
        if (!Pollinators.Any(q => Dist(p, q) <= G.PollinatorRadius)) return null;
        var fam = _data.FindCrop(p.Crop)?.Family;
        int r = (int)MathF.Ceiling(G.CrossRadius);
        Plot? best = null; float bd = float.MaxValue;
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (!_plots.TryGetValue((p.X + dx, p.Y + dy), out var q) || q.Wild || !q.Planted || q.Crop == null) continue;
                if (_data.FindCrop(q.Crop)?.Family != fam) continue;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d <= G.CrossRadius && d < bd) { bd = d; best = q; }
            }
        return best;
    }

    /// <summary>
    /// Bicme ile hasat (GDD 8.2-8.3): verim = taban x (1 + 0,08 x V) x kalite x (art arda farkli aile +%10) x (1 - Zarar);
    /// her tohum bitkinin genlerini alir (es varsa caprazlama), mutasyon zari; tohumlar Tohum Kaydi'na yazilir.
    /// Kalici cali Genc evreye doner; yabani bitki 2 buyume gunu sonra yeniden biter.
    /// </summary>
    public HarvestResult? HarvestFull(int x, int y, Rng rng)
    {
        var p = Get(x, y);
        if (p == null || !p.Planted || p.Stage != CropStage.Mature) return null;
        var def = _data.Crop(p.Crop!);
        int stars = Stars(p);
        float q = QualityMult(stars);
        // Kabarik urun bicilirse ya da toplanmadan sonduyse iki kat normal urun (GDD 8.4).
        float raw = def.BaseYield * (1f + 0.08f * p.Genes.V) * q * (p.Swollen != null || p.Deflated ? _data.Crops.DeflateYieldMult : 1f)
            * (p.RotationBonus ? 1f + G.RotationBonus : 1f) * (1f - p.PestDamage);
        int yield = (int)MathF.Floor(raw) + (rng.Chance(raw - MathF.Floor(raw)) ? 1 : 0);

        var partner = CrossPartner(p);
        var lots = new List<SeedLot>();
        for (int i = 0; i < def.SeedReturn; i++)
        {
            var g = partner != null ? Genetics.Cross(G, p.Genes, partner.Genes, rng) : Genetics.Inherit(G, p.Genes, rng);
            Seeds.Add(def.Id, g, p.Alaca);
            var same = lots.FirstOrDefault(l => l.Genes.SameAs(g));
            if (same != null) same.Count++; else lots.Add(new SeedLot { Crop = def.Id, Genes = g, Alaca = p.Alaca, Count = 1 });
            int parentMax = partner != null ? Math.Max(p.Genes.Sum, partner.Genes.Sum) : p.Genes.Sum;
            if (g.Sum - parentMax >= 2) Log.Note(G, "jump", def.Id, g, p.Alaca, Time);
            Log.Offer(G, def.Id, g, Time);
        }
        if (partner != null) { Log.Crosses++; Log.Note(G, "cross", def.Id, p.Genes, p.Alaca, Time); }
        if (p.Alaca != null) { Log.AlacaFinds++; Log.Note(G, "alaca", def.Id, p.Genes, p.Alaca, Time); }

        var result = new HarvestResult
        {
            Crop = def.Id,
            Item = p.Alaca != null ? Genetics.AlacaItem(def.Id, p.Alaca) : def.ProductId,
            Yield = Math.Max(1, yield),
            Stars = stars,
            Seeds = def.SeedReturn,
            SeedItem = def.SeedId,
            Alaca = p.Alaca,
            Crossed = partner != null,
            Wild = p.Wild,
            SeedLots = lots,
        };

        if (p.Wild) { p.Harvested = true; p.RegrowT = 0; }
        else
        {
            p.Fertility = Math.Max(0f, p.Fertility - FertilityLoss(p));
            p.PrevFamily = def.Family;
            if (def.Perennial && p.Swollen == null)
            {
                // Kalici cali: Genc evreye doner, ayni bitki yeniden olgunlasir.
                p.Growth = G.RegrowFrom; p.Regrowing = true; p.PestDamage = 0f; p.NeverDry = true; p.Deflated = false;
            }
            else p.Harvested = true;
        }
        Harvested?.Invoke(p);
        return result;
    }
}
