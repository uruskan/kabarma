using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=save (PLAN 7.2 kosul 5; URETIM M2 "kayit-yukleme sonrasi dunya ayni"): bot ilk gunu (gece dahil) oynar,
/// oyun diske kaydedilir, sahne ayni surecte bastan kurulup kayit yuklenir. Diskteki kayit ile yuklenen dunyanin yeni
/// anlik goruntusu bolum bolum bayt bayt ayni olmali; sahnedeki nesne sayilari da ayni. Ardindan ayni surecte yeni oyun
/// acilir: statikler temiz, senaryo basta. Yalniz user://test/save_* altina yazar ve sonunda siler.
/// Cikti: "RESULT ok ..." / "RESULT fail ...". Argumanlar: --save_limit=N (oyun sn, bot bitmezse hata; varsayilan 1500).
/// </summary>
public partial class SaveTest : Node
{
    readonly Main _main;
    int _stage;
    float _t, _wait;
    string _root;
    const string Slot = "yuva1";
    SaveGame _disk;
    string _digestBefore;
    readonly List<string> _fail = new();
    Director _loaded;
    bool _loadedFired;

    public SaveTest(Main main) { _main = main; Name = "SaveTest"; ProcessMode = ProcessModeEnum.Always; }

    /// <summary>Director kurulmadan once (Main.Tests): bot, sessiz, kosuya ozel test yuvasi.</summary>
    public static void Prepare()
    {
        Main.Args["bot"] = "1";
        Main.Args.TryAdd("mute", "1");
        Sfx.Muted = true;
        Main.Args["save_root"] = $"user://test/save_{OS.GetProcessId()}_{Time.GetTicksUsec()}";
        Main.Args["save_slot"] = Slot;
    }

    public override void _Ready() => _root = Main.Args["save_root"];

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        var d = _main.Game;
        switch (_stage)
        {
            case 0: // bot ilk gunu bitirsin
                float limit = Main.Args.TryGetValue("save_limit", out var l) ? float.Parse(l, System.Globalization.CultureInfo.InvariantCulture) : 1500f;
                if (d?.Bot != null && d.Bot.Done) { GD.Print($"SAVETEST bot done t={_t:0}"); _stage = 1; _wait = 0; }
                else if (_t > limit) Finish($"bot ilk gunu {limit:0} sn icinde bitirmedi (adim {d?.Bot?.CurrentStep})");
                break;
            case 1: // kaydedilebilir ani bekle (gece, rapor, elde yuk), kaydet, sahneyi bastan yukle
            case 4:
                _wait += dt;
                if (d.CanSave(out var why)) { SaveAndReload(d); return; }
                if (_wait > 120f) Finish($"120 sn boyunca kaydedilemedi: {why}");
                break;
            case 2: // OnLoaded karsilastirir
            case 5:
                _wait += dt;
                if (_loadedFired) { _loadedFired = false; _stage++; _wait = 0; }
                else if (_wait > 30f) Finish("yukleme 30 sn icinde bitmedi");
                break;
            case 3: // ikinci tur: botun birakmadigi nesneler (araba, arabada kasa, yerde urun ve yumurta, acik kapi)
                _wait += dt;
                if (_wait < 2f) break;
                Populate(d);
                _stage = 4; _wait = 0;
                break;
            case 6: // yuklenen dunya birkac kare islesin, sonra ayni surecte yeni oyun
                _wait += dt;
                if (_wait < 2f) break;
                _stage = 7; _wait = 0;
                CallDeferred(nameof(DoFresh));
                break;
            case 7:
                _wait += dt;
                if (_wait < 1f) break;
                CheckFresh(_loaded);
                Finish(null);
                break;
        }
    }

    int _round;

    void SaveAndReload(Director d)
    {
        _round++;
        if (!d.Saver.SaveNow("test", quiet: true)) { Finish($"tur {_round}: kayit yazilamadi: {d.Saver.LastError}"); return; }
        _digestBefore = Digest(d);
        try { _disk = d.Store.Read(Slot, out var from); GD.Print($"SAVETEST read {from}"); }
        catch (SaveException e) { Finish($"tur {_round}: kayit okunamadi: {e.Message}"); return; }
        long size = new System.IO.FileInfo(d.Store.WorldPath(Slot)).Length;
        GD.Print($"SAVETEST round={_round} saved day={_disk.Manifest.Day} sections={_disk.Sections.Count} bytes={size} {_digestBefore}");
        if (size > d.Data.Save.MaxWorldBytes) _fail.Add($"kayit {size} B > sinir");
        Main.Args.Remove("bot"); // sahne bastan: bot olmadan, ayni yuvadan
        _stage++; _wait = 0;
        CallDeferred(nameof(DoReload)); // kare islenirken agac degismesin
    }

    /// <summary>
    /// Yuklenen oyunda, oyunun kendi yollariyla (istasyon ciktisi, Paketleme, araba yuklemesi, yumurtlama, kapi) bot
    /// kosusunun kayit aninda birakmadigi nesneleri kurar; ikinci tur bunlarin da gidis-donusunu sinar.
    /// </summary>
    void Populate(Director d)
    {
        var tezgah = d.Stations.FirstOrDefault(s => IsInstanceValid(s) && s.Def.Station == "tezgah");
        var pack = d.Stations.FirstOrDefault(s => IsInstanceValid(s) && s.Def.Station == "paketleme") ?? tezgah;
        if (tezgah == null) { _fail.Add("ikinci tur: Tezgah yok"); return; }
        d.SpawnLoad(d.Data.Tool.Recipes.First(r => r.Id == "el_arabasi"), tezgah);
        var item = d.State.Backpack.Stacks.Select(s => s.id).FirstOrDefault(d.State.Cargo.Pricing.Sellable);
        if (item != null) d.State.Cargo.Pack(d.State.Backpack, item, 1);
        if (d.State.Cargo.Close() != null) d.SpawnLoad(d.Data.Tool.Recipes.First(r => r.Id == "kargo_kasasi"), pack);
        var cart = d.Carts.LastOrDefault();
        var crate = d.GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().LastOrDefault();
        if (cart != null && crate != null && !cart.TryLoad(crate, out var why)) _fail.Add($"ikinci tur: kasa arabaya yuklenemedi ({why})");
        var size = d.Data.Crops.SizeClasses.First();
        var crop = d.MakeSwollenForTest("turnip", size, 1.3f);
        d.AddChild(crop);
        crop.GlobalPosition = d.World.Ground(d.Player.GlobalPosition + new Vector3(2, 0, 1)) + Vector3.Up * 0.4f;
        d.Pens?.Lay("egg", d.Player.GlobalPosition + new Vector3(-2, 0, 1), null);
        GD.Print($"SAVETEST populate carts={d.Carts.Count} crate={(crate != null)} {Digest(d)}");
    }
    void DoReload()
    {
        var g = _main.Reload(_disk, _root, Slot);
        _loaded = g;
        g.Loaded += () => OnLoaded(g);
    }

    void DoFresh() => _main.Reload(null, _root, "yuva2");

    void OnLoaded(Director d)
    {
        _loadedFired = true;
        if (d.LastRestore == null) { _fail.Add("yukleme hata verdi (LastRestore yok)"); return; }
        if (d.LastRestore.Missing.Count > 0) _fail.Add($"kayitta eksik bolum: {string.Join(",", d.LastRestore.Missing)}");
        if (d.LastRestore.Unknown.Count > 0) _fail.Add($"taninmayan bolum: {string.Join(",", d.LastRestore.Unknown)}");
        var again = d.Save.Capture(SaveScope.World);
        int same = 0;
        foreach (var (id, blob) in _disk.Sections)
        {
            if (!again.Sections.TryGetValue(id, out var b2)) { _fail.Add($"'{id}' yuklemeden sonra yok"); continue; }
            if (blob.Data.SequenceEqual(b2.Data)) { same++; continue; }
            _fail.Add($"'{id}' farkli");
            GD.Print($"SAVETEST diff {id}\n  kayit: {Clip(blob.Data)}\n  yukle: {Clip(b2.Data)}");
        }
        string after = Digest(d);
        if (after != _digestBefore) _fail.Add($"sahne sayilari farkli: once [{_digestBefore}] sonra [{after}]");
        GD.Print($"SAVETEST compare same={same}/{_disk.Sections.Count} {after}");
    }

    static string Clip(byte[] data)
    {
        var s = System.Text.Encoding.UTF8.GetString(data);
        return s.Length > 600 ? s[..600] + "..." : s;
    }

    /// <summary>Sahnedeki nesnelerin sayimi: verinin kopyasi degil, gercekten kurulmus dugumler.</summary>
    static string Digest(Director d)
    {
        var tree = d.GetTree();
        int Group(string g) => tree.GetNodesInGroup(g).OfType<Node>().Count(n => IsInstanceValid(n) && !n.IsQueuedForDeletion() && d.IsAncestorOf(n));
        return string.Join(" ", new[]
        {
            $"stations={d.Stations.Count(IsInstanceValid)}",
            $"furniture={d.Furniture.Count(IsInstanceValid)}",
            $"ocak={(d.Ocak != null && IsInstanceValid(d.Ocak) ? 1 : 0)}",
            $"pieces={d.Build?.System?.Pieces.Count(p => IsInstanceValid(p) && !p.IsQueuedForDeletion()) ?? 0}",
            $"props={d.Pens?.Props.Count(IsInstanceValid) ?? 0}",
            $"pens={d.Pens?.Pens.Count ?? 0}",
            $"rocks={d.World.Rocks.Count(r => IsInstanceValid(r) && r.Stones > 0)}",
            $"tufts={d.World.Tufts.Sum(t => t.Left)}",
            $"carts={d.Carts.Count(IsInstanceValid)}",
            $"crates={Group("cargo_crates")}",
            $"swollen={Group("swollen")}",
            $"eggs={Group("egg_pickup")}",
            $"plots={d.State.Farm.Plots.Count()}",
            $"planted={d.State.Farm.Plots.Count(p => p.Planted)}",
            $"bag={d.State.Backpack.Stacks.Sum(s => s.count)}",
            $"credits={d.State.Credits}",
            $"step={d.State.Scenario.Current?.Id ?? "end"}",
            $"day={d.State.Calendar.Day}",
        });
    }

    void CheckFresh(Director old)
    {
        var d = _main.Game;
        if (d == null || d == old) { _fail.Add("yeni oyun kurulmadi"); return; }
        if (d.State.Scenario.StepIndex != 0) _fail.Add($"yeni oyunda senaryo adimi {d.State.Scenario.StepIndex}");
        if (d.Stations.Count > 0 || d.Ocak != null) _fail.Add("yeni oyunda istasyon ya da Ocak var");
        if (Zipzip.All.Any(z => !IsInstanceValid(z) || !d.IsAncestorOf(z))) _fail.Add("Zipzip.All eski sahneden dugum tutuyor");
        if (d.State.Credits != 0) _fail.Add($"yeni oyunda kredi {d.State.Credits}");
        GD.Print($"SAVETEST fresh {Digest(d)}");
    }

    void Finish(string fail)
    {
        if (fail != null) _fail.Add(fail);
        Cleanup();
        string res = _fail.Count == 0 ? "RESULT ok save round-trip diff=0" : "RESULT fail " + string.Join("; ", _fail);
        GD.Print(res);
        SetProcess(false);
        GetTree().Quit(_fail.Count == 0 ? 0 : 1);
    }

    /// <summary>Yalniz bu kosunun test klasoru silinir (user://test/save_...).</summary>
    void Cleanup()
    {
        if (string.IsNullOrEmpty(_root) || !_root.StartsWith("user://test/save_") || Main.Args.ContainsKey("save_keep")) return;
        var dir = ProjectSettings.GlobalizePath(_root);
        try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
        catch (System.Exception e) { GD.PushWarning($"SAVETEST temizlenemedi {dir}: {e.Message}"); }
    }
}
