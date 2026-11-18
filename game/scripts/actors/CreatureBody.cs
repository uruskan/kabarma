using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Koddan yaratik govdesi (bodies.json; URETIM §2.2 E yedegi): kemik adlari govde planinin rolleri olan bir Skeleton3D
/// ve kemiklere kati baglanmis (agirlik 1) parcalardan tek bir deri giydirilmis ArrayMesh. Her renk bir yuzey ve kendi
/// StandardMaterial3D'si; boylece Swellable ortak sisme shader'ina cevirir, PoseLibrary klipleri dogrudan oynar.
/// Model yolu "code:&lt;kimlik&gt;" olan turler CreatureRig.Load uzerinden buraya gelir. Durum tutmaz.
/// </summary>
public static class CreatureBody
{
    public const string Prefix = "code:";

    public static bool IsCode(string modelPath) => modelPath != null && modelPath.StartsWith(Prefix);

    static Vector3 V(List<float> l, Vector3 fallback = default) => l is { Count: >= 3 } ? new Vector3(l[0], l[1], l[2]) : fallback;

    static string Mirror(string name) => name.EndsWith("_l") ? name[..^2] + "_r" : name;

    public static Color ColorOf(BodyDef body, string c)
    {
        if (string.IsNullOrEmpty(c)) return Palette.OffWhite;
        if (body.Colors.TryGetValue(c, out var hex)) c = hex;
        return Color.FromHtml(c);
    }

    /// <summary>_l kemiklerinin sag eslerini ekler (veride yazilmamissa); ebeveynler once gelir.</summary>
    public static List<BodyBoneDef> Bones(BodyDef body)
    {
        var list = new List<BodyBoneDef>(body.Bones);
        var names = new HashSet<string>(list.Select(b => b.Name));
        foreach (var b in body.Bones.Where(b => b.Name.EndsWith("_l")))
        {
            var r = Mirror(b.Name);
            if (names.Contains(r)) continue;
            var p = V(b.Pos);
            list.Add(new BodyBoneDef { Name = r, Parent = Mirror(b.Parent), Pos = new List<float> { -p.X, p.Y, p.Z } });
            names.Add(r);
        }
        // Ebeveyn sirasi
        var ordered = new List<BodyBoneDef>();
        var done = new HashSet<string>();
        while (ordered.Count < list.Count)
        {
            int before = ordered.Count;
            foreach (var b in list)
                if (!done.Contains(b.Name) && (string.IsNullOrEmpty(b.Parent) || done.Contains(b.Parent))) { ordered.Add(b); done.Add(b.Name); }
            if (ordered.Count == before) { GD.PrintErr($"BODY ebeveyni bulunmayan kemik: {string.Join(",", list.Where(b => !done.Contains(b.Name)).Select(b => b.Name + "<-" + b.Parent))}"); break; }
        }
        return ordered;
    }

    /// <summary>Aynalanmis parcalar dahil butun parcalar.</summary>
    public static IEnumerable<BodyPartDef> Parts(BodyDef body)
    {
        foreach (var p in body.Parts)
        {
            yield return p;
            if (!p.Mirror) continue;
            var pos = V(p.Pos);
            var rot = V(p.Rot);
            yield return new BodyPartDef
            {
                Bone = Mirror(p.Bone), Shape = p.Shape, Size = p.Size, Color = p.Color, Round = p.Round,
                Pos = new List<float> { -pos.X, pos.Y, pos.Z }, Rot = new List<float> { rot.X, -rot.Y, -rot.Z },
            };
        }
    }

    /// <summary>Parcanin mesh dizileri (model uzayinda): MeshKit ile kurulur.</summary>
    public static Godot.Collections.Array PartArrays(BodyPartDef p)
    {
        var size = V(p.Size, new Vector3(0.2f, 0.2f, 0.2f));
        var rot = V(p.Rot);
        var xf = new Transform3D(Basis.FromEuler(new Vector3(Mathf.DegToRad(rot.X), Mathf.DegToRad(rot.Y), Mathf.DegToRad(rot.Z)), EulerOrder.Yxz), V(p.Pos));
        var kit = new MeshKit();
        // Kucuk parca (goz, tirnak) az bolumlu: ucgen butcesi (SANAT-SES §4) govdeye harcanir.
        float big = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        int seg = big < 0.12f ? 8 : big < 0.3f ? 12 : 14, ring = seg / 2;
        switch (p.Shape)
        {
            case "pillow":
                kit.Pillow(size, xf, Colors.White, p.Round, seg, ring);
                break;
            case "capsule":
                {
                    float r = size.X * 0.5f;
                    kit.Add(new CapsuleMesh { Radius = r, Height = Mathf.Max(size.Y, r * 2.01f), RadialSegments = seg, Rings = Mathf.Max(2, ring / 2) }, xf.ScaledLocal(new Vector3(1f, 1f, size.Z / Mathf.Max(0.001f, size.X))), Colors.White);
                    break;
                }
            case "cylinder":
                kit.Add(new CylinderMesh { TopRadius = size.X * 0.5f, BottomRadius = size.X * 0.5f, Height = size.Y, RadialSegments = seg, Rings = 1 }, xf.ScaledLocal(new Vector3(1f, 1f, size.Z / Mathf.Max(0.001f, size.X))), Colors.White);
                break;
            case "cone":
                kit.Add(new CylinderMesh { TopRadius = 0.004f, BottomRadius = size.X * 0.5f, Height = size.Y, RadialSegments = Mathf.Max(6, seg - 4), Rings = 1 }, xf.ScaledLocal(new Vector3(1f, 1f, size.Z / Mathf.Max(0.001f, size.X))), Colors.White);
                break;
            default: // sphere (elips)
                kit.Add(new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = seg, Rings = ring }, xf.ScaledLocal(size), Colors.White);
                break;
        }
        return kit.Build().SurfaceGetArrays(0);
    }

    sealed class Surface
    {
        public readonly List<Vector3> V = new(), N = new();
        public readonly List<int> B = new(), I = new();
        public readonly List<float> W = new();
    }

    /// <summary>Iskeletli, deri giydirilmis govde. Kok dugumun altinda Skeleton3D ve "Body" MeshInstance3D.</summary>
    public static Node3D Build(BodyDef body, string name)
    {
        var root = new Node3D { Name = name };
        var skel = new Skeleton3D { Name = "Skeleton3D" };
        root.AddChild(skel);
        var bones = Bones(body);
        var index = new Dictionary<string, int>();
        var global = new Dictionary<string, Vector3>();
        foreach (var b in bones)
        {
            int i = skel.AddBone(b.Name);
            index[b.Name] = i;
            var pos = V(b.Pos);
            global[b.Name] = pos;
            var local = pos;
            if (!string.IsNullOrEmpty(b.Parent) && index.TryGetValue(b.Parent, out var pi))
            {
                skel.SetBoneParent(i, pi);
                local = pos - global[b.Parent];
            }
            skel.SetBoneRest(i, new Transform3D(Basis.Identity, local));
        }
        skel.ResetBonePoses();

        var surfaces = new Dictionary<Color, Surface>();
        foreach (var p in Parts(body))
        {
            if (!index.TryGetValue(p.Bone, out var bi)) { GD.PrintErr($"BODY {name}: parca kemigi yok '{p.Bone}'"); continue; }
            var c = ColorOf(body, p.Color);
            if (!surfaces.TryGetValue(c, out var s)) surfaces[c] = s = new Surface();
            var arr = PartArrays(p);
            var verts = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var norms = arr[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var idx = arr[(int)Mesh.ArrayType.Index].AsInt32Array();
            int b0 = s.V.Count;
            for (int k = 0; k < verts.Length; k++)
            {
                s.V.Add(verts[k]); s.N.Add(norms[k]);
                s.B.Add(bi); s.B.Add(0); s.B.Add(0); s.B.Add(0);
                s.W.Add(1f); s.W.Add(0f); s.W.Add(0f); s.W.Add(0f);
            }
            foreach (var k in idx) s.I.Add(b0 + k);
        }
        var mesh = new ArrayMesh();
        foreach (var (color, s) in surfaces)
        {
            var a = new Godot.Collections.Array();
            a.Resize((int)Mesh.ArrayType.Max);
            a[(int)Mesh.ArrayType.Vertex] = s.V.ToArray();
            a[(int)Mesh.ArrayType.Normal] = s.N.ToArray();
            a[(int)Mesh.ArrayType.Bones] = s.B.ToArray();
            a[(int)Mesh.ArrayType.Weights] = s.W.ToArray();
            a[(int)Mesh.ArrayType.Index] = s.I.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, a);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, new StandardMaterial3D { AlbedoColor = color, Roughness = 0.72f });
        }
        var skin = new Skin();
        for (int i = 0; i < skel.GetBoneCount(); i++) skin.AddBind(i, skel.GetBoneGlobalRest(i).AffineInverse());
        var mi = new MeshInstance3D { Name = "Body", Mesh = mesh, Skin = skin };
        skel.AddChild(mi);
        mi.Skeleton = new NodePath("..");
        return root;
    }

    /// <summary>Iskeletsiz tek mesh (suru uyesi, kovan): butun parcalar birlesir, renk basina bir yuzey.</summary>
    public static ArrayMesh BuildStatic(BodyDef body)
    {
        var mesh = new ArrayMesh();
        foreach (var g in Parts(body).GroupBy(p => ColorOf(body, p.Color)))
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var ix = new List<int>();
            foreach (var p in g)
            {
                var arr = PartArrays(p);
                int b0 = v.Count;
                v.AddRange(arr[(int)Mesh.ArrayType.Vertex].AsVector3Array());
                n.AddRange(arr[(int)Mesh.ArrayType.Normal].AsVector3Array());
                ix.AddRange(arr[(int)Mesh.ArrayType.Index].AsInt32Array().Select(k => b0 + k));
            }
            var a = new Godot.Collections.Array();
            a.Resize((int)Mesh.ArrayType.Max);
            a[(int)Mesh.ArrayType.Vertex] = v.ToArray();
            a[(int)Mesh.ArrayType.Normal] = n.ToArray();
            a[(int)Mesh.ArrayType.Index] = ix.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, a);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, new StandardMaterial3D { AlbedoColor = g.Key, Roughness = 0.72f });
        }
        return mesh;
    }

    /// <summary>Maya kesesi (SANAT-SES §4.4): kemige bagli, isiyan kure; kabarinca gorunur.</summary>
    public static MeshInstance3D Sac(BodyDef body, Skeleton3D skel)
    {
        if (string.IsNullOrEmpty(body.SacBone) || skel == null) return null;
        int bi = PoseLibrary.FindBone(skel, body.SacBone);
        if (bi < 0) return null;
        var att = new BoneAttachment3D { Name = "SacAttach", BoneName = skel.GetBoneName(bi) };
        skel.AddChild(att);
        var rest = skel.GetBoneGlobalRest(bi).Origin;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = Palette.MayaTeal, EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 2.2f, Roughness = 0.3f,
        };
        var sac = new MeshInstance3D
        {
            Name = "MayaSac", Visible = false,
            Mesh = new SphereMesh { Radius = body.SacRadius, Height = body.SacRadius * 2f, RadialSegments = 14, Rings = 7, Material = mat },
            Position = V(body.SacPos) - rest,
        };
        att.AddChild(sac);
        return sac;
    }
}
