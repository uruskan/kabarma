namespace Kabarma.Sim;

/// <summary>Bir aracin duz durumu (TEKNIK §9): konum, yon, hiz, depo, can, devrik mi.</summary>
public sealed class VehicleState
{
    public int Id { get; set; }
    public string Vehicle { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float Speed { get; set; }
    /// <summary>Depodaki yakit, gaz saniyesi olarak.</summary>
    public float Fuel { get; set; }
    public float Hp { get; set; }
    public bool Flipped { get; set; }
    public List<int> Riders { get; set; } = new();
}

/// <summary>Surucunun girdisi: gaz (-1 geri, +1 ileri), direksiyon (-1 sol, +1 sag), fren; bulunulan zeminin egimi (derece).</summary>
public struct VehicleInput
{
    public float Throttle;
    public float Steer;
    public bool Brake;
    public float SlopeDeg;
}

/// <summary>Arac kurali (ICERIK §14, GDD §20): ivme, fren, surtunme, hiza bagli donus, yakit, dik yokusta devrilme.</summary>
public static class Vehicles
{
    public static VehicleState New(VehicleDef d, int id, float x, float z, float yaw = 0f) =>
        new() { Id = id, Vehicle = d.Id, X = x, Z = z, Yaw = yaw, Hp = d.Hp };

    public static float TankSeconds(VehicleDef d) => d.TankUnits * d.UnitSeconds;

    /// <summary>Depoya en fazla bu kadar yakit birimi girer.</summary>
    public static int RoomFor(VehicleDef d, VehicleState s) =>
        d.UnitSeconds <= 0 ? 0 : (int)MathF.Floor((TankSeconds(d) - s.Fuel) / d.UnitSeconds);

    /// <summary>Birim yakit koyar; kac birim girdigini doner.</summary>
    public static int Refuel(VehicleDef d, VehicleState s, int units)
    {
        int n = Math.Clamp(units, 0, RoomFor(d, s));
        s.Fuel += n * d.UnitSeconds;
        return n;
    }

    public static bool Board(VehicleDef d, VehicleState s, int who)
    {
        if (s.Flipped || s.Riders.Contains(who) || s.Riders.Count >= d.Seats) return false;
        s.Riders.Add(who);
        return true;
    }

    public static void Leave(VehicleState s, int who) => s.Riders.Remove(who);

    /// <summary>Bir adim: hiz ve konum guncellenir. Surucu yoksa ya da depo bossa gaz islemez.</summary>
    public static void Step(VehicleDef d, VehicleState s, VehicleInput i, float dt)
    {
        if (s.Flipped || s.Hp <= 0) { s.Speed = 0; return; }
        float throttle = s.Riders.Count > 0 && s.Fuel > 0 ? Math.Clamp(i.Throttle, -1f, 1f) : 0f;
        if (throttle != 0) s.Fuel = MathF.Max(0, s.Fuel - dt * MathF.Abs(throttle));

        float v = s.Speed;
        if (i.Brake) v = Toward(v, 0, d.Brake * dt);
        else if (throttle > 0) v = v < 0 ? Toward(v, 0, d.Brake * dt) : MathF.Min(d.MaxSpeed, v + d.Accel * throttle * dt);
        else if (throttle < 0) v = v > 0 ? Toward(v, 0, d.Brake * dt) : MathF.Max(-d.ReverseSpeed, v + d.Accel * throttle * dt);
        else v = Toward(v, 0, d.Coast * dt);
        s.Speed = v;

        float grip = Math.Clamp(MathF.Abs(v) / MathF.Max(0.1f, d.TurnAtSpeed), 0f, 1f);
        s.Yaw += Math.Clamp(i.Steer, -1f, 1f) * d.TurnRate * grip * MathF.Sign(v) * dt;
        s.X += MathF.Sin(s.Yaw) * v * dt;
        s.Z += MathF.Cos(s.Yaw) * v * dt;

        if (i.SlopeDeg > d.FlipSlopeDeg && MathF.Abs(v) > d.FlipSpeed) { s.Flipped = true; s.Speed = 0; }
    }

    /// <summary>Devrilen araci yeniden dogrultur (oyuncu itince).</summary>
    public static void Right(VehicleState s) { s.Flipped = false; s.Speed = 0; }

    static float Toward(float v, float to, float step) => v > to ? MathF.Max(to, v - step) : MathF.Min(to, v + step);
}

/// <summary>Kayit bolumu "vehicles" (Godot tarafi, SaveOrder.Spawn).</summary>
public sealed class VehiclesSave
{
    public int NextId { get; set; } = 1;
    public List<VehicleState> Vehicles { get; set; } = new();
}
