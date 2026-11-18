using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kartlar sinamasi (--test=kartlar, M3): gercek gece, gercek tuslar (Input.ParseInputEvent).
/// 1. Ilk teklifte U ile yenileme, K + X ile surgun, Z ile secim (GDD 15.1).
/// 2. Sonraki tekliflerde varsa yorunge ya da beden karti, yoksa ilki secilir.
/// 3. Sinama (bot degil) uc karti Grant ile verir ve oyundaki karsiliklarini olcer:
///    Yumurta Topu dusmana vurur, Kabuk Player.Hurt'u emer, Cam Top can tavanini %40 dusurur.
/// 4. Safakta can tavani tabana doner.
/// Asi ve 5. rutbe ilk gecenin 3 seviyesinde durustce ulasilamaz; onlar Sim senaryolarinda (CardScenarioTests).
/// Sonuc: "KARTLAR RESULT ok=N fail=M", basarisizlikta cikis kodu 1.
/// </summary>
public partial class KartlarTest : Node
{
    Director _d;
    int _ok, _fail, _stage;
    float _t, _stageT, _inputT;
    readonly Queue<Action> _inputs = new();
    float _baseHp;
    int _orbitalHpBefore = -1;
    float _zipHpBefore, _orbitalAt;
    bool _granted, _done;

    public KartlarTest() { Name = "KartlarTest"; ProcessMode = ProcessModeEnum.Always; }

    void Check(bool c, string what) { if (c) _ok++; else _fail++; GD.Print($"KARTLAR {(c ? "OK  " : "FAIL")} {what}"); }
    static void Key(Key k, bool p) => Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = p });
    void Tap(Key k) { _inputs.Enqueue(() => Key(k, true)); _inputs.Enqueue(() => Key(k, false)); }
    void Then(Action a) => _inputs.Enqueue(a);
    /// <summary>Pencereli kosuda kanit goruntusu: progress/m3/kartlar/.</summary>
    void Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        var dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m3/kartlar/";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        GetViewport().GetTexture().GetImage().SavePng(dir + name + ".png");
        GD.Print("KARTLAR shot " + name);
    }

    void Next() { _stage++; _stageT = 0; GD.Print($"KARTLAR stage {_stage} t={_t:0.0}"); }

    void Finish()
    {
        if (_done) return;
        _done = true;
        GD.Print($"KARTLAR RESULT ok={_ok} fail={_fail}");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    void KillAll() { foreach (var z in Zipzip.All.ToArray()) if (!z.Dead && z.IsInsideTree()) z.Hit(99999, false, Vector3.Zero, 0, false); }

    public override void _Process(double delta)
    {
        if (_done) return;
        float dt = (float)delta;
        _t += dt; _stageT += dt;
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d == null) return;
        if (_t > 800f) { Check(false, $"zaman asimi stage={_stage}"); Finish(); return; }
        _inputT -= (float)GetProcessDeltaTime() / Mathf.Max(0.01f, (float)Engine.TimeScale);
        if (_inputs.Count > 0) { if (_inputT <= 0) { _inputs.Dequeue()(); _inputT = 0.08f; } return; }
        Step();
    }

    void Step()
    {
        var d = _d;
        switch (_stage)
        {
            case 0:
                if (d.Phase == Director.Act.Night && d.Night != null && d.Player != null) { _baseHp = d.Player.MaxHp; Next(); }
                else if (_stageT > 200f) { Check(false, "gece baslamadi"); Finish(); }
                break;
            case 1: // ilk teklif: yenile, surgun, sec (gercek tuslar)
            {
                var n = d.Night;
                if (n.Phase == NightPhase.Waves) KillAll();
                if (n.Offer == null || n.Offer.Age < 0.2f) { if (_stageT > 300f) { Check(false, "teklif gelmedi"); Finish(); } break; }
                var before = n.Offer;
                Check(before.Cards[0].Id == "catal", "ilk teklifin ilki Catal (GDD 24)");
                Tap(Godot.Key.U);
                Then(() =>
                {
                    Check(n.Hand.RerollsLeft == 1 && n.Offer != null && n.Offer != before, $"U ile yenileme (kalan {n.Hand.RerollsLeft})");
                });
                Tap(Godot.Key.K);
                Then(() => Check(d.BanishMode, "K surgun kipini acti"));
                Then(() => Shot("kart_teklifi_surgun"));
                string gone = null;
                Then(() => gone = n.Offer?.Cards.Count > 1 ? n.Offer.Cards[1].Id : null);
                Tap(Godot.Key.X);
                Then(() =>
                {
                    Check(n.Hand.BanishesLeft == 1 && gone != null && n.Hand.Banished.Contains(gone) && n.Offer.Cards.All(c => c.Id != gone), $"X ile {gone} surgun edildi");
                    Check(!n.Hand.Pool().Any(c => c.Id == gone), "surgun edilen kart havuzda yok");
                });
                Tap(Godot.Key.Z);
                Then(() => Check(n.CardsCount == 1, "Z ile kart secildi"));
                Next();
                break;
            }
            case 2: // kalan teklifler ve sinamanin verdigi uc kart
            {
                var n = d.Night;
                if (n.Phase == NightPhase.Dawn) { Next(); break; }
                if (!_granted)
                {
                    _granted = true;
                    n.Grant("yumurta_topu"); n.Grant("kabuk"); n.Grant("cam_top"); // SINAMA: oyun karsiligini olcmek icin (bot degil)
                    Then(() =>
                    {
                        Check(Mathf.IsEqualApprox(d.Player.MaxHp, _baseHp * 0.6f), $"Cam Top can tavani {d.Player.MaxHp} (taban {_baseHp})");
                        float hp = d.Player.Hp;
                        d.Player.Hurt(10f, d.Player.GlobalPosition + Vector3.Forward);
                        Check(Mathf.IsEqualApprox(d.Player.Hp, hp), $"Kabuk 10 hasari emdi (can {hp} -> {d.Player.Hp}, kalkan {n.Hand.Shield})");
                    });
                    break;
                }
                // Yumurta Topu: dalga surerken dusmanlari yorunge vursun (sinama vurmaz, 14 m icinde)
                if (n.Phase == NightPhase.Waves && _orbitalHpBefore < 0)
                {
                    var near = Zipzip.All.Where(z => !z.Dead && z.Center.DistanceTo(d.Player.GlobalPosition) < 13f).ToList();
                    if (near.Count == 0)
                    {
                        // en yakin dusmani oyuncunun 6 m yanina getir (sinama duzeni; vurmuyor)
                        var z0 = Zipzip.All.FirstOrDefault(z => !z.Dead);
                        if (z0 != null) z0.GlobalPosition = d.Player.GlobalPosition + new Vector3(6, 0, 0);
                        break;
                    }
                    _orbitalHpBefore = near.Count;
                    _zipHpBefore = near.Sum(z => z.Hp);
                    _orbitalAt = _t;
                    break;
                }
                if (_orbitalHpBefore >= 0 && _orbitalHpBefore < 1000)
                {
                    if (_t - _orbitalAt < 4f) break;
                    Check(d.OrbitalHits > 0, $"Yumurta Topu {d.OrbitalHits} kez vurdu");
                    _orbitalHpBefore = 1000;
                }
                if (_orbitalHpBefore >= 1000 && n.Phase == NightPhase.Waves) KillAll();
                if (n.Offer != null && n.Offer.Age > 0.3f)
                {
                    int pick = n.Offer.Cards.ToList().FindIndex(c => c.Class is "yorunge" or "beden");
                    Tap(pick switch { 1 => Godot.Key.X, 2 => Godot.Key.C, _ => Godot.Key.Z });
                }
                if (_stageT > 500f) { Check(false, "safak gelmedi"); Finish(); }
                break;
            }
            case 3: // safak
            {
                var n = d.Night;
                Check(Mathf.IsEqualApprox(d.Player.MaxHp, _baseHp), $"safakta can tavani tabana dondu ({d.Player.MaxHp})");
                Check(n.Stats.IsBase && n.Ranks.Count == 0, "safakta kartlar sondu");
                Check(n.CardsAtDawn.Any(c => c.Id == "cam_top") && n.CardsAtDawn.Count >= 4, $"rapor listesi: {string.Join(",", n.CardsAtDawn.Select(c => c.Id + ":" + c.Rank))}");
                d.Hud.OpenCardBook();
                Then(() => Shot("kart_defteri"));
                Then(() => d.Hud.HideMenu());
                Then(Finish);
                Check(d.State.CardBook.Open.Count() == 24, $"acik kartlar: 20 + ebeveynleri bastan acik 4 asi ({d.State.CardBook.Open.Count()})");
                Next();
                break;
            }
        }
    }
}
