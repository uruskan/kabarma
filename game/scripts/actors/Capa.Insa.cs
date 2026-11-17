using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Capa, Insa basligi: yerlestirilebilirler (Ocak, istasyonlar) ve yapi seti koprusu.</summary>
public partial class Capa
{
    Node3D _ghost;

    // ---------- Insa ----------

    // ---------- Insa: yerlestirilebilirler ----------

    int _placeIndex;
    StandardMaterial3D _okOverlay, _badOverlay;
    string _ghostId;
    float _ghostYaw;
    bool _ghostOk;

    /// <summary>Su an secilebilen yerlestirilebilirler: Ocak kurulmadan once yalnizca Ocak (tapu yoksa yapi yok).</summary>
    public System.Collections.Generic.List<string> Placeables
    {
        get
        {
            if (_d.Ocak == null) return new() { "ocak" };
            var l = _d.Data.Tool.Placeables.Where(p => !p.Once).Select(p => p.Id).ToList();
            if (_d.Build != null) l.AddRange(_d.Build.PieceIds);
            return l;
        }
    }

    public string CurrentPlaceable { get { var l = Placeables; return l[Mathf.PosMod(_placeIndex, l.Count)]; } }

    public void CyclePlaceable(int dir) { _placeIndex += dir; Sfx.Play("place", null, -16f); if (_d.Build != null) _d.Build.Select(CurrentPlaceable); }
    public void RotateGhost() { _ghostYaw += Mathf.Pi / 12f; if (_d.Build != null && _d.Build.Handles(CurrentPlaceable)) _d.Build.Rotate(); }

    PlaceableDef DefOf(string id) => _d.Data.Tool.Placeables.FirstOrDefault(p => p.Id == id);

    string CostText(PlaceableDef def) => def.Cost.Count == 0 ? "bedava" : string.Join(", ", def.Cost.Select(kv => $"{kv.Value} {ItemNames.Of(kv.Key)}"));

    void TryPlace()
    {
        var def = DefOf(_ghostId);
        if (def == null) return;
        if (!_ghostOk) { _d.Hud.Hint("Buraya konmaz", 1f); Sfx.Play("hit", null, -10f); return; }
        if (def.Cost.Any(kv => _d.State.Backpack.Count(kv.Key) < kv.Value)) { _d.Hud.Hint($"Yetmiyor: {CostText(def)}", 1.5f); Sfx.Play("hit", null, -10f); return; }
        foreach (var kv in def.Cost) _d.State.Backpack.Remove(kv.Key, kv.Value);
        var at = _ghost.GlobalPosition; float yaw = _ghostYaw;
        _ghost.QueueFree(); _ghost = null; _ghostId = null;
        bool furniture = def.Slots > 0 || def.Spawn || def.LightRange > 0; // Sandik, Uyku Tulumu, Mesale: ekonomi (entegrasyon)
        if (def.Station == null && def.Id != "ocak" && !furniture) { _d.PlaceProp(def, at, yaw); return; } // Director.Pen.cs: Yemlik, Folluk, Ahsap Kapi
        if (def.Id == "ocak") _d.PlaceOcak(at);
        else if (def.Station != null) _d.PlaceStation(def, at, yaw);
        else _d.PlaceFurniture(def, at, yaw); // Sandik, Uyku Tulumu, Mesale (Director.Economy.cs)
    }

    void UpdateGhost(Vector3 aim)
    {
        bool show = Current == Head.Insa && !(_d.Build != null && _d.Build.Handles(CurrentPlaceable));
        if (_d.Build != null && (Current != Head.Insa || !_d.Build.Handles(CurrentPlaceable))) _d.Build.Hide();
        if (!show) { if (_ghost != null) _ghost.Visible = false; return; }
        var id = CurrentPlaceable;
        if (_ghost != null && _ghostId != id) { _ghost.QueueFree(); _ghost = null; }
        if (_ghost == null)
        {
            _ghostId = id;
            _ghost = Station.MakeVisual(DefOf(id));
            foreach (var mi in _ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                mi.Transparency = 0.55f;
            foreach (var c in _ghost.FindChildren("*", "CollisionShape3D", true, false).OfType<CollisionShape3D>()) c.Disabled = true;
            foreach (var l in _ghost.FindChildren("*", "OmniLight3D", true, false).OfType<OmniLight3D>()) l.Visible = false;
            foreach (var g in _ghost.FindChildren("*", "GpuParticles3D", true, false).OfType<GpuParticles3D>()) g.Visible = false;
            _ghost.TopLevel = true;
            AddChild(_ghost);
        }
        var p = _d.Player.GlobalPosition;
        var flat = aim - p; flat.Y = 0;
        if (flat.Length() > _t.BuildRange) flat = flat.Normalized() * _t.BuildRange;
        if (flat.Length() < 2f) flat = flat.Normalized() * 2f;
        var at = _d.World.Ground(p + flat);
        _ghost.GlobalPosition = at;
        _ghost.Rotation = new Vector3(0, _ghostYaw, 0);
        _ghost.Visible = true;
        // Gecerlilik: tapu icinde, tarla karelerinin, istasyonlarin ve kapsulun uzerinde degil.
        _ghostOk = id == "ocak" || (_d.Ocak != null && at.DistanceTo(_d.OcakPos) <= Ocak.ClaimRadius && at.DistanceTo(_d.OcakPos) > 2.2f);
        if (_ghostOk && id != "ocak")
        {
            if (_d.Stations.Any(st => st.GlobalPosition.DistanceTo(at) < 2.2f)) _ghostOk = false;
            if (_d.Furniture.Any(f => f.GlobalPosition.DistanceTo(at) < f.Def.Radius + (DefOf(id)?.Radius ?? 1f) + 0.2f)) _ghostOk = false;
            if (_d.Carts.Any(c => c.GlobalPosition.DistanceTo(at) < 1.6f)) _ghostOk = false;
            if (_d.State.Farm.Plots.Any(pl => _d.FarmView.CellCenter(pl.X, pl.Y).DistanceTo(at) < 1.6f)) _ghostOk = false;
            if (at.DistanceTo(_d.Pod.GlobalPosition) < 3.5f) _ghostOk = false;
        }
        if (_ghostOk && _d.Pens != null) _ghostOk = _d.Pens.SnapGhost(id, _ghost, at); // Ahsap Kapi Kapi Kasasi'na oturur
        var def = DefOf(id);
        _okOverlay ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.4f, 1f, 0.55f, 0.35f) };
        _badOverlay ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1f, 0.3f, 0.25f, 0.4f) };
        foreach (var mi in _ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()) { mi.Transparency = 0.4f; mi.MaterialOverlay = _ghostOk ? _okOverlay : _badOverlay; mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
        _d.Hud.Hint($"İnşa: {def?.Name} ({(def != null ? CostText(def) : "")})  ·  {{act:build_next}}: değiştir  ·  {{act:build_rotate}}: döndür", 0.2f);
    }

    public void PlaceOcak(Vector3 at)
    {
        if (_d.Ocak != null) return;
        if (_ghost != null) _ghost.Visible = false;
        _d.PlaceOcak(at);
    }
}
