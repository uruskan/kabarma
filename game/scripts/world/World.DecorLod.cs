using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dekor LOD'lari ve dokulu bitki malzemesi (bitki-pbr akisi; modeller pipeline/build_bitki.py).
///  - Modelin yanindaki &lt;ad&gt;_lod1.glb ve &lt;ad&gt;_lod2.glb varsa, her 50 m'lik parcaya uzak LOD'lar icin ayri
///    MultiMeshInstance3D eklenir (ayni ornek donusumleri); gorus araligi parca merkezine gore: LOD0 &lt; Lod1Distance,
///    LOD1 &lt; Lod2Distance, sonra LOD2. LOD0 dugum adi degismez ({id}_{cx}_{cz}: TreeChunksAt, Bicme esnemesi).
///  - Biçme ile toplanan tutamlara (harvest) LOD eklenmez: TakeTuft tek MultiMesh'e yazar.
///  - Sema degismez (biomes.json satirlari ayni); LOD dosya adi kuraliyla bulunur.
/// </summary>
public partial class World
{
    public const float Lod1Distance = 48f, Lod2Distance = 115f;

    /// <summary>Ice aktarilan glTF malzemesinden ruzgar malzemesine: alfa makasi, puruzluluk dokusu, yaprak arka isigi.</summary>
    static void PlantPbr(ShaderMaterial m, BaseMaterial3D b, DecorDef def)
    {
        if (b.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor)
        {
            m.SetShaderParameter("alpha_scissor", Mathf.Max(0.05f, b.AlphaScissorThreshold));
            m.SetShaderParameter("backlight_amount", def.Tree ? 0.4f : 0.25f);
            m.SetShaderParameter("volume_normals", def.Tree);
        }
        if (b.RoughnessTexture != null)
        {
            m.SetShaderParameter("roughness_tex", b.RoughnessTexture);
            m.SetShaderParameter("has_roughness_tex", true);
        }
    }

    /// <summary>Uzak LOD parcalari (bkz. sinif ozeti). Kac LOD dugumu eklendigini verir.</summary>
    int AddDecorLods(DecorDef def, Dictionary<int, (DecorChunkRef chunk, int index, Transform3D local)> chunks, float height)
    {
        if (!string.IsNullOrEmpty(def.Harvest) || chunks.Count == 0) return 0;
        var meshes = new List<Mesh>();
        for (int lod = 1; lod <= 2; lod++)
        {
            var path = def.Model.Replace(".glb", $"_lod{lod}.glb");
            if (!ResourceLoader.Exists(path)) break;
            var mesh = LodMesh(path, def, height);
            if (mesh == null) break;
            meshes.Add(mesh);
        }
        if (meshes.Count == 0) return 0;
        float end = def.ViewDistance > 0 ? def.ViewDistance + DecorChunk * 0.7f : 0f;
        var lod0 = GetChildren().OfType<MultiMeshInstance3D>().ToDictionary(n => n.Multimesh, n => n);
        int added = 0;
        foreach (var group in chunks.Values.GroupBy(c => c.chunk))
        {
            var chunk = group.Key;
            if (!lod0.TryGetValue(chunk.Mm, out var near)) continue;
            var items = group.OrderBy(c => c.index).ToList();
            float[] edges = { Lod1Distance, Lod2Distance };
            // LOD0 yakinda kalir; son LOD dekorun kendi gorus mesafesine (yoksa sonsuza) uzanir.
            if (end <= 0 || end > edges[0]) { near.VisibilityRangeEnd = edges[0]; near.VisibilityRangeEndMargin = 4f; }
            for (int i = 0; i < meshes.Count; i++)
            {
                float begin = edges[i];
                if (end > 0 && begin >= end) break;
                float stop = i + 1 < meshes.Count ? edges[i + 1] : end;
                if (end > 0 && stop > end) stop = end;
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = meshes[i], InstanceCount = items.Count };
                for (int k = 0; k < items.Count; k++) mm.SetInstanceTransform(k, items[k].local);
                var mmi = new MultiMeshInstance3D
                {
                    Name = $"{near.Name}_lod{i + 1}",
                    Multimesh = mm,
                    Position = near.Position,
                    CastShadow = near.CastShadow,
                    VisibilityRangeBegin = begin,
                    VisibilityRangeBeginMargin = 4f,
                    VisibilityRangeEnd = stop,
                    VisibilityRangeEndMargin = stop > 0 ? 4f : 0f,
                };
                AddChild(mmi);
                added++;
            }
        }
        return added;
    }

    Mesh LodMesh(string path, DecorDef def, float height)
    {
        var node = Assets.Load(path);
        var src = Assets.FirstMesh(node);
        node?.Free();
        if (src == null || def.Wind <= 0 || src is not ArrayMesh am) return src;
        var mesh = (ArrayMesh)am.Duplicate();
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
            mesh.SurfaceSetMaterial(i, WindMaterial(mesh.SurfaceGetMaterial(i), def, height));
        return mesh;
    }
}
