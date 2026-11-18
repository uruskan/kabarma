namespace Kabarma.Sim;

/// <summary>Maruziyet sorgusunun girdisi: oyuncunun donanimi ve durumu. Duz ve durumsuz.</summary>
public sealed class ClimateContext
{
    public bool Night { get; init; }
    /// <summary>Giyili kusam modulleri (filtre_maskesi, serin_yelek, termal_astar, maya_yalitimi).</summary>
    public IReadOnlySet<string> Gear { get; init; } = new HashSet<string>();
    /// <summary>Etkisi suren yemekler.</summary>
    public IReadOnlySet<string> Foods { get; init; } = new HashSet<string>();
    /// <summary>Oyuncunun icinde bulundugu ozel bolgeler ("gas_pit").</summary>
    public IReadOnlySet<string> Zones { get; init; } = new HashSet<string>();
    /// <summary>"shelter", "shade", "wet".</summary>
    public IReadOnlySet<string> Conditions { get; init; } = new HashSet<string>();
}

public readonly record struct HazardExposure(string Hazard, bool Resisted, float DrainPerSecond, bool BlocksStaminaRegen);

/// <summary>
/// Biyom sozlesmesi (M4 biyom): kimlikler, kademe ve "bu noktada hangi iklim tehlikesi, direnc yetiyor mu" sorgusu.
/// "Bu noktada hangi biyom" sorgusu <see cref="BiomeMap.BiomeAt"/>'tir; ikisi birlikte:
/// <c>climate.Exposure(map.BiomeAt(x, z), ctx)</c>. Yaratik, Ata ve ekin akislari kimlik ve kademeyi buradan okur.
/// </summary>
public sealed class BiomeClimate
{
    readonly ClimateFile _f;
    public BiomeClimate(GameData data) { _f = data.Climate; }

    public IReadOnlyList<string> BiomeIds => _f.BiomeIds;

    /// <summary>Kademe (T1-T6); bilinmeyen biyom 0.</summary>
    public int Tier(string biome) => _f.Biomes.TryGetValue(biome, out var b) ? b.Tier : 0;

    public IReadOnlyList<string> EntryGear(string biome) =>
        _f.Biomes.TryGetValue(biome, out var b) ? b.EntryGear : Array.Empty<string>();

    /// <summary>Biyomun bu baglamda gecerli tehlikeleri ve direnc durumu (direnc yetiyorsa Resisted, DrainPerSecond 0).</summary>
    public List<HazardExposure> Exposure(string biome, ClimateContext ctx)
    {
        var list = new List<HazardExposure>();
        if (!_f.Biomes.TryGetValue(biome, out var b)) return list;
        foreach (var bh in b.Hazards)
        {
            if (bh.When == "day" && ctx.Night) continue;
            if (bh.When == "night" && !ctx.Night) continue;
            if (bh.Zone != "" && !ctx.Zones.Contains(bh.Zone)) continue;
            if (!_f.Hazards.TryGetValue(bh.Hazard, out var h)) continue;
            bool resisted = h.Gear.Any(ctx.Gear.Contains) || h.Foods.Any(ctx.Foods.Contains) || h.Conditions.Any(ctx.Conditions.Contains);
            list.Add(new HazardExposure(bh.Hazard, resisted, resisted ? 0f : h.DrainPerSecond, !resisted && h.BlocksStaminaRegen));
        }
        return list;
    }

    /// <summary>Toplam can kaybi (sn basina) ve dayaniklilik yenilenmesi engelleniyor mu.</summary>
    public (float drain, bool blocksRegen) Total(string biome, ClimateContext ctx)
    {
        float drain = 0; bool block = false;
        foreach (var e in Exposure(biome, ctx)) { drain += e.DrainPerSecond; block |= e.BlocksStaminaRegen; }
        return (drain, block);
    }
}
