using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>Ses efektleri ve muzik. Her calista perde +-%8, duzey +-2 dB oynar (SANAT-SES §10.1).</summary>
public static class Sfx
{
    static readonly Dictionary<string, AudioStream> Cache = new();
    public static Node Root;
    static AudioStreamPlayer _music;
    static AudioStreamPlayer _beam;
    public static bool Muted;

    static AudioStream Get(string name)
    {
        if (Cache.TryGetValue(name, out var s)) return s;
        string path = name == "kabarma_night" ? "res://assets/audio/music/kabarma_night.mp3" : $"res://assets/audio/sfx/{name}.mp3";
        s = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        if (s is AudioStreamMP3 mp3 && (name == "kabarma_night" || name == "beam_loop")) mp3.Loop = true;
        Cache[name] = s;
        return s;
    }

    public static readonly string[] Names = { "beam_loop", "pickup", "place", "till", "water", "swell", "pop", "nailgun", "hit", "levelup", "siren", "rocket", "kabarma_night" };

    /// <summary>Butun ses dosyalari yukleniyor mu: acilista bir kez denetlenir.</summary>
    public static int CheckAll()
    {
        int ok = 0;
        foreach (var n in Names) { var s = Get(n); if (s != null && s.GetLength() > 0.05) ok++; else GD.PrintErr($"AUDIO missing {n}"); }
        return ok;
    }

    public static void Play(string name, Vector3? at = null, float db = 0f)
    {
        if (Root == null || Muted) return;
        var stream = Get(name);
        if (stream == null) return;
        float pitch = 1f + (float)GD.RandRange(-0.08, 0.08);
        float vol = db + (float)GD.RandRange(-2.0, 2.0);
        if (at is Vector3 p)
        {
            var pl = new AudioStreamPlayer3D { Stream = stream, PitchScale = pitch, VolumeDb = vol, UnitSize = 8f, MaxDistance = 70f };
            Root.AddChild(pl);
            pl.GlobalPosition = p;
            pl.Finished += pl.QueueFree;
            pl.Play();
        }
        else
        {
            var pl = new AudioStreamPlayer { Stream = stream, PitchScale = pitch, VolumeDb = vol };
            Root.AddChild(pl);
            pl.Finished += pl.QueueFree;
            pl.Play();
        }
    }

    public static void Beam(bool on)
    {
        if (Root == null) return;
        if (_beam == null)
        {
            _beam = new AudioStreamPlayer { Stream = Get("beam_loop"), VolumeDb = -8f };
            Root.AddChild(_beam);
        }
        if (on && !_beam.Playing && !Muted) _beam.Play();
        if (!on && _beam.Playing) _beam.Stop();
    }

    public static void Music(bool on)
    {
        if (Root == null) return;
        if (_music == null)
        {
            _music = new AudioStreamPlayer { Stream = Get("kabarma_night"), VolumeDb = -6f };
            Root.AddChild(_music);
        }
        if (on && !Muted)
        {
            _music.VolumeDb = -40f;
            _music.Play();
            _music.CreateTween().TweenProperty(_music, "volume_db", -6f, 2.5f);
        }
        else if (!on && _music.Playing)
        {
            var tw = _music.CreateTween();
            tw.TweenProperty(_music, "volume_db", -40f, 3f);
            tw.TweenCallback(Callable.From(_music.Stop));
        }
    }
}
