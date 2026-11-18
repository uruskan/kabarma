namespace Kabarma.Sim;

// Ciftligin hayvan durumu ve zaman adimi (GDD 9.1-9.4): ihtiyac ve mutluluk, urun, gubre, isci isi, ureme, kacma,
// Suluk, Kasinma Fircasi, Ari Kovani, Kulucka. Hayvanlar, Yemlik ve Folluk Coop'ta (ilk gunle ortak); geri kalan burada.
// Agil uyeligi konumdan turetilir (PenShape.Contains); agil listesi her duvar degisiminde yeniden kurulur, kimligi yoktur.
// Durum duz ve serilestirilebilir (TEKNIK 9): kimlikler int, RNG durumu alan.

/// <summary>Suluk (ICERIK 10.2): agilin su ihtiyaci.</summary>
public sealed class Trough
{
    public int Id { get; set; }
    public float Water { get; set; }
    public int Capacity { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>Kasinma Fircasi (ICERIK 10.2): yaricapindaki hayvanlara mutluluk.</summary>
public sealed class Brush
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>Ari Kovani (ICERIK 10.2): Vizviz surusu yerlesir.</summary>
public sealed class Hive
{
    public int Id { get; set; }
    public int AnimalId { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>Kulucka (ICERIK 11): yumurtadan evcil yavru (GDD 9.1).</summary>
public sealed class Incubator
{
    public int Id { get; set; }
    /// <summary>Icindeki yumurtanin turu ("" = bos).</summary>
    public string Species { get; set; } = "";
    public float Progress { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>Agilda biriken gubre yigini (GDD 9.2: toplamak mutluluk verir).</summary>
public sealed class ManurePile
{
    public int Id { get; set; }
    public int Count { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

public enum RanchEventKind { Product, Nested, Grounded, Manure, Worked, Scouted, Born, Hatched, WantsOut, Escaped }

/// <summary>Bir adimda olan sey: Godot katmani gorur (efekt, ses, ipucu); Sim testleri sayar.</summary>
public readonly record struct RanchEvent(RanchEventKind Kind, int AnimalId, string Item = "", int Count = 0, int OtherId = 0);

public sealed class Ranch
{
    public HusbandryRules Rules { get; }
    readonly Coop _coop;
    readonly PenFile _pen;

    public List<PenAnimal> Animals => _coop.Animals;
    public List<Feeder> Feeders => _coop.Feeders;
    public List<Nest> Nests => _coop.Nests;
    public List<Trough> Troughs { get; set; } = new();
    public List<Brush> Brushes { get; set; } = new();
    public List<Hive> Hives { get; set; } = new();
    public List<Incubator> Incubators { get; set; } = new();
    public List<ManurePile> Manure { get; set; } = new();
    /// <summary>Hayvancilik saati (oyun saniyesi); gun = Clock / DaySeconds.</summary>
    public double Clock { get; set; }
    public int NextId { get; set; } = 1;
    public RanchRng Rng { get; set; } = new();

    public Ranch(Coop coop, PenFile pen, HusbandryFile def, CalendarFile cal, ulong seed)
    {
        _coop = coop;
        _pen = pen;
        Rules = new HusbandryRules(def, cal);
        Rng.State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed * 2654435761UL + 0x5851F42D4C957F2DUL;
    }

    public int Day => (int)(Clock / Rules.DaySeconds);
    public int NewId() => NextId++;

    // ---------- kayit ve olusturma ----------

    /// <summary>Yeni yabani hayvan: yabani genler, kimlik (Godot bir aktor dogurdugunda cagirir).</summary>
    public PenAnimal Wild(string species, float x, float z)
    {
        var a = new PenAnimal { Species = species, Id = NewId(), Genes = Rules.Wild(Rng), X = x, Z = z };
        Animals.Add(a);
        return a;
    }

    /// <summary>Kimligi olmayan (ilk gunun Gidak'i gibi) hayvana kimlik ve yabani gen verir.</summary>
    public void Adopt(PenAnimal a)
    {
        if (a.Id == 0) { a.Id = NewId(); a.Genes = Rules.Wild(Rng); }
        if (a.Tame && a.Role == "" && Rules.Known(a.Species)) a.Role = Rules.Species(a.Species).Roles.FirstOrDefault() ?? "";
    }

    public PenAnimal? ById(int id) => id == 0 ? null : Animals.FirstOrDefault(a => a.Id == id);

    public Feeder AddFeeder(float x, float z) { var f = new Feeder { Id = NewId(), Capacity = _pen.FeederCapacity, X = x, Z = z }; Feeders.Add(f); return f; }
    public Nest AddNest(float x, float z) { var n = new Nest { Id = NewId(), Slots = _pen.NestSlots, X = x, Z = z }; Nests.Add(n); return n; }
    public Trough AddTrough(float x, float z) { var t = new Trough { Id = NewId(), Capacity = Rules.Def.Trough.Capacity, Water = Rules.Def.Trough.Capacity, X = x, Z = z }; Troughs.Add(t); return t; }
    public Brush AddBrush(float x, float z) { var b = new Brush { Id = NewId(), X = x, Z = z }; Brushes.Add(b); return b; }
    public Hive AddHive(float x, float z) { var h = new Hive { Id = NewId(), X = x, Z = z }; Hives.Add(h); return h; }
    public Incubator AddIncubator(float x, float z) { var i = new Incubator { Id = NewId(), X = x, Z = z }; Incubators.Add(i); return i; }

    /// <summary>
    /// Evcil hayvan agilin icinde: kayit olur (GDD 9.1). Agildakilerin tur alanlari toplami (bu hayvan dahil) agil
    /// alanini asarsa olmaz. Kovan turu agila kayit olmaz.
    /// </summary>
    public bool Register(PenAnimal a, PenShape pen)
    {
        if (a.Registered || !a.Tame) return false;
        var s = Rules.Species(a.Species);
        if (s.Home != "pen") return false;
        if (UsedArea(pen, except: a) + s.Area > pen.Area + 0.01f) return false;
        a.Registered = true;
        a.WantsOut = false;
        a.LowSeconds = 0;
        if (a.Role == "") a.Role = s.Roles.FirstOrDefault() ?? "";
        return true;
    }

    /// <summary>Kovan turu bos bir Ari Kovani'na yerlesir ve kayit olur.</summary>
    public bool Settle(PenAnimal a, Hive h)
    {
        if (a.Registered || !a.Tame || h.AnimalId != 0 || Rules.Species(a.Species).Home != "hive") return false;
        h.AnimalId = a.Id;
        a.HiveId = h.Id;
        a.Registered = true;
        a.X = h.X; a.Z = h.Z;
        if (a.Role == "") a.Role = Rules.Species(a.Species).Roles.FirstOrDefault() ?? "";
        return true;
    }

    public static bool In(PenShape pen, float x, float z) => pen.Contains(new P2(x, z));
    public PenShape? PenOf(PenAnimal a, IReadOnlyList<PenShape> pens) => pens.FirstOrDefault(p => In(p, a.X, a.Z));

    /// <summary>Agildaki kayitli hayvanlarin tur alanlari toplami (yavrular dahil).</summary>
    public float UsedArea(PenShape pen, PenAnimal? except = null) =>
        Animals.Where(x => x != except && x.Registered && x.HiveId == 0 && In(pen, x.X, x.Z)).Sum(x => Rules.Known(x.Species) ? Rules.Species(x.Species).Area : _pen.Animals.FirstOrDefault(p => p.Id == x.Species)?.Area ?? 0f);

    // ---------- ihtiyaclar ----------

    public Feeder? FeederFor(PenShape pen, string food) =>
        Feeders.FirstOrDefault(f => f.Food == food && f.Amount > 0 && In(pen, f.X, f.Z));

    public int ManureIn(PenShape pen) => Manure.Where(m => In(pen, m.X, m.Z)).Sum(m => m.Count);

    /// <summary>Hayvanin ihtiyaclari (GDD 9.2). roof: Godot katmani agilin ustunde cati var mi diye bakar.</summary>
    public Needs NeedsOf(PenAnimal a, PenShape? pen, bool roof)
    {
        var s = Rules.Species(a.Species);
        bool petted = a.LastPetDay == Day;
        bool brush = Brushes.Any(b => MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)) <= Rules.Def.BrushRadius);
        if (a.HiveId != 0) // kovan: barinak, yem (cicek) ve alan kovandan
            return new Needs(true, Troughs.Any(t => t.Water > 0 && Dist(t.X, t.Z, a.X, a.Z) <= Rules.Def.BrushRadius * 2), true, true, brush, petted, 0);
        if (pen == null) return new Needs(false, false, false, false, brush, petted, 0);
        return new Needs(
            FeederFor(pen, s.Food) != null,
            Troughs.Any(t => t.Water > 0 && In(pen, t.X, t.Z)),
            roof,
            UsedArea(pen) <= pen.Area + 0.01f,
            brush, petted, ManureIn(pen));
    }

    static float Dist(float ax, float az, float bx, float bz) => MathF.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

    // ---------- zaman adimi ----------

    /// <summary>
    /// Butun hayvanlarin adimi. pens: su anki agillar; roof(pen): ustu kapali mi. Dondurulen olaylar Godot katmanina
    /// (efekt, ses, yeni aktor) ve testlere.
    /// </summary>
    public List<RanchEvent> Tick(float dt, IReadOnlyList<PenShape> pens, Func<PenShape, bool>? roof = null)
    {
        var ev = new List<RanchEvent>();
        if (dt <= 0) return ev;
        Clock += dt;
        float days = dt / Rules.DaySeconds;
        var def = Rules.Def;
        foreach (var a in Animals.ToList())
        {
            if (!Rules.Known(a.Species)) continue;
            if (a.Id == 0) Adopt(a);
            if (a.Happiness < 0) a.Happiness = def.Happiness.Start;
            a.AgeDays += days;
            if (!a.Registered) continue;
            var s = Rules.Species(a.Species);
            var pen = a.HiveId != 0 ? null : PenOf(a, pens);
            if (a.HiveId == 0 && pen == null) { TickOutside(a, dt, ev); continue; }

            // mutluluk hedefe kayar (GDD 9.2)
            var needs = NeedsOf(a, pen, pen != null && (roof?.Invoke(pen) ?? false));
            float target = Rules.Target(needs);
            float step = def.Happiness.RatePerDay * days;
            a.Happiness = a.Happiness < target ? Math.Min(target, a.Happiness + step) : Math.Max(target, a.Happiness - step);

            // su
            if (pen != null)
            {
                var t = Troughs.FirstOrDefault(x => x.Water > 0 && In(pen, x.X, x.Z));
                if (t != null) t.Water = Math.Max(0, t.Water - def.Trough.PerAnimalPerDay * days);
            }

            bool adult = a.Adult(def.Breeding.AdultDays);
            if (adult && a.Role == "uretici") Produce(a, s, pen, dt, ev);
            if (adult && a.Role == "isci" && s.Work != null) Work(a, s, pen, dt, ev);
            if (pen != null) DropManure(a, dt, ev);
            if (pen != null && s.Home == "pen") Breed(a, s, pen, pens, dt, ev);
            Unhappy(a, dt, ev);
        }
        foreach (var inc in Incubators) Incubate(inc, dt, ev);
        return ev;
    }

    /// <summary>Kayitli ama agilin disinda (kapi acikken cikti): kacar (GDD 9.1 kacma).</summary>
    void TickOutside(PenAnimal a, float dt, List<RanchEvent> ev)
    {
        if (a.WantsOut) { Escape(a); ev.Add(new RanchEvent(RanchEventKind.Escaped, a.Id)); }
    }

    void Produce(PenAnimal a, SpeciesDef s, PenShape? pen, float dt, List<RanchEvent> ev)
    {
        float speed = Rules.ProductSpeed(a);
        foreach (var p in s.Products)
        {
            if (p.Seconds <= 0) continue;
            float t = a.Timers.GetValueOrDefault(p.Item) + dt * speed;
            if (t < p.Seconds) { a.Timers[p.Item] = t; continue; }
            // Urun yalniz Yemlik'te turun yemi varken (GDD 9.2); kovan turu yemini disaridan bulur.
            Feeder? f = null;
            if (s.Home == "pen")
            {
                f = pen == null ? null : Feeders.FirstOrDefault(x => x.Food == s.Food && x.Amount >= Math.Max(1, p.Food) && In(pen, x.X, x.Z));
                if (f == null) { a.Timers[p.Item] = p.Seconds; continue; } // yem gelince hemen
            }
            if (f != null) f.Amount -= p.Food;
            a.Timers[p.Item] = t - p.Seconds;
            int n = Rules.ProductAmount(a, p);
            int stars = Rules.Tier(a.Happiness).Stars;
            if (p.Nest)
            {
                a.Laid += n;
                for (int i = 0; i < n; i++)
                {
                    var target = CoopRules.Place(pen == null ? Array.Empty<Nest>() : Nests.Where(x => In(pen, x.X, x.Z)), out _);
                    ev.Add(new RanchEvent(target == LayTarget.Nest ? RanchEventKind.Nested : RanchEventKind.Grounded, a.Id, p.Item, 1));
                }
                continue;
            }
            AddPending(a, p.Item, n, stars);
            ev.Add(new RanchEvent(RanchEventKind.Product, a.Id, p.Item, n));
        }
    }

    public static void AddPending(PenAnimal a, string item, int n, int stars)
    {
        var st = a.Pending.FirstOrDefault(x => x.Item == item && x.Stars == stars);
        if (st == null) a.Pending.Add(new ProductStack { Item = item, Count = n, Stars = stars });
        else st.Count += n;
    }

    void Work(PenAnimal a, SpeciesDef s, PenShape? pen, float dt, List<RanchEvent> ev)
    {
        var w = s.Work!;
        float t = a.Timers.GetValueOrDefault("work") + dt * Rules.ProductSpeed(a);
        if (t < w.Seconds) { a.Timers["work"] = t; return; }
        switch (w.Kind)
        {
            case "dig":
            {
                float total = w.Yield.Sum(y => y.Weight), r = Rng.Float() * total;
                var pick = w.Yield.FirstOrDefault(y => (r -= y.Weight) < 0) ?? w.Yield.LastOrDefault();
                if (pick == null) break;
                AddPending(a, pick.Item, 1, 1);
                ev.Add(new RanchEvent(RanchEventKind.Worked, a.Id, pick.Item, 1));
                break;
            }
            case "compost":
            {
                var pile = pen == null ? null : Manure.FirstOrDefault(m => m.Count > 0 && In(pen, m.X, m.Z));
                if (pile == null) { a.Timers["work"] = w.Seconds; return; } // gubre yoksa bekler
                if (--pile.Count <= 0) Manure.Remove(pile);
                AddPending(a, w.Out, 1, 1);
                ev.Add(new RanchEvent(RanchEventKind.Worked, a.Id, w.Out, 1));
                break;
            }
            case "scout":
                ev.Add(new RanchEvent(RanchEventKind.Scouted, a.Id, "", (int)w.Radius));
                break;
        }
        a.Timers["work"] = t - w.Seconds;
    }

    void DropManure(PenAnimal a, float dt, List<RanchEvent> ev)
    {
        var m = Rules.Def.Manure;
        if (m.Seconds <= 0) return;
        float t = a.Timers.GetValueOrDefault("manure") + dt;
        if (t < m.Seconds) { a.Timers["manure"] = t; return; }
        a.Timers["manure"] = t - m.Seconds;
        var pile = Manure.FirstOrDefault(p => Dist(p.X, p.Z, a.X, a.Z) <= m.PileRadius);
        if (pile == null) Manure.Add(pile = new ManurePile { Id = NewId(), X = a.X, Z = a.Z });
        pile.Count++;
        ev.Add(new RanchEvent(RanchEventKind.Manure, a.Id, m.Item, 1, pile.Id));
    }

    /// <summary>Gubre yigini cantaya (GDD 9.2: toplamak mutluluk verir; ICERIK 2.2 Gubre). Alinan adet.</summary>
    public int CollectManure(ManurePile p, Inventory bag)
    {
        int put = bag.Add(Rules.Def.Manure.Item, p.Count);
        p.Count -= put;
        if (p.Count <= 0) Manure.Remove(p);
        return put;
    }

    // ---------- ureme (GDD 9.3) ----------

    bool CanBreed(PenAnimal a) => a.Registered && a.Tame && a.Adult(Rules.Def.Breeding.AdultDays) && a.Happiness >= Rules.Def.Breeding.MinHappiness;

    void Breed(PenAnimal a, SpeciesDef s, PenShape pen, IReadOnlyList<PenShape> pens, float dt, List<RanchEvent> ev)
    {
        var b = Rules.Def.Breeding;
        if (a.BreedTimer >= 0)
        {
            var mate = ById(a.MateId);
            if (mate == null || !CanBreed(a) || !CanBreed(mate) || PenOf(mate, pens) != pen) { a.BreedTimer = -1; a.MateId = 0; return; }
            a.BreedTimer -= dt;
            if (a.BreedTimer > 0) return;
            a.BreedTimer = -1; a.MateId = 0;
            if (UsedArea(pen) + s.Area > pen.Area + 0.01f) return; // yer kalmadi
            var child = new PenAnimal
            {
                Species = a.Species, Id = NewId(), Genes = Rules.Inherit(a.Genes, mate.Genes, Rng), Trust = 1f, Registered = true,
                AgeDays = 0, Happiness = (a.Happiness + mate.Happiness) * 0.5f, MotherId = a.Id, FatherId = mate.Id,
                Role = s.Roles.FirstOrDefault() ?? "", X = a.X + 0.3f, Z = a.Z + 0.3f,
            };
            Animals.Add(child);
            ev.Add(new RanchEvent(RanchEventKind.Born, child.Id, a.Species, 1, a.Id));
            return;
        }
        if (!CanBreed(a) || Animals.Any(x => x.MateId == a.Id && x.BreedTimer >= 0)) return;
        if (UsedArea(pen) + s.Area > pen.Area + 0.01f) return; // GDD 9.3: bos yer
        var partner = Animals.FirstOrDefault(x => x != a && x.Species == a.Species && x.Id > a.Id && x.BreedTimer < 0
            && CanBreed(x) && !Animals.Any(y => y.MateId == x.Id && y.BreedTimer >= 0) && PenOf(x, pens) == pen);
        if (partner == null) return;
        a.MateId = partner.Id;
        a.BreedTimer = Rng.Range(b.MinDays, b.MaxDays) * Rules.DaySeconds;
    }

    // ---------- kacma ----------

    void Unhappy(PenAnimal a, float dt, List<RanchEvent> ev)
    {
        var e = Rules.Def.Escape;
        if (a.Happiness >= e.Below) { a.LowSeconds = 0; a.WantsOut = false; return; }
        a.LowSeconds += dt;
        if (!a.WantsOut && a.LowSeconds >= e.Seconds)
        {
            a.WantsOut = true;
            ev.Add(new RanchEvent(RanchEventKind.WantsOut, a.Id));
        }
    }

    /// <summary>Kacti: kayit duser, guven yariya iner (GDD 9.1 kacma). Kovandaysa kovan bosalir.</summary>
    public void Escape(PenAnimal a)
    {
        a.Registered = false;
        a.WantsOut = false;
        a.LowSeconds = 0;
        a.BreedTimer = -1; a.MateId = 0;
        a.Trust = Math.Min(a.Trust, Rules.Def.Escape.TrustAfter);
        var h = Hives.FirstOrDefault(x => x.Id == a.HiveId);
        if (h != null) h.AnimalId = 0;
        a.HiveId = 0;
    }

    // ---------- Suluk, Kulucka ----------

    /// <summary>Safak: Suluk dolar (yagmur gelene kadar, husbandry.json trough.refillAtDawn).</summary>
    public void Dawn()
    {
        if (!Rules.Def.Trough.RefillAtDawn) return;
        foreach (var t in Troughs) t.Water = t.Capacity;
    }

    /// <summary>Kulucka'ya yumurta koyar (cantadan). Kondu mu.</summary>
    public bool PutEgg(Incubator inc, Inventory bag, string eggItem)
    {
        if (inc.Species != "") return false;
        var s = Rules.Def.Species.FirstOrDefault(x => x.Tame == "egg" && x.Egg == eggItem);
        if (s == null || !bag.Remove(eggItem, 1)) return false;
        inc.Species = s.Id;
        inc.Progress = 0;
        return true;
    }

    void Incubate(Incubator inc, float dt, List<RanchEvent> ev)
    {
        if (inc.Species == "") return;
        inc.Progress += dt;
        if (inc.Progress < Rules.Def.Incubator.Seconds) return;
        var a = new PenAnimal
        {
            Species = inc.Species, Id = NewId(), Genes = Rules.Wild(Rng), Trust = 1f, AgeDays = 0,
            Happiness = Rules.Def.Happiness.Start, X = inc.X + 0.8f, Z = inc.Z,
            Role = Rules.Species(inc.Species).Roles.FirstOrDefault() ?? "",
        };
        Animals.Add(a);
        inc.Species = "";
        inc.Progress = 0;
        ev.Add(new RanchEvent(RanchEventKind.Hatched, a.Id, a.Species, 1, inc.Id));
    }
}
