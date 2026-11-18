using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot'un ilk is emri zincirini (Ciraklik) dogrulamasi (ilerleme akisi). Bot yeni bir sey yapmaz: ilk gunun adimlarini
/// oyuncu yolundan oynar (9 salgam ek, agila kayit, kargo, ilk Kabarma). Zincirin ilk dort adimi (Ata hari) ve
/// adim odulleri (Kredi, Veri) sayilmis olmali; olmazsa "RESULT fail orders ..." yazilir. Esya verilmez, adim atlanmaz.
/// </summary>
public partial class Bot
{
    /// <summary>Ciraklik'in adimlari bitmis mi? Sorun varsa metin, yoksa null.</summary>
    string OrdersCheck()
    {
        var s = _d.State;
        var c = s.Orders.Chain("ciraklik");
        if (c == null) return "ciraklik zinciri yok";
        for (int i = 0; i < 4; i++) if (!c.StepDone(i)) return $"ciraklik adim {i + 1} bitmedi ({c.Def.Steps[i].Goal}: {c.Progress(i)}/{c.Def.Steps[i].Target})";
        if (!s.Orders.DefterOpen) return "Defter acilmadi";
        if (s.Veri < 10 + 20 + 40 + 70) return $"odemeler gelmedi: veri={s.Veri}";
        if (s.OrderCredits < 100 + 400 + 1500 + 6000) return $"odemeler gelmedi: kredi={s.OrderCredits}";
        return null;
    }
}
