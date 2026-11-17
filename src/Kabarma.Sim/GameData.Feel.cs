using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kabarma.Sim;

// Vurus hissi: feel.json (GDD 4 "Vurus hissi olcutleri", SANAT-SES §7 Vurus, §8.3 sürgüler).
// F1 paneli bu dosyanin butun sayilarini gosterir. Oyuncunun ayari ayri dosyada durur (user://feel_tuning.json)
// ve Overlay ile bunun ustune biner; feel.json'un kendisi oyundan degismez.

/// <summary>His kabul olcutleri (GDD 4, URETIM 6 M1 kabul, TEKNIK 11). F1'de salt okunur.</summary>
public sealed class FeelCriteria
{
    public float MaxLatencyMs { get; set; }
    public float HitStopMinMs { get; set; }
    public float HitStopMaxMs { get; set; }
    /// <summary>Gecikme sinamasinin kare hizi (basliksiz kosuda Engine.MaxFps).</summary>
    public float TargetFps { get; set; }
    /// <summary>Basliksiz kosuda cizim ve ekrana basma icin eklenen kare sayisi (kestirim).</summary>
    public float PresentFrames { get; set; }
}

/// <summary>Ana carpanlar: erisilebilirlik sürgüleri (SANAT-SES 8.3) ve katman basina ac-kapa.</summary>
public sealed class FeelMaster
{
    public float HitStop { get; set; } = 1f;
    public float Shake { get; set; } = 1f;
    public float Recoil { get; set; } = 1f;
    public float DamageNumbers { get; set; } = 1f;
    public float Squash { get; set; } = 1f;
    public float Debris { get; set; } = 1f;
    public float ScreenFlash { get; set; } = 1f;
    public float Sound { get; set; } = 1f;
}

public sealed class HitStopFeel
{
    /// <summary>Duraklama boyunca Engine.TimeScale.</summary>
    public float TimeScale { get; set; }
}

public sealed class ShakeFeel
{
    public float DecayPerSecond { get; set; }
    public float Offset { get; set; }
}

/// <summary>Geri tepme yayi: x'' = -k x - c x' (Player.Feel.cs).</summary>
public sealed class RecoilFeel
{
    public float Impulse { get; set; }
    public float Stiffness { get; set; }
    public float Damping { get; set; }
    public float PushBack { get; set; }
    public float Lift { get; set; }
    public float Tilt { get; set; }
}

/// <summary>Hasar sayilari: kritik buyuk ve sari (SANAT-SES 7).</summary>
public sealed class DamageNumberFeel
{
    public float Size { get; set; }
    public float CritSize { get; set; }
    public float Outline { get; set; }
    public float CritOutline { get; set; }
    public float PixelSize { get; set; }
    public float Jitter { get; set; }
    public float Lift { get; set; }
    public float Rise { get; set; }
    public float RiseSeconds { get; set; }
    public float PopFrom { get; set; }
    public float PopSeconds { get; set; }
    public float CritScale { get; set; }
    public float FadeDelay { get; set; }
    public float FadeSeconds { get; set; }
}

/// <summary>Vurulan hedefte ezilip esneme (GDD 4): sönümlü yay.</summary>
public sealed class SquashFeel
{
    public float Amount { get; set; }
    public float Seconds { get; set; }
    public float Wobbles { get; set; }
}

public sealed class ScreenFlashFeel
{
    public float Alpha { get; set; }
    public float Seconds { get; set; }
}

/// <summary>
/// Bir his kaynagi (silah, baslik ya da baslik olayi): atis, isabet ve parca katmanlari. Kullanilmayan alan 0 kalir.
/// Ses alanlari ses kimligidir (game/assets/audio/sfx/AD.mp3); bos ses calmaz.
/// </summary>
public sealed class FeelSource
{
    public static readonly FeelSource None = new();

    // Atis (namlu)
    public float FireShake { get; set; }
    public float FireRecoil { get; set; }
    public float Muzzle { get; set; }
    public float MuzzleSpeed { get; set; }
    public float MuzzleSize { get; set; }
    public float MuzzleLife { get; set; }
    public float Trail { get; set; }
    public string FireSfx { get; set; } = "";
    public float FireDb { get; set; }

    // Isabet
    public float HitStopMs { get; set; }
    public float CritHitStopMs { get; set; }
    public float HitShake { get; set; }
    public float CritHitShake { get; set; }
    public float HitRecoil { get; set; }
    public float Squash { get; set; }
    public float CritFlash { get; set; }
    public string HitSfx { get; set; } = "";
    public float HitDb { get; set; }
    public string CritSfx { get; set; } = "";
    public float CritDb { get; set; }
    public string WeakSfx { get; set; } = "";
    public float WeakDb { get; set; }
    /// <summary>Isabet sesinin calma olasiligi (surekli basliklarda kulak yorulmasin).</summary>
    public float SfxChance { get; set; } = 1f;

    // Parca (saçilma)
    public float Debris { get; set; }
    public float CritDebris { get; set; }
    public float DebrisSpeed { get; set; }
    public float DebrisSize { get; set; }
    public float DebrisLife { get; set; }
    public float DebrisGravity { get; set; }
    /// <summary>Surekli basliklarda (Kazma) iki parca patlamasi arasindaki en kisa sure.</summary>
    public float DebrisInterval { get; set; }
    public string DebrisSfx { get; set; } = "";
    public float DebrisDb { get; set; }

    // Bos tetik (cephane yok, cok agir)
    public string EmptySfx { get; set; } = "";
    public float EmptyDb { get; set; }
}

public sealed class FeelFile
{
    public FeelCriteria Criteria { get; set; } = new();
    public FeelMaster Master { get; set; } = new();
    public HitStopFeel HitStop { get; set; } = new();
    public ShakeFeel Shake { get; set; } = new();
    public RecoilFeel Recoil { get; set; } = new();
    public DamageNumberFeel DamageNumbers { get; set; } = new();
    public SquashFeel Squash { get; set; } = new();
    public ScreenFlashFeel ScreenFlash { get; set; } = new();
    public Dictionary<string, FeelSource> Sources { get; set; } = new();
    /// <summary>Yer tutucu isaretleri (URETIM 3): cikis kapisinda bos olmali.</summary>
    public List<string> Placeholders { get; set; } = new();

    public FeelSource Source(string id) => Sources.TryGetValue(id, out var s) ? s : FeelSource.None;

    /// <summary>
    /// Oyuncu ayarini (user) varsayilanin (baseDoc) ustune bindirir. Yalnizca varsayilanda olan anahtarlar ve yalnizca
    /// sayilar alinir: eski bir ayar dosyasindaki bilinmeyen ya da tipi degismis anahtar yok sayilir, veriye yeni eklenen
    /// anahtar varsayilanla gelir. Sonuc yeni bir belgedir.
    /// </summary>
    public static JsonObject Overlay(JsonObject baseDoc, JsonNode? user)
    {
        var result = (JsonObject)baseDoc.DeepClone();
        if (user is JsonObject u) Merge(result, u);
        return result;
    }

    static void Merge(JsonObject target, JsonObject user)
    {
        foreach (var (key, value) in target.ToList())
        {
            if (key.StartsWith('_') || !user.TryGetPropertyValue(key, out var uv) || uv == null) continue;
            if (value is JsonObject to && uv is JsonObject uo) Merge(to, uo);
            else if (IsNumber(value) && IsNumber(uv)) target[key] = JsonValue.Create(Num(uv!));
        }
    }

    /// <summary>Kayit icin: doc'ta varsayilandan farkli olan sayilar (oyuncu ayar dosyasi yalnizca degisenleri tasir).</summary>
    public static JsonObject Diff(JsonObject baseDoc, JsonObject doc)
    {
        var r = new JsonObject();
        foreach (var (key, value) in doc)
        {
            if (key.StartsWith('_') || !baseDoc.TryGetPropertyValue(key, out var b)) continue;
            if (value is JsonObject vo && b is JsonObject bo)
            {
                var d = Diff(bo, vo);
                if (d.Count > 0) r[key] = d;
            }
            else if (IsNumber(value) && IsNumber(b) && Math.Abs(Num(value!) - Num(b!)) > 1e-9)
                r[key] = JsonValue.Create(Num(value));
        }
        return r;
    }

    public static bool IsNumber(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.Number;

    /// <summary>Sayi dugumunun degeri; dugum metinden okunmus da olsa koddan (int, float) kurulmus da olsa.</summary>
    public static double Num(JsonNode n) => n.Deserialize<double>();
}

public sealed partial class GameData
{
    [DataFile("feel.json")]
    public FeelFile Feel { get; private set; } = new();

    /// <summary>Veri belgesini (yorum ve sondaki virgul serbest) agac olarak okur: F1 paneli icin.</summary>
    public static JsonObject ParseDoc(string json) =>
        JsonNode.Parse(json, null, DocOptions) as JsonObject ?? throw new InvalidDataException("ust duzey JSON nesnesi olmali");

    /// <summary>Feel belgesini (Overlay sonucu ya da panelin degistirdigi agac) tipli nesneye cevirir.</summary>
    public static FeelFile ParseFeel(JsonObject doc) =>
        doc.Deserialize<FeelFile>(Options) ?? throw new InvalidDataException("feel okunamadi");
}
