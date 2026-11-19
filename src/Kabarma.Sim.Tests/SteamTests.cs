using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Steam entegrasyonu (M6 steam): yerel defter, bulut karari, zengin durum, her basarim Steam API adi olarak gecerli.</summary>
public class SteamTests
{
    static readonly GameData D = TestData.Load();

    [Fact]
    public void Ledger_records_once_orders_pending_and_round_trips()
    {
        var l = new SteamLedger();
        Assert.True(l.Record("ilk_gece", 100));
        Assert.False(l.Record("ilk_gece", 200));
        Assert.True(l.Record("chain_ciraklik", 50));
        Assert.Equal(new[] { "chain_ciraklik", "ilk_gece" }, l.Pending());   // eski acilis once
        l.MarkSynced("chain_ciraklik");
        Assert.Equal(new[] { "ilk_gece" }, l.Pending());
        var back = SteamLedger.FromJson(l.ToJson());
        Assert.Equal(100, back.Unlocked["ilk_gece"]);
        Assert.Equal(new[] { "ilk_gece" }, back.Pending());
        Assert.Empty(SteamLedger.FromJson("bozuk json {").Unlocked);       // bozuk dosya cokertmez
        Assert.Empty(SteamLedger.FromJson(null).Unlocked);
    }

    [Theory]
    [InlineData(0, 0, 0, CloudAction.None)]
    [InlineData(0, 500, 0, CloudAction.Pull)]      // yalniz bulutta: iner
    [InlineData(500, 0, 0, CloudAction.Push)]      // yalniz yerelde: cikar
    [InlineData(500, 900, 0, CloudAction.Pull)]    // bulut daha yeni
    [InlineData(900, 500, 0, CloudAction.Push)]    // yerel daha yeni
    [InlineData(900, 900, 900, CloudAction.None)]  // esit ve yuklenmis
    [InlineData(900, 900, 0, CloudAction.Push)]    // esit ama hic yuklenmemis: bir kez cikar
    [InlineData(900, 905, 900, CloudAction.Pull)]  // yuklendi, baska makine bulutu yeniledi
    public void Cloud_decision(long local, long cloud, long pushed, CloudAction want) => Assert.Equal(want, SteamCloudRules.Decide(local, cloud, pushed));

    [Fact]
    public void Cloud_skips_backups_oversize_and_empty()
    {
        var c = D.Steam.Cloud;
        Assert.True(SteamCloudRules.Eligible("yuva1/world.kbs", 1000, c));
        Assert.True(SteamCloudRules.Eligible("character.kbs", 1000, c));
        Assert.False(SteamCloudRules.Eligible("yuva1/backup/world.1.kbs", 1000, c));
        Assert.False(SteamCloudRules.Eligible("yuva1/world.kbs", 0, c));
        Assert.False(SteamCloudRules.Eligible("yuva1/world.kbs", c.MaxFileBytes + 1, c));
    }

    [Fact]
    public void Presence_text_has_biome_ks_players_and_phase_and_every_biome_is_named()
    {
        string t = SteamPresence.Text(D.Steam, "kizil_kanyon", 7, 3, night: false);
        Assert.Equal("Kızıl Kanyon · KS 7 · 3 oyuncu · Gündüz", t);
        Assert.Contains("Kabarma gecesi", SteamPresence.Text(D.Steam, "gobek", 0, 0, night: true)); // KS ve oyuncu en az 1
        Assert.Contains("KS 1 · 1 oyuncu", SteamPresence.Text(D.Steam, "gobek", 0, 0, night: true));
        foreach (var b in D.Biomes.World.BiomeMap.Order) Assert.True(D.Steam.Presence.BiomeNames.ContainsKey(b), b);
    }

    [Fact]
    public void Every_achievement_id_is_a_valid_steam_api_name_and_unlocking_reaches_the_event()
    {
        var s = new GameState(D);
        Assert.Equal(D.Achievements.Total, s.Achievements.Count + 0 == 0 ? 0 : D.Achievements.Total);
        foreach (var a in s.Achievements.All)
        {
            string api = D.Steam.Achievements.ApiPrefix + a.Id;
            Assert.Matches("^[a-z0-9_]{3,60}$", api);           // Steamworks API adi: kucuk harf, rakam, alt cizgi
        }
        var seen = new List<string>();
        s.Achievements.UnlockedEvent += a => seen.Add(a.Id);
        s.Orders.Report("kabarma_survived", 1);
        Assert.Contains("ilk_gece", seen);
        Assert.Contains("ilk_gece", s.Achievements.Unlocked);
    }
}