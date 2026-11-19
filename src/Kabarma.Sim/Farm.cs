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

    // M2 (tarim, GDD 8.2-8.3)
    /// <summary>Tapu disindaki yabani bitki: surulmez, kabarmaz, bicilince yabani tohum verir.</summary>
    public bool Wild { get; internal set; }
    /// <summary>Alaca kimligi (benekli/altin/aurora) ya da null; ekimde atilir.</summary>
    public string? Alaca { get; internal set; }
    /// <summary>Gunduz zararlisinin Zarar'i 0..1; 1'de ekin yenir, hasat x (1 - Zarar).</summary>
    public float PestDamage { get; internal set; }
    /// <summary>Bu karede son hasat edilen ekinin ailesi (art arda farkli aile +%10).</summary>
    public string? PrevFamily { get; internal set; }
    public bool RotationBonus { get; internal set; }
    /// <summary>Korkuluk ya da Sarimsak korumasinda (Tick hesaplar).</summary>
    public bool Protected { get; internal set; }
    /// <summary>Kalici cali hasattan sonra yeniden olgunlasiyor.</summary>
    public bool Regrowing { get; internal set; }
    /// <summary>Yabani bitkinin hasattan beri bekledigi sure (sn).</summary>
    public float RegrowT { get; internal set; }

    /// <summary>Sera icinde: biyom bandindan bagimsiz (Farm.SetGreenhouse).</summary>
    public bool Greenhouse { get; internal set; }
    /// <summary>Sig su karesi (Toprak basligiyla acilan havuz); su basmis ekin ister.</summary>
    public bool Flooded { get; internal set; }

    public bool Planted => Crop != null && !Eaten && !Harvested;
    public CropStage Stage => Growth >= 1f ? CropStage.Mature : Growth >= 0.6f ? CropStage.Young : Growth >= 0.2f ? CropStage.Sprout : CropStage.Seed;
}

/// <summary>Tapu icindeki tarla: surme, ekme, sulama, buyume, kabarma (GDD 8). Genler ve zararli: Farm.Genetics.cs.</summary>
public sealed partial class Farm
{
    readonly GameData _data;
    readonly Dictionary<(int, int), Plot> _plots = new();

    public Farm(GameData data) { _data = data; }

    /// <summary>Tapudaki tarla kareleri (surulmus). Yabani bitkiler haric: senaryo, pusula, gece sayaclari bunu sayar.</summary>
    public IEnumerable<Plot> Plots => _plots.Values.Where(p => !p.Wild);
    /// <summary>Yabani bitkiler dahil butun kareler (Bicme, gorunus).</summary>
    public IEnumerable<Plot> AllPlots => _plots.Values;
    public IEnumerable<Plot> WildPlots => _plots.Values.Where(p => p.Wild);
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

    public bool Plant(int x, int y, string cropId, Inventory inv) => Plant(x, y, cropId, inv, null);

    /// <summary>
    /// Eker: cantadan bir tohum duser; genler Tohum Kaydi'nin en iyi satirindan, kayit yoksa taban genleri (GDD 8.3).
    /// rng verilirse Alaca zari ekimde atilir.
    /// </summary>
    public bool Plant(int x, int y, string cropId, Inventory inv, Rng? rng)
    {
        var p = Get(x, y);
        if (p == null || p.Wild) return false;
        if (p.Crop != null && (p.Planted)) return false;
        var def = _data.FindCrop(cropId);
        if (def == null) return false;
        if (!inv.Remove(def.SeedId, 1)) return false;
        if (p.Crop != null) Clear(x, y); // hasat edilmis kare yeniden ekilir
        var lot = Seeds.Take(cropId, _data.Crops.Genetics);
        p.Crop = cropId;
        p.Genes = lot?.Genes.Clone() ?? new Genes { V = def.SeedGenes.V, H = def.SeedGenes.H, D = def.SeedGenes.D, M = def.SeedGenes.M };
        p.Alaca = rng != null ? Genetics.RollAlaca(_data.Crops.Genetics, lot?.Alaca, rng) : null;
        p.RotationBonus = p.PrevFamily != null && p.PrevFamily != def.Family;
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

    /// <summary>Buyume hizi carpani: hiz = taban x f(Nem) x f(Bereket) x f(Isik) x f(Sicaklik) x (1 + 0,04 x H).</summary>
    public float GrowthFactor(Plot p, bool light)
    {
        var def = p.Crop != null ? _data.FindCrop(p.Crop) : null;
        bool dark = def?.Dark == true;
        float lightF = dark ? (light ? 0f : 1f) : (light ? 1f : 0f); // GDD 8.2: mantarlar tersidir
        return MoistureFactor(p.Moisture, p.Genes.D, def?.WetLoving == true) * FertilityFactor(p.Fertility) * lightF
            * BandFactor(def, p.Genes.D, p.Greenhouse) * FloodFactor(def, p) * (1f + 0.04f * p.Genes.H);
    }

    public void Tick(float dt, bool light)
    {
        Time += dt;
        var gr = _data.Crops.Genetics;
        float day = Math.Max(0.001f, _data.Crops.GrowDaySeconds);
        UpdateProtection(dt);
        foreach (var p in _plots.Values)
        {
            if (p.Wild) { TickWild(p, dt, day); continue; }
            // GDD 8.2: bos kare gunde +3 Bereket (hasat edilmis ya da yenmis kare de bostur)
            if (!p.Planted) { p.Fertility = Math.Min(100f, p.Fertility + gr.FallowPerDay * dt / day); continue; }
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
                // Kalici cali hasattan sonra regrowFrom'dan olgunluga regrowDays buyume gununde doner.
                float span = p.Regrowing && def.RegrowDays > 0 ? def.RegrowDays * day / Math.Max(0.01f, 1f - gr.RegrowFrom) : def.GrowSeconds;
                p.Growth = Math.Min(1f, p.Growth + dt / span * GrowthFactor(p, light));
                if (before < 1f && p.Growth >= 1f) Matured?.Invoke(p);
                // Olgun ekin artik su icmez; buyurken nem azalir.
                p.Moisture = p.Flooded ? 100f : Math.Max(0f, p.Moisture - def.MoistureDrainPerSecond * dt);
                if (p.Moisture < _data.Crops.Moisture.StopBelow) p.NeverDry = false;
            }
        }
    }

    public int MatureCount => _plots.Values.Count(p => !p.Wild && p.Planted && p.Stage == CropStage.Mature && p.Swollen == null);

    /// <summary>GDD 8.4: Boy puani = M + KS / 5 + kart bonusu.</summary>
    public SizeClassDef SizeFor(Plot p, int ks, float bonus)
    {
        float score = p.Genes.M + ks / 5f + bonus;
        return _data.Crops.SizeClasses.Where(c => score >= c.MinScore).OrderBy(c => c.MinScore).Last();
    }

    /// <summary>Cekisme (GDD 22.2 kural 7): false donen kare kabarmaz (cevrimdisi takimin tapusu). Null: hepsi kabarir.</summary>
    public Func<Plot, bool>? SwellFilter { get; set; }

    /// <summary>Kabarma: olgun ekinler kabarir.</summary>
    public List<Plot> SwellMature(int ks, float bonus)
    {
        var list = new List<Plot>();
        foreach (var p in _plots.Values)
        {
            if (p.Wild || !p.Planted || p.Stage != CropStage.Mature || p.Swollen != null) continue;
            if (SwellFilter != null && !SwellFilter(p)) continue;
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
        var h = HarvestFull(x, y, rng);
        return h == null ? null : (h.Yield, h.Stars, h.Seeds);
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
        p.Alaca = null; p.PestDamage = 0f; p.RotationBonus = false; p.Regrowing = false; // PrevFamily kalir (art arda aile)
        Cleared?.Invoke(p);
    }

    /// <summary>Kabarik urun tarladan sokulur (yuk nesnesi olur). Kare bos kalir, bereket duser.</summary>
    public SizeClassDef? Uproot(int x, int y)
    {
        var p = Get(x, y);
        if (p == null || p.Swollen == null || p.Harvested || p.Eaten) return null;
        p.Harvested = true;
        p.Fertility = Math.Max(0f, p.Fertility - FertilityLoss(p)); // kok ekini -10, digerleri -8 (GDD 8.2)
        p.PrevFamily = p.Crop != null ? _data.FindCrop(p.Crop)?.Family : null;
        return p.Swollen;
    }

    /// <summary>Satis degeri: normal fiyat x boy carpani (GDD 8.4).</summary>
    public int SellValue(string cropId, SizeClassDef size) => _data.Crop(cropId).BasePrice * size.PriceMult;
}
