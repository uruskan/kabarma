using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Director: kemer secimi (GDD 4 ve 5, SANAT-SES 8.1, URETIM 6 M1 karari 20). 1-8 kemer yuvasini secer, baslik secmez
/// (baslik yalniz Q carkindan). Secilen yuva mavi-beyaz olur ve adi kemerin ustunde belirir; yemekse yenir (eski Alt+1-8
/// kisayolunun isi). Tohum ve civi kemerden baslik takmaz: Toprak ve Civi Tabancasi carktan secilir.
/// </summary>
public partial class Director
{
    public void SelectBelt(int slot)
    {
        Hud.SelectBeltSlot(slot);
        var id = State.Belt.At(slot);
        if (id == null) return;
        var food = Data.Tool.Foods.FirstOrDefault(f => f.Id == id);
        if (food != null) EatFood(food);
        else Sfx.Play("place", null, -18f);
    }
}
