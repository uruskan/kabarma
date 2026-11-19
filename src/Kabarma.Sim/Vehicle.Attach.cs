namespace Kabarma.Sim;

/// <summary>Eklenti kurallari (ICERIK §14): yuva, uyum, etki; arac uretimi ve Zeplin Hangari; kasa yukleme. Durumsuz, Sim'de.</summary>
public static partial class Vehicles
{
    public static VehicleMods ModsOf(VehiclesFile f, VehicleState s)
    {
        float speed = 1f, armor = 0f, ram = 0f, glide = 0f;
        int crates = 0, gunners = 0; bool light = false;
        foreach (var id in s.Attachments)
        {
            var a = f.Attachments.FirstOrDefault(x => x.Id == id);
            if (a == null) continue;
            var p = a.Params;
            if (p.TryGetValue("speedMult", out var sm)) speed *= sm;
            if (p.TryGetValue("armorHp", out var ah)) armor += ah;
            if (p.TryGetValue("ramDamage", out var rd)) ram = MathF.Max(ram, rd);
            if (p.TryGetValue("crates", out var c)) crates += (int)c;
            if (p.TryGetValue("glide", out var g)) glide = MathF.Max(glide, g);
            if (p.TryGetValue("gunnerSeat", out var gs)) gunners += (int)gs;
            if (p.TryGetValue("light", out var l) && l > 0) light = true;
        }
        return new VehicleMods(speed, armor, ram, crates, glide, light, gunners);
    }

    public static bool Fits(AttachmentDef a, VehicleDef d) => a.Fits.Contains("*") || a.Fits.Contains(d.Id);

    public static int FreeMounts(VehicleDef d, VehicleState s, VehiclesFile f, string mount)
    {
        int used = s.Attachments.Count(id => f.Attachments.FirstOrDefault(x => x.Id == id)?.Mount == mount);
        return (mount == "hitch" ? d.Hitches : d.BodyMounts) - used;
    }

    /// <summary>Eklentiyi takar: arac uyar, yuva bos, ayni eklenti yok. Nedeni dondurur ("" = takildi).</summary>
    public static string Attach(VehicleDef d, VehicleState s, VehiclesFile f, string attachmentId)
    {
        var a = f.Attachments.FirstOrDefault(x => x.Id == attachmentId);
        if (a == null) return "bilinmeyen eklenti";
        if (!Fits(a, d)) return $"{a.Name} {d.Name}'e uymaz";
        if (s.Attachments.Contains(attachmentId)) return "zaten takılı";
        if (FreeMounts(d, s, f, a.Mount) <= 0) return a.Mount == "hitch" ? "çeki demiri dolu" : "gövde yuvası dolu";
        s.Attachments.Add(attachmentId);
        if (a.Params.TryGetValue("waterUnits", out var w)) s.Water = (int)w; // Su Tankeri dolu gelir
        return "";
    }

    /// <summary>Eklentiyi cikarir; takili degilse false.</summary>
    public static bool Detach(VehicleState s, string attachmentId)
    {
        if (!s.Attachments.Remove(attachmentId)) return false;
        if (!s.Attachments.Any(x => x == "su_tankeri")) s.Water = 0;
        return true;
    }

    /// <summary>Kasa kapasitesi: arac + Romork (+4). Kamyon 6, Hava Yastikli 2, Zeplin altina 3.</summary>
    public static int CrateCapacity(VehicleDef d, VehicleState s, VehiclesFile f) => d.CargoCrates + ModsOf(f, s).Crates;

    public static bool LoadCrate(VehicleDef d, VehicleState s, VehiclesFile f, int crateId)
    {
        if (s.Crates.Contains(crateId) || s.Crates.Count >= CrateCapacity(d, s, f)) return false;
        s.Crates.Add(crateId);
        return true;
    }

    public static bool UnloadCrate(VehicleState s, int crateId) => s.Crates.Remove(crateId);

    /// <summary>Tampon hizli giderken carptigi yaratiga verdigi hasar (hiz esigi RamMinSpeed); yoksa 0.</summary>
    public static float RamDamage(VehicleState s, VehicleMods m, VehicleRulesDef rules) =>
        m.RamDamage > 0 && MathF.Abs(s.Speed) >= rules.RamMinSpeed ? m.RamDamage * MathF.Abs(s.Speed) / rules.RamMinSpeed * 0.5f : 0f;

    /// <summary>Pulluk, Ekim Makinesi ve Su Tankeri'nin bu adimda isledigi hucreler (araci ortalayan, width genisliginde serit). Hucre = 1 m.</summary>
    public static IEnumerable<(int x, int y)> WorkCells(VehicleState s, AttachmentDef a, float behind = 2.4f)
    {
        float width = a.Params.TryGetValue("width", out var w) ? w : 1f;
        float fx = MathF.Sin(s.Yaw), fz = MathF.Cos(s.Yaw);
        float cx = s.X - fx * behind, cz = s.Z - fz * behind;
        float px = fz, pz = -fx; // sagina dik
        int n = Math.Max(1, (int)MathF.Round(width));
        for (int k = 0; k < n; k++)
        {
            float off = (k - (n - 1) / 2f);
            yield return ((int)MathF.Floor(cx + px * off), (int)MathF.Floor(cz + pz * off));
        }
    }

    /// <summary>Biçerdover: oniindeki yaricap icinde (m) mi.</summary>
    public static bool InHarvestCone(VehicleDef d, VehicleState s, float x, float z)
    {
        if (d.HarvestRadius <= 0) return false;
        float fx = MathF.Sin(s.Yaw), fz = MathF.Cos(s.Yaw);
        float cx = s.X + fx * d.HarvestRadius * 0.6f, cz = s.Z + fz * d.HarvestRadius * 0.6f;
        return (x - cx) * (x - cx) + (z - cz) * (z - cz) <= d.HarvestRadius * d.HarvestRadius;
    }

    /// <summary>Zeplin Hangari'nin park yaricapinda (m) mi; iniste park sayilir.</summary>
    public static bool InHangar(VehicleState s, float hx, float hz, VehicleRulesDef rules) =>
        (s.X - hx) * (s.X - hx) + (s.Z - hz) * (s.Z - hz) <= rules.HangarParkRadius * rules.HangarParkRadius;

    /// <summary>Uretim: arac tarifinin istasyonu; recipes.json'daki arac satirlarinin kimligi arac kimligidir (cekirge dahil).</summary>
    public static bool IsProduct(VehiclesFile f, string recipeId) => f.Vehicles.Any(v => v.Id == recipeId);
}
