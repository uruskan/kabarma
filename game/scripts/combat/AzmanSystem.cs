using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Azmanlar (ICERIK §4.2): Azman Homur (Altin Cayir) ve Azman Bozuk (Sis Korusu). Kural ve durum Sim'de
/// (AzmanRules, GameState.Azman: kayit bolumu "azman"); bu dugum aktorleri kurar, komutlari oynatir, isabeti iletir.
///
/// Gercek gecede plan (Waves.Compose) her 5. KS'nin Doruk dalgasina bir Azman yeri koyar; kabarma3'un
/// SpawnKabaran'i o yeri <see cref="SpawnSlot"/>'a verir (tapu sinirindaki giris noktasinda doger).
/// <see cref="OnPeak"/> plansiz cagirma icin (sinama). Safakta <see cref="DismissAll"/> cagrilir.
///
/// Ortak oyun: Azman yalniz host'ta dogar ve dusunur. "azman" dunya bolumu (NetSync.Save.cs) istemciye gider;
/// istemci ayni durumdan kukla kurar (konuma kayar, yapay zeka yok). Istemcinin isabeti "azman.hit" ile host'a gider.
/// Oyuncuya hasar her makinede yerel oyuncu icin olculur (kuklanin konumu ve saldiri durumuyla).
/// </summary>
public partial class AzmanSystem : Node
{
    readonly Director _d;
    readonly Dictionary<ulong, AzmanActor> _actors = new();
    /// <summary>Sinama ve rapor.</summary>
    public readonly List<string> DefeatedLog = new();
    public int WallHits, Uproots, LogHits, LogLandings, RidersDropped;
    /// <summary>Bu oturumda sokulen agaclar (ayni agac iki kez sokulmez; agac yerinde kalir, yer tutucu).</summary>
    public readonly HashSet<Vector3> UsedTrees = new();

    public AzmanSystem(Director d) { _d = d; }

    public AzmanFile Data => _d.Data.Azman;
    public AzmanProgress Progress => _d.State.Azman;
    public IEnumerable<AzmanActor> Actors => _actors.Values.Where(a => IsInstanceValid(a));
    bool Puppet => _d.NetClient;
    int TeamSize => _d.Net?.PlayerCount ?? 1;

    public override void _Ready()
    {
        Events.Dawn += DismissAll; // safakta kalan Azman cekilir
        TreeExiting += () => Events.Dawn -= DismissAll;
        Callable.From(BindNet).CallDeferred(); // ag InitSavas'tan sonra kurulur (Director.InitNet)
    }

    /// <summary>
    /// kabarma3 kancasi (Director.Kabarma.cs SpawnKabaran): plandaki Azman yeri geldi. Turu "azman_" + slot turu;
    /// tanim yoksa false (cagiran Dev elit yedegine duser). Istemcide true (host doger).
    /// </summary>
    public bool SpawnSlot(WaveSlot slot, int ks, Vector3 at)
    {
        if (Puppet) return true;
        string id = "azman_" + slot.Species;
        if (Data.Azmanlar.All(a => a.Id != id)) return false;
        return Summon(id, ks, at) != null;
    }

    void BindNet()
    {
        _d.Net?.On("azman.hit", (actor, r) =>
        {
            ulong id = r.U64();
            var hit = new CombatHit(r.F32(), (DamageType)r.U8(), CombatTeam.Player) { Procs = (StatusProc)r.U8(), Crit = r.Bool(), Weak = r.Bool() };
            var (x, y, z) = actor.Pos;
            var from = new Vector3(x, y, z);
            // Menzil denetimi (TEKNIK 5.5): uzak istemci yakindaki Azman'a vurabilir.
            if (_actors.TryGetValue(id, out var a) && IsInstanceValid(a) && from.DistanceTo(a.GlobalPosition) < 60f) Hit(a, hit, from);
        });
    }

    /// <summary>
    /// kabarma3 kancasi: Doruk asamasi basliyor. KS her 5'in katiysa biyomun Azman'i at noktasinda doger. Doganin
    /// aktoru (yoksa null). Istemcide hicbir sey yapmaz (host doger, kukla kayitla gelir).
    /// </summary>
    public AzmanActor OnPeak(int ks, string biome, Vector3 at)
    {
        if (Puppet || !AzmanRules.ShouldAppear(Data, ks)) return null;
        var def = Data.ForBiome(biome);
        return def == null ? null : Summon(def.Id, ks, at);
    }

    /// <summary>Azman'i dogrudan cagirir (OnPeak, sinama). Host'ta.</summary>
    public AzmanActor Summon(string azmanId, int ks, Vector3 at)
    {
        if (Puppet) return null;
        var def = Data.Get(azmanId);
        var fight = AzmanRules.Spawn(Progress, Data, def, ks, TeamSize, _d.State.Ids.New(), at.X, at.Z);
        GD.Print($"AZMAN summon {def.Id} ks={ks} hp={fight.Body.MaxHp:0} at=({at.X:0},{at.Z:0})");
        _d.Hud?.Say($"{def.Name} geliyor!");
        Sfx.Play("gokyuzu_dalga", at, 2f, 0.8f);
        return Spawn(fight, arriving: true);
    }

    AzmanActor Spawn(AzmanFight fight, bool arriving)
    {
        var def = Data.Get(fight.AzmanId);
        var a = new AzmanActor(_d, this, def, fight.Id) { Name = $"Azman_{def.Id}_{fight.Id}" };
        AddChild(a);
        a.GlobalPosition = _d.World.Ground(new Vector3(fight.X, 0, fight.Z));
        a.Begin(arriving && !Puppet);
        _actors[fight.Id] = a;
        _d.Savas.Add(a);
        return a;
    }

    /// <summary>Safak: kalan Azmanlar cekilir (odul yok).</summary>
    public void DismissAll()
    {
        if (Puppet) return;
        foreach (var f in Progress.Active.ToList())
        {
            GD.Print($"AZMAN dismiss {f.AzmanId}");
            AzmanRules.Dismiss(Progress, f);
        }
        Reconcile();
    }

    public AzmanFight FightOf(ulong id) => Progress.Get(id);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Reconcile();
        if (Puppet) return;
        foreach (var f in Progress.Active.ToList())
        {
            if (!_actors.TryGetValue(f.Id, out var a) || !IsInstanceValid(a)) continue;
            var def = Data.Get(f.AzmanId);
            var areas = new List<CombatArea>();
            var cmds = AzmanRules.Tick(f, _d.Data.Combat, def, dt, areas);
            _d.Savas.Areas(a, areas);
            foreach (var c in cmds) Dispatch(a, f, def, c);
        }
    }

    /// <summary>Durumla aktorleri esler: kayittan ya da host'tan gelen dovus icin aktor kurar, biteni kaldirir.</summary>
    void Reconcile()
    {
        foreach (var f in Progress.Active)
            if (!_actors.TryGetValue(f.Id, out var a) || !IsInstanceValid(a)) Spawn(f, arriving: false);
        foreach (var id in _actors.Keys.ToList())
        {
            if (Progress.Get(id) != null) continue;
            var a = _actors[id];
            _actors.Remove(id);
            if (!IsInstanceValid(a)) continue;
            _d.Savas.Remove(a);
            if (!a.Dying) a.QueueFree();
        }
    }

    void Dispatch(AzmanActor a, AzmanFight f, AzmanDef def, AzmanCommand c)
    {
        switch (c.Cue)
        {
            case AzmanCue.Telegraph: a.Telegraph(c.Attack); break;
            case AzmanCue.Attack: a.Attack(c.Attack); break;
            case AzmanCue.DropRiders: RidersDropped += c.Count; a.DropRiders(c.Count); break;
            case AzmanCue.Defeated: Defeat(a, f, def); break;
        }
    }

    /// <summary>Isabet (silah, kutuk, alan etkisi): istemcide host'a gider. Sonuc doner.</summary>
    public CombatResult Hit(AzmanActor a, CombatHit hit, Vector3 from)
    {
        var f = FightOf(a.FightId);
        if (f == null || f.Defeated) return default;
        if (_d.Net?.Forward("azman.hit", w => { w.U64(a.FightId); w.F32(hit.Amount); w.U8((byte)hit.Type); w.U8((byte)hit.Procs); w.Bool(hit.Crit); w.Bool(hit.Weak); }) == true)
            return new CombatResult { Damage = hit.Amount }; // gorunus icin tahmin; gercek can host'tan
        var def = Data.Get(f.AzmanId);
        var cmds = new List<AzmanCommand>();
        var areas = new List<CombatArea>();
        var r = AzmanRules.Damage(f, _d.Data.Combat, def, hit, cmds, areas);
        _d.Savas.Areas(a, areas);
        foreach (var c in cmds) Dispatch(a, f, def, c);
        return r;
    }

    /// <summary>Hucum yapiya tosladi (Homur): Azman sersemler.</summary>
    public void WallStun(AzmanActor a)
    {
        var f = FightOf(a.FightId);
        if (f == null || Puppet) return;
        WallHits++;
        AzmanRules.WallStun(f, Data.Get(f.AzmanId));
    }

    public void EndAct(AzmanActor a) { var f = FightOf(a.FightId); if (f != null && !Puppet) AzmanRules.EndAct(f); }

    void Defeat(AzmanActor a, AzmanFight f, AzmanDef def)
    {
        GD.Print($"AZMAN defeated {def.Id} t={f.Time:0.0} ks={f.Ks}");
        DefeatedLog.Add(def.Id);
        var at = a.Center;
        int left = AzmanRules.Reward(Progress, f, def, _d.State.Backpack);
        foreach (var it in def.Reward.Items) Fx.FlyTo(at, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Warning, 0.2f);
        if (left > 0) _d.Hud?.Hint($"Çanta dolu: {left} ödül yere düşmedi", 3f);
        _d.Hud?.Say($"{def.Name} yenildi!");
        Events.RaiseAzmanDefeated(def.Id, def.Reward.Oz, at.X, at.Z); // Oz'u gece akisi (kabarma3) bu olayla yere birakir
        a.OnDefeated();
        _actors.Remove(f.Id);
        _d.Savas.Remove(a);
    }
}
