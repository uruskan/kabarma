namespace Kabarma.Sim;

/// <summary>
/// Istasyon uretimi (GDD 11, ICERIK §11, §20): tarif malzemeyi cantadan alir, yakitli istasyon is basina yakit yakar,
/// sure dolunca urunu verir. Yuk urunu (tarif "load") cantaya girmez: Produced olayi dunyaya cikarir.
/// Kilitli tarif (UnlockOn) o esya ilk alinana kadar siraya girmez (GDD 12 Kesif). Ambardan uretim M2'de.
/// </summary>
public sealed class StationQueue
{
    readonly GameData _data;
    public string Station { get; }
    public PlaceableDef? Def { get; }
    public RecipeDef? Active { get; private set; }
    public float Progress { get; private set; }   // 0..1
    public int Queued { get; private set; }
    public event Action<RecipeDef, int>? Produced;
    /// <summary>Oyuncunun simdiye kadar eline gecen esyalar (GameState.Known). Null: hepsi acik.</summary>
    public ISet<string>? Known { get; set; }

    public StationQueue(GameData data, string station)
    {
        _data = data;
        Station = station;
        Def = data.Tool.Placeables.FirstOrDefault(p => p.Station == station);
    }

    public IEnumerable<RecipeDef> Recipes => _data.Tool.Recipes.Where(r => r.Station == Station);

    public static bool CanAfford(RecipeDef r, Inventory inv) => r.In.All(kv => inv.Count(kv.Key) >= kv.Value);

    public bool IsUnlocked(RecipeDef r) => r.UnlockOn == null || Known == null || Known.Contains(r.UnlockOn);

    /// <summary>Is basina yakilacak yakit (istasyon yakitsizsa null). Tarif yakiti girdi olarak da istiyorsa ikisi toplanir.</summary>
    public string? FuelFor(RecipeDef r, Inventory inv)
    {
        if (Def == null || Def.FuelPerJob <= 0 || Def.Fuel.Count == 0) return null;
        return Def.Fuel.FirstOrDefault(f => inv.Count(f) - r.In.GetValueOrDefault(f) >= Def.FuelPerJob) ?? Def.Fuel[0];
    }

    public bool HasFuel(RecipeDef r, Inventory inv)
    {
        var f = FuelFor(r, inv);
        return f == null || inv.Count(f) - r.In.GetValueOrDefault(f) >= Def!.FuelPerJob;
    }

    /// <summary>Malzeme, yakit ve kilit tamam mi.</summary>
    public bool CanMake(RecipeDef r, Inventory inv) => IsUnlocked(r) && CanAfford(r, inv) && HasFuel(r, inv);

    /// <summary>Malzemeyi ve yakiti hemen alir ve siraya ekler (oyuncu istasyondan uzaklassa da uretim surer).</summary>
    public bool Enqueue(RecipeDef r, Inventory inv)
    {
        if (r.Station != Station || !CanMake(r, inv)) return false;
        if (Active != null && Active.Id != r.Id) return false; // MVP: istasyon basina tek tur sira
        var fuel = FuelFor(r, inv); // yakit, girdiler alinmadan secilir (FuelFor girdiyi zaten ayirir)
        foreach (var kv in r.In) inv.Remove(kv.Key, kv.Value);
        if (fuel != null && Def != null) inv.Remove(fuel, Def.FuelPerJob);
        _fuel.Add(fuel);
        Active = r;
        Queued++;
        return true;
    }

    readonly List<string?> _fuel = new();

    /// <summary>
    /// Siradaki butun isleri iptal eder (karar 23: iptal malzemeyi geri verir): kalan adetlerin girdisi ve yakiti cantaya
    /// doner, sure sifirlanir. Donen: geri verilen (esya, adet).
    /// </summary>
    public Dictionary<string, int> Cancel(Inventory inv)
    {
        var back = new Dictionary<string, int>();
        if (Active == null || Queued == 0) return back;
        void Give(string item, int n) { int a = inv.Add(item, n); if (a > 0) back[item] = back.GetValueOrDefault(item) + a; }
        foreach (var kv in Active.In) Give(kv.Key, kv.Value * Queued);
        for (int i = _fuel.Count - Queued; i < _fuel.Count; i++) if (i >= 0 && _fuel[i] is string f && Def != null) Give(f, Def.FuelPerJob);
        _fuel.Clear();
        Active = null; Queued = 0; Progress = 0;
        return back;
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
            var r = Active!;
            if (!r.Load) inv.Add(r.Id, r.Out);
            done++;
            if (Queued == 0) { Active = null; Progress = 0; }
            Produced?.Invoke(r, r.Out);
        }
        if (Queued == 0) { Active = null; Progress = 0; _fuel.Clear(); }
        return done;
    }
}

/// <summary>
/// Yemek guclendirmesi (GDD 3.3): aclik yok; yemek suresince azami Can ve Dayaniklilik ekler, bazilari can yeniler.
/// Uc yemek yuvasi; ayni yemek iki yuvaya konmaz (yeniden yenirse suresi tazelenir).
/// </summary>
public sealed class FoodBuffs
{
    readonly List<(FoodDef food, float left)> _active = new();
    public int Slots { get; }
    public IReadOnlyList<(FoodDef food, float left)> Active => _active;

    public FoodBuffs(int slots = 3) { Slots = slots; }

    public bool Eat(FoodDef f)
    {
        int i = _active.FindIndex(a => a.food.Id == f.Id);
        if (i >= 0) { _active[i] = (f, f.Seconds); return true; }  // ayni yemek suresini yeniler, ikinci yuvaya girmez
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
    public float BonusStamina => _active.Sum(a => a.food.Stamina);
    public float Regen => _active.Sum(a => a.food.Regen);
}
