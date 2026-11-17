using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Pano, esya ikonlari: game/assets/icons/items/&lt;kimlik&gt;.png (ikon oturumu, modelden cekilen ikonlar). Dosya yoksa
/// yer tutucu (esyanin tonunda kare ve adi) cizilir. ikon oturumunun ItemIcons yardimcisi birlesince
/// <see cref="ItemIconTexture"/> tek satirla ona baglanir (ornek: ItemIcons.Get(id)).
/// </summary>
public partial class Hud
{
    /// <summary>Esyanin ikonu ya da null (yer tutucu cizilir). Onbellekli: her kare diske gitmez.</summary>
    Texture2D ItemIconTexture(string id) => ItemIcons.Find(id); // ItemIcons.cs (ikon oturumu): onbellekli, yoksa null
}
