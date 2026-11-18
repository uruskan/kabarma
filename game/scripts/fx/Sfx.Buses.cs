using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Sfx: ses kanallari (SANAT-SES §10.1): Ana, Muzik, Efekt, Suna (telsiz), Ortam, Arayuz. Kanallar calisma aninda
/// audio.json'dan kurulur (bus duzeni dosyasi yok). Ana: tepe siniri −1 dB. Suna: bant geciren suzgec (alcak ve
/// yuksek geciren) ve hafif doyum (SANAT-SES §10.3). Muzik: Suna konusurken kisma icin yukseltec.
/// Kanal duzeyleri F2 panelinden ayarlanir ve user://audio_settings.cfg dosyasina yazilir (sinamalarda okunmaz, yazilmaz).
/// </summary>
public static partial class Sfx
{
    const string SettingsPath = "user://audio_settings.cfg";
    static Tween _duck;

    static bool Testing => Main.Args.ContainsKey("test") || Main.Args.ContainsKey("bot");

    static void SetupBuses()
    {
        foreach (var b in _a.Buses)
        {
            if (b.Id == "Master" || AudioServer.GetBusIndex(b.Id) >= 0) continue;
            AudioServer.AddBus();
            int i = AudioServer.BusCount - 1;
            AudioServer.SetBusName(i, b.Id);
            AudioServer.SetBusSend(i, "Master");
        }

        var limiter = Effect<AudioEffectHardLimiter>(0);
        limiter.CeilingDb = _a.MasterCeilingDb;

        var r = _a.Radio;
        int v = AudioServer.GetBusIndex(r.Bus);
        if (v >= 0 && r.Filter)
        {
            Effect<AudioEffectHighPassFilter>(v).CutoffHz = r.HighPassHz;
            Effect<AudioEffectLowPassFilter>(v).CutoffHz = r.LowPassHz;
            var dist = Effect<AudioEffectDistortion>(v);
            dist.Mode = AudioEffectDistortion.ModeEnum.Overdrive;
            dist.Drive = r.Drive;
            dist.PreGain = r.PreGainDb;
            dist.PostGain = r.PostGainDb;
        }
        int m = AudioServer.GetBusIndex(_a.Music.Bus);
        if (m >= 0) Effect<AudioEffectAmplify>(m).VolumeDb = 0f;

        foreach (var b in _a.Buses) SetVolume(b.Id, b.Volume, save: false);
        if (!Testing) LoadSettings();
        AudioServer.SetBusMute(0, Muted);
    }

    /// <summary>Kanaldaki <typeparamref name="T"/> etkisi; yoksa ekler (sahne yeniden kurulunca ikilenmez).</summary>
    static T Effect<T>(int bus) where T : AudioEffect, new()
    {
        for (int i = 0; i < AudioServer.GetBusEffectCount(bus); i++)
            if (AudioServer.GetBusEffect(bus, i) is T found) return found;
        var e = new T();
        AudioServer.AddBusEffect(bus, e);
        return e;
    }

    /// <summary>Kanal adi; Godot'ta yoksa "Master" (cokmesin).</summary>
    public static string BusOr(string bus) => !string.IsNullOrEmpty(bus) && AudioServer.GetBusIndex(bus) >= 0 ? bus : "Master";

    /// <summary>Kanal duzeyi, dogrusal 0..1.</summary>
    public static float GetVolume(string bus)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i < 0) return 0f;
        return AudioServer.IsBusMute(i) && i != 0 ? 0f : Mathf.DbToLinear(AudioServer.GetBusVolumeDb(i));
    }

    /// <summary>Kanal duzeyini ayarlar (0 = sessiz) ve istenirse ayar dosyasina yazar.</summary>
    public static void SetVolume(string bus, float linear, bool save = true)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i < 0) return;
        linear = Mathf.Clamp(linear, 0f, 1f);
        AudioServer.SetBusVolumeDb(i, linear <= 0.0001f ? -80f : Mathf.LinearToDb(linear));
        if (i != 0) AudioServer.SetBusMute(i, linear <= 0.0001f);
        if (save && !Testing) SaveSettings();
    }

    static void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SettingsPath) != Error.Ok) return;
        foreach (var b in _a.Buses)
            if (cfg.HasSectionKey("volume", b.Id)) SetVolume(b.Id, (float)cfg.GetValue("volume", b.Id).AsDouble(), save: false);
    }

    static void SaveSettings()
    {
        var cfg = new ConfigFile();
        foreach (var b in _a.Buses) cfg.SetValue("volume", b.Id, GetVolume(b.Id));
        cfg.Save(SettingsPath);
    }

    /// <summary>Suna konusurken muzik kanali kisilir (SANAT-SES §10.1: 4 dB), bitince geri gelir.</summary>
    public static void Duck(bool on)
    {
        if (_a == null || _root == null || !_root.IsInsideTree()) return;
        int m = AudioServer.GetBusIndex(_a.Music.Bus);
        if (m < 0) return;
        var amp = Effect<AudioEffectAmplify>(m);
        float target = on ? _a.Radio.DuckDb : 0f;
        if (_duck != null && _duck.IsValid()) _duck.Kill();
        _duck = _root.CreateTween();
        _duck.SetIgnoreTimeScale(true);
        _duck.SetPauseMode(Tween.TweenPauseMode.Process);
        _duck.TweenProperty(amp, "volume_db", target, _a.Radio.DuckFadeSeconds);
    }

    /// <summary>Muzik kanalindaki kisma (dB); sinama icin.</summary>
    public static float DuckLevel => _a == null || AudioServer.GetBusIndex(_a.Music.Bus) < 0 ? 0f : Effect<AudioEffectAmplify>(AudioServer.GetBusIndex(_a.Music.Bus)).VolumeDb;

    /// <summary>Kanalin etki siniflari (sinama icin).</summary>
    public static string[] BusEffects(string bus)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i < 0) return System.Array.Empty<string>();
        return Enumerable.Range(0, AudioServer.GetBusEffectCount(i)).Select(k => AudioServer.GetBusEffect(i, k).GetClass()).ToArray();
    }
}
