namespace Kabarma.Sim;

// Ses: audio.json (SANAT-SES §10). Kanallar, ses efektleri (M1'in 60 sesi, §10.1), muzik (§10.2), Suna telsizi (§10.3).
// Dosya yollari kalip: {id} ses kimligi, {n} varyasyon numarasi (1'den), {m0} M0 dosya adi, {lang} dil, {line} replik kimligi.
// Eksik dosya: M0 sesi (varsa), yoksa sessiz. "placeholder": true olanlar yer tutucudur (URETIM §3).

/// <summary>Ses kanali (Godot ses yolu). Ilk kanal "Master": Godot'un ana yolu; digerleri ona gonderir.</summary>
public sealed class AudioBusDef
{
    public string Id { get; set; } = "";
    /// <summary>Ayar panelindeki Turkce ad.</summary>
    public string Name { get; set; } = "";
    /// <summary>Varsayilan duzey, dogrusal 0..1.</summary>
    public float Volume { get; set; } = 1f;
}

/// <summary>Her calista oynama (SANAT-SES §10.1: perde ±%8, duzey ±2 dB).</summary>
public sealed class AudioVariationDef
{
    public float Pitch { get; set; }
    public float VolumeDb { get; set; }
}

/// <summary>Konumlu ses: uzaklikla zayiflama ve alcak geciren suzgec (SANAT-SES §10.1 "Mekan").</summary>
public sealed class AudioSpatialDef
{
    public float UnitSize { get; set; }
    public float MaxDistance { get; set; }
    public float LowPassHz { get; set; }
    public float LowPassDb { get; set; }
}

/// <summary>Suna telsizi (SANAT-SES §10.3): bant geciren suzgec, hafif doyum, basta ve sonda mandal.</summary>
public sealed class RadioDef
{
    public string Bus { get; set; } = "";
    public float HighPassHz { get; set; }
    public float LowPassHz { get; set; }
    public float Drive { get; set; }
    public float PreGainDb { get; set; }
    public float PostGainDb { get; set; }
    /// <summary>Basta ve sonda calan mandal sesinin kimligi (sounds icinde).</summary>
    public string Latch { get; set; } = "";
    /// <summary>Mandaldan sonra sesin baslamasina kadar.</summary>
    public float LatchGapSeconds { get; set; }
    /// <summary>Ses bittikten sonra altyazinin ekranda kalma suresi.</summary>
    public float TailSeconds { get; set; }
    /// <summary>Suna konusurken muzik kisilir (SANAT-SES §10.1: 4 dB).</summary>
    public float DuckDb { get; set; }
    public float DuckFadeSeconds { get; set; }
    /// <summary>Seslendirme dili (tr, en). Altyazi seslendirilen metnin aynisi oldugu icin arayuz diliyle ayni.</summary>
    public string Language { get; set; } = "tr";
    public string Path { get; set; } = "";
}

/// <summary>Bir ses efekti: SANAT-SES §10.1 M1 listesinin bir satiri.</summary>
public sealed class SoundDef
{
    public string Id { get; set; } = "";
    /// <summary>§10.1 M1 tablosundaki grup: capa, silah, oyuncu, yaratik, tarim, yapi, istasyon, kabarma, kart, arayuz, ortam.</summary>
    public string Group { get; set; } = "";
    /// <summary>Belgedeki adi.</summary>
    public string Name { get; set; } = "";
    public string Bus { get; set; } = "";
    public bool Loop { get; set; }
    public int Variants { get; set; }
    public float VolumeDb { get; set; }
    /// <summary>M0'da uretilmis tek surum (sfx/{m0}.mp3). Eski cagri adi olarak da gecerli: Sfx.Play("place") bu sese gider.</summary>
    public string? M0 { get; set; }
    /// <summary>Oyunda nerede calar: olay ya da kod yeri.</summary>
    public string Play { get; set; } = "";
    /// <summary>Calma noktasi kodda bagli mi (false: sahibi akisin baglamasi gerekiyor, raporda listeli).</summary>
    public bool Wired { get; set; }
    /// <summary>Ayni anda en fazla kac kopya (0: genel sinir).</summary>
    public int MaxVoices { get; set; }
    /// <summary>Kaynak: m0, synth, elevenlabs; null = dosya yok.</summary>
    public string? Source { get; set; }
    public bool Placeholder { get; set; } = true;
}

public sealed class MusicLayerDef
{
    public string File { get; set; } = "";
    public float VolumeDb { get; set; }
    /// <summary>Katman dosyasi yoksa calan M0 dosyasi (res:// yolu).</summary>
    public string? M0 { get; set; }
}

/// <summary>Gunduz cayir parcasi. Sessizlik de bir durumdur (SANAT-SES §10.2): parca bitince bir sure susar.</summary>
public sealed class DayMusicDef
{
    public string Id { get; set; } = "";
    public string File { get; set; } = "";
    public float VolumeDb { get; set; }
    /// <summary>Gezme muziginin susma payi (0,5 = zamanin yarisinda susar).</summary>
    public float SilenceShare { get; set; }
    /// <summary>Gune donunce (acilis, safak) muzik baslamadan once.</summary>
    public float StartDelaySeconds { get; set; }
    public bool Placeholder { get; set; } = true;
}

/// <summary>Kabarma: ayni ton ve tempoda 3 yogunluk katmani; dalga numarasina ve Gosteri'ye gore olcu sinirinda gecer.</summary>
public sealed class KabarmaMusicDef
{
    public string Id { get; set; } = "";
    public float Bpm { get; set; }
    public int BeatsPerBar { get; set; }
    public List<MusicLayerDef> Layers { get; set; } = new();
    /// <summary>Yogunluk esikleri: yogunluk her esigi gecince bir katman daha acilir (katman sayisi = 1 + gecilen esik).</summary>
    public List<float> Thresholds { get; set; } = new();
    public float WaveWeight { get; set; }
    public float GosteriWeight { get; set; }
    public float LayerFadeSeconds { get; set; }
    /// <summary>Katman azalmadan once en az kac olcu bekler (cirpinmasin).</summary>
    public int HoldBars { get; set; }
    public bool Placeholder { get; set; } = true;
}

public sealed class MusicDef
{
    public string Bus { get; set; } = "";
    /// <summary>Durumlar arasi gecis (SANAT-SES §10.2: en az iki saniye).</summary>
    public float CrossfadeSeconds { get; set; }
    public DayMusicDef Day { get; set; } = new();
    public KabarmaMusicDef Kabarma { get; set; } = new();
}

/// <summary>Ortam donguleri: gunduz cayir; Kabarma gecesi gokyuzu dalgalanmasi.</summary>
public sealed class AmbienceDef
{
    public string Day { get; set; } = "";
    public string Night { get; set; } = "";
    public float FadeSeconds { get; set; }
}

/// <summary>
/// Izleyerek calan sesler (Foley.cs): oyuncunun adimi ve inisi, Gidak'in bekleme gidaklamasi, Ocak atesi.
/// Baska akislarin koduna dokunmadan durumdan okunur.
/// </summary>
public sealed class FoleyDef
{
    /// <summary>Yuruyuste iki adim arasi yol (m); kosuda SprintStride.</summary>
    public float Stride { get; set; }
    public float SprintStride { get; set; }
    /// <summary>Bu hizin altinda adim sesi yok (m/sn).</summary>
    public float MinSpeed { get; set; }
    /// <summary>Inis sesi icin en az dusus hizi (m/sn).</summary>
    public float LandSpeed { get; set; }
    /// <summary>Ayak arazinin bu kadar ustundeyse tahta (yapi) zemini sayilir (m).</summary>
    public float WoodHeight { get; set; }
    public float StepDb { get; set; }
    /// <summary>Gidak bekleme gidaklamasi: iki gidaklama arasi en az ve en cok (sn).</summary>
    public float CluckMin { get; set; }
    public float CluckMax { get; set; }
    /// <summary>Ocak atesi dongusunun duyulma uzakligi (m).</summary>
    public float FireDistance { get; set; }
}

public sealed class AudioFile
{
    public FoleyDef Foley { get; set; } = new();
    public List<AudioBusDef> Buses { get; set; } = new();
    /// <summary>Ana cikista tepe siniri (SANAT-SES §10.1: −1 dBTP).</summary>
    public float MasterCeilingDb { get; set; }
    public AudioVariationDef Variation { get; set; } = new();
    public AudioSpatialDef Spatial { get; set; } = new();
    public int MaxVoices { get; set; }
    /// <summary>
    /// Dongu sesi tek seferlik cagrilirsa (her karede Sfx.Play) dongu calar ve son cagridan bu kadar sonra durur.
    /// Yalniz M0 yedegi olan dongu sesi eski tek seferlik davranisla calar.
    /// </summary>
    public float HoldSeconds { get; set; }
    public string SfxPath { get; set; } = "";
    public string M0Path { get; set; } = "";
    public RadioDef Radio { get; set; } = new();
    public MusicDef Music { get; set; } = new();
    public AmbienceDef Ambience { get; set; } = new();
    public List<SoundDef> Sounds { get; set; } = new();

    /// <summary>Kimlikle ya da eski M0 adiyla ("place", "hit"...) ses; yoksa null.</summary>
    public SoundDef? Find(string name) =>
        Sounds.FirstOrDefault(s => s.Id == name) ?? Sounds.FirstOrDefault(s => s.M0 == name);

    public string VariantPath(SoundDef s, int n) => SfxPath.Replace("{id}", s.Id).Replace("{n}", n.ToString());
    public string M0File(SoundDef s) => s.M0 == null ? "" : M0Path.Replace("{m0}", s.M0);
    public string VoicePath(string lang, string line) => Radio.Path.Replace("{lang}", lang).Replace("{line}", line);
}

public sealed partial class GameData
{
    [DataFile("audio.json")]
    public AudioFile Audio { get; private set; } = new();
}
