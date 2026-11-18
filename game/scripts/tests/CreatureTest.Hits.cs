using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=creatures, Capa vuruslari (Wildlife.Strike; Capa.Weapons'in Tirpan ve Civi mermileri bunu cagirir):
///  - mermi dogrusu yaratigi bulur, can duser, 12 m icindeki suru arkadasi urker ve kacar;
///  - can bitince "Down" olayi yayilir, yaratik sahneden kalkar ve bir daha vurulmaz;
///  - suru tek can havuzudur: vurulunca havuz duser, bitince "Down".
/// </summary>
public partial class CreatureTest
{
    void CheckHits(GameData data, PoseLibrary poses)
    {
        var def = data.Creatures.Creatures.FirstOrDefault(c => c.Wild is { FleeOnHit: true } w && w.Stance != WildStance.Predator && w.Active != "night" && c.Hp > 0 && c.Rig != null);
        var sdef = data.Creatures.Creatures.FirstOrDefault(c => c.Swarm != null);
        if (def == null || sdef == null) { _fail.Add("vurus: uygun tur yok"); return; }
        var downs = new List<string>();
        void OnActed(string sp, string what, float x, float z) { if (what == "Down") downs.Add(sp); }
        Events.WildActed += OnActed;

        var world = new FlatWorld { Player = new Vector3(3000, 0, 3000) };
        var a = NewWild(def, poses, world, new Vector3(2000, 0, 0));
        var b = NewWild(def, poses, world, new Vector3(2004, 0, 0));
        var creatures = new List<WildCreature> { a, b };
        var swarms = new List<WildSwarm>();
        Run(a.Tick, 0.5f); Run(b.Tick, 0.5f);

        // Ilk vurus: can duser, komsu urker.
        Vector3 Mid(Node3D n) => n.GlobalPosition + Vector3.Up * 0.4f;
        float hp0 = a.State.Hp, dmg = Mathf.Ceil(hp0 / 3f);
        var hit = Wildlife.Strike(creatures, swarms, Mid(a) + new Vector3(0, 0, -3), Mid(a) + new Vector3(0, 0, 3), 0.1f, dmg);
        if (hit != a || a.State.Hp >= hp0) Fail(def.Id, $"vurus degmedi (hp {a.State.Hp}/{hp0})");
        world.Player = a.GlobalPosition + new Vector3(-6, 0, 0);
        b.Tick(Dt);
        if (b.State.Mode != WildMode.Flee) Fail(def.Id, $"komsu urkmedi (mod {b.State.Mode})");
        var miss = Wildlife.Strike(creatures, swarms, Mid(a) + new Vector3(0, 30, -3), Mid(a) + new Vector3(0, 30, 3), 0.1f, dmg);
        if (miss != null) Fail(def.Id, "ustten gecen mermi degdi");

        // Can biter: Down, dugum kalkar, ikinci kez vurulmaz.
        for (int i = 0; i < 6 && !a.Dead; i++) Wildlife.Strike(new List<WildCreature> { a }, swarms, Mid(a) + new Vector3(0, 0, -3), Mid(a) + new Vector3(0, 0, 3), 0.1f, dmg);
        if (!a.Dead || !downs.Contains(def.Id) || !a.IsQueuedForDeletion()) Fail(def.Id, $"olmedi / Down yok (hp {a.State.Hp}, down {downs.Count})");
        if (Wildlife.Strike(new List<WildCreature> { a }, swarms, Mid(a) + new Vector3(0, 0, -3), Mid(a) + new Vector3(0, 0, 3), 0.1f, dmg) != null) Fail(def.Id, "olu yaratik yine vuruldu");

        // Suru: tek can havuzu.
        var s = new WildSwarm(sdef, data, world, 9, new Vector3(2100, 0, 0), 777) { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(s);
        Run(s.Tick, 0.5f);
        swarms.Add(s);
        var sc = s.GlobalPosition + Vector3.Up * sdef.Swarm!.Height;
        float shp = s.State.Hp;
        for (int i = 0; i < 200 && !s.Dead; i++)
        {
            if (Wildlife.Strike(new List<WildCreature>(), swarms, sc + new Vector3(0, 0, -5), sc + new Vector3(0, 0, 5), 0.2f, Mathf.Max(1f, shp / 4f)) == null) break;
            sc = s.GlobalPosition + Vector3.Up * sdef.Swarm.Height;
        }
        if (!s.Dead || !downs.Contains(sdef.Id)) Fail(sdef.Id, $"suru havuzu bitmedi (hp {s.State.Hp}/{shp})");

        Events.WildActed -= OnActed;
        GD.Print($"WILD hits {def.Id} hp={hp0}->{a.State.Hp} komsu={b.State.Mode} suru {sdef.Id} {shp}->{s.State.Hp} down={string.Join(",", downs)}");
    }
}
