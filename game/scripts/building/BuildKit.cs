using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Kabarma.Game;

/// <summary>Yuva (soket): parcanin yerel koordinatinda nokta + yatay yon (Godot ekseni, metre).</summary>
public sealed class SocketDef
{
    public string Name;
    public string Type;
    public Vector3 Pos;
    public Vector3 Dir;
}

/// <summary>Carpisma sekli: kutu (merkez, boyut, eksenler) ya da dis bukey nokta bulutu. Node doluysa kapi kanadina aittir.</summary>
public sealed class ColliderDef
{
    public string Type;
    public Vector3 Center;
    public Vector3 Size;
    public Basis Basis = Basis.Identity;
    public Vector3[] Points;
    public string Node;
}

/// <summary>Araziye serbest oturma: "top" (temel: ust yuz arazinin clearance kadar ustunde, ayaklar depth'e kadar iner) ya da "bottom" (cit, kule: taban ortalama zeminde, egim farki spread'i gecemez).</summary>
public sealed class GroundDef
{
    public string Mode = "top";
    public float Clearance = 0.35f;
    public float Depth = 1.45f;
    public float Spread = 0.8f;
}

public sealed class SnapRule
{
    public string Attach;
    public string Target;
    public string Align; // opposite | same | either | any4
    public HashSet<string> Kinds;

    public bool Allows(string kind) => Kinds == null || Kinds.Contains(kind);
}

public sealed class PieceDef
{
    public string Id;
    public string Name;
    public string Kind;
    public string Origin;
    public int Tris;
    public Vector3 BoundsMin, BoundsMax, Center;
    public SocketDef[] Sockets = Array.Empty<SocketDef>();
    public ColliderDef[] Colliders = Array.Empty<ColliderDef>();
    public GroundDef Ground;
    public Dictionary<string, Vector3> Nodes = new();
    public string ScenePath;
    public bool Horizontal;
    PackedScene _scene;

    public PackedScene Scene => _scene ??= ResourceLoader.Exists(ScenePath) ? GD.Load<PackedScene>(ScenePath) : null;

    /// <summary>Yuva tipi ayni olan ilk yuva (yoksa null).</summary>
    public SocketDef Socket(string type) => Sockets.FirstOrDefault(s => s.Type == type);
}

/// <summary>
/// Yapi kiti: sockets.json + parca GLB'leri (pipeline/build_kit.py uretir). Bir malzeme seti = bir kit (su an ahsap).
/// Kural tablosu: hayaletin "attach" yuvasi, yerlesik parcanin "target" yuvasina hizalama kuraliyla oturur.
/// </summary>
public sealed class BuildKit
{
    public const string WoodDir = "res://assets/models/build/wood";

    public string Material = "wood";
    public float Grid = 3f;
    public float Storey = 3f;
    public int MaxSpan = 3;
    public float PieceHp = 400f;
    public readonly List<PieceDef> Pieces = new();
    public readonly Dictionary<string, PieceDef> ById = new();
    public readonly List<SnapRule> Rules = new();

    public PieceDef this[string id] => ById.TryGetValue(id, out var d) ? d : null;

    public static BuildKit Load(string dir = WoodDir)
    {
        var path = dir + "/sockets.json";
        if (!FileAccess.FileExists(path))
        {
            GD.PushError($"BuildKit: {path} yok (pipeline/build_kit.py calistirilmali)");
            return null;
        }
        var text = FileAccess.GetFileAsString(path);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var kit = new BuildKit
        {
            Material = Str(root, "material", "wood"),
            Grid = Num(root, "grid", 3f),
            Storey = Num(root, "storey", 3f),
        };
        var horizontal = new HashSet<string> { "floor", "roof", "beam" };
        if (root.TryGetProperty("support", out var sup))
        {
            kit.MaxSpan = (int)Num(sup, "max_span", 3);
            kit.PieceHp = Num(sup, "hp", 400);
            if (sup.TryGetProperty("horizontal_kinds", out var hk))
                horizontal = hk.EnumerateArray().Select(e => e.GetString()).ToHashSet();
        }
        if (root.TryGetProperty("rules", out var rules))
            foreach (var r in rules.EnumerateArray())
                kit.Rules.Add(new SnapRule
                {
                    Attach = Str(r, "attach"),
                    Target = Str(r, "target"),
                    Align = Str(r, "align", "either"),
                    Kinds = r.TryGetProperty("kinds", out var ks) ? ks.EnumerateArray().Select(e => e.GetString()).ToHashSet() : null,
                });
        foreach (var p in root.GetProperty("pieces").EnumerateObject())
        {
            var e = p.Value;
            var d = new PieceDef
            {
                Id = p.Name,
                Name = Str(e, "name", p.Name),
                Kind = Str(e, "kind", "wall"),
                Origin = Str(e, "origin", "bottom_center"),
                Tris = (int)Num(e, "tris", 0),
                ScenePath = dir + "/" + Str(e, "glb", p.Name + ".glb"),
            };
            d.Horizontal = horizontal.Contains(d.Kind);
            if (e.TryGetProperty("bounds", out var b))
            {
                d.BoundsMin = Vec(b.GetProperty("min"));
                d.BoundsMax = Vec(b.GetProperty("max"));
            }
            d.Center = e.TryGetProperty("center", out var c) ? Vec(c) : (d.BoundsMin + d.BoundsMax) * 0.5f;
            if (e.TryGetProperty("sockets", out var ss))
                d.Sockets = ss.EnumerateArray().Select(s => new SocketDef
                {
                    Name = Str(s, "name"),
                    Type = Str(s, "type"),
                    Pos = Vec(s.GetProperty("pos")),
                    Dir = Vec(s.GetProperty("dir")),
                }).ToArray();
            if (e.TryGetProperty("colliders", out var cs))
                d.Colliders = cs.EnumerateArray().Select(ParseCollider).ToArray();
            if (e.TryGetProperty("ground", out var g))
                d.Ground = new GroundDef
                {
                    Mode = Str(g, "mode", "top"),
                    Clearance = Num(g, "clearance", 0.35f),
                    Depth = Num(g, "depth", 1.45f),
                    Spread = Num(g, "spread", 0.8f),
                };
            if (e.TryGetProperty("nodes", out var nodes))
                foreach (var n in nodes.EnumerateObject())
                    d.Nodes[n.Name] = Vec(n.Value.GetProperty("pivot"));
            kit.Pieces.Add(d);
            kit.ById[d.Id] = d;
        }
        return kit;
    }

    static ColliderDef ParseCollider(JsonElement c)
    {
        var d = new ColliderDef { Type = Str(c, "type", "box"), Node = c.TryGetProperty("node", out var n) ? n.GetString() : null };
        if (d.Type == "convex")
        {
            d.Points = c.GetProperty("points").EnumerateArray().Select(Vec).ToArray();
            return d;
        }
        d.Center = Vec(c.GetProperty("center"));
        d.Size = Vec(c.GetProperty("size"));
        if (c.TryGetProperty("basis", out var bs))
        {
            var cols = bs.EnumerateArray().Select(Vec).ToArray();
            d.Basis = new Basis(cols[0], cols[1], cols[2]);
        }
        return d;
    }

    /// <summary>Parcanin gorsel modelini (GLB sahnesi) uretir. Butun parcalar ortak wood_kit.tres malzemesini paylasir.</summary>
    public Node3D Instantiate(PieceDef d)
    {
        var inst = d.Scene?.Instantiate<Node3D>();
        if (inst == null)
        {
            GD.PushWarning($"BuildKit: {d.ScenePath} yuklenemedi, kutu yedegi");
            var box = new MeshInstance3D { Mesh = new BoxMesh { Size = d.BoundsMax - d.BoundsMin } };
            box.Position = d.Center;
            inst = new Node3D();
            inst.AddChild(box);
        }
        inst.Name = "Model";
        return inst;
    }

    /// <summary>Carpisma sekli ve yerel donusumu.</summary>
    public static (Shape3D shape, Transform3D xf) MakeShape(ColliderDef c)
    {
        if (c.Type == "convex")
            return (new ConvexPolygonShape3D { Points = c.Points }, Transform3D.Identity);
        return (new BoxShape3D { Size = c.Size }, new Transform3D(c.Basis, c.Center));
    }

    static string Str(JsonElement e, string k, string def = null) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : def;
    static float Num(JsonElement e, string k, float def) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : def;
    static Vector3 Vec(JsonElement a)
    {
        var f = a.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
        return new Vector3(f[0], f[1], f[2]);
    }
}
