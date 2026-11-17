namespace Kabarma.Sim;

public enum NightPhase { Alarm, Waves, Dawn }

/// <summary>
/// Kartlarin toplam etkisi; silah ve beden bu degerlerle calisir (ICERIK 8.1). Kart yokken her deger tabandir.
/// Asagidaki yardimcilar kartin kuralini tek yerde tutar: Godot katmani taban degeri verir, sonucu kullanir.
/// </summary>
public sealed class CombatStats
{
    public int Projectiles = 1;
    public int Bounces;
    public float FireRateMult = 1f;
    public float SizeMult = 1f;     // boy ve hasar
    public float BurnChance;
    public float FreezeChance;
    public float CritChance;
    public float KnockbackMult = 1f;
    public float PickupRangeMult = 1f;
    public float MoveSpeedMult = 1f;

    /// <summary>GDD 15: safakta butun kartlar soner.</summary>
    public void Reset()
    {
        Projectiles = 1; Bounces = 0; FireRateMult = 1f; SizeMult = 1f; BurnChance = 0f; FreezeChance = 0f;
        CritChance = 0f; KnockbackMult = 1f; PickupRangeMult = 1f; MoveSpeedMult = 1f;
    }

    public bool IsBase => Projectiles == 1 && Bounces == 0 && FireRateMult == 1f && SizeMult == 1f && BurnChance == 0f &&
                          FreezeChance == 0f && CritChance == 0f && KnockbackMult == 1f && PickupRangeMult == 1f && MoveSpeedMult == 1f;

    /// <summary>Tez El: ates hizi carpani araligi kisaltir.</summary>
    public float FireInterval(float baseInterval) => baseInterval / FireRateMult;
    /// <summary>Iri Tane: mermi hasari (boy da ayni carpanla buyur).</summary>
    public float Damage(float baseDamage) => baseDamage * SizeMult;
    /// <summary>Iri Tane: mermi boyu ve menzili.</summary>
    public float Size(float baseSize) => baseSize * SizeMult;
    /// <summary>Tokmak: itme.</summary>
    public float Knockback(float baseKnockback) => baseKnockback * KnockbackMult;
    /// <summary>Miknatis: toplama menzili.</summary>
    public float PickupRadius(float baseRadius) => baseRadius * PickupRangeMult;
    /// <summary>Cevik: yurume ve kosu hizi.</summary>
    public float MoveSpeed(float baseSpeed) => baseSpeed * MoveSpeedMult;
    /// <summary>Keskin Goz: silahin kendi kritik sansina eklenir.</summary>
    public float Crit(float weaponCrit) => Math.Min(1f, weaponCrit + CritChance);
}

/// <summary>Bir kartin gecedeki son rutbesi (Hasat Raporu icin, safakta kart sonmeden once alinir).</summary>
public sealed record TakenCard(string Id, string Name, int Rank);

/// <summary>
/// Yerde bekleyen Oz (URETIM 6 M1 karari 19): oldurulen dusmanin yerine duser, degeri oldurme aninda hesaplanir.
/// Konum yatay duzlemde (x, z), metre.
/// </summary>
public sealed class OzMote
{
    public required int Id { get; init; }
    public required float X { get; init; }
    public required float Z { get; init; }
    public required float Value { get; init; }
}

public sealed class CardOffer
{
    public required IReadOnlyList<CardDef> Cards { get; init; }
    public float Age;
}

/// <summary>Kabarma gecesi: asamalar, dalgalar, Oz, seviye, kart secimi, Gosteri carpani (GDD 14, 15).</summary>
public sealed class KabarmaNight
{
    readonly GameData _data;
    readonly NightDef _def;
    readonly Rng _rng;
    readonly Dictionary<string, int> _ranks = new();
    readonly List<float> _recentKills = new();

    public NightPhase Phase { get; private set; } = NightPhase.Alarm;
    public float PhaseTime { get; private set; }
    public float TotalTime { get; private set; }
    public int WaveIndex { get; private set; } = -1;
    public int Ks => _def.Ks;
    public float Oz { get; private set; }
    public int Level { get; private set; }
    public int CardLevels { get; private set; }
    public float Gosteri { get; private set; }
    public float PeakGosteri { get; private set; }
    public int Kills { get; private set; }
    public float Maya { get; private set; }
    public bool Swelled { get; private set; }
    public CardOffer? Offer { get; private set; }
    public readonly Queue<int> PendingLevels = new();
    public CombatStats Stats { get; } = new();
    public IReadOnlyDictionary<string, int> Ranks => _ranks;
    public int SpawnedThisWave { get; private set; }

    public event Action? SwellNow, DawnNow;
    public event Action<int, WaveDef>? WaveStarted;
    public event Action<int>? LevelUp;
    public event Action<CardDef, int>? CardTaken;
    /// <summary>Oz toplandi. auto: dalga sonunda ya da safakta yerde kalan Oz oyuncuya uctu.</summary>
    public event Action<OzMote, bool>? OzCollected;

    readonly List<OzMote> _motes = new();
    int _nextMote;
    /// <summary>Yerde bekleyen Oz.</summary>
    public IReadOnlyList<OzMote> Motes => _motes;
    public float OzOnGround => _motes.Sum(m => m.Value);
    /// <summary>Toplama yaricapi: kabarma.json ozPickupRadius x Miknatis carpani.</summary>
    public float PickupRadius => Stats.PickupRadius(_data.Kabarma.OzPickupRadius);

    /// <param name="memberCard">Uye tipinin baslangic karti (GDD 23). Gece tanimi izin vermezse verilmez (GDD 24: ilk gece).</param>
    public KabarmaNight(GameData data, NightDef def, ulong seed, string? memberCard = null)
    {
        _data = data; _def = def; _rng = new Rng(Scramble(seed));
        Gosteri = data.Kabarma.Gosteri.Min;
        PeakGosteri = Gosteri;
        if (def.MemberStartCard && !string.IsNullOrEmpty(memberCard)) Grant(memberCard);
    }

    /// <summary>
    /// splitmix64: kucuk tohumla xorshift'in ilk ciktilari da kucuk olur ve kart teklifi hep havuzun basindan secer
    /// (yeniden oynanan gecede uc teklifin ucunde de Catal ilk siradaydi). Tohum burada karistirilir; Rng'nin kendisi ortak.
    /// </summary>
    static ulong Scramble(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public NightDef Def => _def;
    /// <summary>Bu gece acilan kart tekliflerinin sayisi (ilki GDD 24 kuralina tabi).</summary>
    public int OffersOpened { get; private set; }
    /// <summary>Safakta alinan kart listesi (son rutbeleriyle). Gece surerken bos.</summary>
    public IReadOnlyList<TakenCard> CardsAtDawn { get; private set; } = Array.Empty<TakenCard>();
    /// <summary>Isabet hasarlarinin toplami (Hasat Raporu).</summary>
    public float DamageDealt { get; private set; }
    /// <summary>Bu gece kabaran ekinler, boy sinifina gore (Hasat Raporu, GDD 8.4).</summary>
    public readonly Dictionary<string, int> SwollenBySize = new();
    /// <summary>Bu gece yenen ekinler (GDD 14.4: kaybedilir).</summary>
    public int CropsEaten { get; private set; }
    /// <summary>Bu gece oyuncunun yere dusme sayisi (GDD 3.6).</summary>
    public int PlayerDowns { get; private set; }
    /// <summary>Bu gece kabaran hayvanlar (GDD 9.5).</summary>
    public int AnimalsSwollen { get; private set; }

    public void RecordSwell(SizeClassDef size) => SwollenBySize[size.Id] = SwollenBySize.GetValueOrDefault(size.Id) + 1;
    public void RecordEaten() { if (Phase != NightPhase.Dawn) CropsEaten++; }
    public void RecordPlayerDown() { if (Phase != NightPhase.Dawn) PlayerDowns++; }
    public void RecordAnimalSwell(int n = 1) => AnimalsSwollen += n;

    public WaveDef? CurrentWave => WaveIndex >= 0 && WaveIndex < _def.Waves.Count ? _def.Waves[WaveIndex] : null;
    public float HpMult => MathF.Pow(_data.Kabarma.HpGrowth, Ks - 1);
    public float DamageMult => MathF.Pow(_data.Kabarma.DamageGrowth, Ks - 1);

    /// <summary>GDD 15: n. seviye esigi = 20 x n^1,5 (toplam Oz).</summary>
    public float Threshold(int n) => _data.Kabarma.LevelBase * MathF.Pow(n, _data.Kabarma.LevelExponent);

    /// <summary>Kac dusmanin su an dogmasi gerektigi: dalga suresine yayilir.</summary>
    public int SpawnDue()
    {
        var w = CurrentWave;
        if (Phase != NightPhase.Waves || w == null) return 0;
        float spread = Math.Max(1f, w.Seconds * 0.6f);
        int shouldHave = Math.Min(w.Count, 1 + (int)(PhaseTime / spread * w.Count));
        int due = shouldHave - SpawnedThisWave;
        SpawnedThisWave += Math.Max(0, due);
        return Math.Max(0, due);
    }

    public void Tick(float dt)
    {
        TotalTime += dt;
        PhaseTime += dt;
        var g = _data.Kabarma.Gosteri;
        Gosteri = Math.Max(g.Min, Gosteri - g.DecayPerSecond * dt);
        if (Offer != null) Offer.Age += dt;

        switch (Phase)
        {
            case NightPhase.Alarm:
                if (!Swelled && PhaseTime >= _def.AlarmSeconds - _def.SwellSeconds)
                {
                    Swelled = true;
                    SwellNow?.Invoke();
                }
                if (PhaseTime >= _def.AlarmSeconds) NextWave();
                break;
            case NightPhase.Waves:
                var w = CurrentWave!;
                if (PhaseTime >= w.Seconds + w.GapAfter) NextWave();
                break;
        }
    }

    /// <summary>Son dalga, tum dusmanlar olunce erken biter (oyuncuyu bekletmemek icin).</summary>
    public void WaveCleared()
    {
        if (Phase != NightPhase.Waves) return;
        var w = CurrentWave!;
        if (SpawnedThisWave < w.Count) return;
        CollectAll(); // M1 karari 19: dalga bitince yerde kalan Oz oyuncuya ucar
        if (WaveIndex == _def.Waves.Count - 1) EnterDawn();
        else if (PhaseTime < w.Seconds) PhaseTime = w.Seconds; // bir sonraki dalgaya kisa aradan sonra gec
    }

    void NextWave()
    {
        CollectAll(); // suresi dolan dalga da biter
        WaveIndex++;
        PhaseTime = 0;
        SpawnedThisWave = 0;
        if (WaveIndex >= _def.Waves.Count) { EnterDawn(); return; }
        Phase = NightPhase.Waves;
        WaveStarted?.Invoke(WaveIndex, _def.Waves[WaveIndex]);
    }

    void EnterDawn()
    {
        if (Phase == NightPhase.Dawn) return;
        CollectAll(); // safakta yerde Oz kalmaz (kayip yok)
        Phase = NightPhase.Dawn;
        PhaseTime = 0;
        Offer = null;
        PendingLevels.Clear();
        // GDD 15: safakta butun kartlar soner. Rapor icin once listesi alinir.
        CardsAtDawn = _ranks.Select(kv => new TakenCard(kv.Key, _data.Cards.Cards.FirstOrDefault(c => c.Id == kv.Key)?.Name ?? kv.Key, kv.Value))
                            .OrderByDescending(c => c.Rank).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
        _ranks.Clear();
        Stats.Reset();
        DawnNow?.Invoke();
    }

    /// <summary>
    /// Oldurme. Maya = agirlik x 2 x (1 + 0,15 x KS) x Gosteri, hemen sayilir. Oz = turun Oz degeri (agirlik) x Gosteri;
    /// oldurme aninda hesaplanir ama (x, z)'ye parcacik olarak duser ve toplaninca sayilir (URETIM 6 M1 karari 19).
    /// </summary>
    public OzMote OnKill(CreatureDef c, float x = 0f, float z = 0f)
    {
        Kills++;
        var g = _data.Kabarma.Gosteri;
        _recentKills.Add(TotalTime);
        _recentKills.RemoveAll(t => TotalTime - t > g.StreakWindow);
        if (_recentKills.Count >= g.StreakKills)
        {
            Gosteri = Math.Min(g.Max, Gosteri + g.StreakBonus);
            _recentKills.Clear();
        }
        PeakGosteri = Math.Max(PeakGosteri, Gosteri);

        Maya += c.Weight * _data.Kabarma.MayaPerWeight * (1f + _data.Kabarma.MayaKsFactor * Ks) * Gosteri;
        var mote = new OzMote { Id = ++_nextMote, X = x, Z = z, Value = c.Weight * _data.Kabarma.OzPerWeight * Gosteri };
        _motes.Add(mote);
        return mote;
    }

    /// <summary>Oyuncunun (x, z) konumundan toplama yaricapi icindeki Oz'u toplar. Toplanan sayisi.</summary>
    public int CollectNear(float x, float z)
    {
        if (_motes.Count == 0 || Phase == NightPhase.Dawn) return 0;
        float r2 = PickupRadius * PickupRadius;
        int n = 0;
        for (int i = _motes.Count - 1; i >= 0; i--)
        {
            var m = _motes[i];
            float dx = m.X - x, dz = m.Z - z;
            if (dx * dx + dz * dz > r2) continue;
            _motes.RemoveAt(i);
            Collect(m, false);
            n++;
        }
        return n;
    }

    /// <summary>Yerde kalan butun Oz oyuncuya ucar ve sayilir (dalga sonu, safak).</summary>
    public void CollectAll()
    {
        while (_motes.Count > 0)
        {
            var m = _motes[0];
            _motes.RemoveAt(0);
            Collect(m, true);
        }
    }

    void Collect(OzMote m, bool auto)
    {
        Oz += m.Value;
        OzCollected?.Invoke(m, auto);
        while (Level < _def.MaxCardLevels && Oz >= Threshold(Level + 1))
        {
            Level++;
            PendingLevels.Enqueue(Level);
            LevelUp?.Invoke(Level);
        }
        TryOpenOffer();
    }

    public void TryOpenOffer()
    {
        if (Offer != null || PendingLevels.Count == 0 || Phase == NightPhase.Dawn) return;
        PendingLevels.Dequeue();
        // GDD 24: ilk gecenin ilk teklifinde Catal her zaman var (ilk sirada: 20 sn'de secilmezse ilki alinir).
        string? forced = OffersOpened == 0 ? _def.FirstOfferCard : null;
        OffersOpened++;
        Offer = new CardOffer { Cards = RollOffer(forced) };
    }

    /// <summary>
    /// GDD 15: acik havuzdan 3 farkli kart, nadirlik agirligiyla; 5. rutbedeki kart gelmez.
    /// <paramref name="forced"/> verilirse (ve havuzdaysa) o kart ilk siraya konur, kalanlar agirlikla secilir.
    /// </summary>
    public List<CardDef> RollOffer(string? forced = null)
    {
        var pool = _data.Cards.Cards.Where(c => Rank(c.Id) < _data.Cards.MaxRank).ToList();
        var picked = new List<CardDef>();
        var must = string.IsNullOrEmpty(forced) ? null : pool.FirstOrDefault(c => c.Id == forced);
        if (must != null) { picked.Add(must); pool.Remove(must); }
        while (picked.Count < _data.Cards.OfferSize && pool.Count > 0)
        {
            float total = pool.Sum(c => (float)_data.Cards.RarityWeights.GetValueOrDefault(c.Rarity, 1));
            float r = _rng.NextFloat() * total;
            int i = 0;
            for (; i < pool.Count - 1; i++)
            {
                r -= _data.Cards.RarityWeights.GetValueOrDefault(pool[i].Rarity, 1);
                if (r < 0) break;
            }
            picked.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return picked;
    }

    public int Rank(string id) => _ranks.GetValueOrDefault(id);

    public bool ShouldAutoPick => Offer != null && Offer.Age >= _data.Cards.AutoPickSeconds;

    public CardDef? Choose(int index)
    {
        if (Offer == null || index < 0 || index >= Offer.Cards.Count) return null;
        var card = Offer.Cards[index];
        Offer = null;
        Take(card);
        TryOpenOffer();
        return card;
    }

    /// <summary>Teklif disindan bir karti bir rutbe verir (uye tipinin baslangic karti, sinamalar). Safaktan sonra islemez.</summary>
    public CardDef? Grant(string id)
    {
        if (Phase == NightPhase.Dawn) return null;
        var card = _data.Cards.Cards.FirstOrDefault(c => c.Id == id) ?? throw new InvalidDataException($"Bilinmeyen kart: {id}");
        if (Rank(id) >= _data.Cards.MaxRank) return null;
        Take(card);
        return card;
    }

    void Take(CardDef card)
    {
        _ranks[card.Id] = Rank(card.Id) + 1;
        CardsCount++;
        Apply(card);
        CardTaken?.Invoke(card, _ranks[card.Id]);
    }

    public int CardsCount { get; private set; }

    void Apply(CardDef c)
    {
        switch (c.Stat)
        {
            case "projectiles": Stats.Projectiles += (int)c.PerRank; break;
            case "bounces": Stats.Bounces += (int)c.PerRank; break;
            case "fireRate": Stats.FireRateMult += c.PerRank; break;
            case "size": Stats.SizeMult += c.PerRank; break;
            case "burnChance": Stats.BurnChance += c.PerRank; break;
            case "freezeChance": Stats.FreezeChance += c.PerRank; break;
            case "critChance": Stats.CritChance += c.PerRank; break;
            case "knockback": Stats.KnockbackMult += c.PerRank; break;
            case "pickupRange": Stats.PickupRangeMult += c.PerRank; break;
            case "moveSpeed": Stats.MoveSpeedMult += c.PerRank; break;
            default: throw new InvalidDataException($"Bilinmeyen kart etkisi: {c.Stat}");
        }
    }

    /// <summary>Tek isabetin hasari. Kritik ve zayif nokta carpanlari burada.</summary>
    public (float damage, bool crit) RollHit(WeaponDef w, bool weakPoint, float weakMult)
    {
        float dmg = Stats.Damage(w.Damage);
        bool crit = _rng.Chance(Stats.Crit(w.CritChance));
        if (crit) dmg *= w.CritMult;
        if (weakPoint) dmg *= weakMult;
        dmg = MathF.Round(dmg);
        if (Phase != NightPhase.Dawn) DamageDealt += dmg;
        return (dmg, crit);
    }

    public bool RollBurn() => _rng.Chance(Stats.BurnChance);
    public bool RollFreeze() => _rng.Chance(Stats.FreezeChance);
}
