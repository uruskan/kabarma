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
        foreach (var def in Data.Atalar)
        {
            var stone = new AtaStone(_d, def, new Vector3(Spot(def).X, 0, Spot(def).Z)) { Name = "AtaTasi_" + def.Id };
            AddChild(stone);
            _stones[def.Id] = stone;
            BuildPillars(def);
            _d.FeelPanel?.AddButton($"Ata çağır: {def.Name}", () => DevSummon(def.Id)); // GDD §18.1 M2 sinamasi
        }
        Bar = new AtaBar { Name = "AtaBar" };
        AddChild(Bar);
        // Kayittan suren dovus geldiyse Ata arenada yeniden kurulur.
        if (Fight != null) CallDeferred(nameof(Respawn));
        GD.Print($"ATALAR arenas={string.Join(",", Data.Atalar.Select(a => $"{a.Id}@{ArenaCenter(a).X:0},{ArenaCenter(a).Z:0}/r{ArenaRadius(a):0}{(Spot(a).FromSite ? ":" + Spot(a).SiteId : ":yedek")}"))}");
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
            foreach (var def in Data.Atalar) AltarTick(def, p);
            return;
        }
        var def0 = Data.Get(Fight.AtaId);
        if (Actor == null || !IsInstanceValid(Actor)) { SpawnActor(def0, arriving: false); return; }
        Fight.AddsAlive = Targets.OfType<AtaMinion>().Count(m => m.Alive);
        foreach (var c in AtaRules.Tick(Fight, def0, dt)) Dispatch(def0, c);
        if (Actor.Burning > 0) AtaRules.Burn(Fight, def0, dt);
        // Oyuncu arenadan uzaklasti: Ata merkezine doner; uzun sure yoksa dovus biter (adak geri gelmez, M2).
        float away = new Vector2(p.GlobalPosition.X - ArenaCenter(def0).X, p.GlobalPosition.Z - ArenaCenter(def0).Z).Length();
        _awayT = away > def0.Arena.Leash ? _awayT + dt : 0f;
        if (_awayT > Data.AbandonSeconds) { GD.Print($"ATA abandon {def0.Id}"); _d.Hud.Say($"{def0.Name} ormana çekildi."); Cleanup(); AtaRules.Abandon(Progress); }
    }

    void AltarTick(AtaDef def, Player p)
    {
        var altar = AltarPos(def);
        // Kabarik urun sunaga birakildi (Cekim): adakta eksikse kabul.
        foreach (var sc in GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>())
        {
            if (sc.Held || sc.Gone || sc.GlobalPosition.DistanceTo(altar) > def.Arena.AltarReach) continue;
            if (!AtaRules.OfferSwollen(Progress, def, sc.CropId)) continue;
            sc.Sold = true;
            Fx.Burst(sc.GlobalPosition, Palette.MayaTeal, 18, 4f, 0.14f, 0.7f, true);
            Sfx.Play("ui_onay", altar);
            sc.QueueFree();
            _stones[def.Id].Refresh();
            TrySummon(def);
        }
        if (p.GlobalPosition.DistanceTo(altar) > def.Arena.AltarReach + 1f) return;
        if (Progress.IsDefeated(def.Id)) { _d.Hud.Hint($"Ata Taşı · {def.Name} yenildi · Kabarık Ata M4'te"); return; }
        _d.Hud.Hint($"Ata Taşı · {def.OfferingName}: {OfferingText(def)}  ·  {{act:interact}} adak sun");
        if (Input.IsActionJustPressed("interact")) Offer(def);
    }

    public string OfferingText(AtaDef def) => string.Join("  ", def.Offering.Select(o =>
        $"{(o.Swollen ? "Kabarık " : "")}{ItemNames.Of(o.Item)} {o.Count - AtaRules.Missing(Progress, def, o)}/{o.Count}"));

    /// <summary>Etkilesim: cantadaki eksik bilesenler sunaga gecer; tamamlaninca Ata gelir.</summary>
    public void Offer(AtaDef def)
    {
        int n = AtaRules.OfferFromBag(Progress, def, _d.State.Backpack);
        if (n > 0) { Sfx.Play("ui_onay", AltarPos(def)); Fx.Burst(AltarPos(def) + Vector3.Up, Palette.MayaTeal, 12, 3f, 0.1f, 0.6f, true); _stones[def.Id].Refresh(); }
        else Sfx.Play("ui_hata", AltarPos(def));
        if (!TrySummon(def) && n == 0) _d.Hud.Hint($"Adak eksik: {OfferingText(def)}", 2f);
    }

    bool TrySummon(AtaDef def)
    {
        var f = AtaRules.Summon(Progress, Data, def, TeamSize);
        if (f == null) return false;
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
            case AtaCue.WeakPointBroken: GD.Print($"ATA weak_broken {def.Id} #{c.Count}"); Actor.OnWeakPointBroken(c.Count); break;
            case AtaCue.Defeated: Defeat(def); break;
        }
    }

    void SpawnAdds(AtaDef def, int n)
    {
        var add = Data.AddOf(def.Add);
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

    /// <summary>Govdeye hasar (silah, firlatilan parca). Uygulanan hasar doner.</summary>
    public float Damage(float amount, AtaHitKind kind)
    {
        if (Fight == null) return 0;
        var def = Data.Get(Fight.AtaId);
        var cmds = new List<AtaCommand>();
        float dmg = AtaRules.Damage(Fight, Data, def, amount, kind, cmds);
        foreach (var c in cmds) Dispatch(def, c);
        return dmg;
    }

    /// <summary>Zayif noktaya (kovan) hasar. Kirildiysa true.</summary>
    public bool DamageWeakPoint(int index, float amount)
    {
        if (Fight == null) return false;
        var def = Data.Get(Fight.AtaId);
        var cmds = new List<AtaCommand>();
        bool broke = AtaRules.HitWeakPoint(Fight, Data, def, index, amount, cmds);
        foreach (var c in cmds) Dispatch(def, c);
        return broke;
    }

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
        int left = AtaRules.Reward(Progress, def, _d.State.Backpack);
        DefeatedLog.Add(def.Id);
        Bar.Victory(def);
        Sfx.Play("kabarmis_patlama", at, 4f, 0.6f);
        Sfx.Play("seviye_atlama", null, 0f);
        int k = 0;
        foreach (var it in def.Reward.Items)
        {
            Fx.Text3D(at + Vector3.Up * (1.2f + 0.7f * k++), $"+{it.Count} {ItemNames.Of(it.Item)}", Palette.Warning, 64, 2.2f, 3f);
            for (int i = 0; i < it.Count; i++) Fx.FlyTo(at, () => _d.Player.BackpackPoint.GlobalPosition, Palette.MayaTeal, 0.18f);
        }
        var seal = Data.SealOf(def.Reward.Seal);
        _d.Hud.Say($"{def.Name} yenildi! Mühür: {seal?.Name}. Ulti açıldı.");
        if (left > 0) GD.Print($"ATA reward_overflow {left}");
        Events.RaiseAtaDefeated(def.Id);
        _stones[def.Id].Refresh();
    }

    /// <summary>Yardimci ya da parca oldu: hedef listesinden cikar.</summary>
    public void Remove(IAtaTarget t) => Targets.Remove(t);
}
