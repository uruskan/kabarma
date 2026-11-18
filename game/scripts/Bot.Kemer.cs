using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, karar 23'un yolu (GDD 4, 5, 7): yerlestirilebilir once uretilir (Tab → Uretim; istasyon tarifi o istasyonun E
/// menusunden), cantaya girer, kemerden ele alinir, hayaletle kurulur. Malzeme oyun yolundan toplanir (Kazma, Bicme);
/// esya verilmez, adim atlanmaz. Butun dokunuslar Bot.Hands.cs yontemleriyle (girdi kipinde sentetik olay).
/// </summary>
public partial class Bot
{
    /// <summary>
    /// Yerlestirilebilir cantada mi; degilse oyuncunun yolundan edinir: malzemesini toplar, Uretim panelinden (el) ya da
    /// istasyonun E menusunden uretir, uretim bitene dek bekler. Cantada ve paneller kapaliysa true.
    /// </summary>
    bool HavePlaceable(string id)
    {
        var inv = _d.State.Backpack;
        if (inv.Count(id) > 0)
        {
            if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; }
            return true;
        }
        var r = _d.Data.Tool.RecipeFor(id);
        if (r == null) { if (_stepT > 3f) { GD.Print($"BOT {id}: tarifi yok"); _stepT = 0; } return false; }
        // Uretiliyor: bekle (paneli kapat, istasyonun yaninda dur).
        bool inHand = _d.State.Craft.Jobs.Any(j => j.Recipe.Id == id);
        var making = _d.Stations.FirstOrDefault(s => IsInstanceValid(s) && s.Queue.Active?.Id == id);
        if (inHand || making != null)
        {
            if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; }
            P.MoveOverride = Vector3.Zero;
            if (making != null) Aim(making.GlobalPosition + Vector3.Up);
            return false;
        }
        // Malzeme: once hepsi (panel kapali; toplarken Capa elde).
        foreach (var (item, n) in r.In)
            if (inv.Count(item) < n)
            {
                if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; }
                EnsureItem(item, n);
                return false;
            }
        if (CraftRules.IsHand(r)) { CraftHand(id); return false; }
        // Istasyon tarifi (Tas Firin, Ocakbasi, Paketleme: Tezgah'ta): istasyona yuru, E, satiri sec.
        var st = StationOf(r.Station);
        if (st == null) { if (_stepT > 3f) { GD.Print($"BOT {id}: {r.Station} yok"); _stepT = 0; } return false; }
        if (st.Queue.Active != null) { WaitAt(st); return false; }
        if (_d.Hud.MenuOpen && _d.Hud.CraftStation != r.Station) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; }
        UseStation(st, RecipeIndex(st, id));
        return false;
    }

    /// <summary>El tarifini Uretim panelinden uretir: Tab, Q / RB ile Uretim sayfasi, satir (1-9 ya da d-pad + A).</summary>
    void CraftHand(string id)
    {
        P.MoveOverride = Vector3.Zero;
        if (_menuT > 0) return;
        if (!_d.Hud.InventoryOpen) { Bag(); _menuT = 0.4f; return; }
        if (_d.Hud.InventoryPage != Hud.CraftPage || _d.Hud.CraftStation != null) { NextPage(); _menuT = 0.3f; return; }
        int i = _d.Hud.CraftRowIds.ToList().IndexOf(id);
        if (i < 0) { GD.Print($"BOT {id}: Uretim panelinde yok"); _menuT = 1f; return; }
        MenuPick(i);
        _menuT = 0.6f;
        GD.Print($"BOT craft {id} row={i}");
    }

    /// <summary>
    /// Yerlestirilebiliri kurar: edinir (HavePlaceable), kemerden ele alir, durulacak yere yurur, hayalete nisan alir, sol
    /// tik. Kurulunca cantadan duser (Capa.Insa).
    /// </summary>
    void PlaceHeld(string id, Vector3 at, Vector3 stand, float within = 0.6f, bool routed = false)
    {
        if (!HavePlaceable(id)) { _stepT = 0; return; }
        if (!HoldItem(id)) { _stepT = 0; return; }
        Aim(at);
        bool there = routed ? MoveRouted(stand, within) : MoveTo(stand, within, false);
        if (!there) { _stepT = 0; return; }
        if (_stepT > 1.0f) { FirePressed = true; _stepT = 0; GD.Print($"BOT place {id} at {at}"); }
    }

    /// <summary>Insa Plani'ni ele alir ve parcayi secer. Plan elde ve parca secili ise true.</summary>
    bool PlanWith(string piece)
    {
        if (!HoldItem(PlanTools.Plan)) return false;
        if (_d.Capa.PlanPiece == piece) return true;
        if (_stepT > 0.12f) { PickPiece(piece); _stepT = 0; }
        return false;
    }
}
