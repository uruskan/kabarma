namespace Kabarma.Game;

/// <summary>
/// Pano, tus kapakcigi metni: girdi akisinin glifinden (Controls.Glyph; input.json). Klavyede "[E]" koseli ayraci
/// kapakcik cizildigi icin atilir. Controls yoksa (eski sinamalar) verilen yedek yazilir.
/// </summary>
public partial class Hud
{
    /// <summary>Son kullanilan aygit oyun kolu mu (Controls yoksa fare/klavye olaylarindan).</summary>
    bool PadDevice => _d?.Controls != null ? _d.Controls.Last == InputDevice.Pad : _padLast;

    /// <summary>Eylemin tus kapakcigi metni (ornek "Q", "LB").</summary>
    string KeyText(string action, string fallback)
    {
        var c = _d?.Controls;
        if (c == null) return fallback;
        string g = c.Glyph(action).Trim();
        if (g.Length > 2 && g[0] == '[' && g[^1] == ']') g = g[1..^1];
        return string.IsNullOrEmpty(g) || g.EndsWith("?") ? fallback : g;
    }
}
