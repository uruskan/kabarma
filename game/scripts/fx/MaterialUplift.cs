using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kredisiz PBR yukseltmesi (gorsel-model): pipeline/pbr_bake.py'nin pisirdigi albedo, normal ve ORM haritalarini
/// modelin ice aktarilmis malzemesine takar (data/materials.json). Malzeme ice aktarmanin paylasilan kaynagidir:
/// bir kez degisir, butun ornekler ve ondan turetilen dekor ruzgar / yaratik sisme malzemeleri de alir.
/// </summary>
public static class MaterialUplift
{
    static MaterialsFile _file;
    static readonly HashSet<ulong> _done = new();
    public static int Applied { get; private set; }

    static MaterialsFile File
    {
        get
        {
            if (_file != null) return _file;
            const string path = "res://data/materials.json";
            _file = FileAccess.FileExists(path) ? GameData.ParseMaterials(FileAccess.GetFileAsString(path)) : new MaterialsFile();
            return _file;
        }
    }

    /// <summary>Kapali mi (--no_uplift: once/sonra karsilastirmasi).</summary>
    public static bool Disabled => Main.Args.ContainsKey("no_uplift");

    public static void Apply(Node3D root, string modelPath)
    {
        if (root == null || Disabled || !File.Models.TryGetValue(modelPath, out var def)) return;
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mi.Mesh == null) continue;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                if (mi.Mesh.SurfaceGetMaterial(i) is not BaseMaterial3D m) continue;
                if (!_done.Add(m.GetInstanceId())) continue;
                if (!def.Surfaces.TryGetValue(m.ResourceName ?? "", out var maps))
                {
                    if (def.Surfaces.Count != 1) { GD.PushWarning($"UPLIFT {def.Id}: '{m.ResourceName}' yuzeyi materials.json'da yok"); continue; }
                    maps = def.Surfaces.Values.First();
                }
                Set(m, maps, def.NormalScale);
                Applied++;
            }
        }
    }

    static void Set(BaseMaterial3D m, SurfaceMaps maps, float normalScale)
    {
        var albedo = Tex(maps.Albedo);
        var normal = Tex(maps.Normal);
        var orm = Tex(maps.Orm);
        if (albedo != null) m.AlbedoTexture = albedo;
        if (normal != null)
        {
            m.NormalEnabled = true;
            m.NormalTexture = normal;
            m.NormalScale = normalScale;
        }
        if (orm != null)
        {
            m.RoughnessTexture = orm;
            m.RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Green;
            m.Roughness = 1f;
            m.MetallicTexture = orm;
            m.MetallicTextureChannel = BaseMaterial3D.TextureChannel.Blue;
            m.Metallic = 1f;
            m.MetallicSpecular = 0.5f;
            m.AOEnabled = true;
            m.AOTexture = orm;
            m.AOTextureChannel = BaseMaterial3D.TextureChannel.Red;
            m.AOLightAffect = 0f;
        }
    }

    static Texture2D Tex(string path) => !string.IsNullOrEmpty(path) && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
}
