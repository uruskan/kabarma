using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ekonomi sinamasi (--test=ekonomi): gercek oyunu (Director) kurar, sonra M1 ekonomi tesliminin oyun katmanini sinar:
/// istasyonlar ve yerlestirilebilirler, yakit, Ocakbasi yemekleri, El Arabasi (itme hizi %80, depar yok, tek yuk ya da 6 yuva,
/// 200 kg siniri, devrilme ve E ile duzeltme), kargo kasasi (25 kg yuk nesnesi, deger = icerik), Kargo Rampasi (1-6 kasa ya da
/// tek kabarik urun; ilk satis aninda, sonra 60 sn), sokum (%100 iade, dusen parcalar dahil), onarim (%25 × hasar), %10 can tabani,
/// uyku (Events.Slept), yemek yuvalari. Sinama oldugu icin malzeme dogrudan verilir (bot degildir; bot oyun yolundan toplar).
/// Sonuc: "ECONOMY RESULT ok=N fail=M"; basarisizlikta cikis kodu 1.
/// </summary>
public partial class EconomyTest : Node
{
    readonly Director _d;
    int _ok, _fail;
    readonly List<string> _log = new();
    string _outDir;

    public EconomyTest(Director d) { _d = d; Name = "EconomyTest"; }

    void Check(bool cond, string what)
    {
        if (cond) _ok++; else _fail++;
        Log($"{(cond ? "OK  " : "FAIL")} {what}");
    }

    void Log(string s) { GD.Print("ECON " + s); _log.Add(s); }

    async Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    Inventory Bag => _d.State.Backpack;
    Vector3 G(float x, float z) => _d.World.Ground(_d.OcakPos + new Vector3(x, 0, z));
    PlaceableDef Def(string id) => _d.Data.Tool.Placeables.Single(p => p.Id == id);

    public override async void _Ready()
    {
        _outDir = Main.Args.TryGetValue("test_out", out var o) ? o.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m1/ekonomi/";
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        GetTree().CreateTimer(240, true, false, true).Timeout += () => { Log("ZAMAN ASIMI"); _fail++; Finish(); };
        await Frames(10);
        try
        {
            _d.PlaceOcak(_d.World.Ground(new Vector3(0, 0, 0)));
            await Frames(2);
            await Stations();
            await Cart();
            await CrateAndRamp();
            await Building();
            await SleepAndFood();
            await HarvestQuality();
        }
        catch (Exception e) { _fail++; Log("HATA " + e); }
        Finish();
    }

    /// <summary>Biçme hasadinin yildizi cantaya gecer (Capa.Bicme), kasaya kaliteyle girer, kasa degeri taban × kalite.</summary>
    async Task HarvestQuality()
    {
        var f = _d.State.Farm;
        int x = 4, y = 12;
        if (Bag.Count("turnip_seed") == 0) Bag.Add("turnip_seed", 1);
        Check(f.Till(x, y) && f.Plant(x, y, "turnip", Bag), "kalite: kare surulup ekildi");
        f.Water(x, y);
        for (int i = 0; i < 4000 && f.Get(x, y)!.Stage != CropStage.Mature; i++) { f.Tick(0.5f, true); f.Water(x, y); }
        Check(f.Get(x, y)!.NeverDry, "kalite: nem hic dusmedi (en az ★2, GDD 8.2)");
        var c = _d.FarmView.CellCenter(x, y);
        var p = _d.Player;
        p.GlobalPosition = c + new Vector3(0, 0.3f, -2.5f);
        await Frames(4);
        int before1 = Bag.Count("turnip", 1), before2 = Bag.Count("turnip", 2), before3 = Bag.Count("turnip", 3);
        int n = _d.Capa.Mow(c);
        int got2 = Bag.Count("turnip", 2) - before2, got3 = Bag.Count("turnip", 3) - before3;
        Check(n == 1 && got2 + got3 > 0 && Bag.Count("turnip", 1) == before1, $"Biçme hasadi yildizla cantada (★2 +{got2}, ★3 +{got3})");
        // kasaya kaliteyle girer, deger taban × kalite
        int stars = got3 > 0 ? 3 : 2, got = got3 > 0 ? got3 : got2;
        var cargo = _d.State.Cargo;
        if (cargo.Open != null && !cargo.Open.Items.IsEmpty) { cargo.Close(); while (cargo.TakeClosed() != null) { } } // onceki bolumden kalan kasa
        var tmp = new Inventory(1, _d.Data.Tool.StackOf);
        Bag.Remove("turnip", got, stars);
        tmp.Add("turnip", got, stars);
        cargo.Pack(tmp, "turnip", got);
        var crate = cargo.Open;
        Check(crate != null && crate.Items.Count("turnip", stars) == got, "kalite kasaya gecti");
        // M2 (ekonomi2): fiyat Borsa × doygunluk da icerir; onceki satislar Kok'u doyurmus olabilir.
        int expect = (int)Math.Round(got * cargo.Pricing.Base("turnip") * _d.Data.Tool.Quality[stars - 1] * _d.State.Market.FactorOf("turnip"), MidpointRounding.AwayFromZero);
        Check(crate != null && cargo.Value(crate) == expect, $"kasa degeri taban × kalite × Borsa ({(crate != null ? cargo.Value(crate) : 0)} = {expect})");
        await NewCropInGreenhouse();
    }

    /// <summary>tarim4 (M4): Kanyon ekini (Misir) seradaki karede buyur, Bicme ile hasat edilir (bant disinda sera sart).</summary>
    async Task NewCropInGreenhouse()
    {
        var f = _d.State.Farm;
        int x = 6, y = 12;
        Bag.Add("corn_seed", 1);
        Check(f.Till(x, y), "sera: kare surulup");
        f.SetGreenhouse(x, y, true);
        Check(f.Plant(x, y, "corn", Bag), "sera: misir ekildi");
        f.Water(x, y);
        for (int i = 0; i < 6000 && f.Get(x, y)!.Stage != CropStage.Mature; i++) { f.Tick(0.5f, true); f.Water(x, y); }
        Check(f.Get(x, y)!.Stage == CropStage.Mature, "sera: misir banttan bagimsiz olgunlasti");
        var c = _d.FarmView.CellCenter(x, y);
        _d.Player.GlobalPosition = c + new Vector3(0, 0.3f, -2.5f);
        await Frames(4);
        int n = _d.Capa.Mow(c);
        Check(n == 1 && (Bag.Count("corn") + Bag.Count("corn", 2) + Bag.Count("corn", 3)) > 0, "sera: misir Bicme ile hasat edildi (cantada)");
    }

    bool _finished;
    void Finish()
    {
        if (_finished) return;
        _finished = true;
        Log($"ECONOMY RESULT ok={_ok} fail={_fail}");
        GD.Print($"ECONOMY RESULT ok={_ok} fail={_fail}");
        FileAccess.Open(_outDir + "ekonomi_test.log", FileAccess.ModeFlags.Write)?.StoreString(string.Join("\n", _log) + "\n");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    void Shot(string file)
    {
        if (DisplayServer.GetName() == "headless") return;
        GetViewport().GetTexture().GetImage().SavePng(_outDir + file);
        Log($"SHOT {file}");
    }

    async Task Look(Vector3 from, Vector3 at, string file)
    {
        if (DisplayServer.GetName() == "headless") return;
        var cam = new Camera3D { Fov = 55f };
        _d.AddChild(cam);
        cam.GlobalPosition = from;
        cam.LookAt(at);
        cam.MakeCurrent();
        await Frames(8);
        Shot(file);
        cam.QueueFree();
        _d.Player.Cam.MakeCurrent();
    }

    Station _tezgah, _firin, _ocb, _pak;

    // ---------------------------------------------------------------- istasyonlar ve yerlestirilebilirler

    async Task Stations()
    {
        _tezgah = _d.PlaceStation(Def("tezgah"), G(-5, 2), 0);
        _firin = _d.PlaceStation(Def("tas_firin"), G(-5, -3.5f), 0);
        _ocb = _d.PlaceStation(Def("ocakbasi"), G(-9, -1), 0);
        _pak = _d.PlaceStation(Def("paketleme"), G(-1, 6), 0);
        var chest = _d.PlaceFurniture(Def("sandik"), G(4, 6), 0);
        var torch = _d.PlaceFurniture(Def("mesale"), G(2, 3), 0);
        await Frames(3);
        Check(_ocb.GetChild(0) is OcakbasiVisual, "Ocakbasi koddan (tas halka + izgara + ates)");
        var light = torch.FindChildren("*", "OmniLight3D", true, false).OfType<OmniLight3D>().FirstOrDefault();
        Check(light != null && Mathf.IsEqualApprox(light.OmniRange, 6f), $"Mesale 6 m isik (range={light?.OmniRange})");
        Check(chest.Storage?.Slots == 20, "Sandik 20 yuva");
        Check(Assets.Load($"{PlaceableVisuals.Dir}/sandik.glb") != null && Assets.Load($"{PlaceableVisuals.Dir}/uyku_tulumu.glb") != null && Assets.Load($"{PlaceableVisuals.Dir}/mesale.glb") != null, "kalas dili modelleri yuklu (sandik, uyku_tulumu, mesale)");

        // Tas Firin: yakit ister; ilk kulce Civi Tabancasi tarifini acar.
        Bag.Add("copper_ore", 6);
        var ingot = _firin.Queue.Recipes.Single(r => r.Id == "copper_ingot");
        Check(!_firin.Queue.CanMake(ingot, Bag), "Tas Firin yakitsiz calismaz");
        Bag.Add("wood", 40);
        int w0 = Bag.Count("wood");
        Check(_firin.Queue.Enqueue(ingot, Bag) && _firin.Queue.Enqueue(ingot, Bag) && _firin.Queue.Enqueue(ingot, Bag), "3 Bakir Kulce siraya");
        Check(Bag.Count("wood") == w0 - 3, "is basina 1 Odun yakildi");
        var gun = _tezgah.Queue.Recipes.Single(r => r.Id == "civi_tabancasi");
        Check(!_tezgah.Queue.IsUnlocked(gun), "Civi Tabancasi tarifi kilitli");
        await Wait(13f);
        Check(Bag.Count("copper_ingot") == 3, $"3 kulce cikti ({Bag.Count("copper_ingot")})");
        Check(_tezgah.Queue.IsUnlocked(gun), "ilk kulceyle Civi Tabancasi acildi");
        int nails = Bag.Count("civi");
        Check(_tezgah.Queue.Enqueue(_tezgah.Queue.Recipes.Single(r => r.Id == "civi"), Bag), "Civi siraya (1 kulce)");
        await Wait(2.5f);
        Check(Bag.Count("civi") == nails + 20, "1 kulce -> 20 Civi");
        Check(_tezgah.Queue.Enqueue(gun, Bag), "Civi Tabancasi siraya (2 kulce + 4 Odun)");
        await Wait(4.5f);
        Check(Bag.Count("civi_tabancasi") == 1, "Civi Tabancasi cantada");

        // Ocakbasi: uc yemek, yakitla
        Bag.Add("turnip", 40);
        Bag.Add("egg", 2);
        foreach (var id in new[] { "kozde_salgam", "haslanmis_yumurta", "sebze_corbasi" })
        {
            var r = _ocb.Queue.Recipes.Single(x => x.Id == id);
            Check(_ocb.Queue.Enqueue(r, Bag), $"Ocakbasi: {id} siraya");
            await Wait(r.Seconds + 0.6f);
            Check(Bag.Count(id) == 1, $"Ocakbasi: {id} pisti");
        }
        await Look(G(-2, -12) + Vector3.Up * 6f, G(-5, 0) + Vector3.Up, "ekonomi_istasyonlar.png");
    }

    // ---------------------------------------------------------------- El Arabasi

    async Task Cart()
    {
        int w = Bag.Count("wood"), s = Bag.Count("stone");
        Bag.Add("stone", 10);
        var r = _tezgah.Queue.Recipes.Single(x => x.Id == "el_arabasi");
        Check(_tezgah.Queue.Enqueue(r, Bag), "El Arabasi siraya (12 Odun + 4 Taş)");
        Check(Bag.Count("wood") == w - 12 && Bag.Count("stone") == s + 10 - 4, "El Arabasi bedeli dustu");
        await Wait(r.Seconds + 0.8f);
        var cart = _d.Carts.FirstOrDefault();
        Check(cart != null && Bag.Count("el_arabasi") == 0, "El Arabasi dunyaya cikti (cantaya girmedi)");
        if (cart == null) return;
        // duz bir yere al
        cart.GlobalTransform = new Transform3D(Basis.Identity, G(10, -4));
        var p = _d.Player;
        p.GlobalPosition = cart.Handle + Vector3.Up * 0.3f - cart.Forward * 0.6f;
        await Frames(4);
        float walk = p.WalkSpeed, sprint = p.SprintSpeed, mult = p.SpeedMult;
        _d.Interact();
        await Frames(2);
        Check(cart.Pushed && !_d.Hud.MenuOpen, "tutamakta E: dogrudan tutamaga gecildi (menu yok)");
        Check(Mathf.IsEqualApprox(p.SpeedMult, mult * 0.8f) && Mathf.IsEqualApprox(p.SprintSpeed, walk), "itme: yurume hizinin %80'i, depar yok");
        var dir = cart.Forward;
        p.MoveOverride = dir * 1.5f; // depara basiyor
        await Wait(0.8f);
        var a = p.GlobalPosition; ulong t0 = Time.GetTicksUsec();
        await Wait(1.5f);
        var b = p.GlobalPosition; float secs = (Time.GetTicksUsec() - t0) / 1e6f;
        p.MoveOverride = Vector3.Zero;
        float speed = new Vector2(b.X - a.X, b.Z - a.Z).Length() / Mathf.Max(0.01f, secs);
        Check(speed > walk * 0.8f * 0.7f && speed < walk * 0.8f * 1.15f, $"itme hizi {speed:0.00} m/sn (beklenen ~{walk * 0.8f:0.0})");
        float gap = new Vector2(cart.GlobalPosition.X - p.GlobalPosition.X, cart.GlobalPosition.Z - p.GlobalPosition.Z).Length();
        Check(gap > 0.8f && gap < cart.Length + 1.6f, $"araba oyuncunun onunde ({gap:0.00} m)");
        _d.Interact(); // birak
        Check(!cart.Pushed && Mathf.IsEqualApprox(p.SprintSpeed, sprint) && Mathf.IsEqualApprox(p.SpeedMult, mult), "E ile birakildi, hiz geri geldi");
        // yandan E: yuva ve yuk menusu (tutamaga gecmez)
        p.GlobalPosition = _d.World.Ground(cart.GlobalPosition + cart.GlobalBasis.X * 1.4f) + Vector3.Up * 0.3f;
        await Frames(3);
        _d.Interact();
        await Frames(2);
        Check(_d.Hud.MenuOpen && !cart.Pushed, "arabanin yaninda E: yuva menusu");
        _d.Hud.HideMenu();

        // 6 yuva ya da tek yuk, ikisi birden degil; 200 kg siniri
        Check(cart.Slots.Slots == 6, "araba 6 yuva");
        Bag.MoveTo(cart.Slots, "turnip", 5);
        var crate = MakeCrate("turnip", 5);
        Check(!cart.TryLoad(crate, out var why1), $"yuvada esya varken yuk alinmaz ({why1})");
        cart.Slots.MoveTo(Bag, "turnip", 5);
        Check(cart.TryLoad(crate, out _), "bos arabaya kasa yuklendi");
        Check(!cart.CanUseSlots, "yuk varken yuvalar kapali");
        var heavy = new SwollenCrop("turnip", _d.Data.Crops.SizeClasses.First(x => x.Kg > 200), new Node3D(), null, 1f);
        _d.AddChild(heavy);
        cart.DropLoad();
        Check(!cart.TryLoad(heavy, out var why2), $"200 kg'dan agir yuk alinmaz ({why2})");
        heavy.QueueFree();
        Check(cart.TryLoad(crate, out _), "kasa yeniden yuklendi");
        await Wait(0.5f); // yere indirme tween'i (0,35 sn) biter, kasa yatağa oturur
        Check(crate.GlobalPosition.DistanceTo(cart.Bed) < 1.2f, "kasa arabanin kasasinda duruyor");
        await Look(cart.GlobalPosition + new Vector3(3.5f, 2.5f, 3.5f), cart.GlobalPosition + Vector3.Up * 0.6f, "ekonomi_el_arabasi.png");

        // itip rampaya kadar gotur: arabayla tasinan kasa da gider
        cart.Grab();
        var dest = _d.Pod.PadCenter;
        for (int i = 0; i < 400 && cart.Pushed; i++)
        {
            var to = dest - p.GlobalPosition; to.Y = 0;
            if (to.Length() < 5.5f) break;
            p.MoveOverride = to.Normalized();
            await Frames(1);
        }
        p.MoveOverride = Vector3.Zero;
        Check(crate.OnCart == cart && cart.GlobalPosition.DistanceTo(dest) < 9f, $"kasa arabayla rampaya geldi ({cart.GlobalPosition.DistanceTo(dest):0.0} m)");
        cart.Release();
        _cart = cart;
        _crateOnCart = crate;

        // devrilme ve E ile duzeltme
        cart.GlobalTransform = new Transform3D(Basis.Identity, G(10, -4));
        var spare = MakeCrate("egg", 1);
        cart.DropLoad();
        cart.TryLoad(spare, out _);
        cart.Tip();
        await Wait(1.6f);
        Check(cart.Tipped, $"araba devrildi (yukari {cart.GlobalBasis.Y.Normalized().Dot(Vector3.Up):0.00})");
        Check(cart.Load == null, "devrilince yuk yere dustu");
        p.GlobalPosition = cart.GlobalPosition - cart.Forward * 1.6f + Vector3.Up * 0.3f;
        await Frames(2);
        _d.Interact();
        await Wait(0.8f);
        Check(!cart.Tipped, "E ile duzeltildi");
        cart.TryLoad(crate, out _);
    }

    ElArabasi _cart;
    CargoCrateNode _crateOnCart;

    CargoCrateNode MakeCrate(string id, int n)
    {
        Bag.Add(id, n);
        _d.State.Cargo.Pack(Bag, id, n);
        _d.State.Cargo.Close();
        var c = _d.State.Cargo.TakeClosed();
        var node = new CargoCrateNode(_d, c);
        _d.AddChild(node);
        node.GlobalPosition = G(8, -6) + Vector3.Up * node.Radius;
        return node;
    }

    // ---------------------------------------------------------------- kasa ve rampa

    async Task CrateAndRamp()
    {
        var cargo = _d.State.Cargo;
        // Paketleme: cantadan kasaya, kapat, kasa yuk nesnesi olarak cikar
        int turnips = Bag.Count("turnip");
        Check(cargo.Pack(Bag, "turnip", 30) == 30 && cargo.Pack(Bag, "stone", 5) == 0, "kasaya 30 Salgam girdi, Tas girmez");
        Check(cargo.Close() != null && _pak.Queue.Enqueue(_pak.Queue.Recipes.Single(r => r.Id == "kargo_kasasi"), Bag), "kasa kapandi, Paketleme sirada");
        int before = GetTree().GetNodesInGroup("cargo_crates").Count;
        await Wait(3.6f);
        var packed = GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().FirstOrDefault(c => c.Crate.Items.Count("turnip") == 30);
        Check(packed != null && GetTree().GetNodesInGroup("cargo_crates").Count == before + 1, "Paketleme'den kasa dunyaya cikti");
        Check(packed != null && packed.Crate.Kg == 25f && packed.Size.Kg == 25, "kasa 25 kg yuk nesnesi");
        Check(packed != null && cargo.Value(packed.Crate) == 450, $"kasa degeri icerigi: 30 × 15 = {(packed != null ? cargo.Value(packed.Crate) : -1)}");
        Check(Bag.Count("turnip") == turnips - 30, "Salgamlar cantadan cikti");

        _d.Pod.BecomeRamp();
        await Frames(2);
        // Rampa: Cekim'le birakma yolu (Director.SellCrop) -> 6 kasa, 7.'si alinmaz
        var crates = new List<CargoCrateNode>();
        if (packed != null) crates.Add(packed);
        if (_crateOnCart != null) crates.Add(_crateOnCart);
        while (crates.Count < 7) crates.Add(MakeCrate("egg", 1));
        foreach (var c in crates) _d.SellCrop(c);
        await Frames(2);
        Check(cargo.Pad.Count == 6 && crates.Count(c => c.OnRamp) == 6, $"rampa 6 kasa aldi ({cargo.Pad.Count})");
        Check(!crates[6].OnRamp, "7. kasa alinmadi");
        Check(_crateOnCart == null || _crateOnCart.OnCart == null, "arabadaki kasa rampaya gecti");
        var swollen = new SwollenCrop("turnip", _d.Data.Crops.SizeClasses[0], new Node3D(), null, 1f);
        _d.AddChild(swollen);
        swollen.GlobalPosition = _d.Pod.PadCenter + Vector3.Up;
        _d.SellCrop(swollen);
        Check(!swollen.Sold, "rampada kasa varken kabarik urun alinmaz");
        await Look(_d.Pod.PadCenter + new Vector3(-5, 4, 6), _d.Pod.PadCenter, "ekonomi_rampa.png");

        // Gonder (E menusu): ilk satis aninda oder
        int value = cargo.Pad.Sum(cargo.Value);
        int credits = _d.State.Credits;
        int sold = 0, paid = 0;
        Events.Sold += (item, n, v) => { if (item == "kargo_kasasi") sold += v; };
        Events.Paid += (v, _) => paid += v;
        _d.Player.GlobalPosition = _d.Pod.PadCenter + (_d.Player.GlobalPosition - _d.Pod.PadCenter).Normalized() * 3.2f + Vector3.Up * 0.3f;
        await Frames(2);
        _d.OpenRampMenu();
        _d.Hud.ActivateMenu(0);
        await Frames(2);
        Check(cargo.Pad.Count == 0 && sold == value, $"6 kasa firlatildi ({value} Kredi)");
        Check(_d.State.Credits == credits + value && paid == value, "ilk satis: odeme aninda");
        // ikinci gonderim: 60 sn sonra
        _d.SellCrop(crates[6]);
        int c2 = _d.State.Credits;
        int v2 = cargo.Value(crates[6].Crate);
        _d.OpenRampMenu();
        _d.Hud.ActivateMenu(0);
        await Wait(2f);
        Check(_d.State.Credits == c2 && cargo.Pending.Count == 1, "sonraki satis: odeme bekliyor");
        float left = cargo.Pending[0].Left;
        Check(left > 55f && left <= 60f, $"odeme ~60 sn sonra ({left:0.0} sn kaldi)");
        _d.SpeedScale = 12f;
        await Wait(left + 2f);
        _d.SpeedScale = 1f;
        Check(_d.State.Credits == c2 + v2, $"60 sn sonra odendi (+{v2})");
        // bos rampaya kabarik urun gider
        _d.SellCrop(swollen);
        Check(swollen.Sold, "bos rampaya kabarik urun yuklendi ve firlatildi");
    }

    // ---------------------------------------------------------------- yapi: sokum, onarim, can tabani

    async Task Building()
    {
        var b = _d.Build;
        Check(b != null, "yapi seti yuklu");
        if (b == null) return;
        var sys = b.System;
        sys.SetActive(true);
        BuildPiece Place(string id, Vector3 target, Vector3 from)
        {
            sys.SetActive(true); // Insa basligi takili degilken Capa her karede yapi setini gizler
            sys.Select(id);
            for (int i = 0; i < 8; i++)
            {
                sys.UpdateAim(from, target - from);
                if (sys.Current != null && sys.Current.Valid) break;
                sys.Rotate();
            }
            var piece = sys.TryPlace();
            Check(piece != null, $"{id} kondu ({sys.Current?.Reason})");
            return piece;
        }
        var c = G(9, 8);
        var f = Place("kare_temel", c, c + new Vector3(0, 8, 4));
        if (f == null) { sys.SetActive(false); return; }
        await Frames(2);
        float top = f.GlobalPosition.Y + f.Def.BoundsMax.Y;
        var t = new Vector3(f.GlobalPosition.X, top, f.GlobalPosition.Z) + Vector3.Forward * (1.5f - 0.45f);
        var wall = Place("duvar", t, t + Vector3.Up * 6f - Vector3.Forward * 2f);
        sys.SetActive(false);
        await Frames(2);

        // hasar: %10'da durur; onarim: hasar oraninda %25
        float max = sys.Kit.PieceHp;
        b.Damage(f, 10000);
        Check(Mathf.IsEqualApprox(f.Hp, max * 0.1f), $"can %10'da durdu ({f.Hp})");
        int w = Bag.Count("wood");
        Check(b.Repair(f) && Bag.Count("wood") == w - 2 && Mathf.IsEqualApprox(f.Hp, max), "onarim: 6 Odun × %25 × %90 -> 2 Odun, can doldu");
        Check(!b.Repair(f), "hasarsiz parca onarilmaz");

        // sokum: temel sokulunce ustundeki duvar duser; ikisi de iade
        w = Bag.Count("wood");
        int pieces = sys.Pieces.Count;
        Check(b.Deconstruct(f), "temel sokuldu");
        await Frames(2);
        Check(sys.Pieces.Count == pieces - 2, "desteksiz duvar dustu");
        Check(Bag.Count("wood") == w + 6 + 4, $"sokum iadesi %100 (temel 6 + dusen duvar 4) -> {Bag.Count("wood") - w}");

        // yerlestirilebilir sokumu
        var chest = _d.Furniture.First(x => x.Storage != null);
        Bag.MoveTo(chest.Storage, "egg", 1);
        if (chest.Storage.IsEmpty) { Bag.Add("wheat", 1); Bag.MoveTo(chest.Storage, "wheat", 1); }
        Check(!b.Deconstruct(chest), "dolu Sandik sokulmez");
        foreach (var id in chest.Storage.Contents.Select(x => x.id).Distinct().ToList()) chest.Storage.MoveTo(Bag, id, 999);
        // Karar 23: yerlestirilebilir uretilmis esyadir; sokulunce esyanin kendisi cantaya doner, kemerden yeniden kurulur.
        w = Bag.Count("sandik");
        Check(b.Deconstruct(chest) && Bag.Count("sandik") == w + 1 && !_d.Furniture.Contains(chest), "bos Sandik sokuldu, Sandik cantaya dondu");
        _tezgah.Queue.Enqueue(_tezgah.Queue.Recipes.Single(r => r.Id == "el_arabasi"), Bag);
        Check(!b.Deconstruct(_tezgah), "uretim surerken istasyon sokulmez");
        await Wait(4.6f);
        w = Bag.Count("tezgah");
        Check(b.Deconstruct(_tezgah) && Bag.Count("tezgah") == w + 1 && !_d.Stations.Contains(_tezgah), "Tezgah sokuldu, Tezgah cantaya dondu");
    }

    // ---------------------------------------------------------------- uyku ve yemek

    async Task SleepAndFood()
    {
        Bag.Add("fiber", 6);
        var bag = _d.PlaceFurniture(Def("uyku_tulumu"), G(6, 9), 0);
        Check(_d.Player.RespawnPoint.DistanceTo(bag.GlobalPosition) < 1f, "Uyku Tulumu dogma noktasi");
        int slept = 0;
        Events.Slept += () => slept++;
        _d.Player.GlobalPosition = bag.GlobalPosition + new Vector3(1.2f, 0.3f, 0);
        await Frames(2);
        _d.Interact();
        Check(slept == 1, "E ile uyundu: Events.Slept");
        await Look(bag.GlobalPosition + new Vector3(2.5f, 2f, 3f), bag.GlobalPosition, "ekonomi_uyku_tulumu.png");

        float hp0 = _d.Data.Tool.Player.MaxHp;
        foreach (var food in _d.Data.Tool.Foods.Take(3)) Check(_d.EatFood(food), $"yendi: {food.Id}"); // M1 ucu; M4 yemekleri asagida
        await Frames(2);
        Check(Mathf.IsEqualApprox(_d.Player.MaxHp, hp0 + 50f), $"uc yemek: azami can +50 ({_d.Player.MaxHp})");
        Check(Mathf.IsEqualApprox(_d.State.Food.BonusStamina, 40f), "uc yemek: azami dayaniklilik +40 (Sim)");
        Check(_d.State.Food.Active.Count == 3, "uc yemek yuvasi dolu");
        // tarim4 (M4): yeni bir yemek (Menemen, ICERIK 15 #4) yuvalar bosalinca yenir
        _d.State.Food.Tick(10000f);
        await Frames(2);
        var menemen = _d.Data.Tool.Foods.Single(x => x.Id == "menemen");
        Bag.Add("menemen", 1);
        Check(_d.EatFood(menemen), "yendi: menemen (M4 yemegi)");
        Check(Mathf.IsEqualApprox(_d.State.Food.BonusMaxHp, 25f) && Mathf.IsEqualApprox(_d.State.Food.BonusStamina, 25f), "menemen: Can +25, Dayaniklilik +25");
    }
}
