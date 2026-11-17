namespace Kabarma.Sim;

/// <summary>Istasyon uretimi (GDD 11): tarif malzemeyi cantadan alir, sure dolunca urunu verir. Ambardan uretim M2'de.</summary>
public sealed class StationQueue
{
    readonly GameData _data;
    public string Station { get; }
    public RecipeDef? Active { get; private set; }
    public float Progress { get; private set; }   // 0..1
    public int Queued { get; private set; }
    public event Action<RecipeDef, int>? Produced;

    public StationQueue(GameData data, string station) { _data = data; Station = station; }

    public IEnumerable<RecipeDef> Recipes => _data.Tool.Recipes.Where(r => r.Station == Station);

    public static bool CanAfford(RecipeDef r, Inventory inv) => r.In.All(kv => inv.Count(kv.Key) >= kv.Value);

    /// <summary>Malzemeyi hemen alir ve siraya ekler (oyuncu istasyondan uzaklassa da uretim surer).</summary>
    public bool Enqueue(RecipeDef r, Inventory inv)
    {
        if (r.Station != Station || !CanAfford(r, inv)) return false;
        if (Active != null && Active.Id != r.Id) return false; // MVP: istasyon basina tek tur sira
        foreach (var kv in r.In) inv.Remove(kv.Key, kv.Value);
        Active = r;
        Queued++;
        return true;
    }

    /// <returns>Bu tikte biten adet.</returns>
    public int Tick(float dt, Inventory inv)
    {
        if (Active == null || Queued == 0) return 0;
        Progress += dt / Math.Max(0.01f, Active.Seconds);
        int done = 0;
        while (Progress >= 1f && Queued > 0)
        {
            Progress -= 1f;
            Queued--;
            inv.Add(Active.Id, Active.Out);
            done++;
            Produced?.Invoke(Active, Active.Out);
        }
        if (Queued == 0) { Active = null; Progress = 0; }
        return done;
    }
}

/// <summary>Yemek guclendirmesi (GDD 3.3): aclik yok, yemek gecici can ve yenilenme verir. Uc yemek yuvasi.</summary>
public sealed class FoodBuffs
{
    public const int Slots = 3;
    readonly List<(FoodDef food, float left)> _active = new();
    public IReadOnlyList<(FoodDef food, float left)> Active => _active;

    public bool Eat(FoodDef f)
    {
        int i = _active.FindIndex(a => a.food.Id == f.Id);
        if (i >= 0) { _active[i] = (f, f.Seconds); return true; }  // ayni yemek suresini yeniler
        if (_active.Count >= Slots) return false;
        _active.Add((f, f.Seconds));
        return true;
    }

    public void Tick(float dt)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var (f, left) = _active[i];
            left -= dt;
            if (left <= 0) _active.RemoveAt(i); else _active[i] = (f, left);
        }
    }

    public float BonusMaxHp => _active.Sum(a => a.food.MaxHp);
    public float Regen => _active.Sum(a => a.food.Regen);
}
