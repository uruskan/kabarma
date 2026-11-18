using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Kayit dosyalari (TEKNIK 9): bicim, CRC, gecici dosya + yeniden adlandirma, 5 yedek, boyut siniri, yuvalar.</summary>
public class SaveStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "kabarma_save_test_" + Guid.NewGuid().ToString("N"));
    readonly SaveFile _cfg = TestData.Load().Save;

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    static SaveGame Game(int n, string reason = "elle")
    {
        var g = new SaveGame { Manifest = new SaveManifest { Seed = 4417, Day = n, Reason = reason, SavedUtc = $"2026-11-18T10:{n:00}:00Z" } };
        g.Sections["sayi"] = new SaveBlob { Version = 1, Data = System.Text.Encoding.UTF8.GetBytes(n.ToString()) };
        return g;
    }

    [Fact]
    public void Data_matches_teknik()
    {
        Assert.Equal(5, _cfg.Backups);
        Assert.Equal(30L * 1024 * 1024, _cfg.MaxWorldBytes);
        Assert.Equal(5f, _cfg.AutosaveMinutes);
        Assert.StartsWith("user://test", _cfg.TestRoot);
        Assert.NotEqual(_cfg.Root, _cfg.TestRoot);
    }

    [Fact]
    public void Codec_round_trips_and_detects_corruption()
    {
        var g = Game(3);
        var bytes = SaveCodec.Encode(g);
        var back = SaveCodec.Decode(bytes);
        Assert.Equal(g.Sections["sayi"].Data, back.Sections["sayi"].Data);
        Assert.Equal(3, back.Manifest.Day);
        using (var ms = new MemoryStream(bytes)) Assert.Equal(3, SaveCodec.ReadManifest(ms).Day);

        var bad = (byte[])bytes.Clone();
        bad[^1] ^= 0xFF;
        Assert.ThrowsAny<Exception>(() => SaveCodec.Decode(bad));
        Assert.Throws<SaveException>(() => SaveCodec.Decode("NOPE...."u8.ToArray()));
    }

    [Fact]
    public void Five_rotating_backups()
    {
        var st = new SaveStore(_dir, _cfg);
        for (int i = 1; i <= 8; i++) st.Write("yuva1", Game(i));
        Assert.Equal(8, st.Read("yuva1").Manifest.Day);
        for (int k = 1; k <= 5; k++)
            Assert.Equal(8 - k, SaveCodec.Decode(File.ReadAllBytes(st.BackupPath("yuva1", k))).Manifest.Day);
        Assert.False(File.Exists(st.BackupPath("yuva1", 6)));
        Assert.False(File.Exists(st.WorldPath("yuva1") + ".tmp"));
        Assert.True(File.Exists(Path.Combine(st.SlotDir("yuva1"), _cfg.ManifestFile)));
    }

    [Fact]
    public void Corrupt_main_falls_back_to_newest_backup()
    {
        var st = new SaveStore(_dir, _cfg);
        st.Write("yuva2", Game(1));
        st.Write("yuva2", Game(2));
        File.WriteAllBytes(st.WorldPath("yuva2"), new byte[] { 1, 2, 3 });
        var g = st.Read("yuva2", out var from);
        Assert.Equal(1, g.Manifest.Day);
        Assert.EndsWith(".1", from);
    }

    [Fact]
    public void Too_large_save_is_refused_and_old_save_kept()
    {
        var small = new SaveFile { MaxWorldBytes = 4096, Backups = 5 };
        var st = new SaveStore(_dir, small);
        st.Write("yuva1", Game(1));
        var big = Game(2);
        var rnd = new Random(1);
        var blob = new byte[64 * 1024];
        rnd.NextBytes(blob); // sikismaz
        big.Sections["dev"] = new SaveBlob { Version = 1, Data = blob };
        Assert.Throws<SaveException>(() => st.Write("yuva1", big));
        Assert.Equal(1, st.Read("yuva1").Manifest.Day);
    }

    [Fact]
    public void Slots_list_and_latest()
    {
        var st = new SaveStore(_dir, _cfg);
        Assert.Null(st.Latest());
        st.Write("yuva1", Game(5));
        st.Write("yuva3", Game(9));
        var list = st.List();
        Assert.Equal(_cfg.Slots, list.Count);
        Assert.True(list[0].Exists);
        Assert.False(list[1].Exists);
        Assert.Equal(9, list[2].Manifest!.Day);
        Assert.Equal("yuva3", st.Latest());
        Assert.Throws<ArgumentException>(() => st.SlotDir("../disari"));
    }

    [Fact]
    public void Character_file_is_separate()
    {
        var st = new SaveStore(_dir, _cfg);
        Assert.Null(st.ReadCharacter());
        st.WriteCharacter(Game(4));
        Assert.Equal(4, st.ReadCharacter()!.Manifest.Day);
        Assert.False(Directory.Exists(Path.Combine(_dir, "yuva1")));
    }

    [Fact]
    public void Real_state_save_fits_far_under_limit()
    {
        var s = new GameState(TestData.Load());
        var g = new SaveRegistry().AddRange(s.SaveSections()).Capture(SaveScope.World, s.Manifest());
        var st = new SaveStore(_dir, _cfg);
        long n = st.Write("yuva1", g);
        Assert.True(n < _cfg.MaxWorldBytes / 100, $"{n} bayt");
    }
}
