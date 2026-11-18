namespace Kabarma.Sim;

/// <summary>Kalici Kabarma ilerlemesi (GDD 14.4 ilk gecis odulu): en yuksek gecilen KS, acik KS, kart jetonlari.</summary>
public sealed class KabarmaProgress
{
    public int HighestCleared { get; set; }
    public int CardTokens { get; set; }
    public int FullNights { get; set; }
    public int Toppled { get; set; }
    /// <summary>Son tam Kabarma gununun takvim gunu (GDD 2: her N. gece).</summary>
    public int LastDay { get; set; }
}

/// <summary>
/// GameState'in tam Kabarma girisi (kabarma3). Takvim/Can, Maya Feneri (gucler) ve Tatbikat (gucler) buradan baslatir:
/// <c>State.StartKabarma(State.KabarmaSpecFor(kind, ks, players, x, z))</c>. Safakta ilk gecis odulu yazilir.
/// </summary>
public sealed partial class GameState
{
    public KabarmaProgress Kabarma { get; private set; } = new();

    /// <summary>Acik en yuksek KS: gecilenin bir ustu, kabarma.json full.maxKs ile sinirli (M3: 10).</summary>
    public int UnlockedKs => Math.Min(Data.Kabarma.Full.MaxKs, Kabarma.HighestCleared + 1);

    /// <summary>Tapudaki olgun (kabarabilir) ekinler, aileye gore (GDD 14.3 "ne ekersen onu cekersin").</summary>
    public Dictionary<string, int> MatureFamilies(Func<Plot, bool>? inside = null)
    {
        var res = new Dictionary<string, int>();
        foreach (var p in Farm.Plots)
        {
            if (!p.Planted || p.Stage != CropStage.Mature || p.Crop == null) continue;
            if (inside != null && !inside(p)) continue; // Kucuk Kabarma: yaricap (Godot hucre -> dunya)
            var fam = Data.FindCrop(p.Crop)?.Family;
            if (string.IsNullOrEmpty(fam)) continue;
            res[fam] = res.GetValueOrDefault(fam) + 1;
        }
        return res;
    }

    /// <summary>Durumdan baslatma tanimi: biyom merkezden, aileler tapudan (kucukte <paramref name="inside"/> ile yaricaptan), tohum gece sayacindan.</summary>
    public KabarmaSpec KabarmaSpecFor(KabarmaKind kind, int ks, int players, float x, float z, Func<Plot, bool>? inside = null)
    {
        float r = kind == KabarmaKind.Small ? Data.Kabarma.Full.Small.Radius : 0f;
        return new KabarmaSpec
        {
            Kind = kind,
            Ks = Math.Clamp(ks, 1, Math.Max(1, Data.Kabarma.Full.MaxKs)),
            Players = Math.Max(1, players),
            Biome = BiomeMap.BiomeAt(x, z),
            PlantedFamilies = MatureFamilies(inside),
            CenterX = x, CenterZ = z, Radius = r,
            OcakTier = Research.OcakTier,
            Seed = Seed * 131 + (ulong)NightsStarted * 7 + (ulong)ks,
        };
    }

    /// <summary>Tam / kucuk / tatbikat Kabarma'sini baslatir (ilk gece senaryosu StartNight ile kalir).</summary>
    public KabarmaNight StartKabarma(KabarmaSpec spec, string? memberCard = null)
    {
        var plan = Waves.Compose(Data, spec);
        Night = new KabarmaNight(Data, plan, spec.Seed, memberCard);
        OnNightStarted(Night);
        var n = Night;
        n.Ended += end =>
        {
            if (spec.Kind != KabarmaKind.Full) return;
            Kabarma.FullNights++;
            if (end == KabarmaEnd.KazanToppled) { Kabarma.Toppled++; return; }
            if (spec.Ks > Kabarma.HighestCleared && spec.Ks <= Data.Kabarma.Full.MaxKs)
            {
                // GDD 14.4 ilk gecis: bir ust KS acilir, 1 kart jetonu ve kredi primi.
                Kabarma.HighestCleared = spec.Ks;
                Kabarma.CardTokens += Data.Kabarma.Full.FirstClearCardTokens;
                FirstClearBonus(Data.Kabarma.Full.FirstClearCredits * spec.Ks);
            }
        };
        return n;
    }

    void FirstClearBonus(int credits) { if (credits > 0) Credit(credits, "kabarma_ilk_gecis"); }

    [SaveSection("kabarma", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Kabarma), nameof(UnlockedKs) })]
    KabarmaProgress KabarmaSection
    {
        get => new() { HighestCleared = Kabarma.HighestCleared, CardTokens = Kabarma.CardTokens, FullNights = Kabarma.FullNights, Toppled = Kabarma.Toppled, LastDay = Kabarma.LastDay };
        set => Kabarma = value ?? new KabarmaProgress();
    }
}
