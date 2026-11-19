namespace Kabarma.Sim;

public sealed class TuzukSave
{
    public List<string> NewMoon { get; set; } = new();
}

/// <summary>Tuzuk ve Yeni Ay durumu (GDD 14.8): kayitli tek sey Yeni Ay'da secilen maddeler; KS sirasi dunya tohumundan turetilir.</summary>
public sealed partial class GameState
{
    /// <summary>Yeni Ay'da secilen madde kimlikleri (KS 1'den tek kat acik).</summary>
    public List<string> NewMoonClauses { get; private set; } = new();

    /// <summary>Yeni Ay secimi: yalniz dunya kurulurken (ilk Kabarma baslamadan). Bilinmeyen kimlik yok sayilir. Donen: kabul edilen sayi.</summary>
    public int SetNewMoon(IEnumerable<string> ids)
    {
        if (NightsStarted > 0) return 0;
        NewMoonClauses = ids.Where(id => Data.Tuzuk.Clauses.Any(c => c.Id == id)).Distinct().ToList();
        return NewMoonClauses.Count;
    }

    /// <summary>O Kabarma'nin tuzuku (Waves.Compose KabarmaSpec.Tuzuk'u kullanir).</summary>
    public TuzukSet TuzukFor(int ks, KabarmaKind kind) => Tuzuk.For(Data, Seed, ks, NewMoonClauses, kind);

    [SaveSection("tuzuk", 1, Order = SaveOrder.State, Covers = new[] { nameof(NewMoonClauses) })]
    TuzukSave TuzukSection
    {
        get => new() { NewMoon = NewMoonClauses.ToList() };
        set { NewMoonClauses = (value?.NewMoon ?? new()).Where(id => Data.Tuzuk.Clauses.Any(c => c.Id == id)).ToList(); }
    }
}