namespace Kabarma.Sim;

// Atalar (GDD §18, §18.1): duz, serilestirilebilir durum (TEKNIK §9). Godot tipi, dugum ya da olay aboneligi yok;
// Ata kimligiyle baslanir. Kurallar AtaRules.cs'de.

/// <summary>Bir Ata dovusunun anlik durumu. Godot katmani yalniz okur ve AtaRules ile degistirir.</summary>
public sealed class AtaFight
{
    public string AtaId { get; set; } = "";
    public float Hp { get; set; }
    public float MaxHp { get; set; }
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
    /// <summary>Toplam alinan hasar, kaynaga gore (sinama ve rapor).</summary>
    public Dictionary<string, float> DamageBySource { get; set; } = new();
}

/// <summary>Bir Ata'nin kalici kaydi: adak ilerlemesi, yenilme sayisi.</summary>
public sealed class AtaRecord
{
    public string AtaId { get; set; } = "";
    /// <summary>Sunaga konmus adak (esya kimligi -> adet).</summary>
    public Dictionary<string, int> Offered { get; set; } = new();
    public int Defeats { get; set; }
}

/// <summary>Atalarin kalici ilerlemesi (GameState.Atalar): kayitlar, muhurler, ultiler, acilanlar, suren dovus.</summary>
public sealed class AtaProgress
{
    public List<AtaRecord> Records { get; set; } = new();
    public List<string> Seals { get; set; } = new();
    public List<string> Ultis { get; set; } = new();
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
            if (d != null && d.Effect == effect) m *= d.Mult;
        }
        return m;
    }
}

/// <summary>Dovusun bir karede Godot katmanina soyledigi (saldiri, yardimci, asama, yenilgi).</summary>
public enum AtaCue { Telegraph, Attack, SpawnAdds, PhaseChanged, Stunned, WeakPointBroken, Defeated }

public readonly record struct AtaCommand(AtaCue Cue, string Attack = "", int Count = 0, int Phase = 0);
