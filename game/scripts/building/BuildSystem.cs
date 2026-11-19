using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Yerlesik yapi parcasi: carpisma govdesi + model. Kural durumu (malzeme, can, aciklik, yuk, plan) Sim'deki
/// <see cref="StructurePiece"/>'tadir (S); dugum yalnizca gorunus ve carpismadir (TEKNIK §9).
/// </summary>
public partial class BuildPiece : StaticBody3D
{
    public PieceDef Def;
    public Node3D Model;
    public StructurePiece S;
    public string SlotKey;
    internal readonly List<BuildSystem.WorldSocket> WorldSockets = new();

    public int Sid => S?.Id ?? 0;
    public float Hp { get => S?.Hp ?? 0; set { if (S != null) S.Hp = value; } }
    /// <summary>Araziye kendi basina oturuyor (temel, cit, kule).</summary>
    public bool Grounded => S?.Grounded ?? false;
    /// <summary>Zemine en kisa destek yolunun acikligi (int.MaxValue = desteksiz).</summary>
    public int Span => S?.Span ?? int.MaxValue;
    public float Load => S?.Load ?? 0;
    public string Material => S?.Material ?? "wood";
    public bool Planned => S?.Planned ?? false;
}

/// <summary>Hayaletin o anki yerlesim onerisi.</summary>
public sealed class BuildPlacement
{
    public PieceDef Def;
    public Transform3D Xf;
    public string Mode = "free"; // snap | ground | free
    public bool Valid;
    public string Reason = "";
    public bool Grounded;
    public int Span = int.MaxValue;
    /// <summary>0 rahat .. 1 sinirda (hayalet rengi; GDD §7).</summary>
    public float Strain;
    public bool Plan;
    public StabilityReport Report;
    public string SlotKey;
    public readonly List<BuildPiece> Supporters = new();
    public readonly List<BuildPiece> Supported = new();
}

/// <summary>
/// Insa sistemi (GDD §7). Hayalet onizleme, yuvaya oturma, denge rengi, yerlestirme, plan modu, malzeme yukseltme,
/// Plan Defteri yerlestirmesi; tasima dengesi Sim'deki <see cref="StructureGraph"/>'tadir (aciklik + yuk, artimli).
/// Desteksiz ya da asiri yuklu kalan parcalar fizik nesnesi olarak duser (toz, kirinti, ses). Girdi baglamayi cagiran yapar:
///   Init(worldRoot, heightAt) -> SetActive(true) -> her karede UpdateAim(kameraIsinBasi, yon) -> TryPlace() / Rotate() / NextPiece(+-1) / Remove(node)
/// Ilk temel arazide serbest durur ve 15° adimla doner; sonraki parcalar yakindaki yuvalara oturur (R yonelimi degistirir).
/// </summary>
public partial class BuildSystem : Node3D
{
    /// <summary>Parcalarin carpisma katmani (katman 1 de acik: oyuncu ve yaratiklar carpar). Plan parcasi yalniz bunda.</summary>
    public const uint BuildLayer = 1u << 9;
    /// <summary>Dusen parcalar: nisan isini onlara takilmaz.</summary>
    public const uint FallingLayer = 1u << 2;
    public const float RotateStepDeg = 15f;

    public float MaxReach = 14f;
    public float SnapRadius = 2.3f;
    public float SearchRadius = 3.6f;

    public event Action<BuildPiece> Placed;
    public event Action<BuildPiece> Removed;
    /// <summary>Destegi kalmayip ya da asiri yuklenip dusmeye baslayan parca (cagrildiktan hemen sonra serbest birakilir) ve dusen govde.</summary>
    public event Action<BuildPiece, RigidBody3D> Fell;
    public event Action<PieceDef> SelectionChanged;
    /// <summary>Plan modu hayaleti birakildi (bedel odenmedi).</summary>
    public event Action<BuildPiece> PlanPlaced;
    /// <summary>Malzeme yukseltildi (parca, eski malzeme).</summary>
    public event Action<BuildPiece, string> Upgraded;

    public BuildKit Kit { get; private set; }
    public GameData Data { get; private set; }
    public StructureGraph Graph { get; private set; }
    public PieceDef Selected => Kit != null && Kit.Pieces.Count > 0 ? Kit.Pieces[_sel] : null;
    public BuildPlacement Current => _current;
    public bool Active => _active;
    public IReadOnlyList<BuildPiece> Pieces => _pieces;
    public Node3D PiecesRoot => _piecesRoot;
    /// <summary>Plan modu (GDD §7): sol tik bedelsiz plan hayaleti birakir.</summary>
    public bool PlanMode { get; set; }

    internal sealed class WorldSocket
    {
        public BuildPiece Piece;
        public SocketDef Def;
        public Vector3 Pos;
        public Vector3 Dir;
    }

    const float Cell = 1.5f;
    Node3D _root, _piecesRoot;
    Func<Vector3, float> _heightAt;
    bool _active;
    int _sel;
    int _rotStep;
    int _rotIndex;
    BuildPlacement _current;
    Node3D _ghost;
    PieceDef _ghostDef;
    StandardMaterial3D _ghostMat, _planPieceMat;
    readonly List<BuildPiece> _pieces = new();
    readonly Dictionary<int, BuildPiece> _byId = new();
    readonly Dictionary<Vector3I, List<WorldSocket>> _hash = new();
    readonly HashSet<string> _slots = new();
    readonly HashSet<Rid> _aimExclude = new();
    readonly Dictionary<string, BuildKit> _kits = new();
    readonly Dictionary<(string, ulong), Material> _tints = new();
    int _serial;
    readonly RandomNumberGenerator _rng = new() { Seed = 4417 };

    // ------------------------------------------------------------------ kurulum

    /// <summary>
    /// worldRoot: parcalarin eklenecegi dugum. heightAt: dunya noktasinin (x,z) arazi yuksekligi (null olabilir).
    /// graph: kural durumu (oyunda GameState.Build.Graph; verilmezse bos bir graf). data: verilmezse res://data'dan.
    /// </summary>
    public void Init(Node3D worldRoot, Func<Vector3, float> heightAt, BuildKit kit = null, StructureGraph graph = null, GameData data = null)
    {
        _root = worldRoot;
        _heightAt = heightAt;
        Kit = kit ?? BuildKit.Load();
        Data = data ?? GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        if (graph == null) { graph = new StructureGraph(); graph.Bind(Data.Build); }
        Graph = graph;
        if (Kit != null) { _kits[Kit.Material] = Kit; Kit.AddAliases(Data.Build, ItemNames.Of); } // Cam seti (M4)
        if (GetParent() == null) worldRoot.AddChild(this);
        Name = "BuildSystem";
        _piecesRoot = new Node3D { Name = "BuildPieces" };
        worldRoot.AddChild(_piecesRoot);
        _ghostMat = GhostMaterial(ColorOf(Data.Build.Ghost.Ok));
        _planPieceMat = GhostMaterial(ColorOf(Data.Build.Ghost.PlanPiece));
        _sel = 0;
    }

    static Color ColorOf(float[] c) => c.Length >= 4 ? new Color(c[0], c[1], c[2], c[3]) : new Color(c[0], c[1], c[2]);

    static StandardMaterial3D GhostMaterial(Color c) => new()
    {
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = c,
        CullMode = BaseMaterial3D.CullModeEnum.Back,
    };

    /// <summary>Sekil birimi (ICERIK §10.1 Odun adedi; placeables.json buildCosts).</summary>
    public int UnitsOf(string id) => Data.Tool.BuildCosts.TryGetValue(id, out int c) ? c : 4;
    public float MaxHp(BuildPiece p) => p?.S == null ? Kit.PieceHp : Graph.MaxHp(p.S);
    public BuildPiece ById(int sid) => _byId.TryGetValue(sid, out var p) ? p : null;

    public void SetActive(bool on)
    {
        _active = on;
        if (_ghost != null) _ghost.Visible = on && _current != null;
        if (_bpGhost != null) _bpGhost.Visible = on && Blueprint != null;
        if (!on) _current = null;
    }

    /// <summary>Nisan isini bu govdeden gecirir (oyuncunun kendi kapsulu gibi).</summary>
    public void ExcludeFromAim(CollisionObject3D body) => _aimExclude.Add(body.GetRid());

    public void Select(string id)
    {
        int i = Kit.Pieces.FindIndex(p => p.Id == id);
        if (i < 0 || i == _sel) return;
        _sel = i;
        _rotIndex = 0;
        SelectionChanged?.Invoke(Selected);
    }

    public void NextPiece(int dir)
    {
        int n = Kit.Pieces.Count;
        _sel = ((_sel + dir) % n + n) % n;
        _rotIndex = 0;
        SelectionChanged?.Invoke(Selected);
    }

    /// <summary>R: serbest yerlesimde 15° doner; yuvaya otururken bir sonraki uygun yonelime gecer.</summary>
    public void Rotate(int dir = 1)
    {
        if (_current != null && _current.Mode == "snap" && Blueprint == null) _rotIndex += dir;
        else _rotStep += dir;
    }

    /// <summary>Isteğe bagli hazir tus baglama: R dondur, Q/E parca degistir. Islediyse true.</summary>
    public bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.R: Rotate(); return true;
            case Key.Q: NextPiece(-1); return true;
            case Key.E: NextPiece(1); return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ malzeme modelleri

    /// <summary>Malzemenin kiti (res://assets/models/build/&lt;malzeme&gt;/sockets.json); yoksa null.</summary>
    public BuildKit KitFor(string material)
    {
        if (_kits.TryGetValue(material, out var k)) return k;
        var dir = "res://assets/models/build/" + material;
        k = FileAccess.FileExists(dir + "/sockets.json") ? BuildKit.Load(dir) : null;
        _kits[material] = k;
        return k;
    }

    /// <summary>Parcanin o malzemedeki modeli. Malzeme kiti yoksa ahsap modeli malzemenin rengine boyanir (yer tutucu).</summary>
    public Node3D ModelFor(PieceDef def, string material)
    {
        var kit = KitFor(material);
        var md = kit?[def.Id];
        if (md?.Scene != null) return kit.Instantiate(md);
        var inst = Kit.Instantiate(def);
        var m = Data.Build.Material(material);
        if (m == null || material == Kit.Material) return inst;
        var tint = new Color(m.Tint[0], m.Tint[1], m.Tint[2]);
        foreach (var mi in inst.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var src = mi.GetActiveMaterial(s);
                var key = (material, src?.GetInstanceId() ?? 0);
                if (!_tints.TryGetValue(key, out var mat))
                {
                    var dup = src is BaseMaterial3D b ? (BaseMaterial3D)b.Duplicate() : new StandardMaterial3D();
                    dup.AlbedoColor = tint * 1.15f;
                    if (m.Alpha < 1f) { dup.AlbedoColor = new Color(tint.R * 1.1f, tint.G * 1.1f, tint.B * 1.1f, m.Alpha); dup.Transparency = BaseMaterial3D.TransparencyEnum.Alpha; dup.Roughness = 0.1f; dup.Metallic = 0.1f; } // Cam: saydam
                    _tints[key] = mat = dup;
                }
                mi.SetSurfaceOverrideMaterial(s, mat);
            }
        return inst;
    }

    // ------------------------------------------------------------------ nisan ve hayalet

    public void UpdateAim(Vector3 rayFrom, Vector3 rayDir)
    {
        if (!_active || Selected == null || !IsInsideTree()) return;
        rayDir = rayDir.Normalized();
        var hit = AimHit(rayFrom, rayDir);
        if (Blueprint != null) { UpdateBlueprintAim(hit); return; }
        var flat = new Vector3(rayDir.X, 0, rayDir.Z);
        var look = hit + (flat.LengthSquared() > 1e-4f ? flat.Normalized() * 0.6f : Vector3.Zero);
        var def = Selected;
        var p = BestSnap(def, hit, look);
        if (p == null && def.Ground != null) p = GroundPlacement(def, hit);
        if (p == null)
        {
            var b = new Basis(Vector3.Up, Mathf.DegToRad(_rotStep * RotateStepDeg));
            p = new BuildPlacement { Def = def, Xf = new Transform3D(b, hit), Mode = "free", Valid = false, Reason = "yuva yok" };
        }
        _current = p;
        UpdateGhost();
    }

    Vector3 AimHit(Vector3 from, Vector3 dir)
    {
        float best = MaxReach;
        var space = GetWorld3D().DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(from, from + dir * MaxReach, 1u | BuildLayer);
        if (_aimExclude.Count > 0) q.Exclude = new Godot.Collections.Array<Rid>(_aimExclude);
        var r = space.IntersectRay(q);
        if (r.Count > 0) best = from.DistanceTo((Vector3)r["position"]);
        if (_heightAt != null)
        {
            float prev = 0;
            for (float t = 0.25f; t <= best; t += 0.25f)
            {
                var p = from + dir * t;
                if (p.Y <= _heightAt(p))
                {
                    float lo = prev, hi = t;
                    for (int i = 0; i < 12; i++)
                    {
                        float m = (lo + hi) * 0.5f;
                        var pm = from + dir * m;
                        if (pm.Y <= _heightAt(pm)) hi = m; else lo = m;
                    }
                    best = Mathf.Min(best, hi);
                    break;
                }
                prev = t;
            }
        }
        return from + dir * best;
    }

    /// <summary>Hayalet rengi (GDD §7): gecersiz kirmizi; plan kipinde mavi; yoksa zorlanmaya gore yesilden sariya.</summary>
    public Color GhostColor(BuildPlacement p)
    {
        var g = Data.Build.Ghost;
        if (p == null || !p.Valid) return ColorOf(g.Bad);
        if (p.Plan) return ColorOf(g.Plan);
        return ColorOf(g.Ok).Lerp(ColorOf(g.Strained), Mathf.Clamp(p.Strain, 0f, 1f));
    }

    void UpdateGhost()
    {
        var def = Selected;
        if (_ghostDef != def)
        {
            _ghost?.QueueFree();
            _ghost = Kit.Instantiate(def);
            _ghost.Name = "Ghost";
            _ghost.TopLevel = true;
            AddChild(_ghost);
            _ghostDef = def;
            foreach (var mi in _ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                mi.MaterialOverride = _ghostMat;
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        _ghost.Visible = _active && _current != null;
        if (_current == null) return;
        _ghost.GlobalTransform = _current.Xf;
        _ghostMat.AlbedoColor = GhostColor(_current);
    }

    // ------------------------------------------------------------------ yuva arama

    static IEnumerable<Vector3> AlignDirs(string align, Vector3 d)
    {
        var r90 = new Vector3(d.Z, 0, -d.X);
        switch (align)
        {
            case "opposite": yield return -d; break;
            case "same": yield return d; break;
            case "any4": yield return d; yield return r90; yield return -d; yield return -r90; break;
            default: yield return d; yield return -d; break;
        }
    }

    bool RuleExists(string attach, string target, string kind) =>
        Kit.Rules.Any(r => r.Attach == attach && r.Target == target && r.Allows(kind));

    sealed class Cand
    {
        public Transform3D Xf;
        public float Score;
        public float SockDist;
        public Vector3 Centre;
        public float Yaw;
    }

    BuildPlacement BestSnap(PieceDef def, Vector3 hit, Vector3 look)
    {
        var cands = new List<Cand>();
        foreach (var ws in QuerySockets(hit, SearchRadius))
            foreach (var a in def.Sockets)
                foreach (var rule in Kit.Rules)
                {
                    if (rule.Attach != a.Type || rule.Target != ws.Def.Type || !rule.Allows(def.Kind)) continue;
                    foreach (var w in AlignDirs(rule.Align, ws.Dir))
                    {
                        float yaw = Mathf.Atan2(w.X, w.Z) - Mathf.Atan2(a.Dir.X, a.Dir.Z);
                        var basis = new Basis(Vector3.Up, yaw);
                        var xf = new Transform3D(basis, ws.Pos - basis * a.Pos);
                        var c = xf * def.Center;
                        float sd = ws.Pos.DistanceTo(hit);
                        cands.Add(new Cand { Xf = xf, Centre = c, SockDist = sd, Score = c.DistanceTo(look) + 0.5f * sd, Yaw = Mathf.PosMod(yaw, Mathf.Tau) });
                    }
                }
        if (cands.Count == 0) return null;
        var groups = cands.GroupBy(c => PosKey(c.Centre, 10f)).Select(g => g.ToList()).OrderBy(g => g.Min(c => c.Score));
        foreach (var g in groups)
        {
            if (g.Min(c => c.SockDist) > SnapRadius) continue;
            var opts = g.GroupBy(c => Mathf.RoundToInt(Mathf.RadToDeg(c.Yaw)) % 360).Select(x => x.OrderBy(c => c.Score).First()).OrderBy(c => c.Yaw).ToList();
            var pick = opts[((_rotIndex % opts.Count) + opts.Count) % opts.Count];
            var p = Evaluate(def, pick.Xf, "snap");
            if (p.Reason == "dolu") continue;
            return p;
        }
        return null;
    }

    BuildPlacement GroundPlacement(PieceDef def, Vector3 hit, string material = "wood")
    {
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(_rotStep * RotateStepDeg));
        var xf = new Transform3D(basis, new Vector3(hit.X, 0, hit.Z));
        bool ok = GroundFit(def, ref xf, out string why, adjustY: true);
        var p = Evaluate(def, xf, "ground", material);
        if (!ok) { p.Valid = false; p.Reason = why; }
        else if (p.Valid && Overlaps(def, xf)) { p.Valid = false; p.Reason = "çakışıyor"; }
        return p;
    }

    /// <summary>Arazi oturmasi. adjustY: yuksekligi araziye gore koyar; degilse verilen yuksekligin uygunlugunu sinar.</summary>
    bool GroundFit(PieceDef def, ref Transform3D xf, out string why, bool adjustY)
    {
        why = "";
        if (def.Ground == null) { why = "araziye konmaz"; return false; }
        if (_heightAt == null) return true;
        var mn = def.BoundsMin;
        var mx = def.BoundsMax;
        var pts = new[] { new Vector3(mn.X, 0, mn.Z), new Vector3(mx.X, 0, mn.Z), new Vector3(mn.X, 0, mx.Z), new Vector3(mx.X, 0, mx.Z), new Vector3(0, 0, 0) };
        float lo = float.MaxValue, hi = float.MinValue, sum = 0;
        foreach (var lp in pts)
        {
            var wp = xf.Basis * lp + new Vector3(xf.Origin.X, 0, xf.Origin.Z);
            float h = _heightAt(wp);
            lo = Mathf.Min(lo, h);
            hi = Mathf.Max(hi, h);
            sum += h;
        }
        var g = def.Ground;
        if (g.Mode == "top")
        {
            float y = adjustY ? hi + g.Clearance : xf.Origin.Y;
            if (adjustY) xf.Origin = new Vector3(xf.Origin.X, y, xf.Origin.Z);
            if (y - lo > g.Depth) { why = "arazi çok eğimli / boşlukta"; return false; }
            if (hi > y + 0.25f) { why = "toprağa gömülüyor"; return false; }
            return true;
        }
        float avg = sum / pts.Length;
        if (adjustY) xf.Origin = new Vector3(xf.Origin.X, avg, xf.Origin.Z);
        if (hi - lo > g.Spread) { why = "arazi çok eğimli"; return false; }
        if (!adjustY && Mathf.Abs(avg - xf.Origin.Y) > 0.6f) { why = "zemine değmiyor"; return false; }
        return true;
    }

    bool Overlaps(PieceDef def, Transform3D xf)
    {
        var size = def.BoundsMax - def.BoundsMin - new Vector3(0.3f, 0.2f, 0.3f);
        if (size.X <= 0.05f || size.Y <= 0.05f || size.Z <= 0.05f) return false;
        var q = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = size },
            Transform = xf * new Transform3D(Basis.Identity, def.Center),
            CollisionMask = BuildLayer,
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(q, 1).Count > 0;
    }

    static string SlotGroup(PieceDef d) => d.Kind switch
    {
        "foundation" or "platform" => "F",
        "floor" => "Z",
        "stairs" => "M",
        "pillar" => "P",
        "beam" => "B",
        "wall" or "fence" => "W",
        "roof" => d.Id == "cati_tepesi" ? "T" : d.Id == "duz_cati" ? "Z" : "R",
        _ => d.Kind,
    };

    static string SlotKeyOf(PieceDef d, Transform3D xf)
    {
        var g = SlotGroup(d);
        var p = g is "W" or "P" or "B" ? xf.Origin : xf * d.Center;
        return g + ":" + PosKey(p, 4f);
    }

    static string PosKey(Vector3 p, float q) => $"{Mathf.RoundToInt(p.X * q)}:{Mathf.RoundToInt(p.Y * q)}:{Mathf.RoundToInt(p.Z * q)}";

    /// <summary>Aday parcanin Sim karsiligi (kenarlar yuvalardan).</summary>
    StructurePiece Candidate(BuildPlacement p, string material) => new()
    {
        Shape = p.Def.Id,
        Material = material,
        Units = UnitsOf(p.Def.Id),
        Pos = new[] { p.Xf.Origin.X, p.Xf.Origin.Y, p.Xf.Origin.Z },
        Yaw = p.Xf.Basis.GetEuler().Y,
        CenterY = (p.Xf * p.Def.Center).Y,
        Grounded = p.Grounded,
        Horizontal = p.Def.Horizontal,
        SupportedBy = p.Supporters.Select(s => s.Sid).ToList(),
        Supports = p.Supported.Select(s => s.Sid).ToList(),
    };

    BuildPlacement Evaluate(PieceDef def, Transform3D xf, string mode, string material = "wood")
    {
        var p = new BuildPlacement { Def = def, Xf = xf, Mode = mode, Valid = true, SlotKey = SlotKeyOf(def, xf), Plan = PlanMode };
        if (_slots.Contains(p.SlotKey)) { p.Valid = false; p.Reason = "dolu"; return p; }
        // cakisan yuvalardan destek baglari
        foreach (var s in def.Sockets)
        {
            var wp = xf * s.Pos;
            foreach (var os in QuerySockets(wp, 0.1f))
            {
                if (os.Pos.DistanceTo(wp) > 0.06f) continue;
                if (RuleExists(s.Type, os.Def.Type, def.Kind) && !p.Supporters.Contains(os.Piece)) p.Supporters.Add(os.Piece);
                if (RuleExists(os.Def.Type, s.Type, os.Piece.Def.Kind) && !p.Supported.Contains(os.Piece)) p.Supported.Add(os.Piece);
            }
        }
        if (mode == "ground") p.Grounded = true;
        else if (def.Ground != null)
        {
            var x = xf;
            p.Grounded = GroundFit(def, ref x, out string why, adjustY: false);
            if (!p.Grounded && def.Origin == "top_center")
            {
                p.Valid = false;
                p.Reason = why;
                return p;
            }
        }
        var rep = Graph.Evaluate(Candidate(p, material), assumePlanned: PlanMode);
        p.Report = rep;
        p.Span = rep.Span;
        p.Strain = rep.Strain;
        p.Valid = rep.Valid;
        p.Reason = rep.Reason;
        return p;
    }

    // ------------------------------------------------------------------ yerlestirme, sokme, dusme

    /// <summary>Gecerli hayaleti yerlestirir (plan modunda plan hayaleti olarak); olmadiysa null.</summary>
    public BuildPiece TryPlace()
    {
        if (!_active || _current == null || !_current.Valid || Blueprint != null) return null;
        var piece = Spawn(_current, PlanMode, Data.Build.SetMaterialOf(_current.Def.Id) ?? "wood"); // Cam seti kendi malzemesiyle (M4)
        _current = null;
        if (_ghost != null) _ghost.Visible = false;
        return piece;
    }

    BuildPiece Spawn(BuildPlacement p, bool planned, string material)
    {
        var def = p.Def;
        var s = Candidate(p, material);
        s.Planned = planned;
        var m = Data.Build.Material(material);
        s.Hp = m?.Hp ?? Kit.PieceHp;
        Graph.Add(s);
        var piece = SpawnNode(def, p.Xf, s, p.SlotKey);
        if (planned) PlanPlaced?.Invoke(piece);
        else Placed?.Invoke(piece);
        return piece;
    }

    /// <summary>Sim parcasinin dugumu: govde, model, carpisma, yuvalar (Sim'e eklemez).</summary>
    BuildPiece SpawnNode(PieceDef def, Transform3D xf, StructurePiece s, string slotKey)
    {
        var piece = new BuildPiece
        {
            Def = def,
            S = s,
            Name = $"{def.Id}_{++_serial}",
            CollisionLayer = s.Planned ? BuildLayer : 1u | BuildLayer,
            CollisionMask = 0,
            SlotKey = slotKey,
        };
        _piecesRoot.AddChild(piece);
        piece.GlobalTransform = xf;
        piece.Model = s.Planned ? PlanModel(def) : ModelFor(def, s.Material);
        piece.AddChild(piece.Model);
        foreach (var c in def.Colliders)
        {
            var (shape, lxf) = BuildKit.MakeShape(c);
            piece.AddChild(new CollisionShape3D { Shape = shape, Transform = lxf, Name = c.Node != null ? "Col_" + c.Node : "Col" });
        }
        foreach (var sd in def.Sockets)
        {
            var ws = new WorldSocket { Piece = piece, Def = sd, Pos = xf * sd.Pos, Dir = (xf.Basis * sd.Dir).Normalized() };
            piece.WorldSockets.Add(ws);
            var k = HashKey(ws.Pos);
            if (!_hash.TryGetValue(k, out var list)) _hash[k] = list = new List<WorldSocket>();
            list.Add(ws);
        }
        _slots.Add(piece.SlotKey);
        _pieces.Add(piece);
        _byId[s.Id] = piece;
        return piece;
    }

    /// <summary>
    /// Kayittan sonra (build.view AfterRestore): butun dugumleri siler ve Sim grafindaki parcalardan yeniden kurar.
    /// Olay yaymaz (sayaclar kayittan gelir).
    /// </summary>
    public void RebuildFromGraph()
    {
        foreach (var p in _pieces.ToList()) { Detach(p); p.QueueFree(); }
        _hash.Clear();
        _slots.Clear();
        _byId.Clear();
        foreach (var s in Graph.Pieces.Values.OrderBy(x => x.Id))
        {
            var def = Kit[s.Shape];
            if (def == null) { GD.PushWarning($"BuildSystem: kayitta bilinmeyen parca '{s.Shape}'"); continue; }
            var xf = new Transform3D(new Basis(Vector3.Up, s.Yaw), new Vector3(s.Pos[0], s.Pos[1], s.Pos[2]));
            SpawnNode(def, xf, s, SlotKeyOf(def, xf));
        }
        GD.Print($"BUILD restored pieces={_pieces.Count}");
    }

    Node3D PlanModel(PieceDef def)
    {
        var m = Kit.Instantiate(def);
        foreach (var mi in m.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            mi.MaterialOverride = _planPieceMat;
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return m;
    }

    /// <summary>Sinama icin (coop cam adimi): merkez cevresinde parcanin gecerli ilk yerlesimini bulur (yuvaya oturur); yoksa null.</summary>
    public Transform3D? FindValidPlacement(string id, Vector3 center, float radius = 4f, string material = "wood")
    {
        var def = Kit[id];
        if (def == null) return null;
        material = Data.Build.SetMaterialOf(id) ?? material;
        for (float dy = 0f; dy <= 1.01f; dy += 0.5f)
            for (float dx = -radius; dx <= radius; dx += 0.5f)
                for (float dz = -radius; dz <= radius; dz += 0.5f)
                    for (int k = 0; k < 4; k++)
                    {
                        var xf = new Transform3D(new Basis(Vector3.Up, k * Mathf.Pi / 2f), center + new Vector3(dx, dy, dz));
                        var p = Evaluate(def, xf, "snap", material);
                        if (p != null && p.Valid) return p.Xf;
                    }
        return null;
    }
    /// <summary>Komut dosyasi icin: hayaletsiz, kurallari sinayarak yerlestirir (gecersizse null, neden reason'da).</summary>
    public BuildPiece PlaceAt(string id, Transform3D xf, out string reason, string material = "wood")
    {
        var def = Kit[id];
        material = Data.Build.SetMaterialOf(id) ?? material; // Cam seti kendi malzemesiyle (M4)
        reason = def == null ? "parça yok" : "";
        if (def == null) return null;
        var p = Evaluate(def, xf, "snap", material);
        // Ortak oyun istemcisi: host dogruladi; yalniz dolu yuva reddedilir (plan ve malzeme "pieces.state" ile gelir).
        if (!p.Valid && !(RemoteAuthority && p.Reason != "dolu")) { reason = p.Reason; return null; }
        return Spawn(p, PlanMode, material);
    }

    /// <summary>Ortak oyun istemcisi: yapi durumu host'tan gelir, yerel denge denetimi yerlesimi reddetmez (NetSync.Yapi.cs).</summary>
    public bool RemoteAuthority { get; set; }

    /// <summary>Host'un parca durumunu uygular (denetimsiz): malzeme, plan bayragi, konan malzeme. Gorunus degisirse model yenilenir.</summary>
    public void ApplyRemote(BuildPiece piece, string material, bool planned, Dictionary<string, int> delivered)
    {
        if (piece?.S == null) return;
        bool look = false;
        if (piece.Material != material) { Graph.TrySetMaterial(piece.Sid, material, force: true); look = true; }
        if (piece.Planned != planned)
        {
            Graph.SetPlanned(piece.Sid, planned);
            piece.CollisionLayer = planned ? BuildLayer : 1u | BuildLayer;
            look = true;
        }
        piece.S.Delivered = delivered;
        if (look) SwapModel(piece, planned ? PlanModel(piece.Def) : ModelFor(piece.Def, material));
    }

    /// <summary>Nisan isininin vurdugu parca (sokme, onarim, yukseltme, plan malzemesi icin).</summary>
    public BuildPiece PieceAt(Vector3 rayFrom, Vector3 rayDir)
    {
        var q = PhysicsRayQueryParameters3D.Create(rayFrom, rayFrom + rayDir.Normalized() * MaxReach, BuildLayer);
        if (_aimExclude.Count > 0) q.Exclude = new Godot.Collections.Array<Rid>(_aimExclude);
        var r = GetWorld3D().DirectSpaceState.IntersectRay(q);
        return r.Count > 0 ? r["collider"].As<GodotObject>() as BuildPiece : null;
    }

    /// <summary>Parcayi soker (node parcanin kendisi ya da herhangi bir cocugu olabilir). Desteksiz ya da asiri yuklu kalanlar duser.</summary>
    public bool Remove(Node node)
    {
        var piece = FindPiece(node);
        if (piece == null || !_pieces.Contains(piece)) return false;
        var fallen = Graph.Remove(piece.Sid, collapse: !RemoteAuthority); // istemcide cokmeyi host yapar, "pieces" getirir
        Detach(piece);
        Removed?.Invoke(piece);
        piece.QueueFree();
        foreach (var s in fallen) if (ById(s.Id) is { } f) Fall(f);
        return true;
    }

    static BuildPiece FindPiece(Node n)
    {
        while (n != null && n is not BuildPiece) n = n.GetParent();
        return n as BuildPiece;
    }

    void Detach(BuildPiece piece)
    {
        foreach (var ws in piece.WorldSockets)
            if (_hash.TryGetValue(HashKey(ws.Pos), out var list)) list.Remove(ws);
        _slots.Remove(piece.SlotKey);
        _pieces.Remove(piece);
        _byId.Remove(piece.Sid);
    }

    /// <summary>
    /// Malzemeyi degistirir (yerinde yukseltme): Sim yuk sinirini denetler, can hasar oraniyla gecer, model degisir.
    /// Islediyse true; olmadiysa neden rep'te.
    /// </summary>
    public bool Upgrade(BuildPiece piece, string material, out StabilityReport rep)
    {
        rep = null;
        if (piece?.S == null || piece.Planned) return false;
        string old = piece.Material;
        float oldMax = Graph.MaxHp(piece.S);
        rep = Graph.TrySetMaterial(piece.Sid, material);
        if (!rep.Valid) return false;
        piece.S.Hp = StructureCosts.UpgradedHp(piece.S.Hp, oldMax, Graph.MaxHp(piece.S));
        SwapModel(piece, ModelFor(piece.Def, material));
        Upgraded?.Invoke(piece, old);
        return true;
    }

    /// <summary>Malzemesi tamamlanan plan parcasini kurar. Dengesi tutmazsa (alti kurulmamis) neden rep'te, false.</summary>
    public bool CompletePlan(BuildPiece piece, out StabilityReport rep)
    {
        rep = null;
        if (piece?.S == null || !piece.Planned) return false;
        rep = Graph.Complete(piece.Sid);
        if (!rep.Valid) return false;
        piece.CollisionLayer = 1u | BuildLayer;
        SwapModel(piece, ModelFor(piece.Def, piece.Material));
        Placed?.Invoke(piece);
        return true;
    }

    static void SwapModel(BuildPiece piece, Node3D model)
    {
        piece.Model?.QueueFree();
        piece.Model = model;
        piece.AddChild(model);
    }

    void Fall(BuildPiece piece)
    {
        if (!_pieces.Contains(piece)) return;
        Detach(piece);
        var xf = piece.GlobalTransform;
        var col = Data.Build.Collapse;
        var body = new RigidBody3D
        {
            Name = piece.Name + "_dusen",
            CollisionLayer = FallingLayer,
            CollisionMask = 1u | FallingLayer,
            Mass = Mathf.Max(20f, (piece.Def.BoundsMax - piece.Def.BoundsMin).Length() * 25f),
        };
        _piecesRoot.AddChild(body);
        body.GlobalTransform = xf;
        piece.Model.Reparent(body, false);
        piece.Model.Transform = Transform3D.Identity;
        foreach (var c in piece.Def.Colliders)
        {
            var (shape, lxf) = BuildKit.MakeShape(c);
            body.AddChild(new CollisionShape3D { Shape = shape, Transform = lxf });
        }
        body.AngularVelocity = new Vector3(_rng.RandfRange(-0.6f, 0.6f), _rng.RandfRange(-0.3f, 0.3f), _rng.RandfRange(-0.6f, 0.6f));
        body.LinearVelocity = new Vector3(0, -0.5f, 0);
        CollapseFx(piece, xf * piece.Def.Center, col);
        Fell?.Invoke(piece, body);
        piece.QueueFree();
        GetTree().CreateTimer(col.Lifetime).Timeout += () => { if (IsInstanceValid(body)) body.QueueFree(); };
    }

    /// <summary>Cokme: toz, birkac kirinti (fizik) ve ses (GDD §7; build.json collapse).</summary>
    void CollapseFx(BuildPiece piece, Vector3 at, BuildCollapseDef col)
    {
        var m = Data.Build.Material(piece.Material);
        var tint = m != null ? new Color(m.Tint[0], m.Tint[1], m.Tint[2]) : new Color(0.7f, 0.6f, 0.45f);
        Fx.Burst(at, tint, col.Dust, 3.5f, 0.12f, 0.9f);
        Sfx.Play(col.Sfx, at, 0f, 0.7f);
        var mat = new StandardMaterial3D { AlbedoColor = tint * 0.9f };
        for (int i = 0; i < 4; i++)
        {
            float s = _rng.RandfRange(0.12f, 0.3f);
            var chunk = new RigidBody3D { CollisionLayer = FallingLayer, CollisionMask = 1u | FallingLayer, Mass = 4f };
            chunk.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One * s, Material = mat } });
            chunk.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * s } });
            _piecesRoot.AddChild(chunk);
            chunk.GlobalPosition = at + new Vector3(_rng.RandfRange(-0.8f, 0.8f), _rng.RandfRange(-0.4f, 0.6f), _rng.RandfRange(-0.8f, 0.8f));
            chunk.LinearVelocity = new Vector3(_rng.RandfRange(-2f, 2f), _rng.RandfRange(0.5f, 3f), _rng.RandfRange(-2f, 2f));
            GetTree().CreateTimer(col.Lifetime * 0.6f).Timeout += () => { if (IsInstanceValid(chunk)) chunk.QueueFree(); };
        }
    }

    // ------------------------------------------------------------------ yuva karma tablosu

    static Vector3I HashKey(Vector3 p) => new(Mathf.FloorToInt(p.X / Cell), Mathf.FloorToInt(p.Y / Cell), Mathf.FloorToInt(p.Z / Cell));

    IEnumerable<WorldSocket> QuerySockets(Vector3 c, float r)
    {
        var lo = HashKey(c - Vector3.One * r);
        var hi = HashKey(c + Vector3.One * r);
        float r2 = r * r;
        for (int x = lo.X; x <= hi.X; x++)
            for (int y = lo.Y; y <= hi.Y; y++)
                for (int z = lo.Z; z <= hi.Z; z++)
                    if (_hash.TryGetValue(new Vector3I(x, y, z), out var list))
                        foreach (var ws in list)
                            if (ws.Pos.DistanceSquaredTo(c) <= r2)
                                yield return ws;
    }
}
