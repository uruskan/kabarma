using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Basliksiz sinama --test=ilerleme (ilerleme akisi): is emri odemesi (Kredi, Veri), Defter'in uc alt sekmesi gercek
/// girdiyle (sol/sag, yukari/asagi, 1, Enter), Ustalik secimi ve takip zinciri sabitleme, basarim bildirimi.
/// --ilerleme_shots=klasor: pencereli kosuda her sekmenin goruntusunu kaydeder (progress/m2/ilerleme).
/// Sonuc: "ILERLEME RESULT ok|fail ...", cikis kodu 0|1. Bot ilk is emri adimini ayrica tum oyun yolundan tamamlar (Bot.Ilerleme.cs).
/// </summary>
public partial class IlerlemeTest : Node
{
    readonly Director _d;
    int _stage, _checks;
    float _t, _stageT, _inputT;
    readonly Queue<Action> _inputs = new();

    public IlerlemeTest(Director d) { _d = d; ProcessMode = ProcessModeEnum.Always; }

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless") GetTree().Root.Size = new Vector2I(1600, 900);
    }

    void Check(bool ok, string what)
    {
        if (!ok) { GD.Print($"ILERLEME RESULT fail stage={_stage} {what} t={_t:0.0}"); GetTree().Quit(1); SetProcess(false); throw new OperationCanceledException(what); }
        _checks++;
    }

    static void Key(Key k, bool pressed) => Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = pressed });
    void Tap(Key k) { _inputs.Enqueue(() => Key(k, true)); _inputs.Enqueue(() => Key(k, false)); }
    void Then(Action a) => _inputs.Enqueue(a);
    void Next() { _stage++; _stageT = 0; GD.Print($"ILERLEME stage {_stage} t={_t:0.0}"); }

    void Shot(string name)
    {
        if (!Main.Args.TryGetValue("ilerleme_shots", out var dir) || DisplayServer.GetName() == "headless") return;
        var root = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../" + (dir == "1" ? "progress/m2/ilerleme" : dir);
        DirAccess.MakeDirRecursiveAbsolute(root);
        GetViewport().GetTexture().GetImage().SavePng($"{root}/{name}.png");
        GD.Print($"ILERLEME SHOT {root}/{name}.png");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt; _stageT += dt;
        if (_t > 120f) { try { Check(false, "zaman asimi"); } catch (OperationCanceledException) { } return; }
        _inputT -= (float)GetProcessDeltaTime() / Mathf.Max(0.01f, (float)Engine.TimeScale);
        if (_inputs.Count > 0) { if (_inputT <= 0) { try { _inputs.Dequeue()(); } catch (OperationCanceledException) { } _inputT = 0.08f; } return; }
        try { Step(); } catch (OperationCanceledException) { }
    }

    void Step()
    {
        var d = _d; var s = d.State; var hud = d.Hud;
        switch (_stage)
        {
            case 0:
                if (_stageT < 0.5f) break;
                Check(!s.Orders.DefterOpen && s.Orders.Chains.Count == 12, "12 zincir, Defter kapali olmali");
                Check(s.Achievements.Count == 14, "basarimlar: 12 zincir + 2");
                // Is emri odemesi: Defter acilinca adim biter bitmez Kredi ve Veri
                s.Orders.OpenDefter();
                int c0 = s.Credits, v0 = s.Research.Veri;
                s.Orders.Report("swollen_crop", 5);
                Check(s.Credits == c0 + 100 && s.Research.Veri == v0 + 10, $"adim odulu: kredi {s.Credits - c0} veri {s.Research.Veri - v0}");
                Check(s.Orders.Tracked?.Def.Id == "salgam_baronu", "takip: Salgam Baronu");
                // Ustalik: ciftcilik 10. seviye, secim bekliyor
                s.Mastery.AddXp("ciftcilik", s.Mastery.Threshold(10));
                Check(s.Mastery.PendingTiers("ciftcilik").Contains(10), "ciftcilik 10. seviye secim bekliyor");
                hud.OpenInventory(1);
                Next();
                break;
            case 1: // Defter acik, Is emirleri sekmesi
                if (_stageT < 0.3f) break;
                Check(hud.InventoryOpen && hud.InventoryPage == 1 && hud.DefterTab == 0, "Defter sayfasi acik, sekme 0");
                var txt = hud.DefterText();
                Check(txt.Contains("Şalgam Baronu") && txt.Contains("Çıraklık") && txt.Contains("Devler Ligi") && txt.Contains("zincir daha"), "zincir satirlari: " + txt.Length);
                Shot("defter_is_emirleri");
                Tap(Godot.Key.Right);          // Ustalik sekmesi
                Then(() => { });
                Next();
                break;
            case 2:
                if (_stageT < 0.3f) break;
                Check(hud.DefterTab == 1, "Sag tusu Ustalik sekmesine gecmeli");
                Tap(Godot.Key.Down); Tap(Godot.Key.Down); // kazi, hasat, ciftcilik
                Next();
                break;
            case 3:
                if (_stageT < 0.3f) break;
                Check(hud.DefterText().Contains("SEÇ!") && hud.DefterText().Contains("Bol Hasat"), "Ustalik sayfasi: secim bekleyen dal ve ozellik adi");
                Shot("defter_ustalik");
                Tap(Godot.Key.Key1);           // A: Bol Hasat
                Next();
                break;
            case 4:
                if (_stageT < 0.3f) break;
                Check(s.Mastery.Has("ciftcilik_10a_bol_hasat") && !s.Mastery.PendingTiers("ciftcilik").Any(), "1 tusu A ozelligini secmeli");
                Check(Mathf.Abs(s.Mastery.Sum("crop_yield") - 0.20f) < 0.001f, "Bol Hasat + 10 seviye = %20 verim");
                Tap(Godot.Key.Right);          // Basarimlar
                Next();
                break;
            case 5:
                if (_stageT < 0.3f) break;
                Check(hud.DefterTab == 2 && hud.DefterText().Contains("İlk Gece"), "Basarimlar sekmesi");
                Shot("defter_basarimlar");
                Tap(Godot.Key.Right); Tap(Godot.Key.Right); // Cipler sekmesi uzerinden Is emirlerine don
                Tap(Godot.Key.Down); Tap(Godot.Key.Down); // ciraklik, salgam_baronu, yesil_parmak
                Tap(Godot.Key.Enter);
                Next();
                break;
            case 6:
                if (_stageT < 0.3f) break;
                Check(hud.DefterTab == 0 && s.Orders.PinnedId == "yesil_parmak", $"Enter takip zincirini sabitlemeli (pin={s.Orders.PinnedId})");
                Check(s.Orders.Tracked?.Def.Id == "yesil_parmak", "takip: Yesil Parmak");
                // Basarim: ilk gece sayaci
                s.Orders.Report("kabarma_survived");
                Check(s.Achievements.IsUnlocked("ilk_gece"), "ilk_gece basarimi");
                hud.CloseInventory();
                GD.Print($"ILERLEME RESULT ok checks={_checks} credits={s.Credits} veri={s.Research.Veri} t={_t:0.0}");
                GetTree().Quit(0);
                SetProcess(false);
                break;
        }
    }
}
