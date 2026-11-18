using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Borsa sinamasi (--test=borsa; ekonomi2): gercek oyunu kurar ve Kargo Rampasi terminalini sinar: rampa panelinde Borsa,
/// Katalog, Cark satirlari ve sol sutun panelleri; kasa satisi Borsa fiyatindan ve fiyat kilidi; gun degisiminde sicak
/// kategori; katalogdan Salgam Tohumu (kapsul iner, E ile acilir, cantaya gecer) ve El Arabasi govdesi (rampaya iner);
/// Tohum Carki (gunun ilki bedava, sonra 500). Sinama oldugu icin mal dogrudan verilir (bot degildir; bot oyun yolundan
/// satar ve alir: Bot.Market.cs). Pencereli kosuda panellerin goruntusu progress/m2/ekonomi/ altina.
/// Sonuc: "BORSA RESULT ok=N fail=M"; basarisizlikta cikis kodu 1.
/// </summary>
public partial class MarketTest : Node
{
    readonly Director _d;
    int _ok, _fail;
    bool _finished;
    readonly List<string> _log = new();
    string _outDir;

    public MarketTest(Director d) { _d = d; Name = "MarketTest"; }

    void Check(bool cond, string what) { if (cond) _ok++; else _fail++; Log($"{(cond ? "OK  " : "FAIL")} {what}"); }
    void Log(string s) { GD.Print("BORSA " + s); _log.Add(s); }
    async Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    GameState S => _d.State;

    public override async void _Ready()
    {
        _outDir = Main.Args.TryGetValue("test_out", out var o) ? o.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m2/ekonomi/";
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        GetTree().CreateTimer(300, true, false, true).Timeout += () => { Log("ZAMAN ASIMI"); _fail++; Finish(); };
        await Frames(10);
        try
        {
            _d.PlaceOcak(_d.World.Ground(new Vector3(0, 0, 0)));
            _d.Pod.BecomeRamp();
            await Frames(3);
            await SellAtMarket();
            await Terminal();
            await BuyCatalog();
            await Wheel();
        }
        catch (Exception e) { _fail++; Log("HATA " + e); }
        Finish();
    }

    void NearRamp() => _d.Player.GlobalPosition = _d.Pod.PadCenter + new Vector3(0, 0.3f, 3.4f);

    /// <summary>Kasa: neutral ilk satis (aninda), sonra gun degisir, ikinci kasa Borsa fiyatindan ve kilitli.</summary>
    async Task SellAtMarket()
    {
        Check(!S.Market.State.Open && S.Market.FactorOf("turnip") == 1f, "Borsa ilk Kabarma'ya kadar notr");
        int c0 = S.Credits;
        int v1 = ShipCrate("turnip", 40);
        Check(v1 == 600 && S.Credits == c0 + 600, $"ilk kasa notr fiyattan, aninda (40 × 15 = {v1})");
        Check(Math.Abs(S.Market.Saturation("kok") - 0.97f) < 1e-4, $"kasa Kok'u %3 doyurdu ({S.Market.Saturation("kok"):0.00})");
        S.Calendar.Start(S.Data.Calendar.FirstDay);     // ilk safak: Borsa acilir
        S.Market.OnMorning(S.Data.Calendar.FirstDay + 1); // ilk gun degisimi
        await Frames(2);
        Check(S.Market.State.Hot.Length > 0, $"gun degisti: sicak kategori {S.Market.State.Hot}");
        float f = S.Market.FactorOf("copper_ore");
        int expect = (int)Math.Round(30 * 15 * f, MidpointRounding.AwayFromZero);
        int c1 = S.Credits;
        int v2 = ShipCrate("copper_ore", 30);
        Check(v2 == expect, $"ikinci kasa Borsa fiyatindan: 30 × 15 × {f:0.00} = {v2}");
        S.Market.OnMorning(S.Data.Calendar.FirstDay + 2); // yolda fiyat degisir
        await Wait(S.Data.Economy.Ramp.PayoutSeconds + 1);
        Check(S.Credits == c1 + v2, $"odeme firlatma anindaki fiyat (kilit): +{S.Credits - c1}");
    }

    int ShipCrate(string item, int n)
    {
        var cargo = S.Cargo;
        S.Backpack.Add(item, n);
        cargo.Pack(S.Backpack, item, n);
        var crate = cargo.Close();
        cargo.TakeClosed();
        cargo.LoadCrate(crate);
        return cargo.LaunchCrates();
    }

    async Task Terminal()
    {
        NearRamp();
        await Frames(2);
        _d.Interact();
        await Frames(3);
        Check(_d.Hud.StationOpen && _d.Hud.MenuTitleText == "Kargo Rampası", "E: rampa paneli");
        Check(_d.RampTerminalRow >= 1 && _d.Hud.MenuRows == _d.RampTerminalRow + 3, $"rampa panelinde Borsa/Katalog/Cark satirlari ({_d.RampTerminalRow}..{_d.Hud.MenuRows - 1})");
        Check(_d.Hud.MarketMode == "borsa", "rampa panelinin sol sutunu Borsa");
        await Shot("borsa_rampa.png");
        _d.Hud.ActivateMenu(_d.RampTerminalRow);
        await Frames(3);
        Check(_d.Hud.MenuTitleText == Director.BorsaTitle && _d.Hud.MenuRows == 13 && _d.Hud.MarketMode == "borsa", "Borsa paneli: 12 kategori + Geri");
        _d.Hud.ActivateMenu(9); // Maden ve Kulce
        await Frames(3);
        Check(_d.Hud.MarketCategory == "maden", "satir secimi kategorinin gecmisini acar");
        await Shot("borsa.png");
        _d.Hud.ActivateMenu(12); // Geri
        await Frames(2);
        Check(_d.Hud.MenuTitleText == "Kargo Rampası", "Geri: rampa paneli");
    }

    async Task BuyCatalog()
    {
        _d.Hud.ActivateMenu(_d.RampTerminalRow + 1);
        await Frames(3);
        Check(_d.Hud.MenuTitleText == Director.CatalogTitle && _d.Hud.MarketMode == "katalog", "Katalog paneli");
        var visible = S.Catalog.Visible.Select(e => e.Id).ToList();
        int seedRow = visible.IndexOf("tohum_salgam"), cartRow = visible.IndexOf("govde_el_arabasi");
        Check(seedRow >= 0 && cartRow >= 0, "katalogda tohum paketi ve El Arabasi govdesi");
        int c0 = S.Credits, seeds = S.Backpack.Count("turnip_seed");
        string delivered = null;
        Action<string, int> onDel = (src, _) => delivered = src;
        Events.Delivered += onDel;
        _d.Hud.ActivateMenu(seedRow);
        await Frames(2);
        Check(S.Credits == c0 - 300 && S.Catalog.Deliveries.Count == 1, $"tohum paketi alindi: -300 Kredi ({S.Credits})");
        await Shot("katalog.png");
        int carts = _d.Carts.Count;
        _d.Hud.ActivateMenu(cartRow);
        await Frames(2);
        Check(S.Credits == c0 - 700 && S.Catalog.Deliveries.Count == 2, "El Arabasi govdesi alindi: -400 Kredi");
        _d.Hud.HideMenu();
        await Wait(S.Data.Catalog.CapsuleSeconds + 3.5f);
        var pod = _d.GetTree().GetNodesInGroup("deliveries").OfType<EconomyDeliveryPod>().FirstOrDefault();
        Check(pod != null && pod.Landed, "kapsul gokten indi");
        if (pod != null)
        {
            Check(pod.GlobalPosition.DistanceTo(_d.OcakPos) < 9f, $"kapsul Ocak'in yaninda ({pod.GlobalPosition.DistanceTo(_d.OcakPos):0.0} m)");
            _d.Player.GlobalPosition = pod.GlobalPosition + new Vector3(1.2f, 0.3f, 2.2f);
            _d.Player.AimOverride = pod.GlobalPosition + Vector3.Up * 0.5f;
            await Frames(10);
            await Shot("kapsul.png");
            _d.Player.AimOverride = null;
            _d.Player.GlobalPosition = pod.GlobalPosition + new Vector3(1.2f, 0.3f, 0);
            await Frames(3);
            _d.Interact();
            await Frames(3);
            Check(S.Backpack.Count("turnip_seed") == seeds + 10 && delivered == "tohum_salgam", $"E: kapsul acildi, +10 Salgam Tohumu ({S.Backpack.Count("turnip_seed")})");
            await Frames(5);
            Check(!IsInstanceValid(pod) || pod.IsQueuedForDeletion(), "bos kapsul kalkar");
        }
        await Wait(S.Data.Catalog.RampSeconds - S.Data.Catalog.CapsuleSeconds);
        Check(_d.Carts.Count == carts + 1 && S.Catalog.Deliveries.Count == 0, $"El Arabasi rampaya indi ({_d.Carts.Count})");
        Events.Delivered -= onDel;
    }

    async Task Wheel()
    {
        NearRamp();
        await Frames(2);
        _d.Interact();
        await Frames(2);
        _d.Hud.ActivateMenu(_d.RampTerminalRow + 2);
        await Frames(3);
        Check(_d.Hud.MenuTitleText == Director.WheelTitle && _d.Hud.MarketMode == "cark", "Tohum Carki paneli");
        Check(S.WheelFreeToday, "gunun ilk cevirmesi bedava");
        int c0 = S.Credits;
        _d.Hud.ActivateMenu(0);
        await Frames(3);
        var w = S.Wheel.State;
        Check(w.Spins == 1 && S.Credits == c0 + w.LastCredits && !S.WheelFreeToday, $"bedava cevirme: {w.Last} (+{w.LastCredits} Kredi)");
        await Shot("cark.png");
        int c1 = S.Credits;
        bool canPay = c1 >= 500;
        _d.Hud.ActivateMenu(0);
        await Frames(3);
        if (canPay) Check(w.Spins == 2 && S.Credits == c1 - 500 + w.LastCredits, $"ikinci cevirme 500 Kredi: {w.Last}");
        else Check(w.Spins == 1 && S.Credits == c1, "Kredi yetmezse cevrilmez");
        _d.Hud.HideMenu();
    }

    async Task Shot(string file)
    {
        if (DisplayServer.GetName() == "headless") return;
        await Frames(4);
        GetViewport().GetTexture().GetImage().SavePng(_outDir + file);
        Log($"SHOT {file}");
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        Log($"BORSA RESULT ok={_ok} fail={_fail}");
        GD.Print($"BORSA RESULT ok={_ok} fail={_fail}");
        FileAccess.Open(_outDir + "borsa_test.log", FileAccess.ModeFlags.Write)?.StoreString(string.Join("\n", _log) + "\n");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }
}
