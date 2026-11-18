using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Elde tutulan obur esyalar (GDD 4 "Elde tutulan obur esyalar"; URETIM 6 M1 karari 23). Capa elde degilken Capa.Process
/// buraya gelir: Insa Plani (yapi seti, sol tik kurar; parca carki Director.Input.cs), yerlestirilebilir (hayalet, sol tik
/// kurar, R dondurur; Capa.Insa.cs), yemek (sol tik yer). Meseale elde isik verir (Player.Hand.cs). Bos el: hicbir sey;
/// E her durumda calisir.
/// </summary>
public partial class Capa
{
    /// <returns>Bu karede girdiye gorsel tepki verildiyse true (gecikme olcumu).</returns>
    bool TickHand(float dt, bool fire, bool firePressed, Vector3 aim)
    {
        var id = HeldItem;
        if (id == null) { HideHandGhosts(); return false; }
        if (id == PlanTools.Plan)
        {
            if (_ghost != null) _ghost.Visible = false;
            if (_d.Build == null) { _d.Hud.Hint("İnşa Planı: yapı seti yok", 0.2f); return false; }
            int before = _d.Build.System.Pieces.Count;
            _d.Build.Tick(_d.Player, aim, firePressed && !_d.Hud.WheelOpen);
            return _d.Build.System.Pieces.Count > before;
        }
        if (DefOf(id) != null)
        {
            _d.Build?.Hide();
            UpdateGhost(aim);
            if (firePressed && _ghost != null && _ghost.Visible) { TryPlace(); return true; } // kurar ya da "konmaz" ipucu
            return false;
        }
        HideHandGhosts();
        var food = _d.Data.Tool.Foods.FirstOrDefault(f => f.Id == id);
        if (food != null)
        {
            _d.Hud.Hint($"{food.Name} ×{_d.State.Backpack.Count(id)}  ·  {{act:fire}}: ye", 0.2f);
            if (firePressed) { _d.EatFood(food); return true; }
        }
        return false;
    }
}
