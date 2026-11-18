using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kemerden kurma (GDD 5, 7; URETIM 6 M1 karari 23). Elde yerlestirilebilir varsa (Ocak, Tezgah, Mesale...) hayaleti
/// gorunur, sol tik kurar, R dondurur; kurulunca cantadan 1 duser. Elde Insa Plani varsa yapi seti (BuildBridge) parca
/// kurar: secili parca <see cref="PlanPiece"/> (sag tik basili parca carki, Director.Input.cs). Insa basligi kalkti.
/// </summary>
public partial class Capa
{
    Node3D _ghost;
    StandardMaterial3D _okOverlay, _badOverlay;
    string _ghostId;
    float _ghostYaw;
    bool _ghostOk;
    string _planPiece;

    /// <summary>Elde tutulan esya: kemerin secili yuvasi (null: eller bos).</summary>
    public string HeldItem => _d?.State?.Belt.Held;
    /// <summary>Capa elde mi (kemerde Capa'nin yuvasi secili).</summary>
    public bool InHand => HeldItem == PlanTools.Capa;
    /// <summary>Insa Plani elde mi.</summary>
    public bool PlanInHand => HeldItem == PlanTools.Plan;

    /// <summary>Insa Plani'nin carkindaki parcalar (placeables.json planWheel; yapi setinde olanlar).</summary>
    public List<string> PlanPieces => _d.Data.Tool.PlanWheel.Where(id => _d.Build != null && _d.Build.Handles(id)).ToList();

    /// <summary>Insa Plani'nin secili parcasi.</summary>
    public string PlanPiece
    {
        get
        {
            var l = PlanPieces;
            if (_planPiece == null || !l.Contains(_planPiece)) _planPiece = l.FirstOrDefault() ?? "";
            return _planPiece;
        }
    }

    /// <summary>
    /// Su an kurulacak sey: Insa Plani eldeyse secili parca, yerlestirilebilir eldeyse onun kimligi; yoksa bos.
    /// BuildBridge.Impl bunu okur.
    /// </summary>
    public string CurrentPlaceable => PlanInHand ? PlanPiece : HeldItem is string h && DefOf(h) != null ? h : "";

    /// <summary>Parca carkindan secim.</summary>
    public void SelectPiece(string id)
    {
        if (string.IsNullOrEmpty(id) || _planPiece == id) return;
        _planPiece = id;
        Sfx.Play("place", null, -16f);
        _d.Build?.Select(id);
    }

    public void RotateGhost() { _ghostYaw += Mathf.Pi / 12f; if (PlanInHand && _d.Build != null) _d.Build.Rotate(); }

    PlaceableDef DefOf(string id) => _d.Data.Tool.Placeables.FirstOrDefault(p => p.Id == id);

    /// <summary>Capa ya da bos el: hayalet ve yapi seti gizlenir.</summary>
    void HideHandGhosts()
    {
        if (_ghost != null) _ghost.Visible = false;
        _d.Build?.Hide();
        _d.Power?.CancelTool(); // guc: Kablo elden inince secim biter
    }

    /// <summary>Eldeki yerlestirilebiliri hayaletin yerine kurar; cantadan 1 duser.</summary>
    void TryPlace()
    {
        var def = DefOf(_ghostId);
        if (def == null) return;
        if (!_ghostOk) { _d.Hud.Hint(def.Id != "ocak" && _d.Ocak == null ? "Önce Ocak'ı kur: tapu Ocak'la gelir" : "Buraya konmaz", 1.2f); Sfx.Play("hit", null, -10f); return; }
        if (_d.State.Backpack.Count(def.Id) < 1) { _d.Hud.Hint($"{def.Name} yok: Tab → Üretim", 1.5f); Sfx.Play("hit", null, -10f); return; }
        if (_d.Net?.ForwardPlaceItem(def.Id, _ghost.GlobalPosition, _ghostYaw) == true) { _ghost.Visible = false; return; } // ortak: host kurar
        _d.State.Backpack.Remove(def.Id, 1);
        var at = _ghost.GlobalPosition; float yaw = _ghostYaw;
        _ghost.QueueFree(); _ghost = null; _ghostId = null;
        if (_d.PlacePower(def, at, yaw)) return; // guc akisi: guc ve sinyal parcalari (power/Director.Power.cs)
        if (_d.PlaceDefense(def, at, yaw)) return; // savunma M3: kule ve tuzak (defense/Director.Defense.cs)
        bool furniture = def.Slots > 0 || def.Spawn || def.LightRange > 0; // Sandik, Uyku Tulumu, Mesale: ekonomi (entegrasyon)
        if (PenSystem.IsPenProp(def.Id) || (def.Station == null && def.Id != "ocak" && !furniture)) { _d.PlaceProp(def, at, yaw); return; } // Director.Pen.cs: Yemlik, Folluk, Ahsap Kapi
        if (def.Id == "ocak") _d.PlaceOcak(at);
        else if (def.Station != null) _d.PlaceStation(def, at, yaw);
        else _d.PlaceFurniture(def, at, yaw); // Sandik, Uyku Tulumu, Mesale (Director.Economy.cs)
    }

    void UpdateGhost(Vector3 aim)
    {
        var id = HeldItem;
        var def = id != null ? DefOf(id) : null;
        if (def == null) { if (_ghost != null) _ghost.Visible = false; return; }
        if (_ghost != null && _ghostId != id) { _ghost.QueueFree(); _ghost = null; }
        if (_ghost == null)
        {
            _ghostId = id;
            _ghost = Station.MakeVisual(def);
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
        _ghostOk = id == "ocak" ? _d.Ocak == null : _d.Ocak != null && at.DistanceTo(_d.OcakPos) <= Ocak.ClaimRadius && at.DistanceTo(_d.OcakPos) > 2.2f;
        if (_ghostOk && id != "ocak")
        {
            if (_d.Stations.Any(st => st.GlobalPosition.DistanceTo(at) < 2.2f)) _ghostOk = false;
            if (_d.Furniture.Any(f => f.GlobalPosition.DistanceTo(at) < f.Def.Radius + def.Radius + 0.2f)) _ghostOk = false;
            if (_d.Carts.Any(c => c.GlobalPosition.DistanceTo(at) < 1.6f)) _ghostOk = false;
            if (_d.State.Farm.Plots.Any(pl => _d.FarmView.CellCenter(pl.X, pl.Y).DistanceTo(at) < 1.6f)) _ghostOk = false;
            if (at.DistanceTo(_d.Pod.GlobalPosition) < 3.5f) _ghostOk = false;
        }
        if (_ghostOk && _d.Pens != null) _ghostOk = _d.Pens.SnapGhost(id, _ghost, at); // Ahsap Kapi Kapi Kasasi'na oturur
        _okOverlay ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.4f, 1f, 0.55f, 0.35f) };
        _badOverlay ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1f, 0.3f, 0.25f, 0.4f) };
        foreach (var mi in _ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()) { mi.Transparency = 0.4f; mi.MaterialOverlay = _ghostOk ? _okOverlay : _badOverlay; mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
        int have = _d.State.Backpack.Count(id);
        _d.Hud.Hint($"{def.Name} ×{have}  ·  {{act:fire}}: kur  ·  {{act:build_rotate}}: döndür", 0.2f);
    }

    public void PlaceOcak(Vector3 at)
    {
        if (_d.Ocak != null) return;
        if (_ghost != null) _ghost.Visible = false;
        _d.PlaceOcak(at);
    }
}

/// <summary>Kemer aletlerinin kimlikleri (craft.json beltTools ile ayni; kodun ayirdigi iki alet).</summary>
public static class PlanTools
{
    public const string Capa = "capa", Plan = "insa_plani";
}
