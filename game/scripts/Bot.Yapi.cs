using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, M2 yapi (GDD 7, 24 adim 17): barinagin bir duvarini Kerpic-Tas'a yukseltir. Kerpic Tas Firin'da uretilir
/// (2 Kil + 1 Lif, 1 Odun yakit; malzeme oyun yolundan: Kazma ile kil, Bicme ile lif), Tas Kazma ile. Sonra Insa Plani
/// elde duvara bakilir, E basili cark, "Yukselt". Esya verilmez, adim atlanmaz.
/// </summary>
public partial class Bot
{
    float _upT;

    void RegisterYapi()
    {
        _steps["yukselt"] = Machine(StepYukselt);
    }

    void StepYukselt(Vector3 o)
    {
        var b = _d.Build;
        if (b == null) { GD.Print("BOT no build kit"); return; }
        var sys = b.System;
        var home = o + ShelterAt;
        var wall = sys.Pieces.Where(p => p.Def.Id == "duvar" && p.Material == "wood" && !p.Planned)
            .OrderBy(p => Flat(p.GlobalPosition, home)).FirstOrDefault();
        if (wall == null) { P.MoveOverride = Vector3.Zero; if (_stepT > 5f) { GD.Print("BOT yukselt: ahsap duvar yok"); _stepT = 0; } return; }
        var up = StructureCosts.Upgrade(_d.Data.Build, wall.Material, sys.UnitsOf(wall.Def.Id), b.InClaimAt(wall.GlobalPosition));
        if (up == null) return;
        // Malzeme: once ham (tas, kil, lif, yakit odunu), sonra Kerpic Tas Firin'da.
        foreach (var (item, n) in up.Value.Pay)
            if (!HaveCount(item, n)) return;
        if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return; }
        if (!HoldItem(PlanTools.Plan)) return;
        // Duvarin disindan, ortasina bak (ic tarafta Uyku Tulumu ve kapi var).
        var c = wall.GlobalPosition + Vector3.Up * 1.4f;
        var outward = new Vector3(wall.GlobalPosition.X - home.X, 0, wall.GlobalPosition.Z - home.Z);
        outward = outward.LengthSquared() > 0.01f ? outward.Normalized() : Vector3.Forward;
        Aim(c);
        if (!MoveTo(_d.World.Ground(wall.GlobalPosition + outward * 3.2f), 0.6f, false)) { _upT = 0; return; }
        _upT += (float)GetProcessDeltaTime();
        if (_upT < 0.8f) return;
        // Oyuncu gibi: kameranin baktigi ahsap duvar hangisiyse o yukseltilir (barinagin duvarlari ayni bedelde).
        var t = b.TargetAt(P, P.AimPoint(out _, 60f));
        if (t is BuildPiece { Material: "wood", Planned: false } tw && tw.Def.Id == "duvar") wall = tw;
        else { if (_stepT > 4f) { GD.Print($"BOT yukselt: nisan {(t as BuildPiece)?.Def.Id ?? t?.Name ?? "bos"} (ahsap duvar degil)"); _stepT = 0; } return; }
        b.UpgradePreview(wall, out bool ok);
        if (!ok) { if (_stepT > 4f) { GD.Print($"BOT yukselt bekliyor: {b.UpgradePreview(wall, out _)}"); _stepT = 0; } return; }
        if (_stepT > 1.5f) { PlanAction(wall, 0); _stepT = 0; GD.Print($"BOT upgrade {wall.Def.Id} -> {up.Value.To.Id}"); }
    }

    /// <summary>
    /// Cantada en az n tane var mi; yoksa oyuncunun yolundan edinir: ham kaynak toplanir (EnsureItem), uretilen esya
    /// istasyon menusunden uretilir (malzeme ve 1 Odun yakit once). Yeterliyse true.
    /// </summary>
    bool HaveCount(string id, int n)
    {
        var inv = _d.State.Backpack;
        if (inv.Count(id) >= n) return true;
        var r = _d.Data.Tool.RecipeFor(id);
        if (r == null) { if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; } return EnsureItem(id, n); }
        var st = StationOf(r.Station);
        if (st == null) { if (_stepT > 3f) { GD.Print($"BOT {id}: {r.Station} yok"); _stepT = 0; } return false; }
        if (st.Queue.Active != null) { if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; } WaitAt(st); return false; }
        foreach (var (item, k) in r.In)
            if (inv.Count(item) < k)
            {
                if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; }
                EnsureItem(item, k);
                return false;
            }
        if (inv.Count("wood") < 1) { if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; } EnsureItem("wood", 1); return false; }
        if (_d.Hud.MenuOpen && _d.Hud.CraftStation != r.Station) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; }
        UseStation(st, RecipeIndex(st, id));
        return false;
    }

    /// <summary>
    /// Insa Plani'nin E basili carkindan eylem (0 Yukselt, 1 Onar, 2 Sok). Dogrudan kipte carkin sectigi yontem; girdi
    /// kipinde E basili tutulur, cark acilinca dilime gidilir, birakilir.
    /// </summary>
    void PlanAction(Node3D target, int action)
    {
        if (!UsesInput) { _d.Build.RunPlanAction(target, action); return; }
        _driver.SelectPlanAction(action, 3);
    }
}
