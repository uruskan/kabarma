using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Basliksiz sinama --test=gece (gecehud akisi): ilk gece (kartlar, Catal, Cevik, safakta sonme), Hasat Raporu ve Defter,
/// envanter izgarasi, takvim (normal gece, uyku), F1'den Kucuk Kabarma yeniden. Girdiler gercek olay olarak verilir
/// (Input.ParseInputEvent): Tab, yon, sayi, Q, Enter, F1 ve oyun kolu (d-pad, LB/RB, X). Dusmanlari sinama vurur (bot yok); satis adimi senaryoya
/// bildirilir (satisin kendisi botun "sell" adiminda sinanir). Sonuc: "GECE RESULT ok|fail ...", cikis kodu 0|1.
/// </summary>
public partial class GeceHudTest : Node
{
    readonly Director _d;
    int _stage;
    float _t, _stageT;
    int _checks;
    bool _firstOfferSeen;
    readonly Queue<Action> _inputs = new();
    float _inputT;

    public GeceHudTest(Director d) { _d = d; ProcessMode = ProcessModeEnum.Always; }

    void Check(bool ok, string what)
    {
        if (!ok) Fail(what);
        _checks++;
    }

    void Fail(string why)
    {
        GD.Print($"GECE RESULT fail stage={_stage} {why} t={_t:0.0}");
        GetTree().Quit(1);
        SetProcess(false);
        throw new OperationCanceledException(why);
    }

    void Next() { _stage++; _stageT = 0; GD.Print($"GECE stage {_stage} t={_t:0.0}"); }

    static void Key(Key k, bool pressed) => Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = pressed });
    void Tap(Key k) { _inputs.Enqueue(() => Key(k, true)); _inputs.Enqueue(() => Key(k, false)); }
    void Then(Action a) => _inputs.Enqueue(a);
    static void Pad(JoyButton b, bool pressed) => Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = pressed, Device = 0 });
    void PadTap(JoyButton b) { _inputs.Enqueue(() => Pad(b, true)); _inputs.Enqueue(() => Pad(b, false)); }

    void KillAll()
    {
        foreach (var z in Zipzip.All.ToArray()) if (!z.Dead && z.IsInsideTree()) z.Hit(99999, false, Vector3.Zero, 0, false);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt; _stageT += dt;
        if (_t > 900f) { try { Fail("zaman asimi"); } catch (OperationCanceledException) { } return; }
        // Girdi kuyrugu: her olay ayri karede (JustPressed'in bir kare surmesi icin)
        _inputT -= (float)GetProcessDeltaTime() / Mathf.Max(0.01f, (float)Engine.TimeScale);
        if (_inputs.Count > 0) { if (_inputT <= 0) { try { _inputs.Dequeue()(); } catch (OperationCanceledException) { } _inputT = 0.08f; } return; }
        try { Step(); } catch (OperationCanceledException) { }
    }

    void Step()
    {
        var d = _d; var s = d.State;
        switch (_stage)
        {
            case 0: // ilk gun: F1 dugmesi daha ise yaramaz; geceyi bekle
                if (_stageT < 0.5f) break;
                foreach (var a in new[] { "ui_accept", "ui_cancel", "ui_right", "ui_down", "bag", "card_1" })
                    GD.Print($"GECE input {a}: " + (InputMap.HasAction(a) ? string.Join(" | ", InputMap.ActionGetEvents(a).Select(e => e.AsText())) : "(eylem yok)"));
                Check(!d.RestartKucukKabarma(), "ilk geceden once yeniden baslatma reddedilmeli");
                Check(!s.Calendar.Running, "ilk geceden once saat islememeli");
                Next();
                break;
            case 1:
                if (d.Phase == Director.Act.Night && d.Night != null) { Next(); break; }
                if (_stageT > 120f) Fail("gece baslamadi");
                break;
            case 2: // ilk gece: vur, kart sec
            {
                var n = d.Night;
                if (n.Phase == NightPhase.Dawn) { Check(_firstOfferSeen, "ilk teklif gorulmedi"); Next(); break; }
                if (n.Motes.Count > 0) _sawOz = true; // M1 karari 19: oldurmede Oz yere duser
                if (n.Phase == NightPhase.Waves) KillAll();
                if (n.Offer != null && n.Offer.Age > 0.2f)
                {
                    if (!_firstOfferSeen)
                    {
                        _firstOfferSeen = true;
                        Check(n.Offer.Cards[0].Id == "catal", "ilk teklifin ilki Catal degil: " + string.Join(",", n.Offer.Cards.Select(c => c.Id)));
                        Check(n.Offer.Cards.Count == 3, "teklif 3 kart olmali");
                    }
                    d.ChooseCard(0);
                    if (n.Rank("cevik") == 0 && n.CardsCount == 1)
                    {
                        n.Grant("cevik"); // Cevik'in oyuna ulastigini gormek icin (teklifte cikmasi sansa bagli)
                        Then(() => Check(Mathf.IsEqualApprox(d.Player.SpeedMult, n.Stats.MoveSpeed(1f)) && d.Player.SpeedMult > 1.05f, $"Cevik oyuncu hizina yansimadi ({d.Player.SpeedMult})"));
                    }
                }
                if (_stageT > 400f) Fail("safak gelmedi");
                break;
            }
            case 3: // safak: kartlar soner, Cevik biter; senaryo satis adiminda
            {
                var n = d.Night;
                Check(n.Stats.IsBase, "safakta kart etkileri sonmedi");
                Check(_sawOz && n.Motes.Count == 0 && n.Oz > 0, "Oz yere dusmedi ya da safakta yerde kaldi");
                Check(Mathf.IsEqualApprox(d.Player.SpeedMult, 1f), "safakta oyuncu hizi tabana donmedi");
                Check(n.CardsAtDawn.Any(c => c.Id == "catal"), "rapor listesinde Catal yok");
                Check(s.Calendar.Running && s.Calendar.Day == s.Data.Calendar.FirstDay && s.Calendar.Part.Id == "sabah", "ilk safakta takvim baslamadi");
                if (s.Scenario.Current?.Id == "sell") s.Scenario.Report("sell"); // satisin kendisi botta
                Next();
                break;
            }
            case 4: // Hasat Raporu (ilk gece: satistan sonra) ve Defter
                if (!d.Hud.ReportOpen) { if (_stageT > 30f) Fail("rapor acilmadi"); break; }
                {
                    var r = s.LastReport;
                    Check(r != null && r.NightNumber == 1 && r.Kills > 0 && r.Cards.Count > 0 && r.SwollenTotal > 0, "rapor alanlari eksik");
                    Check(s.Orders.DefterOpen && s.Orders.Tracked?.Def.Id == "salgam_baronu", "Defter acilmadi ya da siradaki is emri yanlis");
                    Check(s.Orders.Chain("ciraklik")!.StepDone(0) && s.Orders.Chain("ciraklik")!.StepDone(3), "Ciraklik: ilk tarla ve ilk Kabarma isaretlenmeli");
                }
                Tap(global::Godot.Key.Enter); // rapor kapanir
                Next();
                break;
            case 5:
                Check(!d.Hud.ReportOpen && d.Phase == Director.Act.Free, "Enter raporu kapatmadi");
                Tap(global::Godot.Key.Tab); // envanter
                Then(() => Check(d.Hud.InventoryOpen && d.Hud.InventoryPage == 0, "Tab envanteri acmadi"));
                Then(() => Check(d.Hud.InventorySelected == 0, "secim ilk yuvada baslamali"));
                Tap(global::Godot.Key.Right);
                Then(() => Check(d.Hud.InventorySelected == 1, "sag ok secimi ilerletmedi"));
                Tap(global::Godot.Key.Key8);
                Then(() =>
                {
                    var id = s.Backpack.Contents.Count > 1 ? s.Backpack.Contents[1].id : null;
                    Check(id != null && s.Belt.At(7) == id, $"8 tusu secileni 8. kemer yuvasina koymadi ({id} / {s.Belt.At(7)})");
                });
                Tap(global::Godot.Key.Q);
                Then(() => Check(d.Hud.InventoryPage == 1, "Q Defter sayfasina gecmedi"));
                Tap(global::Godot.Key.Tab);
                Then(() => Check(!d.Hud.InventoryOpen && d.Player.MoveOverride == null, "Tab envanteri kapatmadi"));
                Next();
                break;
            case 6: // takvim: normal gece ve uyku
            {
                var c = s.Calendar;
                Events.RaiseSlept();
                Check(c.Day == s.Data.Calendar.FirstDay && c.NightsSkipped == 0, "gunduz uyku geceyi atlatmamali");
                c.SetTimeOfDay(0.9f);
                Check(c.IsNight && c.Part.Id == "gece", "saat geceye alinamadi");
                int day = c.Day;
                Events.RaiseSlept();
                Check(c.Day == day + 1 && !c.IsNight && c.NightsSkipped == 1, "uyku normal geceyi atlatmadi");
                Next();
                break;
            }
            case 7: // F1 paneli: Kucuk Kabarma yeniden (gercek tuslar: F1, Enter)
                Tap(global::Godot.Key.F1);
                Tap(global::Godot.Key.Enter);
                Then(() => Check(d.Phase is Director.Act.Dusk or Director.Act.Night, $"F1 dugmesi geceyi baslatmadi (evre {d.Phase})"));
                Tap(global::Godot.Key.F1);
                Next();
                break;
            case 8:
                if (d.Phase == Director.Act.Night && d.Night != null && s.NightsStarted == 2) { _clockAtReplay = s.Calendar.Seconds; Next(); break; }
                if (_stageT > 30f) Fail("yeniden oynanan gece baslamadi");
                break;
            case 9:
            {
                var n = d.Night;
                if (n.Phase == NightPhase.Waves) KillAll();
                if (n.Offer != null && n.Offer.Age > 0.2f) d.ChooseCard(0);
                Check(Mathf.IsEqualApprox(s.Calendar.Seconds, _clockAtReplay), "Kabarma suresince saat durmali");
                if (n.Phase == NightPhase.Dawn) Next();
                if (_stageT > 400f) Fail("yeniden oynanan gecede safak gelmedi");
                break;
            }
            case 10: // yeniden oynanan gecede rapor safakta acilir
                if (!d.Hud.ReportOpen) { if (_stageT > 30f) Fail("ikinci rapor safakta acilmadi"); break; }
                Check(s.LastReport?.NightNumber == 2, "ikinci raporun gece numarasi yanlis");
                // Kolun A'si raporu kapatamaz: Godot'nun ui_accept'inde kol olayi yok, baglama girdi akisinin input.json'unda.
                Tap(global::Godot.Key.Enter);
                Next();
                break;
            case 11:
                if (_stageT < 2f) break;
                Check(!d.Hud.ReportOpen && d.Phase == Director.Act.Free, "ikinci rapor kapanmadi");
                Check(s.Calendar.Seconds > _clockAtReplay, "safaktan sonra saat devam etmedi");
                // Oyun kolu ile envanter: d-pad secer, RB/LB sayfa degistirir, X (interact) kapatir. Cantayi acan "bag"
                // ile A/B (ui_accept/ui_cancel) kol baglamalari girdi akisinin input.json'unda; bu dalda yok: Tab ile acilir.
                Tap(global::Godot.Key.Tab);
                Then(() => { Check(d.Hud.InventoryOpen && d.Hud.InventoryPage == 0, "envanter acilmadi"); _sel0 = d.Hud.InventorySelected; });
                PadTap(JoyButton.DpadRight);
                Then(() => Check(d.Hud.InventorySelected == _sel0 + 1, $"kolun d-pad sagi secimi ilerletmedi ({_sel0} -> {d.Hud.InventorySelected})"));
                PadTap(JoyButton.DpadDown);
                Then(() => Check(d.Hud.InventorySelected == _sel0 + 1 + 6, $"kolun d-pad asagisi bir satir inmedi ({d.Hud.InventorySelected})"));
                PadTap(JoyButton.RightShoulder);
                Then(() => Check(d.Hud.InventoryPage == 1, "kolun RB'si Defter sayfasina gecmedi"));
                PadTap(JoyButton.LeftShoulder);
                Then(() => Check(d.Hud.InventoryPage == 0, "kolun LB'si Canta sayfasina donmedi"));
                PadTap(JoyButton.X);
                Then(() => Check(!d.Hud.InventoryOpen, "kolun X dugmesi envanteri kapatmadi"));
                Next();
                break;
            case 12:
                GD.Print($"GECE RESULT ok checks={_checks} nights={s.NightsStarted} day={s.Calendar.Day} t={_t:0.0}");
                GetTree().Quit(0);
                SetProcess(false);
                break;
        }
    }

    float _clockAtReplay;
    int _sel0;
    bool _sawOz;
}
