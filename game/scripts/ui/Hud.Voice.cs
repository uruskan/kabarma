using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Hud: Suna'nin sesi (SANAT-SES §10.3). Suna kutusundaki satir altyazidir: seslendirilen metnin aynisi.
/// Satir ekrana gelince basta telsiz mandali, ardindan replik "Suna (telsiz)" kanalinda calar (bant geciren suzgec ve
/// hafif doyum kanalda: Sfx.Buses.cs); ses bitince sondaki mandal. Suna konusurken muzik 4 dB kisilir.
/// Replik kimligi ekrandaki metinden bulunur (VoiceLines.FromJson: scenario.json "lines" kimlikleri, yoksa eski
/// yapidan turetilen kimlik). Metinden bulmak, kimlikle soylenen (ilkgun: Director.SayLine) ve metinle soylenen
/// (gecehud: gece replikleri) satirlarin ikisini de kapsar. Dosya assets/audio/voice/{dil}/{kimlik}.mp3; dosya
/// yoksa satir altyaziyla sessiz gecer.
/// Hud.cs'e iki satir: _Ready'de InitVoice(), _Process'te yeni satir basladiginda VoiceLineStarted(_full).
/// </summary>
public partial class Hud
{
    const string ScenarioPath = "res://data/scenario.json";
    readonly Dictionary<string, string> _voiceIds = new();
    AudioStreamPlayer _voice;
    int _voiceSerial;

    void InitVoice()
    {
        _voiceIds.Clear();
        try
        {
            string json = FileAccess.GetFileAsString(ScenarioPath);
            if (!string.IsNullOrEmpty(json))
                foreach (var lang in new[] { "tr", "en" })
                    foreach (var (id, text) in VoiceLines.FromJson(json, lang)) _voiceIds.TryAdd(text, id);
        }
        catch (System.Exception e) { GD.PushWarning($"Suna sesi: {ScenarioPath} okunamadi ({e.Message}); replikler yalniz altyazi"); }
        _voice = new AudioStreamPlayer { Name = "SunaVoice", Bus = Sfx.BusOr(_d.Data.Audio.Radio.Bus) };
        AddChild(_voice);
        _voice.Finished += () => { Sfx.Play(_d.Data.Audio.Radio.Latch); Sfx.Duck(false); };
    }

    /// <summary>Metnin replik kimligi; veride yoksa null (o satir yalnizca altyazi).</summary>
    public string VoiceId(string text) => text != null && _voiceIds.TryGetValue(text.Trim(), out var id) ? id : null;

    void VoiceLineStarted(string text)
    {
        if (_voice == null) return;
        var r = _d.Data.Audio.Radio;
        int serial = ++_voiceSerial;
        if (_voice.Playing) _voice.Stop();
        Sfx.Duck(false);
        Sfx.Play(r.Latch);

        string id = VoiceId(text);
        var path = id == null ? null : _d.Data.Audio.VoicePath(r.Language, id);
        var stream = path != null && ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        GD.Print($"SUNA line={id ?? "-"} voice={(stream != null ? "yes" : "no")}");
        if (stream == null) return;

        // Altyazi ses bitene dek ekranda kalir.
        _lineT = Mathf.Max(_lineT, r.LatchGapSeconds + (float)stream.GetLength() + r.TailSeconds);
        _voice.Stream = stream;
        Sfx.Duck(true);
        GetTree().CreateTimer(r.LatchGapSeconds, true, false, true).Timeout += () =>
        {
            if (serial != _voiceSerial || !IsInstanceValid(_voice)) return;
            _voice.Play();
        };
    }

    /// <summary>Suna su an sesli konusuyor mu.</summary>
    public bool Speaking => _voice != null && _voice.Playing;
}
