using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Zipzip'in gunduz hali (URETIM §6 M1 "Zipzip": gunduz cayirda 2-3'luk urkek gruplar; olgun ekine gelir, GDD §8.1).
/// Normal (kabarmamis) gorunur. Suru kararlarini ZipzipMeadow verir; burada her uyenin adimi ve klibi:
/// suruyle dolasma (yurume), otlanma ve dinlenme (yeme, dinlenme klipleri), olgun ekinin yanina gelme,
/// oyuncu yaklasinca ya da vurulunca kacis (kosu). Ekini yemesi veride (day.eats).
/// </summary>
public partial class Zipzip
{
    /// <summary>Bagli oldugu suru (gunduz).</summary>
    public ZipzipHerd Herd;
    Vector3 _daySlot;
    float _dayPauseT, _stepAcc;
    string _dayIdle = "idle";

    /// <summary>Surunun bir uyesi olarak cayira koyar.</summary>
    public void InitDay(ZipzipHerd herd, Vector3 slot)
    {
        Mode = ZipzipMode.Day;
        Herd = herd;
        _daySlot = slot;
        _spawnT = 1f; // yerden fiskirmaz; cayirda zaten vardir
        _dayPauseT = (float)GD.RandRange(0.0, 3.0);
    }

    /// <summary>Vuruldu: butun suru kacar.</summary>
    void DayStartled()
    {
        if (Herd == null) return;
        Herd.FleeT = Def.Day?.FleeSeconds ?? 2f;
        Herd.FleeFrom = _dir.Player.GlobalPosition;
    }

    void TickDay(float dt, float baseScale, float slow)
    {
        var d = Def.Day;
        var h = Herd;
        var pos = GlobalPosition;
        float walk = Def.Speed * (d?.WalkSpeedMult ?? 0.3f) * slow;
        float flee = Def.Speed * (d?.FleeSpeedMult ?? 1f) * slow;
        Vector3 goal = pos;
        float speed = walk;
        string still = _dayIdle;

        if (h == null) { }
        else if (h.Leaving)
        {
            var away = pos - _dir.OcakPos; away.Y = 0;
            goal = pos + (away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Forward) * 8f;
            speed = flee;
        }
        else if (h.FleeT > 0)
        {
            var away = pos - h.FleeFrom; away.Y = 0;
            goal = pos + (away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Forward) * 6f;
            speed = flee;
        }
        else if (h.Crop != null && Herds.Attracts(h.Crop.Plot))
        {
            // Olgun ekine gelir: cevresinde durur ve koklar (yeme klibi); veride izin varsa yer.
            var c = h.Crop.GlobalPosition;
            var off = _daySlot with { Y = 0 };
            goal = c + (off.LengthSquared() > 0.01f ? off.Normalized() : Vector3.Back) * (d?.CropStop ?? 1.2f);
            still = "eat";
            if (d != null && d.Eats && new Vector2(goal.X - pos.X, goal.Z - pos.Z).Length() < 0.4f)
            {
                _eatT += dt * slow;
                if (_eatT >= Def.EatSeconds) { _dir.State.Farm.Eat(h.Crop.Plot.X, h.Crop.Plot.Y); _eatT = 0; }
            }
        }
        else
        {
            goal = h.Center + _daySlot;
            _dayPauseT -= dt;
            if (_dayPauseT <= 0)
            {
                // Dururken: bekleme, otlanma ya da dinlenme (8 klibin gunduzlukleri).
                _dayPauseT = (float)GD.RandRange(2.5, 6.0);
                float r = GD.Randf();
                _dayIdle = r < 0.45f ? "idle" : r < 0.8f ? "eat" : "rest";
            }
            still = _dayIdle;
        }

        var flat = goal - pos; flat.Y = 0;
        string slot = still;
        if (flat.Length() > 0.3f)
        {
            var step = flat.Normalized() * Mathf.Min(flat.Length(), speed * dt);
            GlobalPosition = _dir.World.Ground(pos + step);
            float yaw = Mathf.Atan2(flat.X, flat.Z);
            _body.Rotation = new Vector3(0, Mathf.LerpAngle(_body.Rotation.Y, yaw, Mathf.Min(1f, dt * 8f)), 0);
            slot = speed > walk * 1.5f ? "run" : "walk";
            _stepAcc += step.Length();
            if (_stepAcc > 0.3f) { _stepAcc = 0; _sw?.StepJiggle(); }
        }
        if (_knock.LengthSquared() > 0.001f)
        {
            GlobalPosition = _dir.World.Ground(GlobalPosition + _knock * dt);
            _knock = _knock.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 6f));
        }
        TickLook(dt, baseScale, 0f);
        if (_rig != null && _hurtT <= 0)
            _rig.Play(slot, 0.2f, (FreezeT > 0 ? 0.6f : 1f) * (slot == "walk" ? Mathf.Max(0.6f, speed / Mathf.Max(0.01f, walk)) : 1f));
    }
}
