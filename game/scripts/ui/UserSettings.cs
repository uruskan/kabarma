using System;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Oyuncu erisilebilirlik ayarlari (SANAT-SES §8.3; M6 arayuz): renk korlugu suzgeci, ekran sarsintisi, altyazi acik/kapali ve boyu.
/// Arayuz boyu UiScaler'da (user://ui.cfg), tus atamasi Controls'ta (user://input.json), ses kanallari Sfx'te durur.
/// Tercihler oyun kaydi degil: user://settings.cfg. Sinamalarda --settings_path ile baska dosya (oyuncunun dosyasina dokunulmaz).
/// </summary>
public sealed class UserSettings
{
    /// <summary>Oyunun tek ayar nesnesi (dinleyiciler buna baglanir); Load yerinde yeniden okur.</summary>
    public static UserSettings Current { get; } = new();

    /// <summary>0 kapali, 1 protanopi, 2 deuteranopi, 3 tritanopi.</summary>
    public int ColorBlind { get; private set; }
    public bool Shake { get; private set; } = true;
    public bool Subtitles { get; private set; } = true;
    /// <summary>Altyazi boyu secenegi (ui.json access.subtitleScales indeksi).</summary>
    public int SubtitleSize { get; private set; } = 1;

    public event Action Changed;
    string _path = "user://settings.cfg";

    /// <summary>Oyun ayarlarini dosyadan yerinde okur (Current).</summary>
    public static UserSettings Load(string path = null)
    {
        var s = Current;
        s._path = path ?? (Main.Args.TryGetValue("settings_path", out var p) ? p : "user://settings.cfg");
        s.Read();
        s.Changed?.Invoke();
        return s;
    }

    /// <summary>Sinama: oyunun ayarlarindan bagimsiz, baska dosyaya yazan ayar nesnesi.</summary>
    public static UserSettings Detached(string path)
    {
        var s = new UserSettings { _path = path };
        s.Read();
        return s;
    }

    void Read()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(_path) != Error.Ok) return;
        ColorBlind = Math.Clamp((int)cfg.GetValue("access", "color_blind", 0), 0, 3);
        Shake = (bool)cfg.GetValue("access", "shake", true);
        Subtitles = (bool)cfg.GetValue("access", "subtitles", true);
        SubtitleSize = Math.Max(0, (int)cfg.GetValue("access", "subtitle_size", 1));
    }
    public float SubtitleScale
    {
        get
        {
            var list = UiTheme.Data.Access.SubtitleScales;
            return list.Count == 0 ? 1f : list[Math.Clamp(SubtitleSize, 0, list.Count - 1)];
        }
    }

    public void SetColorBlind(int mode) { ColorBlind = Math.Clamp(mode, 0, UiTheme.Data.Access.ColorBlindModes.Count - 1); Changed2(); }
    public void SetShake(bool on) { Shake = on; Changed2(); }
    public void SetSubtitles(bool on) { Subtitles = on; Changed2(); }
    public void SetSubtitleSize(int i) { SubtitleSize = Math.Clamp(i, 0, Math.Max(0, UiTheme.Data.Access.SubtitleScales.Count - 1)); Changed2(); }

    void Changed2()
    {
        Save();
        Changed?.Invoke();
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("access", "color_blind", ColorBlind);
        cfg.SetValue("access", "shake", Shake);
        cfg.SetValue("access", "subtitles", Subtitles);
        cfg.SetValue("access", "subtitle_size", SubtitleSize);
        cfg.Save(_path);
    }
}
