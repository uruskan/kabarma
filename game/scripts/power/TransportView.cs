using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Tasimanin gorunusu (M4 tasima; GDD 11.3): bantta ve boruda giden nesneler, drone'lar (iz birakir), teleferik halati ve
/// yukleri. Kendi durumu yok: hepsi Sim'in TransportState'inden her kare cizilir (yer tutucu kureler ve kutular).
/// </summary>
public partial class TransportView : Node3D
{
    readonly Director _d;
    readonly List<MeshInstance3D> _items = new();
    readonly List<MeshInstance3D> _drones = new();
    readonly List<MeshInstance3D> _cars = new();
    readonly Dictionary<(int, int), (MeshInstance3D mi, Vector3 a, Vector3 b)> _ropes = new();
    readonly Dictionary<string, StandardMaterial3D> _mats = new();
    float _trailT;

    /// <summary>Test ve kanit: son karede cizilen nesne, drone, yuk sayisi.</summary>
    public int ShownItems { get; private set; }
    public int ShownDrones { get; private set; }
    public int ShownCars { get; private set; }

    public TransportView(Director d) { _d = d; Name = "TransportView"; }

    PowerNet Net => _d.State.Power;
    PowerFile F => _d.Data.Power;

    StandardMaterial3D Mat(string key, Color c)
    {
        if (!_mats.TryGetValue(key, out var m))
            _mats[key] = m = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.6f, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 0.25f };
        return m;
    }

    static Color ItemColor(string item)
    {
        uint h = 2166136261;
        foreach (char ch in item) h = (h ^ ch) * 16777619;
        return Color.FromHsv((h % 360) / 360f, 0.55f, 0.95f);
    }

    MeshInstance3D Make(List<MeshInstance3D> pool, int i, Mesh mesh)
    {
        while (pool.Count <= i)
        {
            var mi = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(mi);
            pool.Add(mi);
        }
        return pool[i];
    }

    static readonly SphereMesh ItemMesh = new() { Radius = 0.12f, Height = 0.24f, RadialSegments = 8, Rings = 4 };
    static readonly BoxMesh DroneMesh = new() { Size = new Vector3(0.5f, 0.14f, 0.5f) };
    static readonly BoxMesh CarMesh = new() { Size = new Vector3(0.6f, 0.5f, 0.6f) };

    float GroundOf(PowerNode n) => n.Y - PowerVisuals.Socket(n.Def).Y;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var tr = Net.Transport;
        // ---- bant ve boru uzerindeki nesneler ----
        int k = 0;
        foreach (var (id, lane) in tr.Lanes)
        {
            var n = Net.Get(id);
            var d = n == null ? null : F.Def(n.Def);
            if (n == null || d == null || lane.Items.Count == 0) continue;
            var (dx, dz) = TransportState.Dir(n.Yaw);
            float y = GroundOf(n) + (d.Fluid ? 0.28f : 0.3f);
            foreach (var it in lane.Items)
            {
                var mi = Make(_items, k++, ItemMesh);
                mi.Visible = true;
                mi.MaterialOverride = Mat(it.Item, ItemColor(it.Item));
                mi.GlobalPosition = new Vector3(n.X + dx * it.T, y, n.Z + dz * it.T);
            }
        }
        for (int i = k; i < _items.Count; i++) _items[i].Visible = false;
        ShownItems = k;

        // ---- drone'lar ----
        _trailT -= dt;
        bool trail = _trailT <= 0;
        if (trail) _trailT = 0.1f;
        int dn = 0;
        foreach (var (sid, st) in tr.Drones.Stations)
        {
            var home = Net.Get(sid);
            if (home == null) continue;
            float gy = GroundOf(home);
            foreach (var dr in st.Fleet)
            {
                if (dr.Phase == 0 && !Net.Get(sid)!.Powered) { }
                var mi = Make(_drones, dn++, DroneMesh);
                bool away = dr.Phase != 0;
                float h = away ? 3.2f + Mathf.Sin((float)Time.GetTicksMsec() / 120f + dn) * 0.1f : 1.5f;
                mi.Visible = true;
                mi.MaterialOverride = Mat(away ? "drone_on" : "drone_off", away ? Palette.MayaTeal : Palette.DarkGrey.Lerp(Palette.CoopBlue, 0.4f));
                var gp = _d.World != null ? _d.World.Ground(new Vector3(dr.X, 0, dr.Z)).Y : gy;
                mi.GlobalPosition = new Vector3(dr.X, (away ? gp : gy) + h, dr.Z);
                if (away && trail) Fx.Burst(mi.GlobalPosition, dr.Qty > 0 ? ItemColor(dr.Item) : Palette.MayaTeal, 2, 0.4f, 0.05f, 0.7f);
            }
        }
        for (int i = dn; i < _drones.Count; i++) _drones[i].Visible = false;
        ShownDrones = dn;

        // ---- teleferik: halat ve yukler ----
        var live = new HashSet<(int, int)>();
        int cn = 0;
        foreach (var line in tr.Ropeways.Lines)
        {
            var a = Net.Get(line.A); var b = Net.Get(line.B);
            if (a == null || b == null) continue;
            var pa = new Vector3(a.X, a.Y, a.Z); var pb = new Vector3(b.X, b.Y, b.Z);
            var key = (line.A, line.B);
            live.Add(key);
            if (!_ropes.TryGetValue(key, out var rope) || rope.a.DistanceSquaredTo(pa) > 1e-3f || rope.b.DistanceSquaredTo(pb) > 1e-3f)
            {
                rope.mi?.QueueFree();
                var mi = new MeshInstance3D { Mesh = PowerVisuals.Cable(pa, pb, false, 24, 5), MaterialOverride = Mat("rope", Palette.DarkGrey.Lerp(Palette.Wood, 0.4f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(mi);
                _ropes[key] = (mi, pa, pb);
            }
            foreach (var c in line.Cars)
            {
                var mi = Make(_cars, cn++, CarMesh);
                mi.Visible = true;
                mi.MaterialOverride = Mat(c.Item, ItemColor(c.Item));
                float t = c.T, sag = Mathf.Min(0.6f, pa.DistanceTo(pb) * 0.035f);
                mi.GlobalPosition = pa.Lerp(pb, t) + Vector3.Down * (4f * sag * t * (1f - t) + 0.45f);
            }
        }
        for (int i = cn; i < _cars.Count; i++) _cars[i].Visible = false;
        ShownCars = cn;
        foreach (var key in _ropes.Keys.Where(x => !live.Contains(x)).ToList()) { _ropes[key].mi.QueueFree(); _ropes.Remove(key); }
    }
}
