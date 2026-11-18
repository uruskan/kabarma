namespace Kabarma.Sim;

/// <summary>
/// Uretim kurallari (GDD 5 "Uretim paneli", URETIM 6 M1 karari 23): el tarifi her yerde, istasyon tarifi yalniz o
/// istasyonun yakininda; eksik malzeme ve kaynagi; secilebilen en buyuk adet.
/// </summary>
public static class CraftRules
{
    /// <summary>El tarifi: istasyonu yok (recipes.json "station" bos).</summary>
    public static bool IsHand(RecipeDef r) => string.IsNullOrEmpty(r.Station);

    /// <summary>
    /// Tarif burada yapilabilir mi: el tarifi hep; istasyon tarifi yalniz o istasyon yakindaysa. Donen null: yapilabilir;
    /// degilse gereken istasyonun kimligi (satir "Tezgah gerekir" der).
    /// </summary>
    public static string? NeedStation(RecipeDef r, IEnumerable<string> nearStations) =>
        IsHand(r) || nearStations.Contains(r.Station) ? null : r.Station;

    /// <summary>Bu adet icin eksik malzemeler: (esya, eldeki, gereken). Bossa malzeme tamam.</summary>
    public static List<(string item, int have, int need)> Missing(RecipeDef r, int count, Inventory inv)
    {
        var l = new List<(string, int, int)>();
        foreach (var (item, n) in r.In)
        {
            int have = inv.Count(item), need = n * Math.Max(1, count);
            if (have < need) l.Add((item, have, need));
        }
        return l;
    }

    /// <summary>Cantadaki malzemeyle kac adet yapilabilir (girdisiz tarif icin <paramref name="cap"/>).</summary>
    public static int Affordable(RecipeDef r, Inventory inv, int cap)
    {
        int n = cap;
        foreach (var (item, need) in r.In) if (need > 0) n = Math.Min(n, inv.Count(item) / need);
        return Math.Max(0, n);
    }

    /// <summary>Eksik malzemenin kaynak metni ("Kil: gol kiyisindaki kil yataklari, Kazma"); kaynagi yazilmamissa null.</summary>
    public static ItemSourceDef? Source(ToolFile tool, string item) => tool.Sources.TryGetValue(item, out var s) ? s : null;
}

/// <summary>
/// El uretim kuyrugu (GDD 5): Rust gibi sureli ve sirali. Malzeme siraya girerken cantadan alinir; is basinda bir
/// adet, sure dolunca urun cantaya girer. Iptal edilen isin kalan adetlerinin malzemesi geri doner. Canta doluysa is
/// bekler (urun kaybolmaz). Duz ve seriye dokulebilir (TEKNIK 9).
/// </summary>
public sealed partial class CraftQueue
{
    public sealed class Job
    {
        public RecipeDef Recipe { get; init; } = new();
        /// <summary>Kalan adet (suren dahil).</summary>
        public int Left { get; set; }
        /// <summary>Suren adedin ilerlemesi 0..1 (yalniz ilk iste).</summary>
        public float Progress { get; set; }
    }

    readonly List<Job> _jobs = new();
    public int Max { get; }
    public IReadOnlyList<Job> Jobs => _jobs;
    /// <summary>Ilk is canta dolu oldugu icin bekliyor.</summary>
    public bool Blocked { get; private set; }
    /// <summary>Oyuncunun simdiye kadar eline gecen esyalar (kilitli tarifler); null: hepsi acik.</summary>
    public ISet<string>? Known { get; set; }
    /// <summary>Bir adet bitti: (tarif, urun adedi).</summary>
    public event Action<RecipeDef, int>? Produced;

    public CraftQueue(int max) { Max = Math.Max(1, max); }

    public bool IsUnlocked(RecipeDef r) => r.UnlockOn == null || Known == null || Known.Contains(r.UnlockOn);

    /// <summary>Kuyrugun kalan suresi (sn).</summary>
    public float SecondsLeft => _jobs.Select((j, i) => j.Recipe.Seconds * (j.Left - (i == 0 ? j.Progress : 0f))).Sum();

    /// <summary>Tarifi <paramref name="count"/> adet siraya koyar, malzemeyi hemen alir. Kuyruk dolu, kilitli ya da malzeme yetmiyorsa false.</summary>
    public bool Enqueue(RecipeDef r, int count, Inventory inv)
    {
        if (count < 1 || r.Load || !IsUnlocked(r)) return false;
        // Ayni tarif sondaysa ona eklenir (Rust: ayni satir buyur); yoksa yeni is ister.
        var tail = _jobs.Count > 0 ? _jobs[^1] : null;
        bool merge = tail != null && tail.Recipe.Id == r.Id;
        if (!merge && _jobs.Count >= Max) return false;
        if (CraftRules.Missing(r, count, inv).Count > 0) return false;
        foreach (var (item, n) in r.In) inv.Remove(item, n * count);
        if (merge) tail!.Left += count;
        else _jobs.Add(new Job { Recipe = r, Left = count });
        return true;
    }

    /// <returns>Bu tikte biten adet.</returns>
    public int Tick(float dt, Inventory inv)
    {
        int done = 0;
        Blocked = false;
        float left = dt;
        while (_jobs.Count > 0 && left > 0f)
        {
            var j = _jobs[0];
            float secs = Math.Max(0.01f, j.Recipe.Seconds);
            float need = (1f - j.Progress) * secs;
            if (left < need) { j.Progress += left / secs; break; }
            // Adet bitiyor: urun cantaya sigmazsa bekler.
            if (inv.Room(j.Recipe.Id) < j.Recipe.Out) { j.Progress = 1f; Blocked = true; break; }
            left -= need;
            inv.Add(j.Recipe.Id, j.Recipe.Out);
            j.Left--;
            j.Progress = 0f;
            done++;
            if (j.Left <= 0) _jobs.RemoveAt(0);
            Produced?.Invoke(j.Recipe, j.Recipe.Out);
        }
        return done;
    }

    /// <summary>
    /// Isi iptal eder: kalan adetlerin (suren dahil) malzemesi cantaya geri doner. Donen: geri verilen (esya, adet);
    /// canta doluysa sigmayan kisim donende eksik kalir (Godot onu yere birakir).
    /// </summary>
    public Dictionary<string, int> Cancel(int index, Inventory inv)
    {
        var back = new Dictionary<string, int>();
        if (index < 0 || index >= _jobs.Count) return back;
        var j = _jobs[index];
        _jobs.RemoveAt(index);
        foreach (var (item, n) in j.Recipe.In)
        {
            int added = inv.Add(item, n * j.Left);
            if (added > 0) back[item] = added;
        }
        return back;
    }
}
