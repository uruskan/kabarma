using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: cipler (M3 cip akisi; ICERIK 6.3.1). Kabarma gecesi boyunca takma/cikarma kilitli; dusme kaynaklari:
/// Ata yenilince ("ata:KIMLIK"), safakta Kabarma odulu ("kabarma_reward", KS'ye gore). Holding, Oyuk ve Cark kendi
/// olaylarinda State.DropChip("holding:kamp" | "oyuk:kostebek_bey" | "wheel:common" ...) cagirir; tablolar chips.json "sources".
/// </summary>
public partial class Director
{
    void WireChips()
    {
        var chips = State.Chips;
        chips.Locked = NightActive;
        Events.NightStarted += () => chips.Locked = true;
        Events.Dawn += () =>
        {
            chips.Locked = false;
            int ks = State.Night?.Ks ?? 0;
            Announce(State.DropChip("kabarma_reward", ks));
        };
        Events.AtaDefeated += id => Announce(State.DropChip("ata:" + id));
        chips.Added += c => GD.Print($"CHIP added {c.Type} {c.Rarity} id={c.Id}");
        if (Main.Args.ContainsKey("ciptest")) AddChild(new CipTest(this) { Name = "CipTest" }); // --test=cip
    }

    void Announce(ChipInstance c)
    {
        if (c == null) return;
        var def = State.Chips.TypeOf(c.Type);
        var rar = State.Chips.RarityOf(c.Rarity);
        Hud.Big(Loc.T("dir.big_chip", def?.Name, rar?.Name), Color.FromHtml("#" + (rar?.Color ?? "FFFFFF")), 2.8f);
        Sfx.Play("levelup", null, -8f);
    }
}
