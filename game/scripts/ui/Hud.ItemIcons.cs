using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>Pano: esya ikonlarinin kemere ve menulere en kucuk baglanmasi (ItemIcons, SANAT-SES 8.1).</summary>
public partial class Hud
{
    readonly List<TextureRect> _beltIcon = new();

    /// <summary>Kemer yuvasinin ikonu: yazinin arkasinda, esya bitince soluk.</summary>
    void SetBeltIcon(int i, string id, int count)
    {
        while (_beltIcon.Count <= i) _beltIcon.Add(null);
        var r = _beltIcon[i];
        if (r == null)
        {
            r = ItemIcons.Rect(null, 54, false);
            _beltSlots[i].AddChild(r);
            _beltSlots[i].MoveChild(r, 0);
            _beltIcon[i] = r;
        }
        r.Texture = ItemIcons.Find(id);
        r.Visible = r.Texture != null;
        r.Modulate = new Color(1, 1, 1, count > 0 ? 0.95f : 0.35f);
    }

    /// <summary>Menu satirina ikon: satirin soluna (Label'in sol payi ikona yer acar).</summary>
    static void AddMenuIcon(Label l, string id)
    {
        var tex = ItemIcons.Find(id);
        if (tex == null) return;
        var sb = new StyleBoxEmpty { ContentMarginLeft = 38 };
        l.AddThemeStyleboxOverride("normal", sb);
        var r = ItemIcons.Rect(id, 32);
        r.Position = new Vector2(0, -2);
        r.Size = new Vector2(32, 32);
        l.AddChild(r);
    }
}
