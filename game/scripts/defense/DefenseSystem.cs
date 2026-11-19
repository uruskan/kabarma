using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kuleler ve tuzaklar (GDD §16.4 M3, ICERIK §13). Kurallar Sim'de (TowerRules, TrapRules; durum GameState.Defense,
/// kayit bolumu "defense"); burasi hedef listesini kurar, kuleleri dondurup atar, mermileri ucurur, tuzak etkilerini
/// yaratiga ve oyuncuya uygular, E ile doldurma / onarim / oncelik / kapak islerini yapar. Ortak oyunda kurallar
/// host'ta isler (DefenseSystem.Net.cs). Hedefler M3 sozlesmeleri gelene dek gece dalgasinin Zipzip'leri (GECICI).
/// </summary>
public partial class DefenseSystem : Node3D
{
    public enum Op : byte { Load = 1, Repair = 2, Priority = 3, Toggle = 4 }

    readonly Director _d;
    readonly Dictionary<int, DefenseNode> _nodes = new();
    readonly Dictionary<int, Zipzip> _zips = new();
    readonly Dictionary<ulong, (Vector3 pos, Vector3 vel)> _track = new();
    readonly List<DefenseTarget> _targets = new();
    float _holdT;
    DefenseNode _focus;

    public DefenseSystem(Director d) { _d = d; Name = "DefenseSystem"; }

    public DefenseFile F => _d.Data.Defense;
    public DefenseState S => _d.State.Defense;
    public IEnumerable<DefenseNode> Nodes => _nodes.Values.Where(IsInstanceValid);
    public int Shots { get; private set; }
    public int Kills { get; private set; }
    public int TrapHits { get; private set; }

    public bool Handles(string id) => F.Handles(id);
    public static bool NeedsRepair(float hp, float max) => hp < max * 0.5f;

    public static string PriorityName(string p) => p switch
    {
        "nearest" => "en yakın", "strongest" => "en güçlü", "flying" => "uçan", "thief" => "hırsız", "off" => "kapalı", _ => p,
    };

    // ---------------------------------------------------------------- kurma, sokme, kayittan donus

    /// <summary>Director.PlaceFurniture: kule ya da tuzak kurar; kayitli durum ayni yerdeyse onu alir.</summary>
    public DefenseNode Place(PlaceableDef def, Vector3 at, float yaw, bool fx = true)
    {
        var n = new DefenseNode(_d, def);
        _d.AddChild(n);
        n.GlobalPosition = at;
        n.Rotation = new Vector3(0, yaw, 0);
        _d.Furniture.Add(n);
        int id = S.FindAt(def.Id, ToN(at));
        if (id == 0)
        {
            id = F.Tower(def.Id) is TowerDef td ? S.AddTower(td, ToN(at), yaw).Id : S.AddTrap(F.Trap(def.Id), ToN(at), yaw).Id;
        }
        n.StateId = id;
        _nodes[id] = n;
        if (fx)
        {
            Sfx.Play("place", at, 2f);
            Fx.Burst(at + Vector3.Up * 0.3f, Palette.Wood, 20, 4f, 0.1f, 0.7f);
            GD.Print($"PLACED {def.Id} at {at} defense={id}");
            Events.RaiseStationPlaced(def.Id);
        }
        return n;
    }

    /// <summary>Sokulen yapi: durum gider, depodaki cephane cantaya doner.</summary>
    public void Forget(DefenseNode n)
    {
        _nodes.Remove(n.StateId);
        if (_d.NetClient) return; // durum host'tan gelir
        if (S.Tower(n.StateId) is TowerState t)
            foreach (var (item, k) in t.Mag) _d.State.Backpack.Add(item, k);
        S.Remove(n.StateId);
        GD.Print($"DEFENSE removed {n.Def.Id}({n.StateId})");
    }

    /// <summary>Kayit yuklenince ya da agdan durum gelince: her duruma bir dugum, durumsuz dugum yok.</summary>
    public void Rebuild()
    {
        foreach (var n in _d.Furniture.OfType<DefenseNode>().ToList())
        {
            if (!IsInstanceValid(n)) continue;
            int id = S.FindAt(n.Def.Id, ToN(n.GlobalPosition));
            if (id == 0) { _d.Furniture.Remove(n); _nodes.Remove(n.StateId); n.StateId = -1; n.QueueFree(); continue; }
            _nodes.Remove(n.StateId);
            n.StateId = id; _nodes[id] = n;
        }
        foreach (var (def, id, p, yaw) in S.Towers.Select(t => (t.Def, t.Id, t.At, t.Yaw)).Concat(S.Traps.Select(t => (t.Def, t.Id, t.At, t.Yaw))).ToList())
        {
            if (_nodes.TryGetValue(id, out var have) && IsInstanceValid(have)) continue;
            var pdef = _d.Data.Tool.Placeables.FirstOrDefault(x => x.Id == def);
            if (pdef != null) Place(pdef, new Vector3(p.X, p.Y, p.Z), yaw, fx: false);
        }
    }

    static System.Numerics.Vector3 ToN(Vector3 v) => new(v.X, v.Y, v.Z);
    static Vector3 G(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    // ---------------------------------------------------------------- hedefler (GECICI: dalga Zipzip'leri)

    void CollectTargets(float dt)
    {
        _targets.Clear(); _zips.Clear();
        var seen = new HashSet<ulong>();
        foreach (var z in Zipzip.All)
        {
            if (!IsInstanceValid(z) || z.Dead || !z.IsInsideTree() || z.Display || z.Mode != ZipzipMode.Wave) continue;
            ulong key = z.GetInstanceId();
            seen.Add(key);
            var pos = z.GlobalPosition;
            var vel = Vector3.Zero;
            if (_track.TryGetValue(key, out var tr) && dt > 0) vel = tr.vel.Lerp((pos - tr.pos) / dt, Mathf.Min(1f, dt * 4f));
            _track[key] = (pos, vel);
            int id = (int)(key & 0x7fffffff);
            _zips[id] = z;
            _targets.Add(new DefenseTarget(id, ToN(z.Center), ToN(vel with { Y = 0 }), z.Radius, z.Hp, z.MaxHp));
        }
        foreach (var k in _track.Keys.Where(k => !seen.Contains(k)).ToList()) _track.Remove(k);
        if (_d.Player != null && !_d.Player.Down) _targets.Add(new DefenseTarget(-1, ToN(_d.Player.GlobalPosition), default, 0.4f, _d.Player.Hp, _d.Player.MaxHp, IsPlayer: true));
    }

    /// <summary>Sinama icin: o anki hedef sayisi (oyuncu haric).</summary>
    public int TargetCount => _targets.Count(t => !t.IsPlayer);

    void Hurt(int id, float dmg, Vector3 from, float knock)
    {
        if (!_zips.TryGetValue(id, out var z) || !IsInstanceValid(z) || z.Dead) return;
        var dir = z.GlobalPosition - from; dir.Y = 0;
        z.Hit(DefenseHit.Apply(dmg), false, dir.LengthSquared() > 1e-4f ? dir.Normalized() : Vector3.Forward, knock, false);
        if (z.Dead) { Kills++; GD.Print($"DEFENSE kill total={Kills}"); }
    }

    // ---------------------------------------------------------------- dongu

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        CollectTargets(dt);
        TickProjectiles(dt); // DefenseSystem.Shots.cs
        if (_d.NetClient) return; // ortak: kural host'ta
        foreach (var n in Nodes.ToList())
        {
            if (n.Tower != null && n.TowerState is TowerState t) TickTower(n, t, dt);
            else if (n.Trap != null && n.TrapState is TrapState s) TickTrap(n, s, dt);
        }
    }

    void TickTower(DefenseNode n, TowerState t, float dt)
    {
        var d = n.Tower;
        var at = ToN(n.GlobalPosition);
        int i = TowerRules.Pick(d, at, _targets, t.Priority);
        bool ready = TowerRules.Tick(t, dt);
        if (i < 0) return;
        var x = _targets[i];
        var aimPt = d.Kind == "arc" ? G(x.Pos + x.Vel * d.FlightSeconds) : d.Kind == "single" ? G(TowerRules.Lead(ToN(n.Muzzle), x.Pos, x.Vel, d.Speed)) : G(x.Pos);
        bool aimed = n.Aim(aimPt, dt);
        if (!ready || !aimed) return;
        string item = TowerRules.Fire(t, d);
        if (item == null) return;
        Shots++;
        n.Kick();
        Shoot(n, d, item, x, aimPt); // DefenseSystem.Shots.cs
        if (TowerRules.Status(t) == TowerStatus.Broken) GD.Print($"DEFENSE {d.Id}({t.Id}) bakim ister");
    }

    void TickTrap(DefenseNode n, TrapState s, float dt)
    {
        var d = n.Trap;
        foreach (var e in TrapRules.Step(s, d, _targets, dt))
        {
            TrapHits++;
            if (e.Target == -1) { if (e.Kind == TrapFx.Launch) LaunchPlayer(n, d, G(e.Dir)); continue; }
            if (!_zips.TryGetValue(e.Target, out var z) || !IsInstanceValid(z) || z.Dead) continue;
            switch (e.Kind)
            {
                case TrapFx.Damage: Hurt(e.Target, e.Value, n.GlobalPosition, 0f); Vfx.Play("trap_diken", z.GlobalPosition + Vector3.Up * 0.2f); break;
                case TrapFx.Slow: z.DefenseSlow(e.Seconds); break;
                case TrapFx.Hold:
                    z.DefenseHold(e.Seconds);
                    if (d.Kind == "pit") z.GlobalPosition = PitSpot(n, d, s.Held.IndexOf(e.Target));
                    else if (e.Seconds > 1f) { Vfx.Play("trap_yapiskan", z.GlobalPosition + Vector3.Up * 0.2f); Sfx.Play("hit", z.GlobalPosition, -8f, 0.6f); }
                    break;
                case TrapFx.Launch:
                    n.Kick();
                    z.DefenseLaunch(d, G(e.Dir), e.Value);
                    Sfx.Play("firlatma", n.GlobalPosition, -2f, 1.2f);
                    Vfx.Play("trap_firlatma", n.GlobalPosition + Vector3.Up * 0.3f);
                    break;
                case TrapFx.Fall:
                    z.DefenseHold(0.5f);
                    z.GlobalPosition = PitSpot(n, d, s.Held.IndexOf(e.Target));
                    Sfx.Play("hit", n.GlobalPosition, -2f, 0.7f);
                    Vfx.Play("trap_cukur", n.GlobalPosition + Vector3.Up * 0.1f);
                    GD.Print($"DEFENSE pit fall held={s.Held.Count}");
                    break;
                case TrapFx.Release:
                    var outAt = n.GlobalPosition + n.GlobalBasis.Z * (d.HalfSize + 0.6f);
                    z.GlobalPosition = _d.World.Ground(outAt);
                    break;
            }
        }
    }

    Vector3 PitSpot(DefenseNode n, TrapDef d, int slot)
    {
        float a = slot * 2.1f;
        return n.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * d.HalfSize * 0.45f + Vector3.Down * d.Depth * 0.85f;
    }

    void LaunchPlayer(DefenseNode n, TrapDef d, Vector3 dir)
    {
        var p = _d.Player;
        if (p == null || p.Down) return;
        n.Kick();
        p.Velocity = dir * d.PlayerForwardSpeed + Vector3.Up * d.PlayerUpSpeed;
        p.Shake(0.3f);
        Sfx.Play("firlatma", n.GlobalPosition, -2f, 1.3f);
        GD.Print("DEFENSE launch player");
    }

    // ---------------------------------------------------------------- E

    DefenseNode FocusNode()
    {
        if (_d.Player == null) return null;
        var p = _d.Player.GlobalPosition;
        return Nodes.Where(n => n.InReach(p)).OrderBy(n => n.GlobalPosition.DistanceTo(p)).FirstOrDefault();
    }

    /// <summary>Director.Interact: kisa E. Isledi ise true.</summary>
    public bool Interact()
    {
        var n = FocusNode();
        if (n == null) return false;
        if (n.Tower != null && n.TowerState is TowerState t) Act(NeedsRepair(t.Hp, n.Tower.Hp) ? Op.Repair : Op.Load, n);
        else if (n.Trap != null && n.TrapState is TrapState s)
        {
            if (n.Trap.Kind == "pit" && !TrapRules.Broken(s)) Act(Op.Toggle, n);
            else if (s.Hp < n.Trap.Hp - 0.01f) Act(Op.Repair, n);
            else _d.Hud.Hint($"{n.Trap.Name}: bakım gerekmiyor", 1.5f);
        }
        return true;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _focus = FocusNode();
        if (_focus == null || _d.Hud.MenuOpen) { _holdT = 0; return; }
        _d.Hud.Hint(_focus.Status(), 0.2f);
        if (_focus.Tower == null) return;
        if (Input.IsActionPressed("interact"))
        {
            _holdT += dt;
            if (_holdT >= F.InteractHoldSeconds) { Act(Op.Priority, _focus); _holdT = -0.35f; }
        }
        else _holdT = 0;
    }

    /// <summary>Eylem: istemcide host'a gider; host ve tek oyuncuda yerel cantayla.</summary>
    public void Act(Op op, DefenseNode n)
    {
        if (ForwardAct(op, n)) return; // DefenseSystem.Net.cs
        Apply(op, n.StateId, _d.State.Backpack, true);
    }

    /// <summary>Eylemin kendisi (bag: aktorun cantasi). local: ipucu ve ses bu makinede.</summary>
    public bool Apply(Op op, int id, Inventory bag, bool local)
    {
        var t = S.Tower(id); var s = S.Trap(id);
        var node = _nodes.GetValueOrDefault(id);
        string name = node?.Def.Name ?? "?";
        void Say(string m, float sec = 1.8f) { if (local) _d.Hud.Hint(m, sec); }
        switch (op)
        {
            case Op.Load when t != null:
            {
                var d = F.Tower(t.Def);
                int k = TowerRules.Load(t, d, bag);
                if (k == 0) { Say(t.Loaded >= d.Magazine ? $"{name}: depo dolu" : $"{name}: çantada {string.Join(" ya da ", d.Ammo.Select(ItemNames.Of))} yok"); return false; }
                Say($"{name}: +{k} cephane ({t.Loaded}/{d.Magazine})");
                if (local && node != null) Fx.FlyTo(_d.Player.BackpackPoint.GlobalPosition, () => node.GlobalPosition + Vector3.Up * 1.5f, Palette.Accent, 0.12f);
                Sfx.Play("pickup", node?.GlobalPosition, -6f, 1.2f);
                GD.Print($"DEFENSE load {t.Def}({id}) +{k} = {t.Loaded}");
                return true;
            }
            case Op.Repair:
            {
                string def = t?.Def ?? s?.Def;
                if (def == null) return false;
                float max = F.MaxHp(def);
                float hp = t?.Hp ?? s.Hp;
                var recipe = _d.Data.Tool.RecipeFor(def);
                var cost = TowerRules.RepairCost(recipe?.In ?? new Dictionary<string, int>(), hp, max, F.RepairFraction);
                if (cost.Count == 0) { Say($"{name}: bakım gerekmiyor"); return false; }
                if (cost.Any(kv => bag.Count(kv.Key) < kv.Value)) { Say($"{name}: onarım için {string.Join(", ", cost.Select(kv => $"{kv.Value} {ItemNames.Of(kv.Key)}"))} gerek"); return false; }
                foreach (var (item, k) in cost) bag.Remove(item, k);
                if (t != null) t.Hp = max; else s.Hp = max;
                Say($"{name}: onarıldı");
                Sfx.Play("place", node?.GlobalPosition, -4f, 1.1f);
                GD.Print($"DEFENSE repair {def}({id}) cost={string.Join(",", cost.Select(kv => $"{kv.Key}:{kv.Value}"))}");
                return true;
            }
            case Op.Priority when t != null:
                t.Priority = TowerRules.NextPriority(F, t.Priority);
                Say($"{name}: öncelik {PriorityName(t.Priority)}");
                Sfx.Play("place", node?.GlobalPosition, -10f, 1.5f);
                GD.Print($"DEFENSE priority {t.Def}({id}) {t.Priority}");
                return true;
            case Op.Toggle when s != null:
                TrapRules.Toggle(s);
                Say($"{name}: {(s.Open ? "açık" : "kapalı")}");
                Sfx.Play("place", node?.GlobalPosition, -4f, s.Open ? 0.8f : 1.1f);
                GD.Print($"DEFENSE pit {(s.Open ? "open" : "closed")} ({id})");
                return true;
        }
        return false;
    }
}
