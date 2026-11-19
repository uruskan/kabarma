namespace Kabarma.Sim;

// Atalar (GDD §18, §18.1): duz, serilestirilebilir durum (TEKNIK §9). Godot tipi, dugum ya da olay aboneligi yok;
// Ata kimligiyle baslanir. Kurallar AtaRules.cs'de.

/// <summary>Bir Ata dovusunun anlik durumu. Godot katmani yalniz okur ve AtaRules ile degistirir.</summary>
public sealed class AtaFight
{
    public string AtaId { get; set; } = "";
    /// <summary>Can Body'dedir (savas sozlesmesi); bu ikisi eski kayitlar ve okunurluk icin ona yonlenir.</summary>
    public float Hp { get => Body.Hp; set => Body.Hp = value; }
    public float MaxHp { get => Body.MaxHp; set => Body.MaxHp = value; }
    /// <summary>0, 1, 2: AtaDef.Phases sirasi.</summary>
    public int Phase { get; set; }
    /// <summary>Zirh (0..1): silah hasarindan dusen pay (Koca Pofuduk'un yunu).</summary>
    public float Armor { get; set; }
    /// <summary>Kalan sersemleme (sn).</summary>
    public float StunT { get; set; }
    /// <summary>Hasar almama (inis, asama gecisi) (sn).</summary>
    public float ShieldT { get; set; }
    /// <summary>Sonraki saldiriya kalan (sn); saldiri sirasi AttackIndex.</summary>
    public float AttackT { get; set; }
    public int AttackIndex { get; set; }
    /// <summary>Sezdirme suruyor: kalan sure ve hangi saldiri.</summary>
    public float TelegraphT { get; set; }
    public string Pending { get; set; } = "";
    public float AddT { get; set; }
    /// <summary>Godot katmaninin bildirdigi canli yardimci sayisi.</summary>
    public int AddsAlive { get; set; }
    /// <summary>Kalan zayif nokta canlari (Dede'nin kovanlari).</summary>
    public List<float> WeakPoints { get; set; } = new();
    /// <summary>Zayif noktalarin acik kalacagi sure (sn); AtaPhaseDef.WeakWindow 0 ise kullanilmaz.</summary>
    public float WeakOpenT { get; set; }
    /// <summary>Yerde, alinmamis boynuz parcasi sayisi (bilgi).</summary>
    public int ShardsDropped { get; set; }
    public int TeamSize { get; set; } = 1;
    public float Time { get; set; }
    public bool Defeated { get; set; }
    /// <summary>Kabarik Ata dovusu (M4): KS ve oyuncuya verilen hasar carpani. AtaId yine temel Ata'dir.</summary>
    public bool Kabarik { get; set; }
    public int Ks { get; set; }
    public float DamageMult { get; set; } = 1f;
    /// <summary>Yutulmus oyuncu icerde kalan sure (sn) ve Ata'nin suyun altinda kalan suresi (sn): M4.</summary>
    public float SwallowT { get; set; }
    /// <summary>Konum ve yon (Godot katmani host'ta her kare yazar; ortak oyunda istemcideki kukla bundan yurur).</summary>
    public float X { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float SubmergedT { get; set; }
    /// <summary>Toplam alinan hasar, kaynaga gore (sinama ve rapor).</summary>
    public Dictionary<string, float> DamageBySource { get; set; } = new();
    /// <summary>
    /// Savas govdesi (GDD §16.1, Combat.cs): can, durumlar (yanik, donma, sok, zehir), Kabarik. Zirh puani her silah
    /// isabetinde Armor payindan kurulur; kalkan ve sersemleme Ata'nin kendi alanlarindadir. Hp/MaxHp'den sonra
    /// yazilir: eski kayitta yoksa Hp'nin kurdugu govde kalir. Eklenen alan, surum degismez.
    /// </summary>
    public CombatBody Body { get; set; } = new() { Team = CombatTeam.Wild };
}

/// <summary>Bir Ata'nin kalici kaydi: adak ilerlemesi, yenilme sayisi.</summary>
public sealed class AtaRecord
{
    public string AtaId { get; set; } = "";
    /// <summary>Sunaga konmus adak (esya kimligi -> adet).</summary>
    public Dictionary<string, int> Offered { get; set; } = new();
    public int Defeats { get; set; }
    /// <summary>Guc isteyen adak (Sismik Davul): su ana dek biriken beslenmis sure (sn).</summary>
    public float PowerT { get; set; }
    /// <summary>Kabarik Ata (kimlik kabarik_&lt;ata&gt;): en yuksek yenilen KS ve yenilen KS'ler.</summary>
    public int BestKs { get; set; }
    public List<int> KsBeaten { get; set; } = new();
}

/// <summary>Atalarin kalici ilerlemesi (GameState.Atalar): kayitlar, muhurler, ultiler, acilanlar, suren dovus.</summary>
public sealed class AtaProgress
{
    public List<AtaRecord> Records { get; set; } = new();
    public List<string> Seals { get; set; } = new();
    public List<string> Ultis { get; set; } = new();
    /// <summary>GECICI (ata4): tanimi henuz abilities.json'da olmayan ultiler (AtaRewardDef.PendingUlti).</summary>
    public List<string> UltisOwed { get; set; } = new();
    public List<string> Unlocks { get; set; } = new();
    /// <summary>Suren dovus; yoksa null.</summary>
    public AtaFight? Active { get; set; }

    public AtaRecord Record(string id)
    {
        var r = Records.FirstOrDefault(x => x.AtaId == id);
        if (r == null) { r = new AtaRecord { AtaId = id }; Records.Add(r); }
        return r;
    }

    public bool IsDefeated(string id) => Records.Any(r => r.AtaId == id && r.Defeats > 0);
    public bool IsUnlocked(string key) => Unlocks.Contains(key);
    public bool HasSeal(string id) => Seals.Contains(id);

    /// <summary>
    /// Muhur carpani: etkisi `effect` olan alinmis muhurlerin carpimi (yoksa 1). Ornek: tarim "bereket_tuketimi"
    /// (Bereketli Toprak 0,75), yapi "yapi_cani" (Derin Kok 1,2).
    /// </summary>
    public float SealMult(AtalarFile data, string effect)
    {
        float m = 1f;
        foreach (var s in Seals)
        {
            var d = data.SealOf(s);
            if (d == null) continue;
            if (d.Effect == effect) m *= d.Mult;
            foreach (var e in d.Also) if (e.Effect == effect) m *= e.Mult;
        }
        return m;
    }

    /// <summary>Toplamsal muhur etkisi (Eksi Maya "boy_puani" +2); yoksa 0.</summary>
    public float SealAdd(AtalarFile data, string effect)
    {
        float a = 0f;
        foreach (var s in Seals)
            foreach (var e in data.SealOf(s)?.Also ?? new()) if (e.Effect == effect) a += e.Add;
        return a;
    }
}

/// <summary>Dovusun bir karede Godot katmanina soyledigi (saldiri, yardimci, asama, yenilgi).</summary>
public enum AtaCue { Telegraph, Attack, SpawnAdds, PhaseChanged, Stunned, WeakPointBroken, Defeated }

public readonly record struct AtaCommand(AtaCue Cue, string Attack = "", int Count = 0, int Phase = 0);
