using System;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Bot, eller: adim isleyicilerinin oyuna dokundugu tek yer. Dogrudan kipte (--test=bot) oyunun yontemini cagirir;
/// girdi kipinde (--test=input) ayni karari sentetik klavye-fare ya da oyun kolu olayina cevirir (InputDriver) ve oyun
/// onu gercek giris yolundan (Director.ReadInput, Player) isler.
/// Kural: adim isleyicisi oyunu yalnizca bu yontemlerle, P.MoveOverride / P.AimOverride ve Fire / FirePressed ile surer.
/// Girdi kipinde her isleyicinin cevresinde durum anlik goruntusu alinir; isleyici icinde durum degisirse dogrudan cagri
/// vardir: "RESULT fail bypass" yazilir (baska daldan gelen adimlar da boylece yakalanir).
/// </summary>
public partial class Bot
{
    InputDriver _driver;
    bool _summaryShown;
    float _stepAge, _inDbg;
    string _ageStep = "";

    /// <summary>Girdi kipi: kararlar sentetik olaya doner.</summary>
    public bool UsesInput => _driver != null;
    public InputDriver Driver => _driver;

    public Bot(Director d, InputDevice device) : this(d)
    {
        _driver = new InputDriver(d, this, device);
    }

    // ---------- Eller ----------

    /// <summary>
    /// Capa basligini tak: once Capa ele (kemer, karar 23), sonra Q / LB carki (klavye: Q + fare; kol: LB + sag cubuk).
    /// </summary>
    void Head(Capa.Head h)
    {
        if (!_d.Capa.InHand) { HoldItem(PlanTools.Capa); return; }
        if (!UsesInput) { _d.Capa.Select(h); return; }
        if (_d.Capa.Current != h) _driver.SelectHead((int)h);
    }

    /// <summary>
    /// Kemerdeki esyayi ele al (karar 23): klavye sayi tusu 1-8, kol RB (sonraki yuva). Eldeyse true; kemerde yoksa false.
    /// </summary>
    bool HoldItem(string id)
    {
        if (_d.State.Belt.Held == id) return true;
        int slot = _d.State.Belt.SlotOf(id);
        if (slot < 0) { if (_stepT > 3f) { GD.Print($"BOT {id} kemerde yok"); _stepT = 0; } return false; }
        if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return false; }
        if (!UsesInput) { _d.SelectBelt(slot); return _d.State.Belt.Held == id; }
        if (_driver.Device == InputDevice.Kbm) _driver.Tap($"head_{slot + 1}");
        else _driver.Tap("belt_next");
        return false;
    }

    /// <summary>Insa Plani'nin parcasini sec: sag tik (kol LB) basili parca carki.</summary>
    void PickPiece(string id)
    {
        if (!UsesInput) { _d.Capa.SelectPiece(id); return; }
        var l = _d.Capa.PlanPieces;
        int i = l.IndexOf(id);
        if (i >= 0) _driver.SelectPiece(i, l.Count);
    }

    /// <summary>Kurulacak seyi dondur (R / ✚↓).</summary>
    void Rotate() { if (!UsesInput) _d.Capa.RotateGhost(); else _driver.Tap("build_rotate"); }

    /// <summary>Envanterde sonraki sayfa (klavye Q, kol RB): Canta → Defter → Uretim.</summary>
    void NextPage()
    {
        if (!UsesInput) { _d.Hud.SetInventoryPage((_d.Hud.InventoryPage + 1) % (Hud.CraftPage + 1)); return; }
        _driver.Tap(_driver.Device == InputDevice.Kbm ? "head_wheel" : "belt_next");
    }

    /// <summary>Etkilesim (E / Ⓧ): istasyon menusu, rampa, Gidak'i besleme.</summary>
    void Use() { if (!UsesInput) _d.Interact(); else _driver.Tap("interact"); }

    /// <summary>Canta menusu (Tab / Ⓨ).</summary>
    void Bag() { if (!UsesInput) _d.OpenBagMenu(); else _driver.Tap("bag"); }

    /// <summary>Hasat Raporu'nu kapat (Enter / Ⓐ).</summary>
    void CloseReport() { if (!UsesInput) _d.CloseReport(); else _driver.Tap("ui_accept"); }

    /// <summary>Gece kart teklifinden i. karti sec (Z X C / ✚).</summary>
    void Card(int i) { if (!UsesInput) _d.ChooseCard(i); else _driver.Tap($"card_{i + 1}"); }

    /// <summary>Acik menuden i. satiri sec (klavye: sayi tusu; kol: ✚ ile imleci tasi, Ⓐ).</summary>
    void MenuPick(int i) { if (!UsesInput) _d.Hud.ActivateMenu(i); else _driver.MenuPick(i); }

    /// <summary>Acik menuyu kapat (Esc / Ⓑ).</summary>
    void MenuClose() { if (!UsesInput) _d.Hud.HideMenu(); else _driver.Tap("ui_cancel"); }

    /// <summary>
    /// Acik canta izgarasinda (gecehud, Hud.Inventory.cs) esyaya yuru ve birincil eylemi yap (yemekse ye): yon tuslari /
    /// sol cubuk, Enter / Ⓐ. Girdi kipinde kare basina bir adim; eylem verildiyse true. (Entegrasyon: gecehud izgarasi + girdi.)
    /// </summary>
    bool BagUse(string id)
    {
        if (!UsesInput) { if (!_d.Hud.InventorySelect(id)) return false; _d.Hud.InventoryAccept(); return true; }
        var c = _d.State.Backpack.Contents;
        int target = -1;
        for (int i = 0; i < c.Count; i++) if (c[i].id == id) { target = i; break; }
        if (target < 0) return false;
        int cur = _d.Hud.InventorySelected, cols = _d.Hud.InventoryColumns;
        if (cur == target) { _driver.Tap("ui_accept"); return true; }
        int dx = target % cols - cur % cols, dy = target / cols - cur / cols;
        _driver.Tap(dy != 0 ? (dy > 0 ? "ui_down" : "ui_up") : (dx > 0 ? "move_right" : "move_left"));
        return false;
    }

    // ---------- Adim calistirma ----------

    /// <summary>Adim isleyicisini calistirir. Girdi kipinde: bypass denetimi, sonra sentetik olaylar.</summary>
    void RunStep(string step, Action run)
    {
        if (!UsesInput) { run(); return; }
        float dt = (float)GetProcessDeltaTime();
        if (step != _ageStep) { _ageStep = step; _stepAge = 0; }
        _stepAge += dt;
        if (!_done && _stepAge > _driver.Config.StepTimeoutSeconds && !Failed)
        {
            Failed = true;
            GD.Print($"RESULT fail input stuck step={step} age={_stepAge:0} device={_driver.Device} {_driver.Status()}");
            GetTree().Quit(1);
            return;
        }
        var before = Snapshot();
        _driver.BeginFrame();
        run();
        var after = Snapshot();
        if (before != after && !Failed)
        {
            Failed = true;
            GD.Print($"RESULT fail bypass step={step} device={_driver.Device} before=({before}) after=({after}) : adim isleyicisi oyunu dogrudan degistirdi; Bot.Hands.cs yontemlerini kullan");
            GetTree().Quit(1);
            return;
        }
        _driver.Take(P, Fire, FirePressed);
        FirePressed = false;
        _driver.Tick(dt);
        // Zincir bitti (RESULT ok yazildi): kanit satiri ve cikis; --quit_after yalnizca ust sinir.
        if (_done && !_summaryShown && _driver.Error == null) { _summaryShown = true; GD.Print(_driver.Summary()); GetTree().Quit(Failed ? 1 : 0); }
        _inDbg -= dt;
        if (_inDbg <= 0 && Main.Args.ContainsKey("botdebug")) { _inDbg = 2f; GD.Print($"INPUTDBG {step} {_driver.Status()}"); }
        if (_driver.Error != null && !Failed)
        {
            Failed = true;
            GD.Print($"RESULT fail input {_driver.Error} step={step} device={_driver.Device}");
            GetTree().Quit(1);
        }
    }

    /// <summary>Isleyicinin oyuna dokunup dokunmadigini anlamaya yeten ucuz durum ozeti.</summary>
    readonly record struct HandsState(int Head, string Placeable, bool Menu, bool Report, bool Wheel, int Cards, int Bag, int Slots, int Stations, int Pieces, int Credits, int Plots, bool Ocak, int Belt, int Jobs, int Page);

    HandsState Snapshot()
    {
        var s = _d.State;
        int bag = 0;
        foreach (var c in s.Backpack.Contents) bag = bag * 31 + c.id.GetHashCode() * 7 + c.count;
        return new HandsState((int)_d.Capa.Current, _d.Capa.CurrentPlaceable, _d.Hud.MenuOpen, _d.Hud.ReportOpen, _d.Hud.WheelOpen,
            _d.Night?.CardsCount ?? 0, bag, s.Backpack.UsedSlots, _d.Stations.Count, _d.Build?.System?.Pieces.Count ?? 0, s.Credits, System.Linq.Enumerable.Count(s.Farm.Plots), _d.Ocak != null,
            s.Belt.Selected, s.Craft.Jobs.Count, _d.Hud.InventoryOpen ? _d.Hud.InventoryPage : -1);
    }
}
