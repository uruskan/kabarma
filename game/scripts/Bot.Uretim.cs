using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, zincir M2 (GDD §4, §5, §12; ICERIK §11.1): bir T2 urununu arastirir ve uretir. Oyuncunun yolundan: malzeme
/// Kazma ve Bicme ile, ara urun istasyonun E menusunden, Veri Tarayici ile, arastirma Arastirma Masasi'nin E menusunden
/// (Uretim paneli, Arastirma kategorisi). Esya verilmez, adim atlanmaz.
/// Adim "arastir_t2" (--test=zincir surer; senaryoda yok): Tas Firin → 4 Bakir Kulce → 8 Bakir Tel (Tezgah) → Arastirma
/// Masasi → 25 Veri tarama → Dokuma Tezgahi arastir → Dokuma Tezgahi kur → Kumas.
/// </summary>
public partial class Bot
{
    static readonly Vector3 MasaAt = new(3f, 0, 9.5f);
    static readonly Vector3 DokumaAt = new(7.5f, 0, 8.5f);
    static readonly Vector3 UretimFirinAt = new(-5f, 0, 9f);

    void RegisterUretim()
    {
        _steps["arastir_t2"] = Machine(StepArastirT2);
    }

    /// <summary>Kumas uretildi: --test=zincir bunu bekler.</summary>
    public bool T2Done => _d.State.Backpack.Count("cloth") > 0;

    /// <summary>
    /// --test=zincir: botun kendi senaryo surucusu durdurulmusken (ProcessMode Disabled) verilen adimi bir kare isletir.
    /// Bot._Process ile ayni sayaclar.
    /// </summary>
    public void DriveStep(string step, float dt)
    {
        _t += dt;
        _stepT += dt;
        if (step != _lastStep) { _lastStep = step; _stepT = 0; _cell = 0; Fire = false; GD.Print($"BOT step {step} t={_t:0.0}"); }
        Fire = false;
        if (_steps.TryGetValue(step, out var run)) RunStep(step, run);
        else UnknownStep(step);
    }

    /// <summary>Tarayici (T / ✚↑).</summary>
    void ScanAct() { if (!UsesInput) _d.Scan(); else _driver.Tap("scan"); }

    float _scanT;

    void StepArastirT2(Vector3 o)
    {
        var bag = _d.State.Backpack;
        var rs = _d.State.Research;
        if (bag.Count("cloth") > 0) { P.MoveOverride = Vector3.Zero; return; }

        // 1. Arastirma Masasi (Tezgah'ta: 12 Odun + 6 Taş + 4 Bakir Tel).
        var masa = StationOf("arastirma_masasi");
        if (masa == null)
        {
            if (StationOf("tas_firin") == null) { if (EnsureCost("tas_firin")) PlaceAt("tas_firin", o + UretimFirinAt); return; }
            if (!EnsureMade("copper_wire", 4)) return;
            if (EnsureCost("arastirma_masasi")) PlaceAt("arastirma_masasi", o + MasaAt);
            return;
        }

        // 2. Dokuma Tezgahi arastirmasi: 25 Veri tarama, sonra Masa'nin E menusu.
        if (!rs.IsDone("dokuma_tezgahi"))
        {
            var node = _d.Data.Research.Node("dokuma_tezgahi")!;
            if (rs.Veri < _d.Data.Research.CostOf(node)) { StepScan(); return; }
            ResearchAt(masa, "dokuma_tezgahi");
            return;
        }

        // 3. Dokuma Tezgahi kur (12 Odun + 4 Bakir Tel + 6 Lif), Kumas dok (3 Lif).
        var dokuma = StationOf("dokuma_tezgahi");
        if (dokuma == null)
        {
            if (bag.Count("dokuma_tezgahi") == 0 && !EnsureMade("copper_wire", 4)) return;
            if (EnsureCost("dokuma_tezgahi")) PlaceAt("dokuma_tezgahi", o + DokumaAt);
            return;
        }
        if (dokuma.Queue.Active != null) { WaitAt(dokuma); return; }
        if (EnsureItem("fiber", 3)) UseStation(dokuma, RecipeIndex(dokuma, "cloth"));
    }

    /// <summary>
    /// Ara urunu (tarifi olan esyayi) cantada en az <paramref name="need"/> adede getirir: girdilerini once toplar
    /// (ham kaynak Kazma/Bicme, ara urun yine bu yolla), sonra istasyonun E menusunden uretir. Hazirsa true.
    /// </summary>
    bool EnsureMade(string item, int need)
    {
        var bag = _d.State.Backpack;
        if (bag.Count(item) >= need) return true;
        var r = _d.Data.Tool.RecipeFor(item);
        if (r == null || CraftRules.IsHand(r)) return EnsureItem(item, need);
        var st = StationOf(r.Station);
        if (st == null) { if (_stepT > 3f) { GD.Print($"BOT {item}: {r.Station} yok"); _stepT = 0; } return false; }
        if (st.Queue.Active != null) { WaitAt(st); return false; }
        foreach (var (inp, n) in r.In) if (!EnsureMade(inp, n)) return false;
        if (st.Def.FuelPerJob > 0 && st.Def.Fuel.Count > 0 && !st.Queue.HasFuel(r, bag) && !EnsureItem(st.Def.Fuel.Last(), r.In.GetValueOrDefault(st.Def.Fuel.Last()) + st.Def.FuelPerJob)) return false;
        if (_d.Hud.MenuOpen && _d.Hud.CraftStation != r.Station) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; }
        UseStation(st, RecipeIndex(st, item));
        return false;
    }

    /// <summary>Taranmamis en yakin turu bulur (maden, ot, agac), ona yurur, nisan alir, tarar.</summary>
    void StepScan()
    {
        if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return; }
        var rs = _d.State.Research;
        var p = P.GlobalPosition;
        (Vector3 at, Vector3 aim)? best = null;
        float bd = float.MaxValue;
        void Try(string kind, string id, Vector3 at, Vector3 aim)
        {
            if (rs.Scanned.Contains(id) || rs.ScanEntry(kind, id) == null) return;
            float d = at.DistanceTo(p);
            if (d < bd) { bd = d; best = (at, aim); }
        }
        foreach (var r in _d.World.Rocks) if (IsInstanceValid(r)) Try("mineral", r.Item, r.GlobalPosition, r.Center);
        foreach (var t in _d.World.Tufts) if (t.Left > 0) Try("plant", t.Item, t.Position, t.Position + Vector3.Up * 0.2f);
        foreach (var tr in _d.World.TreePositions) Try("plant", "tree", tr, tr + Vector3.Up * 1.2f);
        if (best == null) { if (_stepT > 3f) { GD.Print($"BOT nothing left to scan veri={rs.Veri}"); _stepT = 0; } return; }
        var (pos, aimAt) = best.Value;
        Aim(aimAt);
        if (!MoveTo(pos, 5f, false)) return;
        _scanT -= (float)GetProcessDeltaTime();
        if (_scanT <= 0) { ScanAct(); _scanT = 0.8f; }
    }

    /// <summary>Arastirma Masasi'na yurur, E (Uretim paneli Arastirma kategorisinde acilir), satiri secer: arastirilir.</summary>
    void ResearchAt(Station masa, string node)
    {
        Aim(masa.GlobalPosition + Vector3.Up);
        if (!MoveTo(masa.GlobalPosition + (P.GlobalPosition - masa.GlobalPosition).Normalized() * 2.2f, 0.5f, false)) return;
        if (_menuT > 0) return;
        if (!_d.Hud.MenuOpen) { Use(); _menuT = 0.6f; return; }
        int i = _d.Hud.CraftRowIds.ToList().IndexOf(node);
        if (i < 0) { MenuClose(); _menuT = 1f; GD.Print($"BOT research row {node} yok: {string.Join(",", _d.Hud.CraftRowIds)}"); return; }
        MenuPick(i);
        _menuT = 0.8f;
        GD.Print($"BOT research {node} row={i}");
    }
}
