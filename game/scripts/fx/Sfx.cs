using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ses efektleri (SANAT-SES §10.1). Sesler data/audio.json'da: kimlik, kanal, duzey, varyasyon sayisi, M0 yedegi.
/// Her calista varyasyonlardan biri secilir (ayni varyasyon art arda calmaz); perde ±%8, duzey ±2 dB oynar.
/// Varyasyon dosyasi yoksa M0 sesi, o da yoksa sessiz gecer: oyun cokmez.
/// Eski cagri adlari ("place", "hit"...) M0 adi olarak gecerlidir; yeni cagrilar ses kimligini kullanir.
/// Parcalar: Sfx.Buses.cs (kanallar, ayarlar, kisma), Sfx.Events.cs (olaylara bagli sesler), MusicDirector.cs (muzik
/// ve ortam), Foley.cs (adim, inis, Gidak, ates), Hud.Voice.cs (Suna), ui/AudioPanel.cs (F2 ses ayarlari).
/// Kurulum: Director._Ready "Sfx.Root = this" atar; Root bir Director ise kanallar ve ses dugumleri kurulur.
/// </summary>
public static partial class Sfx
{
    static Node _root;
    static Director _d;
    static AudioFile _a;
    static readonly Dictionary<string, AudioStream> Streams = new();
    static readonly Dictionary<string, List<string>> VariantFiles = new();
    static readonly Dictionary<string, int> LastVariant = new();
    static readonly Dictionary<string, int> Live = new();
    static readonly HashSet<string> Warned = new();
    static AudioStreamPlayer _beam;
    static string _beamId;
    static MusicDirector _music;

    /// <summary>--mute: ana kanal sessiz, tek seferlik sesler hic kurulmaz (sinamalar).</summary>
    public static bool Muted;

    /// <summary>Seslerin eklendigi dugum. Director atanirsa kanallar, muzik, ortam ve ayar paneli kurulur.</summary>
    public static Node Root
    {
        get => _root;
        set { _root = value; if (value is Director d) Init(d); }
    }

    /// <summary>Yuklu ses verisi (audio.json); Director yokken null.</summary>
    public static AudioFile Data => _a;
    public static MusicDirector MusicDir => _music;

    static void Init(Director d)
    {
        _d = d;
        Setup(d.Data.Audio, d);
        WireEvents(d); // Sfx.Events.cs
        _music = MusicDirector.For(d);
        d.AddChild(_music);
        d.AddChild(new Foley(d) { Name = "Foley" });
        d.AddChild(new AudioPanel { Name = "AudioPanel" });
    }

    /// <summary>Director olmadan kurulum (ses sinamasi): veri, kanallar, onbellek.</summary>
    public static void Setup(AudioFile data, Node root)
    {
        _root = root;
        _a = data;
        Streams.Clear(); VariantFiles.Clear(); LastVariant.Clear(); Live.Clear(); Warned.Clear();
        _beam = null; _beamId = null; _music = null;
        SetupBuses(); // Sfx.Buses.cs
    }

    // ---------- Dosya cozumleme ----------

    /// <summary>Sesin diskte bulunan varyasyon dosyalari (onbellekli).</summary>
    public static IReadOnlyList<string> Variants(SoundDef s)
    {
        if (VariantFiles.TryGetValue(s.Id, out var list)) return list;
        list = new List<string>();
        for (int n = 1; n <= s.Variants; n++)
        {
            var p = _a.VariantPath(s, n);
            if (ResourceLoader.Exists(p)) list.Add(p);
        }
        VariantFiles[s.Id] = list;
        return list;
    }

    /// <summary>Calinacak dosya: rastgele varyasyon (art arda ayni degil), yoksa M0, yoksa null.</summary>
    static string PickPath(SoundDef s)
    {
        var files = Variants(s);
        if (files.Count > 0)
        {
            int last = LastVariant.TryGetValue(s.Id, out var l) ? l : -1;
            int roll = files.Count > 1 ? (int)(GD.Randi() % (uint)(files.Count - 1)) : 0;
            int i = AudioMix.PickVariant(files.Count, last, roll);
            LastVariant[s.Id] = i;
            return files[i];
        }
        var m0 = _a.M0File(s);
        return m0 != "" && ResourceLoader.Exists(m0) ? m0 : null;
    }

    /// <summary>Dosyayi yukler; dongu ayari paylasilan kaynagi bozmasin diye gerekirse kopyalanir.</summary>
    public static AudioStream Load(string path, bool loop)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string key = loop ? path + "|loop" : path;
        if (Streams.TryGetValue(key, out var s)) return s;
        s = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        switch (s)
        {
            case AudioStreamMP3 mp3 when mp3.Loop != loop:
                var m = (AudioStreamMP3)mp3.Duplicate(); m.Loop = loop; s = m; break;
            case AudioStreamOggVorbis ogg when ogg.Loop != loop:
                var o = (AudioStreamOggVorbis)ogg.Duplicate(); o.Loop = loop; s = o; break;
        }
        Streams[key] = s;
        return s;
    }

    /// <summary>Eski MVP davranisi: veri yoksa ya da ad veride yoksa sfx/{ad}.mp3.</summary>
    static AudioStream Legacy(string name)
    {
        var p = $"res://assets/audio/sfx/{name}.mp3";
        if (!ResourceLoader.Exists(p))
        {
            if (Warned.Add(name)) GD.PrintErr($"AUDIO unknown sound '{name}'");
            return null;
        }
        return Load(p, false);
    }

    /// <summary>Kimlik ya da eski ad; bilinmiyorsa null.</summary>
    public static SoundDef Find(string name) => _a?.Find(name);

    /// <summary>Bir sesi calmaya hazir akis (varyasyon secilir). Dongu sesleri icin loop=true.</summary>
    public static AudioStream Stream(string name, bool loop)
    {
        var s = Find(name);
        if (s == null) return _a == null ? Legacy(name) : null;
        return Load(PickPath(s), loop);
    }

    // ---------- Calma ----------

    /// <summary>
    /// Tek seferlik ses. <paramref name="at"/> verilirse konumlu (uzaklikla zayiflar ve bogulur).
    /// <paramref name="db"/> verideki duzeyin ustune eklenir; <paramref name="pitch"/> carpani (ornegin boya gore sisme).
    /// </summary>
    public static void Play(string name, Vector3? at = null, float db = 0f, float pitch = 1f)
    {
        if (_root == null || Muted || !_root.IsInsideTree()) return;
        var s = Find(name);
        AudioStream stream;
        if (s != null) stream = Load(PickPath(s), false);
        else
        {
            if (_a != null && Warned.Add(name)) GD.PrintErr($"AUDIO unknown sound '{name}' (audio.json'da yok)");
            stream = _a == null ? Legacy(name) : null;
        }
        if (stream == null) return;

        string liveKey = s?.Id ?? name;
        int cap = s != null && s.MaxVoices > 0 ? s.MaxVoices : (_a?.MaxVoices ?? 8);
        Live.TryGetValue(liveKey, out int live);
        if (cap > 0 && live >= cap) return;

        float jp = 1f, jdb = 0f;
        if (_a != null) (jp, jdb) = AudioMix.Jitter(_a.Variation, GD.Randf(), GD.Randf());
        float vol = (s?.VolumeDb ?? 0f) + db + jdb;
        string bus = BusOr(s?.Bus ?? "Sfx");
        if (at is Vector3 p)
        {
            var sp = _a?.Spatial;
            var pl = new AudioStreamPlayer3D { Stream = stream, PitchScale = pitch * jp, VolumeDb = vol, Bus = bus };
            if (sp != null) { pl.UnitSize = sp.UnitSize; pl.MaxDistance = sp.MaxDistance; pl.AttenuationFilterCutoffHz = sp.LowPassHz; pl.AttenuationFilterDb = sp.LowPassDb; }
            _root.AddChild(pl);
            pl.GlobalPosition = p;
            pl.Finished += () => Done(liveKey, pl);
            pl.Play();
        }
        else
        {
            var pl = new AudioStreamPlayer { Stream = stream, PitchScale = pitch * jp, VolumeDb = vol, Bus = bus };
            _root.AddChild(pl);
            pl.Finished += () => Done(liveKey, pl);
            pl.Play();
        }
        Live[liveKey] = live + 1;
    }

    static void Done(string key, Node pl)
    {
        if (Live.TryGetValue(key, out int n)) Live[key] = Mathf.Max(0, n - 1);
        pl.QueueFree();
    }

    /// <summary>
    /// Konumlu dongu: <paramref name="parent"/> dugumune baglanir ve calar (ates, gol kiyisi, makine). Ses dosyasi
    /// yoksa null. Cagiran durdurmak icin dugumu siler.
    /// </summary>
    public static AudioStreamPlayer3D Emitter(string id, Node3D parent, float maxDistance = 0f, float db = 0f)
    {
        if (_a == null || parent == null) return null;
        var s = Find(id);
        var stream = s == null ? null : Load(PickPath(s), true);
        if (stream == null) return null;
        var sp = _a.Spatial;
        var pl = new AudioStreamPlayer3D
        {
            Name = $"Loop_{s.Id}", Stream = stream, VolumeDb = s.VolumeDb + db, Bus = BusOr(s.Bus), Autoplay = !Muted,
            UnitSize = sp.UnitSize, MaxDistance = maxDistance > 0 ? maxDistance : sp.MaxDistance,
            AttenuationFilterCutoffHz = sp.LowPassHz, AttenuationFilterDb = sp.LowPassDb,
        };
        parent.AddChild(pl);
        return pl;
    }

    /// <summary>Konumsuz dongu oynatici (ortam, isin); calmaz, cagiran oynatir. Ses dosyasi yoksa akissiz oynatici.</summary>
    public static AudioStreamPlayer LoopPlayer(string id, Node parent)
    {
        var s = Find(id);
        var pl = new AudioStreamPlayer
        {
            Name = $"Loop_{id}", Stream = s == null ? null : Load(PickPath(s), true),
            VolumeDb = s?.VolumeDb ?? 0f, Bus = BusOr(s?.Bus ?? "Sfx"),
        };
        parent.AddChild(pl);
        return pl;
    }

    /// <summary>Capa isini dongusu: takili baslik Cekim ise cekim isini, degilse kazma isini.</summary>
    public static void Beam(bool on)
    {
        if (_root == null) return;
        string id = _d?.Capa?.Current == Capa.Head.Cekim ? "cekim_isini" : "kazma_isini";
        if (_beam == null || !IsInstanceValid(_beam))
        {
            _beam = new AudioStreamPlayer { Name = "Beam" };
            _root.AddChild(_beam);
            _beamId = null;
        }
        if (on && _beamId != id)
        {
            var s = Find(id);
            _beam.Stream = Stream(id, true) ?? (_a == null ? Load("res://assets/audio/sfx/beam_loop.mp3", true) : null);
            _beam.VolumeDb = s?.VolumeDb ?? -8f;
            _beam.Bus = BusOr(s?.Bus ?? "Sfx");
            _beamId = id;
            if (_beam.Playing) _beam.Stop();
        }
        if (on && !_beam.Playing && !Muted && _beam.Stream != null) _beam.Play();
        if (!on && _beam.Playing) _beam.Stop();
    }

    static bool IsInstanceValid(GodotObject o) => GodotObject.IsInstanceValid(o);

    /// <summary>
    /// Kabarma muzigi istegi (eski arayuz: Director.Night alarmda true, safakta false). Muzik durum makinesi
    /// (MusicDirector) gece evresini kendisi de izler; bu cagri yalnizca istegi iletir.
    /// </summary>
    public static void Music(bool on) => _music?.Request(on);

    // ---------- Denetim ----------

    /// <summary>Verideki butun ses kimlikleri (veri yoksa MVP adlari).</summary>
    public static string[] Names => _a != null
        ? _a.Sounds.Select(s => s.Id).ToArray()
        : new[] { "beam_loop", "pickup", "place", "till", "water", "swell", "pop", "nailgun", "hit", "levelup", "siren", "rocket" };

    /// <summary>
    /// Acilista bir kez: kac ses calinabilir (varyasyon ya da M0). M0 yedegi kayipsa hata yazar; yer tutucu ve
    /// dosyasiz sesleri tek satirda sayar.
    /// </summary>
    public static int CheckAll()
    {
        if (_a == null) return Names.Count(n => Legacy(n) != null);
        int ok = 0, variants = 0, onlyM0 = 0, none = 0, placeholder = 0;
        foreach (var s in _a.Sounds)
        {
            if (s.Placeholder) placeholder++;
            var v = Variants(s);
            variants += v.Count;
            if (v.Count > 0) { ok++; continue; }
            var m0 = _a.M0File(s);
            if (m0 != "" && ResourceLoader.Exists(m0)) { ok++; onlyM0++; continue; }
            if (s.M0 != null) GD.PrintErr($"AUDIO missing M0 file {m0} ({s.Id})");
            none++;
        }
        GD.Print($"AUDIO sounds={_a.Sounds.Count} playable={ok} variant_files={variants} m0_only={onlyM0} silent={none} placeholder={placeholder}");
        return ok;
    }
}
