using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Hayvancilik dugumu (GDD 9.1-9.4). Ranch'i (Sim) zamanla isletir ve olaylarini gosterir: urun, gubre, yavru, kacma.
/// Hayvanlara bileseni takar: var olan Pofuduk aktoru, ilk gunun Gidak'i (kendi agil yolu Gidak.Pen.cs), yaratik2'nin
/// WildCreature aktorleri (WildCreature.Pen.cs) ve aktoru olmayan tur icin yer tutucu (TameablePlaceholder: Vizviz surusu).
/// E ile yeme, okşamaya, urune, gubreye ve hayvan paneline bakar (Director.Pen.cs PenInteract'ten).
/// </summary>
public partial class RanchSystem : Node
{
    readonly Director _d;
    readonly List<HusbandryAgent> _agents = new();
    readonly Dictionary<int, Node3D> _piles = new();
    float _acc, _hintT;
    bool _started;
    /// <summary>Panelde secili hayvan (Hud menusu acikken).</summary>
    public HusbandryAgent PanelAgent { get; private set; }
    public const string PanelTitle = "Hayvan";

    public Ranch Ranch => _d.State.Ranch;
    public IReadOnlyList<HusbandryAgent> Agents => _agents;
    public AnimalCard Card { get; private set; }

    public RanchSystem(Director d) { _d = d; Name = "RanchSystem"; ProcessPriority = 95; }

    public override void _Ready()
    {
        Events.Dawn += () => Ranch.Dawn();
        Events.WildActed += OnWildActed;
        Card = new AnimalCard(_d, this);
        _d.Hud.AddChild(Card);
    }

    /// <summary>Ilk karede: hayvanlara bilesen tak, yer tutucu yabanileri kur (dunya hazir olduktan sonra).</summary>
    void Start()
    {
        _started = true;
        // Ilk gunun Gidak'i: kendi agil yolu (Gidak.Pen.cs); bilesen yalniz kart ve panel icin (ilk geceden sonra E'ye girer).
        if (_d.Gidak != null && IsInstanceValid(_d.Gidak)) Attach(_d.Gidak, _d.Gidak.Animal).Passive = true;
        // Kayittan gelen hayvanlar (Ranch.Animals) once kullanilir; yoksa yabani yaratilir. Kalanlara aktor kurulur.
        var free = Ranch.Animals.Where(a => a.Id > 0 && a != TutorialGidak && Ranch.Rules.Known(a.Species)).ToList();
        PenAnimal Take(string species, Vector3 at)
        {
            var a = free.FirstOrDefault(x => x.Species == species && x.AgeDays >= 1);
            if (a == null) return Ranch.Wild(species, at.X, at.Z);
            free.Remove(a);
            return a;
        }
        if (_d.Pofuduk != null && IsInstanceValid(_d.Pofuduk))
        {
            var a = Take("pofuduk", _d.Pofuduk.GlobalPosition);
            if (a.Tame) _d.Pofuduk.GlobalPosition = _d.World.Ground(new Vector3(a.X, 0, a.Z));
            Attach(_d.Pofuduk, a);
        }
        // Evcillesebilir turler cayirda (Pod'a gore): yaratik2'nin WildCreature aktoru (Wildlife.Spawn); suru turu Vizviz
        // ve aktoru olmayanlar yer tutucu (TameablePlaceholder). Istemcide host'un yaratiklari kukla olarak gelir.
        if (_d.Net?.IsClient == true) { GD.Print("RANCH start client"); return; }
        var spots = new (string id, Vector3 off)[]
        {
            ("homur", new(-24, 0, 18)), ("homur", new(-28, 0, 22)),
            ("dalli", new(26, 0, 24)), ("dalli", new(30, 0, 19)),
            ("vicik", new(-20, 0, -26)), ("vicik", new(-17, 0, -30)),
            ("bozuk", new(36, 0, -30)),
            ("vizviz", new(18, 0, 30)),
            ("hu", new(-34, 0, -8)),
        };
        foreach (var (id, off) in spots)
        {
            var s = Ranch.Rules.Species(id);
            var at = _d.World.Ground(Director.PodPos + off);
            var a = Take(id, at);
            var actor = MakeActor(s, at);
            if (a.Tame) actor.GlobalPosition = _d.World.Ground(new Vector3(a.X, 0, a.Z));
            Attach(actor, a);
        }
        foreach (var a in free) SpawnActor(a); // kayittaki yavrular ve kuluckadan cikanlar
        GD.Print($"RANCH start animals={Ranch.Animals.Count} agents={_agents.Count}");
    }

    public HusbandryAgent Attach(Node3D host, PenAnimal a)
    {
        var ag = new HusbandryAgent(_d, this, host, a);
        host.AddChild(ag);
        _agents.Add(ag);
        return ag;
    }

    public HusbandryAgent AgentOf(int id) => _agents.FirstOrDefault(x => IsInstanceValid(x) && x.Animal.Id == id);

    /// <summary>Ilk gunun Gidak'i kendi yolunu yurur; Sim'de yine Ranch'in hayvani (urun, mutluluk, gen).</summary>
    PenAnimal TutorialGidak => _d.Gidak != null && IsInstanceValid(_d.Gidak) ? _d.Gidak.Animal : null;

    public override void _Process(double delta)
    {
        if (!_started) { if (_d.World != null && _d.Pens != null) Start(); else return; }
        _agents.RemoveAll(a => !IsInstanceValid(a));
        Rebind();
        foreach (var ag in _agents)
            if (ag.Host is WildCreature wc && IsInstanceValid(wc))
            {
                wc.State.Tamed = ag.Animal.Tame; // yaratik2 sozlesmesi: State.Tamed hayvan akisinda
                wc.HusbandryPens = _d.Pens;
                if (ag.Animal.Tame) wc.State.Hp = wc.Def.Hp; // evcil hayvan kalici olmez (GDD 9.5): kacak atisla dusmesin
                if (_d.Wildlife != null && !wc.Dead && !_d.Wildlife.Creatures.Contains(wc)) _d.Wildlife.Creatures.Add(wc); // kayit yuklemesi listeyi bosaltir
            }
        _acc += (float)delta;
        if (_acc >= 0.25f)
        {
            var ev = Ranch.Tick(_acc, _d.Pens.Pens, RoofOver);
            _acc = 0;
            foreach (var e in ev) Show(e);
        }
        PilesSync();
        _hintT -= (float)delta;
        if (_hintT <= 0 && !_d.Hud.MenuOpen && _d.Capa.CurrentPlaceable == "")
        {
            _hintT = 0.2f;
            var label = Nearest(_d.Player.GlobalPosition, out _, out _);
            if (label != null) _d.Hud.Hint("{act:interact}: " + label, 0.3f);
        }
        if (PanelAgent != null && !_d.Hud.MenuOpen) PanelAgent = null;
    }

    /// <summary>
    /// Kayit yukleme ve ortak oyun esitlemesi ("pen.animals" yeni PenAnimal nesneleri kurar): bilesenleri ayni kimlikli
    /// hayvana bagla. Ilk gunun Gidak'i kendi Animal ozelliginde yeniden bulunur (Gidak.Pen.cs).
    /// </summary>
    void Rebind()
    {
        var set = new HashSet<PenAnimal>(Ranch.Animals);
        foreach (var ag in _agents)
        {
            if (set.Contains(ag.Animal)) continue;
            if (ag.Passive && _d.Gidak != null && ag.Host == _d.Gidak) { ag.Rebind(_d.Gidak.Animal); continue; }
            ag.Rebind(Ranch.ById(ag.Animal.Id));
        }
    }

    /// <summary>Agilin ustunde cati var mi (GDD 9.2): yapi setinin cati parcalari agil cokgeninin icinde.</summary>
    public bool RoofOver(PenShape pen)
    {
        var sys = _d.Build?.System;
        if (sys == null) return false;
        return sys.Pieces.Any(p => IsInstanceValid(p) && p.Def.Id.Contains("cati") && pen.Contains(new P2(p.GlobalPosition.X, p.GlobalPosition.Z)));
    }

    Vector3 PosOf(int animalId)
    {
        var ag = AgentOf(animalId);
        if (ag != null) return ag.Host.GlobalPosition;
        if (TutorialGidak?.Id == animalId) return _d.Gidak.GlobalPosition;
        var a = Ranch.ById(animalId);
        return a == null ? Director.PodPos : _d.World.Ground(new Vector3(a.X, 0, a.Z));
    }

    void Show(RanchEvent e)
    {
        var a = Ranch.ById(e.AnimalId);
        var at = PosOf(e.AnimalId) + Vector3.Up * 1.2f;
        switch (e.Kind)
        {
            case RanchEventKind.Nested:
            {
                var prop = _d.Pens.Props.FirstOrDefault(p => IsInstanceValid(p) && p.Nest != null && p.Nest.Id == e.OtherId);
                prop?.Bounce();
                Fx.Text3D((prop?.GlobalPosition ?? at) + Vector3.Up * 1.1f, $"+1 {ItemNames.Of(e.Item)}", Palette.OffWhite, 56, 1f, 1.4f);
                Sfx.Play("pickup", at, -4f);
                GD.Print($"EGG laid nest {e.Item}");
                Events.RaiseLaid(e.Item, "nest");
                break;
            }
            case RanchEventKind.Grounded:
                _d.Pens.DropOnGround(e.Item, PosOf(e.AnimalId));
                Events.RaiseLaid(e.Item, "ground");
                break;
            case RanchEventKind.Product:
            case RanchEventKind.Worked:
                Fx.Text3D(at, $"+{e.Count} {ItemNames.Of(e.Item)}", Palette.OffWhite, 52, 1f, 1.4f);
                GD.Print($"RANCH {e.Kind.ToString().ToLowerInvariant()} {a?.Species} {e.Item} x{e.Count}");
                break;
            case RanchEventKind.Scouted:
                Fx.Burst(at, Palette.CoopBlue, 8, 3f, 0.08f, 0.6f);
                GD.Print($"RANCH scouted {a?.Species} r={e.Count}"); // harita acma: dunya2'nin Hud.Map'i gelince baglanir
                break;
            case RanchEventKind.Born:
            case RanchEventKind.Hatched:
                if (a != null) SpawnActor(a);
                Fx.Text3D(at, $"Yeni {Ranch.Rules.Species(e.Item).Name}!", Palette.Accent, 64, 1.2f, 1.6f);
                Sfx.Play("pickup", at, 0f);
                GD.Print($"RANCH born {e.Item} id={e.AnimalId} genes={a?.Genes}");
                Events.RaiseAnimalBorn(e.Item);
                break;
            case RanchEventKind.WantsOut:
                if (a != null) _d.Hud.Hint($"{Ranch.Rules.Species(a.Species).Name} mutsuz: kapı açıksa kaçar", 3f);
                break;
            case RanchEventKind.Escaped:
                Fx.Text3D(at, "Kaçtı!", Palette.Warning, 64, 1.2f, 1.6f);
                GD.Print($"RANCH escaped {a?.Species} id={e.AnimalId}");
                if (a != null) Events.RaiseAnimalEscaped(a.Species);
                break;
        }
    }

    /// <summary>
    /// Turun aktoru: yaratik2'nin WildCreature'i (Wildlife.Spawn; ortak oyunda Wildlife.Creatures uzerinden esitlenir),
    /// yoksa YER TUTUCU (TameablePlaceholder, net yaratik grubunda).
    /// </summary>
    Node3D MakeActor(SpeciesDef s, Vector3 at)
    {
        var def = _d.Data.Creatures.Creatures.FirstOrDefault(c => c.Id == s.Id);
        if (_d.Wildlife != null && def?.Wild != null && _d.Wildlife.Spawn(def, at).FirstOrDefault() is WildCreature wc)
        {
            if (wc.Loaded) return wc;
            _d.Wildlife.Creatures.Remove(wc); wc.QueueFree(); // modeli yok: yer tutucuya dus
        }
        var ph = new TameablePlaceholder(_d, s, at);
        _d.AddChild(ph);
        return ph;
    }

    /// <summary>Yavru ya da kuluckadan cikan icin aktor: Pofuduk ve Gidak kendi aktorleri, digerleri MakeActor.</summary>
    void SpawnActor(PenAnimal a)
    {
        var at = _d.World.Ground(new Vector3(a.X, 0, a.Z));
        Node3D actor;
        if (a.Species is "pofuduk" or "gidak")
        {
            actor = a.Species == "pofuduk" ? new Pofuduk(_d, at) { Name = "Pofuduk_" + a.Id } : new Gidak(_d.Data.Creature("gidak"), _d) { Name = "Gidak_" + a.Id };
            _d.AddChild(actor);
            actor.AddToGroup(NetSync.CreatureGroup); // ortak oyun: host yetkili konum (NetSync.Creatures.cs)
        }
        else actor = MakeActor(Ranch.Rules.Species(a.Species), at);
        actor.GlobalPosition = at;
        Attach(actor, a);
    }

    /// <summary>
    /// Yabani olay (yaratik2, Events.WildActed): Sinsi kumesin yaninda "Took" = yumurta calar (ICERIK 3.8). En yakin
    /// Folluk'tan bir yumurta, yoksa yakindaki hayvanin bekleyen yumurtasi gider. Evcil WildCreature'lar Wildlife
    /// listesinde kalir (kayit yuklemesi listeyi temizler; ortak oyun esitlemesi o listeden okur).
    /// </summary>
    void OnWildActed(string species, string what, float x, float z)
    {
        if (what != "Took" || species != "sinsi" || !TutorialDone) return; // ilk gunun yumurtasi (bot, senaryo) calinmaz
        const float r = 8f;
        var nest = Ranch.Nests.Where(n => n.Eggs > 0 && (n.X - x) * (n.X - x) + (n.Z - z) * (n.Z - z) < r * r)
            .OrderBy(n => (n.X - x) * (n.X - x) + (n.Z - z) * (n.Z - z)).FirstOrDefault();
        string lost = null;
        if (nest != null) { nest.Eggs--; lost = "egg"; }
        else
        {
            foreach (var a in Ranch.Animals.Where(a => (a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z) < r * r))
            {
                var st = a.Pending.FirstOrDefault(p => p.Count > 0 && (p.Item.Contains("egg") || p.Item.Contains("yumurta")));
                if (st == null) continue;
                st.Count--; if (st.Count <= 0) a.Pending.Remove(st);
                lost = st.Item; break;
            }
        }
        if (lost == null) return;
        var at = _d.World.Ground(new Vector3(x, 0, z)) + Vector3.Up * 1.2f;
        Fx.Text3D(at, $"-1 {ItemNames.Of(lost)}", Palette.Warning, 56, 1f, 1.4f);
        GD.Print($"RANCH stolen {lost} by sinsi at={x:0.0},{z:0.0}");
    }

    // ---------- gubre yiginlari ----------

    void PilesSync()
    {
        foreach (var m in Ranch.Manure)
        {
            if (_piles.ContainsKey(m.Id)) continue;
            var n = new MeshKit().Sphere(0.18f, MeshKit.At(new Vector3(0, 0.06f, 0), Vector3.Zero, new Vector3(1.3f, 0.5f, 1.1f)), new Color("#6b4a2b"), 8, 5).Instance();
            n.Name = "Gubre_" + m.Id;
            _d.AddChild(n);
            n.GlobalPosition = _d.World.Ground(new Vector3(m.X, 0, m.Z));
            _piles[m.Id] = n;
        }
        foreach (var id in _piles.Keys.ToList())
        {
            var m = Ranch.Manure.FirstOrDefault(x => x.Id == id);
            if (m != null) { _piles[id].Scale = Vector3.One * Mathf.Min(2f, 0.8f + m.Count * 0.2f); continue; }
            _piles[id].QueueFree();
            _piles.Remove(id);
        }
    }

    // ---------- E ----------

    /// <summary>En yakin hayvan isi (yem, tutma, urun, okşama, panel) ya da gubre; ipucu etiketi ve uzaklik.</summary>
    string Nearest(Vector3 p, out System.Action act, out float dist)
    {
        act = null; dist = float.MaxValue;
        string label = null;
        float range = Ranch.Rules.Def.PetRange + 0.6f;
        var bag = _d.State.Backpack;
        float D(Vector3 q) => new Vector2(q.X - p.X, q.Z - p.Z).Length();
        foreach (var ag in _agents)
        {
            if (!IsInstanceValid(ag) || !IsInstanceValid(ag.Host)) continue;
            if (ag.Passive && (!ag.Animal.Registered || !TutorialDone)) continue; // ilk gunun E sirasi (kapi, Folluk) bozulmasin
            float d = D(ag.Host.GlobalPosition) - ag.Species.Size * 0.4f;
            if (d > range || d >= dist) continue;
            var a = ag.Animal; var s = ag.Species; var r = Ranch.Rules;
            var gg = ag;
            if (!a.Tame)
            {
                if (s.Tame == "subdue" && !a.Bound)
                {
                    bool can = ag.HpFraction < Ranch.Rules.Def.Subdue.HpFraction && Ranch.Rules.Def.Subdue.Tools.Contains(HeadId());
                    label = can ? $"{s.Name}'u tut" : $"{s.Name}: önce canını %25'in altına indir, Çekim ile tut";
                    act = can ? () => Bind(gg) : () => _d.Hud.Hint(label, 1.5f);
                }
                else if (s.Tame == "egg") { label = $"{s.Name} yumurtadan çıkar (Kuluçka)"; act = () => _d.Hud.Hint(label, 1.5f); }
                else if (bag.Count(s.Food) > 0) { label = $"{s.Name}'a {ItemNames.Of(s.Food)} ver ({a.Trust * 100:0}%)"; act = () => Feed(gg); }
                else { label = $"{s.Name} {ItemNames.Of(s.Food)} sever"; act = () => _d.Hud.Hint(label, 1.5f); }
            }
            else if (a.Pending.Count > 0) { label = $"{s.Name}: {string.Join(", ", a.Pending.Select(x => $"{x.Count} {ItemNames.Of(x.Item)}"))} al"; act = () => Collect(gg); }
            else if (a.LastPetDay != Ranch.Day) { label = $"{s.Name}'u okşa"; act = () => Pet(gg); }
            else { label = $"{s.Name}: panel"; act = () => OpenPanel(gg); }
            dist = d;
        }
        foreach (var m in Ranch.Manure)
        {
            float d = D(new Vector3(m.X, 0, m.Z));
            if (d > 1.6f || d >= dist) continue;
            var mm = m;
            dist = d; label = $"{m.Count} Gübre al"; act = () => CollectManure(mm);
        }
        return label;
    }

    /// <summary>Ilk gunun Gidak adimlari (agil, gidak, gece) gecti.</summary>
    bool TutorialDone
    {
        get
        {
            var sc = _d.State.Scenario;
            int night = _d.Data.Scenario.Steps.FindIndex(x => x.Id == "night");
            return sc.Finished || night < 0 || sc.StepIndex > night;
        }
    }

    string HeadId() =>_d.Capa.Current.ToString().ToLowerInvariant();

    /// <summary>E: hayvan isi menzildeyse ve agil nesnesinden yakinsa isler.</summary>
    public bool Interact(Vector3 p)
    {
        if (Nearest(p, out var act, out var dist) == null) return false;
        if (_d.Pens != null && _d.Pens.NearestDistance(p) < dist) return false;
        act();
        return true;
    }

    void Feed(HusbandryAgent ag)
    {
        var s = ag.Species;
        if (!_d.State.Backpack.Remove(s.Food, 1)) return;
        bool tamed = Ranch.Rules.Feed(ag.Animal, s.Food);
        var at = ag.Host.GlobalPosition + Vector3.Up * (s.Size + 0.4f);
        Fx.Burst(at, Palette.Accent, 6, 1.5f, 0.1f, 0.8f, true, -2f);
        Fx.Text3D(at, tamed ? "♥ Seni izliyor" : "♥", Palette.Accent, 72, 1.2f, 1.2f);
        Sfx.Play("pickup", ag.Host.GlobalPosition, -6f);
        GD.Print($"RANCH fed {s.Id} trust={ag.Animal.Trust:0.00} tame={ag.Animal.Tame}");
        if (tamed) Events.RaiseAnimalTamed(s.Id);
    }

    void Bind(HusbandryAgent ag)
    {
        if (!Ranch.Rules.TryBind(ag.Animal, ag.HpFraction, HeadId())) return;
        Fx.Text3D(ag.Host.GlobalPosition + Vector3.Up * 2f, "Tutuldu", Palette.CoopBlue, 64, 1.2f, 1.4f);
        GD.Print($"RANCH bound {ag.Species.Id}");
    }

    void Collect(HusbandryAgent ag)
    {
        var items = ag.Animal.Pending.Select(x => (x.Item, x.Count)).ToList();
        int n = HusbandryRules.Collect(ag.Animal, _d.State.Backpack);
        if (n <= 0) { _d.Hud.Hint("Çanta dolu", 1f); return; }
        Fx.FlyTo(ag.Host.GlobalPosition + Vector3.Up * 0.6f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.OffWhite, 0.12f);
        Sfx.Play("pickup", ag.Host.GlobalPosition, -2f);
        foreach (var (item, count) in items) { Events.RaiseGathered(item, count); Events.RaiseAnimalCollected(item, count); }
        GD.Print($"RANCH collected {ag.Species.Id} {string.Join(",", items.Select(i => i.Item + "x" + i.Count))}");
    }

    void Pet(HusbandryAgent ag)
    {
        if (!Ranch.Rules.Pet(ag.Animal, Ranch.Day)) return;
        var at = ag.Host.GlobalPosition + Vector3.Up * (ag.Species.Size + 0.4f);
        Fx.Text3D(at, "♥", Palette.Accent, 96, 1.2f, 1.2f);
        Fx.Burst(at, Palette.Accent, 8, 2f, 0.1f, 0.8f, true, -2f);
        Sfx.Play("pickup", ag.Host.GlobalPosition, -8f);
    }

    void CollectManure(ManurePile m)
    {
        int n = Ranch.CollectManure(m, _d.State.Backpack);
        if (n <= 0) return;
        Sfx.Play("pickup", _d.Player.GlobalPosition, -4f);
        Events.RaiseGathered(Ranch.Rules.Def.Manure.Item, n);
    }

    // ---------- panel (GDD 9.4 roller ve komutlar) ----------

    public void OpenPanel(HusbandryAgent ag)
    {
        PanelAgent = ag;
        var a = ag.Animal; var s = ag.Species; var r = Ranch.Rules;
        var items = new List<Hud.MenuItem>
        {
            new($"Ürünleri al ({a.Pending.Sum(x => x.Count)})", () => a.Pending.Count > 0, () => Collect(ag)),
            new("Okşa", () => a.LastPetDay != Ranch.Day, () => Pet(ag)),
        };
        foreach (var role in s.Roles)
        {
            var name = r.Def.Roles.FirstOrDefault(x => x.Id == role)?.Name ?? role;
            items.Add(new($"Rol: {name}{(a.Role == role ? "  ✓" : "")}", () => a.Role != role, () => { if (r.SetRole(a, role)) OpenPanel(ag); }));
        }
        foreach (var c in ag.Passive ? new List<NamedId>() : r.Def.Commands) // ilk gunun Gidak'i kendi yolunu yurur
            items.Add(new($"Komut: {c.Name}{(a.Command == c.Id ? "  ✓" : "")}", () => true, () =>
            {
                bool ok = r.Command(a, c.Id, Ranch.Rng);
                _d.Hud.Hint(ok ? $"{s.Name}: {c.Name}" : $"{s.Name} dinlemedi (U {a.Genes.U})", 1.5f);
                OpenPanel(ag);
            }));
        if (a.Role == "yuk")
        {
            items.Add(new("Heybeye yükle (en çok taşıdığın kaynak)", () => _d.State.Backpack.Stacks.Any(x => !_d.State.Belt.IsTool(x.id)), () =>
            {
                var top = _d.State.Backpack.Contents.Where(x => !_d.State.Belt.IsTool(x.id)).OrderByDescending(x => x.count).FirstOrDefault();
                if (top.id != null) r.PackLoad(a, _d.State.Backpack, top.id, top.count);
                OpenPanel(ag);
            }));
            items.Add(new($"Heybeyi boşalt ({a.Pack.Values.Sum()})", () => a.Pack.Count > 0, () => { r.PackUnload(a, _d.State.Backpack); OpenPanel(ag); }));
        }
        _d.Hud.ShowMenu($"{PanelTitle}: {ag.Title}", items, AnimalCard.Summary(this, _d, ag));
    }
}
