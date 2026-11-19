using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director, ilerleme4 (M4): zincir odullerinin sahiplerine baglanmasi (kart, Nisan, uye, kozmetik) ve yeni is emri olculerinin
/// kaynaklari. Kural Sim'dedir (Orders, Wardrobe, CardBook, Catalog); burasi yalniz baglanti.
/// Bagli olculer: build_piece, machine_kind, machine_output, dish_kind, sold_kind, oyuk_cleared, knockdown (credits_earned ve wheel_spin Sim'de).
/// Baglanmayanlar (ilgili sistem State.Orders.Report / ReportKey / Level cagirir): power_w, signal_receiver, comfort, tower_opened,
/// map_pct, scan_kind, kadim_stone, wreck_opened, hot_crate, alaca_row, revive, launch, cargo_ton, drive_m, photo.
/// </summary>
public partial class Director
{
    bool _wasKnocked;

    void WireProgress4()
    {
        var s = State;
        // --- zincir odulleri: kart ve Nisan (uye ve kozmetik: InitDolap) ---
        void Grant(string kind, string id)
        {
            if (kind == "card") s.CardBook.Unlock(id, "order");
            else if (kind == "nisan") s.CardBook.AddPoint(id);
        }
        s.Orders.GrantMade += (_, g) => Grant(g.Kind, g.Id);
        foreach (var key in s.Orders.Granted.ToList())
        {
            int i = key.IndexOf(':');
            if (i > 0 && key[..i] is "card") s.CardBook.Unlock(key[(i + 1)..], "order"); // yukleme: acik olan kart yine acik (Nisan puani tekrar verilmez)
        }

        // --- yeni olculer ---
        Events.PiecePlaced += _ => s.Orders.Report("build_piece");
        Events.StationPlaced += id => s.Orders.ReportKey("machine_kind", id);
        Events.Produced += (item, n) =>
        {
            if (n <= 0) return;
            s.Orders.Report("machine_output", n);
            if (Data.Tool.Foods.Any(f => f.Id == item)) s.Orders.ReportKey("dish_kind", item);
        };
        Events.Sold += (item, _, _) => s.Orders.ReportKey("sold_kind", item);   // credits_earned ve wheel_spin Sim'de (GameState.Night/Market)
        Events.CropShipped += (crop, _, _) => s.Orders.ReportKey("sold_kind", crop);
        Events.OyukCleared += id => s.Orders.ReportKey("oyuk_cleared", id);

        // --- katalog kozmetigi ve Cark ---
        s.Catalog.OwnsCosmetic = id => s.Wardrobe.HasCosmetic(id);
        s.Catalog.CosmeticBought += id => s.Wardrobe.Unlock("cosmetic", id);
    }

    /// <summary>Yere serilme sayaci (Ezilenler Kulubu): Player.Down gecisi.</summary>
    public void TickProgress4Public() => TickProgress4();

    void TickProgress4()
    {
        bool down = Player != null && Player.Down;
        if (down && !_wasKnocked) State.Orders.Report("knockdown");
        _wasKnocked = down;
    }

    /// <summary>Tohum Carki "kozmetik" odulu: dolapta acilmamis bir cark/katalog kozmetigi.</summary>
    void WheelCosmetic(string rewardId)
    {
        if (rewardId != "kozmetik") return;
        var id = State.Wardrobe.WheelPick(State.MasteryRng);
        if (id == null) { Hud?.Hint("Çark: tüm kozmetikler zaten dolapta", 3f); return; }
        State.Wardrobe.Unlock("cosmetic", id);
    }
}
