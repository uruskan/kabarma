using System;
using System.Collections.Generic;
using Godot;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Cizgi glifleri (ozgun, kodla): Capa basliklari ve pano gostergeleri. Birim karede (-1..1) cizgiler; boyut yarim
/// genisliktir, kalinlik boyun yuzde 7,5'i (Rust'in carkindaki ince cizgi ikonlari gibi).
/// </summary>
public partial class Hud
{
    static readonly Dictionary<string, Vector2[][]> Glyphs = BuildGlyphs();

    static Vector2[] Arc(float cx, float cy, float r, float from, float to, int n = 10)
    {
        var p = new Vector2[n + 1];
        for (int i = 0; i <= n; i++) { float a = from + (to - from) * i / n; p[i] = new Vector2(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r); }
        return p;
    }

    static Dictionary<string, Vector2[][]> BuildGlyphs()
    {
        static Vector2 V(float x, float y) => new(x, y);
        float pi = MathF.PI;
        return new()
        {
            // Kazma: kavisli bas, sap
            ["kazma"] = new[] { Arc(0, 0.35f, 0.95f, -pi * 0.85f, -pi * 0.15f, 12), new[] { V(0, -0.6f), V(0.05f, 0.95f) } },
            // Bicme: orak (hilal) ve sap
            ["bicme"] = new[] { Arc(0.05f, -0.1f, 0.7f, pi * 0.95f, pi * 2.1f, 14), new[] { V(0.62f, 0.25f), V(-0.35f, 0.95f) } },
            // Toprak: sürülmüs uc karik ve filiz
            ["toprak"] = new[] { new[] { V(-0.9f, 0.85f), V(0.9f, 0.85f) }, new[] { V(-0.7f, 0.5f), V(0.7f, 0.5f) }, new[] { V(0, 0.5f), V(0, -0.15f) },
                Arc(-0.32f, -0.15f, 0.32f, -pi * 0.05f, -pi * 0.95f, 8), Arc(0.32f, -0.35f, 0.32f, pi * 1.05f, pi * 1.95f, 8) },
            // Sulama: damla
            ["sulama"] = new[] { Concat(new[] { V(0, -0.95f) }, Arc(0, 0.3f, 0.6f, -pi * 0.15f, pi * 1.15f, 14), new[] { V(0, -0.95f) }) },
            // Cekim: miknatis (U) ve cekim dalgasi
            ["cekim"] = new[] { Concat(new[] { V(-0.6f, -0.8f), V(-0.6f, 0.15f) }, Arc(0, 0.15f, 0.6f, pi, 0, 10), new[] { V(0.6f, -0.8f) }),
                new[] { V(-0.85f, -0.8f), V(-0.35f, -0.8f) }, new[] { V(0.35f, -0.8f), V(0.85f, -0.8f) } },
            // Insa: cati ve duvar
            ["insa"] = new[] { new[] { V(-0.9f, -0.05f), V(0, -0.85f), V(0.9f, -0.05f) }, new[] { V(-0.65f, -0.25f), V(-0.65f, 0.85f), V(0.65f, 0.85f), V(0.65f, -0.25f) },
                new[] { V(-0.2f, 0.85f), V(-0.2f, 0.3f), V(0.2f, 0.3f), V(0.2f, 0.85f) } },
            // Tirpan Isini: namlu ve isin dalgasi
            ["tirpan"] = new[] { new[] { V(-0.95f, 0.2f), V(-0.35f, 0.2f), V(-0.35f, -0.2f), V(-0.95f, -0.2f), V(-0.95f, 0.2f) },
                new[] { V(-0.2f, 0), V(0.05f, -0.35f), V(0.3f, 0.35f), V(0.55f, -0.35f), V(0.8f, 0.35f), V(0.95f, 0) } },
            // Civi Tabancasi: civi (bas, govde, uc)
            ["civi"] = new[] { new[] { V(-0.55f, -0.8f), V(0.55f, -0.8f) }, new[] { V(-0.12f, -0.8f), V(-0.12f, 0.45f), V(0, 0.95f), V(0.12f, 0.45f), V(0.12f, -0.8f) } },
            // Can: arti
            ["health"] = new[] { new[] { V(-0.3f, -0.85f), V(0.3f, -0.85f), V(0.3f, -0.3f), V(0.85f, -0.3f), V(0.85f, 0.3f), V(0.3f, 0.3f), V(0.3f, 0.85f),
                V(-0.3f, 0.85f), V(-0.3f, 0.3f), V(-0.85f, 0.3f), V(-0.85f, -0.3f), V(-0.3f, -0.3f), V(-0.3f, -0.85f) } },
            // Dayaniklilik: simsek
            ["stamina"] = new[] { new[] { V(0.25f, -0.95f), V(-0.55f, 0.1f), V(0.05f, 0.1f), V(-0.25f, 0.95f), V(0.55f, -0.15f), V(-0.05f, -0.15f), V(0.25f, -0.95f) } },
        };
    }

    static Vector2[] Concat(params Vector2[][] parts)
    {
        var l = new List<Vector2>();
        foreach (var p in parts) l.AddRange(p);
        return l.ToArray();
    }

    /// <summary>Capa.Head sirasiyla glif adlari.</summary>
    static readonly string[] HeadGlyphNames = { "kazma", "bicme", "toprak", "sulama", "cekim", "tirpan", "civi" };

    static string HeadGlyphName(int head) => head >= 0 && head < HeadGlyphNames.Length ? HeadGlyphNames[head] : "kazma";

    /// <summary>Bir glifi tasarim noktasinda ortali cizer; size yarim genislik (tasarim pikseli).</summary>
    void Glyph(string name, Vector2 centre, float size, Color c, float? width = null)
    {
        if (!Glyphs.TryGetValue(name, out var strokes)) return;
        float wdt = width ?? Math.Max(1.2f, size * 0.075f * 2f);
        foreach (var s in strokes)
        {
            var pts = new Vector2[s.Length];
            for (int i = 0; i < s.Length; i++) pts[i] = centre + s[i] * size;
            Line(c, wdt, pts);
        }
    }
}
