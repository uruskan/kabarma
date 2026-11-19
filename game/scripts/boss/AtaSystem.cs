using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Atalar (GDD §18, §18.1): her Ata icin arenasini (Ata Tasi, sunak, sutunlar) kurar; adagi (canta esyasi etkilesimle,
/// kabarik urun Cekim'le sunaga) alir; adak tamamlaninca ya da F1 gelistirici cagirmasiyla Ata'yi indirir; dovusu
/// Sim kuraliyla (AtaRules) yurutur; odulu verir. Durum State.Atalar'da (duz, kayitli); bu dugum yalniz gorunus ve girdi.
/// </summary>
public partial class AtaSystem : Node3D
{
    readonly Director _d;
    readonly Dictionary<string, AtaStone> _stones = new();
    /// <summary>Silahla vurulabilen canli Ata hedefleri (govde, kovan, yardimci). Mermiler Director.AtaTargetsNear ile sorar.</summary>
    public readonly List<IAtaTarget> Targets = new();
    public AtaActor Actor { get; private set; }
    public AtaBar Bar { get; private set; }
    AtalarFile Data => _d.Data.Atalar;
    public AtaProgress Progress => _d.State.Atalar;
    public AtaFight Fight => Progress.Active;
    /// <summary>Ekip boyu (GDD §18: Ata cani 1 + 0,5 × (n − 1)): ortak oyunda oyuncu sayisi (NetSync), tek oyuncuda 1.</summary>
    public int TeamSize => _d.Net?.PlayerCount ?? 1;
    /// <summary>Sinama ve rapor: yenilen Atalar (sirayla) ve dovus sureleri.</summary>
    public readonly List<string> DefeatedLog = new();
    /// <summary>Sinama: Dede'ye isabet eden firlatilmis boynuz parcasi, kirkma vurusu, sutun toslamasi.</summary>
    public int ShardHits, Shears, PillarStuns;
    float _awayT;

    public AtaSystem(Director d) { _d = d; }

    public override void _Ready()
    {
        Bar = new AtaBar { Name = "AtaBar" };
        AddChild(Bar);
        foreach (var def in Data.Atalar)
        {
            // GECICI (ata4): arena merkezi yer tutucuysa (atalar.json arena.temporary) ve biyom akisi gercek Ata Tasi noktasini
            // henuz koymadiysa dunya kurulmaz; Place(id) ile (sinama, gelistirici) kurulur. Gercek nokta cozulunce kendiliginden kurulur.
            if (IsTemporary(def)) continue;
            BuildArena(def);
        }
        // Kayittan suren dovus geldiyse Ata arenada yeniden kurulur.
        if (Fight != null) CallDeferred(nameof(Respawn));
        GD.Print($"ATALAR arenas={string.Join(",", Data.Atalar.Where(a => _stones.ContainsKey(a.Id)).Select(a => $"{a.Id}@{ArenaCenter(a).X:0},{ArenaCenter(a).Z:0}/r{ArenaRadius(a):0}{(Spot(a).FromSite ? ":" + Spot(a).SiteId : ":yedek")}"))} temporary={string.Join(",", Data.Atalar.Where(IsTemporary).Select(a => a.Id))}");
    }

    /// <summary>Arena merkezi biyom akisinin noktasini beklerken yer tutucu mu (ve henuz kurulmadi).</summary>
    public bool IsTemporary(AtaDef def) => !string.IsNullOrEmpty(def.Arena.Temporary) && !Spot(def).FromSite && !_stones.ContainsKey(def.Id);

    /// <summary>Gecici arenayi simdi kurar (sinama ve gelistirici). Kuruluysa false.</summary>
    public bool Place(string id)
    {
        var def = Data.Get(id);
        if (_stones.ContainsKey(id)) return false;
        BuildArena(def);
        return true;
    }

    void BuildArena(AtaDef def)
    {
        var stone = new AtaStone(_d, def, new Vector3(Spot(def).X, 0, Spot(def).Z)) { Name = "AtaTasi_" + def.Id };
        AddChild(stone);
        _stones[def.Id] = stone;
        BuildPillars(def);
        BuildProps(def);
        _d.FeelPanel?.AddButton($"Ata çağır: {def.Name}", () => DevSummon(def.Id)); // GDD §18.1 M2 sinamasi
    }

    void Respawn() { if (Fight != null && Actor == null) SpawnActor(Data.Get(Fight.AtaId), arriving: false); }

    readonly Dictionary<string, AtaArenaSpot> _spots = new();
    /// <summary>Arena yeri: dunya2'nin Ata Tasi noktasi (arazideyse) ya da atalar.json yedegi (AtaArena.Resolve).</summary>
    public AtaArenaSpot Spot(AtaDef def) => _spots.TryGetValue(def.Id, out var s) ? s : _spots[def.Id] = AtaArena.Resolve(_d.State, def);
    public Vector3 ArenaCenter(AtaDef def) => _d.World.Ground(new Vector3(Spot(def).X, 0, Spot(def).Z));
    public float ArenaRadius(AtaDef def) => Spot(def).Radius;
    public Vector3 StonePos(AtaDef def) => _stones.TryGetValue(def.Id, out var s) ? s.GlobalPosition : ArenaCenter(def);
    public Vector3 AltarPos(AtaDef def) => _stones.TryGetValue(def.Id, out var s) ? s.Altar : ArenaCenter(def);
    public AtaDef Def(string id) => Data.Get(id);
    public IReadOnlyList<Vector3> Pillars(AtaDef def) => def.Arena.Pillars.Select(p => _d.World.Ground(ArenaCenter(def) + new Vector3(p[0], 0, p[1]))).ToList();

    void BuildPillars(AtaDef def)
    {
        foreach (var p in Pillars(def))
        {
            var kit = new MeshKit();
            float r = def.Arena.PillarRadius;
            kit.Cylinder(r * 0.8f, r, 3.2f, MeshKit.At(0, 1.4f, 0), Palette.Rock, 9);
            kit.Pillow(new Vector3(r * 1.1f, 0.5f, r * 1.1f), MeshKit.At(0, 3.05f, 0), Palette.RockDark, 0.4f, 9, 4);
            kit.Sphere(r * 0.35f, MeshKit.At(r * 0.5f, 2.2f, r * 0.55f), Palette.Plant.Lerp(Palette.Shadow, 0.3f), 8, 5); // yosun
            var mi = kit.Instance();
            mi.Name = "AtaSutun";
            AddChild(mi);
            mi.GlobalPosition = p + Vector3.Down * 0.3f;
            mi.RotationDegrees = new Vector3(0, GD.Randf() * 360f, 0);
            var body = new StaticBody3D();
            body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = r, Height = 3.2f }, Position = new Vector3(0, 1.6f, 0) });
            mi.AddChild(body);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var p = _d.Player;
        if (Fight == null)
        {
            SetSwell(1f);
            foreach (var def in Data.Atalar) if (_stones.ContainsKey(def.Id)) AltarTick(def, p, dt);
            return;
        }
        var def0 = Data.Get(Fight.AtaId);
        if (Actor == null || !IsInstanceValid(Actor)) { SpawnActor(def0, arriving: false); return; }
        Fight.AddsAlive = Targets.OfType<AtaMinion>().Count(m => m.Alive);
        foreach (var c in AtaRules.Tick(Fight, def0, dt)) Dispatch(def0, c);
        // savas: yanik ve zehir hasari, yanik yunu yer (AtaRules.TickStatus → Combat.Tick)
        var stCmds = new List<AtaCommand>(); var stAreas = new List<CombatArea>();
        AtaRules.TickStatus(Fight, Data, _d.Data.Combat, def0, dt, stCmds, stAreas);
        foreach (var c in stCmds) Dispatch(def0, c);
        if (Fight == null || Actor == null) return;
        _d.Savas?.Areas(Actor, stAreas);
        foreach (var m in Targets.OfType<AtaMinion>().ToList()) m.TickStatus(dt);
        ColdTick(def0, dt);
        SetSwell(AtaRules.PlayerSwell(Fight, def0));
        // Oyuncu arenadan uzaklasti: Ata merkezine doner; uzun sure yoksa dovus biter (adak geri gelmez, M2).
        float away = new Vector2(p.GlobalPosition.X - ArenaCenter(def0).X, p.GlobalPosition.Z - ArenaCenter(def0).Z).Length();
        _awayT = away > def0.Arena.Leash ? _awayT + dt : 0f;
        if (_awayT > Data.AbandonSeconds) { GD.Print($"ATA abandon {def0.Id}"); _d.Hud.Say($"{def0.Name} ormana çekildi."); Cleanup(); AtaRules.Abandon(Progress); }
    }

    void AltarTick(AtaDef def, Player p, float dt)
    {
        var altar = AltarPos(def);
        bool kab = Progress.IsDefeated(def.Id);
        DrumTick(def, dt, kab);
        // Kabarik urun sunaga birakildi (Cekim): adakta eksikse kabul.
        foreach (var sc in GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>())
        {
            if (sc.Held || sc.Gone || sc.GlobalPosition.DistanceTo(altar) > def.Arena.AltarReach) continue;
            if (!AtaRules.OfferSwollen(Progress, def, sc.CropId, kab)) continue;
            sc.Sold = true;
            Fx.Burst(sc.GlobalPosition, Palette.MayaTeal, 18, 4f, 0.14f, 0.7f, true);
            Sfx.Play("ui_onay", altar);
            sc.QueueFree();
            _stones[def.Id].Refresh();
            TrySummon(def);
        }
        if (p.GlobalPosition.DistanceTo(altar) > def.Arena.AltarReach + 1f) return;
        if (kab)
        {
            // Kabarik Ata (GDD §18): ayni adak + KS × 50 Maya. KS Sekme ile (ui_focus_next) secilir.
            if (Input.IsActionJustPressed("ui_focus_next")) CycleKabarikKs();
            _d.Hud.Hint($"Kabarık {def.Name} · KS {KabarikKs} · {MayaHave}/{AtaRules.MayaCost(Data, KabarikKs)} Maya · {OfferingText(def, true)}{DrumText(def, true)}  ·  {{act:interact}} adak sun  ·  Sekme: KS");
            if (Input.IsActionJustPressed("interact")) Offer(def);
            return;
        }
        _d.Hud.Hint($"Ata Taşı · {def.OfferingName}: {OfferingText(def)}{DrumText(def, false)}  ·  {{act:interact}} adak sun");
        if (Input.IsActionJustPressed("interact")) Offer(def);
    }

    public string OfferingText(AtaDef def, bool kab = false) => string.Join("  ", def.Offering.Select(o =>
        $"{(o.Swollen ? "Kabarık " : "")}{ItemNames.Of(o.Item)} {o.Count - AtaRules.Missing(Progress, def, o, kab)}/{o.Count}"));

    string DrumText(AtaDef def, bool kab) => def.PowerWatts <= 0 ? "" : $"  Davul {Progress.Record(kab ? AtaRules.KabarikId(def.Id) : def.Id).PowerT:0}/{def.PowerSeconds:0} sn ({def.PowerWatts:0} W)";

    // ---------- Kabarik Ata ----------

    /// <summary>Secili KS (atalar.json kabarik.ks listesinden); Sekme ile degisir.</summary>
    public int KabarikKs { get; private set; } = 5;
    int MayaHave => _d.State.Backpack.Count(Data.Kabarik.MayaItem);
    public void SetKabarikKs(int ks) { if (Data.Kabarik.Ks.Contains(ks)) KabarikKs = ks; }
    void CycleKabarikKs() { var l = Data.Kabarik.Ks; KabarikKs = l[(l.IndexOf(KabarikKs) + 1) % l.Count]; Sfx.Play("ui_onay", null); }

    // ---------- Sismik Davul (guc isteyen adak) ----------

    readonly Dictionary<string, PowerPort> _drums = new();
    /// <summary>Davul arenaya kurulur (props "drum"); guc ucu research.json watts tablosundaki "sismik_davul" satirindan (200 W). Hat yoksa tamamlanmaz.</summary>
    void DrumTick(AtaDef def, float dt, bool kab)
    {
        if (def.PowerWatts <= 0 || Fight != null) return;
        if (!_drums.TryGetValue(def.Id, out var port) || port == null)
        {
            var node = GetNodeOrNull<Node3D>("Davul_" + def.Id);
            if (node == null) return;
            port = _d.Power?.Attach(node, "sismik_davul");
            _drums[def.Id] = port;
            if (port == null) return;
        }
        bool items = AtaRules.ItemsComplete(Progress, def, kab);
        bool powered = port.Gate(items);
        if (AtaRules.ChargeDrum(Progress, def, items && powered && port.Powered, dt, kab)) TrySummon(def);
    }

    /// <summary>Etkilesim: cantadaki eksik bilesenler sunaga gecer; tamamlaninca Ata gelir.</summary>
    public void Offer(AtaDef def)
    {
        bool kab = Progress.IsDefeated(def.Id);
        int n = AtaRules.OfferFromBag(Progress, def, _d.State.Backpack, kab);
        if (n > 0) { Sfx.Play("ui_onay", AltarPos(def)); Fx.Burst(AltarPos(def) + Vector3.Up, Palette.MayaTeal, 12, 3f, 0.1f, 0.6f, true); _stones[def.Id].Refresh(); }
        else Sfx.Play("ui_hata", AltarPos(def));
        if (!TrySummon(def) && n == 0) _d.Hud.Hint(kab && AtaRules.OfferingComplete(Progress, def, true) ? $"Maya yetmiyor: {AtaRules.MayaCost(Data, KabarikKs)} gerekir" : $"Adak eksik: {OfferingText(def, kab)}", 2f);
    }

    bool TrySummon(AtaDef def)
    {
        bool kab = Progress.IsDefeated(def.Id);
        var f = kab ? AtaRules.SummonKabarik(Progress, Data, def, KabarikKs, TeamSize, _d.State.Backpack) : AtaRules.Summon(Progress, Data, def, TeamSize);
        if (f == null) return false;
        Arrive(def);
        return true;
    }

    /// <summary>Gelistirici/sinama: Kabarik Ata'yi adak ve Maya beklemeden indirir (Ata once yenilmis olmali).</summary>
    public bool DevSummonKabarik(string id, int ks)
    {
        if (Fight != null) return false;
        var def = Data.Get(id);
        if (AtaRules.SummonKabarik(Progress, Data, def, ks, TeamSize, _d.State.Backpack, force: true) == null) return false;
        GD.Print($"ATA dev_summon_kabarik {id} ks={ks}");
        Arrive(def);
        return true;
    }

    /// <summary>F1 gelistirici cagirmasi (GDD §18.1 M2 sinamasi): adak beklenmez; dovus ve hasar gercek.</summary>
    public bool DevSummon(string id)
    {
        if (Fight != null) return false;
        var def = Data.Get(id);
        if (AtaRules.Summon(Progress, Data, def, TeamSize, force: true) == null) return false;
        GD.Print($"ATA dev_summon {id}");
        Arrive(def);
        return true;
    }

    void Arrive(AtaDef def)
    {
        GD.Print($"ATA summon {def.Id} hp={Fight.MaxHp:0}");
        Sfx.Play("gokyuzu_dalga", ArenaCenter(def), 2f);
        _d.Hud.Say($"{def.Name} geliyor!");
        _stones[def.Id].Pulse();
        SpawnActor(def, arriving: true);
    }

    void SpawnActor(AtaDef def, bool arriving)
    {
        Cleanup();
        Actor = def.Id switch
        {
            "boynuzlu_dede" => new BoynuzluDede(),
            "kurbaga_aga" => new KurbagaAga(),
            "ciyan_pasa" => new CiyanPasa(),
            "buz_tosunu" => new BuzTosunu(),
            "ana_maya" => new AnaMaya(),
            _ => new KocaPofuduk(),
        };
        Actor.Init(_d, this, def);
        Actor.Name = "Ata_" + def.Id;
        AddChild(Actor);
        Actor.GlobalPosition = ArenaCenter(def);
        Actor.Begin(arriving);
        Targets.Add(Actor);
        Bar.Show(def, Fight);
        _awayT = 0;
    }

    void Cleanup()
    {
        foreach (var t in Targets.OfType<Node>().ToList()) if (IsInstanceValid(t)) t.QueueFree();
        Targets.Clear();
        foreach (var n in GetChildren().Where(n => n.IsInGroup("ata_fx"))) n.QueueFree();
        Actor = null;
        Bar?.Hide();
        SetSwell(1f);
    }

    // ---------- Soguk ve kabarma (Buz Tosunu, Ana Maya) ----------

    float _cold, _swell = 1f;

    /// <summary>Oyuncu boyu (Ana Maya asama 3: herkes kabarir). Yer tutucu: Player.Scale; boy puani kartlari gelince onlara baglanir.</summary>
    void SetSwell(float s)
    {
        if (Mathf.Abs(_swell - s) < 0.001f) return;
        _swell = Mathf.MoveToward(_swell, s, 0.02f);
        _d.Player.Scale = Vector3.One * _swell;
    }

    /// <summary>Ayaz Yaylasi tipisi: isinma noktalarindan (props "warm") uzaktaki oyuncu soguk hasari yer.</summary>
    void ColdTick(AtaDef def, float dt)
    {
        var ph = AtaRules.PhaseOf(Fight, def);
        if (ph.ColdDps <= 0) { _cold = 0; return; }
        var p = _d.Player;
        bool near = Props(def, "warm").Any(w => new Vector2(p.GlobalPosition.X - w.pos.X, p.GlobalPosition.Z - w.pos.Z).Length() <= ph.WarmRadius);
        _cold += AtaRules.ColdDamage(Fight, def, near, dt);
        if (_cold < 2f) return;
        p.Hurt(_cold, p.GlobalPosition);
        Fx.Text3D(p.GlobalPosition + Vector3.Up * 2f, "SOĞUK", Palette.SkyNoon, 56, 0.8f, 0.8f);
        _cold = 0;
    }

    // ---------- Arena donanimi (props) ----------

    /// <summary>Arena donanimi dunya konumlariyla (merkeze gore atalar.json arena.props).</summary>
    public IEnumerable<(Vector3 pos, float r)> Props(AtaDef def, string kind) =>
        def.Arena.Props.Where(q => q.Kind == kind).Select(q => (_d.World.Ground(ArenaCenter(def) + new Vector3(q.Pos[0], 0, q.Pos[1])), q.Radius)).ToList();

    void BuildProps(AtaDef def)
    {
        foreach (var q in def.Arena.Props)
        {
            var at = _d.World.Ground(ArenaCenter(def) + new Vector3(q.Pos[0], 0, q.Pos[1]));
            var kit = new MeshKit();
            string name = "AtaProp_" + q.Kind;
            switch (q.Kind)
            {
                case "lake": kit.Cylinder(q.Radius, q.Radius, 0.06f, MeshKit.At(0, 0.03f, 0), def.Id == "buz_tosunu" ? new Color("CFE6F2") : Palette.MayaTeal.Lerp(Palette.Plant, 0.5f).Darkened(0.2f), 28); break;
                case "pad": kit.Cylinder(q.Radius, q.Radius, 0.1f, MeshKit.At(0, 0.1f, 0), Palette.Plant, 14); kit.Sphere(0.25f, MeshKit.At(q.Radius * 0.5f, 0.3f, 0), Palette.Warning, 6, 4); break;
                case "warm": kit.Cylinder(0.5f, 0.6f, 0.3f, MeshKit.At(0, 0.15f, 0), Palette.Rock, 10); kit.Sphere(0.45f, MeshKit.At(0, 0.65f, 0), Palette.Accent, 8, 5); break;
                case "wall": kit.Pillow(new Vector3(q.Radius * 2f, 7f, 2f), MeshKit.At(0, 3.4f, 0), Palette.HoldingRed.Lerp(Palette.Soil, 0.5f), 0.3f, 8, 4); break;
                case "barrel": continue; // varil hedefleri Ciyan Pasa aktoru kurar (AtaBarrel)
                case "drum": kit.Cylinder(1.3f, 1.4f, 1.6f, MeshKit.At(0, 0.8f, 0), Palette.Wood, 14); kit.Cylinder(1.35f, 1.35f, 0.1f, MeshKit.At(0, 1.6f, 0), Palette.OffWhite, 14); name = "Davul_" + def.Id; break;
                default: continue;
            }
            var mi = kit.Instance();
            mi.Name = name;
            AddChild(mi);
            mi.GlobalPosition = at;
            
        }
    }

    void Dispatch(AtaDef def, AtaCommand c)
    {
        switch (c.Cue)
        {
            case AtaCue.Telegraph: Actor.Telegraph(c.Attack); break;
            case AtaCue.Attack: Actor.Attack(c.Attack, c.Count); break;
            case AtaCue.SpawnAdds: SpawnAdds(def, c.Count); break;
            case AtaCue.PhaseChanged:
                GD.Print($"ATA phase {def.Id} -> {c.Phase} t={Fight.Time:0.0}");
                Actor.OnPhase(c.Phase);
                Events.RaiseAtaPhase(def.Id, c.Phase);
                break;
            case AtaCue.Stunned: GD.Print($"ATA stun {def.Id} t={Fight.Time:0.0}"); Actor.OnStunned(); break;
            case AtaCue.WeakPointBroken:
                GD.Print($"ATA weak_broken {def.Id} #{c.Count}");
                Actor.OnWeakPointBroken(c.Count);
                if (AtaRules.PhaseOf(Fight, def).BreakSpawnsAdd) SpawnAdds(def, 1); // Ciyan Pasa: kopan bogum ayri dusman olur
                break;
            case AtaCue.Defeated: Defeat(def); break;
        }
    }

    void SpawnAdds(AtaDef def, int n)
    {
        var phAdd = AtaRules.PhaseOf(Fight, def).AddId;
        var add = Data.AddOf(string.IsNullOrEmpty(phAdd) ? def.Add : phAdd);
        if (add == null) return;
        for (int i = 0; i < n; i++)
        {
            var from = Actor.SpawnPoint(i);
            var m = new AtaMinion(_d, this, add) { Name = "AtaYardimci" };
            AddChild(m);
            m.GlobalPosition = _d.World.Ground(from);
            m.Pop(from + Vector3.Up * 2.5f);
            Targets.Add(m);
        }
        Sfx.Play("zipzip_sicrama", Actor.GlobalPosition, 0f, 0.7f);
    }

    // ---------- Hasar yolu: dugumlerden Sim'e ----------

    /// <summary>Govdeye hasar (firlatilan parca: Darbe, zirhsiz). Uygulanan hasar doner.</summary>
    public float Damage(float amount, AtaHitKind kind) =>
        Damage(new CombatHit(amount, DamageType.Darbe, CombatTeam.Player), kind).Damage;

    /// <summary>Govdeye isabet (savas sozlesmesi): tur, kart durumlari; alan etkileri CombatSystem'de cozulur.</summary>
    public CombatResult Damage(CombatHit hit, AtaHitKind kind)
    {
        if (Fight == null) return default;
        var def = Data.Get(Fight.AtaId);
        hit = hit with { Amount = hit.Amount * AtaRules.PlayerDamageMult(Fight, def) }; // Ana Maya asama 3: kabarmis oyuncular
        var cmds = new List<AtaCommand>();
        var areas = new List<CombatArea>();
        float hp0 = Fight.Hp;
        float dmg = AtaRules.Damage(Fight, Data, _d.Data.Combat, def, hit.Amount, kind, cmds, hit.Type, hit.Procs, areas);
        foreach (var c in cmds) Dispatch(def, c);
        if (Actor != null) _d.Savas?.Areas(Actor, areas);
        return new CombatResult { Damage = dmg, Blocked = dmg <= 0 && hit.Amount > 0, Killed = hp0 > 0 && Fight?.Hp <= 0 };
    }

    /// <summary>Zayif noktaya (kovan) isabet. Kirildiysa true.</summary>
    public bool DamageWeakPoint(int index, CombatHit hit)
    {
        if (Fight == null) return false;
        var def = Data.Get(Fight.AtaId);
        var cmds = new List<AtaCommand>();
        bool broke = AtaRules.HitWeakPoint(Fight, Data, _d.Data.Combat, def, index, hit.Amount, cmds, hit.Type, hit.Procs);
        foreach (var c in cmds) Dispatch(def, c);
        return broke;
    }

    /// <summary>Kurbaga Aga oyuncuyu yakaladi: icerde kalma suresi baslar (Sim).</summary>
    public bool BeginSwallow() => Fight != null && AtaRules.BeginSwallow(Fight, Data.Get(Fight.AtaId));

    /// <summary>Bicme vurusu (Capa.Ata.cs): zirh kirkildiysa true.</summary>
    public bool Shear(Vector3 at)
    {
        if (Fight == null) return false;
        var def = Data.Get(Fight.AtaId);
        float before = Fight.Armor;
        float after = AtaRules.Shear(Fight, def);
        if (after >= before) return false;
        Shears++;
        Actor.OnSheared(before, after, at);
        return true;
    }

    /// <summary>Hucum sutuna toslandi: asamada sersemleme varsa sersemler.</summary>
    public bool PillarHit()
    {
        if (Fight == null) return false;
        var def = Data.Get(Fight.AtaId);
        var cmds = new List<AtaCommand>();
        bool s = AtaRules.Stun(Fight, def, cmds);
        if (s) PillarStuns++;
        foreach (var c in cmds) Dispatch(def, c);
        return s;
    }

    void Defeat(AtaDef def)
    {
        GD.Print($"ATA defeated {def.Id} t={Fight?.Time:0.0} by={string.Join(",", Fight?.DamageBySource.Select(kv => $"{kv.Key}:{kv.Value:0}") ?? new string[0])}");
        var at = Actor.Center;
        Actor.OnDefeated();
        Targets.Remove(Actor);
        foreach (var m in Targets.OfType<AtaMinion>().ToList()) m.Poof();
        Targets.Clear();
        Actor = null;
        bool kab = Fight.Kabarik;
        int ks = Fight.Ks;
        var items = kab ? new List<AtaRewardItemDef> { new() { Item = Data.Kabarik.RewardItem, Count = Mathf.Max(1, ks / 5) * Data.Kabarik.RewardPer5Ks } } : def.Reward.Items;
        int left = kab ? AtaRules.RewardKabarik(Progress, Data, def, ks, _d.State.Backpack) : AtaRules.Reward(Progress, def, _d.State.Backpack);
        DefeatedLog.Add(kab ? AtaRules.KabarikId(def.Id) : def.Id);
        Bar.Victory(def);
        Sfx.Play("kabarmis_patlama", at, 4f, 0.6f);
        Sfx.Play("seviye_atlama", null, 0f);
        int k = 0;
        foreach (var it in items)
        {
            Fx.Text3D(at + Vector3.Up * (1.2f + 0.7f * k++), $"+{it.Count} {ItemNames.Of(it.Item)}", Palette.Warning, 64, 2.2f, 3f);
            for (int i = 0; i < it.Count; i++) Fx.FlyTo(at, () => _d.Player.BackpackPoint.GlobalPosition, Palette.MayaTeal, 0.18f);
        }
        var seal = Data.SealOf(def.Reward.Seal);
        if (kab) _d.Hud.Say($"Kabarık {def.Name} yenildi (KS {ks})! +{items[0].Count} {ItemNames.Of(items[0].Item)}");
        else _d.Hud.Say($"{def.Name} yenildi!" + (seal != null ? $" Mühür: {seal.Name}." : "") + (!string.IsNullOrEmpty(def.Reward.Ulti) ? " Ulti açıldı." : ""));
        if (left > 0) GD.Print($"ATA reward_overflow {left}");
        Events.RaiseAtaDefeated(kab ? AtaRules.KabarikId(def.Id) : def.Id); // Kabarik: kabarik_<ata> (cip kaynagi ata:kabarik_<ata>, is emri #33)
        _stones[def.Id].Refresh();
    }

    /// <summary>Yardimci ya da parca oldu: hedef listesinden cikar.</summary>
    public void Remove(IAtaTarget t) => Targets.Remove(t);
}
