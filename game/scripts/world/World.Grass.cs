using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: ot (gorsel-dunya). Kameraya bagli iki halka (GrassField): yakin halka sik tutamli (biomes.json grass.spacing,
/// on ayarin grassRadius'una kadar), uzak halka seyrek ve genis tutamli (farSpacing, grassFar'a kadar). Ot olur mu,
/// boy ve renk shader'da arazi dokularindan (patika, kum, tarla, aciklik, su, dik egim). On ayarin grassDensity'si
/// araligi daraltir ya da genisletir. Ot Bicme kaynagi degildir (Lif veren ot kumeleri World.Decor.cs'deki "ot" modelidir).
/// </summary>
public partial class World
{
    public int GrassInstances { get; private set; }

    void BuildGrass()
    {
        var g = Biome.Grass;
        var p = Preset;
        if (p.GrassRadius <= 0 || g.Spacing <= 0) return;
        float dens = Mathf.Max(0.1f, p.GrassDensity);
        var blade = GrassClumpMesh(Mathf.Max(3, g.Blades), 4, 1f);
        float near = g.Spacing / Mathf.Sqrt(dens);
        var a = GrassField.Create(this, blade, near, 0f, p.GrassRadius, p.GrassFar > p.GrassRadius ? 0.55f : 0.35f, 1f, "GrassNear");
        AddChild(a);
        GrassInstances += a.Instances;
        if (p.GrassFar > p.GrassRadius)
        {
            // Uzak halka: az bicakli, iki bolumlu, genis bicakli tutam (kose sayisi ~1/5).
            float far = g.FarSpacing / Mathf.Sqrt(dens);
            var lod = GrassClumpMesh(Mathf.Max(3, g.Blades / 2), 2, 1.7f);
            var b = GrassField.Create(this, lod, far, p.GrassRadius - 2f, p.GrassFar, g.FarKeep, far / near * 0.45f, "GrassFar");
            AddChild(b);
            GrassInstances += b.Instances;
        }
    }

    /// <summary>
    /// Tutam: N egri, sivrilen bicak (1 m boy, segs bolum), indeksli (bicak basina 2 x (segs + 1) kose). Kose rengi
    /// alfa = bicak boyunca 0..1, UV.x = bicak payi. Renk shader'da (biyom paletinden). widthMul: uzak halkada genis.
    /// </summary>
    static ArrayMesh GrassClumpMesh(int blades, int segs, float widthMul)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var cols = new List<Color>(); var uvs = new List<Vector2>();
        var idx = new List<int>();
        var r = new RandomNumberGenerator { Seed = 5 };
        for (int b = 0; b < blades; b++)
        {
            float a = b * Mathf.Tau / blades + r.RandfRange(-0.4f, 0.4f);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            float face = a + Mathf.Pi * 0.5f + r.RandfRange(-0.6f, 0.6f);
            var side = new Vector3(Mathf.Cos(face), 0, Mathf.Sin(face));
            float lean = r.RandfRange(0.12f, 0.4f), w = r.RandfRange(0.015f, 0.025f) * widthMul, h = r.RandfRange(0.65f, 1f);
            var basePos = dir * r.RandfRange(0.0f, 0.14f);
            float rank = r.Randf();
            Vector3 At(float t) => basePos + dir * lean * t * t + Vector3.Up * h * (t - 0.12f * t * t);
            int first = verts.Count;
            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs;
                float wt = w * (1f - t * 0.92f);
                var p = At(t);
                var along = (At(Mathf.Min(t + 0.05f, 1f)) - At(Mathf.Max(t - 0.05f, 0f))).Normalized();
                var n = side.Cross(along).Normalized();
                if (n.Y < 0) n = -n;
                // Bicak enine hafif yuvarlak: kenar normalleri disa egik (isik yuzeyde kayar).
                for (int e = -1; e <= 1; e += 2)
                {
                    verts.Add(p + side * wt * e);
                    norms.Add((n + side * e * 0.45f).Normalized());
                    cols.Add(new Color(1, 1, 1, t));
                    uvs.Add(new Vector2(rank, 0));
                }
            }
            for (int s = 0; s < segs; s++)
            {
                int l0 = first + s * 2, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
                idx.Add(l0); idx.Add(l1); idx.Add(r0);
                idx.Add(r0); idx.Add(l1); idx.Add(r1);
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = norms.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = cols.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}