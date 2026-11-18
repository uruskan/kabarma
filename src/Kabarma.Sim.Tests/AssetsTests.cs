using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// Varlik kaydi (assets.json, URETIM §3-4, SANAT-SES §9): durumlar olcutlerle tutarli, yer tutucular isaretli,
/// varlik akisinin dosyalari diskte. Diger akislarin varliklari bu dalda olmayabilir: dosya denetimi yalniz stream "varlik".
/// </summary>
public class AssetsTests
{
    static readonly AssetsFile A = TestData.Load().AssetRegistry;

    [Fact]
    public void Every_asset_has_eight_criteria_and_a_known_status()
    {
        Assert.NotEmpty(A.Assets);
        Assert.Equal(A.Assets.Count, A.Assets.Select(a => a.Id).Distinct().Count());
        foreach (var a in A.Assets)
        {
            Assert.Contains(a.Status, AssetStatus.All);
            Assert.Equal(Enumerable.Range(1, 8), a.Criteria.Select(c => c.Id));
            Assert.False(string.IsNullOrWhiteSpace(a.Name), a.Id);
            Assert.False(string.IsNullOrWhiteSpace(a.Stream), a.Id);
            Assert.False(string.IsNullOrWhiteSpace(a.Source.Pool), a.Id);
            Assert.False(string.IsNullOrWhiteSpace(a.License), a.Id);
        }
    }

    [Fact]
    public void Status_follows_the_criteria_and_owner_approval()
    {
        foreach (var a in A.Assets)
        {
            if (a.Status == AssetStatus.Golden) Assert.True(a.PassesAll && a.OwnerApproved, $"{a.Id}: altin icin sekiz olcut ve sahibin onayi gerekir");
            if (a.Status == AssetStatus.AwaitingOwner) Assert.True(a.PassesAll && !a.OwnerApproved, $"{a.Id}: onay_bekliyor = sekiz olcut gecti, onay yok");
            if (!a.PassesAll) Assert.Equal(AssetStatus.Placeholder, a.Status);
            if (a.Status == AssetStatus.Placeholder) Assert.False(string.IsNullOrWhiteSpace(a.StatusNote), $"{a.Id}: yer tutucunun nedeni yazilmali");
        }
    }

    [Fact]
    public void Visual_scores_match_their_pass_flag()
    {
        // SANAT-SES §9: olcut 6-8 on uzerinden puan, gecme 8. Puan yoksa gecti sayilamaz.
        foreach (var a in A.Assets)
            foreach (var c in a.Criteria.Where(c => c.Id >= 6))
            {
                if (c.Score is float s) { Assert.InRange(s, 0f, 10f); Assert.Equal(s >= 8f, c.Pass); }
                else Assert.NotEqual(true, c.Pass);
            }
    }

    [Fact]
    public void Polygon_criterion_agrees_with_the_class_budget()
    {
        foreach (var a in A.Assets.Where(a => a.Tris > 0 && a.BudgetClass != ""))
        {
            Assert.True(A.Budgets.ContainsKey(a.BudgetClass), $"{a.Id}: butce sinifi yok: {a.BudgetClass}");
            var poly = a.Criteria.Single(c => c.Id == 2);
            if (poly.Pass == true) Assert.True(a.Tris <= A.Budgets[a.BudgetClass].MaxTris, $"{a.Id}: {a.Tris} ucgen butceyi asiyor");
        }
    }

    [Fact]
    public void Varlik_stream_files_exist()
    {
        var game = GameDir();
        foreach (var a in A.Assets.Where(a => a.Stream == "varlik"))
        {
            var files = new[] { a.Model, a.Hull, a.Icon }.Concat(a.Lods.Select(l => l.Path)).Where(p => p != "");
            Assert.NotEmpty(a.Lods);
            foreach (var f in files)
            {
                Assert.StartsWith("res://", f);
                Assert.True(File.Exists(Path.Combine(game, f["res://".Length..])), $"{a.Id}: {f} yok");
            }
        }
    }

    [Fact]
    public void Golden_machine_parts_keep_the_names_game_code_uses()
    {
        // TasFirin (door, door_handle, chimney), Pod (hatch), El Arabasi (ekonomi akisi icin tutamak, teker, yuk yuvasi).
        Assert.Equal(new[] { "base", "body", "chimney", "door", "door_handle", "panel" }, A.Assets.Single(a => a.Id == "tas_firin").Parts.OrderBy(p => p, StringComparer.Ordinal));
        // gorsel-model (18 Kasim): kapsul govdesi tek parca; kapak ve ayaklar ayri Blender modelleri (data/kapsul.json,
        // Pod.Model.cs). El Arabasi'nin yeni parcalamasinda saplar cercevenin icinde: tek 'handle' soketi.
        Assert.DoesNotContain("hatch", A.Assets.Single(a => a.Id == "kapsul").Parts);
        var cart = A.Assets.Single(a => a.Id == "el_arabasi");
        Assert.Contains("wheel", cart.Parts);
        Assert.Superset(new HashSet<string> { "wheel_pivot", "handle", "load_slot" }, cart.Sockets.Select(s => s.Name).ToHashSet());
    }

    static string GameDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "game", "project.godot"))) d = d.Parent;
        Assert.NotNull(d);
        return Path.Combine(d!.FullName, "game");
    }
}
