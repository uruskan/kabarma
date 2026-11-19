using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Gunduz surusu: uyeler, merkez, kacis ve gelinen ekin. Duz durum (kayda hazir); dugumlere uyeler uzerinden baglanir.</summary>
public sealed class ZipzipHerd
{
    public readonly List<Zipzip> Members = new();
    public Vector3 Center, WanderTo;
    public float WanderT, FleeT, SenseT, LeaveT;
    public Vector3 FleeFrom;
    public PlotView Crop;
    public bool Leaving;
}

/// <summary>
/// Cayirdaki Zipzip suruleri (URETIM §6 M1, GDD §8.1): gunduz Ocak'tan spawnMin-spawnMax uzakta 2-3'luk gruplar.
/// Oyuncu fleeRadius'a girince suru kacar; cropSense icinde olgun ekin varsa yanina gelir. Aksamla birlikte cayirdan
/// cekilirler (Kabarma gecesi yaratiklar dalga olarak gelir, GDD §14.2): gece Zipzip.All yalniz dalgadir.
/// Safaktan respawnSeconds sonra yeni suruler gelir. Butun durum bu dugumde (statik yok, TEKNIK §9).
/// </summary>
public partial class ZipzipMeadow : Node
{
    readonly Director _d;
    readonly CreatureDef _def;
    readonly DayHerdDef _day;
    readonly Rng _rng;
    public readonly List<ZipzipHerd> Herds = new();
    float _respawnT;
    public int Spawned { get; private set; }

    public ZipzipMeadow(Director d)
    {
        _d = d;
        _def = d.Data.Creature("zipzip");
        _day = _def.Day;
        _rng = new Rng(GD.Randi() | 1UL);
    }

    /// <summary>
    /// Ogretici kurali (GDD 24, yalniz ilk gun): ilk dokuz salgam ilk Kabarma'ya ve ilk hasada kadar yasar; gunduz suruleri o zamana kadar
    /// ekine gelmez (ilk 10 dakika boyunca ekin yenirse oyuncu Kabarma ve Hasat adimlarini hic goremez). Hasat adimi bitince normal (GDD 8.2).
    /// </summary>
    bool TutorialShield
    {
        get
        {
            var sc = _d.State.Scenario;
            if (sc == null || sc.Finished) return false;
            int last = sc.Steps.ToList().FindIndex(s => s.Id == "hasat");
            if (last < 0) last = sc.Steps.ToList().FindIndex(s => s.Id == "night");
            return last >= 0 && sc.StepIndex <= last;
        }
    }

    bool Night =>_d.Phase is Director.Act.Dusk or Director.Act.Night;

    public override void _Process(double delta)
    {
        if (_day == null || _day.Groups <= 0) return;
        float dt = (float)delta;
        bool out_ = Kabarma.Sim.Herds.Out(Night);
        foreach (var h in Herds) h.Members.RemoveAll(z => !GodotObject.IsInstanceValid(z) || z.Dead);
        Herds.RemoveAll(h => h.Members.Count == 0);

        if (!out_)
        {
            foreach (var h in Herds.Where(h => !h.Leaving)) Leave(h);
            _respawnT = _day.RespawnSeconds;
        }
        else if (Herds.Count(h => !h.Leaving) < _day.Groups)
        {
            _respawnT -= dt;
            if (_respawnT <= 0)
            {
                var sizes = Kabarma.Sim.Herds.GroupSizes(_day, _rng);
                int missing = _day.Groups - Herds.Count(h => !h.Leaving);
                foreach (var n in sizes.Take(missing)) Spawn(n);
                _respawnT = _day.RespawnSeconds;
            }
        }
        foreach (var h in Herds) TickHerd(h, dt);
    }

    void Spawn(int size)
    {
        var home = _d.OcakPos;
        var player = _d.Player.GlobalPosition;
        Vector3 c = home;
        for (int tries = 0; tries < 8; tries++)
        {
            float a = _rng.Range(0f, Mathf.Tau);
            float r = _rng.Range(_day.SpawnMin, _day.SpawnMax);
            c = _d.World.Ground(home + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            if (c.DistanceTo(player) > _day.FleeRadius * 2.5f) break;
        }
        SpawnAt(size, c);
    }

    /// <summary>c merkezli size uyeli bir suru kurar (Spawn ve gorunus sinamasi --creatureview=crop).</summary>
    public ZipzipHerd SpawnAt(int size, Vector3 c)
    {
        var h = new ZipzipHerd { Center = c, WanderTo = c, WanderT = _rng.Range(2f, 5f) };
        for (int i = 0; i < size; i++)
        {
            float a = Mathf.Tau * i / size + _rng.Range(-0.4f, 0.4f);
            var slot = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * _rng.Range(0.8f, 1.6f);
            var z = new Zipzip(_def, _d, 1f, 1f, swollen: false) { Name = $"ZipzipDay{Spawned}" };
            z.InitDay(h, slot);
            _d.AddChild(z);
            z.GlobalPosition = _d.World.Ground(c + slot);
            z.RotationDegrees = Vector3.Zero;
            h.Members.Add(z);
            Spawned++;
        }
        Herds.Add(h);
        GD.Print($"ZIPZIP herd size={size} at={c.X:0},{c.Z:0} herds={Herds.Count}");
        return h;
    }

    void Leave(ZipzipHerd h)
    {
        h.Leaving = true;
        h.LeaveT = _day.LeaveSeconds;
        h.Crop = null;
        // Gece dalgasi sayaci (Director.Night) yalniz dalgayi saysin: cekilen suru hemen listeden cikar.
        foreach (var z in h.Members) Zipzip.All.Remove(z);
    }

    void TickHerd(ZipzipHerd h, float dt)
    {
        if (h.Leaving)
        {
            h.LeaveT -= dt;
            if (h.LeaveT <= 0) { foreach (var z in h.Members) z.QueueFree(); h.Members.Clear(); }
            return;
        }
        var player = _d.Player;
        h.FleeT -= dt;
        if (!player.Down && h.Members.Any(z => Kabarma.Sim.Herds.ShouldFlee(_day, z.GlobalPosition.DistanceTo(player.GlobalPosition))))
        {
            h.FleeT = _day.FleeSeconds;
            h.FleeFrom = player.GlobalPosition;
        }
        if (h.FleeT > 0)
        {
            // Kacarken merkez suruyle gider; kacis bitince oldugu yerde toplanir.
            var c = Vector3.Zero; foreach (var z in h.Members) c += z.GlobalPosition;
            h.Center = h.WanderTo = c / h.Members.Count;
            h.Crop = null;
            return;
        }
        // Olgun ekini sezer (senseSeconds'ta bir).
        h.SenseT -= dt;
        if (h.SenseT <= 0)
        {
            h.SenseT = _day.SenseSeconds;
            var was = h.Crop;
            h.Crop = TutorialShield ? null : _d.FarmView.Views.Where(v => Kabarma.Sim.Herds.Attracts(v.Plot) && v.GlobalPosition.DistanceTo(h.Center) < _day.CropSense)
                .OrderBy(v => v.GlobalPosition.DistanceSquaredTo(h.Center)).FirstOrDefault();
            if (h.Crop != null && h.Crop != was)
                GD.Print($"ZIPZIP herd crop plot={h.Crop.Plot.X},{h.Crop.Plot.Y} dist={h.Crop.GlobalPosition.DistanceTo(h.Center):0.0}");
        }
        if (h.Crop != null) { h.Center = h.Center.MoveToward(h.Crop.GlobalPosition, _def.Speed * _day.WalkSpeedMult * dt); return; }
        // Dolasma: merkez yavasca yeni bir noktaya kayar; Ocak cevresindeki halkada kalir.
        h.WanderT -= dt;
        if (h.WanderT <= 0)
        {
            h.WanderT = _rng.Range(_day.RetargetMin, _day.RetargetMax);
            var to = h.Center + new Vector3(_rng.Range(-1f, 1f), 0, _rng.Range(-1f, 1f)) * _day.WanderRadius * 2f;
            var fromHome = to - _d.OcakPos; fromHome.Y = 0;
            float r = Mathf.Clamp(fromHome.Length(), _day.SpawnMin * 0.6f, _day.SpawnMax * 1.2f);
            h.WanderTo = _d.World.Ground(_d.OcakPos + (fromHome.LengthSquared() > 0.01f ? fromHome.Normalized() : Vector3.Forward) * r);
        }
        h.Center = _d.World.Ground(h.Center.MoveToward(h.WanderTo, _def.Speed * _day.WalkSpeedMult * 0.8f * dt));
    }
}
