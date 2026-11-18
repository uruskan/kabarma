using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: kemer ve elde tutulan esya (GDD 4, 5; SANAT-SES 8.1; URETIM 6 M1 kararlari 20 ve 23). 1-8 ve fare tekerlegi
/// kemer yuvasini secer; secili yuvadaki esya eldedir (Capa, Insa Plani, yerlestirilebilir, yemek, Meshale), bos yuvada
/// eller bostur. Baslik yalniz Q carkindan ve yalniz Capa eldeyken. El uretim kuyrugu da burada isler (Hud.Craft.cs).
/// </summary>
public partial class Director
{
    string _heldWas;

    /// <summary>Kemer yuvasini secer (1-8): esya ele gelir, adi kemerin ustunde belirir.</summary>
    public void SelectBelt(int slot)
    {
        if (slot == State.Belt.Selected) { Hud.SelectBeltSlot(slot); return; }
        State.Belt.Select(slot);
        Hud.SelectBeltSlot(slot);
        Sfx.Play("place", null, -18f);
    }

    /// <summary>Fare tekerlegi / RB-LT: sonraki ya da onceki yuva.</summary>
    public void ScrollBelt(int dir)
    {
        State.Belt.Scroll(dir);
        Hud.SelectBeltSlot(State.Belt.Selected);
        Sfx.Play("place", null, -22f);
    }

    /// <summary>Capa'yi ele alir (kemerdeki yuvasini secer).</summary>
    public void HoldCapa() { int i = State.Belt.SlotOf(PlanTools.Capa); if (i >= 0) SelectBelt(i); }

    /// <summary>Her kare: eldeki esya oyuncuya (Capa sirtta mi, Meshale isigi), el uretim kuyrugu.</summary>
    void TickKemer(float dt)
    {
        var held = State.Belt.Held;
        if (held != _heldWas)
        {
            if (Hud.WheelOpen) Hud.CancelWheel(); // elden inen esyanin carki kapanir
            _heldWas = held;
            GD.Print($"HELD {held ?? "-"}");
        }
        Player.SetHeldItem(held, Data.Tool.Placeables.FirstOrDefault(p => p.Id == "mesale"));
        TickCraft(dt); // Director.Craft.cs
    }
}
