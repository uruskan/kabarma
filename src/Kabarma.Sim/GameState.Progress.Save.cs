namespace Kabarma.Sim;

// Ilerleme akisinin kayit bolumleri (TEKNIK 9.1): Ustalik ve basarimlar karakter dosyasinda (dunyalar arasi tasinir),
// Ustalik sans akisi dunya kaydinda. Is emirleri bolumu SaveSystems.cs'te ("orders"; ilerleme alanlari eklendi).

public sealed class AchievementsSave
{
    public List<string> Unlocked { get; set; } = new();
}

public sealed class ProgressSave
{
    /// <summary>Ustalik kesir yuvarlama akisinin durumu (ayni komutlar ayni sonuc).</summary>
    public ulong MasteryRng { get; set; }
}

public sealed partial class GameState
{
    [SaveSection("mastery", 1, Order = SaveOrder.Progress, Scope = SaveScope.Character, Covers = new[] { nameof(Mastery) })]
    MasteryState MasterySection { get => Mastery.Export(); set => Mastery.Import(value); }

    [SaveSection("achievements", 1, Order = SaveOrder.Progress, Scope = SaveScope.Character, Covers = new[] { nameof(Achievements) })]
    AchievementsSave AchievementsSection
    {
        get => new() { Unlocked = Achievements.Export() };
        set => Achievements.Import(value.Unlocked ?? new());
    }

    [SaveSection("progress", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(MasteryRng) })]
    ProgressSave ProgressSection
    {
        get => new() { MasteryRng = MasteryRng.State };
        set => MasteryRng.State = value.MasteryRng;
    }
}
