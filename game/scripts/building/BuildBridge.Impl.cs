using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Insa Plani (karar 23; eskiden Insa basligi) ↔ yapi seti: secim, hayalet, bedel (sekil basina Odun, ICERIK §10.1), tapu siniri (M1: kurma yalniz tapu icinde).
/// Iki arac kipi (GDD §7; URETIM §6 M1 "Yapi"): Sok (tapu icinde %100 iade; parca ya da yerlestirilebilir) ve
/// Onar (hasar oraninda malzemenin %25'i). Parca cani %10'un altina inmez (Damage).
/// </summary>
public partial class BuildBridge
{
    public const string Sok = "sok", Onar = "onar";

    BuildSystem _sys;
    public BuildSystem System => _sys;
    Node3D _target;
    StandardMaterial3D _sokOverlay, _onarOverlay;
    bool _removing;
    readonly Dictionary<string, int> _fallRefund = new();

    partial void InitImpl()
    {
        _sys = new BuildSystem { Name = "BuildSystem" };
        _d.AddChild(_sys);
        _sys.Init(_d, p => _d.World.HeightAt(p.X, p.Z), null, _d.State.Build.Graph, _d.Data);
        _sys.ExcludeFromAim(_d.Player);
        _sys.SetActive(false);
        Register(Sok);
        Register(Onar);
        foreach (var p in _sys.Kit.Pieces) Register(p.Id);
        _sys.Placed += piece => GD.Print($"BUILT {piece.Def.Id} pieces={_sys.Pieces.Count}");
        // Sokum sirasinda desteksiz kalip dusen parcalar da iade edilir (malzeme kaybolmaz).
        _sys.Fell += (piece, _) => AddFallRefund(piece);
        GD.Print($"BUILD kit pieces={_sys.Kit.Pieces.Count}");
        InitYapi(); // BuildBridge.Yapi.cs: malzeme, yukseltme, plan modu, Plan Defteri (M2)
    }

    BuildRulesDef Rules => _d.Data.Economy.Build;

    public int CostOf(string id)
    {
        if (_d.Data.Tool.BuildCosts.TryGetValue(id, out int c)) return c;
        GD.PushWarning($"BuildBridge: '{id}' icin bedel yok (placeables.json buildCosts)");
        return 4;
    }

    static Dictionary<string, int> Wood(int n) => new() { ["wood"] = n };

    public string NameOf(string id) => id == Sok ? "Sök" : id == Onar ? "Onar" : _sys.Kit.Pieces.FirstOrDefault(p => p.Id == id)?.Name ?? id;

    partial void SelectImpl(string id) => _sys.Select(id);
    partial void RotateImpl() => _sys.Rotate();
    partial void HideImpl() { if (_sys.Active) _sys.SetActive(false); Highlight(null, false); }

    partial void TickImpl(Player p, Vector3 aim, bool place)
    {
        if (PlanTick(p, aim, place)) return; // BuildBridge.Yapi.cs: plan modu, plan parcasi, Plan Defteri
        var mode = _d.Capa.CurrentPlaceable;
        if (mode is Sok or Onar) { if (_sys.Active) _sys.SetActive(false); ToolTick(p, aim, place, mode == Sok); return; }
        Highlight(null, false);
        if (!_sys.Active) { _sys.SetActive(true); _sys.Select(mode); }
        var cam = p.Cam;
        _sys.UpdateAim(cam.GlobalPosition, -cam.GlobalBasis.Z);
        var cur = _sys.Current;
        string id = _sys.Selected?.Id ?? "";
        int cost = CostOf(id);
        int wood = _d.State.Backpack.Count("wood");
        // M2: kurma tapu disinda da serbest (GDD 7); orada sokum iadesi %50 (BuildBridge.Yapi.cs RefundFor).
        string why = cur == null ? "" : !cur.Valid ? cur.Reason : wood < cost ? $"odun yetmiyor ({wood}/{cost})" : "";
        _d.Hud.Hint($"İnşa: {NameOf(id)} ({cost} Odun){(why != "" ? "  ·  " + why : "")}  ·  {{act:build_wheel}} basılı: parça  ·  {{act:build_rotate}}: döndür  ·  {{act:interact}} basılı: onar / sök", 0.2f);
        if (!place || cur == null) return;
        if (why != "") { Sfx.Play("hit", null, -10f); return; }
        var piece = _sys.TryPlace();
        if (piece == null) return;
        _d.State.Backpack.Remove("wood", cost);
        Sfx.Play("place", piece.GlobalPosition, 0f);
        Fx.Burst(piece.GlobalPosition + Vector3.Up * 0.3f, Palette.Wood, 18, 4f, 0.08f, 0.6f);
        Events.RaisePiecePlaced(piece.Def.Id);
    }

    // ---------- Sok ve Onar ----------

    /// <summary>Nisan altindaki hedef: once yapi parcasi (isin), yoksa nisan noktasina yakin istasyon ya da yerlestirilebilir.</summary>
    public Node3D TargetAt(Player p, Vector3 aim)
    {
        var cam = p.Cam;
        var piece = _sys.PieceAt(cam.GlobalPosition, -cam.GlobalBasis.Z);
        if (piece != null) return piece;
        Node3D best = null;
        float bd = float.MaxValue;
        foreach (var st in _d.Stations)
        {
            float d = new Vector2(st.GlobalPosition.X - aim.X, st.GlobalPosition.Z - aim.Z).Length();
            if (d < st.Def.Radius + 0.8f && d < bd) { bd = d; best = st; }
        }
        foreach (var f in _d.Furniture)
        {
            float d = new Vector2(f.GlobalPosition.X - aim.X, f.GlobalPosition.Z - aim.Z).Length();
            if (d < f.Def.Radius + 0.6f && d < bd) { bd = d; best = f; }
        }
        return best;
    }

    void ToolTick(Player p, Vector3 aim, bool fire, bool sok)
    {
        var t = TargetAt(p, aim);
        Highlight(t, sok);
        string head = sok ? "Sök" : "Onar";
        if (t == null) { _d.Hud.Hint($"İnşa · {head}: bir parçaya ya da istasyona nişan al  ·  tekerlek: değiştir", 0.2f); return; }
        string msg = sok ? SokPreview(t, out bool ok) : OnarPreview(t, out ok);
        _d.Hud.Hint($"İnşa · {head}: {msg}", 0.2f);
        if (!fire) return;
        if (!ok) { Sfx.Play("hit", null, -10f); return; }
        if (sok) Deconstruct(t); else Repair(t);
    }

    static (string id, string name, Dictionary<string, int> cost) Describe(BuildBridge b, Node3D t) => t switch
    {
        BuildPiece bp => (bp.Def.Id, bp.Def.Name, b.PieceCost(bp)),
        // Karar 23: yerlestirilebilir sokulunce esyanin kendisi cantaya doner (kemerden yeniden kurulur).
        Station st => (st.Def.Id, st.Def.Name, new Dictionary<string, int> { [st.Def.Id] = 1 }),
        Furniture f => (f.Def.Id, f.Def.Name, new Dictionary<string, int> { [f.Def.Id] = 1 }),
        _ => ("", "", new()),
    };

    static string CostText(IReadOnlyDictionary<string, int> c) => c.Count == 0 ? "bedava" : string.Join(" + ", c.Select(kv => $"{kv.Value} {ItemNames.Of(kv.Key)}"));

    bool Fits(Dictionary<string, int> items)
    {
        var inv = _d.State.Backpack;
        var probe = new Inventory(inv.Slots, _d.Data.Tool.StackOf);
        foreach (var (id, n, stars) in inv.Stacks) probe.Add(id, n, stars);
        return items.All(kv => probe.Add(kv.Key, kv.Value) == kv.Value);
    }

    string SokBlock(Node3D t)
    {
        if (t is Station st)
        {
            if (st.Queue.Active != null) return "üretim sürüyor";
            if (st.Def.Station == "paketleme" && _d.State.Cargo.Open != null && _d.Stations.Count(s => s.Def.Station == "paketleme") == 1) return "kasada mal var";
        }
        if (t is Furniture f && f.Storage != null && !f.Storage.IsEmpty) return "önce boşalt";
        return "";
    }

    string SokPreview(Node3D t, out bool ok)
    {
        var (_, name, cost) = Describe(this, t);
        var refund = RefundFor(t, cost);
        string block = SokBlock(t);
        if (block == "" && !Fits(refund)) block = "çanta dolu";
        ok = block == "";
        return $"{name} (+{CostText(refund)}){(ok ? "" : "  ·  " + block)}";
    }

    string OnarPreview(Node3D t, out bool ok)
    {
        ok = false;
        if (t is not BuildPiece bp) return $"{Describe(this, t).name}: hasarsız";
        float max = _sys.MaxHp(bp);
        var cost = Structures.RepairCost(PieceCost(bp), bp.Hp, max, Rules);
        if (cost.Count == 0) return $"{bp.Def.Name}: hasarsız";
        bool afford = cost.All(kv => _d.State.Backpack.Count(kv.Key) >= kv.Value);
        ok = afford;
        return $"{bp.Def.Name} (can %{bp.Hp / max * 100f:0}, bedel {CostText(cost)}){(afford ? "" : "  ·  malzeme yetmiyor")}";
    }

    /// <summary>Hedefi soker ve bedelini iade eder (M1: kurma yalniz tapu icinde, iade %100). Islediyse true.</summary>
    public bool Deconstruct(Node3D t)
    {
        if (t == null || SokBlock(t) != "") return false;
        var (id, _, cost) = Describe(this, t);
        var refund = RefundFor(t, cost);
        if (!Fits(refund)) return false;
        var at = t.GlobalPosition;
        Highlight(null, false);
        _fallRefund.Clear();
        switch (t)
        {
            case BuildPiece bp:
                _removing = true;
                _sys.Remove(bp);
                _removing = false;
                break;
            case Station st:
                _d.Stations.Remove(st);
                st.QueueFree();
                break;
            case Furniture f:
                _d.Furniture.Remove(f);
                if (f.Def.Spawn && _d.Player.RespawnPoint.DistanceTo(f.GlobalPosition + Vector3.Up * 0.3f) < 0.5f)
                    _d.Player.RespawnPoint = _d.OcakPos + new Vector3(0, 0.3f, -2.5f);
                f.QueueFree();
                break;
            default: return false;
        }
        foreach (var (item, n) in refund) _d.State.Backpack.Add(item, n);
        foreach (var (item, n) in _fallRefund) _d.State.Backpack.Add(item, n);
        int fell = _fallRefund.GetValueOrDefault("wood");
        Fx.Burst(at + Vector3.Up * 0.5f, Palette.Wood, 22, 4f, 0.09f, 0.6f);
        Fx.FlyTo(at + Vector3.Up * 0.5f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Wood, 0.14f);
        Sfx.Play("place", at, -2f);
        GD.Print($"DECONSTRUCTED {id} refund={CostText(refund)}{(fell > 0 ? $" fallen+{fell} wood" : "")}");
        Events.RaiseDeconstructed(id);
        return true;
    }

    /// <summary>Parcayi onarir: hasar oraninda malzemenin %25'i (yukari yuvarlanir). Islediyse true.</summary>
    public bool Repair(Node3D t)
    {
        if (t is not BuildPiece bp) return false;
        float max = _sys.MaxHp(bp);
        var cost = Structures.RepairCost(PieceCost(bp), bp.Hp, max, Rules);
        if (cost.Count == 0 || cost.Any(kv => _d.State.Backpack.Count(kv.Key) < kv.Value)) return false;
        foreach (var (item, n) in cost) _d.State.Backpack.Remove(item, n);
        bp.Hp = max;
        Fx.Burst(bp.GlobalPosition + Vector3.Up * 1f, Palette.Plant, 16, 3f, 0.08f, 0.6f);
        Sfx.Play("place", bp.GlobalPosition, -4f);
        GD.Print($"REPAIRED {bp.Def.Id} cost={CostText(cost)}");
        return true;
    }

    /// <summary>Yapi parcasina hasar (Zipzip hucumu vb.): M1'de yikilma yok, can azaminin %10'unda durur.</summary>
    public float Damage(BuildPiece piece, float amount)
    {
        if (piece == null || !IsInstanceValid(piece)) return 0;
        piece.Hp = Structures.ApplyDamage(piece.Hp, _sys.MaxHp(piece), amount, Rules);
        return piece.Hp;
    }

    void Highlight(Node3D t, bool sok)
    {
        if (_target == t) return;
        if (_target != null && IsInstanceValid(_target))
            foreach (var mi in _target.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()) mi.MaterialOverlay = null;
        _target = t;
        if (t == null) return;
        _sokOverlay ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1f, 0.35f, 0.3f, 0.35f) };
        _onarOverlay ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.45f, 1f, 0.55f, 0.35f) };
        foreach (var mi in t.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()) mi.MaterialOverlay = sok ? _sokOverlay : _onarOverlay;
    }
}
