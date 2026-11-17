using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Oyuncu klip listesi (player_anims.json) SANAT-SES §3.1'in M1 tablosuna ve yer tutucu kuralina uyar.</summary>
public class PlayerAnimsTests
{
    static PlayerAnimsFile Anims => TestData.Load().PlayerAnims;

    [Fact]
    public void M1_has_thirty_clips_twenty_five_produced_five_coded()
    {
        var m1 = Anims.Clips.Where(c => c.M1).ToList();
        Assert.Equal(30, m1.Count);
        Assert.Equal(25, m1.Count(c => c.Source is "library" or "locomotion" or "cut"));
        Assert.Equal(5, m1.Count(c => c.Source == "code"));
    }

    [Fact]
    public void M1_groups_match_the_art_bible_table()
    {
        var g = Anims.Clips.Where(c => c.M1).GroupBy(c => c.Group).ToDictionary(x => x.Key, x => x.Count());
        Assert.Equal(2, g["bekleme"]);
        Assert.Equal(8, g["yurume"]);
        Assert.Equal(8, g["kosu"]);
        Assert.Equal(1, g["depar"]);
        Assert.Equal(3, g["zipla"]);
        Assert.Equal(1, g["dusme"]);
        Assert.Equal(2, g["kalkis"]);
        foreach (var code in new[] { "ekme", "yeme", "uyuma", "itme", "suda" }) Assert.Equal(1, g[code]);
        // Sekiz yon, yurume ve kosuda ayri ayri
        foreach (var grp in new[] { "yurume", "kosu" })
            Assert.Equal(new[] { "b", "bl", "br", "f", "fl", "fr", "l", "r" },
                Anims.Clips.Where(c => c.M1 && c.Group == grp).Select(c => c.Dir).OrderBy(d => d, StringComparer.Ordinal));
    }

    [Fact]
    public void Ids_are_unique()
    {
        var dup = Anims.Clips.GroupBy(c => c.Id).Where(x => x.Count() > 1).Select(x => x.Key);
        Assert.Empty(dup);
    }

    [Fact]
    public void Every_placeholder_falls_back_to_a_playable_clip()
    {
        var a = Anims;
        foreach (var c in a.Clips.Where(c => c.Placeholder))
        {
            Assert.False(string.IsNullOrEmpty(c.Fallback), $"{c.Id}: yedek yok");
            Assert.False(string.IsNullOrEmpty(c.Request), $"{c.Id}: uretim istegi notu yok");
            var (id, _) = a.Resolve(c.Id);
            var r = a.Clip(id);
            Assert.NotNull(r);
            Assert.False(r!.Placeholder, $"{c.Id} -> {id} hala yer tutucu");
        }
    }

    [Fact]
    public void Produced_clips_name_their_delivered_action()
    {
        foreach (var c in Anims.Clips.Where(c => c.Source is "library" && !c.Placeholder))
            Assert.False(string.IsNullOrEmpty(c.Action), $"{c.Id}: action yok");
    }

    [Fact]
    public void Cuts_reference_a_produced_clip_and_a_valid_range()
    {
        var a = Anims;
        foreach (var c in a.Clips.Where(c => c.Source == "cut"))
        {
            var src = a.Clip(c.From);
            Assert.NotNull(src);
            Assert.False(src!.Placeholder);
            Assert.True(c.End > c.Start, $"{c.Id}: aralik bos");
        }
    }

    [Fact]
    public void Backward_placeholder_plays_forward_clip_in_reverse()
    {
        Assert.Equal(("walk", -1f), Anims.Resolve("walk_b"));
        Assert.Equal(("run", 1f), Anims.Resolve("run_l"));
        Assert.Equal(("idle", 1f), Anims.Resolve("idle"));
        Assert.Equal(("nope", 1f), Anims.Resolve("nope"));
    }
}
