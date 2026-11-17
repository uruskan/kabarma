using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Koddan dusuk poligon varlik kiti: ilkel parcalar kose rengiyle tek mesh'te birlesir.
/// Yastik kutu (pahli, tiknaz) SANAT-SES §1'in "buyuk, yumusak, tiknaz" kuralini karsilar.
/// </summary>
public sealed class MeshKit
{
    readonly List<Vector3> _v = new();
    readonly List<Vector3> _n = new();
    readonly List<Color> _c = new();
    readonly List<int> _i = new();

    static Shader _shader;
    static ShaderMaterial _static;

    public static Shader ToonShader => _shader ??= GD.Load<Shader>("res://shaders/toon_vc.gdshader");

    /// <summary>Kabarmayan her sey bu ortak malzemeyi kullanir.</summary>
    public static ShaderMaterial StaticMaterial
    {
        get
        {
            if (_static != null) return _static;
            _static = new ShaderMaterial { Shader = ToonShader };
            return _static;
        }
    }

    /// <summary>Kabarabilen, isabet parlamasi alan nesneler icin ayri malzeme.</summary>
    public static ShaderMaterial NewLiveMaterial() => new() { Shader = ToonShader };

    public MeshKit Add(PrimitiveMesh prim, Transform3D xf, Color color, float pillow = 0f)
    {
        var arrays = prim.GetMeshArrays();
        var verts = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
        var norms = (Vector3[])arrays[(int)Mesh.ArrayType.Normal];
        var idx = (int[])arrays[(int)Mesh.ArrayType.Index];
        int baseIndex = _v.Count;
        var basis = xf.Basis;
        var nBasis = basis.Inverse().Transposed();
        for (int k = 0; k < verts.Length; k++)
        {
            _v.Add(xf * verts[k]);
            _n.Add((nBasis * norms[k]).Normalized());
            _c.Add(color);
        }
        foreach (var i in idx) _i.Add(baseIndex + i);
        return this;
    }

    /// <summary>Yastik kutu: kureyi kutuya yaklastirir; koseler yuvarlak kalir.</summary>
    public MeshKit Pillow(Vector3 size, Transform3D xf, Color color, float roundness = 0.35f, int radial = 16, int rings = 8)
    {
        var s = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = radial, Rings = rings };
        var arrays = s.GetMeshArrays();
        var verts = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
        var idx = (int[])arrays[(int)Mesh.ArrayType.Index];
        float e = Mathf.Clamp(roundness, 0.05f, 1f);
        var pts = new Vector3[verts.Length];
        for (int k = 0; k < verts.Length; k++)
        {
            var v = verts[k];
            var p = new Vector3(Mathf.Sign(v.X) * Mathf.Pow(Mathf.Abs(v.X), e), Mathf.Sign(v.Y) * Mathf.Pow(Mathf.Abs(v.Y), e), Mathf.Sign(v.Z) * Mathf.Pow(Mathf.Abs(v.Z), e));
            pts[k] = p * size * 0.5f;
        }
        // Normalleri yeniden hesapla (yumusak golgeleme).
        var nrm = new Vector3[pts.Length];
        for (int t = 0; t < idx.Length; t += 3)
        {
            var a = pts[idx[t]]; var b = pts[idx[t + 1]]; var c = pts[idx[t + 2]];
            var fn = (b - a).Cross(c - a);
            nrm[idx[t]] += fn; nrm[idx[t + 1]] += fn; nrm[idx[t + 2]] += fn;
        }
        // Godot kuresinin dikis kosesi ayni konumda iki kez var: konuma gore birlestir.
        var byPos = new Dictionary<Vector3I, Vector3>();
        for (int k = 0; k < pts.Length; k++)
        {
            var key = (Vector3I)(pts[k] * 1000f).Round();
            byPos[key] = byPos.TryGetValue(key, out var acc) ? acc + nrm[k] : nrm[k];
        }
        int baseIndex = _v.Count;
        var nBasis = xf.Basis.Inverse().Transposed();
        for (int k = 0; k < pts.Length; k++)
        {
            var key = (Vector3I)(pts[k] * 1000f).Round();
            var n = byPos[key];
            // Godot'un SphereMesh sarimi ters yuzlu cikiyor: normali disa cevir.
            if (n.Dot(pts[k]) < 0) n = -n;
            _v.Add(xf * pts[k]);
            _n.Add((nBasis * n).Normalized());
            _c.Add(color);
        }
        foreach (var i in idx) _i.Add(baseIndex + i);
        return this;
    }

    public MeshKit Sphere(float radius, Transform3D xf, Color color, int radial = 12, int rings = 6) =>
        Add(new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = radial, Rings = rings }, xf, color);

    public MeshKit Cylinder(float rTop, float rBottom, float h, Transform3D xf, Color color, int radial = 10) =>
        Add(new CylinderMesh { TopRadius = rTop, BottomRadius = rBottom, Height = h, RadialSegments = radial, Rings = 1 }, xf, color);

    public MeshKit Capsule(float r, float h, Transform3D xf, Color color, int radial = 12, int rings = 4) =>
        Add(new CapsuleMesh { Radius = r, Height = h, RadialSegments = radial, Rings = rings }, xf, color);

    public ArrayMesh Build()
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _v.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _n.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _c.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = _i.ToArray();
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return m;
    }

    public MeshInstance3D Instance(Material mat = null)
    {
        var mi = new MeshInstance3D { Mesh = Build() };
        mi.MaterialOverride = mat ?? StaticMaterial;
        return mi;
    }

    public static Transform3D At(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));
    public static Transform3D At(Vector3 p) => new(Basis.Identity, p);
    public static Transform3D At(Vector3 p, Vector3 euler, Vector3? scale = null) =>
        new(Basis.FromEuler(euler).Scaled(scale ?? Vector3.One), p);
}
