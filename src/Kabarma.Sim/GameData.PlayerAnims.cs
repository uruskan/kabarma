namespace Kabarma.Sim;

// Oyuncu klipleri (SANAT-SES §3.1, URETIM §6 M1 karari 10): player_anims.json.
// Ayni dosyayi pipeline/merge_player.py de okur (teslim edilen animasyon adi -> klip kimligi).

/// <summary>Bir oyuncu klibi: nereden geldigi, yer tutucu mu, yoksa yerine ne oynar.</summary>
public sealed class PlayerClipDef
{
    public string Id { get; set; } = "";
    /// <summary>M1'in 30 klibinden biri mi (kalanlar odenmis ekler ya da yer tutucu kod pozlari).</summary>
    public bool M1 { get; set; }
    public string Group { get; set; } = "";
    /// <summary>Hareket yonu (f, b, l, r, fl, fr, bl, br); yonsuz kliplerde bos.</summary>
    public string Dir { get; set; } = "";
    /// <summary>library, locomotion, cut, code.</summary>
    public string Source { get; set; } = "";
    /// <summary>Teslim edilen GLB'deki animasyon adi (library, locomotion).</summary>
    public string Action { get; set; } = "";
    public bool Loop { get; set; }
    /// <summary>Oynatma hizi carpani (1 = oldugu gibi).</summary>
    public float Speed { get; set; } = 1f;
    /// <summary>Klibin adim boyuna uyan yurume hizi (m/sn); oynatma hizi govde hiziyla bu orana gore olceklenir.</summary>
    public float RefSpeed { get; set; }
    /// <summary>Kalcanin yatay kaymasi silinir (govdeyi oyun kodu tasir).</summary>
    public bool InPlace { get; set; }
    /// <summary>Henuz uretilmedi: oyun Fallback klibini oynar (URETIM §3 yer tutucu isareti).</summary>
    public bool Placeholder { get; set; }
    public string Fallback { get; set; } = "";
    public float FallbackSpeed { get; set; } = 1f;
    /// <summary>Uretim istegi notu (kutuphane kimligi ya da komut).</summary>
    public string Request { get; set; } = "";
    /// <summary>cut: kaynak klip ve zaman araligi (sn).</summary>
    public string From { get; set; } = "";
    public float Start { get; set; }
    public float End { get; set; }
    /// <summary>cut: kalca yuksekligi sabitlenir (ziplamada yuksekligi fizik verir).</summary>
    public bool FlattenHips { get; set; }
    /// <summary>code: tam govde klibi degil, yurume ustune katman (PlayerBody).</summary>
    public bool Layer { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Kodla animasyon katmanlarinin ayarlari (SANAT-SES §3.1 "Kodla" ve "Fizik" satirlari). Acilar derece.</summary>
public sealed class PlayerRigDef
{
    // Ayak IK'si: ayagin altindan isin; govde zemine gore en cok MaxDrop iner, ayak en cok MaxRise kalkar.
    public float FootRayUp { get; set; } = 0.5f;
    public float FootRayDown { get; set; } = 0.6f;
    public float FootMaxDrop { get; set; } = 0.3f;
    public float FootMaxRise { get; set; } = 0.3f;
    public float FootBlend { get; set; } = 10f;
    // Govde donusu: bakis yonu ile govde arasindaki yatay aci omurgaya paylastirilir (gerisi bas).
    public float TwistMaxDeg { get; set; } = 55f;
    public List<float> TwistShare { get; set; } = new();
    public float PitchShare { get; set; } = 0.3f;
    // Kosarken egilme ve donuste yana yatma.
    public float LeanRunDeg { get; set; } = 6f;
    public float LeanSprintDeg { get; set; } = 12f;
    public float LeanAccelDeg { get; set; } = 1.2f;
    public float BankMaxDeg { get; set; } = 8f;
    public float BankGain { get; set; } = 1.5f;
    public float RecoilSpineDeg { get; set; } = 7f;
    // Canta yayi: x'' = -k (x - hedef) - c x'; hedef = ivme x kazanc.
    public float BagStiffness { get; set; } = 60f;
    public float BagDamping { get; set; } = 6f;
    public float BagGain { get; set; } = 0.02f;
    public float BagMaxPitch { get; set; } = 0.35f;
    public float BagMaxRoll { get; set; } = 0.25f;
    // Ragdoll ve kalkis.
    public float DownSeconds { get; set; } = 3f;
    public float KnockdownSeconds { get; set; } = 5f;
    public float GetupBlend { get; set; } = 0.4f;
    // Katmanlar: el arabasi itme ve suda yurume agirligi.
    public float PushWeight { get; set; } = 1f;
    public float WadeWeight { get; set; } = 0.85f;
    // Ziplama: bu sureden uzun havada kalinca dusme klibi.
    public float FallAfter { get; set; } = 0.7f;
}

public sealed class PlayerAnimsFile
{
    public string Model { get; set; } = "";
    public PlayerRigDef Rig { get; set; } = new();
    public List<PlayerClipDef> Clips { get; set; } = new();

    public PlayerClipDef? Clip(string id) => Clips.FirstOrDefault(c => c.Id == id);

    /// <summary>Oynatilacak klip: yer tutucuysa yedegine iner (zincir), hiz carpanlari carpilir.</summary>
    public (string Id, float Speed) Resolve(string id)
    {
        float speed = 1f;
        var seen = new HashSet<string>();
        var c = Clip(id);
        while (c != null && c.Placeholder && c.Fallback.Length > 0 && seen.Add(c.Id))
        {
            speed *= c.FallbackSpeed;
            id = c.Fallback;
            c = Clip(id);
        }
        return (id, speed);
    }
}

public sealed partial class GameData
{
    [DataFile("player_anims.json")]
    public PlayerAnimsFile PlayerAnims { get; private set; } = new();
}
