using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Bot, ekonomi2 (GDD 13): "katalog" adimi. Onceki adimlarda rampadan satilan kabarik urunun ve kasanin Kredisiyle
/// Kargo Rampasi terminalinden Katalog'u acar, Salgam Tohumu paketini alir, kapsulun inmesini bekler, yanina gidip E ile
/// acar. Hepsi oyuncunun yolundan (Bot.Hands.cs: Use, MenuPick, MenuClose; hareket ve nisan). Esya ve Kredi verilmez:
/// Kredi yetmiyorsa yoldaki kargo odemesi beklenir.
/// </summary>
public partial class Bot
{
    const string BotCatalogEntry = "tohum_salgam";
    bool _poorLogged;

    void RegisterMarket()
    {
        _steps["katalog"] = Machine(StepKatalog);
    }

    void StepKatalog(Vector3 o)
    {
        var s = _d.State;
        var hud = _d.Hud;
        var pad = _d.Pod.PadCenter;

        // 1) Inmis kapsul: yanina git, E ile ac.
        var pod = _d.GetTree().GetNodesInGroup("deliveries").OfType<EconomyDeliveryPod>().Where(x => x.Landed)
            .OrderBy(x => x.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (pod != null)
        {
            if (hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.4f; } return; }
            Aim(pod.GlobalPosition + Vector3.Up * 0.4f);
            if (MoveTo(pod.GlobalPosition, 1.3f, false) && _menuT <= 0) { Use(); _menuT = 0.8f; }
            return;
        }
        // 2) Teslimat yolda: menuyu kapat, bekle.
        if (s.Catalog.Deliveries.Count > 0)
        {
            if (hud.MenuOpen && _menuT <= 0) { MenuClose(); _menuT = 0.4f; }
            P.MoveOverride = Vector3.Zero;
            return;
        }
        // 3) Kredi yetmiyorsa yoldaki kargo odemesini bekle.
        var entry = s.Catalog.Entry(BotCatalogEntry);
        int price = entry == null ? int.MaxValue : s.Catalog.Price(entry);
        if (s.Credits < price)
        {
            P.MoveOverride = Vector3.Zero;
            if (!_poorLogged && s.Cargo.Pending.Count == 0) { _poorLogged = true; GD.Print($"BOT katalog: Kredi yetmiyor ({s.Credits} < {price}), odeme de yok"); }
            return;
        }
        // 4) Rampaya git, E: rampa paneli; Katalog satiri; tohum paketi satiri.
        if (!hud.MenuOpen)
        {
            Aim(pad + Vector3.Up);
            var away = P.GlobalPosition - pad; away.Y = 0;
            if (away.Length() < 0.1f) away = Vector3.Back;
            if (!MoveTo(pad + away.Normalized() * 3.2f, 0.6f, false)) return;
            if (_menuT <= 0) { Use(); _menuT = 0.6f; }
            return;
        }
        if (_menuT > 0) return;
        if (hud.MenuTitleText == "Kargo Rampası" && _d.RampTerminalRow >= 0) { MenuPick(_d.RampTerminalRow + 1); _menuT = 0.6f; return; }
        if (hud.MenuTitleText == Director.CatalogTitle)
        {
            int row = s.Catalog.Visible.Select(e => e.Id).ToList().IndexOf(BotCatalogEntry);
            if (row >= 0) { MenuPick(row); GD.Print($"BOT katalog: {BotCatalogEntry} ({price} Kredi, cuzdan {s.Credits})"); }
            _menuT = 0.8f;
            return;
        }
        MenuClose(); // baska bir panel acik (istasyon, canta)
        _menuT = 0.4f;
    }
}
