using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Ses verisi (audio.json, SANAT-SES §10) ve ses kurallari (AudioMix, LayerPlan, VoiceLines).</summary>
public class AudioTests
{
    static AudioFile A => TestData.Load().Audio;

    [Fact]
    public void M1_lists_60_sounds_in_the_documented_groups()
    {
        var a = A;
        Assert.Equal(60, a.Sounds.Count);
        Assert.Equal(60, a.Sounds.Select(s => s.Id).Distinct().Count());
        // SANAT-SES §10.1 "M1'in 60 sesi" tablosu
        var expected = new Dictionary<string, int>
        {
            ["capa"] = 9, ["silah"] = 8, ["oyuncu"] = 6, ["yaratik"] = 7, ["tarim"] = 4, ["yapi"] = 4,
            ["istasyon"] = 6, ["kabarma"] = 5, ["kart"] = 4, ["arayuz"] = 5, ["ortam"] = 2,
        };
        var groups = a.Sounds.GroupBy(s => s.Group).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(expected.OrderBy(x => x.Key), groups.OrderBy(x => x.Key));
    }

    [Fact]
    public void Twelve_sounds_come_from_M0_and_keep_their_old_names()
    {
        var m0 = A.Sounds.Where(s => s.M0 != null).Select(s => s.M0!).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "beam_loop", "hit", "levelup", "nailgun", "pickup", "place", "pop", "rocket", "siren", "swell", "till", "water" }, m0);
        Assert.Equal("parca_yerlesme", A.Find("place")?.Id);
        Assert.Equal("isabet_kesme", A.Find("hit")?.Id);
        Assert.Equal("kazma_isini", A.Find("kazma_isini")?.Id);
        Assert.Null(A.Find("yok_boyle_ses"));
    }

    [Fact]
    public void Every_sound_has_three_variants_a_known_bus_and_a_play_point()
    {
        var a = A;
        var buses = a.Buses.Select(b => b.Id).ToHashSet();
        Assert.Equal("Master", a.Buses[0].Id);
        foreach (var s in a.Sounds)
        {
            Assert.True(s.Variants == 3, $"{s.Id}: M1'de her ses 3 varyasyon (SANAT-SES §10.1)");
            Assert.Contains(s.Bus, buses);
            Assert.False(string.IsNullOrWhiteSpace(s.Name), s.Id);
            Assert.False(string.IsNullOrWhiteSpace(s.Play), s.Id);
            Assert.Matches("^[a-z0-9_]+$", s.Id);
        }
        Assert.Contains("{id}", a.SfxPath);
        Assert.Contains("{n}", a.SfxPath);
        Assert.Equal("res://assets/audio/sfx/kazma_isini_2.mp3", a.VariantPath(a.Find("kazma_isini")!, 2));
    }

    [Fact]
    public void Loops_are_the_ten_marked_in_the_document()
    {
        var loops = A.Sounds.Where(s => s.Loop).Select(s => s.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "ates_citirti", "cekim_isini", "el_arabasi_teker", "gokyuzu_dalga", "kaya_kemirme", "kazma_isini",
            "ortam_cayir_gunduz", "ortam_gol_kiyisi", "sulama", "tezgah_calisma" }, loops);
    }

    [Fact]
    public void Music_radio_and_ambience_refer_to_existing_things()
    {
        var a = A;
        var buses = a.Buses.Select(b => b.Id).ToHashSet();
        Assert.Contains(a.Music.Bus, buses);
        Assert.Contains(a.Radio.Bus, buses);
        Assert.True(a.Music.CrossfadeSeconds >= 2f, "SANAT-SES §10.2: en az iki saniyelik gecis");
        var k = a.Music.Kabarma;
        Assert.Equal(3, k.Layers.Count);
        Assert.Equal(k.Layers.Count - 1, k.Thresholds.Count);
        Assert.Equal(k.Thresholds.OrderBy(x => x), k.Thresholds);
        Assert.NotNull(a.Find(a.Radio.Latch));
        Assert.Equal(-4f, a.Radio.DuckDb); // SANAT-SES §10.1: Suna konusurken muzik 4 dB kisilir
        Assert.True(a.Find(a.Ambience.Day)?.Loop);
        Assert.True(a.Find(a.Ambience.Night)?.Loop);
        Assert.Equal("res://assets/audio/voice/tr/giris_1.mp3", a.VoicePath("tr", "giris_1"));
        Assert.True(a.MasterCeilingDb <= -1f);
    }

    [Fact]
    public void Kabarma_layers_open_with_waves_and_gosteri()
    {
        var d = TestData.Load();
        var k = d.Audio.Music.Kabarma;
        var g = d.Kabarma.Gosteri;
        int waves = d.Kabarma.FirstNight.Waves.Count;
        int L(int wave, float gosteri) => AudioMix.LayersFor(AudioMix.Intensity(k, wave, waves, gosteri, g.Min, g.Max), k.Thresholds, k.Layers.Count);

        Assert.Equal(1, L(-1, g.Min)); // alarm: yalniz temel
        Assert.Equal(1, L(0, g.Min));
        Assert.Equal(3, L(waves - 1, g.Max));
        // Dalga ilerledikce katman azalmaz; Gosteri arttikca azalmaz.
        for (int w = 0; w < waves - 1; w++) Assert.True(L(w + 1, g.Min) >= L(w, g.Min));
        Assert.True(L(0, g.Max) >= L(0, g.Min));
        Assert.True(L(0, g.Max) > 1, "tek basina yuksek Gosteri ilk dalgada da bir katman acar");
    }

    [Fact]
    public void Layer_plan_rises_at_once_and_falls_after_holding()
    {
        var p = new LayerPlan(holdBars: 4);
        p.Reset(1, 0);
        Assert.True(p.OnBar(1, 3));
        Assert.Equal(3, p.Active);
        Assert.False(p.OnBar(2, 1)); // henuz 4 olcu gecmedi
        Assert.Equal(3, p.Active);
        Assert.True(p.OnBar(5, 1));
        Assert.Equal(1, p.Active);
        Assert.False(p.OnBar(6, 1));
    }

    [Fact]
    public void Bars_and_silence_follow_tempo_and_share()
    {
        Assert.Equal(2.0, AudioMix.BarSeconds(120, 4), 6);
        Assert.Equal(0, AudioMix.BarIndex(1.99, 2.0));
        Assert.Equal(1, AudioMix.BarIndex(2.0, 2.0));
        Assert.Equal(180f, AudioMix.SilenceAfter(180f, 0.5f), 3);
        Assert.Equal(0f, AudioMix.SilenceAfter(180f, 0f));
        Assert.True(float.IsPositiveInfinity(AudioMix.SilenceAfter(180f, 1f)));
    }

    [Fact]
    public void Variant_never_repeats_and_jitter_stays_in_range()
    {
        for (int last = 0; last < 3; last++)
            for (int roll = 0; roll < 2; roll++)
            {
                int v = AudioMix.PickVariant(3, last, roll);
                Assert.InRange(v, 0, 2);
                Assert.NotEqual(last, v);
            }
        Assert.Equal(0, AudioMix.PickVariant(1, 0, 5));
        var j = A.Variation;
        foreach (var (u, w) in new[] { (0f, 0f), (1f, 1f), (0.5f, 0.5f) })
        {
            var (pitch, db) = AudioMix.Jitter(j, u, w);
            Assert.InRange(pitch, 1f - j.Pitch - 1e-5f, 1f + j.Pitch + 1e-5f);
            Assert.InRange(db, -j.VolumeDb - 1e-5f, j.VolumeDb + 1e-5f);
        }
        Assert.Equal(0.08f, j.Pitch); // SANAT-SES §10.1: perde ±%8
        Assert.Equal(2f, j.VolumeDb); // duzey ±2 dB
    }

    /// <summary>
    /// Bugunku scenario.json (yapisi ne olursa olsun): kimlikler tekil ve dosya adina uygun; ekrana gelen her Suna
    /// satiri (giris, adim baslangici ve bitisi, gece) bir replige cozulur. Adim alani eski yapida metin, ilkgun
    /// yapisinda kimliktir; ikisi de kabul edilir. Gece replikleri iki yapida da metin olarak ekrana gelir.
    /// </summary>
    [Fact]
    public void Voice_lines_of_todays_scenario_cover_every_suna_line()
    {
        string json = TestData.Read("scenario.json");
        var sc = TestData.Load().Scenario;
        var lines = VoiceLines.FromJson(json, "tr");
        Assert.NotEmpty(lines);
        Assert.Equal(lines.Count, lines.Select(l => l.Id).Distinct().Count());
        Assert.All(lines, l => Assert.Matches("^[a-z0-9_]+$", l.Id));
        Assert.All(lines, l => Assert.False(string.IsNullOrWhiteSpace(l.Text)));

        var said = sc.Intro.Concat(sc.Steps.SelectMany(s => new[] { s.Start, s.Done })).Where(s => !string.IsNullOrWhiteSpace(s));
        Assert.All(said, s => Assert.Contains(lines, l => l.Id == s || l.Text == s!.Trim()));
        Assert.All(sc.NightLines.Values.Where(t => !string.IsNullOrWhiteSpace(t)), t => Assert.Contains(lines, l => l.Text == t.Trim()));
    }

    // ilkgun akisinin yapisi (claude/m1-ilkgun-3d5928): kimlik acik, iki dil ayni dosyada.
    const string IdShape = """
    {
      "intro": ["giris_1", "giris_2"],
      "steps": [ { "id": "mine", "target": 1, "goal": "Tas topla {act:fire}", "start": null, "done": "tas_bitti" } ],
      "nightLineIds": { "alarm": "gece_alarm" },
      "nextOrder": "Siradaki is Defter'de.",
      "nextOrderLine": "siradaki_is",
      "lines": {
        "giris_1": { "tr": "Beni duyuyor musun?", "en": "Can you hear me?" },
        "giris_2": { "tr": " Kapsul saglam. ", "en": "The pod is intact." },
        "tas_bitti": { "tr": "Taslar tamam.", "en": "" },
        "gece_alarm": { "tr": "Bir sey kabariyor!", "en": "Something is swelling!" },
        "siradaki_is": { "tr": "Defteri ac.", "en": "Open the ledger." }
      }
    }
    """;

    [Fact]
    public void Voice_lines_read_explicit_ids_in_both_languages()
    {
        var tr = VoiceLines.FromJson(IdShape, "tr");
        Assert.Equal(new[] { "giris_1", "giris_2", "tas_bitti", "gece_alarm", "siradaki_is" }, tr.Select(l => l.Id));
        Assert.Equal("Kapsul saglam.", tr[1].Text); // kirpilir: ekrandaki metinle eslesir
        Assert.DoesNotContain(tr, l => l.Text.StartsWith("Siradaki is")); // nextOrder ekranda, seslendirilmez

        var en = VoiceLines.FromJson(IdShape, "en");
        Assert.Equal(new[] { "giris_1", "giris_2", "gece_alarm", "siradaki_is" }, en.Select(l => l.Id)); // bos metin atlanir
        Assert.Equal("Can you hear me?", en[0].Text);
    }

    // Eski yapi (af67734): kimlik yok, Turkce metin; kimlik turetilir.
    const string TextShape = """
    {
      "intro": ["Merhaba.", "Kapsul saglam."],
      "steps": [ { "id": "mine", "start": "Tas topla.", "done": "Tamam." }, { "id": "ocak", "start": "", "done": null } ],
      "nightLines": { "alarm": "Alarm!" },
      "nextOrder": "Siradaki is."
    }
    """;

    [Fact]
    public void Voice_lines_derive_ids_from_the_old_text_shape()
    {
        var tr = VoiceLines.FromJson(TextShape, "tr");
        Assert.Equal(new[] { "intro_1", "intro_2", "mine_start", "mine_done", "night_alarm", "next_order" }, tr.Select(l => l.Id));
        Assert.Equal("Tas topla.", tr[2].Text);
        Assert.Empty(VoiceLines.FromJson(TextShape, "en")); // eski yapida Ingilizce metin yok
    }
}
