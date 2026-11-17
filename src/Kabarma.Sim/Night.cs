namespace Kabarma.Sim;

public enum NightPhase { Alarm, Waves, Dawn }

/// <summary>Kartlarin toplam etkisi; silah ve beden bu degerlerle calisir (ICERIK 8.1).</summary>
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

    public KabarmaNight(GameData data, NightDef def, ulong seed)
    {
        _data = data; _def = def; _rng = new Rng(seed);
        Gosteri = data.Kabarma.Gosteri.Min;
        PeakGosteri = Gosteri;
    }

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
        if (WaveIndex == _def.Waves.Count - 1) EnterDawn();
        else if (PhaseTime < w.Seconds) PhaseTime = w.Seconds; // bir sonraki dalgaya kisa aradan sonra gec
    }

    void NextWave()
    {
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
        Phase = NightPhase.Dawn;
        PhaseTime = 0;
        Offer = null;
        PendingLevels.Clear();
        DawnNow?.Invoke();
    }

    /// <summary>Oz = turun Oz degeri (agirlik) x Gosteri. Maya = agirlik x 2 x (1 + 0,15 x KS) x Gosteri.</summary>
    public void OnKill(CreatureDef c)
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

        Oz += c.Weight * _data.Kabarma.OzPerWeight * Gosteri;
        Maya += c.Weight * _data.Kabarma.MayaPerWeight * (1f + _data.Kabarma.MayaKsFactor * Ks) * Gosteri;

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
        Offer = new CardOffer { Cards = RollOffer() };
    }

    /// <summary>GDD 15: acik havuzdan 3 farkli kart, nadirlik agirligiyla; 5. rutbedeki kart gelmez.</summary>
    public List<CardDef> RollOffer()
    {
        var pool = _data.Cards.Cards.Where(c => Rank(c.Id) < _data.Cards.MaxRank).ToList();
        var picked = new List<CardDef>();
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
        _ranks[card.Id] = Rank(card.Id) + 1;
        CardsCount++;
        Apply(card);
        CardTaken?.Invoke(card, _ranks[card.Id]);
        TryOpenOffer();
        return card;
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
        float dmg = w.Damage * Stats.SizeMult;
        bool crit = _rng.Chance(w.CritChance + Stats.CritChance);
        if (crit) dmg *= w.CritMult;
        if (weakPoint) dmg *= weakMult;
        return (MathF.Round(dmg), crit);
    }

    public bool RollBurn() => _rng.Chance(Stats.BurnChance);
    public bool RollFreeze() => _rng.Chance(Stats.FreezeChance);
}
