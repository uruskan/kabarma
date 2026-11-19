using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Zincir sinamasi (--test=zincir, M2): gercek oyun ve bot. Bot ilk gunun senaryosunu Tezgah kurulana dek kendi oynar
/// (mine, odun, ocak, tezgah); sonra senaryo surucusu durur ve bot "arastir_t2" adimini oynar (Bot.Uretim.cs): Bakir
/// Tel, Arastirma Masasi, Tarayici ile Veri, Dokuma Tezgahi arastirmasi, Dokuma Tezgahi, Kumas. Hepsi oyuncunun yolundan;
/// esya verilmez. Bot bitince oyun katmani denetimleri (ambardan uretim deposu, yer tutucu gorunusler). Pencereli kosuda
/// kanit goruntuleri progress/m2/zincir/ altina.
/// Sonuc: "ZINCIR RESULT ok=N fail=M"; basarisizlikta cikis kodu 1.
/// </summary>
public partial class ZincirTest : Node
{
    Director _d;
    int _ok, _fail;
    readonly List<string> _log = new();
    string _outDir;
    float _t, _limit = 1500f;
    bool _driving, _finished, _checking;

    public ZincirTest() { Name = "ZincirTest"; }

    void Check(bool cond, string what) { if (cond) _ok++; else _fail++; Log($"{(cond ? "OK  " : "FAIL")} {what}"); }
    void Log(string s) { GD.Print("ZINCIR " + s); _log.Add(s); }

    public override void _Ready()
    {
        _outDir = Main.Args.TryGetValue("test_out", out var o) ? o.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m2/zincir/";
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        if (Main.Args.TryGetValue("zincir_limit", out var l)) _limit = float.Parse(l, System.Globalization.CultureInfo.InvariantCulture);
    }

    public override void _Process(double delta)
    {
        if (_finished || _checking) return;
        float dt = (float)delta;
        _t += dt;
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d?.Bot == null) return;
        if (_t > _limit) { Log($"ZAMAN ASIMI t={_t:0} adim={_d.State.Scenario.Current?.Id} veri={_d.State.Research.Veri} masa={_d.Stations.Any(s => s.Def.Id == "arastirma_masasi")}"); _fail++; Finish(); return; }
        if (!_driving)
        {
            // Ilk gunun botu Ocak ve Tezgah'i kurana dek oynar.
            bool ready = _d.Ocak != null && _d.Stations.Any(s => s.Def.Id == "tezgah") && _d.State.Scenario.Current?.Id != "tezgah";
            if (!ready) return;
            _driving = true;
            _d.Bot.ProcessMode = ProcessModeEnum.Disabled;
            Log($"ilk gun botu durdu (adim {_d.State.Scenario.Current?.Id}, t={_t:0}); arastir_t2 basliyor");
            return;
        }
        _d.Bot.DriveStep("arastir_t2", dt);
        if (_d.Bot.Failed) { _fail++; Finish(); return; }
        if (_d.Bot.T2Done) { _checking = true; CallDeferred(nameof(AfterBot)); }
    }

    async void AfterBot()
    {
        var s = _d.State;
        Log($"bot bitti t={_t:0}");
        Check(s.Research.IsDone("dokuma_tezgahi"), "Dokuma Tezgahi arastirildi (Arastirma Masasi, Veri)");
        Check(s.Research.Scanned.Count >= 3, $"Tarayici: {s.Research.Scanned.Count} tur tarandi ({string.Join(",", s.Research.Scanned)})");
        Check(s.Backpack.Count("cloth") >= 1, "T2 urunu Kumas uretildi");
        Check(_d.Stations.Any(x => x.Def.Id == "arastirma_masasi") && _d.Stations.Any(x => x.Def.Id == "dokuma_tezgahi"), "Arastirma Masasi ve Dokuma Tezgahi kurulu");
        Check(!s.Research.IsDone("bicki"), "arastirilmayan dugum kilitli kaldi (Bicki)");
        // Ambardan uretim: tapudaki Sandik istasyon deposuna girer (bot bitti; sinama dogrudan kurar).
        var chest = _d.PlaceFurniture(_d.Data.Tool.Placeables.Single(p => p.Id == "sandik"), _d.World.Ground(_d.OcakPos + new Vector3(-3f, 0, 12f)), 0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        int before = _d.StationStore().Count("fiber");
        chest.Storage.Add("fiber", 7);
        Check(_d.StationStore().Count("fiber") == before + 7, "tapudaki Sandik istasyon deposunda (GDD 5 ambardan uretim)");
        Check(_d.StoreFor(_d.Data.Tool.RecipeFor("sandik")!).Count("fiber") == s.Backpack.Count("fiber"), "el tarifi yalniz cantadan");
        // Yer tutucu gorunusler: modeli olmayan her zincir istasyonu koddan.
        var missing = _d.Data.Tool.Placeables.Where(p => p.Station != null && p.Model == null && p.Id is not ("tas_firin" or "ocakbasi") && ZincirVisuals.Make(p) == null && ZincirVisuals4.Make(p) == null).Select(p => p.Id).ToList();
        Check(missing.Count == 0, "zincir istasyonlarinin yer tutucusu var" + (missing.Count > 0 ? ": eksik " + string.Join(",", missing) : ""));
        await Zincir4Phase(); // M4: T3 ve T5 urunu (ZincirTest.T3.cs)
        if (DisplayServer.GetName() != "headless") await Shots();
        Finish();
    }

    async System.Threading.Tasks.Task Shots()
    {
        var p = _d.Player;
        var masa = _d.Stations.First(x => x.Def.Id == "arastirma_masasi");
        p.GlobalPosition = masa.GlobalPosition + new Vector3(0, 0.3f, 2.4f);
        for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Shot("zincir_istasyonlar.png");
        _d.Hud.OpenResearch();
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Shot("zincir_arastirma_paneli.png");
        _d.Hud.HideMenu();
        var dok = _d.Stations.First(x => x.Def.Id == "dokuma_tezgahi");
        p.GlobalPosition = dok.GlobalPosition + new Vector3(0, 0.3f, 2.2f);
        for (int i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _d.OpenStationMenu(dok);
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Shot("zincir_dokuma_menusu.png");
        _d.Hud.HideMenu();
    }

    void Shot(string file)
    {
        GetViewport().GetTexture().GetImage().SavePng(_outDir + file);
        Log($"SHOT {file}");
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        string line = $"ZINCIR RESULT ok={_ok} fail={_fail} veri={_d?.State.Research.Veri} t={_t:0}";
        Log(line);
        GD.Print(line);
        FileAccess.Open(_outDir + "zincir_test.log", FileAccess.ModeFlags.Write)?.StoreString(string.Join("\n", _log) + "\n");
        GetTree().Quit(_fail == 0 && _ok > 0 ? 0 : 1);
    }
}
