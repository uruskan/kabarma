namespace Kabarma.Sim;

/// <summary>Kabartma Odasi sonucu (GDD 14.10).</summary>
public enum SwellRoomResult { Ok, NoPower, KabarmaActive, NoMaya, NoTarget }

/// <summary>
/// Kabartma Odasi (T6, 800 W; GDD 14.10): gunduz tek bir olgun tarla karesini ya da evcil hayvani kabartir; calisma basina
/// 50 Maya (cantadaki "maya" esyasi). Kural burada, Godot yalnizca hedefi secer ve gorunumu uygular. Bedel yalniz basari
/// halinde odenir.
/// </summary>
public static class SwellRoom
{
    static bool KabarmaActive(GameState s) => s.Night != null && s.Night.Phase != NightPhase.Dawn;

    /// <summary>Istasyonun (x, z) yaricapindaki en yakin olgun, kabarmamis tarla karesi; yoksa null (hucre merkezi: x + 0,5).</summary>
    public static Plot? NearestMature(GameState s, float x, float z)
    {
        float r = s.Data.Tuzuk.SwellRoom.Radius;
        Plot? best = null; float bd = float.MaxValue;
        foreach (var p in s.Farm.Plots)
        {
            if (!p.Planted || p.Stage != CropStage.Mature || p.Swollen != null) continue;
            float dx = p.X + 0.5f - x, dz = p.Y + 0.5f - z, d = MathF.Sqrt(dx * dx + dz * dz);
            if (d <= r && d < bd) { best = p; bd = d; }
        }
        return best;
    }

    /// <summary>Genel ortak giris sirasi: guc, Kabarma, Maya. Hedef ayri denetlenir.</summary>
    static SwellRoomResult Gate(GameState s, bool powered, IItemStore bag)
    {
        if (!powered) return SwellRoomResult.NoPower;
        if (KabarmaActive(s)) return SwellRoomResult.KabarmaActive;
        if (bag.Count(MayaVault.Item) < s.Data.Tuzuk.SwellRoom.MayaCost) return SwellRoomResult.NoMaya;
        return SwellRoomResult.Ok;
    }

    /// <summary>Tarla karesi kabartir. KS: acilmis en yuksek KS.</summary>
    public static SwellRoomResult UsePlot(GameState s, IItemStore bag, bool powered, Plot target)
    {
        var g = Gate(s, powered, bag);
        if (g != SwellRoomResult.Ok) return g;
        if (!s.Farm.SwellOne(target, Math.Max(1, s.UnlockedKs), 0f)) return SwellRoomResult.NoTarget;
        bag.Remove(MayaVault.Item, s.Data.Tuzuk.SwellRoom.MayaCost);
        return SwellRoomResult.Ok;
    }

    /// <summary>Evcil hayvan icin bedeli oder; kabarik kalma suresini (sn) Godot hayvana uygular (savas hayvani kurallari savas-hayvan akisinda).</summary>
    public static SwellRoomResult UseAnimal(GameState s, IItemStore bag, bool powered, bool hasAnimal)
    {
        var g = Gate(s, powered, bag);
        if (g != SwellRoomResult.Ok) return g;
        if (!hasAnimal) return SwellRoomResult.NoTarget;
        bag.Remove(MayaVault.Item, s.Data.Tuzuk.SwellRoom.MayaCost);
        return SwellRoomResult.Ok;
    }
}