using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Uretim hattindan (pipeline/clean_asset.py) gecen modelleri oyuna alir: hepsi ayni toon malzemeyi kullanir.</summary>
public static class Assets
{
    static ShaderMaterial _pipelineStatic;

    public static ShaderMaterial PipelineMaterial
    {
        get
        {
            if (_pipelineStatic != null) return _pipelineStatic;
            _pipelineStatic = new ShaderMaterial { Shader = MeshKit.ToonShader };
            _pipelineStatic.SetShaderParameter("vertex_linear", true);
            return _pipelineStatic;
        }
    }

    public static ShaderMaterial NewLivePipelineMaterial()
    {
        var m = new ShaderMaterial { Shader = MeshKit.ToonShader };
        m.SetShaderParameter("vertex_linear", true);
        return m;
    }

    /// <summary>
    /// Modeli yukler; yoksa null (cagiran koddan kurulan yedege duser).
    /// Varsayilan: dokulu modelin kendi malzemeleri korunur (sanat yonu: dokulu, orta poligon).
    /// paletted=true: eski palet/kose rengi hattindan gelen modeller icin toon malzemesi.
    /// </summary>
    public static Node3D Load(string path, bool paletted = false, Material material = null)
    {
        if (!ResourceLoader.Exists(path)) return null;
        var inst = GD.Load<PackedScene>(path)?.Instantiate<Node3D>();
        if (inst == null) return null;
        MaterialUplift.Apply(inst, path); // gorsel-model: pisirilmis PBR haritalari (data/materials.json)
        if (paletted || material != null)
            foreach (var mi in inst.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                mi.MaterialOverride = material ?? PipelineMaterial;
        return inst;
    }

    public static Mesh FirstMesh(Node3D n) =>
        n?.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault()?.Mesh;
}
