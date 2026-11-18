namespace Kabarma.Sim;

/// <summary>
/// Tam Kabarma (kabarma3, GDD 14.1-14.6): plandan kurulan gece. Ilk gecenin akisi (asamalar, Oz, kartlar) aynen kullanilir;
/// burada plan kuyrugu (220 siniri ve tasma), Doruk, Maya Kazani, bitis nedeni, Gosteri olaylari ve oldurenin kaydi var.
/// </summary>
public sealed partial class KabarmaNight
{
    readonly List<(WaveSlot slot, int wave)> _queue = new();
    readonly Dictionary<int, int> _killsBy = new();
    int _nextKabaran;

    /// <summary>Tam/kucuk/tatbikat gecesinin plani; ilk gecede null.</summary>
    public WavePlan? Plan { get; private set; }
    public KabarmaKind Kind => Plan?.Spec.Kind ?? KabarmaKind.First;
    public KabarmaEnd EndReason { get; private set; }
    public KazanState? Kazan { get; private set; }
    /// <summary>Maya carpani: ekinsiz gece yarim, tatbikat sifir (GDD 14.3, 14.7).</summary>
    public float MayaMult => Plan?.MayaMult ?? 1f;
    /// <summary>Henuz sahneye girmemis (sirada bekleyen) girisler.</summary>
    public int PendingSpawns => _queue.Count;
    /// <summary>Doruk dalgasi suruyor mu.</summary>
    public bool InDoruk => Plan != null && Phase == NightPhase.Waves && WaveIndex >= 0 && WaveIndex < Plan.Waves.Count && Plan.Waves[WaveIndex].Doruk;
    public IReadOnlyDictionary<int, int> KillsBy => _killsBy;

    /// <summary>Doruk basladi (Azman yeri varsa slot.Azman; savas akisinin Azman'i baglanir).</summary>
    public event Action<PlannedWave>? DorukStarted;
    /// <summary>Gece bitti: nedeniyle (safak ya da Kazan devrildi).</summary>
    public event Action<KabarmaEnd>? Ended;

    /// <summary>Plandan gece: KabarmaNight(data, Waves.Compose(data, spec), seed).</summary>
    public KabarmaNight(GameData data, WavePlan plan, ulong seed, string? memberCard = null)
        : this(data, Waves.ToNightDef(plan, data.Kabarma.FirstNight), seed, memberCard)
    {
        Plan = plan;
        if (plan.Spec.Kind != KabarmaKind.Small)
        {
            float hp = Waves.KazanHp(data.Kabarma.Full, plan.Spec.OcakTier);
            Kazan = new KazanState { Hp = hp, MaxHp = hp };
        }
    }

    partial void OnWaveBegin(int index);
    partial void OnWaveBegin(int index)
    {
        if (Plan == null || index >= Plan.Waves.Count) return;
        var w = Plan.Waves[index];
        foreach (var s in w.Slots) _queue.Add((s, index)); // onceki dalgadan tasanlar onde kalir
        if (w.Doruk) DorukStarted?.Invoke(w);
    }

    partial void OnDawnBegin();
    partial void OnDawnBegin()
    {
        _queue.Clear(); // GDD 14.3: safakta girmemis olanlar duser
        if (EndReason == KabarmaEnd.None) EndReason = KabarmaEnd.Dawn;
        if (Kazan != null && _data.Kabarma.Full.Kazan.RepairAtDawn) { Kazan.Hp = Kazan.MaxHp; }
        Ended?.Invoke(EndReason);
    }

    /// <summary>
    /// Su an sahneye girmesi gereken girisler: dalga suresinin %60'ina yayilir, sahnedeki sayi MaxActive'i asmaz;
    /// sigmayan sirada kalir ve sonraki dalgaya devreder (GDD 14.3 tasma). Azman yeri de doner (Azman = true).
    /// </summary>
    public List<(WaveSlot slot, int wave)> TakeDue(int active)
    {
        var res = new List<(WaveSlot, int)>();
        var w = CurrentWave;
        if (Plan == null || Phase != NightPhase.Waves || w == null) return res;
        float spread = Math.Max(1f, w.Seconds * 0.6f);
        int planned = Plan.Waves[WaveIndex].Slots.Count;
        int shouldHave = Math.Min(planned, 1 + (int)(PhaseTime / spread * planned));
        int room = Math.Max(0, Plan.MaxActive - active);
        // Bu dalganin hedefi + onceki dalgalardan tasanlar (kuyrugun basi).
        int carried = _queue.Count(q => q.wave < WaveIndex);
        int due = Math.Min(room, Math.Min(_queue.Count, carried + Math.Max(0, shouldHave - SpawnedThisWave)));
        for (int i = 0; i < due; i++)
        {
            var q = _queue[0];
            _queue.RemoveAt(0);
            res.Add(q);
            if (q.wave == WaveIndex) SpawnedThisWave++;
        }
        return res;
    }

    /// <summary>Kuyruktan cikan giris icin Kabaran durumu (kimlik gece icinde artar).</summary>
    public KabaranState NewKabaran(WaveSlot slot, int wave, float x, float z) =>
        Waves.NewKabaran(_data, Plan ?? throw new InvalidOperationException("Plansiz gece"), slot, ++_nextKabaran, wave, x, z);

    /// <summary>Oldurmeyi oyuncuya yazar (ortak oyunda Oz oyuncu basina, GDD 15). OnKill'den sonra cagrilir.</summary>
    public void CreditKill(int player) => _killsBy[player] = _killsBy.GetValueOrDefault(player) + 1;

    /// <summary>GDD 14.5: Gosteri'yi artiran olay (savunma: cevreyle oldurme, savas-hayvan: hayvan oldurmesi, ...).</summary>
    public void ReportGosteri(GosteriEvent e)
    {
        if (Phase == NightPhase.Dawn) return;
        var g = _data.Kabarma.Gosteri;
        float bonus = g.Events.GetValueOrDefault(e.ToString(), 0f);
        Gosteri = Math.Min(g.Max, Gosteri + bonus);
        PeakGosteri = Math.Max(PeakGosteri, Gosteri);
    }

    /// <summary>Kazan'a hasar (GDD 14.4). Devrilirse gece o an biter, gecenin Maya'sinin yarisi gider. Devrildiyse true.</summary>
    public bool DamageKazan(float dmg)
    {
        if (Kazan == null || Kazan.Toppled || Phase == NightPhase.Dawn) return false;
        Kazan.Hp = Math.Max(0f, Kazan.Hp - dmg);
        if (Kazan.Hp > 0) return false;
        Kazan.Toppled = true;
        Maya *= 1f - _data.Kabarma.Full.Kazan.ToppleMayaLoss;
        EndReason = KabarmaEnd.KazanToppled;
        EnterDawn();
        return true;
    }

    /// <summary>Bozgun (GDD 14.4): butun oyuncular ayni anda yerde; Kazan %25 hasar alir.</summary>
    public bool Rout() => Kazan != null && DamageKazan(Kazan.MaxHp * _data.Kabarma.Full.Kazan.RoutDamage);

    /// <summary>Safagi bekleyen son dalga ile sahnede kimse kalmayinca (Director cagirir).</summary>
    public bool AllSpawned => Plan == null || (_queue.Count == 0 && WaveIndex >= Plan.Waves.Count - 1);
}
