using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>El soketleri ve tutulan nesneler (grips.json): her cerceve kurulabilir, Capa ve El Arabasi tanimli.</summary>
public class GripsTests
{
    static GripsFile G => TestData.Load().Grips;

    static void AssertFrame(string what, float[] pos, float[] bar, float[] fingers)
    {
        Assert.True(pos.Length == 3 && bar.Length == 3 && fingers.Length == 3, $"{what}: vektorler 3 bilesenli olmali");
        float bl = MathF.Sqrt(bar[0] * bar[0] + bar[1] * bar[1] + bar[2] * bar[2]);
        float fl = MathF.Sqrt(fingers[0] * fingers[0] + fingers[1] * fingers[1] + fingers[2] * fingers[2]);
        Assert.True(bl > 0.5f && fl > 0.5f, $"{what}: bar ya da fingers bos");
        float dot = (bar[0] * fingers[0] + bar[1] * fingers[1] + bar[2] * fingers[2]) / (bl * fl);
        Assert.True(MathF.Abs(dot) < 0.9f, $"{what}: bar ile fingers neredeyse ayni yonde (cerceve kurulamaz)");
        foreach (var p in pos) Assert.InRange(p, -1f, 1f);
    }

    [Fact]
    public void Both_hands_have_sockets_on_hand_bones()
    {
        Assert.Equal("RightHand", G.Hands["right"].Bone);
        Assert.Equal("LeftHand", G.Hands["left"].Bone);
        foreach (var (side, h) in G.Hands) AssertFrame("el " + side, h.Pos, h.Bar, h.Fingers);
        Assert.True(G.ArmBlend > 0f);
    }

    [Fact]
    public void Capa_is_held_in_the_right_hand_with_a_model_and_muzzle()
    {
        var c = G.Items["capa"];
        Assert.NotNull(c.Right);
        AssertFrame("capa sag", c.Right.Pos, c.Right.Bar, c.Right.Fingers);
        Assert.StartsWith("res://", c.Model);
        // Tutamak ve namlu modelin dugumleri (pipeline/grip_markers.py); elle konum yok.
        Assert.Equal("grip", c.Right.Node);
        Assert.Equal("muzzle", c.MuzzleNode);
        Assert.All(c.Right.Pos, p => Assert.Equal(0f, p));
        Assert.InRange(c.AimReach, 0.2f, 0.7f);
        Assert.NotNull(c.Back);
    }

    [Fact]
    public void Two_handed_items_name_both_grips()
    {
        var cart = G.Items["el_arabasi"];
        Assert.NotNull(cart.Right);
        Assert.NotNull(cart.Left);
        Assert.Equal("handle_r", cart.Right.Node);
        Assert.Equal("handle_l", cart.Left.Node);
        AssertFrame("araba sag", cart.Right.Pos, cart.Right.Bar, cart.Right.Fingers);
        AssertFrame("araba sol", cart.Left.Pos, cart.Left.Bar, cart.Left.Fingers);
        // Saplar modelin dugumleri (pipeline/grip_markers.py): elle konum yok. Itme uzakligi kol boyunda.
        Assert.All(new[] { cart.Right.Pos, cart.Left.Pos }, p => Assert.All(p, v => Assert.Equal(0f, v)));
        Assert.InRange(cart.PushReach, 0.05f, 0.7f);
    }
}
