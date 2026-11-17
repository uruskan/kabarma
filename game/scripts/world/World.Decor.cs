using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: bitki ortusu ve sus kayalari (URETIM 6 M1: 8 bitki, 4 kaya). Her tur data/biomes.json'daki "decor" satiri:
/// model, adet, olcek, yerlestirme kurali. Ornekler 50 m'lik parcalarda MultiMesh olarak cizilir (TEKNIK §10).
/// Bicme verimi olan turler (ot, cali: Lif; yabani bugday: Bugday) ayni zamanda tutamdir (World.Resources.cs).
/// Kendi tohumlu RNG'si var: dekor sirasini degistirmek kayalari ve tutamlari etkilemez.
/// </summary>
public partial class World
{
    public readonly List<Vector3> TreePositions = new();
    const float DecorChunk = 50f;

    /// <summary>Yerlestirilen ornek (sinama ve goruntu araci okur).</summary>
    public readonly Dictionary<string, int> DecorCounts = new();

    void BuildDecor()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(T.Seed + 11) };
        float density = Preset.PlantDensity;
        foreach (var def in Biome.Decor)
        {
            var mesh = DecorMesh(def, out float modelHeight);
            if (mesh == null) { GD.PrintErr($"WORLD decor model yok: {def.Model}"); continue; }
            var placed = new List<Transform3D>();
            foreach (var patch in Biome.Patches.Where(p => p.Decor == def.Id))
                for (int i = 0; i < patch.Count; i++)
                {
                    for (int tries = 0; tries < 20; tries++)
                    {
                        float a = rng.Randf() * Mathf.Tau, d = Mathf.Sqrt(rng.Randf()) * patch.Radius;
                        var p = new Vector3(patch.Center[0] + Mathf.Cos(a) * d, 0, patch.Center[1] + Mathf.Sin(a) * d);
                        if (ShoreDistance(p.X, p.Z) < def.LakeClear) continue;
                        placed.Add(DecorTransform(def, p, rng));
                        break;
                    }
                }
            int want = Mathf.RoundToInt(def.Count * density);
            float half = Size / 2f - 4f;
            for (int tries = 0; tries < want * 12 && placed.Count < want + PatchCount(def.Id); tries++)
            {
                var p = new Vector3(rng.RandfRange(-half, half), 0, rng.RandfRange(-half, half));
                float dist = new Vector2(p.X, p.Z).Length();
                if (dist < def.MinDistance || dist > def.MaxDistance || InClearing(p)) continue;
                float shore = ShoreDistance(p.X, p.Z);
                if (shore < def.LakeClear) continue;
                if (SlopeAt(p.X, p.Z) > (def.Tree ? 22f : 30f)) continue;
                placed.Add(DecorTransform(def, p, rng));
            }
            DecorCounts[def.Id] = placed.Count;
            var chunks = AddChunkedInstances(def.Id, mesh, placed, def.Shadow, def.ViewDistance);
            if (def.Tree) TreePositions.AddRange(placed.Select(x => x.Origin));
            if (def.CollideRadius > 0)
                foreach (var xf in placed)
                {
                    float s = xf.Basis.Scale.Y;
                    var body = new StaticBody3D { Name = def.Id + "Body" };
                    body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = def.CollideRadius * xf.Basis.Scale.X, Height = def.CollideHeight * s }, Position = new Vector3(0, def.CollideHeight * s * 0.5f, 0) });
                    AddChild(body);
                    body.GlobalPosition = xf.Origin;
                }
            if (!string.IsNullOrEmpty(def.Harvest)) AddTufts(def, chunks);
        }
    }

    int PatchCount(string id) => Biome.Patches.Where(p => p.Decor == id).Sum(p => p.Count);

    /// <summary>Bu noktadaki agac turlerinin MultiMesh parcalari (Capa.Bicme agac esnemesi, capa akisi; entegrasyon).</summary>
    public IEnumerable<MultiMeshInstance3D> TreeChunksAt(Vector3 p) => Biome.Decor.Where(d => d.Tree)
        .Select(d => GetNodeOrNull<MultiMeshInstance3D>($"{d.Id}_{Mathf.FloorToInt(p.X / DecorChunk)}_{Mathf.FloorToInt(p.Z / DecorChunk)}"))
        .Where(m => m != null);

    public bool InClearing(Vector3 p) =>
        Biomes.World.Clearings.Any(c => c.Count >= 3 && new Vector2(p.X - c[0], p.Z - c[1]).Length() < c[2]);

    Transform3D DecorTransform(DecorDef def, Vector3 p, RandomNumberGenerator rng)
    {
        float s = rng.RandfRange(def.Scale[0], def.Scale.Count > 1 ? def.Scale[1] : def.Scale[0]);
        var basis = Basis.FromEuler(new Vector3(0, rng.Randf() * Mathf.Tau, 0)).Scaled(Vector3.One * s);
        return new Transform3D(basis, Ground(p) + Vector3.Down * def.Sink * s);
    }

    /// <summary>Modelin mesh'i; ruzgarli turlerde her yuzeyin malzemesi dokuyu koruyan ruzgar malzemesine doner.</summary>
    Mesh DecorMesh(DecorDef def, out float height)
    {
        height = 1f;
        var node = Assets.Load(def.Model);
        var src = Assets.FirstMesh(node);
        node?.Free();
        if (src == null) return null;
        height = Mathf.Max(0.05f, src.GetAabb().End.Y);
        if (def.Wind <= 0 || src is not ArrayMesh am) return src;
        var mesh = (ArrayMesh)am.Duplicate();
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
            mesh.SurfaceSetMaterial(i, WindMaterial(mesh.SurfaceGetMaterial(i), def, height));
        return mesh;
    }

    Shader _plantShader;

    ShaderMaterial WindMaterial(Material from, DecorDef def, float height)
    {
        _plantShader ??= GD.Load<Shader>("res://shaders/plant_wind.gdshader");
        var m = new ShaderMaterial { Shader = _plantShader };
        if (from is BaseMaterial3D b)
        {
            m.SetShaderParameter("albedo_color", b.AlbedoColor);
            if (b.AlbedoTexture != null) { m.SetShaderParameter("albedo_tex", b.AlbedoTexture); m.SetShaderParameter("has_albedo_tex", true); }
            if (b.NormalEnabled && b.NormalTexture != null) { m.SetShaderParameter("normal_tex", b.NormalTexture); m.SetShaderParameter("has_normal_tex", true); }
            m.SetShaderParameter("roughness_value", Mathf.Max(0.6f, b.Roughness));
        }
        m.SetShaderParameter("wind_strength", def.Wind);
        m.SetShaderParameter("model_height", height);
        m.SetShaderParameter("bend_start", def.Tree ? 0.45f : 0f);
        return m;
    }

    /// <summary>Bir parca (chunk) icindeki ornekler: MultiMesh, ornegin yerel donusumu.</summary>
    public sealed class DecorChunkRef
    {
        public MultiMesh Mm;
        public Vector3 Origin;
        public readonly List<int> Global = new();
    }

    /// <summary>Ornekleri 50 m'lik parcalara boler; her parca bir MultiMeshInstance3D (gorus mesafesiyle elenir).</summary>
    /// <remarks>Basliksiz calismada MultiMesh ornek donusumleri okunamaz (sahte cizici); yerel donusum burada tutulur.</remarks>
    Dictionary<int, (DecorChunkRef chunk, int index, Transform3D local)> AddChunkedInstances(string name, Mesh mesh, List<Transform3D> xfs, bool shadow, float viewDistance)
    {
        var map = new Dictionary<int, (DecorChunkRef, int, Transform3D)>();
        var groups = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < xfs.Count; i++)
        {
            var key = (Mathf.FloorToInt(xfs[i].Origin.X / DecorChunk), Mathf.FloorToInt(xfs[i].Origin.Z / DecorChunk));
            if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<int>();
            l.Add(i);
        }
        foreach (var ((cx, cz), list) in groups)
        {
            var origin = new Vector3((cx + 0.5f) * DecorChunk, 0, (cz + 0.5f) * DecorChunk);
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = list.Count };
            var chunk = new DecorChunkRef { Mm = mm, Origin = origin };
            for (int k = 0; k < list.Count; k++)
            {
                var xf = xfs[list[k]];
                var local = new Transform3D(xf.Basis, xf.Origin - origin);
                mm.SetInstanceTransform(k, local);
                chunk.Global.Add(list[k]);
                map[list[k]] = (chunk, k, local);
            }
            var mmi = new MultiMeshInstance3D
            {
                Name = $"{name}_{cx}_{cz}",
                Multimesh = mm,
                Position = origin,
                CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            };
            if (viewDistance > 0) { mmi.VisibilityRangeEnd = viewDistance + DecorChunk * 0.7f; mmi.VisibilityRangeEndMargin = 5f; }
            AddChild(mmi);
        }
        return map;
    }
}
