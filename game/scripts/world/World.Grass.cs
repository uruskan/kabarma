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
        var blade = GrassClumpMesh(Mathf.Max(3, g.Blades));
        float near = g.Spacing / Mathf.Sqrt(dens);
        var a = GrassField.Create(this, blade, near, 0f, p.GrassRadius, p.GrassFar > p.GrassRadius ? 0.55f : 0.35f, 1f, "GrassNear");
        AddChild(a);
        GrassInstances += a.Instances;
        if (p.GrassFar > p.GrassRadius)
        {
            float far = g.FarSpacing / Mathf.Sqrt(dens);
            var b = GrassField.Create(this, blade, far, p.GrassRadius - 2f, p.GrassFar, g.FarKeep, far / near * 0.55f, "GrassFar");
            AddChild(b);
            GrassInstances += b.Instances;
        }
    }

    /// <summary>
    /// Tutam: N egri, sivrilen bicak (1 m boy, 4 bolum). Kose rengi alfa = bicak boyunca 0..1, UV.x = bicak payi.
    /// Renk shader'da (biyom paletinden).
    /// </summary>
    static ArrayMesh GrassClumpMesh(int blades)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var r = new RandomNumberGenerator { Seed = 5 };
        const int segs = 4;
        for (int b = 0; b < blades; b++)
        {
            float a = b * Mathf.Tau / blades + r.RandfRange(-0.4f, 0.4f);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            float face = a + Mathf.Pi * 0.5f + r.RandfRange(-0.6f, 0.6f);
            var side = new Vector3(Mathf.Cos(face), 0, Mathf.Sin(face));
            float lean = r.RandfRange(0.12f, 0.4f), w = r.RandfRange(0.015f, 0.025f), h = r.RandfRange(0.65f, 1f);
            var basePos = dir * r.RandfRange(0.0f, 0.14f);
            float rank = r.Randf();
            Vector3 At(float t) => basePos + dir * lean * t * t + Vector3.Up * h * (t - 0.12f * t * t);
            for (int s = 0; s < segs; s++)
            {
                float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                float w0 = w * (1f - t0 * 0.92f), w1 = w * (1f - t1 * 0.92f);
                var p0 = At(t0); var p1 = At(t1);
                var along = (p1 - p0).Normalized();
                var n = side.Cross(along).Normalized();
                if (n.Y < 0) n = -n;
                // Bicak enine hafif yuvarlak: kenar normalleri disa egik (isik yuzeyde kayar).
                void V(Vector3 p, float t, float edge)
                {
                    st.SetColor(new Color(1, 1, 1, t));
                    st.SetUV(new Vector2(rank, 0));
                    st.SetNormal((n + side * edge * 0.45f).Normalized());
                    st.AddVertex(p);
                }
                V(p0 - side * w0, t0, -1); V(p1 - side * w1, t1, -1); V(p0 + side * w0, t0, 1);
                V(p0 + side * w0, t0, 1); V(p1 - side * w1, t1, -1); V(p1 + side * w1, t1, 1);
            }
        }
        return st.Commit();
    }
}
