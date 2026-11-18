using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Biyom dogma tablolarinin (biomes.json "spawns", TEKNIK §7 "M2 dunyasi") Godot tarafi (dunya2). Kural Sim'de
/// (SpawnTable: secim, suru boyu, yer); aktoru yaratik akislari kaydeder:
///   d.Spawner.Register("dalli", (d, at, n) => ... yeni aktorler ...);
/// Kaydi olmayan satir atlanir (bugun Zipzip ZipzipMeadow ile, digerleri yaratik2 / hayvan akislariyla gelir).
/// Saat uymayan (gunduz satiri gece) ve oyuncudan uzak suru cekilir. Durum bu dugumde, statik yok (TEKNIK §9).
/// </summary>
public partial class BiomeSpawner : Node
{
    public delegate IReadOnlyList<Node3D> Factory(Director d, Vector3 at, int count);

    readonly Director _d;
    readonly Dictionary<string, Factory> _factories = new();
    readonly Rng _rng = new(GD.Randi() | 1UL);

    sealed class Group
    {
        public CreatureSpawnDef Def;
        public string Biome;
        public List<Node3D> Members = new();
    }
    readonly List<Group> _groups = new();
    readonly Dictionary<CreatureSpawnDef, float> _cooldown = new();
    float _tick;

    /// <summary>Uzaklik (m): oyuncu bundan uzaktaysa saati gecmis suru silinir.</summary>
    const float DespawnDistance = 80f;

    public BiomeSpawner(Director d) { _d = d; Name = "BiomeSpawner"; }

    public void Register(string creature, Factory f) => _factories[creature] = f;
    public bool Known(string creature) => _factories.ContainsKey(creature);
    public int Alive(string creature) => _groups.Where(g => g.Def.Creature == creature).Sum(g => g.Members.Count);

    bool Night => _d.State.Calendar.IsNight || _d.Phase is Director.Act.Dusk or Director.Act.Night;

    public override void _Process(double delta)
    {
        _tick -= (float)delta;
        if (_tick > 0 || _factories.Count == 0) return;
        _tick = 2f;
        foreach (var g in _groups) g.Members.RemoveAll(m => !IsInstanceValid(m));
        bool night = Night;
        var p = _d.Player.GlobalPosition;
        // Saati gecmis ve uzak suruler cekilir; olen suru yerine Respawn sonra yenisi.
        foreach (var g in _groups.ToList())
        {
            bool stale = !SpawnTable.TimeMatches(g.Def.Time, night);
            bool far = g.Members.All(m => m.GlobalPosition.DistanceTo(p) > DespawnDistance);
            if (stale && far) { foreach (var m in g.Members) m.QueueFree(); g.Members.Clear(); }
            if (g.Members.Count == 0) { _groups.Remove(g); _cooldown[g.Def] = g.Def.Respawn; }
        }
        foreach (var k in _cooldown.Keys.ToList()) _cooldown[k] -= 2f;
        var table = _d.State.Spawns;
        var ocak = _d.OcakPos;
        foreach (var biome in _d.State.BiomeMap.Order)
        {
            var def = table.Pick(biome, night, _rng,
                c => _groups.Count(g => g.Def == c) + (_cooldown.TryGetValue(c, out var cd) && cd > 0 ? 999 : 0),
                Known);
            if (def == null) continue;
            var at = table.Place(biome, def, _rng, ocak.X, ocak.Z, _d.State.Pois);
            if (at == null) continue;
            var pos = _d.World.Ground(new Vector3(at.Value.x, 0, at.Value.z));
            int n = SpawnTable.GroupSize(def, _rng);
            var made = _factories[def.Creature](_d, pos, n);
            if (made == null || made.Count == 0) continue;
            _groups.Add(new Group { Def = def, Biome = biome, Members = made.ToList() });
            GD.Print($"SPAWN {biome} {def.Creature} x{made.Count} at={pos.X:0},{pos.Z:0} night={night}");
        }
    }
}
