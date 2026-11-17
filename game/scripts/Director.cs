using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyunun akisi: ilk 10 dakika (PLAN 1.4). Kurallar Kabarma.Sim'de; burasi dunyayi, oyuncuyu ve panoyu baglar.
/// </summary>
public partial class Director : Node3D
{
    public GameData Data { get; private set; }
    public GameState State { get; private set; }
    public World World { get; private set; }
    public FarmView FarmView { get; private set; }
    public Player Player { get; private set; }
    public Capa Capa { get; private set; }
    public Hud Hud { get; private set; }
    public Pod Pod { get; private set; }
    public Ocak Ocak { get; private set; }
    public Gidak Gidak { get; private set; }
    public KabarmaNight Night => State.Night;
    public bool NightActive => Night != null && Night.Phase != NightPhase.Dawn;
    public Vector3 OcakPos => Ocak?.GlobalPosition ?? Pod.GlobalPosition;
    public Vector3 GidakHome => (Ocak?.GlobalPosition ?? Vector3.Zero) + new Vector3(-6, 0, 5);
    public Bot Bot { get; private set; }
    public Pofuduk Pofuduk { get; private set; }
    public TasFirin Firin { get; private set; }
    public readonly System.Collections.Generic.List<Station> Stations = new();
    public BuildBridge Build { get; private set; }

    public enum Act { Day, Dusk, Night, Dawn, Report, Free }
    public Act Phase { get; private set; } = Act.Day;
    float _actT;
    float _hitStop;
    float _slowMo = 1f;
    public float SpeedScale = 1f; // --fast
    int _swollenAtDawn;
    bool _introDone;

    public static readonly Vector3 PodPos = new(9, 0, -9);

    public override void _Ready()
    {
        Data = GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        State = new GameState(Data);
        Fx.Root = this;
        Sfx.Root = this;

        World = new World { Name = "World" };
        AddChild(World);
        FarmView = new FarmView { Name = "Farm" };
        AddChild(FarmView);
        FarmView.Init(State.Farm, World, Data);

        Pod = new Pod { Name = "Pod" };
        AddChild(Pod);
        Pod.GlobalPosition = World.Ground(PodPos) + Vector3.Down * 0.1f;
        Pod.RotationDegrees = new Vector3(0, 200, 0);
        // M0 denemesi #1: sulama kovasi (Tripo web, 2.908 ucgen) kapsulun yaninda duruyor: Sulama basliginin dunyadaki isareti.
        var kova = Assets.Load("res://assets/models/kova/kova.glb");
        if (kova != null)
        {
            AddChild(kova);
            kova.GlobalPosition = World.Ground(PodPos + new Vector3(-2.2f, 0, 1.2f));
            kova.RotationDegrees = new Vector3(0, 35, 0);
        }

        Player = new Player { Name = "Player" };
        var pd = Data.Tool.Player;
        Player.WalkSpeed = pd.WalkSpeed; Player.SprintSpeed = pd.SprintSpeed; Player.JumpVelocity = pd.Jump; Player.MaxHp = pd.MaxHp;
        AddChild(Player);
        var spawn = World.Ground(PodPos + new Vector3(-1.5f, 0, 3.5f)) + Vector3.Up * 0.2f;
        Player.GlobalPosition = spawn;
        Player.RespawnPoint = spawn;
        Player.Yaw.Rotation = new Vector3(0, Mathf.DegToRad(150), 0); // inis kapsulunden cayira ve krater sutununa bakis

        Player.Wheel = rel => Hud?.WheelMotion(rel);
        FeelPanel = new FeelPanel { Name = "FeelPanel" };
        AddChild(FeelPanel);
        Hud = new Hud { Name = "Hud" };
        Hud.Init(this);
        AddChild(Hud);

        Capa = new Capa { Name = "Capa" };
        Capa.Init(this);
        AddChild(Capa);

        Gidak = new Gidak(Data.Creature("gidak"), this) { Name = "Gidak" };
        AddChild(Gidak);
        Gidak.GlobalPosition = World.Ground(new Vector3(-8, 0, 9));
        Gidak.Visible = false; // "grow" adiminda gelir

        GD.Print($"AUDIO ok {Sfx.CheckAll()}/{Sfx.Names.Length}");
        // M0 hat denemeleri: Pofuduk (dort ayak, iskelet + yurume) ve Tas Firin (adli parcalar).
        Pofuduk = new Pofuduk(this, new Vector3(-12, 0, 10)) { Name = "Pofuduk" };
        AddChild(Pofuduk);
        // Tas Firin artik Insa basligiyla kurulan bir istasyon (M1).
        Build = BuildBridge.TryCreate(this);

        State.Scenario.StepChanged += OnStepChanged;
        State.Farm.Eaten += p => { GD.Print($"EATEN plot {p.X},{p.Y}"); Sfx.Play("pop", FarmView.CellCenter(p.X, p.Y), -2f); };
        foreach (var l in Data.Scenario.Intro) Hud.Say(l);

        if (Main.Args.ContainsKey("bot")) { Bot = new Bot(this); AddChild(Bot); }
        // Sinama: --look=x,y,z:tx,ty,tz sabit bir kameradan bakar (varlik denetimi goruntuleri icin).
        if (Main.Args.TryGetValue("look", out var look))
        {
            var ps = look.Split(':');
            Vector3 V(string t) { var a = t.Split(','); return new Vector3(float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture)); }
            var cam = new Camera3D { Fov = 50f };
            AddChild(cam);
            cam.GlobalPosition = V(ps[0]);
            cam.LookAt(V(ps[1]));
            cam.MakeCurrent();
        }
        if (Main.Args.TryGetValue("skip", out var skip) && skip == "night") CallDeferred(nameof(SkipToNight));
        if (Main.Args.ContainsKey("zipview")) CallDeferred(nameof(ZipView));
        if (Main.Args.ContainsKey("showwheel")) GetTree().CreateTimer(1.0).Timeout += () => { Hud.OpenWheel(0); Hud.WheelMotion(new Vector2(30, 10)); };
    }

    void OnStepChanged(StepDef done, StepDef next)
    {
        Hud.Say(done.Done);
        if (next?.Start != null && done.Id != "sell") Hud.Say(next.Start); // rapor kapaninca okunur
        GD.Print($"STEP {done.Id} -> {next?.Id ?? "end"} t={Time.GetTicksMsec() / 1000f:0.0}");
        switch (next?.Id)
        {
            case "ocak": Capa.Select(Capa.Head.Insa); break;
            case "till": Capa.Select(Capa.Head.Toprak); break;
            case "water": Capa.Select(Capa.Head.Sulama); break;
            case "grow":
                Gidak.Visible = true;
                Gidak.GlobalPosition = World.Ground(GidakHome + new Vector3(-6, 0, 4));
                break;
            case "night": BeginDusk(); break;
            case "sell": Capa.Select(Capa.Head.Cekim); break;
        }
        if (done.Id == "sell") { Phase = Act.Report; _actT = 0; }
        if (next == null) GD.Print("CHAIN done");
        CallDeferred(nameof(CatchUp));
    }

    /// <summary>Yeni adim basladiginda onceden yapilmis isi say (ornegin surme sirasinda ekilen kare).</summary>
    void CatchUp()
    {
        var f = State.Farm;
        State.Scenario.SetProgress("till", f.Plots.Count());
        State.Scenario.SetProgress("plant", f.Plots.Count(p => p.Crop != null));
    }

    /// <summary>Sinama: bir normal ve bir kabarmis Zipzip'i kameranin onune koyar (varlik denetimi).</summary>
    void ZipView()
    {
        var def = Data.Creature("zipzip");
        var basePos = new Vector3(1.5f, 0, 4.5f);
        var a = new Zipzip(def, this, 1f, 1f, swollen: false) { Display = true }; AddChild(a); a.GlobalPosition = World.Ground(basePos);
        var b = new Zipzip(def, this, 1f, 1f, swollen: true) { Display = true }; AddChild(b); b.RotationDegrees = new Vector3(0, 200, 0); b.GlobalPosition = World.Ground(basePos + new Vector3(-2.5f, 0, 1.5f));
    }

    /// <summary>Sinama kisayolu: gunduz adimlarini kurallar uzerinden hizla yapar, gece basinda birakir.</summary>
    void SkipToNight()
    {
        var sc = State.Scenario;
        for (int i = 0; i < 10; i++) { State.Backpack.Add("stone", 1); sc.Report("mine"); }
        PlaceOcak(World.Ground(new Vector3(0, 0, 0)));
        var f = State.Farm;
        for (int x = 3; x < 6; x++) for (int y = -1; y < 2; y++) { f.Till(x, y); f.Plant(x, y, "turnip", State.Backpack); f.Water(x, y); }
        sc.SetProgress("till", 9); sc.SetProgress("plant", 9); sc.SetProgress("water", 9);
        while (f.MatureCount < 9) f.Tick(1f, true);
        Player.GlobalPosition = World.Ground(new Vector3(1, 0, -5)) + Vector3.Up * 0.3f;
        Player.Yaw.Rotation = new Vector3(0, Mathf.DegToRad(200), 0);
    }

    public Station PlaceStation(PlaceableDef def, Vector3 at, float yaw)
    {
        var st = new Station(this, def);
        AddChild(st);
        st.GlobalPosition = at;
        st.Rotation = new Vector3(0, yaw, 0);
        Stations.Add(st);
        Sfx.Play("place", at, 2f);
        Fx.Burst(at + Vector3.Up * 0.3f, Palette.Ground, 26, 5f, 0.12f, 0.8f);
        GD.Print($"PLACED {def.Id} at {at}");
        if (def.Id == "tezgah") State.Scenario.Report("tezgah_kur");
        if (def.Id == "tas_firin") State.Scenario.Report("firin_kur");
        return st;
    }

    public void OnProduced(RecipeDef r)
    {
        if (r.Id == "civi") State.Scenario.Report("civi");
        if (r.Id == "salgam_kasasi") State.Scenario.Report("paketle");
        GD.Print($"PRODUCED {r.Id} civi={State.Backpack.Count("civi")} soup={State.Backpack.Count("salgam_corbasi")} crates={State.Backpack.Count("salgam_kasasi")}");
    }

    // ---------- Menuler (istasyon, canta, satis) ----------

    public bool MenuOpen => Hud.MenuOpen;

    public void OpenStationMenu(Station st)
    {
        var items = new System.Collections.Generic.List<Hud.MenuItem>();
        foreach (var r in st.Queue.Recipes)
        {
            var rr = r;
            string cost = string.Join(" + ", r.In.Select(kv => $"{kv.Value} {ItemNames.Of(kv.Key)}"));
            items.Add(new Hud.MenuItem($"{r.Name} ×{r.Out}   ←   {cost}   ({r.Seconds:0.#} sn)", () => StationQueue.CanAfford(rr, State.Backpack), () =>
            {
                if (st.Queue.Enqueue(rr, State.Backpack)) { Sfx.Play("place", st.GlobalPosition, -4f); GD.Print($"QUEUED {rr.Id}"); }
                else Hud.Hint("Malzeme yetmiyor ya da istasyon başka bir şey üretiyor", 1.5f);
            }));
        }
        Hud.ShowMenu(st.Def.Name, items, "1–9: sıraya ekle  ·  E / Esc: kapat");
    }

    /// <summary>Kemer yuvasini kullanir: yemekse yer, degilse ne oldugunu soyler.</summary>
    public void UseBelt(int slot)
    {
        var id = State.Belt.At(slot);
        if (id == null) return;
        Hud.FlashBelt(slot);
        var food = Data.Tool.Foods.FirstOrDefault(f => f.Id == id);
        if (food != null) { EatFood(food); return; }
        if (id == "turnip_seed") { Capa.Select(Capa.Head.Toprak); Hud.Hint("Toprak başlığı: sürülmüş kareye ek", 1.2f); return; }
        if (id == "civi") { Capa.Select(Capa.Head.Civi); return; }
        Hud.Hint($"{ItemNames.Of(id)} ×{State.Backpack.Count(id)}", 1f);
    }

    public bool EatFood(FoodDef ff)
    {
        if (State.Backpack.Count(ff.Id) <= 0) { Hud.Hint($"{ff.Name} yok", 1f); return false; }
        if (!State.Food.Eat(ff)) { Hud.Hint("Üç yemek yuvası dolu", 1.2f); return false; }
        State.Backpack.Remove(ff.Id, 1);
        Sfx.Play("pickup", null, -2f);
        Hud.Hint($"{ff.Name}: doydun, güçlendin", 1.5f);
        GD.Print($"ATE {ff.Id}");
        State.Scenario.Report("corba");
        return true;
    }

    public void OpenBagMenu()
    {
        var items = new System.Collections.Generic.List<Hud.MenuItem>();
        foreach (var f in Data.Tool.Foods)
        {
            var ff = f;
            items.Add(new Hud.MenuItem($"Ye: {f.Name}  (+{f.MaxHp:0} can, +{f.Regen:0.#}/sn, {f.Seconds / 60f:0} dk)  ·  elde {State.Backpack.Count(f.Id)}", () => State.Backpack.Count(ff.Id) > 0, () => EatFood(ff)));
        }
        foreach (var g in State.Backpack.Contents.Select(c => c.id).Distinct().Take(8 - items.Count))
        {
            var id = g;
            items.Add(new Hud.MenuItem($"{ItemNames.Of(id)} ×{State.Backpack.Count(id)}   ·   {(State.Belt.Contains(id) ? "kemerde (çıkar)" : "kemere tak")}", () => true, () => { State.Belt.Toggle(id); OpenBagMenu(); }));
        }
        var lines = State.Backpack.Contents.GroupBy(c => c.id).Select(g => $"{ItemNames.Of(g.Key)} {g.Sum(c => c.count)}");
        Hud.ShowMenu($"Sırt Çantası · {State.Backpack.UsedSlots}/{State.Backpack.Slots} yuva", items, string.Join("   ·   ", lines) + $"\nYemek yuvaları: {string.Join(", ", State.Food.Active.Select(a => $"{a.food.Name} {a.left / 60f:0.0} dk"))}");
    }

    public void OpenSellMenu()
    {
        var items = new System.Collections.Generic.List<Hud.MenuItem>();
        foreach (var kv in Data.Tool.Prices)
        {
            var id = kv.Key;
            int have = id == "egg" ? State.Eggs : State.Backpack.Count(id);
            items.Add(new Hud.MenuItem($"Sat: {ItemNames.Of(id)} ×{have}   ({kv.Value} Kredi/adet)", () => (id == "egg" ? State.Eggs : State.Backpack.Count(id)) > 0, () =>
            {
                int n = id == "egg" ? State.Eggs : State.Backpack.Count(id);
                int v = State.SellItem(id, n);
                if (v > 0) { if (id == "salgam_kasasi") State.Scenario.Report("kasa_sat"); Pod.Launch(); Sfx.Play("rocket", Pod.PadCenter, 0f); Hud.Big($"+{v} Kredi", Palette.Warning, 2.5f); GD.Print($"SOLD {id} x{n} +{v}"); OpenSellMenu(); }
            }));
        }
        Hud.ShowMenu("Kargo Rampası · Kooperatif alım fiyatları", items, "Kabarık ürünleri Çekim başlığıyla rampaya taşı.  ·  E / Esc: kapat");
    }

    public void PlaceOcak(Vector3 at)
    {
        Ocak = new Ocak { Name = "Ocak" };
        AddChild(Ocak);
        Ocak.GlobalPosition = at;
        Player.RespawnPoint = at + new Vector3(0, 0.3f, -2.5f);
        Sfx.Play("place", at, 2f);
        Fx.Burst(at + Vector3.Up * 0.3f, Palette.Ground, 30, 5f, 0.12f, 0.8f);
        Fx.Burst(at + Vector3.Up * 1f, Palette.Warning, 16, 3f, 0.1f, 1f, true, -1f);
        State.Scenario.Report("ocak");
    }

    // ---------- Gece ----------

    void BeginDusk()
    {
        Phase = Act.Dusk;
        _actT = 0;
    }

    void BeginNight()
    {
        Phase = Act.Night;
        _actT = 0;
        var n = State.StartNight();
        n.SwellNow += () =>
        {
            int count = State.Farm.SwellMature(n.Ks, 0).Count;
            Hud.Say(Data.Scenario.NightLines["swell"]);
            Gidak.Swell(true);
            Pofuduk.Swell(true);
            Hud.Say(Data.Scenario.NightLines["gidak"]);
            GD.Print($"NIGHT swell crops={count}");
        };
        n.WaveStarted += (i, w) =>
        {
            if (i == 0) Hud.Say(Data.Scenario.NightLines["wave"]);
            Hud.Big($"DALGA {i + 1}", Palette.MayaTeal, 2f);
            GD.Print($"NIGHT wave {i + 1} count={w.Count}");
        };
        n.LevelUp += lv =>
        {
            Sfx.Play("levelup", null, -2f);
            Fx.Burst(Player.GlobalPosition + Vector3.Up * 0.6f, Palette.MayaViolet, 24, 3.5f, 0.07f, 0.7f, true, -1f);
            if (lv == 1) Hud.Say(Data.Scenario.NightLines["level"]);
        };
        n.CardTaken += (c, r) => { Hud.Big($"{c.Name} {(r > 1 ? "· Rütbe " + r : "")}", Palette.MayaViolet, 1.6f); GD.Print($"NIGHT card {c.Id} rank={r}"); };
        n.DawnNow += BeginDawn;
        Capa.Select(Capa.Head.Tirpan);
        Sfx.Play("siren", null, -2f);
        Sfx.Music(true);
        Hud.Say(Data.Scenario.NightLines["alarm"]);
        Hud.Big("KABARMA", Palette.MayaViolet, 3f);
        GD.Print("NIGHT start");
    }

    void BeginDawn()
    {
        Phase = Act.Dawn;
        _actT = 0;
        // Kalan dusmanlar soner ve kacar.
        foreach (var z in Zipzip.All.ToArray()) { Fx.Burst(z.Center, Palette.MayaTeal, 10, 3f, 0.1f, 0.6f, true, -1f); z.QueueFree(); }
        Zipzip.All.Clear();
        Gidak.Swell(false);
        Pofuduk.Swell(false);
        Sfx.Music(false);
        Pod.BecomeRamp();
        Hud.ShowOffer(null, Night);
        _slowMo = 1f;
        _swollenAtDawn = State.Farm.Plots.Count(p => p.Swollen != null && p.Planted);
        Hud.Big("ŞAFAK", Palette.Warning, 3f);
        GD.Print($"NIGHT dawn kills={Night.Kills} level={Night.Level} maya={Night.Maya:0} swollenLeft={_swollenAtDawn}");
        State.Scenario.Report("night");
        if (_swollenAtDawn == 0)
        {
            // Satacak kabarik urun kalmadi: satis adimi bos gecer, rapor yine gelir.
            Hud.Say("Tarlada kabarık bir şey kalmamış. Kooperatif bunu not etti. Bir dahaki sefere tarlanın başında dur.");
            State.Scenario.Report("sell");
        }
    }

    public void OnZipzipKilled(Zipzip z)
    {
        if (Night == null || Night.Phase == NightPhase.Dawn) return;
        Night.OnKill(z.Def);
        Fx.FlyTo(z.Center, () => Player.BackpackPoint.GlobalPosition, Palette.MayaViolet, 0.1f);
        RefreshOffer();
    }

    public void OnAnimalHit() { }
    public FeelPanel FeelPanel { get; private set; }

    /// <summary>Girise gorsel tepki verildi (gecikme olcumu, F1 paneli).</summary>
    public void MarkReaction() => FeelPanel?.MarkReaction();

    public int Harvests { get; private set; }
    public void OnHarvest(int n)
    {
        Harvests += n;
        State.Scenario.SetProgress("hasat", State.Backpack.Count("turnip"));
        GD.Print($"HARVEST {n} total={Harvests} turnips={State.Backpack.Count("turnip")}");
    }

    CardOffer _shownOffer;
    void RefreshOffer()
    {
        if (Night == null) return;
        if (Night.Offer != _shownOffer)
        {
            _shownOffer = Night.Offer;
            Hud.ShowOffer(_shownOffer, Night);
        }
        // Tek oyuncuda secim sirasinda zaman %30'a yavaslar (GDD 15).
        _slowMo = _shownOffer != null ? Data.Cards.SlowMoScale : 1f;
    }

    public void ChooseCard(int i)
    {
        if (Night?.Offer == null) return;
        Night.Choose(i);
        RefreshOffer();
    }

    public void HitStop(float seconds) => _hitStop = Mathf.Max(_hitStop, seconds);

    void SpawnZipzip()
    {
        var def = Data.Creature(Data.Kabarma.FirstNight.Enemy);
        var center = FarmCenter();
        float a = (float)GD.RandRange(0, Mathf.Tau);
        float r = (float)GD.RandRange(22, 28);
        var p = World.Ground(center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
        var z = new Zipzip(def, this, Night.HpMult, Night.DamageMult, swollen: true);
        AddChild(z);
        z.GlobalPosition = p;
        Fx.Burst(p + Vector3.Up * 0.5f, Palette.MayaViolet, 14, 4f, 0.12f, 0.6f, true, -2f);
    }

    public Vector3 FarmCenter()
    {
        var plots = State.Farm.Plots.ToList();
        if (plots.Count == 0) return OcakPos;
        var c = Vector3.Zero;
        foreach (var p in plots) c += FarmView.CellCenter(p.X, p.Y);
        return c / plots.Count;
    }

    // ---------- Satis ----------

    public void SellCrop(SwollenCrop s)
    {
        if (s.Sold) return;
        s.Sold = true;
        int v = State.Sell(s.CropId, s.Size);
        var tw = s.CreateTween();
        tw.TweenProperty(s, "global_position", Pod.PadCenter + Vector3.Up * s.Radius, 0.3f);
        tw.TweenProperty(s, "scale", Vector3.One * 0.01f, 0.4f).SetDelay(0.2f);
        tw.TweenCallback(Callable.From(s.QueueFree));
        Pod.Launch();
        Sfx.Play("rocket", Pod.PadCenter, 2f);
        Hud.Big($"+{v} Kredi", Palette.Warning, 3.5f);
        Fx.Burst(Pod.PadCenter + Vector3.Up, Palette.Warning, 40, 8f, 0.12f, 1.2f, true, 6f);
        GD.Print($"SOLD {s.CropId} {s.Size.Id} +{v} credits={State.Credits}");
        State.Scenario.Report("sell");
    }

    // ---------- Kare ----------

    public override void _Process(double delta)
    {
        float realDt = (float)delta;
        if (_hitStop > 0) { _hitStop -= realDt; Engine.TimeScale = 0.08f * SpeedScale; }
        else Engine.TimeScale = _slowMo * SpeedScale;
        float dt = realDt; // delta zaten TimeScale ile olcekli
        _actT += dt;

        // Giris
        bool fire = Input.IsActionPressed("fire");
        bool firePressed = Input.IsActionJustPressed("fire");
        if (Bot != null) { fire = Bot.Fire; firePressed = Bot.FirePressed; Bot.FirePressed = false; }
        if (Hud.MenuOpen)
        {
            for (int i = 0; i < 9; i++) if (Input.IsActionJustPressed($"head_{i + 1}")) Hud.ActivateMenu(i);
            if (Input.IsActionJustPressed("interact") || Input.IsActionJustPressed("ui_cancel") || Input.IsActionJustPressed("bag")) { Hud.HideMenu(); _menuClosedThisFrame = true; }
            fire = false; firePressed = false;
        }
        else
        {
            bool alt = Input.IsKeyPressed(Key.Alt);
            for (int i = 0; i < 8; i++)
                if (Input.IsActionJustPressed($"head_{i + 1}")) { if (alt) UseBelt(i); else Capa.Select((Capa.Head)i); }
            if (Input.IsActionJustPressed("bag")) OpenBagMenu();
            // Baslik carki: Q basili tut, fareyle sec, birak
            if (Input.IsActionJustPressed("head_wheel")) { Hud.OpenWheel((int)Capa.Current); Player.LookLocked = true; }
            if (Hud.WheelOpen && Input.IsActionJustReleased("head_wheel")) { int sel = Hud.CloseWheel(); Player.LookLocked = false; if (sel >= 0) Capa.Select((Capa.Head)sel); }
            if (Capa.Current == Capa.Head.Insa)
            {
                if (Input.IsActionJustPressed("build_next")) Capa.CyclePlaceable(1);
                if (Input.IsActionJustPressed("build_prev")) Capa.CyclePlaceable(-1);
                if (Input.IsActionJustPressed("build_rotate")) Capa.RotateGhost();
            }
            else if (!Hud.WheelOpen)
            {
                int n = Capa.HeadNames.Length;
                if (Input.IsActionJustPressed("build_next")) Capa.Select((Capa.Head)(((int)Capa.Current + 1) % n));
                if (Input.IsActionJustPressed("build_prev")) Capa.Select((Capa.Head)(((int)Capa.Current + n - 1) % n));
            }
        }
        if (Input.IsActionJustPressed("card_1")) ChooseCard(0);
        if (Input.IsActionJustPressed("card_2")) ChooseCard(1);
        if (Input.IsActionJustPressed("card_3")) ChooseCard(2);
        if (Input.IsActionJustPressed("interact") && !Hud.MenuOpen && !_menuClosedThisFrame) Interact();
        _menuClosedThisFrame = false;
        // Yemek guclendirmesi: ek can ve yenilenme (GDD 3.3)
        State.Food.Tick(dt);
        Player.MaxHp = Data.Tool.Player.MaxHp + State.Food.BonusMaxHp;
        if (!Player.Down && Player.Hp < Player.MaxHp) Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + State.Food.Regen * dt);
        if (Input.IsActionJustPressed("ui_accept") && Hud.ReportOpen) CloseReport();

        Capa.Process(dt, fire && !Hud.ReportOpen, firePressed && !Hud.ReportOpen);

        // Ekin: gunduz isik var; gece ve aksamda buyume durur (GDD 8.2 Isik).
        bool light = Phase is Act.Day or Act.Dawn or Act.Report or Act.Free;
        State.Farm.Tick(dt, light);
        State.Scenario.SetProgress("grow", State.Farm.MatureCount);

        // Gidak yakinsa ipucu
        if (Gidak.Visible && !Gidak.Swollen && Gidak.Fed == 0 && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < 3f && State.Backpack.Count("wheat") > 0)
            Hud.Hint("E: Gıdak'a buğday ver");

        switch (Phase)
        {
            case Act.Dusk:
                World.SetDay(Mathf.Clamp(_actT / 6f, 0, 1));
                if (_actT > 6f) BeginNight();
                break;
            case Act.Night:
                var n = Night;
                World.SetNight(Mathf.Clamp(_actT / Data.Kabarma.FirstNight.AlarmSeconds, 0, 1));
                n.Tick(dt);
                if (n.Phase == NightPhase.Dawn) break;
                int due = n.SpawnDue();
                for (int i = 0; i < due; i++) SpawnZipzip();
                if (due == 0 && Zipzip.All.Count(z => !z.Dead) == 0) n.WaveCleared();
                if (n.ShouldAutoPick) ChooseCard(0);
                RefreshOffer();
                break;
            case Act.Dawn:
            case Act.Report:
            case Act.Free:
                // Safak: Maya renkleri soner, pembe-altin sabah, sonra gune doner.
                float since = _sinceDawn += dt;
                World.SetNight(1f - Mathf.Clamp(since / 5f, 0, 1));
                World.SetDawn(Mathf.Clamp(since / 3f, 0, 1) * (1f - Mathf.Clamp((since - 20f) / 30f, 0, 0.6f)));
                if (Phase == Act.Report && _actT > 3.5f && !Hud.ReportOpen && !_reportShown) { _reportShown = true; Hud.ShowReport(State, Night, State.Farm.Plots.Count(p => p.Swollen != null && p.Planted)); GD.Print("REPORT shown"); }
                break;
        }
    }

    bool _reportShown;
    float _sinceDawn;

    bool _menuClosedThisFrame;

    public void CloseReport()
    {
        if (!Hud.ReportOpen) return;
        Hud.HideReport();
        Phase = Act.Free;
        var cur = State.Scenario.Current;
        if (cur?.Start != null) Hud.Say(cur.Start);
        if (cur?.Id == "tezgah_kur") Capa.Select(Capa.Head.Insa);
    }

    public void Interact()
    {
        var st = Stations.Where(s => s.InReach(Player.GlobalPosition)).OrderBy(s => s.GlobalPosition.DistanceTo(Player.GlobalPosition)).FirstOrDefault();
        if (st != null) { OpenStationMenu(st); return; }
        if (Pod.IsRamp && new Vector2(Player.GlobalPosition.X - Pod.PadCenter.X, Player.GlobalPosition.Z - Pod.PadCenter.Z).Length() < 4.5f) { OpenSellMenu(); return; }
        if (Gidak.Visible && !Gidak.Swollen && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < 3f && State.Backpack.Remove("wheat", 1))
            Gidak.Feed();
    }
}
