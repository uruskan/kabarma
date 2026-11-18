using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: ilerleme (M2 ilerleme akisi). Oyun olaylari (Events) -> Ustalik XP'si, is emri olculeri; Sim'in ilerleme
/// olaylari (odeme, basarim, seviye) -> oyuncuya bildirim. Kurallar Sim'dedir (Orders.cs, Mastery.cs, Achievements.cs,
/// GameState.Progress.cs); burasi yalnizca baglantidir.
/// Is emri olcusu kaynaklari: planted/swollen_*/crop_kind/kabarma_survived/cargo_sold Sim'de (GameState.Night/Progress);
/// animal_registered, tamed_species, herd_size, animal_product burada (Events); gen, Alaca, binek, hayvan oldurme,
/// Ata: ilgili akis kendi sistemini bagladiginda State.Orders.Report / ReportKey / Level cagirir (docs/ICERIK.md 18).
/// </summary>
public partial class Director
{
    void WireProgress()
    {
        var s = State;
        var m = s.Mastery;

        // --- Ustalik XP'si ve toplama bonusu ---
        Events.Gathered += (item, n) =>
        {
            m.Earn("gather:" + item, n);
            int extra = s.GatherBonus(item, n); // Cift Damla, Oduncu, Toplayici ve dal seviyesi
            if (extra > 0) s.Backpack.Add(item, extra);
        };
        Events.Produced += (item, _) => m.Earn(Data.Tool.Foods.Any(f => f.Id == item) ? "cook" : "produce");
        Events.StationPlaced += _ => m.Earn("station");
        Events.PiecePlaced += _ => m.Earn("build");
        Events.Ate += _ => m.Earn("ate");
        Events.CreatureKilled += (_, swollen) => m.Earn(swollen ? "kill_swollen" : "kill");

        // --- Hayvan olculeri (Events: Gidak.Pen.cs ve PenSystem yayar) ---
        Events.AnimalPenned += species =>
        {
            s.Orders.Report("animal_registered");
            s.Orders.ReportKey("tamed_species", species);
            s.Orders.Level("herd_size", s.Coop.Animals.Count(a => a.Registered));
            m.Earn("pen");
        };
        Events.Laid += (_, _) => { s.Orders.Report("animal_product"); m.Earn("lay"); };

        // --- Bildirimler ---
        s.OrderPaid += (credits, veri, what) =>
        {
            GD.Print($"ORDER PAID {what} credits={credits} veri={veri}");
            if (State.Orders.DefterOpen) Hud.Hint($"+{credits:N0} Kredi · +{veri} Veri", 3f);
        };
        s.Orders.GrantMade += (chain, g) =>
        {
            GD.Print($"ORDER GRANT {g.Kind}:{g.Id}");
            Hud.Hint($"{chain.Def.Name}: {g.Name}", 4f);
        };
        s.Achievements.UnlockedEvent += a =>
        {
            GD.Print($"ACHIEVEMENT {a.Id}");
            Hud.Big($"BAŞARIM · {a.Name}", Palette.Warning, 2.6f);
            Sfx.Play("levelup", null, -8f);
        };
        m.LevelUp += (b, lv) =>
        {
            if (lv % 5 == 0 || lv < 5) Hud.Hint($"{m.Branches.First(x => x.Id == b).Name}: seviye {lv}", 2f);
        };
        m.PerkUnlocked += (b, tier) =>
        {
            GD.Print($"MASTERY perk tier open {b} {tier}");
            Hud.Big($"USTALIK · {m.Branches.First(x => x.Id == b).Name} {tier}: özellik seç (Defter)", Palette.CoopBlue.Lerp(Palette.OffWhite, 0.3f), 3.2f);
        };

        if (Main.Args.ContainsKey("ilerlemetest")) AddChild(new IlerlemeTest(this) { Name = "IlerlemeTest" }); // --test=ilerleme
    }
}
