using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>Dunya: agaclar, calilar, ot tutamlari ve kazilacak kayalar. Hepsi ayni tohumlu _rng'den (sira onemli: once dekor, sonra kaya).</summary>
public partial class World
{
    readonly List<Rock> _rocks = new();
    public readonly List<Vector3> TreePositions = new();
    public IReadOnlyList<Rock> Rocks => _rocks;
    readonly RandomNumberGenerator _rng = new() { Seed = 4417 };

    void BuildDecor()
    {
        // Agaclar: tiknaz govde + yuvarlak tac kumeleri. Ussun 22 m icine konmaz.
        var trees = new MeshKit();
        var bushes = new MeshKit();
        // M0 denemesi #1: Tripo'dan dokulu cayir agaci (6.788 ucgen, 1K doku). Varsa butun agaclar ondan, tek MultiMesh.
        var treeNode = Assets.Load("res://assets/models/agac/agac.glb");
        var treeMesh = Assets.FirstMesh(treeNode);
        treeNode?.Free(); // yalnizca mesh'i lazim; sahneye girmeyen dugum serbest birakilir
        var treeXf = new System.Collections.Generic.List<Transform3D>();
        for (int i = 0; i < 70; i++)
        {
            var p = new Vector3(_rng.RandfRange(-75, 75), 0, _rng.RandfRange(-75, 75));
            if (p.Length() < 24f || new Vector2(p.X - 9f, p.Z + 9f).Length() < 10f) continue;
            p = Ground(p);
            if (_rng.Randf() < 0.45f)
            {
                float s = _rng.RandfRange(0.8f, 1.5f);
                if (treeMesh != null)
                {
                    // Model 4,2 m; cesitlilik icin boy ve yon rastgele.
                    treeXf.Add(new Transform3D(Basis.FromEuler(new Vector3(0, _rng.Randf() * Mathf.Tau, 0)).Scaled(Vector3.One * s * 0.95f), p + Vector3.Down * 0.1f));
                    continue;
                }
                trees.Cylinder(0.28f * s, 0.42f * s, 2.4f * s, MeshKit.At(p + new Vector3(0, 1.1f * s, 0)), Palette.Wood.Lerp(Palette.Shadow, 0.25f), 8);
                var leaf = Palette.Plant.Lerp(Palette.Accent, _rng.Randf() < 0.25f ? 0.45f : 0f);
                trees.Sphere(1.6f * s, MeshKit.At(p + new Vector3(0, 3.2f * s, 0), Vector3.Zero, new Vector3(1, 0.85f, 1)), leaf, 10, 6);
                trees.Sphere(1.1f * s, MeshKit.At(p + new Vector3(0.9f * s, 2.7f * s, 0.3f * s)), leaf.Lerp(Palette.Shadow, 0.12f), 9, 5);
                trees.Sphere(1.0f * s, MeshKit.At(p + new Vector3(-0.8f * s, 2.8f * s, -0.4f * s)), leaf.Lerp(Palette.Fog, 0.1f), 9, 5);
            }
            else
            {
                float s = _rng.RandfRange(0.5f, 1.0f);
                var c = Palette.Plant.Lerp(Palette.Ground, _rng.Randf() * 0.4f);
                bushes.Sphere(0.9f * s, MeshKit.At(p + new Vector3(0, 0.4f * s, 0), Vector3.Zero, new Vector3(1.3f, 0.8f, 1.1f)), c, 9, 5);
                if (_rng.Randf() < 0.4f)
                    bushes.Sphere(0.22f * s, MeshKit.At(p + new Vector3(0.3f, 1.0f * s, 0.2f)), Palette.Accent, 6, 4);
            }
        }
        if (treeMesh != null && treeXf.Count > 0)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = treeMesh, InstanceCount = treeXf.Count };
            for (int i = 0; i < treeXf.Count; i++) mm.SetInstanceTransform(i, treeXf[i]);
            AddChild(new MultiMeshInstance3D { Multimesh = mm, Name = "Trees" });
            foreach (var xf in treeXf)
            {
                TreePositions.Add(xf.Origin);
                var body = new StaticBody3D();
                body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.45f * xf.Basis.Scale.X, Height = 3f * xf.Basis.Scale.Y }, Position = new Vector3(0, 1.5f * xf.Basis.Scale.Y, 0) });
                AddChild(body);
                body.GlobalPosition = xf.Origin;
            }
        }
        else AddChild(trees.Instance());
        var b = bushes.Instance();
        b.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(b);

        // Ot tutamlari: ussun cevresinde yogun.
        var grass = new MeshKit();
        for (int i = 0; i < 900; i++)
        {
            float a = _rng.Randf() * Mathf.Tau, d = Mathf.Sqrt(_rng.Randf()) * 60f;
            var p = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            if (new Vector2(p.X - 9f, p.Z + 9f).Length() < 6f || p.Length() < 4f) continue;
            p = Ground(p);
            var c = Palette.Plant.Lerp(Palette.Ground, _rng.Randf() * 0.5f);
            for (int k = 0; k < 3; k++)
            {
                var off = new Vector3(_rng.RandfRange(-0.15f, 0.15f), 0.22f, _rng.RandfRange(-0.15f, 0.15f));
                grass.Cylinder(0.0f, 0.07f, 0.45f, MeshKit.At(p + off, new Vector3(_rng.RandfRange(-0.3f, 0.3f), 0, _rng.RandfRange(-0.3f, 0.3f))), c, 4);
            }
        }
        var g = grass.Instance();
        g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(g);
    }

    void BuildRocks()
    {
        // Kazilacak kayalar ussun cevresinde, ilk dakikada bulunacak kadar yakin.
        var spots = new[] { new Vector3(-7, 0, 6), new Vector3(-10, 0, 1), new Vector3(5, 0, 11), new Vector3(12, 0, 6), new Vector3(-4, 0, 14), new Vector3(16, 0, -4), new Vector3(-15, 0, -8), new Vector3(2, 0, -16), new Vector3(-13, 0, 10), new Vector3(9, 0, 15), new Vector3(-18, 0, 2), new Vector3(18, 0, 9) };
        int i = 0;
        foreach (var s in spots)
        {
            var rock = new Rock(_rng.RandiRange(4, 6), _rng.RandfRange(0.9f, 1.3f), (ulong)(1000 + i++));
            AddChild(rock);
            rock.GlobalPosition = Ground(s) + Vector3.Down * 0.2f;
            _rocks.Add(rock);
        }
    }

    public void RemoveRock(Rock r) => _rocks.Remove(r);
}
