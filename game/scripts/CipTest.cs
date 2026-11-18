using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Basliksiz sinama --test=cip (cip akisi): Defter'in Cipler sekmesi gercek girdiyle (sag, yukari/asagi, Enter): cip takma,
/// yuva siniri, cikarma, Kabarma kilidi; etkinin Capa'ya yansimasi; Ata dususu. --cip_shots=klasor: pencereli kosuda
/// goruntu (progress/m3/cip). Sonuc: "CIP RESULT ok|fail ...", cikis kodu 0|1.
/// </summary>
public partial class CipTest : Node
{
    readonly Director _d;
    int _stage, _checks;
    float _t, _stageT, _inputT;
    readonly Queue<Action> _inputs = new();

    public CipTest(Director d) { _d = d; ProcessMode = ProcessModeEnum.Always; }

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless") GetTree().Root.Size = new Vector2I(1600, 900);
    }

    void Check(bool ok, string what)
    {
        if (!ok) { GD.Print($"CIP RESULT fail stage={_stage} {what} t={_t:0.0}"); GetTree().Quit(1); SetProcess(false); throw new OperationCanceledException(what); }
        _checks++;
    }

    static void Key(Key k, bool pressed) => Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = pressed });
    void Tap(Key k, int times = 1) { for (int i = 0; i < times; i++) { _inputs.Enqueue(() => Key(k, true)); _inputs.Enqueue(() => Key(k, false)); } }
    void Next() { _stage++; _stageT = 0; GD.Print($"CIP stage {_stage} t={_t:0.0}"); }

    void Shot(string name)
    {
        if (!Main.Args.TryGetValue("cip_shots", out var dir) || DisplayServer.GetName() == "headless") return;
        var root = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../" + (dir == "1" ? "progress/m3/cip" : dir);
        DirAccess.MakeDirRecursiveAbsolute(root);
        GetViewport().GetTexture().GetImage().SavePng($"{root}/{name}.png");
        GD.Print($"CIP SHOT {root}/{name}.png");
    }

    public override void _Process(double delta)
    {
        _t += (float)delta; _stageT += (float)delta;
        if (_t > 120f) { try { Check(false, "zaman asimi"); } catch (OperationCanceledException) { } return; }
        _inputT -= (float)GetProcessDeltaTime() / Mathf.Max(0.01f, (float)Engine.TimeScale);
        if (_inputs.Count > 0) { if (_inputT <= 0) { try { _inputs.Dequeue()(); } catch (OperationCanceledException) { } _inputT = 0.08f; } return; }
        try { Step(); } catch (OperationCanceledException) { }
    }

    void Step()
    {
        var d = _d; var s = d.State; var b = s.Chips; var hud = d.Hud;
        switch (_stage)
        {
            case 0:
                if (_stageT < 0.5f) break;
                Check(b.Locked == false && b.Capacity() == 1, "baslangic: kilitsiz, Mk.I tek yuva");
                b.Add("kritik", "rare"); b.Add("hiz", "epic"); b.Add("guc", "common"); b.Add("damar_goren", "common"); b.Add("seken", "legendary");
                hud.OpenInventory(1);
                Tap(Godot.Key.Right, 3);                 // Cipler sekmesi
                Next();
                break;
            case 1:
                if (_stageT < 0.3f) break;
                Check(hud.InventoryOpen && hud.DefterTab == 3, "Cipler sekmesi acilmali");
                var txt = hud.DefterText();
                Check(txt.Contains("ÇİP DEPOSU") && txt.Contains("Seken · Efsane") && txt.Contains("satır daha"), "depo satirlari (pencere)");
                Shot("cipler_depo");
                // 8 baslik x (baslik + 1 yuva) = 16 satir; 16. satir depodaki ilk cip (en nadir: Seken Efsane)
                Tap(Godot.Key.Down, 16);
                Next();
                break;
            case 2:
                if (_stageT < 0.3f) break;
                Tap(Godot.Key.Enter);                    // aktif baslik Tirpan Isini: Seken (Efsane) takilir
                Next();
                break;
            case 3:
                if (_stageT < 0.3f) break;
                var seken = b.On("tirpan_isini").FirstOrDefault();
                Check(seken != null && seken.Type == "seken" && seken.Rarity == "legendary", "Enter depodaki ilk cipi aktif basliga takmali");
                Check(Math.Abs(b.Stat("tirpan_isini", "bounce_count") - 1f) < 0.001f, "Efsane ek etkisi");
                Tap(Godot.Key.Enter);                    // sonraki depo cipi (Hiz): yuva dolu
                Next();
                break;
            case 4:
                if (_stageT < 0.3f) break;
                Check(b.On("tirpan_isini").Count() == 1, "Mk.I'de tek yuva: ikinci cip takilmamali");
                Shot("cipler_takili");
                Tap(Godot.Key.Up, 15);                   // yuva satirina (indeks 1) don
                Next();
                break;
            case 5:
                if (_stageT < 0.3f) break;
                Tap(Godot.Key.Enter);                    // dolu yuva: cikar
                Next();
                break;
            case 6:
                if (_stageT < 0.3f) break;
                Check(!b.On("tirpan_isini").Any() && b.InStorage.Count() == 5, "dolu yuvada Enter cipi depoya almali");
                // Kilit: Kabarma gecesi
                b.Locked = true;
                Check(b.Install("tirpan_isini", b.Owned[0].Id) == ChipResult.Locked, "Kabarma kilidi");
                b.Locked = false;
                // Etki Capa'ya: cip takili, ates hizi carpani
                s.Research.SetOcakTier(1);
                var hiz = b.Owned.First(c => c.Type == "hiz");
                Check(b.Install("tirpan_isini", hiz.Id) == ChipResult.Ok, "Hiz takilir");
                Check(Mathf.Abs(b.Stat("tirpan_isini", "fire_rate") - 0.25f) < 0.001f, "Hiz Destansi +%25");
                // Dusme: Ata yenilince
                int before = b.Owned.Count;
                Events.RaiseAtaDefeated("boynuzlu_dede");
                Check(b.Owned.Count == before + 1, "Ata dususu depoya cip eklemeli");
                hud.CloseInventory();
                GD.Print($"CIP RESULT ok checks={_checks} chips={b.Owned.Count} t={_t:0.0}");
                GetTree().Quit(0);
                SetProcess(false);
                break;
        }
    }
}
