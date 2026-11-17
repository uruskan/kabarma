using System;
using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Muzik durum makinesi (SANAT-SES §10.2) ve ortam donguleri.
/// Durumlar: Sessizlik (aksam), Gunduz (cayir parcasi; bitince bir sure susar, "sessizlik de bir durumdur"),
/// Kabarma (3 katman ayni anda baslar; katmanlar dalga numarasina ve Gosteri'ye gore olcu sinirinda acilip kapanir).
/// Durumlar arasi gecis en az iki saniye (audio.json music.crossfadeSeconds). Saat gercek zamandir: vurus
/// duraklamasi ve --fast muzigi etkilemez.
/// Durum kaynagi disaridan verilir (For: Director evresi ve gece; sinama: elle).
/// </summary>
public partial class MusicDirector : Node
{
    public enum State { Silence, Day, Kabarma }

    /// <summary>Gece bilgisi: dalga sirasi (alarmda -1), dalga sayisi, Gosteri ve sinirlari.</summary>
    public readonly record struct NightInfo(int Wave, int WaveCount, float Gosteri, float GosteriMin, float GosteriMax);

    const float Silent = -60f;
    readonly AudioFile _a;
    readonly Func<State> _target;
    readonly Func<NightInfo?> _night;
    readonly double _barSeconds;
    readonly Dictionary<AudioStreamPlayer, Tween> _tweens = new();
    readonly List<AudioStreamPlayer> _layers = new();
    AudioStreamPlayer _day, _ambDay, _ambNight;
    LayerPlan _plan;
    ulong _lastMs, _kabarmaMs;
    long _bar;
    double _dayWait;
    bool _dayPlaying, _requested;
    int _lastWave = -1;

    public State Current { get; private set; } = State.Silence;
    public int LayersActive => _plan?.Active ?? 0;
    /// <summary>Calinabilir katman sayisi (dosyasi ya da M0 yedegi olan).</summary>
    public int LayersLoaded { get; private set; }
    public bool DayLoaded => _day?.Stream != null;

    public MusicDirector(AudioFile audio, Func<State> target, Func<NightInfo?> night, double barSecondsOverride = -1)
    {
        _a = audio;
        _target = target;
        _night = night;
        var k = audio.Music.Kabarma;
        _barSeconds = barSecondsOverride >= 0 ? barSecondsOverride : AudioMix.BarSeconds(k.Bpm, k.BeatsPerBar);
        Name = "Music";
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>Oyundaki kurulum: hedef durum Director evresinden ve geceden okunur.</summary>
    public static MusicDirector For(Director d)
    {
        MusicDirector m = null;
        m = new MusicDirector(d.Data.Audio,
            () =>
            {
                var n = d.Night;
                if (m._requested) return State.Kabarma;
                if (d.Phase == Director.Act.Night && n != null && n.Phase != NightPhase.Dawn) return State.Kabarma;
                if (d.Phase == Director.Act.Dusk) return State.Silence;
                return State.Day;
            },
            () =>
            {
                var n = d.Night;
                if (n == null) return null;
                var g = d.Data.Kabarma.Gosteri;
                return new NightInfo(n.Phase == NightPhase.Waves ? n.WaveIndex : -1, d.Data.Kabarma.FirstNight.Waves.Count, n.Gosteri, g.Min, g.Max);
            });
        return m;
    }

    /// <summary>Kabarma muzigi istegi (Sfx.Music). Gece evresi zaten izlenir; istek yalnizca ek bir isarettir.</summary>
    public void Request(bool on) => _requested = on;

    public override void _Ready()
    {
        var music = _a.Music;
        string bus = Sfx.BusOr(music.Bus);
        _day = new AudioStreamPlayer { Name = "Day", Bus = bus, Stream = Sfx.Load(music.Day.File, false) };
        AddChild(_day);
        LayersLoaded = 0;
        foreach (var l in music.Kabarma.Layers)
        {
            var s = Sfx.Load(l.File, true) ?? Sfx.Load(l.M0, true);
            if (s != null) LayersLoaded++;
            var p = new AudioStreamPlayer { Name = $"Layer{_layers.Count + 1}", Bus = bus, Stream = s, VolumeDb = Silent };
            AddChild(p);
            _layers.Add(p);
        }
        _ambDay = Sfx.LoopPlayer(_a.Ambience.Day, this);
        _ambNight = Sfx.LoopPlayer(_a.Ambience.Night, this);
        _plan = new LayerPlan(music.Kabarma.HoldBars);
        _lastMs = Time.GetTicksMsec();
        GD.Print($"MUSIC ready day={(DayLoaded ? "yes" : "no")} layers={LayersLoaded}/{_layers.Count} bar={_barSeconds:0.00}s");
    }

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksMsec();
        double dt = (now - _lastMs) / 1000.0;
        _lastMs = now;

        var target = _target();
        if (target != Current) Enter(target);
        if (Current == State.Day) TickDay(dt);
        else if (Current == State.Kabarma) TickKabarma(now);
    }

    void Enter(State to)
    {
        var from = Current;
        Current = to;
        float xf = _a.Music.CrossfadeSeconds;
        float af = _a.Ambience.FadeSeconds;
        if (from == State.Day && _day.Playing) Fade(_day, Silent, xf, stop: true);
        if (from == State.Kabarma) foreach (var l in _layers) if (l.Playing) Fade(l, Silent, xf, stop: true);
        _dayPlaying = false;

        Ambient(_ambDay, to == State.Day, af);
        Ambient(_ambNight, to == State.Kabarma, af);

        if (to == State.Day) _dayWait = _a.Music.Day.StartDelaySeconds;
        if (to == State.Kabarma) StartKabarma(xf);
        GD.Print($"MUSIC {from} -> {to}");
    }

    void Ambient(AudioStreamPlayer p, bool on, float fade)
    {
        if (p?.Stream == null) return;
        var def = Sfx.Find(p == _ambDay ? _a.Ambience.Day : _a.Ambience.Night);
        float db = def?.VolumeDb ?? 0f;
        if (on)
        {
            if (!p.Playing) { p.VolumeDb = Silent; p.Play(); }
            Fade(p, db, fade);
        }
        else if (p.Playing) Fade(p, Silent, fade, stop: true);
    }

    // ---------- Gunduz ----------

    void TickDay(double dt)
    {
        if (_day.Stream == null) return;
        if (_dayPlaying)
        {
            if (_day.Playing) return;
            _dayPlaying = false;
            _dayWait = AudioMix.SilenceAfter((float)_day.Stream.GetLength(), _a.Music.Day.SilenceShare);
            GD.Print($"MUSIC day silence {_dayWait:0}s");
            return;
        }
        _dayWait -= dt;
        if (_dayWait > 0 || double.IsInfinity(_dayWait)) return;
        _day.VolumeDb = Silent;
        _day.Play();
        Fade(_day, _a.Music.Day.VolumeDb, _a.Music.CrossfadeSeconds);
        _dayPlaying = true;
    }

    // ---------- Kabarma ----------

    void StartKabarma(float fade)
    {
        _kabarmaMs = Time.GetTicksMsec();
        _bar = 0;
        _lastWave = -1;
        _plan.Reset(Desired(), 0);
        // Katmanlar ayni karede baslar ve hep calar; kapali katman duyulmayacak kadar kisiktir (hizali kalsinlar).
        foreach (var l in _layers) if (l.Stream != null) { l.VolumeDb = Silent; l.Play(); }
        ApplyLayers(fade);
    }

    int Desired()
    {
        var k = _a.Music.Kabarma;
        if (_night() is not NightInfo n) return 1;
        float i = AudioMix.Intensity(k, n.Wave, n.WaveCount, n.Gosteri, n.GosteriMin, n.GosteriMax);
        return AudioMix.LayersFor(i, k.Thresholds, k.Layers.Count);
    }

    void TickKabarma(ulong now)
    {
        if (_night() is NightInfo n && n.Wave != _lastWave)
        {
            if (n.Wave > _lastWave && n.Wave >= 0) Sfx.Play("dalga_girisi");
            _lastWave = n.Wave;
        }
        long bar = AudioMix.BarIndex((now - _kabarmaMs) / 1000.0, _barSeconds);
        if (bar == _bar) return;
        _bar = bar;
        if (_plan.OnBar(bar, Desired()))
        {
            ApplyLayers(_a.Music.Kabarma.LayerFadeSeconds);
            GD.Print($"MUSIC layers={_plan.Active} bar={bar}");
        }
    }

    void ApplyLayers(float fade)
    {
        var defs = _a.Music.Kabarma.Layers;
        for (int i = 0; i < _layers.Count; i++)
            if (_layers[i].Stream != null) Fade(_layers[i], i < _plan.Active ? defs[i].VolumeDb : Silent, fade);
    }

    // ---------- Yardimci ----------

    void Fade(AudioStreamPlayer p, float db, float seconds, bool stop = false)
    {
        if (p == null) return;
        if (_tweens.TryGetValue(p, out var old) && old.IsValid()) old.Kill();
        var tw = CreateTween();
        tw.SetIgnoreTimeScale(true);
        tw.SetPauseMode(Tween.TweenPauseMode.Process);
        tw.TweenProperty(p, "volume_db", db, Mathf.Max(0.01f, seconds));
        if (stop) tw.TweenCallback(Callable.From(p.Stop));
        _tweens[p] = tw;
    }

    /// <summary>Katman oynaticilarinin anlik duzeyleri (sinama icin).</summary>
    public float[] LayerVolumes() => _layers.ConvertAll(l => l.VolumeDb).ToArray();
}
