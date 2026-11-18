namespace Kabarma.Sim;

/// <summary>
/// Binek (GDD 9.5, 9.4): eyer takma, binme, inme (Baglama Diregi'ne baglama), hiz ve yol sayaci. Binen aktor kimlikle
/// tutulur (ortak oyun: es kimligi). Sayilar husbandry.json "mount". Godot katmani Mount*.cs.
/// </summary>
public sealed class MountRules
{
    readonly Ranch _r;
    float _carry, _xpCarry;

    public MountRules(Ranch r) { _r = r; }

    MountDef Def => _r.Rules.Def.Mount;
    bool Adult(PenAnimal a) => a.Adult(_r.Rules.Def.Breeding.AdultDays);

    /// <summary>Tur binilebilir mi (binek hizi yazili ve Binek rolu alabiliyor).</summary>
    public bool Rideable(string species) => Def.Speeds.ContainsKey(species) && _r.Rules.Species(species).Roles.Contains("binek");

    public bool CanSaddle(PenAnimal a) => a.Tame && Adult(a) && !a.Saddled && a.Role == "binek" && Rideable(a.Species);

    /// <summary>Eyerlik'te: cantadaki eyeri hayvana takar.</summary>
    public bool Saddle(PenAnimal a, Inventory bag)
    {
        if (!CanSaddle(a) || !bag.Remove(Def.Item, 1)) return false;
        a.Saddled = true;
        return true;
    }

    /// <summary>Binilebilir mi: eyerli, Binek, evcil yetiskin, yorgun degil, bos; kabarikken yalniz formu binilirse.</summary>
    public bool CanRide(PenAnimal a)
    {
        if (!a.Tame || !Adult(a) || !a.Saddled || a.Role != "binek" || a.Rider != 0 || _r.Exhausted(a) || a.Bound) return false;
        if (a.War != null && _r.Rules.Def.FormOf(a.Species) is not { Rideable: true }) return false;
        return Rideable(a.Species);
    }

    /// <summary>Biner: hayvan agil kaydindan ve diregden cikar (binek oyuncuyla gezer).</summary>
    public bool Mount(PenAnimal a, int rider)
    {
        if (rider == 0 || !CanRide(a) || _r.Animals.Any(x => x.Rider == rider)) return false;
        a.Rider = rider;
        a.Registered = false;
        a.Hidden = false;
        a.Called = false;
        Untie(a);
        return true;
    }

    /// <summary>Iner (x, z): yakinda bos Baglama Diregi varsa hayvan ona baglanir. Bagli direk doner.</summary>
    public TiePost? Dismount(PenAnimal a, float x, float z)
    {
        if (a.Rider == 0) return null;
        a.Rider = 0;
        a.X = x; a.Z = z;
        float r = _r.Rules.Def.Combat.PostRadius;
        var post = _r.Posts.Where(p => p.AnimalId == 0 && (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z) <= r * r)
            .OrderBy(p => (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z)).FirstOrDefault();
        if (post != null) { post.AnimalId = a.Id; a.PostId = post.Id; a.Command = "bekle"; }
        else a.Command = "izle";
        return post;
    }

    void Untie(PenAnimal a)
    {
        if (a.PostId == 0) return;
        var p = _r.Posts.FirstOrDefault(x => x.Id == a.PostId);
        if (p != null) p.AnimalId = 0;
        a.PostId = 0;
    }

    /// <summary>Binek hizi: turun binek hizi x B geni (BSpeedPerPoint) x (1 + Ustalik mount_speed).</summary>
    public float Speed(PenAnimal a, float masteryBonus = 0f)
    {
        float baseSpeed = Def.Speeds.GetValueOrDefault(a.Species);
        float gene = 1f + (a.Genes.B - 1) * _r.Rules.Def.Genes.BSpeedPerPoint;
        return baseSpeed * gene * (1f + masteryBonus);
    }

    /// <summary>
    /// Binilen yol: tam metreler (mount_m olcusu) ve Surus XP'si dilimleri (her xpMeters) doner; kesir birikir.
    /// </summary>
    public (int meters, int xpChunks) Travel(float meters)
    {
        if (meters <= 0) return (0, 0);
        _carry += meters;
        _xpCarry += meters;
        int whole = (int)_carry;
        _carry -= whole;
        int chunks = 0;
        if (Def.XpMeters > 0) { chunks = (int)(_xpCarry / Def.XpMeters); _xpCarry -= chunks * Def.XpMeters; }
        return (whole, chunks);
    }
}
