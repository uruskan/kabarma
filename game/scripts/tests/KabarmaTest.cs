using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Basliksiz sinama --test=kabarma (kabarma3, GDD 14.1-14.6): oyunun gercek gece akisinda
///  1) tam Kabarma KS 1: alarm, 8 dalga + Doruk, turler karisik dogar, yuruyen turler yurur, dusmanlar (sinama vurur,
///     Kabarans.Hit) olur, safak nedeni Dawn, ilk gecis: KS 2 acilir, kart jetonu, rapor;
///  2) KS 2 savunmasiz: Kabaranlar Kazan'a yuklenir, Kazan devrilir, gece o an biter, KS acilmaz;
///  3) Kucuk Kabarma: KS yarisi, Kazan yok, 3 dalga; 4) Tatbikat: Maya yok.
/// Sonuc: "KABARMA RESULT ok|fail ...", cikis kodu 0|1.
/// </summary>
public partial class KabarmaTest : Node
{
    readonly Director _d;
    int _stage;
    float _t, _stageT, _killT;
    int _checks;
    readonly HashSet<string> _species = new();
    bool _doruk;
    KabarmaEnd _end;
    Zipzip _walker;
    Vector3 _walkerFrom;
    float _walked;
    int _peak;

    public KabarmaTest(Director d) { _d = d; ProcessMode = ProcessModeEnum.Always; }

    void Check(bool ok, string what) { if (!ok) Fail(what); _checks++; }

    void Fail(string why)
    {
        GD.Print($"KABARMA RESULT fail stage={_stage} {why} t={_t:0.0}");
        GetTree().Quit(1);
        SetProcess(false);
        throw new OperationCanceledException(why);
    }

    void Next() { _stage++; _stageT = 0; GD.Print($"KABARMA stage {_stage} t={_t:0.0}"); }

    void KillAll()
    {
        foreach (var k in Kabarans.Live.ToList()) Kabarans.Hit(k, 1e6f, _d.Player.GlobalPosition, 0f, 1, false, _d.Night);
        if (_d.Wildlife != null)
            foreach (var s in _d.Wildlife.Swarms.ToList())
                if (IsInstanceValid(s) && !s.Dead) s.Hit(1e6f, _d.Player.GlobalPosition);
    }

    void Watch(KabarmaNight n)
    {
        n.DorukStarted += _ => _doruk = true;
        n.Ended += e => _end = e;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt; _stageT += dt;
        if (_t > 2400f) { try { Fail("zaman asimi"); } catch (OperationCanceledException) { } return; }
        try { Step(dt); } catch (OperationCanceledException) { }
    }

    void Step(float dt)
    {
        var n = _d.Night;
        switch (_stage)
        {
            case 0: // ilk gece (senaryo, --skip=night ile hemen) biter, satis adimi gecer, rapor kapanir, oyun serbest
                _killT -= dt;
                if (_killT <= 0)
                {
                    _killT = 1f;
                    if (_d.NightActive && n?.Plan == null)
                        foreach (var z in Zipzip.All.ToArray()) if (!z.Dead && z.IsInsideTree()) z.Hit(99999, false, Vector3.Zero, 0, false);
                    if (_d.Phase == Director.Act.Dawn && _stageT > 4f) _d.State.Scenario.Report("sell");
                    if (_d.Hud.ReportOpen) _d.CloseReport();
                    if ((int)_stageT % 30 == 0) GD.Print($"KABARMA wait phase={_d.Phase} night={_d.NightActive} report={_d.Hud.ReportOpen}");
                }
                if (_d.Phase != Director.Act.Free || _stageT < 2f) return;
                Check(_d.State.UnlockedKs == 1, $"baslangic KS {_d.State.UnlockedKs}");
                // Olcum kipi (--kabarma_perf=KS, istege bagli --kabarma_stress=KS:oyuncu): tek tam gece, perf satirlari ve goruntuler.
                if (Main.Args.TryGetValue("kabarma_perf", out var pk))
                {
                    Check(_d.StartKabarma(KabarmaKind.Full, int.TryParse(pk, out var pks) ? pks : 10), "olcum gecesi baslamadi");
                    _stage = 20; _stageT = 0;
                    return;
                }
                Check(_d.StartKabarma(KabarmaKind.Full, 1), "tam Kabarma baslamadi");
                Next();
                return;
            case 20: // olcum: Kazan devrilmesin diye cani buyuk (yalniz bu kipte), kimse vurmaz; kalabalik birikir
                if (n == null || n.Plan == null) { if (_stageT > 30f) Fail("olcum gecesi kurulmadi"); return; }
                if (n.Kazan != null) n.Kazan.Hp = 1e9f;
                int live = Kabarans.Live.Count();
                _peak = Mathf.Max(_peak, live);
                if (live >= 60) _d.DebugShot($"kabarma_ks{n.Ks}_kalabalik", 0.2f);
                if (live >= 150) _d.DebugShot($"kabarma_ks{n.Ks}_yogun", 0.2f);
                if (n.InDoruk) _d.DebugShot($"kabarma_ks{n.Ks}_doruk", 3f);
                if (n.Phase != NightPhase.Dawn && _stageT < 1100f) return;
                GD.Print($"KABARMA RESULT ok perf ks={n.Ks} players={n.Plan.Spec.Players} slots={n.Plan.TotalSlots} peak={_peak} t={_t:0}");
                GetTree().Quit(0);
                SetProcess(false);
                return;
            case 1: // aksam -> gece planli mi
                if (n == null || n.Plan == null) { if (_stageT > 30f) Fail("planli gece kurulmadi"); return; }
                Check(n.Kind == KabarmaKind.Full && n.Plan.Waves.Count == 9 && n.Kazan != null, "plan/Kazan");
                Watch(n);
                Next();
                return;
            case 2: // dalgalar: tur cesitliligi, yuruyus, oldurme
                foreach (var k in Kabarans.Live) _species.Add(k.State.Species);
                if (_walker == null || !IsInstanceValid(_walker) || _walker.Dead)
                {
                    _walker = Kabarans.Live.OfType<Zipzip>().FirstOrDefault(z => z.Walks);
                    if (_walker != null) _walkerFrom = _walker.GlobalPosition;
                }
                else _walked = Mathf.Max(_walked, _walker.GlobalPosition.DistanceTo(_walkerFrom));
                _killT -= dt;
                if (_killT <= 0 && _walked > 2f) { _killT = 1.5f; KillAll(); }
                else if (_killT <= 0 && _stageT > 25f) { _killT = 1.5f; KillAll(); }
                if (n.Phase != NightPhase.Dawn) return;
                Check(_end == KabarmaEnd.Dawn, $"safak nedeni {_end}");
                Check(_doruk, "Doruk gelmedi");
                Check(_species.Count >= 3, $"tur cesidi {_species.Count} ({string.Join(",", _species)})");
                Check(_walked > 2f, $"yuruyen tur yurumedi ({_walked:0.0} m)");
                Check(_d.State.Kabarma.HighestCleared == 1 && _d.State.UnlockedKs == 2 && _d.State.Kabarma.CardTokens == 1, "ilk gecis odulu");
                Check(n.Kills > 0 && n.Maya > 0 && n.KillsBy.GetValueOrDefault(1) > 0, $"oldurme/Maya kills={n.Kills} maya={n.Maya}");
                GD.Print($"KABARMA ks1 kills={n.Kills} maya={n.Maya:0} species={string.Join(",", _species.OrderBy(s => s))} walked={_walked:0.0}");
                Next();
                return;
            case 3: // rapor kapanip serbest kalinca KS 2, savunmasiz
                if (_stageT < 8f) return;
                if (_d.Hud.ReportOpen) { _d.CloseReport(); return; }
                if (_d.Phase != Director.Act.Free && _d.Phase != Director.Act.Report) return;
                _end = KabarmaEnd.None;
                Check(_d.StartKabarma(KabarmaKind.Full, 2), "KS 2 baslamadi");
                Next();
                return;
            case 4:
                if (n == null || n.Plan == null || n.Ks != 2) { if (_stageT > 30f) Fail("KS 2 gece kurulmadi"); return; }
                Watch(n);
                Next();
                return;
            case 5: // kimse vurmaz: Kabaranlar Kazan'a yuklenir
                if (n.Phase != NightPhase.Dawn) { if (_stageT > 900f) Fail($"Kazan devrilmedi hp={n.Kazan?.Hp:0}"); return; }
                Check(_end == KabarmaEnd.KazanToppled && n.Kazan.Toppled, $"bitis {_end}");
                Check(_d.State.Kabarma.HighestCleared == 1 && _d.State.Kabarma.Toppled == 1, "devrilen gece KS acti");
                GD.Print($"KABARMA ks2 toppled t={_stageT:0} maya={n.Maya:0}");
                Next();
                return;
            case 6:
                if (_stageT < 8f) return;
                if (_d.Hud.ReportOpen) { _d.CloseReport(); return; }
                if (_d.Phase != Director.Act.Free && _d.Phase != Director.Act.Report) return;
                if (_d.Player.Down) return;
                Check(_d.StartKabarma(KabarmaKind.Small, 4, _d.OcakPos), "Kucuk Kabarma baslamadi");
                Next();
                return;
            case 7:
                if (n == null || n.Kind != KabarmaKind.Small) { if (_stageT > 30f) Fail("kucuk gece kurulmadi"); return; }
                Check(n.Ks == 2 && n.Kazan == null && n.Plan.Waves.Count == 3, $"kucuk ks={n.Ks} waves={n.Plan.Waves.Count}");
                Next();
                return;
            case 8:
                _killT -= dt;
                if (_killT <= 0) { _killT = 1f; KillAll(); }
                if (n.Phase != NightPhase.Dawn) { if (_stageT > 600f) Fail("kucuk gece bitmedi"); return; }
                GD.Print($"KABARMA small kills={n.Kills}");
                Next();
                return;
            case 9:
                if (_stageT < 8f) return;
                if (_d.Hud.ReportOpen) { _d.CloseReport(); return; }
                if (_d.Phase != Director.Act.Free && _d.Phase != Director.Act.Report) return;
                Check(_d.StartKabarma(KabarmaKind.Drill, 1), "Tatbikat baslamadi");
                Next();
                return;
            case 10:
                if (n == null || n.Kind != KabarmaKind.Drill) { if (_stageT > 30f) Fail("tatbikat kurulmadi"); return; }
                _killT -= dt;
                if (_killT <= 0) { _killT = 1f; KillAll(); }
                if (n.Kills < 3) { if (_stageT > 300f) Fail("tatbikatta oldurme yok"); return; }
                Check(n.Maya == 0f, $"tatbikat Maya {n.Maya}");
                GD.Print($"KABARMA RESULT ok checks={_checks} species={_species.Count} t={_t:0}");
                GetTree().Quit(0);
                SetProcess(false);
                return;
        }
    }
}
