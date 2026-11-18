using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kurulu kule ya da tuzak (GDD §16.4 M3). Furniture'dan turer: kemerden kurulur, Insa Plani'nin Sok kipiyle sokulur,
/// ortak oyunda "placed" bolumuyle cogalir. Kural durumu Sim'de (GameState.Defense, kimlik StateId); burasi gorunus:
/// kulenin donen basi ("head") ve namlusu ("muzzle"), Firlatma Pedi'nin plakasi, Cukur Kapagi'nin kanatlari.
/// </summary>
public partial class DefenseNode : Furniture
{
    readonly Director _dd;
    public int StateId;
    public TowerDef Tower { get; }
    public TrapDef Trap { get; }
    Node3D _head, _muzzle, _plate, _leafL, _leafR;
    float _yaw, _recoil, _plateT, _open;

    public DefenseNode(Director d, PlaceableDef def) : base(d, def)
    {
        _dd = d;
        Tower = d.Data.Defense.Tower(def.Id);
        Trap = d.Data.Defense.Trap(def.Id);
    }

    public override void _Ready()
    {
        base._Ready();
        _head = FindChild("head", true, false) as Node3D;
        _muzzle = FindChild("muzzle", true, false) as Node3D;
        _plate = FindChild("plate", true, false) as Node3D;
        _leafL = FindChild("leaf_l", true, false) as Node3D;
        _leafR = FindChild("leaf_r", true, false) as Node3D;
        if (Trap != null) foreach (var b in GetChildren().OfType<StaticBody3D>()) b.QueueFree(); // tuzagin ustunden gecilir
        _yaw = _head?.Rotation.Y ?? 0f;
    }

    public TowerState TowerState => _dd.State.Defense.Tower(StateId);
    public TrapState TrapState => _dd.State.Defense.Trap(StateId);

    /// <summary>Atis noktasi (namlu ya da bas yuksekligi).</summary>
    public Vector3 Muzzle => _muzzle?.GlobalPosition ?? GlobalPosition + Vector3.Up * (Tower?.HeadHeight ?? 1f);

    /// <summary>Basi hedefe cevirir (dunya yonu); yeterince donduyse true.</summary>
    public bool Aim(Vector3 at, float dt)
    {
        if (_head == null) return true;
        var local = ToLocal(at);
        float want = Mathf.Atan2(-local.X, -local.Z); // model ileri = -Z
        _yaw = Mathf.LerpAngle(_yaw, want, Mathf.Min(1f, dt * 10f));
        return Mathf.Abs(Mathf.AngleDifference(_yaw, want)) < 0.35f;
    }

    public void Kick() { _recoil = 1f; _plateT = 1f; }

    public override void _Process(double delta)
    {
        base._Process(delta);
        float dt = (float)delta;
        _recoil = Mathf.Max(0, _recoil - dt * 6f);
        if (_head != null) { _head.Rotation = new Vector3(0, _yaw, 0); _head.Scale = Vector3.One * (1f - 0.06f * Mathf.Sin(_recoil * Mathf.Pi)); }
        if (_plate != null)
        {
            _plateT = Mathf.Max(0, _plateT - dt * 2.5f);
            _plate.Position = new Vector3(_plate.Position.X, 0.06f + Mathf.Sin(_plateT * Mathf.Pi) * 0.35f, _plate.Position.Z);
        }
        if (_leafL != null)
        {
            _open = Mathf.MoveToward(_open, TrapState?.Open == true ? 1f : 0f, dt * 3f);
            float a = Mathf.SmoothStep(0, 1, _open) * Mathf.Pi / 2f;
            _leafL.Rotation = new Vector3(0, 0, -a);
            _leafR.Rotation = new Vector3(0, 0, a);
        }
    }

    /// <summary>Yakindaki oyuncuya durum satiri (Hud ipucu).</summary>
    public string Status()
    {
        var f = _dd.Data.Defense;
        if (Tower != null && TowerState is TowerState t)
        {
            string ammo = string.Join(", ", Tower.Ammo.Where(a => t.Mag.GetValueOrDefault(a) > 0).Select(a => $"{ItemNames.Of(a)} {t.Mag[a]}"));
            string st = TowerRules.Status(t) switch { TowerStatus.Broken => "bakım ister", TowerStatus.Empty => "cephane yok", TowerStatus.Off => "kapalı", _ => "hazır" };
            return $"{Tower.Name} · {st} · {(ammo == "" ? "boş" : ammo)} ({t.Loaded}/{Tower.Magazine}) · can {t.Hp:0}/{Tower.Hp:0} · öncelik: {DefenseSystem.PriorityName(t.Priority)}  ·  {{act:interact}}: {(DefenseSystem.NeedsRepair(t.Hp, Tower.Hp) ? "onar" : "doldur")}  ·  {{act:interact}} basılı: öncelik";
        }
        if (Trap != null && TrapState is TrapState s)
        {
            string st = TrapRules.Broken(s) ? "bakım ister" : Trap.Kind == "pit" ? (s.Open ? $"açık ({s.Held.Count}/{Trap.Capacity})" : "kapalı") : "kurulu";
            string act = Trap.Kind == "pit" ? "aç / kapat" : "onar";
            return $"{Trap.Name} · {st} · can {s.Hp:0}/{Trap.Hp:0}  ·  {{act:interact}}: {act}";
        }
        return Def.Name;
    }

    public override void _ExitTree()
    {
        // Insa'nin Sok kipi: listeden cikarilip siliniyor -> kural durumu da gider, depodaki cephane cantaya doner.
        if (IsQueuedForDeletion() && !_dd.Furniture.Contains(this)) _dd.Defense?.Forget(this);
        base._ExitTree();
    }
}
