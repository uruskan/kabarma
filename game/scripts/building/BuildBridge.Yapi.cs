using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// M2 yapi (GDD §7): malzemeye gore bedel ve can, tapu disinda %50 sokum iadesi, yerinde yukseltme (E basili carkin
/// "Yukselt" dilimi), plan modu (G: bedelsiz plan hayaleti; plan parcasina bakip sol tik malzeme koyar) ve Plan Defteri
/// (B basili: 12 yuvali cark; bos yuva bakilan yapiyi kaydeder, dolu yuva yapiyi hayalet olarak yerlestirir).
/// Kural durumu Sim'de (GameState.Build); bu dosya girdi, ipucu, ses ve efekttir.
/// </summary>
public partial class BuildBridge
{
    BuildFile BData => _d.Data.Build;
    bool _bookWheel;
    int _bookSlot;

    void InitYapi()
    {
        _sys.PlanPlaced += piece => GD.Print($"PLANNED {piece.Def.Id} pieces={_sys.Pieces.Count}");
        _sys.Fell += (piece, _) => GD.Print($"COLLAPSE {piece.Def.Id} {piece.Material} span={(piece.Span == int.MaxValue ? "-" : piece.Span.ToString())} load={piece.Load:0.#}");
        // Kayit (TEKNIK 9.1): kural durumu Sim "build" bolumunde; dugumler butun bolumler okununca yeniden kurulur.
        _d.Save.Add(SaveSection.Json("build.view", 1, () => 0, _ => { }, SaveOrder.Spawn, afterRestore: _sys.RebuildFromGraph));
    }

    /// <summary>Parcanin malzemesindeki bedeli; plan parcasinda konmus malzeme (sokulunce aynen doner).</summary>
    public Dictionary<string, int> PieceCost(BuildPiece bp) =>
        bp.Planned ? new Dictionary<string, int>(bp.S.Delivered) : StructureCosts.Cost(BData, bp.Material, _sys.UnitsOf(bp.Def.Id));

    public bool InClaimAt(Vector3 at) => _d.Ocak != null && Structures.InClaim(at.X - _d.OcakPos.X, at.Z - _d.OcakPos.Z, Ocak.ClaimRadius);

    /// <summary>Sokum iadesi: yapi parcasi tapu icinde %100, disinda %50 (GDD §7); plan parcasi konani aynen; yerlestirilebilir esyanin kendisi.</summary>
    Dictionary<string, int> RefundFor(Node3D t, IReadOnlyDictionary<string, int> cost) => t switch
    {
        BuildPiece { Planned: true } => new Dictionary<string, int>(cost),
        BuildPiece bp => StructureCosts.Refund(cost, InClaimAt(bp.GlobalPosition), BData),
        _ => Structures.Refund(cost, Rules),
    };

    /// <summary>Sokum sirasinda coken parca: iadesi ayni kuralla (malzeme kaybolmaz).</summary>
    void AddFallRefund(BuildPiece piece)
    {
        if (!_removing) return;
        foreach (var (k, n) in RefundFor(piece, PieceCost(piece))) _fallRefund[k] = _fallRefund.GetValueOrDefault(k) + n;
    }

    string MatName(string id) => BData.Material(id)?.Name ?? id;

    // ---------- Yukselt (E basili carkin ilk dilimi) ----------

    public string UpgradePreview(Node3D t, out bool ok)
    {
        ok = false;
        if (t is not BuildPiece bp) return "yalnız yapı parçası yükseltilir";
        if (bp.Planned) return $"{bp.Def.Name}: plan, önce malzemesini getir";
        var up = StructureCosts.Upgrade(BData, bp.Material, _sys.UnitsOf(bp.Def.Id), InClaimAt(bp.GlobalPosition));
        if (up == null) return $"{bp.Def.Name}: {MatName(bp.Material)} en üst malzeme";
        var (to, pay, back) = up.Value;
        var rep = _sys.Graph.TrySetMaterial(bp.Sid, to.Id, commit: false);
        bool afford = pay.All(kv => _d.State.Backpack.Count(kv.Key) >= kv.Value);
        string block = !rep.Valid ? $"alttaki taşıyamaz ({rep.Reason})" : !afford ? "malzeme yetmiyor" : !Fits(back) ? "çanta dolu" : "";
        ok = block == "";
        return $"{MatName(bp.Material)} → {to.Name}: {CostText(pay)}{(back.Count > 0 ? $" (+{CostText(back)} geri)" : "")}{(ok ? "" : "  ·  " + block)}";
    }

    /// <summary>Parcayi bir ust malzemeye yukseltir: yeni bedel cantadan, eskinin iadesi cantaya. Islediyse true.</summary>
    public bool Upgrade(Node3D t)
    {
        if (t is not BuildPiece bp) return false;
        UpgradePreview(t, out bool ok);
        if (!ok) return false;
        var (to, pay, back) = StructureCosts.Upgrade(BData, bp.Material, _sys.UnitsOf(bp.Def.Id), InClaimAt(bp.GlobalPosition)).Value;
        string from = bp.Material;
        if (!_sys.Upgrade(bp, to.Id, out _)) return false;
        foreach (var (item, n) in pay) _d.State.Backpack.Remove(item, n);
        foreach (var (item, n) in back) _d.State.Backpack.Add(item, n);
        var at = bp.GlobalPosition + Vector3.Up * 1.2f;
        Fx.Burst(at, new Color(to.Tint[0], to.Tint[1], to.Tint[2]), 26, 4f, 0.1f, 0.7f);
        Sfx.Play("place", at, 0f, 0.8f);
        GD.Print($"UPGRADED {bp.Def.Id} {from}->{to.Id} pay={CostText(pay)} back={CostText(back)} hp={bp.Hp:0}/{_sys.MaxHp(bp):0}");
        Events.RaisePieceUpgraded(bp.Def.Id, to.Id);
        return true;
    }

    // ---------- Plan modu, plan parcasina malzeme, Plan Defteri ----------

    /// <summary>TickImpl'in basinda: plan modu, Plan Defteri ve plan parcasi isledi ise true (olagan kurma atlanir).</summary>
    bool PlanTick(Player p, Vector3 aim, bool place)
    {
        var mode = _d.Capa.CurrentPlaceable;
        if (mode is Sok or Onar) return false;
        if (Input.IsActionJustPressed("build_plan") && !_d.Hud.MenuOpen && !_d.Hud.WheelOpen)
        {
            if (_sys.Blueprint != null) _sys.EndBlueprint();
            else _sys.PlanMode = !_sys.PlanMode;
            Sfx.Play("place", null, -14f, _sys.PlanMode ? 1.2f : 0.9f);
            GD.Print($"PLANMODE {(_sys.PlanMode ? "on" : "off")}");
        }
        if (BookTick(p)) return true;
        var cam = p.Cam;
        if (_sys.Blueprint != null) { BlueprintTick(cam, place); return true; }
        if (!_sys.PlanMode)
        {
            var target = _sys.PieceAt(cam.GlobalPosition, -cam.GlobalBasis.Z);
            if (target is { Planned: true }) { DeliverTick(target, place); return true; }
            return false;
        }
        if (!_sys.Active) { _sys.SetActive(true); _sys.Select(mode); }
        _sys.UpdateAim(cam.GlobalPosition, -cam.GlobalBasis.Z);
        var cur = _sys.Current;
        string id = _sys.Selected?.Id ?? "";
        string why = cur == null ? "" : !cur.Valid ? cur.Reason : "";
        _d.Hud.Hint($"Plan modu: {NameOf(id)} (malzeme sonra: {CostOf(id)} Odun){(why != "" ? "  ·  " + why : "")}  ·  {{act:fire}}: plan bırak  ·  {{act:build_plan}}: kapat  ·  {{act:plan_book}} basılı: Plan Defteri", 0.2f);
        if (!place || cur == null) return true;
        if (why != "") { Sfx.Play("hit", null, -10f); return true; }
        var piece = _sys.TryPlace();
        if (piece != null) Sfx.Play("place", piece.GlobalPosition, -8f, 1.25f);
        return true;
    }

    void DeliverTick(BuildPiece target, bool place)
    {
        if (_sys.Active) _sys.SetActive(false);
        var cost = StructureCosts.Cost(BData, target.Material, _sys.UnitsOf(target.Def.Id));
        var missing = StructureCosts.Missing(cost, target.S.Delivered);
        _d.Hud.Hint($"Plan: {target.Def.Name} ({MatName(target.Material)})  ·  eksik {CostText(missing)}  ·  {{act:fire}}: malzeme koy  ·  {{act:interact}} basılı: sök", 0.2f);
        if (!place) return;
        var moved = StructureCosts.Deliver(cost, target.S.Delivered, _d.State.Backpack);
        if (moved.Count == 0 && missing.Count > 0) { Sfx.Play("hit", null, -10f); return; }
        if (moved.Count > 0)
        {
            Fx.FlyTo(_d.Player.BackpackPoint.GlobalPosition, () => target.GlobalPosition + Vector3.Up, Palette.Wood, 0.12f);
            GD.Print($"PLAN_DELIVER {target.Def.Id} {CostText(moved)}");
        }
        if (StructureCosts.Missing(cost, target.S.Delivered).Count > 0) { Sfx.Play("place", target.GlobalPosition, -10f, 1.3f); return; }
        if (!_sys.CompletePlan(target, out var rep))
        {
            _d.Hud.Hint($"Plan: {target.Def.Name} hazır ama {(rep?.Reason == "destek yok" ? "önce altını bitir" : rep?.Reason)}", 1.5f);
            Sfx.Play("hit", null, -10f);
            return;
        }
        Sfx.Play("place", target.GlobalPosition, 0f);
        Fx.Burst(target.GlobalPosition + Vector3.Up * 0.3f, Palette.Wood, 18, 4f, 0.08f, 0.6f);
        GD.Print($"PLAN_COMPLETE {target.Def.Id}");
        Events.RaisePlanCompleted(target.Def.Id);
        Events.RaisePiecePlaced(target.Def.Id);
    }

    bool BookTick(Player p)
    {
        var hud = _d.Hud;
        if (!_bookWheel)
        {
            if (!Input.IsActionJustPressed("plan_book") || hud.MenuOpen || hud.WheelOpen) return false;
            var book = _d.State.Build.PlanBook;
            var slices = book.Slots.Select((bp, i) => bp == null
                ? new Hud.WheelSlice("insa", null, $"{i + 1} · Boş", "Bakılan yapıyı buraya kaydet")
                : new Hud.WheelSlice("insa", null, $"{i + 1} · {bp.Name}", $"{bp.Pieces.Count} parça · {CostText(bp.TotalCost(BData, _sys.UnitsOf))}")).ToList();
            hud.OpenWheel(Hud.WheelKind.Book, slices, Math.Clamp(_bookSlot, 0, Math.Max(0, slices.Count - 1)));
            p.LookLocked = true;
            _bookWheel = true;
            return true;
        }
        if (!hud.WheelOpen || hud.OpenWheelKind != Hud.WheelKind.Book) { _bookWheel = false; return true; } // Esc: vazgecildi
        if (!Input.IsActionPressed("plan_book"))
        {
            int sel = hud.CloseWheel();
            p.LookLocked = false;
            _bookWheel = false;
            if (sel >= 0) BookPick(p, sel);
        }
        return true;
    }

    /// <summary>Plan Defteri yuvasi: bossa bakilan yapiyi kaydeder, doluysa yerlestirme kipine gecer.</summary>
    public bool BookPick(Player p, int slot)
    {
        var book = _d.State.Build.PlanBook;
        if (slot < 0 || slot >= book.Slots.Count) return false;
        _bookSlot = slot;
        var bp = book.Get(slot);
        if (bp == null)
        {
            var cam = p.Cam;
            var t = _sys.PieceAt(cam.GlobalPosition, -cam.GlobalBasis.Z);
            var cap = _sys.Capture(t, $"Yapı {slot + 1}");
            if (cap == null) { _d.Hud.Hint("Plan Defteri: kaydetmek için bir yapı parçasına bak", 2f); Sfx.Play("hit", null, -10f); return false; }
            book.Save(slot, cap);
            Sfx.Play("place", null, -6f, 1.4f);
            _d.Hud.Hint($"Plan Defteri {slot + 1}: {cap.Pieces.Count} parça kaydedildi", 2f);
            GD.Print($"PLANBOOK saved slot={slot} pieces={cap.Pieces.Count}");
            return true;
        }
        if (!_sys.Active) _sys.SetActive(true);
        _sys.BeginBlueprint(bp);
        GD.Print($"PLANBOOK place slot={slot} pieces={bp.Pieces.Count}");
        return true;
    }

    void BlueprintTick(Camera3D cam, bool place)
    {
        if (!_sys.Active) _sys.SetActive(true);
        _sys.UpdateAim(cam.GlobalPosition, -cam.GlobalBasis.Z);
        var bp = _sys.Blueprint;
        bool ok = _sys.Current != null && _sys.Current.Valid;
        _d.Hud.Hint($"Plan Defteri: {bp.Name} ({bp.Pieces.Count} parça){(ok ? "" : "  ·  " + (_sys.Current?.Reason ?? "temel için düz yer"))}  ·  {{act:fire}}: plan olarak bırak  ·  {{act:build_rotate}}: döndür  ·  {{act:build_plan}}: vazgeç", 0.2f);
        if (!place) return;
        var (k, n) = _sys.PlaceBlueprint();
        if (k == 0) { Sfx.Play("hit", null, -10f); return; }
        Sfx.Play("place", null, -4f, 1.2f);
        _d.Hud.Hint($"Plan Defteri: {k}/{n} parça plan olarak kondu", 2.5f);
        _sys.EndBlueprint();
    }
}
