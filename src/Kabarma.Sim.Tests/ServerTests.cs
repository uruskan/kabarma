using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Adanmis sunucu (M5 sunucu akisi): yonetim komutlari, yasak, sifre, katilma denetimi, veri.</summary>
public class ServerTests
{
    sealed class Fake : IServerHost
    {
        public List<ServerPlayer> List = new() { new(1, "Ayse", "10.0.0.1", false), new(2, "Mehmet", "10.0.0.2", false), new(3, "Meryem", "10.0.0.3", true) };
        public List<(int, string)> Kicked = new();
        public List<string> Said = new();
        public ServerRuntime Runtime { get; } = new();
        public string Password { get; set; } = "";
        public long NowUnix { get; set; } = 1_800_000_000;
        public int Saves, Persists; public bool Down, SaveOk = true;
        public IReadOnlyList<ServerPlayer> Players() => List;
        public bool Kick(int peer, string reason) { Kicked.Add((peer, reason)); return List.RemoveAll(p => p.Peer == peer) > 0; }
        public bool Save(out string why) { why = SaveOk ? "" : "gece"; if (SaveOk) Saves++; return SaveOk; }
        public void Announce(string t) => Said.Add(t);
        public string StatusLine() => $"oyuncu {List.Count}";
        public void Shutdown() => Down = true;
        public void Persist() => Persists++;
    }

    [Fact]
    public void Data_loads_and_defaults_are_sane()
    {
        var s = TestData.Load().Server;
        Assert.True(s.Port > 1024 && s.MaxPlayers >= 2 && s.AutosaveMinutes > 0);
        Assert.False(string.IsNullOrEmpty(s.SaveSlot));
        Assert.Equal(s.Port, TestData.Load().Net.Port);
    }

    [Fact]
    public void Players_list_kick_by_number_name_and_unique_prefix()
    {
        var h = new Fake();
        Assert.Contains("3 oyuncu", ServerAdmin.Execute(h, "oyuncular"));
        Assert.Contains("(yönetici)", ServerAdmin.Execute(h, "oyuncular"));
        Assert.StartsWith("atıldı: Ayse", ServerAdmin.Execute(h, "at 1 kavga"));
        Assert.Equal((1, "kavga"), h.Kicked[0]);
        Assert.StartsWith("HATA", ServerAdmin.Execute(h, "at Me"));          // Mehmet ve Meryem: belirsiz onek
        Assert.StartsWith("atıldı: Mehmet", ServerAdmin.Execute(h, "at meh"));
        Assert.StartsWith("HATA: oyuncu yok", ServerAdmin.Execute(h, "at kimse"));
        Assert.StartsWith("HATA: at", ServerAdmin.Execute(h, "at"));
    }

    [Fact]
    public void Ban_kicks_blocks_rejoin_by_name_and_address_expires_and_can_be_lifted()
    {
        var h = new Fake();
        Assert.StartsWith("yasaklandı: Ayse (kalıcı)", ServerAdmin.Execute(h, "yasakla Ayse 0 hile"));
        Assert.Single(h.Kicked); Assert.Equal(1, h.Persists);
        Assert.Contains("yasaklarsın".Replace("yasaklarsın", "yasaklı"), ServerAdmin.CheckJoin(h, "ayse", "", "1.1.1.1")!);   // ad, buyuk-kucuk farksiz
        Assert.Contains("yasaklı", ServerAdmin.CheckJoin(h, "Baska", "", "10.0.0.1")!);                                      // adres
        Assert.Null(ServerAdmin.CheckJoin(h, "Baska", "", "9.9.9.9"));
        // sureli yasak
        Assert.Contains("(5 dk)", ServerAdmin.Execute(h, "yasakla Mehmet 5 sagir"));
        Assert.Contains("dk sonra gel", ServerAdmin.CheckJoin(h, "mehmet", "", "")!);
        h.NowUnix += 6 * 60;
        Assert.Null(ServerAdmin.CheckJoin(h, "mehmet", "", ""));                // sure doldu
        Assert.DoesNotContain("mehmet", ServerAdmin.Execute(h, "yasaklar"));
        Assert.StartsWith("yasak kaldırıldı", ServerAdmin.Execute(h, "yasak_kaldir ayse"));
        Assert.Null(ServerAdmin.CheckJoin(h, "ayse", "", "10.0.0.1"));
        Assert.StartsWith("HATA", ServerAdmin.Execute(h, "yasak_kaldir yok"));
        // cevrimdisi oyuncuya da yasak (ad)
        Assert.StartsWith("yasaklandı: Hilekar", ServerAdmin.Execute(h, "yasakla Hilekar"));
        Assert.Contains("yasaklı", ServerAdmin.CheckJoin(h, "HILEKAR", "", "")!);
        // kalicilik: JSON gidis-donus
        var back = JsonSerializer.Deserialize<ServerRuntime>(JsonSerializer.Serialize(h.Runtime))!;
        Assert.Contains(back.Bans, b => b.Name == "hilekar");
    }

    [Fact]
    public void Password_is_checked_on_join_set_cleared_and_persisted_and_names_are_unique()
    {
        var h = new Fake();
        Assert.Equal("sunucu şifresiz", ServerAdmin.Execute(h, "sifre"));
        Assert.Equal("şifre değişti", ServerAdmin.Execute(h, "sifre gizli"));
        Assert.Equal("gizli", h.Runtime.Password);
        Assert.Equal("sunucu şifreli", ServerAdmin.Execute(h, "sifre"));
        Assert.Equal("sunucu şifresi yanlış", ServerAdmin.CheckJoin(h, "Yeni", "yanlis", ""));
        Assert.Null(ServerAdmin.CheckJoin(h, "Yeni", "gizli", ""));
        Assert.Equal("bu ad kullanımda", ServerAdmin.CheckJoin(h, "ayse", "gizli", ""));
        Assert.Equal("şifre kaldırıldı", ServerAdmin.Execute(h, "sifre -"));
        Assert.Null(ServerAdmin.CheckJoin(h, "Yeni", "", ""));
        Assert.Equal("", h.Runtime.Password);                                     // bos dize: sifre kaldirildi (server.json'a donmez)
    }

    [Fact]
    public void Save_announce_status_shutdown_help_and_unknown()
    {
        var h = new Fake();
        Assert.Equal("kaydedildi", ServerAdmin.Execute(h, "kaydet"));
        Assert.Equal(1, h.Saves);
        h.SaveOk = false;
        Assert.Equal("HATA: kaydedilemedi: gece", ServerAdmin.Execute(h, "kaydet"));
        Assert.Equal("duyuruldu", ServerAdmin.Execute(h, "duyuru Sunucu 5 dk sonra kapanıyor"));
        Assert.Equal("Sunucu 5 dk sonra kapanıyor", h.Said[0]);
        Assert.StartsWith("HATA", ServerAdmin.Execute(h, "duyuru"));
        Assert.Equal("oyuncu 3", ServerAdmin.Execute(h, "durum"));
        Assert.Equal(ServerAdmin.Help, ServerAdmin.Execute(h, "YARDIM"));
        Assert.StartsWith("HATA: bilinmeyen", ServerAdmin.Execute(h, "uc"));
        Assert.Equal("", ServerAdmin.Execute(h, "   "));
        Assert.False(h.Down);
        ServerAdmin.Execute(h, "kapat");
        Assert.True(h.Down);
        foreach (var c in ServerAdmin.Commands) Assert.Contains(c, ServerAdmin.Help.Replace("yasak_kaldir", "yasak_kaldir"));
    }
}
