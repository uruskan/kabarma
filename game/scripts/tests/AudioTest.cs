using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=audio: ses katmaninin basliksiz sinamasi (ses akisi). Kanallar ve etkileri kuruluyor mu, ikinci kurulum
/// ikilemiyor mu; 60 sesin hepsi (ve eski M0 adlari) cokmeden caliyor mu; kanal duzeyi; muzik durum makinesi
/// (Gunduz → Kabarma, katmanlar dalga ve Gosteri ile aciliyor, bekleme olcusunden sonra kapaniyor → Gunduz →
/// Sessizlik); Suna kismasi. Sonuc: "AUDIO RESULT ok ..." ve cikis kodu 0, yoksa 1.
/// </summary>
public partial class AudioTest : Node
{
    readonly List<string> _fails = new();

    public override void _Ready() => _ = Run();

    void Check(bool ok, string what) { if (!ok) { _fails.Add(what); GD.Print($"AUDIO FAIL {what}"); } }

    async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true, false, true), SceneTreeTimer.SignalName.Timeout);

    async Task Run()
    {
        var data = GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        var a = data.Audio;
        Sfx.Setup(a, this);

        // Kanallar ve etkiler
        foreach (var b in a.Buses) Check(AudioServer.GetBusIndex(b.Id) >= 0, $"bus {b.Id} yok");
        var radio = Sfx.BusEffects(a.Radio.Bus);
        foreach (var e in new[] { "AudioEffectHighPassFilter", "AudioEffectLowPassFilter", "AudioEffectDistortion" })
            Check(radio.Contains(e) == a.Radio.Filter, a.Radio.Filter ? $"telsiz kanalinda {e} yok" : $"suzgec kapaliyken telsiz kanalinda {e} var");
        Check(Sfx.BusEffects("Master").Contains("AudioEffectHardLimiter"), "ana kanalda tepe siniri yok");
        Check(Sfx.BusEffects(a.Music.Bus).Contains("AudioEffectAmplify"), "muzik kanalinda kisma yok");
        int buses = AudioServer.BusCount, voiceFx = radio.Length;
        Sfx.Setup(a, this);
        Check(AudioServer.BusCount == buses && Sfx.BusEffects(a.Radio.Bus).Length == voiceFx, "ikinci kurulum kanal ya da etki ikiledi");

        // Butun sesler: cokmeden calar; M0 yedegi olanlar calinabilir
        int playable = 0, files = 0;
        foreach (var s in a.Sounds)
        {
            var v = Sfx.Variants(s);
            files += v.Count;
            bool m0 = s.M0 != null && ResourceLoader.Exists(a.M0File(s));
            if (v.Count > 0 || m0) playable++;
            if (s.M0 != null) Check(m0 || v.Count > 0, $"{s.Id}: M0 dosyasi {a.M0File(s)} yok");
            Sfx.Play(s.Id);
            Sfx.Play(s.Id, Vector3.Zero, -6f, 0.9f);
            if (s.Loop) { var p = Sfx.LoopPlayer(s.Id, this); p.QueueFree(); }
        }
        foreach (var old in new[] { "beam_loop", "pickup", "place", "till", "water", "swell", "pop", "nailgun", "hit", "levelup", "siren", "rocket" })
            Check(Sfx.Find(old) != null, $"eski ad '{old}' cozulmuyor");
        Sfx.Play("yok_boyle_ses"); // cokmemeli
        Sfx.Beam(true); Sfx.Beam(false);
        Check(Sfx.CheckAll() == playable, "CheckAll sayisi tutmuyor");

        // Kanal duzeyi (dosyaya yazmadan)
        Sfx.SetVolume("Music", 0.5f, save: false);
        Check(Mathf.Abs(Sfx.GetVolume("Music") - 0.5f) < 0.01f, "kanal duzeyi ayarlanmiyor");
        Sfx.SetVolume("Music", 0f, save: false);
        Check(Sfx.GetVolume("Music") == 0f, "kanal sifirda susmuyor");
        foreach (var b in a.Buses) Sfx.SetVolume(b.Id, b.Volume, save: false);

        // Muzik durum makinesi: olcu 0,25 sn (sinama hizi), bekleme 4 olcu
        var target = MusicDirector.State.Day;
        MusicDirector.NightInfo? night = null;
        var g = data.Kabarma.Gosteri;
        int waves = data.Kabarma.FirstNight.Waves.Count;
        var m = new MusicDirector(a, () => target, () => night, barSecondsOverride: 0.25);
        AddChild(m);
        await Wait(0.2);
        Check(m.Current == MusicDirector.State.Day, $"acilista Gunduz bekleniyordu: {m.Current}");
        target = MusicDirector.State.Kabarma;
        night = new MusicDirector.NightInfo(-1, waves, g.Min, g.Min, g.Max);
        await Wait(0.4);
        Check(m.Current == MusicDirector.State.Kabarma && m.LayersActive == 1, $"alarm: Kabarma ve 1 katman bekleniyordu: {m.Current} {m.LayersActive}");
        // Sureler bilerek genis (paralel Godot oturumlarinda kare takilmasi): her bekleme en az 4 olcu.
        night = new MusicDirector.NightInfo(1, waves, g.Min, g.Min, g.Max);
        await Wait(1.0);
        Check(m.LayersActive == 2, $"2. dalga: 2 katman bekleniyordu: {m.LayersActive}");
        night = new MusicDirector.NightInfo(waves - 1, waves, g.Max, g.Min, g.Max);
        await Wait(1.0);
        Check(m.LayersActive == 3, $"son dalga + Gosteri: 3 katman bekleniyordu: {m.LayersActive}");
        // Azalis: zamandan bagimsiz denetim. Kapandigi olcu, acildigi olcudan en az HoldBars sonra olmali.
        long openedAt = m.LayerChangedBar;
        night = new MusicDirector.NightInfo(0, waves, g.Min, g.Min, g.Max);
        for (int i = 0; i < 60 && m.LayersActive == 3; i++) await Wait(0.05);
        Check(m.LayersActive == 1, $"bekleme sonrasi 1 katman bekleniyordu: {m.LayersActive}");
        Check(m.LayerChangedBar - openedAt >= a.Music.Kabarma.HoldBars, $"katman {a.Music.Kabarma.HoldBars} olcu beklemeden kapandi ({openedAt} -> {m.LayerChangedBar})");
        target = MusicDirector.State.Day;
        await Wait(0.2);
        Check(m.Current == MusicDirector.State.Day, "safak: Gunduz'e donmedi");
        target = MusicDirector.State.Silence;
        await Wait(0.2);
        Check(m.Current == MusicDirector.State.Silence, "aksam: Sessizlik'e gecmedi");

        // Tutulan dongu: ayni anahtarla tekrar tekrar cagri tek oynatici kurar, cagri kesilince soner.
        var loop = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 22050, Data = new byte[22050 * 2], LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopEnd = 22050 };
        for (int i = 0; i < 5; i++) { Sfx.HoldStream("sinama_dongu", loop, Vector3.Zero, -6f, "Sfx"); await Wait(0.05); }
        Check(Sfx.HeldCount == 1, $"tutulan dongu tek olmali: {Sfx.HeldCount}");
        await Wait(a.HoldSeconds + 0.6);
        Check(Sfx.HeldCount == 0, "tutulan dongu cagri kesilince sonmedi");

        // Suna kismasi
        Sfx.Duck(true);
        await Wait(a.Radio.DuckFadeSeconds + 0.3);
        Check(Mathf.Abs(Sfx.DuckLevel - a.Radio.DuckDb) < 0.1f, $"kisma {a.Radio.DuckDb} dB bekleniyordu: {Sfx.DuckLevel:0.0}");
        Sfx.Duck(false);
        await Wait(a.Radio.DuckFadeSeconds + 0.3);
        Check(Mathf.Abs(Sfx.DuckLevel) < 0.1f, "kisma geri gelmedi");

        string ok = _fails.Count == 0 ? "ok" : "fail";
        GD.Print($"AUDIO RESULT {ok} sounds={a.Sounds.Count} playable={playable} variant_files={files} buses={a.Buses.Count} layers_loaded={m.LayersLoaded}/{a.Music.Kabarma.Layers.Count} day_music={(m.DayLoaded ? "yes" : "no")}"
                 + (_fails.Count > 0 ? " :: " + string.Join("; ", _fails) : ""));
        // Calan sesler durmadan cikilirsa Godot cikista AudioStreamPlayback sizintisi yazar (Sfx.StopAll).
        Sfx.StopAll(GetTree().Root);
        await Wait(0.1);
        GetTree().Quit(_fails.Count == 0 ? 0 : 1);
    }
}
