using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Yabani yaratiklarin kayit bolumu "wildlife" (kayit sozlesmesi, TEKNIK §9.1): surular, uyeleri, sururler ve yeniden
/// dogma sayaclari duz DTO'ya (WildlifeSave) yazilir; yuklemede aktorler ayni durumla yeniden kurulur. Gorunus sinamasinin
/// sergi aktorleri (Spawn ile dogrudan kurulanlar) kayda girmez.
/// </summary>
public partial class Wildlife
{
    string RowKey(CreatureSpawnDef row)
    {
        foreach (var (id, b) in _d.Data.Biomes.Biomes)
            if (b.Spawns.Creatures.Contains(row)) return $"{id}:{row.Creature}";
        return ":" + row.Creature;
    }

    CreatureSpawnDef RowOf(string biome, string creature) =>
        _d.Data.Biomes.Biomes.TryGetValue(biome, out var b) ? b.Spawns.Creatures.FirstOrDefault(r => r.Creature == creature) : null;

    public WildlifeSave Capture()
    {
        var s = new WildlifeSave { NextId = _nextId, Rand = _rand };
        foreach (var (row, members) in _groups)
        {
            var key = RowKey(row).Split(':');
            var g = new WildGroupSave { Biome = key[0], Creature = row.Creature };
            foreach (var m in members.Where(IsInstanceValid))
            {
                if (m is WildSwarm sw) g.Swarm = sw.State;
                else if (m is WildCreature c)
                {
                    g.Herd = c.Herd;
                    g.Members.Add(new WildMemberSave { State = c.State, Y = c.GlobalPosition.Y, Yaw = c.Rotation.Y });
                }
            }
            s.Groups.Add(g);
        }
        foreach (var (row, t) in _respawn) if (t > 0) s.Respawn[RowKey(row)] = t;
        return s;
    }

    public void Apply(WildlifeSave s)
    {
        foreach (var g in _groups) foreach (var m in g.members) if (IsInstanceValid(m)) m.QueueFree();
        _groups.Clear(); _respawn.Clear(); Creatures.Clear(); Swarms.Clear();
        _nextId = s.NextId; _rand = s.Rand;
        foreach (var g in s.Groups)
        {
            var row = RowOf(g.Biome, g.Creature);
            var def = _d.Data.Creatures.Creatures.FirstOrDefault(c => c.Id == g.Creature);
            if (row == null || def == null) { GD.PrintErr($"WILDLIFE kayit: bilinmeyen {g.Biome}:{g.Creature}"); continue; }
            var members = new List<Node3D>();
            if (g.Swarm != null && def.Swarm != null)
            {
                var sw = new WildSwarm(def, _d.Data, _world, g.Swarm.Id, new Vector3(g.Swarm.HomeX, 0, g.Swarm.HomeZ), g.Swarm.Seed);
                CopyInto(g.Swarm, sw.State);
                AddChild(sw);
                if (g.Swarm.Swollen) sw.SnapSwell(true);
                Swarms.Add(sw); members.Add(sw);
            }
            else if (def.Wild != null && g.Herd != null)
            {
                bool lead = true;
                foreach (var m in g.Members)
                {
                    var c = new WildCreature(def, _d.Poses, _world, g.Herd, m.State) { LeadsHerd = lead };
                    lead = false;
                    AddChild(c);
                    c.GlobalPosition = new Vector3(m.State.X, m.Y, m.State.Z);
                    c.Rotation = new Vector3(0, m.Yaw, 0);
                    if (m.State.Swollen) c.SnapSwell(true);
                    Creatures.Add(c); members.Add(c);
                }
            }
            if (members.Count > 0) _groups.Add((row, members));
        }
        foreach (var (k, t) in s.Respawn)
        {
            var parts = k.Split(':');
            var row = parts.Length == 2 ? RowOf(parts[0], parts[1]) : null;
            if (row != null) _respawn[row] = t;
        }
        GD.Print($"WILDLIFE restored groups={_groups.Count} creatures={Creatures.Count} swarms={Swarms.Count}");
    }

    /// <summary>Duz durum sinifinin ozelliklerini kopyalar (WildSwarm.State salt okunur alan).</summary>
    static void CopyInto<T>(T from, T to)
    {
        foreach (var p in typeof(T).GetProperties().Where(p => p.CanRead && p.CanWrite)) p.SetValue(to, p.GetValue(from));
    }
}
