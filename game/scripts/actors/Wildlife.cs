using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Yabani yaratiklar (ICERIK §3.8): dunya2'nin dogma tablosuna (biomes.json spawns.creatures, Sim SpawnTable ve BiomeMap)
/// gore suruler ve sururler kurar. Aktoru yaratik2'de olan satirlar (creatures.json wild ya da swarm) burada dogar;
/// Zipzip ZipzipMeadow'da kalir. Oyuncudan DespawnDistance'tan uzaklasan suru kalkar, satirin Respawn suresi sonra yenisi
/// gelebilir. Kabarma gecesi hepsi cayirdan kacip cekilir (dalga olarak gelirler, GDD §14.2); normal gecede gunduzculer
/// uyur, gececiler uyanir (WildBrain.Awake). Butun durum bu dugumde (statik yok, TEKNIK §9).
/// </summary>
public partial class Wildlife : Node3D
{
    /// <summary>Dogma yeri oyuncuya en cok bu kadar uzak olur; suru bundan uzaklasinca kalkar (m).</summary>
    const float SpawnDistance = 170f, DespawnDistance = 240f, NearPlayer = 25f, ScanSeconds = 2f;
    readonly Director _d;
    readonly IWildWorld _world;
    public readonly List<WildCreature> Creatures = new();
    public readonly List<WildSwarm> Swarms = new();
    readonly List<(CreatureSpawnDef row, List<Node3D> members)> _groups = new();
    readonly Dictionary<CreatureSpawnDef, float> _respawn = new();
    readonly Rng _rng;
    SpawnTable _table;
    ulong _rand;
    int _nextId = 1;
    float _leaveT = -1, _scanT;
    /// <summary>Kendi dogma dongusu. dunya2'nin Godot dogurucusu gelince false yapilip Spawn fabrika olarak kullanilir.</summary>
    public bool OwnSpawning = !Main.Args.ContainsKey("nowild"); // --nowild: yabani dogma kapali (karsilastirma)

    public Wildlife(Director d)
    {
        _d = d;
        _world = new DirectorWildWorld(d);
        _rand = GD.Randi() | 1UL;
        _rng = new Rng(_rand);
    }

    public IWildWorld World => _world;

    /// <summary>Aktoru bu dugumde olan tur (dogma satiri buraya gelir).</summary>
    public bool Knows(string creature) =>
        _d.Data.Creatures.Creatures.FirstOrDefault(c => c.Id == creature) is { } c && (c.Wild != null || c.Swarm != null);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        TickSiraGate(dt); // gorunum araci: Wildlife.Sira.cs
        Creatures.RemoveAll(c => !IsInstanceValid(c) || c.Dead);
        Swarms.RemoveAll(s => !IsInstanceValid(s) || s.Dead);
        foreach (var g in _groups) g.members.RemoveAll(m => !IsInstanceValid(m) || m is WildCreature { Dead: true } || m is WildSwarm { Dead: true });
        foreach (var g in _groups.Where(g => g.members.Count == 0)) _respawn[g.row] = g.row.Respawn;
        _groups.RemoveAll(g => g.members.Count == 0);
        // Surunun lideri olmeyen ilk uye
        foreach (var g in _groups)
        {
            var wc = g.members.OfType<WildCreature>().ToList();
            if (wc.Count > 0 && !wc.Any(c => c.LeadsHerd)) wc[0].LeadsHerd = true;
        }

        if (!WildBrain.Present(_d.NightActive))
        {
            // Kabarma gecesi: hepsi Ocak'tan disa kacar, sonra sahneden kalkar.
            if (_leaveT < 0)
            {
                _leaveT = 4f;
                var o = _d.OcakPos;
                foreach (var c in Creatures) { if (c.Herd != null && c.Def.Wild != null) WildBrain.Startle(c.Def.Wild, c.Herd, o.X, o.Z); c.Startle(); }
                GD.Print($"WILDLIFE leave creatures={Creatures.Count} swarms={Swarms.Count}");
            }
            _leaveT -= dt;
            if (_leaveT <= 0)
            {
                foreach (var g in _groups) foreach (var m in g.members) m.QueueFree();
                _groups.Clear();
            }
            return;
        }
        _leaveT = -1;
        foreach (var k in _respawn.Keys.ToList()) _respawn[k] -= dt;
        if (!OwnSpawning) return;

        _scanT -= dt;
        if (_scanT > 0) return;
        _scanT = ScanSeconds;
        var pp = _d.Player.GlobalPosition with { Y = 0 };
        // Uzaklasan suru kalkar (yeri bosalir, satirin suresi sonra yenisi gelebilir).
        foreach (var g in _groups.Where(g => g.members.All(m => m.GlobalPosition.DistanceTo(pp) > DespawnDistance)).ToList())
        {
            foreach (var m in g.members) m.QueueFree();
            _groups.Remove(g);
            _respawn[g.row] = g.row.Respawn;
        }
        _table ??= new SpawnTable(_d.Data.Biomes, _d.State.BiomeMap);
        bool night = _d.State.Calendar.IsNight;
        var o2 = _d.OcakPos;
        foreach (var biome in _d.Data.Biomes.Biomes.Keys)
        {
            var row = _table.Pick(biome, night, _rng, r => _groups.Count(g => g.row == r) + (_respawn.TryGetValue(r, out var rt) && rt > 0 ? r.MaxGroups : 0), Knows);
            if (row == null) continue;
            var at = _table.Place(biome, row, _rng, o2.X, o2.Z, _d.State.Pois, 60); // kesif noktalarindan uzak, satirin nearPoi'si (dunya2)
            if (at == null) continue;
            var p = new Vector3(at.Value.x, 0, at.Value.z);
            float d = p.DistanceTo(pp);
            if (d > SpawnDistance || d < NearPlayer) continue;
            var members = Spawn(_d.Data.Creature(row.Creature), p, count: SpawnTable.GroupSize(row, _rng));
            _groups.Add((row, members));
            _respawn[row] = 0;
        }
    }
    /// <summary>at noktasinda turun bir surusunu kurar (dogma satirindan ve gorunus sinamasindan).</summary>
    public List<Node3D> Spawn(CreatureDef def, Vector3 at, bool display = false, int count = 1)
    {
        var list = new List<Node3D>();
        at = _world.Ground(at);
        if (def.Swarm != null)
        {
            ulong seed = (ulong)(WildRandom.Hash(_rand, _nextId, 9) * uint.MaxValue) | 1UL;
            var s = new WildSwarm(def, _d.Data, _world, _nextId++, at, seed) { Display = display };
            AddChild(s);
            s.GlobalPosition = at;
            Swarms.Add(s);
            list.Add(s);
            GD.Print($"WILDLIFE spawn {def.Id} swarm at={at.X:0},{at.Z:0}");
            return list;
        }
        if (def.Wild == null) return list;
        var herd = new WildHerdState { Id = _nextId++, Species = def.Id, HomeX = at.X, HomeZ = at.Z, WanderX = at.X, WanderZ = at.Z, Rand = _rand ^ (ulong)_nextId * 0x9E3779B97F4A7C15UL | 1UL };
        int n = Mathf.Max(1, count);
        for (int i = 0; i < n; i++)
        {
            float a = i * 2.4f, r = i == 0 ? 0 : 1.2f + 0.5f * i;
            var st = new WildState { Id = _nextId++, Herd = herd.Id, SlotX = Mathf.Cos(a) * r, SlotZ = Mathf.Sin(a) * r, Rand = _rand ^ ((ulong)_nextId << 7) | 1UL };
            var c = new WildCreature(def, _d.Poses, _world, herd, st) { LeadsHerd = i == 0, Display = display };
            AddChild(c);
            c.GlobalPosition = _world.Ground(at + new Vector3(st.SlotX, 0, st.SlotZ));
            c.Rotation = new Vector3(0, WildRandom.Next(ref _rand) * Mathf.Tau, 0);
            Creatures.Add(c);
            list.Add(c);
        }
        GD.Print($"WILDLIFE spawn {def.Id} herd={n} at={at.X:0},{at.Z:0}");
        return list;
    }
}

/// <summary>Director'u yabani yaratiga dar bir arayuzle acar: zemin, oyuncu, gece, hedefler, olaylar.</summary>
public sealed class DirectorWildWorld : IWildWorld
{
    readonly Director _d;
    public DirectorWildWorld(Director d) { _d = d; }

    public Vector3 Ground(Vector3 p) => _d.World.Ground(p);
    public Vector3 PlayerPos => _d.Player.GlobalPosition;
    public bool Night => _d.State.Calendar.IsNight;

    public bool FindTarget(string kind, Vector3 from, float range, out Vector3 at)
    {
        at = default;
        IEnumerable<Node3D> cands = kind switch
        {
            "crop" or "flower" => _d.FarmView.Views.Where(v => Herds.Attracts(v.Plot)),
            "grain" => _d.FarmView.Views.Where(v => Herds.Attracts(v.Plot) && _d.Data.Crop(v.Plot.Crop!).Family == "grain"),
            "seed" => _d.FarmView.Views.Where(v => v.Plot.Planted && v.Plot.Stage is CropStage.Seed or CropStage.Sprout),
            "coop" => _d.GetTree().GetNodesInGroup("gidak").OfType<Node3D>(),
            _ => Enumerable.Empty<Node3D>(),
        };
        var best = cands.Where(IsValid).OrderBy(n => n.GlobalPosition.DistanceSquaredTo(from)).FirstOrDefault();
        if (best == null || best.GlobalPosition.DistanceTo(from) > range) return false;
        at = best.GlobalPosition;
        return true;
    }

    static bool IsValid(Node3D n) => GodotObject.IsInstanceValid(n) && n.IsInsideTree();

    public void Report(WildCreature c, WildEvent e)
    {
        var p = c.GlobalPosition;
        Events.RaiseWildActed(c.Def.Id, e.ToString(), p.X, p.Z);
        // Gunduz vurusu M2'de kucuk hasardir (GDD §16.2 "Gunduz tavri"); tam savas M3.
        if (e == WildEvent.Struck && c.Def.Wild != null && c.Def.Wild.AttackDamage > 0 && p.DistanceTo(_d.Player.GlobalPosition) < c.Def.Wild.AttackRange * 1.6f)
            _d.Player.Hurt(c.Def.Wild.AttackDamage, p);
        if (e is WildEvent.Warned or WildEvent.Struck or WildEvent.Took) GD.Print($"WILD {c.Def.Id}#{c.State.Id} {e} at={p.X:0.0},{p.Z:0.0}");
    }

    public void ReportSwarm(WildSwarm s, WildEvent e, bool sting)
    {
        var p = s.GlobalPosition;
        Events.RaiseWildActed(s.Def.Id, e.ToString(), p.X, p.Z);
        if (sting && s.Def.Swarm != null && s.Def.Swarm.StingDamage > 0) _d.Player.Hurt(s.Def.Swarm.StingDamage, p);
        if (e != WildEvent.None) GD.Print($"WILD {s.Def.Id}#{s.State.Id} {e} at={p.X:0.0},{p.Z:0.0}");
    }
}
