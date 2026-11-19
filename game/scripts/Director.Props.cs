using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director, kalan yerlestirilebilirler (M4 kale; ICERIK §10.2): hizmet parcalarinin E davranisi. Borsa Tahtasi, Katalog
/// Terminali ve Tohum Carki rampa panelindeki terminalleri (Director.EconomyMarket.cs) kendi yanindan acar; Is Emri Panosu
/// Defter'in is emirleri sayfasini; Harita Masasi harita sayfasini; Gardirop ve Tablo gibi parcalarin isi sonraki akislarda
/// (dolap: kozmetik) ve simdilik bilgi satiri verir. Depo (slots), yatak (spawn) ve isik (lightRange) zaten veriden.
/// </summary>
public partial class Director
{
    /// <summary>E: menzildeki Furniture bir hizmet parcasiysa kendi arayuzunu acar. Isledi ise true.</summary>
    public bool InteractProp(Furniture f)
    {
        switch (f.Def.Id)
        {
            case "borsa_tahtasi": OpenBorsaMenu(); return true;
            case "katalog_terminali": OpenCatalogMenu(); return true;
            case "tohum_carki": OpenWheelMenu(); return true;
            case "is_emri_panosu": Hud.OpenInventory(1); return true; // Defter: is emirleri
            case "harita_masasi": Hud.Hint(Loc.T("dir.hint_map_table"), 2.5f); return true;
            case "gardirop": Hud.Hint(Loc.T("dir.hint_wardrobe_todo"), 2.5f); return true;
            case "radyo": Hud.Hint(Loc.T("dir.hint_radio"), 1.8f); return true;
            case "saat": Hud.Hint(Loc.T("dir.hint_clock", State.Calendar.Day), 1.8f); return true;
        }
        return false;
    }
}
