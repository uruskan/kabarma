using System.Text.RegularExpressions;

namespace Kabarma.Sim;

/// <summary>
/// Metindeki tus yer tutucusu: <c>{act:eylem}</c> (eylem kimligi input.json'da; ornek "Tezgâh'ta {act:interact} ile ac").
/// Ekranda son kullanilan aygitin glifine doner (Godot: Controls.Expand). Seslendirilen replik tus adi icermez
/// (SANAT-SES §10.3): ses ve ses eslestirmesi Strip ile yer tutucusuz metni kullanir.
/// </summary>
public static partial class InputText
{
    [GeneratedRegex(@"\{act:([a-z0-9_]+)\}")]
    private static partial Regex Token();

    /// <summary>Her <c>{act:x}</c> yerine glyph(x) koyar. Bilinmeyen eylem icin glyph ne donerse o yazilir.</summary>
    public static string Expand(string text, Func<string, string> glyph)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{act:")) return text;
        return Token().Replace(text, m => glyph(m.Groups[1].Value));
    }

    /// <summary>Yer tutuculari siler ve kalan bosluk, bos parantez ve noktalama artiklarini toplar (ses, altyazi).</summary>
    public static string Strip(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{act:")) return text;
        var s = Token().Replace(text, "");
        s = Regex.Replace(s, @"\(\s*[:/+·,–-]?\s*\)", "");
        s = Regex.Replace(s, @"\s+([.,;:!?])", "$1");
        s = Regex.Replace(s, @"(^|[\s(])[:/+·–-]\s+", "$1");
        s = Regex.Replace(s, @"\s{2,}", " ");
        return s.Trim();
    }

    /// <summary>Metindeki eylem kimlikleri (veri denetimi icin).</summary>
    public static IEnumerable<string> Actions(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (Match m in Token().Matches(text)) yield return m.Groups[1].Value;
    }

    /// <summary>Hatali yazilmis yer tutucu (kapanmamis, bos, buyuk harf): "{act:" ile baslayip Token'a uymayanlar.</summary>
    public static IEnumerable<string> Malformed(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        int i = 0;
        while ((i = text.IndexOf("{act:", i, StringComparison.Ordinal)) >= 0)
        {
            var m = Token().Match(text, i);
            if (!m.Success || m.Index != i) yield return text.Substring(i, Math.Min(24, text.Length - i));
            i += 5;
        }
    }
}
