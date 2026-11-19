using System.Linq;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>M6 arayuz: ui.json "access" (erisilebilirlik ve fotograf modu) sayilari tutarli, pozlar player_anims.json'da var.</summary>
public class ArayuzDataTests
{
    [Fact]
    public void Access_settings_and_photo_mode_numbers_are_consistent()
    {
        var d = TestData.Load();
        var a = d.Ui.Access;
        Assert.Equal(4, a.ColorBlindModes.Count);                 // kapali + uc palet
        Assert.Contains(1f, a.UiScales);
        Assert.Contains(1f, a.SubtitleScales);
        Assert.True(a.SubtitleScales.SequenceEqual(a.SubtitleScales.OrderBy(x => x)));
        Assert.InRange(a.PhotoFovDefault, a.PhotoFovMin, a.PhotoFovMax);
        Assert.True(a.PhotoFovStep > 0 && a.PhotoSpeed > 0 && a.PhotoRange > 5);
        var clips = d.PlayerAnims.Clips.Select(c => c.Id).ToHashSet();
        foreach (var p in a.PhotoPoses) Assert.True(clips.Contains(p) || p is "sleep" or "eat" or "plant", $"poz {p} yok");
        Assert.Equal(12, d.Cosmetics.Cosmetics.Count(c => c.Slot == "vizor")); // fotograf modu yuz listesi
    }
}