using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kabarma'da hayvanlar (GDD 9.5; Sim Husbandry.Combat.cs). Kabarma basinda Savas Agili'ndaki Savascilar kabarir ve
/// agillarinin cevresinde en yakin dusmana saldirir; ahirdakiler ve direge bagli olanlar saklanir (gorunmez). Cani
/// biten bayilir ve yatar; safakta agilinda yorgun uyanir. Ahir yikilirsa saklananlar kacisir. Ahir Zili ve Eyerlik E'si.
///
/// GECICI (kabarma3 Wave sozlesmesi main'e girene kadar): dusman = Zipzip.All; Zipzip'in hayvana vurusu temas kurali
/// (husbandry.json combat.enemyReach, enemyHitInterval; hasar creatures.json zipzip.damage). Sozlesme gelince yalniz
/// Enemies() ve EnemyHits() degisir. Yapay zeka yalniz host'ta (ortak oyunda istemci kuklayi gorur).
/// Ilk gece (ogretici) dokunulmaz: Director.Night Gidak ve Pofuduk'u kendisi sisirir.
/// </summary>
public partial class RanchSystem
{
    readonly Dictionary<int, float> _warT = new(), _hurtT = new();
    float _warScan;
    bool _warBound;

    AnimalCombatDef WarDef => Ranch.Rules.Def.Combat;
    bool IsHost => _d.Net?.IsClient != true;

    /// <summary>_Ready'den bir kez: gece ve safak olaylari, ortak oyun istekleri.</summary>
    void SetupWar()
    {
        if (_warBound) return;
        _warBound = true;
        Events.NightStarted += OnKabarma;
        Events.Dawn += OnWarDawn; // Ranch.Dawn'dan sonra abone: uyanan hayvanin konumu hazir
        _d.Net?.On("ranch.bell", (actor, r) => { var p = FindProp(WarDef.Bell, r.F32(), r.F32()); if (p != null && Near(actor, p.GlobalPosition, 4f)) Ring(p); });
        _d.Net?.On("ranch.saddle", (actor, r) =>
        {
            var p = FindProp(Ranch.Rules.Def.Mount.Rack, r.F32(), r.F32()); var a = Ranch.ById((int)r.VarU());
            if (p != null && a != null && Near(actor, p.GlobalPosition, 4f) && Ranch.Mount.Saddle(a, actor.Bag)) Saddled(a);
        });
        RegisterMountNet(); // Mount.cs
    }

    static bool Near(NetActor a, Vector3 p, float r) => !a.HasFrame || new Vector2(a.Last.X - p.X, a.Last.Z - p.Z).Length() <= r;

    /// <summary>Ortak oyun: istekteki konuma en yakin (0,75 m) bu turden agil nesnesi (dugum kimligi uclar arasi farkli).</summary>
    PenProp FindProp(string def, float x, float z) => _d.Pens?.Props.Where(p => IsInstanceValid(p) && p.Def.Id == def)
        .OrderBy(p => new Vector2(p.GlobalPosition.X - x, p.GlobalPosition.Z - z).Length())
        .FirstOrDefault(p => new Vector2(p.GlobalPosition.X - x, p.GlobalPosition.Z - z).Length() < 0.75f);

    bool TutorialNight => !TutorialDone;

    void OnKabarma()
    {
        if (TutorialNight || !IsHost) return;
        var ev = Ranch.KabarmaBegin(_d.Pens.Pens, id => _d.Data.Creature(id).Hp);
        foreach (var e in ev) ShowWar(e);
        GD.Print($"RANCH kabarma fighters={ev.Count(e => e.Kind == WarEventKind.Swelled)} hidden={ev.Count(e => e.Kind == WarEventKind.Hid)}");
    }

    void OnWarDawn()
    {
        foreach (var ag in _agents.Where(IsInstanceValid))
        {
            ag.Override = null;
            if (ag.Passive) continue;
            ag.Host.Visible = true;
            SwellHost(ag.Host, false);
            ag.Host.Rotation = new Vector3(0, ag.Host.Rotation.Y, 0);
            var a = ag.Animal;
            if (a.TiredDawns > 0 && a.X == a.HomeX && a.Z == a.HomeZ) ag.Host.GlobalPosition = _d.World.Ground(new Vector3(a.X, 0, a.Z));
        }
        _warT.Clear(); _hurtT.Clear();
    }

    /// <summary>Her kare (_Process): savasanlarin hedefi ve vurusu, dusmanin temas vurusu, durumlar, ahirin yikilmasi.</summary>
    void WarProcess(float dt)
    {
        if (IsHost && Ranch.AnyDaySwell) foreach (var e in Ranch.DaySwellTick(dt)) ShowWar(e); // Kabartma Odasi (gece4): sure dolunca soner
        if (!Ranch.AtWar && !Ranch.AnyDaySwell) return;
        // istemci: gorunus yalniz (konum host'tan)
        foreach (var ag in _agents.Where(IsInstanceValid))
        {
            if (ag.Passive) continue;
            var a = ag.Animal;
            ag.Host.Visible = !a.Hidden;
            if (a.Fainted) ag.Host.Rotation = new Vector3(0, ag.Host.Rotation.Y, Mathf.Lerp(ag.Host.Rotation.Z, 1.45f, Mathf.Min(1f, dt * 4f)));
        }
        if (!IsHost) return;
        var combat = _d.Data.Combat;
        var wev = new List<WarEvent>();
        foreach (var ag in _agents.Where(IsInstanceValid))
        {
            var a = ag.Animal;
            if (a.War == null || a.Fainted || ag.Passive) { ag.Override = null; continue; }
            var f = Ranch.Rules.Def.FormOf(a.Species);
            if (f == null) continue;
            var me = ag.Host.GlobalPosition;
            var home = new Vector3(a.HomeX, me.Y, a.HomeZ);
            var target = Enemies().Where(z => z.GlobalPosition.DistanceTo(me) < WarDef.SightRadius && z.GlobalPosition.DistanceTo(home) < WarDef.GuardRadius)
                .OrderBy(z => z.GlobalPosition.DistanceSquaredTo(me)).FirstOrDefault();
            ag.Override = target?.GlobalPosition ?? home;
            ag.OverrideSpeed = ag.Species.Speed * 1.6f;
            // vurus
            float t = _warT.GetValueOrDefault(a.Id) - dt;
            if (target != null && t <= 0 && target.GlobalPosition.DistanceTo(me) <= f.Reach + target.Radius)
            {
                t = f.Interval;
                Strike(ag, f, target);
            }
            _warT[a.Id] = t;
            // GECICI temas kurali: dusman hayvana vurur
            float h = _hurtT.GetValueOrDefault(a.Id) - dt;
            if (h <= 0)
            {
                h = WarDef.EnemyHitInterval;
                foreach (var z in Enemies().Where(z => z.GlobalPosition.DistanceTo(me) <= WarDef.EnemyReach + z.Radius))
                {
                    var r = Ranch.Hit(a, combat, new CombatHit(z.Def.Damage, DamageType.Darbe, CombatTeam.Wild), wev);
                    if (r.Damage > 0) { Fx.Text3D(me + Vector3.Up * (ag.Species.Size + 0.8f), $"-{r.Damage:0}", Palette.Warning, 48, 0.7f, 1f); (ag.Host as WildCreature)?.Look?.HitJiggle(); }
                    if (a.Fainted) break;
                }
            }
            _hurtT[a.Id] = h;
        }
        Ranch.WarStatusTick(combat, dt, wev);
        _warScan -= dt;
        if (_warScan <= 0) { _warScan = 0.5f; wev.AddRange(Ranch.WarTick(_d.Pens.Pens)); }
        foreach (var e in wev) ShowWar(e);
    }

    /// <summary>Dusmanlar (GECICI: Zipzip; dalga sozlesmesi gelince Wave*).</summary>
    IEnumerable<Zipzip> Enemies()
    {
        var night = Zipzip.All.Where(z => IsInstanceValid(z) && !z.Dead);
        // Gunduz (Kabartma Odasi kabartmasi): cayirdaki zararli gruplari da hedeftir.
        if (_d.Meadow == null || Ranch.AtWar) return night;
        return night.Concat(_d.Meadow.Herds.SelectMany(h => h.Members).Where(z => IsInstanceValid(z) && !z.Dead));
    }

    void Strike(HusbandryAgent ag, AnimalFormDef f, Zipzip target)
    {
        float dmg = f.Damage * _d.State.Mastery.Mult("swell_animal_damage"); // Hayvancilik "Savas Cigligi"
        var me = ag.Host.GlobalPosition;
        var victims = f.Radius > 0 ? Enemies().Where(z => z.GlobalPosition.DistanceTo(target.GlobalPosition) <= f.Radius).ToList() : new List<Zipzip> { target };
        foreach (var z in victims)
        {
            var dir = z.GlobalPosition - me; dir.Y = 0;
            bool wasDead = z.Dead;
            z.Hit(dmg, false, dir.LengthSquared() > 0.001f ? dir.Normalized() : Vector3.Forward, f.Knock, false);
            if (!wasDead && z.Dead)
            {
                _d.State.Orders.Report("animal_kills");
                GD.Print($"RANCH war kill {ag.Animal.Species} id={ag.Animal.Id}");
            }
        }
        Fx.Burst(target.GlobalPosition + Vector3.Up * 0.6f, Palette.MayaTeal, 6, 2f, 0.08f, 0.4f, true, -1f);
        Sfx.Play("hit", me, -6f);
    }

    void ShowWar(WarEvent e)
    {
        var ag = AgentOf(e.AnimalId);
        var a = Ranch.ById(e.AnimalId);
        var at = (ag?.Host.GlobalPosition ?? Director.PodPos) + Vector3.Up * 1.4f;
        switch (e.Kind)
        {
            case WarEventKind.Swelled:
                if (ag != null) SwellHost(ag.Host, true);
                Fx.Text3D(at, Ranch.Rules.Def.FormOf(a?.Species ?? "")?.Name ?? Loc.T("ranch.swelled"), Palette.MayaViolet, 64, 1.2f, 1.6f);
                break;
            case WarEventKind.Hid:
                if (ag != null) ag.Host.Visible = false;
                break;
            case WarEventKind.Fainted:
                Fx.Text3D(at, Loc.T("ranch.fainted_short"), Palette.Warning, 64, 1.2f, 1.6f);
                if (ag != null) SwellHost(ag.Host, false);
                GD.Print($"RANCH fainted {a?.Species} id={e.AnimalId}");
                break;
            case WarEventKind.Died:
                Fx.Text3D(at, Loc.T("ranch.died_hard_moon"), Palette.Warning, 64, 1.2f, 1.6f);
                ag?.Host.QueueFree();
                break;
            case WarEventKind.Scattered:
                if (ag != null) { ag.Host.Visible = true; ag.Host.GlobalPosition = _d.World.Ground(ag.Host.GlobalPosition + new Vector3((float)GD.RandRange(-1.0, 1.0), 0, (float)GD.RandRange(-1.0, 1.0)).Normalized() * WarDef.ScatterRadius); }
                Fx.Text3D(at, Loc.T("ranch.fled_all"), Palette.Warning, 64, 1.2f, 1.6f);
                GD.Print($"RANCH scattered {a?.Species} id={e.AnimalId}");
                if (a != null) Events.RaiseAnimalEscaped(a.Species);
                break;
            case WarEventKind.Called:
                Fx.Text3D(at, "♪", Palette.CoopBlue, 64, 1f, 1.2f);
                break;
            case WarEventKind.SwellEnded:
                if (ag != null && !Ranch.AtWar) { SwellHost(ag.Host, false); ag.Override = null; }
                break;
        }
    }

    static void SwellHost(Node3D host, bool on)
    {
        switch (host)
        {
            case WildCreature w: w.Swell(on); break;
            case Pofuduk p: p.Swell(on); break;
            case Gidak g: g.Swell(on); break;
            default: host.Scale = Vector3.One * (on ? 1.8f : 1f); break;
        }
    }

    // ---------- Ahir Zili ve Eyerlik (PenSystem.Nearest bu yolu sorar) ----------

    /// <summary>Agil nesnesinin E eylemi ve etiketi (yoksa null): Ahir Zili, Eyerlik.</summary>
    public (Action, string) PropAction(PenProp pr)
    {
        if (pr.Def.Id == WarDef.Bell) return (() => RingRequest(pr), Loc.T("ranch.ring_bell"));
        if (pr.Def.Id == Ranch.Rules.Def.Mount.Rack)
        {
            var mdef = Ranch.Rules.Def.Mount;
            if (_d.State.Backpack.Count(mdef.Item) <= 0) return (null, null);
            var ag = _agents.Where(x => IsInstanceValid(x) && Ranch.Mount.CanSaddle(x.Animal) && x.Host.GlobalPosition.DistanceTo(pr.GlobalPosition) <= mdef.RackRadius)
                .OrderBy(x => x.Host.GlobalPosition.DistanceTo(pr.GlobalPosition)).FirstOrDefault();
            if (ag == null) return (null, null);
            return (() => SaddleRequest(pr, ag), Loc.T("ranch.saddle_on", ag.Species.Name));
        }
        return (null, null);
    }

    void RingRequest(PenProp pr)
    {
        if (_d.Net?.Forward("ranch.bell", w => { w.F32(pr.GlobalPosition.X); w.F32(pr.GlobalPosition.Z); }) == true) return;
        Ring(pr);
    }

    void Ring(PenProp pr)
    {
        var pen = _d.Pens.PenAt(pr.GlobalPosition) ?? _d.Pens.Pens.OrderBy(p => new Vector2(p.Centroid.X - pr.GlobalPosition.X, p.Centroid.Z - pr.GlobalPosition.Z).Length()).FirstOrDefault();
        var home = pen != null ? new Vector3(pen.Centroid.X, 0, pen.Centroid.Z) : pr.GlobalPosition;
        var ev = Ranch.Bell(pr.GlobalPosition.X, pr.GlobalPosition.Z, home.X, home.Z);
        pr.Bounce();
        Sfx.Play("levelup", pr.GlobalPosition, -6f);
        foreach (var e in ev) ShowWar(e);
        GD.Print($"RANCH bell called={ev.Count}");
    }

    void SaddleRequest(PenProp pr, HusbandryAgent ag)
    {
        if (_d.Net?.Forward("ranch.saddle", w => { w.F32(pr.GlobalPosition.X); w.F32(pr.GlobalPosition.Z); w.VarU((uint)ag.Animal.Id); }) == true) return;
        if (Ranch.Mount.Saddle(ag.Animal, _d.State.Backpack)) Saddled(ag.Animal);
    }

    void Saddled(PenAnimal a)
    {
        var ag = AgentOf(a.Id);
        if (ag != null) Fx.Text3D(ag.Host.GlobalPosition + Vector3.Up * (ag.Species.Size + 0.6f), Loc.T("ranch.saddled"), Palette.CoopBlue, 64, 1.2f, 1.4f);
        GD.Print($"RANCH saddled {a.Species} id={a.Id}");
    }
}
