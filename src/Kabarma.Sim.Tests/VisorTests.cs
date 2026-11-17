using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Vizor verisi (visor.json): SANAT-SES §3'un dort ifadesi var, olaylar var olan ifadeleri kullanir.</summary>
public class VisorTests
{
    static VisorFile Visor => TestData.Load().Visor;

    [Fact]
    public void Art_bible_expressions_exist()
    {
        foreach (var e in new[] { "saskin", "yorgun", "mutlu", "korkmus", "notr", "uyku", "bayilma" })
            Assert.True(Visor.Expressions.ContainsKey(e), $"{e} ifadesi yok");
    }

    [Fact]
    public void Event_steps_use_known_expressions_and_positive_durations()
    {
        var v = Visor;
        foreach (var (ev, steps) in v.Events)
        {
            Assert.NotEmpty(steps);
            foreach (var s in steps)
            {
                Assert.True(v.Expressions.ContainsKey(s.Expr), $"{ev}: bilinmeyen ifade {s.Expr}");
                Assert.True(s.Seconds > 0, $"{ev}: sure 0");
            }
        }
    }

    [Fact]
    public void Eye_shapes_are_drawable()
    {
        var known = new HashSet<string> { "round", "happy", "wide", "closed", "x" };
        foreach (var (id, e) in Visor.Expressions) Assert.Contains(e.Eye, known);
    }

    [Fact]
    public void Gameplay_events_have_a_face()
    {
        // Gorevdeki olaylar: hasar, kart, satis, uyku (uyanma), yemek; ayrica gece ve safak.
        foreach (var ev in new[] { "hasar", "kart", "satis", "uyanma", "yemek", "gece", "safak" })
            Assert.True(Visor.Events.ContainsKey(ev), $"{ev} olayinin ifadesi yok");
    }
}
