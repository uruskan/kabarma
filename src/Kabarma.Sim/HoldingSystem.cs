namespace Kabarma.Sim;

/// <summary>
/// Bereket Holding kurallari (GDD §19, §19.4.1; ICERIK §3.7): kampin kurulusu ve seviyesi, birim bilesimi, Perde, Husumet,
/// kervan ve pusu, onlarin baskini ve senin baskinin, ganimet, Sube Cekirdegi'nin yikilip yeniden kurulmasi.
/// Konum ve hareket Godot katmaninda (birimler X/Z'yi buraya yazar); kurallar ve sayaclar burada, durum HoldingState'te.
/// </summary>
public sealed class HoldingSystem
{
    readonly HoldingFile _h;
    readonly CombatFile _combat;
    public HoldingState State { get; private set; }

    /// <summary>Kamp kuruldu ya da yeniden kuruldu.</summary>
    public event Action<HoldingCamp>? CampFounded;
    /// <summary>Kamp seviye atladi (kamp, yeni seviye).</summary>
    public event Action<HoldingCamp, int>? LevelUp;
    /// <summary>Husumet degisti (kamp, degisim, neden).</summary>
    public event Action<HoldingCamp, float, string>? HusumetChanged;
    /// <summary>Kervan yola cikti.</summary>
    public event Action<HoldingCamp, HoldingCaravan>? CaravanDeparted;
    /// <summary>Pusu basarili (kamp, ganimet).</summary>
    public event Action<HoldingCamp, HoldingLoot>? Ambushed;
    /// <summary>Yapi yikildi (kamp, yapi, ganimet).</summary>
    public event Action<HoldingCamp, string, HoldingLoot>? StructureDestroyed;
    /// <summary>Sube Cekirdegi yikildi.</summary>
    public event Action<HoldingCamp>? CoreDestroyed;
    /// <summary>Onlarin baskin ekibi yola cikti (kamp, birimler).</summary>
    public event Action<HoldingCamp, List<HoldingUnit>>? RaidLaunched;

    public HoldingSystem(GameData d, HoldingState? state = null)
    {
        _h = d.Holding;
        _combat = d.Combat;
        State = state ?? new HoldingState();
    }

    public void Restore(HoldingState s) => State = s;

    public HoldingFile Def => _h;

    // ---------- Bilesim ----------

    /// <summary>Seviye tavani: min(maxLevel, Ocak basamagi × 2).</summary>
    public int Cap(int ocakTier) => Math.Clamp(Math.Max(1, ocakTier) * _h.Camp.CapPerOcakTier, 1, _h.Camp.MaxLevel);

    /// <summary>Seviye L icin (bekci, isci, kule, sur): 2+L, 2+L/2, L/2, L/3 (asagi yuvarlanir).</summary>
    public (int guards, int workers, int towers, int walls) Composition(int level)
    {
        var c = _h.Camp.Composition;
        return (c.GuardsBase + (int)MathF.Floor(level * c.GuardsPerLevel + 1e-4f),
                c.WorkersBase + (int)MathF.Floor(level * c.WorkersPerLevel + 1e-4f),
                (int)MathF.Floor(level * c.TowersPerLevel + 1e-4f),
                (int)MathF.Floor(level * c.WallsPerLevel + 1e-4f));
    }

    public float StructureMaxHp(HoldingStructureDef s, int level) => s.Hp + s.HpPerLevel * (level - 1);

    public HoldingCamp? Camp(int id) => State.Camps.FirstOrDefault(c => c.Id == id);
    public HoldingCamp? FirstCamp => State.Camps.FirstOrDefault();

    // ---------- Gun ve gece ----------

    /// <summary>Kabarma bitti (safak). Gercek Kabarma'ysa ve kamp yoksa kamp ertesi sabah kurulur.</summary>
    public void OnKabarmaEnded(bool real, int day)
    {
        if (!real || State.Camps.Count > 0 || State.FoundOnDay > 0) return;
        State.FoundOnDay = day + 1;
    }

    /// <summary>
    /// Takvimin sabahi: bekleyen kamp kurulur, yikilan kamp 3 gun sonra 1. seviyeden doner; her kampa gunluk ve ihracat
    /// Husumet'i; baskin yemeyen kamp 5 gunde bir seviye atlar (tavan Ocak basamagi × 2); birimler bilesime tamamlanir.
    /// </summary>
    public void OnMorning(int day, int ocakTier)
    {
        if (day <= State.Day) return;
        State.Day = day;
        if (State.FoundOnDay > 0 && day >= State.FoundOnDay && State.Camps.Count == 0)
        {
            State.FoundOnDay = 0;
            var a = _h.Camp.Anchor;
            var camp = new HoldingCamp { Id = ++State.Serial, X = a.Count > 0 ? a[0] : 0, Z = a.Count > 1 ? a[1] : 0, FoundedDay = day, Husumet = _h.Husumet.Start };
            State.Camps.Add(camp);
            Build(camp);
            CampFounded?.Invoke(camp);
        }
        float export = Math.Min(_h.Husumet.ExportMax, State.ExportToday / 1000 * _h.Husumet.ExportPerThousand);
        State.ExportToday = 0;
        int cap = Cap(ocakTier);
        foreach (var c in State.Camps)
        {
            if (c.Destroyed)
            {
                if (day >= c.RebuildDay) { c.Destroyed = false; c.Level = 1; c.QuietDays = 0; c.FoundedDay = day; Build(c); CampFounded?.Invoke(c); }
                continue;
            }
            if (c.FoundedDay == day) continue; // kuruldugu sabah sayilmaz
            AddHusumet(c, _h.Husumet.Daily, "gun");
            if (export > 0) AddHusumet(c, export, "ihracat");
            c.QuietDays++;
            if (c.QuietDays >= _h.Camp.LevelEveryDays && c.Level < cap)
            {
                c.Level++;
                c.QuietDays = 0;
                foreach (var s in _h.Camp.Structures) if (c.Structures.TryGetValue(s.Id, out var hp) && hp > 0) c.Structures[s.Id] = StructureMaxHp(s, c.Level);
                LevelUp?.Invoke(c, c.Level);
            }
            c.Units.RemoveAll(u => u.Job == "caravan"); // dunku kervan biter
            c.Caravan = null;
            Replenish(c);
        }
    }

    /// <summary>Kargo satisi (ihracat geliri): ertesi sabah bin Kredi basina +1 Husumet (en fazla +3).</summary>
    public void RecordExport(int credits) { if (credits > 0) State.ExportToday += credits; }

    /// <summary>
    /// Kabarma gecesi basladi. Gercekse Perde kalkar; Husumet 50 ve ustundeki kamp baskin ekibi gonderir (L/2 + 2 birim:
    /// bir ustabasi, bir mekanik, gerisi bekci). Donen: yola cikan ekipler.
    /// </summary>
    public List<(HoldingCamp camp, List<HoldingUnit> units)> OnNightStart(bool real)
    {
        var raids = new List<(HoldingCamp, List<HoldingUnit>)>();
        State.NightOpen = real;
        if (!real) return raids;
        foreach (var c in State.Camps)
        {
            c.RaidedTonight = false;
            c.RaidSent = false;
            c.RaidEntered = false;
            if (c.Destroyed || c.Husumet < _h.Raid.Threshold) continue;
            int n = _h.Raid.BaseCount + c.Level / 2;
            var team = new List<HoldingUnit>();
            for (int i = 0; i < n; i++)
            {
                string kind = i == 0 ? "ustabasi" : i == 1 && n > 2 ? "mekanik" : "bekci";
                team.Add(NewUnit(c, kind, "raid"));
            }
            c.Units.AddRange(team);
            c.RaidSent = true;
            raids.Add((c, team));
            RaidLaunched?.Invoke(c, team);
        }
        return raids;
    }

    /// <summary>Onlarin baskin ekibi oyuncunun tapusuna girdi: o kampin Husumet'i −40 (gece basina bir kez).</summary>
    public void OnRaidEntered(int campId)
    {
        var c = Camp(campId);
        if (c == null || !c.RaidSent || c.RaidEntered) return;
        c.RaidEntered = true;
        AddHusumet(c, _h.Husumet.TheyRaided, "onlarin_baskini");
    }

    /// <summary>Safak: Perde iner; baskin ekipleri dagilir.</summary>
    public void OnDawn()
    {
        State.NightOpen = false;
        foreach (var c in State.Camps) c.Units.RemoveAll(u => u.Job == "raid");
    }

    /// <summary>Perde: gunduz (gercek Kabarma disinda) kamp tapusu icinde hasar yok.</summary>
    public bool PerdeUp(HoldingCamp c) => !c.Destroyed && !State.NightOpen;

    public bool InPerde(HoldingCamp c, float x, float z) =>
        (x - c.X) * (x - c.X) + (z - c.Z) * (z - c.Z) <= _h.Camp.PerdeRadius * _h.Camp.PerdeRadius;

    /// <summary>Bu noktada hasar engelli mi (Perde'si inik bir kampin tapusunda).</summary>
    public bool DamageBlockedAt(float x, float z) => State.Camps.Any(c => PerdeUp(c) && InPerde(c, x, z));

    // ---------- Senin baskinin ----------

    /// <summary>
    /// Kamp yapisina vurus. Perde inikse engellenir; Cekirdek oteki yapilar yikilmadan kilitlidir. Gecenin ilk isabeti
    /// Husumet +25 ve seviye sayacini sifirlar. Yikilirsa ganimet doner (loot).
    /// </summary>
    public HoldingHit HitStructure(int campId, string structId, float dmg, out HoldingLoot? loot)
    {
        loot = null;
        var c = Camp(campId);
        if (c == null || c.Destroyed || !c.Structures.TryGetValue(structId, out var hp) || hp <= 0) return HoldingHit.Blocked;
        if (PerdeUp(c)) return HoldingHit.Blocked;
        if (!c.RaidedTonight)
        {
            c.RaidedTonight = true;
            c.QuietDays = 0;
            State.PlayerRaids++;
            AddHusumet(c, _h.Husumet.NightRaid, "gece_baskini");
        }
        var def = _h.Camp.Structures.First(s => s.Id == structId);
        if (def.Core && c.Structures.Any(kv => kv.Key != structId && kv.Value > 0)) return HoldingHit.Locked;
        hp = Math.Max(0, hp - Math.Max(0, dmg));
        c.Structures[structId] = hp;
        if (hp > 0) return HoldingHit.Hit;
        loot = Loot(structId, c.Level);
        StructureDestroyed?.Invoke(c, structId, loot);
        if (def.Core)
        {
            c.Destroyed = true;
            c.RebuildDay = State.Day + _h.Camp.RebuildDays;
            c.Husumet = 0;
            c.Units.Clear();
            c.Caravan = null;
            State.CoresDestroyed++;
            HusumetChanged?.Invoke(c, 0, "cekirdek");
            CoreDestroyed?.Invoke(c);
        }
        return HoldingHit.Destroyed;
    }

    public HoldingLoot Loot(string source, int level)
    {
        if (!_h.Loot.TryGetValue(source, out var l)) return new HoldingLoot(source, new(), 0, "");
        return new HoldingLoot(source, new Dictionary<string, int>(l.Items), l.Credits * Math.Max(1, level), l.Token);
    }

    // ---------- Birimler ----------

    public HoldingUnit? Unit(int id) => State.Camps.SelectMany(c => c.Units).FirstOrDefault(u => u.Id == id);
    public HoldingCamp? CampOf(HoldingUnit u) => State.Camps.FirstOrDefault(c => c.Units.Contains(u));

    /// <summary>
    /// Birime vurus (zirh duser). Perde'deki birim (gunduz kamp tapusunda) hasar almaz. Donen: verilen hasar; olduyse
    /// Alive=false. Kervanin son korumasi ve araci duserse pusu basarili olur.
    /// </summary>
    public float HitUnit(int unitId, float dmg)
    {
        var u = Unit(unitId);
        if (u == null || !u.Alive) return 0;
        var c = CampOf(u)!;
        if (PerdeUp(c) && InPerde(c, u.X, u.Z)) return 0;
        var def = _h.Unit(u.Kind);
        // Ortak hasar arayuzu (savas, Combat.cs): birimin zirh orani zirh puanina cevrilir; durumlar M3'te birimde tutulmaz.
        var body = CombatBody.Of(u.Hp, CombatTeam.Holding, "", Combat.ArmorPointsFor(_combat, Math.Clamp(def.Armor, 0f, 0.9f)));
        float done = Combat.Apply(_combat, body, new CombatHit(Math.Max(0, dmg), DamageType.Darbe, CombatTeam.Player)).Damage;
        u.Hp = body.Hp;
        if (u.Hp <= 0) { u.Hp = 0; u.Alive = false; u.Carrying = false; }
        if (u.Job == "caravan") CheckAmbush(c);
        return done;
    }

    /// <summary>Kervan aracina vurus.</summary>
    public float HitCaravan(int campId, float dmg)
    {
        var c = Camp(campId);
        var k = c?.Caravan;
        if (c == null || k == null || k.Ambushed || k.VehicleHp <= 0) return 0;
        if (PerdeUp(c) && InPerde(c, k.X, k.Z)) return 0;
        float done = Math.Min(k.VehicleHp, Math.Max(0, dmg));
        k.VehicleHp -= done;
        CheckAmbush(c);
        return done;
    }

    void CheckAmbush(HoldingCamp c)
    {
        var k = c.Caravan;
        if (k == null || k.Ambushed || k.VehicleHp > 0 || c.Units.Any(u => u.Job == "caravan" && u.Alive)) return;
        k.Ambushed = true;
        k.Phase = "done";
        State.Ambushes++;
        AddHusumet(c, _h.Husumet.Ambush, "pusu");
        var loot = new HoldingLoot("caravan", new Dictionary<string, int>(k.Cargo), k.Credits, "");
        c.Units.RemoveAll(u => u.Job == "caravan");
        Ambushed?.Invoke(c, loot);
    }

    /// <summary>Gunun kervani yola cikar (gunde bir; kamp ayaktaysa). Bekci sayisi 2 + L/2, en fazla 4.</summary>
    public HoldingCaravan? DepartCaravan(int campId, int day)
    {
        var c = Camp(campId);
        if (c == null || c.Destroyed || c.LastCaravanDay >= day) return null;
        var kd = _h.Caravan;
        c.LastCaravanDay = day;
        var k = new HoldingCaravan
        {
            Day = day, X = c.X, Z = c.Z, VehicleHp = kd.VehicleHp,
            Cargo = new Dictionary<string, int>(kd.Cargo), Credits = kd.CreditsPerLevel * c.Level,
        };
        c.Caravan = k;
        int guards = Math.Min(kd.GuardsMax, kd.GuardsBase + c.Level / 2);
        for (int i = 0; i < guards; i++) c.Units.Add(NewUnit(c, "bekci", "caravan"));
        CaravanDeparted?.Invoke(c, k);
        return k;
    }

    /// <summary>Kervan kampa dondu: korumalari kadroya karisir (ayri birim olarak kalkar).</summary>
    public void CaravanHome(int campId)
    {
        var c = Camp(campId);
        if (c?.Caravan == null || c.Caravan.Ambushed) return;
        c.Caravan.Phase = "done";
        c.Units.RemoveAll(u => u.Job == "caravan"); // korumalar kamp kadrosuna doner (kadro bilesimden sayilir)
    }

    HoldingUnit NewUnit(HoldingCamp c, string kind, string job)
    {
        var def = _h.Unit(kind);
        return new HoldingUnit { Id = ++State.Serial, Kind = kind, Job = job, Hp = def.Hp, X = c.X, Z = c.Z };
    }

    /// <summary>Kamp yapilarini ve birimlerini seviyeye gore kurar.</summary>
    void Build(HoldingCamp c)
    {
        c.Structures.Clear();
        foreach (var s in _h.Camp.Structures) c.Structures[s.Id] = StructureMaxHp(s, c.Level);
        c.Units.Clear();
        Replenish(c);
    }

    /// <summary>Kamp birimlerini (olenler sabah yenilenir) seviye bilesimine tamamlar.</summary>
    void Replenish(HoldingCamp c)
    {
        c.Units.RemoveAll(u => !u.Alive && u.Job == "camp");
        var (guards, workers, _, _) = Composition(c.Level);
        int g = c.Units.Count(u => u.Job == "camp" && u.Kind == "bekci" && u.Alive);
        int w = c.Units.Count(u => u.Job == "camp" && u.Kind == "isci" && u.Alive);
        for (; g < guards; g++) c.Units.Add(NewUnit(c, "bekci", "camp"));
        for (; w < workers; w++) c.Units.Add(NewUnit(c, "isci", "camp"));
    }

    void AddHusumet(HoldingCamp c, float delta, string why)
    {
        float before = c.Husumet;
        c.Husumet = Math.Clamp(c.Husumet + delta, 0, _h.Husumet.Max);
        if (Math.Abs(c.Husumet - before) > 1e-4f) HusumetChanged?.Invoke(c, c.Husumet - before, why);
    }
}
