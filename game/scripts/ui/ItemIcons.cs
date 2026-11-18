using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Esya ikonlari (SANAT-SES 8.1): kimlikten 256 px seffaf ikon. Dosyalar res://assets/icons/items/&lt;kimlik&gt;.png;
/// kaynak ve istemler ayni klasorde SOURCES.md. Eksik ikonda yer tutucu (_placeholder.png) doner.
/// Kapsam: kaynaklar, urunler, yemekler, yerlestirilebilirler, istasyonlar, yapi parcalari, Capa basliklari, silahlar, kartlar.
/// </summary>
public static class ItemIcons
{
    public const string Dir = "res://assets/icons/items/";
    const string PlaceholderPath = Dir + "_placeholder.png";
    static readonly Dictionary<string, Texture2D> Cache = new();
    static Texture2D _placeholder;

    /// <summary>Capa basligi -> ikon kimligi (Capa.Head sirasiyla).</summary>
    static readonly string[] HeadIds = { "kazma", "bicme", "toprak", "sulama", "cekim", "insa", "tirpan_isini", "civi_tabancasi" };

    /// <summary>Kimligin kendi ikonu var mi (yer tutucu sayilmaz).</summary>
    public static bool Has(string id) => Find(id) != null;

    /// <summary>Kimligin ikonu; yoksa null.</summary>
    public static Texture2D Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out var t)) return t;
        var path = Dir + id + ".png";
        t = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        Cache[id] = t;
        return t;
    }

    /// <summary>Kimligin ikonu; yoksa yer tutucu (asla null degil, yer tutucu dosyasi da yoksa 1x1 bos doku).</summary>
    public static Texture2D Get(string id) => Find(id) ?? Placeholder;

    /// <summary>Capa basliginin ikonu.</summary>
    public static Texture2D Head(Capa.Head h) => Get(HeadIds[(int)h]);

    public static Texture2D Placeholder => _placeholder ??=
        (ResourceLoader.Exists(PlaceholderPath) ? ResourceLoader.Load<Texture2D>(PlaceholderPath) : null)
        ?? ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));

    /// <summary>Arayuz icin hazir ikon dugumu (oran korunur, fare olaylarini yutmaz).</summary>
    public static TextureRect Rect(string id, float size, bool placeholder = true)
    {
        var tex = placeholder ? Get(id) : Find(id);
        return new TextureRect
        {
            Texture = tex,
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, // 256 px ikon 32-54 px'te titremesin
        };
    }
}
