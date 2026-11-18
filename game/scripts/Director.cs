using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyunun akisi: ilk 10 dakika (PLAN 1.4). Kurallar Kabarma.Sim'de; burasi dunyayi, oyuncuyu ve panoyu baglar.
/// Parcalar: Director.Input.cs (giris), Director.Night.cs (gece akisi), Director.Menus.cs (istasyon, menu, rampa),
/// Director.Scenario.cs (adim tepkileri).
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
    float _slowMo = 1f;
    public float SpeedScale = 1f; // --fast
    bool _introDone;

    public static readonly Vector3 PodPos = new(9, 0, -9);

    public override void _Ready()
    {
        Events.Clear();
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
        WireFeel(); // Director.Feel.cs: his ayarlari oyuncuya (geri tepme yayi, sarsinti)
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
        InitCreatures(); // Director.Creatures.cs: Zipzip suruleri, poz kutuphanesi, gorunus sinamasi
        // Tas Firin artik Insa basligiyla kurulan bir istasyon (M1).
        Build = BuildBridge.TryCreate(this);
        SetupPens(); // Director.Pen.cs: agil, kapilar, Yemlik, Folluk (yapi setinden sonra)
        SetupRanch(); // Director.Ranch.cs: hayvancilik (M2 hayvan akisi)
        InitAtalar(); // boss/Director.Ata.cs: Ata Taslari, arenalar, Ata dovusu (GDD §18.1)

        State.Scenario.StepChanged += OnStepChanged;
        WireScenario(); // Director.Scenario.cs: olay -> adim sayaci
        InitEconomy();  // Director.Economy.cs: odeme, kargo, yerlestirilebilirler
        InitPower();    // power/Director.Power.cs: guc agi, Kablo, sinyal (guc akisi)
        WireNightAndDay(); // Director.Calendar.cs: takvim, uyku, Defter olaylari
        InitExplore();     // Director.Explore.cs: harita, kesif noktalari, Seyir Kulesi, mancinik (dunya2)
        WireProgress();    // Director.Progress.cs: Ustalik XP'si, is emri olculeri, basarim ve odul bildirimleri
        State.Farm.Eaten += p => { GD.Print($"EATEN plot {p.X},{p.Y}"); Sfx.Play("pop", FarmView.CellCenter(p.X, p.Y), -2f); };
        Pod.BeginHatchIntro(this); // Pod.Hatch.cs: kapak acilir, Suna giris repliklerini soyler (PLAN 1.4, 0:00)
        InitNet(); // Director.Net.cs (ortak): --coop varsa NetSync; tek oyuncuda null

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
        if (Main.Args.ContainsKey("ragtest")) GetTree().CreateTimer(1.5).Timeout += () => Player.Hurt(999, Player.GlobalPosition + Player.Visual.GlobalBasis.Z * 2f);
        if (Main.Args.ContainsKey("showwheel")) GetTree().CreateTimer(1.0).Timeout += () => { Hud.OpenWheel(0); Hud.WheelMotion(new Vector2(30, 10)); };
    }

    public override void _ExitTree() => Events.Clear();

    /// <summary>Sinama: bir normal ve bir kabarmis Zipzip'i kameranin onune koyar (varlik denetimi).</summary>
    void ZipView()
    {
        var def = Data.Creature("zipzip");
        var basePos = new Vector3(1.5f, 0, 4.5f);
        var a = new Zipzip(def, this, 1f, 1f, swollen: false) { Display = true }; AddChild(a); a.GlobalPosition = World.Ground(basePos);
        var b = new Zipzip(def, this, 1f, 1f, swollen: true) { Display = true }; AddChild(b); b.RotationDegrees = new Vector3(0, 200, 0); b.GlobalPosition = World.Ground(basePos + new Vector3(-2.5f, 0, 1.5f));
    }

    public void OnAnimalHit() { }
    public FeelPanel FeelPanel { get; private set; }

    /// <summary>Girise gorsel tepki verildi (gecikme olcumu, F1 paneli).</summary>
    public void MarkReaction() => FeelPanel?.MarkReaction();

    public Vector3 FarmCenter()
    {
        var plots = State.Farm.Plots.ToList();
        if (plots.Count == 0) return OcakPos;
        var c = Vector3.Zero;
        foreach (var p in plots) c += FarmView.CellCenter(p.X, p.Y);
        return c / plots.Count;
    }

    // ---------- Kare ----------

    public override void _Process(double delta)
    {
        float realDt = (float)delta;
        if (HitStopActive()) Engine.TimeScale = Feel.Data.HitStop.TimeScale * SpeedScale; // Director.Feel.cs: gercek saatle
        else Engine.TimeScale = _slowMo * SpeedScale;
        NetTimeScale(); // Director.Net.cs (ortak): ortak oyunda zaman yavaslamaz
        float dt = realDt; // delta zaten TimeScale ile olcekli
        _actT += dt;

        ReadInput(out bool fire, out bool firePressed); // Director.Input.cs
        // Yemek guclendirmesi: ek can ve yenilenme (GDD 3.3)
        State.Food.Tick(dt);
        Player.MaxHp = Data.Tool.Player.MaxHp + State.Food.BonusMaxHp;
        if (!Player.Down && Player.Hp < Player.MaxHp) Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + State.Food.Regen * dt);
        if (Input.IsActionJustPressed("ui_accept") && Hud.ReportOpen) CloseReport();
        TickEconomy(dt); // Director.Economy.cs: gecikmeli odeme, kasa izleme
        TickKemer(dt);   // Director.Belt.cs: elde tutulan esya, el uretim kuyrugu (karar 23)
        TickZincir(dt);  // Director.Zincir.cs: Tarayici, Ocak basamagi, arastirma geri bildirimi (zincir M2)

        Capa.Process(dt, fire && !Hud.ReportOpen, firePressed && !Hud.ReportOpen);

        // Ekin: gunduz isik var; gece ve aksamda buyume durur (GDD 8.2 Isik).
        bool light = Phase is Act.Day or Act.Dawn or Act.Report or Act.Free;
        light = light && ClockDaylight; // Director.Calendar.cs: takvimin normal gecesinde de buyume durur
        State.Farm.Tick(dt * State.Mastery.Mult("crop_growth"), light); // Ustalik: Cabuk Filiz
        State.Scenario.SetProgress("grow", State.Farm.MatureCount);

        // Gidak yakinsa ipucu
        if (Gidak.Visible && !Gidak.Swollen && Gidak.Fed == 0 && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < 3f && State.Backpack.Count("wheat") > 0)
            Hud.Hint("{act:interact}: Gıdak'a buğday ver");

        TickPhase(dt); // Director.Night.cs
    }
}
