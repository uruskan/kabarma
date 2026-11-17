using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Insa basligi ↔ yapi seti: secim, hayalet, maliyet (odun), tapu siniri.</summary>
public partial class BuildBridge
{
    BuildSystem _sys;
    public BuildSystem System => _sys;

    partial void InitImpl()
    {
        _sys = new BuildSystem { Name = "BuildSystem" };
        _d.AddChild(_sys);
        _sys.Init(_d, p => _d.World.HeightAt(p.X, p.Z));
        _sys.ExcludeFromAim(_d.Player);
        _sys.SetActive(false);
        foreach (var p in _sys.Kit.Pieces) Register(p.Id);
        _sys.Placed += piece => GD.Print($"BUILT {piece.Def.Id} pieces={_sys.Pieces.Count}");
        GD.Print($"BUILD kit pieces={_sys.Kit.Pieces.Count}");
    }

    public int CostOf(string id)
    {
        var def = _sys.Kit.Pieces.FirstOrDefault(p => p.Id == id);
        if (def == null) return 0;
        return _d.Data.Tool.BuildCosts.TryGetValue(def.Kind ?? "", out int c) ? c : 4;
    }

    public string NameOf(string id) => _sys.Kit.Pieces.FirstOrDefault(p => p.Id == id)?.Name ?? id;

    partial void SelectImpl(string id) => _sys.Select(id);
    partial void RotateImpl() => _sys.Rotate();
    partial void HideImpl() { if (_sys.Active) _sys.SetActive(false); }

    partial void TickImpl(Player p, Vector3 aim, bool place)
    {
        if (!_sys.Active) { _sys.SetActive(true); _sys.Select(_d.Capa.CurrentPlaceable); }
        var cam = p.Cam;
        _sys.UpdateAim(cam.GlobalPosition, -cam.GlobalBasis.Z);
        var cur = _sys.Current;
        string id = _sys.Selected?.Id ?? "";
        int cost = CostOf(id);
        int wood = _d.State.Backpack.Count("wood");
        bool inClaim = cur != null && _d.Ocak != null && cur.Xf.Origin.DistanceTo(_d.OcakPos) <= Ocak.ClaimRadius;
        string why = cur == null ? "" : !cur.Valid ? cur.Reason : !inClaim ? "tapunun dışı" : wood < cost ? $"odun yetmiyor ({wood}/{cost})" : "";
        _d.Hud.Hint($"İnşa: {NameOf(id)} ({cost} Odun){(why != "" ? "  ·  " + why : "")}  ·  {{act:build_next}}: değiştir  ·  {{act:build_rotate}}: döndür", 0.2f);
        if (!place || cur == null) return;
        if (why != "") { Sfx.Play("hit", null, -10f); return; }
        var piece = _sys.TryPlace();
        if (piece == null) return;
        _d.State.Backpack.Remove("wood", cost);
        Sfx.Play("place", piece.GlobalPosition, 0f);
        Fx.Burst(piece.GlobalPosition + Vector3.Up * 0.3f, Palette.Wood, 18, 4f, 0.08f, 0.6f);
        Events.RaisePiecePlaced(piece.Def.Id);
    }
}
