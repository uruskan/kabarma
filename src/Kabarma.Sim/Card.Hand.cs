namespace Kabarma.Sim;

/// <summary>
/// Bir oyuncunun gecelik kart eli (GDD 15, 15.1): rutbeler, teklif, yenileme ve surgun, 12 yuva, asi, Nisan.
/// KabarmaNight bunu tutar ve dis yuzunu (Offer, Choose, Grant, Rank, Stats) buna devreder. Etkiler iki yerde:
/// M1'in 10 degeri CombatStats'ta (Capa ve Director onlari okur), geri kalanlar Mod(stat) sozlugunde.
/// Savas kurallari Card.Combat.cs, asi kurallari Asi.cs.
/// </summary>
public sealed partial class CardHand
{
    readonly GameData _d;
    readonly CardsFile _c;
    readonly Rng _rng;
    readonly CardBook? _book;
    readonly Dictionary<string, int> _ranks = new(StringComparer.Ordinal);
    readonly HashSet<string> _banished = new(StringComparer.Ordinal);
    readonly Dictionary<string, float> _mods = new(StringComparer.Ordinal);

    public CombatStats Stats { get; } = new();
    public CardOffer? Offer { get; private set; }
    public int RerollsLeft { get; private set; }
    public int BanishesLeft { get; private set; }
    public int CardsCount { get; private set; }
    public bool Faded { get; private set; }
    public IReadOnlyDictionary<string, int> Ranks => _ranks;
    public IReadOnlyCollection<string> Banished => _banished;

    public event Action<CardDef, int>? Taken;
    /// <summary>Borc gibi kartlarin hemen verdigi seviyeler (gecenin sinirina sayilmaz).</summary>
    public event Action<int>? LevelsGranted;

    /// <param name="book">Acik kartlar ve Nisan; null ise bastan acik 20 kart ve Nisan'siz.</param>
    public CardHand(GameData data, Rng rng, CardBook? book = null)
    {
        _d = data; _c = data.Cards; _rng = rng; _book = book;
        RerollsLeft = _c.Rerolls; BanishesLeft = _c.Banishes;
    }

    public CardBook? Book => _book;
    public int Rank(string id) => _ranks.GetValueOrDefault(id);
    public bool IsOpen(CardDef c) => _book?.IsUnlocked(c.Id) ?? (c.IsAsi ? c.Parents.All(p => _c.Find(p)?.Unlock == "start") : c.Unlock == "start");
    public int NisanLevel(string id) => _book?.Level(id) ?? 0;
    public float NisanBonus(string id) => _book?.Bonus(id) ?? 1f;
    public int Cap(CardDef c) => _c.RankCap(c);

    /// <summary>12 yuvadan dolu olanlar: her asi iki ebeveyninin yuvasini bir yuvaya indirir.</summary>
    public int SlotsUsed => _ranks.Count(kv => kv.Value > 0 && !_c.Card(kv.Key).IsAsi) - _ranks.Count(kv => kv.Value > 0 && _c.Card(kv.Key).IsAsi);

    /// <summary>Su an teklife girebilecek (asi olmayan) kartlar.</summary>
    public List<CardDef> Pool()
    {
        bool full = SlotsUsed >= _c.MaxSlots;
        return _c.Cards.Where(c => !c.IsAsi && IsOpen(c) && !_banished.Contains(c.Id) && Rank(c.Id) < Cap(c)
                                   && (!full || Rank(c.Id) > 0)).ToList();
    }

    /// <summary>
    /// 3 kartlik teklif: varsa sunulacak asi ilk sirada, sonra zorunlu kart (ilk gecenin Catal'i), kalanlar nadirlik
    /// agirligiyla. Havuz bossa bos liste.
    /// </summary>
    public List<CardDef> RollOffer(string? forced = null, IEnumerable<string>? exclude = null)
    {
        var pool = Pool();
        if (exclude != null) { var ex = exclude.ToHashSet(); pool.RemoveAll(c => ex.Contains(c.Id)); }
        var picked = new List<CardDef>();
        var asi = AsiReady().FirstOrDefault(a => exclude == null || !exclude.Contains(a.Id));
        if (asi != null) picked.Add(asi);
        var must = string.IsNullOrEmpty(forced) ? null : pool.FirstOrDefault(c => c.Id == forced);
        if (must != null && picked.Count < _c.OfferSize) { picked.Add(must); pool.Remove(must); }
        while (picked.Count < _c.OfferSize && pool.Count > 0) picked.Add(Draw(pool));
        return picked;
    }

    CardDef Draw(List<CardDef> pool)
    {
        float total = pool.Sum(c => (float)_c.RarityWeights.GetValueOrDefault(c.Rarity, 1));
        float r = _rng.NextFloat() * total;
        int i = 0;
        for (; i < pool.Count - 1; i++)
        {
            r -= _c.RarityWeights.GetValueOrDefault(pool[i].Rarity, 1);
            if (r < 0) break;
        }
        var c = pool[i];
        pool.RemoveAt(i);
        return c;
    }

    /// <summary>Yeni teklif acar. Teklif edilecek kart yoksa null (seviye bosa gider, GDD 15.1).</summary>
    public CardOffer? Open(string? forced = null)
    {
        if (Faded) return null;
        var cards = RollOffer(forced);
        Offer = cards.Count == 0 ? null : new CardOffer { Cards = cards };
        return Offer;
    }

    public void TickOffer(float dt) { if (Offer != null) Offer.Age += dt; }
    public bool ShouldAutoPick => Offer != null && Offer.Age >= _c.AutoPickSeconds;

    public CardDef? Choose(int index)
    {
        if (Offer == null || index < 0 || index >= Offer.Cards.Count) return null;
        var card = Offer.Cards[index];
        Offer = null;
        Take(card);
        return card;
    }

    /// <summary>Yenileme: teklifi yeniden ceker; asi kalir, zorunlu kart yeniden zorlanmaz, sure bastan baslar.</summary>
    public bool Reroll()
    {
        if (Offer == null || RerollsLeft <= 0) return false;
        RerollsLeft--;
        var asi = Offer.Cards.FirstOrDefault(c => c.IsAsi);
        var fresh = RollOffer(exclude: asi == null ? null : new[] { asi.Id }); // asi tekrar zorlanmasin diye disarida
        if (asi != null) { fresh.Insert(0, asi); while (fresh.Count > _c.OfferSize) fresh.RemoveAt(fresh.Count - 1); }
        Offer = new CardOffer { Cards = fresh };
        return true;
    }

    /// <summary>Surgun: teklifteki karti o gecelik havuzdan cikarir, yerine yeni kart ceker. Eldeki kart ve asi surgun edilmez.</summary>
    public bool Banish(int index)
    {
        if (Offer == null || BanishesLeft <= 0 || index < 0 || index >= Offer.Cards.Count) return false;
        var card = Offer.Cards[index];
        if (card.IsAsi || Rank(card.Id) > 0) return false;
        BanishesLeft--;
        _banished.Add(card.Id);
        var list = Offer.Cards.ToList();
        var pool = Pool();
        pool.RemoveAll(c => list.Any(o => o.Id == c.Id));
        if (pool.Count > 0) list[index] = Draw(pool); else list.RemoveAt(index);
        float age = Offer.Age;
        Offer = list.Count == 0 ? null : new CardOffer { Cards = list, Age = age };
        return true;
    }

    /// <summary>Teklif disindan bir rutbe (uye karti, sinama). Tavandaysa ya da safaktan sonra null.</summary>
    public CardDef? Grant(string id)
    {
        if (Faded) return null;
        var card = _c.Card(id);
        if (Rank(id) >= Cap(card)) return null;
        Take(card);
        return card;
    }

    void Take(CardDef card)
    {
        int r = Rank(card.Id);
        int next = r + 1;
        // Nisan 5: kart geceye 2. rutbeden baslar (GDD 15).
        if (r == 0 && !card.IsAsi && NisanLevel(card.Id) >= _c.Nisan.Thresholds.Count && _c.Nisan.Thresholds.Count > 0)
            next = Math.Max(next, _c.Nisan.StartRankAtMax);
        next = Math.Min(next, Cap(card));
        _ranks[card.Id] = next;
        CardsCount++;
        Recompute();
        if (card.Stat == "debtLevels" && r == 0) LevelsGranted?.Invoke((int)card.PerRank);
        Taken?.Invoke(card, next);
    }

    /// <summary>Kartin bir etkisinin bu rutbedeki degeri (Nisan dahil).</summary>
    public float EffectValue(CardDef c, CardEffect e, int rank)
    {
        if (rank <= 0) return 0f;
        float v = e.Once ? e.PerRank : e.PerRank * rank;
        if (!CardsFile.CountStats.Contains(e.Stat)) v *= NisanBonus(c.Id);
        return v;
    }

    /// <summary>Butun etkileri rutbelerden yeniden kurar.</summary>
    void Recompute()
    {
        Stats.Reset();
        _mods.Clear();
        foreach (var (id, rank) in _ranks)
        {
            if (rank <= 0) continue;
            var c = _c.Card(id);
            foreach (var e in c.AllEffects())
            {
                float v = EffectValue(c, e, rank);
                if (!ApplyCore(e.Stat, v)) _mods[e.Stat] = _mods.GetValueOrDefault(e.Stat) + v;
            }
        }
        RebuildOrbitals();
    }

    /// <summary>M1'in 10 degeri CombatStats'a gider (Capa ve Director okur).</summary>
    bool ApplyCore(string stat, float v)
    {
        switch (stat)
        {
            case "projectiles": Stats.Projectiles += (int)MathF.Round(v); return true;
            case "bounces": Stats.Bounces += (int)MathF.Round(v); return true;
            case "fireRate": Stats.FireRateMult += v; return true;
            case "size": Stats.SizeMult += v; return true;
            case "burnChance": Stats.BurnChance += v; return true;
            case "freezeChance": Stats.FreezeChance += v; return true;
            case "critChance": Stats.CritChance += v; return true;
            case "knockback": Stats.KnockbackMult += v; return true;
            case "pickupRange": Stats.PickupRangeMult += v; return true;
            case "moveSpeed": Stats.MoveSpeedMult += v; return true;
            default: return false;
        }
    }

    /// <summary>Kart degistiricisi (Mod adlari cards.json stat'lari). Kart yoksa 0.</summary>
    public float Mod(string stat) => _mods.GetValueOrDefault(stat);
    public bool Has(string stat) => _mods.GetValueOrDefault(stat) > 0f;
    public IReadOnlyDictionary<string, float> Mods => _mods;

    /// <summary>Kumarbaz (GDD 15.1): her dalga basinda rutbesi kadar kez: biri +1, digeri -1.</summary>
    public (string? up, string? down) OnWaveStart()
    {
        int times = (int)MathF.Round(Mod("gamble"));
        string? lastUp = null, lastDown = null;
        for (int k = 0; k < times; k++)
        {
            var ups = _ranks.Where(kv => kv.Value > 0 && kv.Key != "kumarbaz" && !_c.Card(kv.Key).IsAsi && kv.Value < Cap(_c.Card(kv.Key)))
                            .Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (ups.Count == 0) break;
            var up = ups[_rng.Range(0, ups.Count)];
            var downs = _ranks.Where(kv => kv.Value > 1 && kv.Key != "kumarbaz" && kv.Key != up && !_c.Card(kv.Key).IsAsi)
                              .Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
            _ranks[up]++;
            lastUp = up;
            if (downs.Count > 0) { var down = downs[_rng.Range(0, downs.Count)]; _ranks[down]--; lastDown = down; }
        }
        if (lastUp != null) Recompute();
        return (lastUp, lastDown);
    }

    /// <summary>
    /// Safak (GDD 15): kartlar soner. Tavandaki kartlar defterden Nisan puani alir. Donus: son rutbeler (rapor icin) ve
    /// Kooperatif'e gidecek Maya payi (Borc).
    /// </summary>
    public (List<TakenCard> cards, float debtMaya) Dawn()
    {
        var list = _ranks.Where(kv => kv.Value > 0)
                         .Select(kv => new TakenCard(kv.Key, _c.Find(kv.Key)?.Name ?? kv.Key, kv.Value))
                         .OrderByDescending(c => c.Rank).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
        _book?.AwardNight(_ranks);
        float debt = Mod("debtMaya");
        _ranks.Clear();
        _banished.Clear();
        Offer = null;
        Faded = true;
        KillStacks = 0;
        Recompute();
        return (list, debt);
    }
}
