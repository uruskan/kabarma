using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Guc agi ve temel sinyalin Godot katmani (GDD 11.1, 11.2). Kurallar Sim'de (GameState.Power: PowerNet); burasi
/// parcalarin ve kablolarin gorunusunu, Kablo esyasiyla elle kablo cekmeyi (sol tik soket A, sol tik soket B; R: guc /
/// sinyal), E panelini, basinc plakasi ve Hayvan Carki dolulugunu ve guc isteyen istasyonlarin ucunu (PowerPort) tasir.
/// Kendi durumu yok: dugumler ve kablolar Sim'deki kimliklerden kurulur (kayit yuklenince Rebuild).
/// </summary>
public partial class PowerSystem : Node3D
{
    readonly Director _d;
    readonly Dictionary<int, Node3D> _owners = new();
    readonly Dictionary<(int, int, bool), MeshInstance3D> _cables = new();
    readonly Dictionary<(int, int, bool), (Vector3, Vector3)> _cableEnds = new();
    StandardMaterial3D _powerMat, _powerLiveMat, _signalMat, _signalLiveMat, _previewOk, _previewBad;
    MeshInstance3D _preview;
    int _pick = -1;
    string _lastReason;

    /// <summary>Kablo kipinde sinyal teli mi (R degistirir).</summary>
    public bool SignalMode { get; private set; }
    /// <summary>Elde Kablo varken secili ilk soket (yoksa -1).</summary>
    public int Picked => _pick;

    public PowerSystem(Director d) { _d = d; Name = "PowerSystem"; }

    public PowerFile F => _d.Data.Power;
    public PowerNet Net => _d.State.Power;
    public IEnumerable<PowerDevice> Devices => _owners.Values.OfType<PowerDevice>().Where(IsInstanceValid);

    public override void _Ready()
    {
        _powerMat = Mat(Palette.DarkGrey.Lerp(Palette.Shadow, 0.3f), 0f);
        _powerLiveMat = Mat(Palette.DarkGrey.Lerp(Palette.Warning, 0.25f), 0.35f);
        _signalMat = Mat(Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.4f), 0f);
        _signalLiveMat = Mat(Palette.CoopBlue.Lerp(Palette.MayaTeal, 0.4f), 0.8f);
        _previewOk = Mat(new Color(0.45f, 1f, 0.55f, 0.7f), 0.6f, true);
        _previewBad = Mat(new Color(1f, 0.35f, 0.3f, 0.7f), 0.6f, true);
    }

    static StandardMaterial3D Mat(Color c, float glow, bool transparent = false) => new()
    {
        AlbedoColor = c, Roughness = 0.7f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        EmissionEnabled = glow > 0, Emission = c, EmissionEnergyMultiplier = glow,
        Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        ShadingMode = transparent ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
    };

    // ---------------------------------------------------------------- kurma, soku, istasyon ucu

    public bool IsDevice(string id) => F.IsDevice(id);

    /// <summary>Kemerden kurulan guc parcasi (Capa.Insa TryPlace).</summary>
    public PowerDevice Place(PlaceableDef def, Vector3 at, float yaw)
    {
        var pdef = F.Def(def.Id);
        var node = Net.Add(F, def.Id, at.X, at.Y, at.Z);
        var dev = Spawn(def, pdef, node.Id, at, yaw);
        Sfx.Play("place", at, 2f);
        Fx.Burst(at + Vector3.Up * 0.3f, Palette.Wood, 18, 4f, 0.1f, 0.7f);
        GD.Print($"PLACED {def.Id} at {at} power={node.Id}");
        Events.RaiseStationPlaced(def.Id);
        return dev;
    }

    PowerDevice Spawn(PlaceableDef def, PowerDeviceDef pdef, int id, Vector3 at, float yaw)
    {
        var dev = new PowerDevice(_d, def, pdef, id);
        AddChild(dev);
        dev.GlobalPosition = at;
        dev.Rotation = new Vector3(0, yaw, 0);
        _owners[id] = dev;
        var n = Net.Get(id);
        var s = dev.SocketWorld;
        if (n != null) { n.X = s.X; n.Y = s.Y; n.Z = s.Z; n.Yaw = yaw; } // kablo boyu soketten olculur
        return dev;
    }

    /// <summary>Guc isteyen istasyonu aga baglar (PowerPort). power.json "consumers"ta yoksa null (guc istemez).</summary>
    public PowerPort Attach(Node3D owner, string placeableId)
    {
        float w = F.ConsumerWatts(placeableId);
        if (w <= 0 || owner == null) return null;
        var s = owner.GlobalTransform * PowerVisuals.Socket(placeableId);
        // Kayittan donen istasyon: ayni yerdeki sahipsiz ucunu (kablolariyla) geri alir.
        var n = Net.Nodes.FirstOrDefault(x => x.Def == placeableId && !_owners.ContainsKey(x.Id) && new Vector3(x.X, x.Y, x.Z).DistanceTo(s) < 0.6f)
            ?? Net.Add(F, placeableId, s.X, s.Y, s.Z);
        _owners[n.Id] = owner;
        return new PowerPort(this, n.Id, w);
    }

    /// <summary>Dugumu ve kablolarini siler; <paramref name="refund"/> ise kablolar cantaya doner.</summary>
    public void Unregister(int id, bool refund)
    {
        int freed = Net.Remove(id);
        if (refund && freed > 0) Refund(freed);
        _owners.Remove(id);
        if (_pick == id) _pick = -1;
    }

    void Refund(int cables)
    {
        int added = _d.State.Backpack.Add(F.CableItem, cables);
        if (added < cables) _d.Hud.Hint("Çanta dolu: Kablo yere düşmedi, kayboldu", 2f);
    }

    /// <summary>Parcayi soker: esya ve kablolari cantaya doner.</summary>
    public void Dismantle(PowerDevice dev)
    {
        Unregister(dev.NodeId, refund: true);
        _d.State.Backpack.Add(dev.Def.Id, 1);
        Fx.Burst(dev.GlobalPosition + Vector3.Up * 0.4f, Palette.Wood, 16, 3f, 0.08f, 0.6f);
        Sfx.Play("pickup", dev.GlobalPosition, -4f);
        GD.Print($"POWER dismantle {dev.Def.Id}");
        dev.QueueFree();
        _d.Hud.HideMenu();
    }

    /// <summary>Kayit yuklenince: Sim'deki parcalardan gorunusu yeniden kurar (istasyon uclari istasyonla gelir).</summary>
    public void Rebuild()
    {
        foreach (var dev in Devices.ToList()) dev.QueueFree();
        foreach (var k in _owners.Where(kv => kv.Value is PowerDevice).Select(kv => kv.Key).ToList()) _owners.Remove(k);
        foreach (var c in _cables.Values) c.QueueFree();
        _cables.Clear(); _cableEnds.Clear();
        foreach (var n in Net.Nodes)
        {
            var pdef = F.Def(n.Def);
            var def = _d.Data.Tool.Placeables.FirstOrDefault(p => p.Id == n.Def);
            if (pdef == null || def == null || !F.IsDevice(n.Def)) continue;
            var at = new Vector3(n.X, n.Y, n.Z) - new Basis(Vector3.Up, n.Yaw) * PowerVisuals.Socket(n.Def);
            Spawn(def, pdef, n.Id, at, n.Yaw);
        }
    }

    // ---------------------------------------------------------------- her kare

    readonly Dictionary<Station, PowerPort> _stationPorts = new();

    /// <summary>
    /// Guc isteyen istasyonlar (research.json "watts"): kurulunca uc acilir ve StationQueue.PowerOk baglanir (zincir'in
    /// kancasi; istasyon dosyasina dokunulmaz). Sira yalniz o istasyonun ucu beslenirken ilerler; is yokken talep yok.
    /// Ayni turden iki istasyon ayni sirayi paylasir: sirayi isleten (ilk) istasyonun ucu gecerli.
    /// </summary>
    void SyncStations()
    {
        foreach (var st in _d.Stations)
        {
            if (!IsInstanceValid(st) || st.Def.Station == null || _stationPorts.ContainsKey(st)) continue;
            var port = Attach(st, st.Def.Station);
            if (port == null) continue;
            _stationPorts[st] = port;
            var q = st.Queue;
            q.PowerOk = () =>
            {
                var first = _d.Stations.FirstOrDefault(s => IsInstanceValid(s) && s.Queue == q);
                return first != null && _stationPorts.TryGetValue(first, out var p) && p.Powered;
            };
            GD.Print($"POWER station {st.Def.Id} {port.Watts:0} W node={port.NodeId}");
        }
        foreach (var (st, port) in _stationPorts.ToList())
        {
            if (!IsInstanceValid(st) || !st.IsInsideTree()) { _stationPorts.Remove(st); continue; }
            var n = Net.Get(port.NodeId);
            if (n != null) n.Wants = st.Queue.Active != null;
        }
    }

    /// <summary>Istasyonun guc ucu (guc istemiyorsa null).</summary>
    public PowerPort PortOf(Station st) => st != null && _stationPorts.TryGetValue(st, out var p) ? p : null;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        SyncStations();
        Occupancy();
        _d.State.TickPower(dt);
        SyncCables();
    }

    /// <summary>Basinc Plakasi ve Hayvan Carki: ustunde / icinde kim var (oyuncu, Gidak; "power_pressers" grubu).</summary>
    void Occupancy()
    {
        var who = new List<Vector3> { _d.Player.GlobalPosition };
        foreach (var g in GetTree().GetNodesInGroup("gidak").OfType<Node3D>()) if (g.Visible) who.Add(g.GlobalPosition);
        foreach (var g in GetTree().GetNodesInGroup("power_pressers").OfType<Node3D>()) who.Add(g.GlobalPosition);
        var animals = GetTree().GetNodesInGroup("gidak").OfType<Node3D>().Where(g => g.Visible).Select(g => g.GlobalPosition).ToList();
        foreach (var dev in Devices)
        {
            var n = Net.Get(dev.NodeId);
            if (n == null) continue;
            var p = dev.GlobalPosition;
            if (dev.PDef.Kind == "plate")
                n.Occupied = who.Any(w => new Vector2(w.X - p.X, w.Z - p.Z).Length() < F.PlateRadius && Mathf.Abs(w.Y - p.Y) < 1.5f);
            else if (dev.PDef.Source == "animal")
                n.Occupied = animals.Any(w => new Vector2(w.X - p.X, w.Z - p.Z).Length() < dev.PDef.AnimalRadius); // hayvan akisi: rol ile baglanacak
        }
    }

    Vector3 SocketOf(int id)
    {
        if (_owners.TryGetValue(id, out var o) && IsInstanceValid(o))
            return o is PowerDevice dev ? dev.SocketWorld : o.GlobalTransform * PowerVisuals.Socket(Net.Get(id)?.Def ?? "");
        var n = Net.Get(id);
        return n == null ? Vector3.Zero : new Vector3(n.X, n.Y, n.Z);
    }

    void SyncCables()
    {
        var live = new HashSet<(int, int, bool)>();
        foreach (var l in Net.Links)
        {
            var key = (l.A, l.B, l.Signal);
            live.Add(key);
            var a = SocketOf(l.A); var b = SocketOf(l.B);
            if (!_cables.TryGetValue(key, out var mi))
            {
                mi = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(mi);
                _cables[key] = mi;
            }
            if (!_cableEnds.TryGetValue(key, out var ends) || ends.Item1.DistanceSquaredTo(a) > 1e-4f || ends.Item2.DistanceSquaredTo(b) > 1e-4f)
            {
                mi.Mesh = PowerVisuals.Cable(a, b, l.Signal);
                _cableEnds[key] = (a, b);
            }
            bool on = l.Signal ? (Net.Get(l.A)?.Signal ?? 0) > 0 : Net.StatsOf(l.A) is { } st && (st.SupplyW > 0 || st.StoredWh > 0);
            mi.MaterialOverride = l.Signal ? (on ? _signalLiveMat : _signalMat) : (on ? _powerLiveMat : _powerMat);
        }
        foreach (var k in _cables.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _cables[k].QueueFree();
            _cables.Remove(k);
            _cableEnds.Remove(k);
        }
        foreach (var k in _owners.Where(kv => !IsInstanceValid(kv.Value)).Select(kv => kv.Key).ToList()) Unregister(k, refund: false);
    }

    // ---------------------------------------------------------------- Kablo eldeyken

    /// <summary>R: guc kablosu / sinyal teli.</summary>
    public void ToggleMode()
    {
        SignalMode = !SignalMode;
        _pick = -1;
        Sfx.Play("place", null, -16f);
        _d.Hud.Hint(SignalMode ? "Sinyal teli: Düğme / Basınç Plakası → Şalter / Lamba" : "Güç kablosu", 1.2f);
    }

    /// <summary>Nisan noktasina en yakin soket (oyuncunun 7 m'si icinde, nisana 1,6 m'den yakin); yoksa -1.</summary>
    public int TargetAt(Vector3 aim)
    {
        var p = _d.Player.GlobalPosition;
        int best = -1; float bd = 1.6f;
        foreach (var (id, owner) in _owners)
        {
            if (!IsInstanceValid(owner)) continue;
            var s = SocketOf(id);
            if (s.DistanceTo(p) > 7f) continue;
            // nisan isini sokete ya da govdeye: yatay uzaklik ve soket yuksekligi
            float d = Mathf.Min(s.DistanceTo(aim), new Vector2(owner.GlobalPosition.X - aim.X, owner.GlobalPosition.Z - aim.Z).Length() + 0.3f);
            if (d < bd) { bd = d; best = id; }
        }
        return best;
    }

    string NameOf(int id)
    {
        var n = Net.Get(id);
        if (n == null) return "?";
        return _d.Data.Tool.Placeables.FirstOrDefault(p => p.Id == n.Def)?.Name ?? n.Def;
    }

    /// <summary>
    /// Capa.TickHand'den (Kablo eldeyken) her kare: hedef soketi gosterir; sol tik ilk soketi secer, ikinci sol tik kabloyu
    /// ceker (1 Kablo harcar). Ayni sokete ya da bosluga tik secimi birakir. Tepki verildiyse true.
    /// </summary>
    public bool TickCableTool(Vector3 aim, bool firePressed)
    {
        int have = _d.State.Backpack.Count(F.CableItem);
        int target = TargetAt(aim);
        string mode = SignalMode ? "Sinyal teli" : "Güç kablosu";
        string reason = null;
        if (_pick >= 0 && Net.Get(_pick) == null) _pick = -1;
        if (_pick >= 0)
        {
            var a = SocketOf(_pick);
            var b = target >= 0 && target != _pick ? SocketOf(target) : aim;
            reason = target >= 0 && target != _pick ? Net.CanConnect(F, _pick, target, SignalMode) : a.DistanceTo(b) > F.CableMaxLength ? $"Kablo en fazla {F.CableMaxLength:0} m" : "";
            ShowPreview(a, b, string.IsNullOrEmpty(reason) && target >= 0);
            _d.Hud.Hint($"{mode} ×{have}  ·  {NameOf(_pick)} → {(target >= 0 && target != _pick ? NameOf(target) : "?")}  {a.DistanceTo(b):0.0}/{F.CableMaxLength:0} m{(string.IsNullOrEmpty(reason) ? "" : "  ·  " + reason)}  ·  {{act:fire}}: tak  ·  {{act:build_rotate}}: güç/sinyal", 0.2f);
        }
        else
        {
            HidePreview();
            _d.Hud.Hint($"{mode} ×{have}  ·  {(target >= 0 ? NameOf(target) + "  ·  {act:fire}: ilk soket" : "bir sokete nişan al")}  ·  {{act:build_rotate}}: güç/sinyal  ·  E: ayar / sök", 0.2f);
        }
        if (!firePressed) return false;
        if (_pick < 0)
        {
            if (target < 0) { Sfx.Play("hit", null, -12f); return true; }
            _pick = target;
            Sfx.Play("place", SocketOf(target), -8f);
            Fx.Burst(SocketOf(target), Palette.Warning, 6, 1.5f, 0.04f, 0.3f);
            GD.Print($"POWER pick {Net.Get(target)?.Def} ({target}) signal={SignalMode}");
            return true;
        }
        if (target < 0 || target == _pick) { _pick = -1; HidePreview(); Sfx.Play("place", null, -18f); return true; }
        if (have < 1) { _d.Hud.Hint("Kablo yok: Tezgâh → Kablo", 1.5f); Sfx.Play("hit", null, -10f); return true; }
        var err = Net.CanConnect(F, _pick, target, SignalMode);
        if (err != null) { _d.Hud.Hint(err, 1.5f); Sfx.Play("hit", null, -10f); _lastReason = err; GD.Print($"POWER refuse {err}"); return true; }
        var link = Net.Connect(F, _pick, target, SignalMode);
        _d.State.Backpack.Remove(F.CableItem, 1);
        var end = SocketOf(target);
        Fx.Burst(end, SignalMode ? Palette.MayaTeal : Palette.Warning, 14, 3f, 0.05f, 0.4f, true); // soket kivilcimi
        Sfx.Play("place", end, -2f);
        GD.Print($"POWER connect {Net.Get(link.A)?.Def}({link.A}) -> {Net.Get(link.B)?.Def}({link.B}) signal={link.Signal} len={link.Length:0.0}");
        _pick = -1;
        HidePreview();
        return true;
    }

    /// <summary>Son reddin nedeni (bot gunlugu).</summary>
    public string LastReason => _lastReason;

    /// <summary>Kablo elden inince: secim ve onizleme biter.</summary>
    public void CancelTool() { _pick = -1; HidePreview(); }

    void ShowPreview(Vector3 a, Vector3 b, bool ok)
    {
        if (_preview == null) { _preview = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(_preview); }
        _preview.Visible = true;
        _preview.Mesh = PowerVisuals.Cable(a, b, SignalMode, 10, 5);
        _preview.MaterialOverride = ok ? _previewOk : _previewBad;
    }

    void HidePreview() { if (_preview != null) _preview.Visible = false; }

    // ---------------------------------------------------------------- E

    /// <summary>
    /// E: yakindaki guc parcasi (istasyondan yakinsa). Dugmeye E basar; digerleri ayar paneli (ac/kapat, oncelik, kablolari
    /// sok, sok). Kablo eldeyken guc isteyen istasyona E de bu paneli acar. Isledi ise true.
    /// </summary>
    public bool Interact()
    {
        var p = _d.Player.GlobalPosition;
        bool cableInHand = _d.State.Belt.Held == F.CableItem;
        var dev = Devices.Where(x => x.InReach(p)).OrderBy(x => x.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        var st = _d.Stations.Where(s => s.InReach(p)).OrderBy(s => s.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        if (dev != null && (st == null || dev.GlobalPosition.DistanceTo(p) < st.GlobalPosition.DistanceTo(p)))
        {
            if (dev.PDef.Kind == "button" && !cableInHand) { Press(dev); return true; }
            OpenPanel(dev.NodeId);
            return true;
        }
        if (cableInHand && st != null)
        {
            var port = _owners.FirstOrDefault(kv => kv.Value == st).Key;
            if (port > 0) { OpenPanel(port); return true; }
        }
        return false;
    }

    public void Press(PowerDevice dev)
    {
        Net.Press(F, dev.NodeId);
        dev.Bounce();
        Sfx.Play("place", dev.GlobalPosition, -6f);
        GD.Print($"POWER press {dev.NodeId}");
    }

    public void OpenPanel(int id)
    {
        var n = Net.Get(id);
        if (n == null) return;
        var pdef = F.Def(n.Def);
        var dev = _owners.TryGetValue(id, out var o) ? o as PowerDevice : null;
        var items = new List<Hud.MenuItem>();
        void Re() => OpenPanel(id);
        if (pdef.Kind is "switch" or "consumer" && F.IsDevice(n.Def))
            items.Add(new Hud.MenuItem(n.On ? "Kapat" : "Aç", () => !(n.SignalWired && pdef.Kind == "switch"), () => { n.On = !n.On; Sfx.Play("place", null, -8f); GD.Print($"POWER toggle {id} on={n.On}"); Re(); }));
        if (pdef.Kind == "consumer")
            items.Add(new Hud.MenuItem($"Öncelik: {n.Priority}  (1 en önemli, 5 ilk kapanır)", () => true, () =>
            {
                n.Priority = n.Priority >= F.PriorityMax ? F.PriorityMin : n.Priority + 1;
                Sfx.Play("place", null, -12f);
                Re();
            }));
        if (pdef.Kind == "button" && dev != null) items.Add(new Hud.MenuItem("Bas", () => true, () => { Press(dev); Re(); }));
        int cables = Net.Links.Count(l => l.A == id || l.B == id);
        items.Add(new Hud.MenuItem($"Kablolarını sök ({cables})", () => cables > 0, () =>
        {
            int k = Net.Disconnect(id);
            Refund(k);
            Sfx.Play("pickup", null, -6f);
            GD.Print($"POWER unplug {id} x{k}");
            Re();
        }, F.CableItem));
        if (dev != null) items.Add(new Hud.MenuItem($"Sök ({dev.Def.Name} ve kabloları çantaya)", () => true, () => Dismantle(dev), dev.Def.Id));
        var stats = Net.StatsOf(id);
        string net = stats == null
            ? "Ağa bağlı değil. Elinde Kablo varken bir sokete, sonra ötekine sol tık (en fazla 12 m)."
            : $"Ağ: üretim {stats.SupplyW:0} W · talep {stats.DemandW:0} W · karşılanan {stats.ServedW:0} W" +
              (stats.CapacityWh > 0 ? $" · akü {stats.StoredWh:0}/{stats.CapacityWh:0} Wh ({(stats.BatteryW >= 0 ? "-" : "+")}{Mathf.Abs(stats.BatteryW):0} W)" : "") +
              (stats.Overloaded ? $"\nAŞIRI YÜK: {stats.Shed} tüketici kapandı (5'ten başlayarak)" : "");
        string title = dev != null ? $"{dev.Def.Name} · {dev.Status()}" : $"{NameOf(id)} · {pdef.Watts:0} W";
        _d.Hud.ShowMenu(title, items, net + "\n{act:interact} / {act:ui_cancel}: kapat");
    }
}
