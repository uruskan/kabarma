using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Plan Defteri yerlestirmesi (GDD §7): kaydedilmis yapinin tamami hayalet olarak nisani izler (dayanak ilk temel arazide
/// serbest oturur, R 15° dondurur); sol tik parcalari sirayla plan hayaleti olarak birakir. Oturamayan atlanir.
/// </summary>
public partial class BuildSystem
{
    public Blueprint Blueprint { get; private set; }
    Node3D _bpGhost;
    StandardMaterial3D _bpMat;
    BuildPlacement _bpAnchor;

    public void BeginBlueprint(Blueprint bp)
    {
        EndBlueprint();
        if (bp == null || bp.Pieces.Count == 0) return;
        Blueprint = bp;
        _bpMat ??= GhostMaterial(ColorOf(Data.Build.Ghost.Plan));
        _bpGhost = new Node3D { Name = "BlueprintGhost", TopLevel = true };
        AddChild(_bpGhost);
        foreach (var p in bp.Pieces)
        {
            var def = Kit[p.Shape];
            if (def == null) continue;
            var m = Kit.Instantiate(def);
            m.Transform = Rel(p);
            foreach (var mi in m.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                mi.MaterialOverride = _bpMat;
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            _bpGhost.AddChild(m);
        }
        if (_ghost != null) _ghost.Visible = false;
    }

    public void EndBlueprint()
    {
        Blueprint = null;
        _bpAnchor = null;
        _bpGhost?.QueueFree();
        _bpGhost = null;
    }

    static Transform3D Rel(BlueprintPiece p) => new(new Basis(Vector3.Up, p.Yaw), new Vector3(p.Pos[0], p.Pos[1], p.Pos[2]));

    void UpdateBlueprintAim(Vector3 hit)
    {
        if (_ghost != null) _ghost.Visible = false;
        var first = Blueprint.Pieces[0];
        var def = Kit[first.Shape];
        bool plan = PlanMode;
        PlanMode = true;
        _bpAnchor = def?.Ground != null ? GroundPlacement(def, hit, first.Material) : null;
        PlanMode = plan;
        var xf = _bpAnchor?.Xf ?? new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(_rotStep * RotateStepDeg)), hit);
        _bpGhost.Visible = _active;
        _bpGhost.GlobalTransform = xf;
        bool ok = _bpAnchor != null && _bpAnchor.Valid;
        _bpMat.AlbedoColor = ok ? ColorOf(Data.Build.Ghost.Plan) : ColorOf(Data.Build.Ghost.Bad);
        _current = _bpAnchor;
    }

    /// <summary>Dayanak gecerliyse plani hayalet parcalar olarak birakir: (konan, toplam). Gecersizse (0, toplam).</summary>
    public (int placed, int total) PlaceBlueprint()
    {
        var bp = Blueprint;
        if (bp == null) return (0, 0);
        int n = bp.Pieces.Count;
        if (_bpAnchor == null || !_bpAnchor.Valid) return (0, n);
        var anchor = _bpAnchor.Xf;
        bool plan = PlanMode;
        PlanMode = true;
        int placed = 0;
        for (int i = 0; i < n; i++)
        {
            var bpp = bp.Pieces[i];
            var def = Kit[bpp.Shape];
            if (def == null) continue;
            var xf = i == 0 ? anchor : anchor * Rel(bpp);
            var p = i == 0 ? _bpAnchor : Evaluate(def, xf, "snap", bpp.Material);
            if (!p.Valid) { GD.Print($"BLUEPRINT skip {bpp.Shape}: {p.Reason}"); continue; }
            Spawn(p, true, bpp.Material);
            placed++;
        }
        PlanMode = plan;
        GD.Print($"BLUEPRINT placed {placed}/{n} '{bp.Name}'");
        return (placed, n);
    }

    /// <summary>Bakilan parcanin bagli yapisini plana cevirir (Plan Defteri "kaydet").</summary>
    public Blueprint Capture(BuildPiece from, string name) =>
        from?.S == null ? null : PlanBook.Capture(Graph, from.Sid, Data.Build.PlanBook.MaxPieces, name);
}
