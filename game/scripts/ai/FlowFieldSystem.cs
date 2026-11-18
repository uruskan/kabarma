using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Akis alani Godot katmani (TEKNIK 8.1, yolbulma akisi): Sim'deki FlowFieldSet'i dunyaya baglar.
/// · Pencere Ocak'in cevresinde kurulur (Ocak yoksa kurulmaz; karakol/Maya Feneri icin Recenter).
/// · Zemin World.HeightAt ve World.IsWater'dan; yapi parcalari (BuildSystem), istasyonlar ve esyalar iz olarak
///   (parca turu flowfield.json pieceKinds'ten), RescanSeconds'ta bir ve yapi olayinda fark taranir.
/// · Hedefler: "kazan" (Ocak), "crop" (ekili her kare), "crop:&lt;aile&gt;" (o ailenin ekili kareleri). Baska anahtar
///   SetGoals ile verilir.
/// · Yalniz host'ta (ve tek oyuncuda) isler: istemcide yaratik kukladir. Turetilmis veri, kayda yazilmaz.
/// Baska akislar: Sample/Steer ile yon, Blocker ile kirilacak engel (OwnerNode ile dugum), StampBox/Unstamp ile kendi
/// engelleri (tuzak, kule, kapi), FindPath ile Holding A*.
/// </summary>
public partial class FlowFieldSystem : Node
{
    readonly Director _d;
    FlowFieldFile Def => _d.Data.FlowField;
    public FlowFieldSet Fields { get; private set; }
    /// <summary>Bu sistemin kendi koydugu izler (parca, istasyon, esya): sahip -> imza (degisince yeniden koyulur).</summary>
    readonly Dictionary<ulong, string> _mine = new();
    readonly HashSet<string> _used = new();
    float _scanT;
    bool _dirty = true;
    Vector3 _center;
    public int Scans { get; private set; }
    /// <summary>Son taramanin ve son Tick'in suresi (ms; olcum).</summary>
    public double LastScanMs { get; private set; }
    public double LastTickMs { get; private set; }
    /// <summary>Son Tick'te guncellenen alan sayisi (0: is yoktu; olcum).</summary>
    public int LastTickWork { get; private set; }

    public FlowFieldSystem(Director d) { _d = d; Name = "FlowField"; }

    public override void _Ready()
    {
        if (_d.Build?.System is { } sys)
        {
            sys.Placed += _ => _dirty = true;
            sys.Removed += _ => _dirty = true;
            sys.Fell += (_, _) => _dirty = true;
            sys.Upgraded += (_, _) => _dirty = true;
        }
    }

    /// <summary>Host (ya da tek oyuncu) mu: istemcide alan kurulmaz.</summary>
    bool Active => !_d.NetClient;

    /// <summary>Pencereyi c merkezli yeniden kurar (karakolda Maya Feneri, GDD 14.6). Izler ve hedefler yeniden taranir.</summary>
    public void Recenter(Vector3 c)
    {
        _center = c;
        var grid = new FlowGrid(Def, c.X, c.Z);
        var w = _d.World;
        grid.BuildTerrain((x, z) => w.HeightAt(x, z), (x, z) => w.IsWater(new Vector3(x, w.HeightAt(x, z), z)));
        Fields = new FlowFieldSet(grid);
        _mine.Clear();
        _dirty = true;
        GD.Print($"FLOW window center={c.X:0},{c.Z:0} size={grid.Size}");
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        if (Fields == null)
        {
            if (_d.Ocak == null) return;
            Recenter(_d.OcakPos);
        }
        _scanT -= (float)delta;
        if (_scanT <= 0 || _dirty)
        {
            _scanT = Def.RescanSeconds;
            ulong t0 = Time.GetTicksUsec();
            Scan();
            LastScanMs = (Time.GetTicksUsec() - t0) / 1000.0;
            _dirty = false;
            Scans++;
        }
        ulong t1 = Time.GetTicksUsec();
        LastTickWork = Fields.Tick();
        LastTickMs = (Time.GetTicksUsec() - t1) / 1000.0;
    }

    // ---------------------------------------------------------------- sorgu (baska akislar)

    /// <summary>Anahtarin alaninda pos'taki ornek. Ilk sorgu anahtari kullanima alir (hedefleri taranir ve hesaplanir).</summary>
    public FlowSample Sample(string key, Vector3 pos)
    {
        if (Fields == null) return default;
        if (_used.Add(key)) { Goals(key); }
        return Fields.Sample(key, pos.X, pos.Z);
    }

    /// <summary>Yuruyus yonu (y = 0, birim); pencere disi ya da yol yoksa null (cagiran dogrudan hedefe yurur).</summary>
    public Vector3? Steer(Vector3 pos, string key)
    {
        var s = Sample(key, pos);
        if (!s.Valid || s.AtGoal) return null;
        return new Vector3(s.DirX, 0, s.DirZ);
    }

    /// <summary>Iz sahibinin dugumu (bu sistemin koydugu parca/istasyon/esya izleri icin); yoksa null.</summary>
    public static Node3D OwnerNode(ulong owner) => GodotObject.InstanceFromId(owner) as Node3D is { } n && GodotObject.IsInstanceValid(n) ? n : null;

    /// <summary>Ozel anahtarin hedefleri (kabarma3: kazan, Holding: kamp). Taramada bu anahtarlarin hedefi degistirilmez.</summary>
    public void SetGoals(string key, IEnumerable<Vector3> world)
    {
        if (Fields == null) return;
        _custom.Add(key);
        _used.Add(key);
        Fields.SetGoals(key, world.Select(p => (p.X, p.Z)));
    }

    readonly HashSet<string> _custom = new();

    /// <summary>Baska akisin engeli (tuzak, kule, kapi): donuk kutu (merkez, yari boyutlar x/z, yon) olarak iz.</summary>
    public void StampBox(ulong owner, string kind, Vector3 center, float halfX, float halfZ, float yaw)
    {
        if (Fields == null) return;
        Fields.Grid.Stamp(owner, kind, Fields.Grid.CellsUnderBox(center.X, center.Z, halfX, halfZ, yaw));
    }

    public void Unstamp(ulong owner) => Fields?.Grid.Unstamp(owner);

    /// <summary>Holding A*: dunya noktalarinin listesi (hucre merkezleri, zemine oturtulmus); yol yoksa bos.</summary>
    public List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        var l = new List<Vector3>();
        if (Fields == null) return l;
        foreach (int c in Fields.FindPath(from.X, from.Z, to.X, to.Z))
        {
            var (x, z) = Fields.Grid.Center(c);
            l.Add(_d.World.Ground(new Vector3(x, 0, z)));
        }
        return l;
    }

    // ---------------------------------------------------------------- tarama

    void Scan()
    {
        var g = Fields.Grid;
        var seen = new HashSet<ulong>();
        if (_d.Build?.System is { } sys)
            foreach (var p in sys.Pieces)
            {
                if (!GodotObject.IsInstanceValid(p) || p.IsQueuedForDeletion() || p.Planned || p.Def == null) continue;
                StampPiece(p, seen);
            }
        foreach (var st in _d.Stations) if (GodotObject.IsInstanceValid(st)) StampSmall(st, "station", 0.7f, seen);
        foreach (var f in _d.Furniture) if (GodotObject.IsInstanceValid(f)) StampSmall(f, "station", 0.5f, seen);
        foreach (var o in _mine.Keys.Where(o => !seen.Contains(o)).ToList()) { g.Unstamp(o); _mine.Remove(o); }
        foreach (var key in _used) if (!_custom.Contains(key)) Goals(key);
    }

    void StampPiece(BuildPiece p, HashSet<ulong> seen)
    {
        string kind = Def.StampOf(p.Def.Kind);
        if (kind is FlowFieldFile.Walk or FlowFieldFile.Ignore) return;
        var xf = p.GlobalTransform;
        var lc = (p.Def.BoundsMin + p.Def.BoundsMax) * 0.5f;
        var half = (p.Def.BoundsMax - p.Def.BoundsMin) * 0.5f;
        var scale = xf.Basis.Scale;
        var c = xf * lc;
        float hy = half.Y * Mathf.Abs(scale.Y);
        float ground = _d.World.HeightAt(c.X, c.Z);
        if (c.Y - hy > ground + Def.BlockHeight || c.Y + hy < ground) return; // ust kat ya da gomulu: zeminde engel degil
        float yaw = xf.Basis.GetEuler().Y;
        ulong id = p.GetInstanceId();
        Put(id, kind, c, half.X * Mathf.Abs(scale.X), half.Z * Mathf.Abs(scale.Z), yaw, seen);
    }

    void StampSmall(Node3D n, string kind, float half, HashSet<ulong> seen) =>
        Put(n.GetInstanceId(), kind, n.GlobalPosition, half, half, n.GlobalRotation.Y, seen);

    void Put(ulong id, string kind, Vector3 c, float hx, float hz, float yaw, HashSet<ulong> seen)
    {
        seen.Add(id);
        // Imza: tur + 1 cm'lik konum + 1 derecelik yon; degismediyse yeniden taranmaz.
        string sig = $"{kind}|{Mathf.RoundToInt(c.X * 100)}|{Mathf.RoundToInt(c.Z * 100)}|{Mathf.RoundToInt(hx * 100)}|{Mathf.RoundToInt(hz * 100)}|{Mathf.RoundToInt(Mathf.RadToDeg(yaw))}";
        if (_mine.TryGetValue(id, out var old) && old == sig) return;
        _mine[id] = sig;
        Fields.Grid.Stamp(id, kind, Fields.Grid.CellsUnderBox(c.X, c.Z, hx, hz, yaw));
    }

    /// <summary>Yerlesik anahtarlarin hedefleri: kazan = Ocak; crop = ekili her kare; crop:aile = o aile.</summary>
    void Goals(string key)
    {
        if (_custom.Contains(key)) return;
        if (key == "kazan") { Fields.SetGoals(key, _d.Ocak != null ? new[] { (_d.OcakPos.X, _d.OcakPos.Z) } : System.Array.Empty<(float, float)>()); return; }
        if (key == "crop" || key.StartsWith("crop:"))
        {
            string fam = key.Length > 5 ? key[5..] : null;
            var views = _d.FarmView?.Views ?? Enumerable.Empty<PlotView>();
            Fields.SetGoals(key, views.Where(v => GodotObject.IsInstanceValid(v) && v.Plot.Planted && !v.Plot.Wild
                    && (fam == null || _d.Data.Crop(v.Plot.Crop!)?.Family == fam))
                .Select(v => (v.GlobalPosition.X, v.GlobalPosition.Z)));
        }
    }
}
