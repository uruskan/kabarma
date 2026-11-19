namespace Kabarma.Sim;

/// <summary>Bir aracin duz durumu (TEKNIK §9): konum, yon, hiz, depo, can, devrik mi, yukseklik, eklentiler.</summary>
public sealed class VehicleState
{
    public int Id { get; set; }
    public string Vehicle { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    /// <summary>Dunya yuksekligi (m). Yerdeki arac zemini izler; havadaki (dusen, ucan) arac burada tutulur.</summary>
    public float Y { get; set; }
    /// <summary>Dikey hiz (m/sn, yukari +): dusen arac (Balon yavaslatir).</summary>
    public float Vy { get; set; }
    public float Yaw { get; set; }
    public float Speed { get; set; }
    /// <summary>Depodaki yakit, gaz saniyesi olarak.</summary>
    public float Fuel { get; set; }
    public float Hp { get; set; }
    public bool Flipped { get; set; }
    /// <summary>Hava araci: zeminden yukseklik (m). Yer araclarinda 0.</summary>
    public float Altitude { get; set; }
    /// <summary>Hava araci: park halinde (Zeplin Hangari'nda).</summary>
    public bool Parked { get; set; }
    public bool Light { get; set; }
    public List<int> Riders { get; set; } = new();
    /// <summary>Takili eklentiler (vehicles.json attachments kimlikleri).</summary>
    public List<string> Attachments { get; set; } = new();
    /// <summary>Yuklenen kargo kasalari (CargoCrate kimlikleri; Kamyon, Romork, Zeplin altina asili).</summary>
    public List<int> Crates { get; set; } = new();
    /// <summary>Su Tankeri'nde kalan su birimi.</summary>
    public int Water { get; set; }
}

/// <summary>Surucunun girdisi: gaz (-1 geri, +1 ileri), direksiyon (-1 sol, +1 sag), fren; bulunulan zeminin egimi (derece); zemin turu; yukselme.</summary>
public struct VehicleInput
{
    public float Throttle;
    public float Steer;
    public bool Brake;
    public float SlopeDeg;
    /// <summary>"" kara, "water", "swamp", "snow", "sand".</summary>
    public string Surface;
    /// <summary>Hava araci: +1 yuksel, -1 alcal.</summary>
    public float Lift;
}

/// <summary>Takili eklentilerin toplam etkisi (Vehicles.ModsOf).</summary>
public readonly record struct VehicleMods(float SpeedMult, float ArmorHp, float RamDamage, int Crates, float Glide, bool HasLight, int GunnerSeats)
{
    public static VehicleMods None => new(1f, 0f, 0f, 0, 0f, false, 0);
}

/// <summary>Arac kurali (ICERIK §14, GDD §20): ivme, fren, surtunme, hiza bagli donus, yakit, zemin, yokus, dusme, ucus.</summary>
public static partial class Vehicles
{
    public static VehicleState New(VehicleDef d, int id, float x, float z, float yaw = 0f) =>
        new() { Id = id, Vehicle = d.Id, X = x, Z = z, Yaw = yaw, Hp = d.Hp };

    public static float TankSeconds(VehicleDef d) => d.TankUnits * d.UnitSeconds;

    /// <summary>Depoya en fazla bu kadar birincil yakit birimi girer.</summary>
    public static int RoomFor(VehicleDef d, VehicleState s) =>
        d.UnitSeconds <= 0 ? 0 : (int)MathF.Floor((TankSeconds(d) - s.Fuel) / d.UnitSeconds);

    /// <summary>Birim birincil yakit koyar; kac birim girdigini doner.</summary>
    public static int Refuel(VehicleDef d, VehicleState s, int units)
    {
        int n = Math.Clamp(units, 0, RoomFor(d, s));
        s.Fuel += n * d.UnitSeconds;
        return n;
    }

    /// <summary>Yakit esyasinin verim carpani (kabul etmezse 0): Bitkisel Yag 1, Biyoyakit 2, aku 4 (vehicles.json fuels).</summary>
    public static float FuelMult(VehicleDef d, string item)
    {
        if (d.Fuels.TryGetValue(item, out var m)) return m;
        return item == d.FuelItem ? 1f : 0f;
    }

    /// <summary>Verilen yakittan en cok units birim koyar (her birim UnitSeconds * carpan saniye); kac birim girdigini doner.</summary>
    public static int Refuel(VehicleDef d, VehicleState s, string item, int units)
    {
        float mult = FuelMult(d, item), per = d.UnitSeconds * mult;
        if (per <= 0 || units <= 0) return 0;
        int room = (int)MathF.Floor((TankSeconds(d) - s.Fuel) / per + 1e-4f);
        int n = Math.Clamp(units, 0, Math.Max(0, room));
        s.Fuel += n * per;
        return n;
    }

    /// <summary>Surucu (ilk binen) ve yolcu koltugu: Seats'e kadar; Arac Kulesi bir yolcuyu nisancı yapar.</summary>
    public static bool Board(VehicleDef d, VehicleState s, int who)
    {
        if (s.Flipped || s.Riders.Contains(who) || s.Riders.Count >= d.Seats) return false;
        s.Riders.Add(who);
        return true;
    }

    public static void Leave(VehicleState s, int who) => s.Riders.Remove(who);

    public static bool IsAir(VehicleDef d) => d.Kind == "air";
    public static bool IsHover(VehicleDef d) => d.Kind == "hover";

    /// <summary>Zemin turunun hiz carpani: arac turu ve eklentiden bagimsiz, vehicles.json surface.</summary>
    public static float SurfaceMult(VehicleDef d, string? surface) =>
        string.IsNullOrEmpty(surface) || IsAir(d) ? 1f : d.Surface.TryGetValue(surface, out var m) ? m : 1f;

    /// <summary>Bir adim: hiz ve konum guncellenir. Surucu yoksa ya da depo bossa gaz islemez.</summary>
    public static void Step(VehicleDef d, VehicleState s, VehicleInput i, float dt) => Step(d, s, i, dt, VehicleMods.None);

    public static void Step(VehicleDef d, VehicleState s, VehicleInput i, float dt, VehicleMods mods)
    {
        if (s.Flipped || s.Hp <= 0) { s.Speed = 0; return; }
        bool driven = s.Riders.Count > 0 && s.Fuel > 0;
        float throttle = driven ? Math.Clamp(i.Throttle, -1f, 1f) : 0f;
        bool air = IsAir(d);
        float lift = driven && air ? Math.Clamp(i.Lift, -1f, 1f) : 0f;
        if (throttle != 0 || lift != 0) s.Fuel = MathF.Max(0, s.Fuel - dt * MathF.Max(MathF.Abs(throttle), MathF.Abs(lift) * 0.6f));
        if (s.Parked && (throttle != 0 || lift > 0)) s.Parked = false;

        if (air) StepAltitude(d, s, lift, dt);

        float cap = d.MaxSpeed * mods.SpeedMult * SurfaceMult(d, i.Surface);
        if (air && s.Altitude < d.TakeoffAltitude) cap = MathF.Min(cap, d.TaxiSpeed); // yerde yalniz taksi
        float rev = d.ReverseSpeed * mods.SpeedMult * SurfaceMult(d, i.Surface);

        float v = s.Speed;
        if (i.Brake) v = Toward(v, 0, d.Brake * dt);
        else if (throttle > 0) v = v < 0 ? Toward(v, 0, d.Brake * dt) : (v > cap ? Toward(v, cap, d.Brake * dt) : MathF.Min(cap, v + d.Accel * throttle * dt));
        else if (throttle < 0) v = v > 0 ? Toward(v, 0, d.Brake * dt) : MathF.Max(-rev, v + d.Accel * throttle * dt);
        else v = Toward(v, 0, d.Coast * dt);
        if (v > cap) v = Toward(v, cap, d.Brake * dt); // zemin ya da eklenti yavaslatti
        s.Speed = v;

        float grip = Math.Clamp(MathF.Abs(v) / MathF.Max(0.1f, d.TurnAtSpeed), 0f, 1f);
        s.Yaw += Math.Clamp(i.Steer, -1f, 1f) * d.TurnRate * grip * MathF.Sign(v) * dt;
        s.X += MathF.Sin(s.Yaw) * v * dt;
        s.Z += MathF.Cos(s.Yaw) * v * dt;

        if (!air && i.SlopeDeg > d.FlipSlopeDeg && MathF.Abs(v) > d.FlipSpeed) { s.Flipped = true; s.Speed = 0; }
    }

    /// <summary>Hava araci: yukselme/alcalma; kumanda yoksa yukseklik sabit kalir (Zeplin havada asili durur).</summary>
    static void StepAltitude(VehicleDef d, VehicleState s, float lift, float dt)
    {
        if (lift != 0) s.Altitude = Math.Clamp(s.Altitude + lift * d.ClimbRate * dt, 0f, d.MaxAltitude);
        else if (s.Riders.Count == 0 && s.Altitude > 0) s.Altitude = MathF.Max(0f, s.Altitude - dt * d.ClimbRate * 0.25f); // surucusuz yavasca iner
    }

    /// <summary>
    /// Dikey: yer araci yokusu izler; ucurumdan atinca duser (Balon dusme hizini yavaslatir, indirince hasar yok).
    /// Hava araci zeminden Altitude kadar yukarida asili. groundY = o noktadaki zemin. Inis hizi esigi asinca hasar.
    /// </summary>
    public static void Settle(VehicleDef d, VehicleState s, float groundY, float dt, VehicleMods mods, VehicleRulesDef rules)
    {
        if (IsAir(d)) { s.Y = groundY + s.Altitude; s.Vy = 0; return; }
        if (s.Y > groundY + 0.35f)
        {
            s.Vy -= 9.81f * dt;
            if (mods.Glide > 0) s.Vy = MathF.Max(s.Vy, -mods.Glide);
            s.Y += s.Vy * dt;
            if (s.Y <= groundY)
            {
                float impact = -s.Vy;
                if (impact > rules.FallDamageSpeed) s.Hp = MathF.Max(0f, s.Hp - (impact - rules.FallDamageSpeed) * rules.DamagePerSpeed);
                s.Y = groundY; s.Vy = 0;
            }
            return;
        }
        s.Y = groundY; s.Vy = 0;
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
