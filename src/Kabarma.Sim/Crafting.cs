namespace Kabarma.Sim;

/// <summary>
/// Istasyon uretimi (GDD 5, 11; ICERIK §11, §20): tarif malzemeyi depodan (canta ya da canta + Sandiklar, GDD §5 ambardan
/// uretim) alir, yakitli istasyon is basina yakit yakar, sure dolunca urunu ayni depoya koyar (yer yoksa is bekler).
/// Yuk urunu (tarif "load") cantaya girmez: Produced olayi dunyaya cikarir. Istasyonun kendi kuyrugu vardir: farkli
/// tarifler sirayla islenir (en cok research.json stationQueueMax is). Kilitli tarif (UnlockOn) Known'da yoksa siraya
/// girmez: ilk alinan esya (GDD 12 Kesif), "arastirma:x" (Arastirma) ya da "ocak:n" (Ocak basamagi).
/// Durum duz ve seriye dokulebilir (TEKNIK §9): isler (tarif, kalan adet, harcanan yakitlar) ve ilerleme.
/// </summary>
public sealed class StationQueue
{
    public sealed class Job
    {
        public RecipeDef Recipe { get; init; } = new();
        /// <summary>Kalan adet (suren dahil).</summary>
        public int Left { get; set; }
        /// <summary>Kalan her adet icin harcanmis yakit (null: yakitsiz); iptalde geri verilir.</summary>
        public List<string?> Fuel { get; } = new();
    }

    readonly GameData _data;
    readonly List<Job> _jobs = new();
    public string Station { get; }
    public PlaceableDef? Def { get; }
    public IReadOnlyList<Job> Jobs => _jobs;
    /// <summary>Suren isin tarifi; sira bossa null.</summary>
    public RecipeDef? Active => _jobs.Count > 0 ? _jobs[0].Recipe : null;
    /// <summary>Suren adedin ilerlemesi 0..1.</summary>
    public float Progress { get; private set; }
    /// <summary>Suren isin kalan adedi (suren dahil).</summary>
    public int Queued => _jobs.Count > 0 ? _jobs[0].Left : 0;
    /// <summary>Butun islerin kalan adedi.</summary>
    public int QueuedTotal => _jobs.Sum(j => j.Left);
    /// <summary>Urun depoya sigmadigi icin bekliyor.</summary>
    public bool Blocked { get; private set; }
    public int MaxJobs => Math.Max(1, _data.Research.StationQueueMax);
    public event Action<RecipeDef, int>? Produced;
    /// <summary>Oyuncunun simdiye kadar eline gecen esyalar ve acilan kilitler (GameState.Known). Null: hepsi acik.</summary>
    public ISet<string>? Known { get; set; }
    /// <summary>
    /// Guc (GDD §11.1; guc akisi baglar): false donerse istasyon durur. Null: guc kurali yok, calisir (research.json watts
    /// guc ag main'e girene kadar uygulanmaz).
    /// </summary>
    public Func<bool>? PowerOk { get; set; }
    /// <summary>Istasyonun istedigi guc (W); 0: gucsuz calisir.</summary>
    public int Watts => _data.Research.Watts.GetValueOrDefault(Station);

    public StationQueue(GameData data, string station)
    {
        _data = data;
        Station = station;
        Def = data.Tool.Placeables.FirstOrDefault(p => p.Station == station);
    }

    public IEnumerable<RecipeDef> Recipes => _data.Tool.Recipes.Where(r => r.Station == Station);

    public static bool CanAfford(RecipeDef r, IItemStore inv) => r.In.All(kv => inv.Count(kv.Key) >= kv.Value);

    public bool IsUnlocked(RecipeDef r) => r.UnlockOn == null || Known == null || Known.Contains(r.UnlockOn);

    /// <summary>Is basina yakilacak yakit (istasyon yakitsizsa null). Tarif yakiti girdi olarak da istiyorsa ikisi toplanir.</summary>
    public string? FuelFor(RecipeDef r, IItemStore inv)
    {
        if (Def == null || Def.FuelPerJob <= 0 || Def.Fuel.Count == 0) return null;
        return Def.Fuel.FirstOrDefault(f => inv.Count(f) - r.In.GetValueOrDefault(f) >= Def.FuelPerJob) ?? Def.Fuel[0];
    }

    public bool HasFuel(RecipeDef r, IItemStore inv)
    {
        var f = FuelFor(r, inv);
        return f == null || inv.Count(f) - r.In.GetValueOrDefault(f) >= Def!.FuelPerJob;
    }

    /// <summary>Malzeme, yakit ve kilit tamam mi.</summary>
    public bool CanMake(RecipeDef r, IItemStore inv) => IsUnlocked(r) && CanAfford(r, inv) && HasFuel(r, inv);

    /// <summary>Sira dolu mu (bu tarif sondaki isle birlesemiyorsa).</summary>
    public bool Full(RecipeDef r) => _jobs.Count >= MaxJobs && _jobs[^1].Recipe.Id != r.Id;

    /// <summary>
    /// Malzemeyi ve yakiti hemen alir ve siraya ekler (oyuncu istasyondan uzaklassa da uretim surer). Ayni tarif sondaysa
    /// ona eklenir (Rust: ayni satir buyur); yoksa yeni is.
    /// </summary>
    public bool Enqueue(RecipeDef r, IItemStore inv)
    {
        if (r.Station != Station || !CanMake(r, inv) || Full(r)) return false;
        var fuel = FuelFor(r, inv); // yakit, girdiler alinmadan secilir (FuelFor girdiyi zaten ayirir)
        foreach (var kv in r.In) inv.Remove(kv.Key, kv.Value);
        if (fuel != null && Def != null) inv.Remove(fuel, Def.FuelPerJob);
        var tail = _jobs.Count > 0 && _jobs[^1].Recipe.Id == r.Id ? _jobs[^1] : null;
        if (tail == null) { tail = new Job { Recipe = r }; _jobs.Add(tail); }
        tail.Left++;
        tail.Fuel.Add(fuel);
        return true;
    }

    /// <summary>
    /// Siradaki butun isleri iptal eder (karar 23: iptal malzemeyi geri verir): kalan adetlerin girdisi ve yakiti depoya
    /// doner, sure sifirlanir. Donen: geri verilen (esya, adet).
    /// </summary>
    public Dictionary<string, int> Cancel(IItemStore inv)
    {
        var back = new Dictionary<string, int>();
        while (_jobs.Count > 0) foreach (var (k, v) in Cancel(_jobs.Count - 1, inv)) back[k] = back.GetValueOrDefault(k) + v;
        return back;
    }

    /// <summary>Tek isi iptal eder; malzemesi ve yakiti depoya doner. Donen: geri verilen.</summary>
    public Dictionary<string, int> Cancel(int index, IItemStore inv)
    {
        var back = new Dictionary<string, int>();
        if (index < 0 || index >= _jobs.Count) return back;
        var j = _jobs[index];
        _jobs.RemoveAt(index);
        if (index == 0) { Progress = 0; Blocked = false; }
        void Give(string item, int n) { int a = inv.Add(item, n); if (a > 0) back[item] = back.GetValueOrDefault(item) + a; }
        foreach (var kv in j.Recipe.In) Give(kv.Key, kv.Value * j.Left);
        if (Def != null) foreach (var f in j.Fuel) if (f != null) Give(f, Def.FuelPerJob);
        return back;
    }

    /// <summary>Kuyrugun kalan suresi (sn).</summary>
    public float SecondsLeft => _jobs.Select((j, i) => j.Recipe.Seconds * (j.Left - (i == 0 ? Progress : 0f))).Sum();

    /// <returns>Bu tikte biten adet.</returns>
    public int Tick(float dt, IItemStore inv)
    {
        Blocked = false;
        if (_jobs.Count == 0) return 0;
        if (PowerOk != null && !PowerOk()) return 0;
        int done = 0;
        float left = dt;
        while (_jobs.Count > 0 && left > 0f)
        {
            var j = _jobs[0];
            float secs = Math.Max(0.01f, j.Recipe.Seconds);
            float need = (1f - Progress) * secs;
            if (left < need) { Progress += left / secs; break; }
            // Adet bitiyor: urun depoya sigmazsa bekler (kaybolmaz).
            if (!j.Recipe.Load && inv.Room(j.Recipe.Id) < j.Recipe.Out) { Progress = 1f; Blocked = true; break; }
            left -= need;
            Progress = 0f;
            if (!j.Recipe.Load) inv.Add(j.Recipe.Id, j.Recipe.Out);
            j.Left--;
            if (j.Fuel.Count > 0) j.Fuel.RemoveAt(0);
            if (j.Left <= 0) _jobs.RemoveAt(0);
            done++;
            Produced?.Invoke(j.Recipe, j.Recipe.Out);
        }
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
