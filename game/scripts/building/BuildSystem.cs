using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Yerlesik yapi parcasi: carpisma govdesi + model + destek baglari (GDD §7 tasima dengesi).</summary>
public partial class BuildPiece : StaticBody3D
{
    public PieceDef Def;
    public Node3D Model;
    public float Hp;
    /// <summary>Araziye kendi basina oturuyor (temel, cit, kule).</summary>
    public bool Grounded;
    /// <summary>En yakin zemine oturan parcadan yatay aciklik (0 = zemine/temele dayali). int.MaxValue = desteksiz.</summary>
    public int Span = int.MaxValue;
    public string SlotKey;
    public readonly HashSet<BuildPiece> SupportedBy = new();
    public readonly HashSet<BuildPiece> Supports = new();
    internal readonly List<BuildSystem.WorldSocket> WorldSockets = new();
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
    public string SlotKey;
    public readonly List<BuildPiece> Supporters = new();
    public readonly List<BuildPiece> Supported = new();
}

/// <summary>
/// Insa sistemi (GDD §7). Hayalet onizleme, yuvaya oturma, gecerlilik rengi, yerlestirme, destek/aciklik kurali,
/// desteksiz kalan parcalarin fizik nesnesi olarak dusmesi. Girdi baglamayi cagiran yapar:
///   Init(worldRoot, heightAt) -> SetActive(true) -> her karede UpdateAim(kameraIsinBasi, yon) -> TryPlace() / Rotate() / NextPiece(+-1) / Remove(node)
/// Ilk temel arazide serbest durur ve 15° adimla doner; sonraki parcalar yakindaki yuvalara oturur (R yonelimi degistirir).
/// </summary>
public partial class BuildSystem : Node3D
{
    /// <summary>Parcalarin carpisma katmani (katman 1 de acik: oyuncu ve yaratiklar carpar).</summary>
    public const uint BuildLayer = 1u << 9;
    /// <summary>Dusen parcalar: nisan isini onlara takilmaz.</summary>
    public const uint FallingLayer = 1u << 2;
    public const float RotateStepDeg = 15f;

    public float MaxReach = 14f;
    public float SnapRadius = 2.3f;
    public float SearchRadius = 3.6f;
    public float FallLifetime = 7f;

    public event Action<BuildPiece> Placed;
    public event Action<BuildPiece> Removed;
    /// <summary>Destegi kalmayip dusmeye baslayan parca (cagrildiktan hemen sonra serbest birakilir) ve dusen govde.</summary>
    public event Action<BuildPiece, RigidBody3D> Fell;
    public event Action<PieceDef> SelectionChanged;

    public BuildKit Kit { get; private set; }
    public PieceDef Selected => Kit != null && Kit.Pieces.Count > 0 ? Kit.Pieces[_sel] : null;
    public BuildPlacement Current => _current;
    public bool Active => _active;
    public IReadOnlyList<BuildPiece> Pieces => _pieces;
    public Node3D PiecesRoot => _piecesRoot;

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
    StandardMaterial3D _ghostOk, _ghostBad;
    readonly List<BuildPiece> _pieces = new();
    readonly Dictionary<Vector3I, List<WorldSocket>> _hash = new();
    readonly HashSet<string> _slots = new();
    readonly HashSet<Rid> _aimExclude = new();
    int _serial;
    readonly RandomNumberGenerator _rng = new() { Seed = 4417 };

    // ------------------------------------------------------------------ kurulum

    /// <summary>worldRoot: parcalarin eklenecegi dugum. heightAt: dunya noktasinin (x,z) arazi yuksekligi (null olabilir).</summary>
    public void Init(Node3D worldRoot, Func<Vector3, float> heightAt, BuildKit kit = null)
    {
        _root = worldRoot;
        _heightAt = heightAt;
        Kit = kit ?? BuildKit.Load();
        if (GetParent() == null) worldRoot.AddChild(this);
        Name = "BuildSystem";
        _piecesRoot = new Node3D { Name = "BuildPieces" };
        worldRoot.AddChild(_piecesRoot);
        _ghostOk = GhostMaterial(new Color(0.45f, 1f, 0.55f, 0.42f));
        _ghostBad = GhostMaterial(new Color(1f, 0.35f, 0.3f, 0.42f));
        _sel = 0;
    }

    static StandardMaterial3D GhostMaterial(Color c) => new()
    {
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = c,
        CullMode = BaseMaterial3D.CullModeEnum.Back,
    };

    public void SetActive(bool on)
    {
        _active = on;
        if (_ghost != null) _ghost.Visible = on && _current != null;
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
        if (_current != null && _current.Mode == "snap") _rotIndex += dir;
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

    // ------------------------------------------------------------------ nisan ve hayalet

    public void UpdateAim(Vector3 rayFrom, Vector3 rayDir)
    {
        if (!_active || Selected == null || !IsInsideTree()) return;
        rayDir = rayDir.Normalized();
        var hit = AimHit(rayFrom, rayDir);
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
        }
        _ghost.Visible = _active && _current != null;
        if (_current == null) return;
        _ghost.GlobalTransform = _current.Xf;
        var mat = _current.Valid ? _ghostOk : _ghostBad;
        foreach (var mi in _ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            mi.MaterialOverride = mat;
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
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

    BuildPlacement GroundPlacement(PieceDef def, Vector3 hit)
    {
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(_rotStep * RotateStepDeg));
        var xf = new Transform3D(basis, new Vector3(hit.X, 0, hit.Z));
        bool ok = GroundFit(def, ref xf, out string why, adjustY: true);
        var p = Evaluate(def, xf, "ground");
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

    BuildPlacement Evaluate(PieceDef def, Transform3D xf, string mode)
    {
        var p = new BuildPlacement { Def = def, Xf = xf, Mode = mode, Valid = true, SlotKey = SlotKeyOf(def, xf) };
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
        string why = "";
        if (mode == "ground") p.Grounded = true;
        else if (def.Ground != null)
        {
            var x = xf;
            p.Grounded = GroundFit(def, ref x, out why, adjustY: false);
            if (!p.Grounded && def.Origin == "top_center")
            {
                p.Valid = false;
                p.Reason = why;
                return p;
            }
        }
        if (p.Grounded) p.Span = 0;
        else if (p.Supporters.Count > 0)
        {
            int m = p.Supporters.Min(s => s.Span);
            p.Span = m == int.MaxValue ? int.MaxValue : m + (def.Horizontal ? 1 : 0);
        }
        if (p.Span == int.MaxValue) { p.Valid = false; p.Reason = "destek yok"; }
        else if (p.Span > Kit.MaxSpan) { p.Valid = false; p.Reason = $"açıklık {p.Span} > {Kit.MaxSpan}"; }
        return p;
    }

    // ------------------------------------------------------------------ yerlestirme, sokme, dusme

    /// <summary>Gecerli hayaleti yerlestirir; olmadiysa null.</summary>
    public BuildPiece TryPlace()
    {
        if (!_active || _current == null || !_current.Valid) return null;
        var piece = Spawn(_current);
        _current = null;
        if (_ghost != null) _ghost.Visible = false;
        return piece;
    }

    BuildPiece Spawn(BuildPlacement p)
    {
        var def = p.Def;
        var piece = new BuildPiece
        {
            Def = def,
            Name = $"{def.Id}_{++_serial}",
            CollisionLayer = 1u | BuildLayer,
            CollisionMask = 0,
            Hp = Kit.PieceHp,
            Grounded = p.Grounded,
            SlotKey = p.SlotKey,
        };
        _piecesRoot.AddChild(piece);
        piece.GlobalTransform = p.Xf;
        piece.Model = Kit.Instantiate(def);
        piece.AddChild(piece.Model);
        foreach (var c in def.Colliders)
        {
            var (shape, lxf) = BuildKit.MakeShape(c);
            piece.AddChild(new CollisionShape3D { Shape = shape, Transform = lxf, Name = c.Node != null ? "Col_" + c.Node : "Col" });
        }
        foreach (var s in def.Sockets)
        {
            var ws = new WorldSocket { Piece = piece, Def = s, Pos = p.Xf * s.Pos, Dir = (p.Xf.Basis * s.Dir).Normalized() };
            piece.WorldSockets.Add(ws);
            var k = HashKey(ws.Pos);
            if (!_hash.TryGetValue(k, out var list)) _hash[k] = list = new List<WorldSocket>();
            list.Add(ws);
        }
        foreach (var s in p.Supporters) { piece.SupportedBy.Add(s); s.Supports.Add(piece); }
        foreach (var o in p.Supported) { o.SupportedBy.Add(piece); piece.Supports.Add(o); }
        _slots.Add(piece.SlotKey);
        _pieces.Add(piece);
        RecomputeSpans();
        Placed?.Invoke(piece);
        return piece;
    }

    /// <summary>Komut dosyasi / plan modu icin: hayaletsiz, kurallari sinayarak yerlestirir (gecersizse null, neden reason'da).</summary>
    public BuildPiece PlaceAt(string id, Transform3D xf, out string reason)
    {
        var def = Kit[id];
        reason = def == null ? "parça yok" : "";
        if (def == null) return null;
        var p = Evaluate(def, xf, "snap");
        if (!p.Valid) { reason = p.Reason; return null; }
        return Spawn(p);
    }

    /// <summary>Nisan isininin vurdugu parca (sokme, onarim, boyama icin).</summary>
    public BuildPiece PieceAt(Vector3 rayFrom, Vector3 rayDir)
    {
        var q = PhysicsRayQueryParameters3D.Create(rayFrom, rayFrom + rayDir.Normalized() * MaxReach, BuildLayer);
        var r = GetWorld3D().DirectSpaceState.IntersectRay(q);
        return r.Count > 0 ? r["collider"].As<GodotObject>() as BuildPiece : null;
    }

    /// <summary>Parcayi soker (node parcanin kendisi ya da herhangi bir cocugu olabilir). Desteksiz kalanlar duser.</summary>
    public bool Remove(Node node)
    {
        var piece = FindPiece(node);
        if (piece == null || !_pieces.Contains(piece)) return false;
        Detach(piece);
        Removed?.Invoke(piece);
        piece.QueueFree();
        foreach (var f in RecomputeSpans()) Fall(f);
        return true;
    }

    static BuildPiece FindPiece(Node n)
    {
        while (n != null && n is not BuildPiece) n = n.GetParent();
        return n as BuildPiece;
    }

    void Detach(BuildPiece piece)
    {
        foreach (var s in piece.SupportedBy) s.Supports.Remove(piece);
        foreach (var o in piece.Supports) o.SupportedBy.Remove(piece);
        piece.SupportedBy.Clear();
        piece.Supports.Clear();
        foreach (var ws in piece.WorldSockets)
            if (_hash.TryGetValue(HashKey(ws.Pos), out var list)) list.Remove(ws);
        _slots.Remove(piece.SlotKey);
        _pieces.Remove(piece);
    }

    /// <summary>Aciklik (span) hesabi: zemine oturanlar 0; destekten desteğe dusey parca +0, yatay (zemin/cati/kiris) +1. Sinir asan ya da ulasilamayan parcalari dondurur.</summary>
    public List<BuildPiece> RecomputeSpans()
    {
        foreach (var p in _pieces) p.Span = int.MaxValue;
        var dq = new LinkedList<BuildPiece>();
        foreach (var p in _pieces.Where(p => p.Grounded)) { p.Span = 0; dq.AddLast(p); }
        while (dq.Count > 0)
        {
            var p = dq.First.Value;
            dq.RemoveFirst();
            foreach (var q in p.Supports)
            {
                int w = q.Def.Horizontal ? 1 : 0;
                int ns = p.Span + w;
                if (ns > Kit.MaxSpan || ns >= q.Span) continue;
                q.Span = ns;
                if (w == 0) dq.AddFirst(q); else dq.AddLast(q);
            }
        }
        return _pieces.Where(p => p.Span > Kit.MaxSpan).ToList();
    }

    void Fall(BuildPiece piece)
    {
        if (!_pieces.Contains(piece)) return;
        Detach(piece);
        var xf = piece.GlobalTransform;
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
        Fell?.Invoke(piece, body);
        piece.QueueFree();
        GetTree().CreateTimer(FallLifetime).Timeout += () => { if (IsInstanceValid(body)) body.QueueFree(); };
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
