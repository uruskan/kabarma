using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Kartlar akisi (M3, GDD 15.1): kart teklifinin eki (yenileme, surgun, 12 yuva, asi ve Nisan etiketi) ve Kart Defteri
/// (acik kartlar, Nisan, kart jetonu). Ikonlar YER TUTUCU: sinif rengi seridi; uretim istegi raporda.
/// </summary>
public partial class Hud
{
    static Color ClassColor(string cls) => cls switch
    {
        "capa" => T.Teal,
        "yorunge" => T.Oz,
        "beden" => T.Stamina,
        "ciftlik" => T.Short,
        "risk" => T.Brick,
        _ => Palette.MayaViolet,
    };

    static string ClassName(string cls) => cls switch
    {
        "capa" => "Çapa", "yorunge" => "Yörünge", "beden" => "Beden", "ciftlik" => "Çiftlik", "risk" => "Risk", _ => "Aşı",
    };

    /// <summary>Hud.Rust'tan, PaintOffer'dan sonra: teklifin ustunde sayaclar, kartlarin uzerinde asi ve Nisan.</summary>
    void PaintCardExtras()
    {
        var offer = _offer;
        var night = _offerNight;
        if (offer == null || night == null || _d.Night != night || night.Offer != offer) return;
        _o = CanvasFrame;
        var h = night.Hand;
        int n = offer.Cards.Count;
        float cw = 250, ch = 164, gap = 18;
        float total = n * cw + (n - 1) * gap;
        float x0 = (T.DesignW - total) / 2f, y = T.Data.Belt.Y - 30 - ch;

        // Ust satir: yenileme, surgun, yuva
        float bx = x0, by = y - 34;
        Rect(bx, by, total, 28, T.A(T.Dialog, 0.85f));
        float kx = bx + 8;
        kx += Keycap(KeyText("card_reroll", "U"), kx, by + 3, 22, h.RerollsLeft > 0 ? T.Ink : T.Muted, PadDevice) + 6;
        kx += Text($"YENİLE {h.RerollsLeft}", kx, by + 5, T.Data.Type.Caption, h.RerollsLeft > 0 ? T.Ink : T.Muted, true, true) + 16;
        kx += Keycap(KeyText("card_banish", "K"), kx, by + 3, 22, h.BanishesLeft > 0 ? T.Ink : T.Muted, PadDevice) + 6;
        Text(_d.BanishMode ? "SÜRGÜN: KARTI SEÇ" : $"SÜRGÜN {h.BanishesLeft}", kx, by + 5, T.Data.Type.Caption, _d.BanishMode ? T.Brick : (h.BanishesLeft > 0 ? T.Ink : T.Muted), true, true);
        TextIn($"YUVA {h.SlotsUsed}/{_d.Data.Cards.MaxSlots}", bx, by + 5, total - 10, T.Data.Type.Caption, h.SlotsUsed >= _d.Data.Cards.MaxSlots ? T.Short : T.Ink, HorizontalAlignment.Right, true, true);

        for (int i = 0; i < n; i++)
        {
            var c = offer.Cards[i];
            float x = x0 + i * (cw + gap);
            Rect(x, y + 4, 4, ch - 8, ClassColor(c.Class)); // YER TUTUCU ikon: sinif rengi
            if (_d.BanishMode && !c.IsAsi && night.Rank(c.Id) == 0)
            {
                Rect(x, y, cw, 3, T.Brick); Rect(x, y + ch - 3, cw, 3, T.Brick);
                Rect(x, y, 3, ch, T.Brick); Rect(x + cw - 3, y, 3, ch, T.Brick);
            }
            string tag = c.IsAsi
                ? "AŞI · " + string.Join(" + ", c.Parents.Select(p => _d.Data.Cards.Find(p)?.Name ?? p))
                : T.Upper(ClassName(c.Class)) + (c.Team ? " · EKİP" : "");
            Text(Fit(T.Upper(tag), cw - 24, T.Data.Type.Caption, true, true), x + 12, y + ch - 44, T.Data.Type.Caption, c.IsAsi ? Palette.MayaViolet : T.Muted, true, true);
            int lv = h.NisanLevel(c.Id);
            if (lv > 0) TextIn($"NİŞAN {lv}", x, y + ch - 25, cw - 12, T.Data.Type.Caption, T.Short, HorizontalAlignment.Right, true, true);
        }
    }

    /// <summary>Kart Defteri (GDD 15): sinif basina acik kartlar, Nisan seviyeleri, kart jetonu harcama.</summary>
    public void OpenCardBook()
    {
        var book = _d.State.CardBook;
        var cards = _d.Data.Cards;
        var items = new List<MenuItem>();
        foreach (var g in cards.Cards.GroupBy(c => c.Class))
        {
            int open = g.Count(c => book.IsUnlocked(c.Id));
            var nisan = g.Where(c => book.Level(c.Id) > 0).Select(c => $"{c.Name} N{book.Level(c.Id)}");
            string extra = nisan.Any() ? " · " + string.Join(", ", nisan) : "";
            items.Add(new MenuItem($"{ClassName(g.Key)}: {open}/{g.Count()} açık{extra}", () => false, () => { }));
        }
        if (book.Jetons > 0)
        {
            var choices = book.JetonChoices.ToList();
            if (choices.Count > 0)
                foreach (var id in choices)
                {
                    var cid = id;
                    items.Add(new MenuItem($"Jetonla aç: {cards.Card(cid).Name}", () => book.Jetons > 0, () => { book.SpendJeton(cid); OpenCardBook(); }));
                }
            else
                foreach (var c in book.Open.Where(c => !c.IsAsi).Take(8))
                {
                    var cid = c.Id;
                    items.Add(new MenuItem($"Jetonla Nişan puanı: {c.Name} ({book.Points(cid)})", () => book.Jetons > 0, () => { book.SpendJeton(cid); OpenCardBook(); }));
                }
        }
        int total = cards.Cards.Count(c => book.IsUnlocked(c.Id));
        ShowMenu($"KART DEFTERİ · {total}/{cards.Cards.Count} AÇIK · JETON {book.Jetons}", items,
                 "Kartlar iş emirleri, üye tipleri, Atalar ve jetonla açılır. Aşı, iki ebeveyni açıkken kendiliğinden açılır.");
    }
}
