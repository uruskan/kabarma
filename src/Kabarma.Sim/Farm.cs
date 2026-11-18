namespace Kabarma.Sim;

public enum CropStage { Seed, Sprout, Young, Mature }

/// <summary>1 x 1 m'lik tarla karesi (GDD 8.1).</summary>
public sealed class Plot
{
    public int X { get; init; }
    public int Y { get; init; }
    public string? Crop { get; internal set; }
    public Genes Genes { get; internal set; } = new();
    public float Growth { get; internal set; }          // 0..1
    public float Moisture { get; internal set; }        // 0..100
    public float Fertility { get; internal set; }       // 0..100
    public bool NeverDry { get; internal set; } = true; // kalite icin: nem hic 20'nin altina dustu mu
    public SizeClassDef? Swollen { get; internal set; } // kabarik urun (yuk nesnesi) ise boyu
    public bool Eaten { get; internal set; }
    public bool Harvested { get; internal set; }
    /// <summary>Kabarik urunun safaktan beri tarlada bekledigi sure; -1: saat islemiyor (GDD 8.4).</summary>
    public float SinceDawn { get; internal set; } = -1f;
    /// <summary>Kabarik urun toplanmadan sondu: normal olgun ekin, hasadi iki kat (GDD 8.4).</summary>
    public bool Deflated { get; internal set; }

    public bool Planted => Crop != null && !Eaten && !Harvested;
    public CropStage Stage => Growth >= 1f ? CropStage.Mature : Growth >= 0.6f ? CropStage.Young : Growth >= 0.2f ? CropStage.Sprout : CropStage.Seed;
}

/// <summary>Tapu icindeki tarla: surme, ekme, sulama, buyume, kabarma (GDD 8).</summary>
public sealed partial class Farm
{
    readonly GameData _data;
    readonly Dictionary<(int, int), Plot> _plots = new();

    public Farm(GameData data) { _data = data; }

    public IEnumerable<Plot> Plots => _plots.Values;
    public Plot? Get(int x, int y) => _plots.TryGetValue((x, y), out var p) ? p : null;

    public event Action<Plot>? Tilled, Planted, Watered, Matured, Swelled, Eaten, Harvested, Cleared;
    /// <summary>Kabarik urun safaktan sonra toplanmadan sondu (GDD 8.4).</summary>
    public event Action<Plot>? Deflated;

    /// <summary>GDD 8.1: Toprak basligi ancak egimi sinirdan az olan kareyi surer.</summary>
    public bool CanTill(float slopeDegrees) => slopeDegrees < _data.Crops.MaxSlopeDegrees;

    /// <summary>Egimi verilen kareyi surer; egim sinirda ya da ustundeyse surmez.</summary>
    public bool Till(int x, int y, float slopeDegrees) => CanTill(slopeDegrees) && Till(x, y);

    public bool Till(int x, int y)
    {
        if (_plots.ContainsKey((x, y))) return false;
        var p = new Plot { X = x, Y = y, Fertility = _data.Crops.StartFertility };
        _plots[(x, y)] = p;
        Tilled?.Invoke(p);
        return true;
    }

    public bool Plant(int x, int y, string cropId, Inventory inv)
    {
        var p = Get(x, y);
        if (p == null) return false;
        if (p.Crop != null && (p.Planted)) return false;
        if (p.Crop != null) Clear(x, y); // hasat edilmis kare yeniden ekilir
        if (!inv.Remove(cropId + "_seed", 1)) return false;
        var def = _data.Crop(cropId);
        p.Crop = cropId;
        p.Genes = new Genes { V = def.SeedGenes.V, H = def.SeedGenes.H, D = def.SeedGenes.D, M = def.SeedGenes.M };
        p.Growth = 0;
        Planted?.Invoke(p);
        return true;
    }

    public bool Water(int x, int y)
    {
        var p = Get(x, y);
        if (p == null) return false;
        float add = p.Crop != null ? _data.Crop(p.Crop).WaterPerShot : 100f;
        p.Moisture = Math.Min(100f, p.Moisture + add);
        Watered?.Invoke(p);
        return true;
    }

    /// <summary>GDD 8.2: Nem carpani.</summary>
    public float MoistureFactor(float moisture)
    {
        var m = _data.Crops.Moisture;
        if (moisture < m.StopBelow) return 0f;
        if (moisture < m.HalfBelow) return 0.5f;
        if (moisture > m.WetAbove) return m.WetFactor;
        return 1f;
    }

    public static float FertilityFactor(float fertility) => 0.5f + fertility / 200f;

    /// <summary>Buyume hizi carpani: hiz = taban x f(Nem) x f(Bereket) x f(Isik) x (1 + 0,04 x H).</summary>
    public float GrowthFactor(Plot p, bool light) =>
        MoistureFactor(p.Moisture) * FertilityFactor(p.Fertility) * (light ? 1f : 0f) * (1f + 0.04f * p.Genes.H);

    public void Tick(float dt, bool light)
    {
        foreach (var p in _plots.Values)
        {
            if (!p.Planted) continue;
            var def = _data.Crop(p.Crop!);
            // GDD 8.4: safaktan sonra 1 gun icinde toplanmayan kabarik urun soner, iki kat normal urune doner.
            if (p.Swollen != null && p.SinceDawn >= 0f)
            {
                p.SinceDawn += dt;
                if (p.SinceDawn >= _data.Crops.DeflateAfterSeconds)
                {
                    p.Swollen = null;
                    p.Deflated = true;
                    p.SinceDawn = -1f;
                    Deflated?.Invoke(p);
                }
            }
            if (p.Growth < 1f)
            {
                float before = p.Growth;
                p.Growth = Math.Min(1f, p.Growth + dt / def.GrowSeconds * GrowthFactor(p, light));
                if (before < 1f && p.Growth >= 1f) Matured?.Invoke(p);
                // Olgun ekin artik su icmez; buyurken nem azalir.
                p.Moisture = Math.Max(0f, p.Moisture - def.MoistureDrainPerSecond * dt);
                if (p.Moisture < _data.Crops.Moisture.StopBelow) p.NeverDry = false;
            }
        }
    }

    public int MatureCount => _plots.Values.Count(p => p.Planted && p.Stage == CropStage.Mature && p.Swollen == null);

    /// <summary>GDD 8.4: Boy puani = M + KS / 5 + kart bonusu.</summary>
    public SizeClassDef SizeFor(Plot p, int ks, float bonus)
    {
        float score = p.Genes.M + ks / 5f + bonus;
        return _data.Crops.SizeClasses.Where(c => score >= c.MinScore).OrderBy(c => c.MinScore).Last();
    }

    /// <summary>Kabarma: olgun ekinler kabarir.</summary>
    public List<Plot> SwellMature(int ks, float bonus)
    {
        var list = new List<Plot>();
        foreach (var p in _plots.Values)
        {
            if (!p.Planted || p.Stage != CropStage.Mature || p.Swollen != null) continue;
            p.Swollen = SizeFor(p, ks, bonus);
            list.Add(p);
            Swelled?.Invoke(p);
        }
        return list;
    }

    /// <summary>Safak: tarladaki kabarik urunlerin sonme saati baslar (GDD 8.4). Saati zaten isleyen ekin bastan baslamaz.</summary>
    public void OnDawn()
    {
        foreach (var p in _plots.Values)
            if (p.Planted && p.Swollen != null && p.SinceDawn < 0f) p.SinceDawn = 0f;
    }

    public bool Eat(int x, int y)
    {
        var p = Get(x, y);
        if (p == null || !p.Planted) return false;
        p.Eaten = true;
        Eaten?.Invoke(p);
        return true;
    }

    /// <summary>
    /// Bicme ile normal hasat (GDD 8.2): verim = taban x (1 + 0,08 x V) x kalite.
    /// Kalite: Bereket 70+ ve nem hic 20'nin altina dusmediyse 3 yildiz; yalnizca nem kosulu 2; aksi 1.
    /// </summary>
    public (int yield, int stars, int seeds)? Harvest(int x, int y, Rng rng)
    {
        var p = Get(x, y);
        if (p == null || !p.Planted || p.Stage != CropStage.Mature) return null;
        var def = _data.Crop(p.Crop!);
        int stars = Stars(p);
        float q = QualityMult(stars);
        // Kabarik urun bicilirse ya da toplanmadan sonduyse iki kat normal urun (GDD 8.4).
        float raw = def.BaseYield * (1f + 0.08f * p.Genes.V) * q * (p.Swollen != null || p.Deflated ? _data.Crops.DeflateYieldMult : 1f);
        int yield = (int)MathF.Floor(raw) + (rng.Chance(raw - MathF.Floor(raw)) ? 1 : 0);
        int seeds = def.SeedReturn;
        p.Harvested = true;
        p.Fertility = Math.Max(0f, p.Fertility - _data.Crops.HarvestFertilityLoss);
        Harvested?.Invoke(p);
        return (Math.Max(1, yield), stars, seeds);
    }

    /// <summary>
    /// Kalite yildizi (GDD 8.2): Bereket esigi ve ustu, nem hic durma esiginin altina dusmediyse 3.
    /// GDD yalnizca 3 yildizi tanimlar; 2 (yalnizca nem kosulu) ve 1 (aksi) bu kodun karari, raporda.
    /// </summary>
    public int Stars(Plot p) => p.NeverDry && p.Fertility >= _data.Crops.Star3MinFertility ? 3 : p.NeverDry ? 2 : 1;

    /// <summary>Kalite carpani (GDD 13: 1,0 / 1,3 / 1,7; GDD 8.2 hasat veriminde de "kalite").</summary>
    public float QualityMult(int stars) => _data.Crops.QualityMult[Math.Clamp(stars - 1, 0, _data.Crops.QualityMult.Count - 1)];

    /// <summary>Hasat edilmis ya da yenmis kareyi yeniden ekime acar (surulmus kalir).</summary>
    public void Clear(int x, int y)
    {
        var p = Get(x, y);
        if (p == null || p.Planted) return;
        p.Crop = null; p.Growth = 0; p.Swollen = null; p.Eaten = false; p.Harvested = false; p.NeverDry = true;
        p.Deflated = false; p.SinceDawn = -1f;
        Cleared?.Invoke(p);
    }

    /// <summary>Kabarik urun tarladan sokulur (yuk nesnesi olur). Kare bos kalir, bereket duser.</summary>
    public SizeClassDef? Uproot(int x, int y)
    {
        var p = Get(x, y);
        if (p == null || p.Swollen == null || p.Harvested || p.Eaten) return null;
        p.Harvested = true;
        p.Fertility = Math.Max(0f, p.Fertility - _data.Crops.HarvestFertilityLoss); // kok ekini -10 (GDD 8.2)
        return p.Swollen;
    }

    /// <summary>Satis degeri: normal fiyat x boy carpani (GDD 8.4).</summary>
    public int SellValue(string cropId, SizeClassDef size) => _data.Crop(cropId).BasePrice * size.PriceMult;
}
