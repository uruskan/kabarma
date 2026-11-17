namespace Kabarma.Sim;

// Kredisiz PBR yukseltmesi: materials.json (pipeline/pbr_bake.py yazar). Oyun kurali degil; gorunus kaydi.
// Model yolu -> yuzey (glTF malzeme adi) -> pisirilmis haritalar. Oyun tarafi: game/scripts/fx/MaterialUplift.cs.

public sealed class SurfaceMaps
{
    public string Albedo { get; set; } = "";
    /// <summary>Teget uzayi, OpenGL (+Y).</summary>
    public string Normal { get; set; } = "";
    /// <summary>R ortam kapanmasi, G roughness, B metal.</summary>
    public string Orm { get; set; } = "";
}

public sealed class MaterialUpliftDef
{
    public string Id { get; set; } = "";
    public float NormalScale { get; set; } = 1f;
    /// <summary>glTF malzeme adi -> haritalar. Tek yuzeyli modelde ad tutmasa da o yuzey kullanilir.</summary>
    public Dictionary<string, SurfaceMaps> Surfaces { get; set; } = new();
}

public sealed class MaterialsFile
{
    /// <summary>res:// model yolu -> yukseltme.</summary>
    public Dictionary<string, MaterialUpliftDef> Models { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("materials.json")]
    public MaterialsFile Materials { get; private set; } = new();

    /// <summary>Yalniz materials.json (Assets.Load butun GameData'yi yuklemeden okur).</summary>
    public static MaterialsFile ParseMaterials(string json) => (MaterialsFile)Parse(typeof(MaterialsFile), _ => json, new[] { "materials.json" });
}
