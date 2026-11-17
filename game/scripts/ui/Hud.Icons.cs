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
    const string ItemIconDir = "res://assets/icons/items/";
    readonly Dictionary<string, Texture2D> _itemIcons = new();

    /// <summary>Esyanin ikonu ya da null (yer tutucu cizilir). Onbellekli: her kare diske gitmez.</summary>
    Texture2D ItemIconTexture(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_itemIcons.TryGetValue(id, out var t)) return t;
        string path = ItemIconDir + id + ".png";
        t = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        _itemIcons[id] = t;
        return t;
    }
}
