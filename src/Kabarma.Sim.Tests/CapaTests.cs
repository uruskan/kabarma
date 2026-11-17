using System.Text.Json.Nodes;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Capa (URETIM 6 M1 Capa satiri, GDD 4): silah acikligi, his verisi ve oyuncu ayarinin bindirilmesi.</summary>
public class CapaTests
{
    [Fact]
    public void Weapons_follow_m1_scope()
    {
        var d = TestData.Load();
        Assert.True(d.Tool.Nailgun.SemiAuto);       // ICERIK 6.2 #2: yari otomatik
        Assert.False(d.Tool.Weapon.SemiAuto);
        Assert.Equal("civi", d.Tool.Nailgun.Ammo);  // gunduz Civi harcar
        Assert.Equal(80f, d.Tool.Carry.MaxKg);      // Cekim: 80 kg siniri
        Assert.True(d.Tool.MowRadius > 0);          // Bicme alan etkili
    }

    [Fact]
    public void Tirpan_is_open_from_start_and_nailgun_flag_comes_from_data()
    {
        var d = TestData.Load();
        var s = new GameState(d);
        Assert.True(s.Capa.IsUnlocked("tirpan_isini"));
        Assert.True(s.Capa.IsUnlocked("civi_tabancasi")); // main'de acik (ekonomi akisi tarifle acar)

        d.Tool.Nailgun.UnlockedAtStart = false;
        var s2 = new GameState(d);
        Assert.False(s2.Capa.IsUnlocked("civi_tabancasi"));
        Assert.True(s2.Capa.Unlock("civi_tabancasi"));
        Assert.False(s2.Capa.Unlock("civi_tabancasi"));
        Assert.True(s2.Capa.IsUnlocked("civi_tabancasi"));
    }

    [Fact]
    public void Weapon_hit_stop_defaults_are_inside_gdd_range()
    {
        var f = TestData.Load().Feel;
        Assert.Equal(50f, f.Criteria.MaxLatencyMs);   // GDD 4
        Assert.Equal(30f, f.Criteria.HitStopMinMs);
        Assert.Equal(80f, f.Criteria.HitStopMaxMs);
        foreach (var id in new[] { "tirpan", "civi" })
        {
            var src = f.Source(id);
            Assert.InRange(src.HitStopMs * f.Master.HitStop, f.Criteria.HitStopMinMs, f.Criteria.HitStopMaxMs);
            Assert.InRange(src.CritHitStopMs * f.Master.HitStop, f.Criteria.HitStopMinMs, f.Criteria.HitStopMaxMs);
        }
        // Her duraklama ya kapali (0) ya aralikta; ekranda da aralikta kalsin diye: duraklama ancak kare basinda kalkar,
        // istenen + bir kare (hedef FPS'te) ust siniri asmamali.
        float frameMs = 1000f / f.Criteria.TargetFps;
        foreach (var (id, src) in f.Sources)
            foreach (var ms in new[] { src.HitStopMs, src.CritHitStopMs })
                Assert.True(ms == 0 || (ms >= f.Criteria.HitStopMinMs && ms + frameMs <= f.Criteria.HitStopMaxMs), $"{id}: {ms} ms (+{frameMs:0.0} ms kare)");
    }

    [Fact]
    public void Every_hit_layer_has_a_source_for_both_weapons()
    {
        var f = TestData.Load().Feel;
        foreach (var id in new[] { "tirpan", "civi" })
        {
            var s = f.Source(id);
            Assert.True(s.FireShake > 0 && s.FireRecoil > 0, id);                           // sarsinti, geri tepme
            Assert.True(s.HitStopMs > 0, id);                                                // duraklama
            Assert.True(s.Debris > 0, id);                                                   // parca
            Assert.False(string.IsNullOrEmpty(s.FireSfx) || string.IsNullOrEmpty(s.HitSfx) || string.IsNullOrEmpty(s.DebrisSfx), id); // uc katmanli ses
        }
        Assert.True(f.DamageNumbers.Size > 0 && f.DamageNumbers.CritSize > f.DamageNumbers.Size); // kritik buyuk
        Assert.True(f.Squash.Amount > 0 && f.Recoil.Stiffness > 0);
    }

    [Fact]
    public void Overlay_takes_user_numbers_only_for_known_keys()
    {
        var baseDoc = GameData.ParseDoc(TestData.Read("feel.json"));
        var user = JsonNode.Parse("""
            { "master": { "shake": 0.5, "nope": 3 },
              "sources": { "civi": { "hitStopMs": 45, "fireSfx": "baska" }, "yok": { "hitStopMs": 1 } },
              "hitStop": "bozuk",
              "recoil": { "stiffness": 200 } }
            """);
        var doc = FeelFile.Overlay(baseDoc, user);
        var f = GameData.ParseFeel(doc);
        Assert.Equal(0.5f, f.Master.Shake);
        Assert.Equal(45f, f.Source("civi").HitStopMs);
        Assert.Equal("nailgun", f.Source("civi").FireSfx);       // yazilar (ses kimlikleri) ayardan gelmez
        Assert.False(f.Sources.ContainsKey("yok"));
        Assert.Equal(200f, f.Recoil.Stiffness);
        Assert.Equal(0.08f, f.HitStop.TimeScale, 3);              // tipi bozuk anahtar yok sayilir
        Assert.False(((JsonObject)doc["master"]!).ContainsKey("nope"));
        // Bindirme varsayilani degistirmez.
        Assert.Equal(1f, GameData.ParseFeel(baseDoc).Master.Shake);
    }

    [Fact]
    public void Diff_saves_only_changed_numbers_and_round_trips()
    {
        var baseDoc = GameData.ParseDoc(TestData.Read("feel.json"));
        var doc = (JsonObject)baseDoc.DeepClone();
        Assert.Empty(FeelFile.Diff(baseDoc, doc));
        ((JsonObject)doc["master"]!)["shake"] = 0.25;
        ((JsonObject)((JsonObject)doc["sources"]!)["tirpan"]!)["hitStopMs"] = 55;
        var diff = FeelFile.Diff(baseDoc, doc);
        Assert.Equal(2, diff.Count);                 // master ve sources
        var back = GameData.ParseFeel(FeelFile.Overlay(baseDoc, JsonNode.Parse(diff.ToJsonString())));
        Assert.Equal(0.25f, back.Master.Shake);
        Assert.Equal(55f, back.Source("tirpan").HitStopMs);
        Assert.Equal(1f, back.Master.Recoil);
    }

    [Fact]
    public void Overlay_without_user_file_is_the_data()
    {
        var baseDoc = GameData.ParseDoc(TestData.Read("feel.json"));
        var a = GameData.ParseFeel(FeelFile.Overlay(baseDoc, null));
        var b = TestData.Load().Feel;
        Assert.Equal(b.Source("tirpan").HitStopMs, a.Source("tirpan").HitStopMs);
        Assert.Equal(b.Sources.Count, a.Sources.Count);
    }
}
