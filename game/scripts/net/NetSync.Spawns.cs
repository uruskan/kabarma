using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Sonradan doğan yaratıklar (TEKNIK 5.2: host yetkili): gece dalgası Zıpzıpları, çayır sürüleri, yabani sürüler ve
/// sürüler (Wildlife). Yalnız host doğurur; istemcide çayır ve yabani doğurucu kapanır, gece dalgası doğurmaz
/// (Director.Night'ta tek satır). Host her yaratığa bir kimlik verir; "spawns" bölümü (kimlik + tür) istemcide kuklaları
/// kurar ve kaldırır; konum ve yön anlık görüntüde (kimlik 0x8000 bitiyle) gelir. İstemcinin kuklaya vuruşu host'a
/// gider (ForwardHit), host gerçek yaratığa uygular.
/// </summary>
public partial class NetSync
{
    const ushort DynBit = 0x8000;
    readonly Dictionary<ushort, (Node3D node, string kind)> _dyn = new();
    readonly Dictionary<Node3D, ushort> _dynId = new();
    ushort _dynNext = 1;
    /// <summary>Sonradan dogmus (host) ya da kuklasi kurulmus (istemci) yaratik sayisi.</summary>
    public int DynamicCount => _dyn.Count;

    void RegisterSpawns()
    {
        Register("spawns", false, WriteSpawns, ReadSpawns);
        On("hit", HostHit);
        On("whit", HostWildHit);
        if (IsClient)
        {
            // Istemcide dogurucular kapanir: yaratiklar host'tan gelir.
            if (D.Meadow != null) D.Meadow.ProcessMode = ProcessModeEnum.Disabled;
            if (D.Wildlife != null) D.Wildlife.OwnSpawning = false;
        }
    }

    static string KindOf(Node3D n) => n switch
    {
        Zipzip { Display: false } z => z.Swollen ? "z1" : "z0",
        WildCreature { Display: false } w => "w:" + w.Def.Id,
        WildSwarm { Display: false } s => "s:" + s.Def.Id,
        _ => null,
    };

    IEnumerable<Node3D> DynamicSources()
    {
        foreach (var z in Zipzip.All) yield return z;
        if (D.Meadow != null) foreach (var h in D.Meadow.Herds) foreach (var z in h.Members) yield return z;
        if (D.Wildlife != null)
        {
            foreach (var c in D.Wildlife.Creatures) yield return c;
            foreach (var s in D.Wildlife.Swarms) yield return s;
        }
    }

    static bool Alive(Node3D n) => GodotObject.IsInstanceValid(n) && n.IsInsideTree() && !n.IsQueuedForDeletion()
        && n is not Zipzip { Dead: true } && n is not WildCreature { Dead: true } && n is not WildSwarm { Dead: true };

    /// <summary>Host: yeni doğanlara kimlik verir, ölenleri çıkarır (TickCreatures ile saniyede bir... ve her bölüm turunda).</summary>
    void ScanDynamic()
    {
        if (!IsHost) return;
        foreach (var id in _dyn.Where(kv => !Alive(kv.Value.node)).Select(kv => kv.Key).ToList())
        {
            _dynId.Remove(_dyn[id].node);
            _dyn.Remove(id);
        }
        foreach (var n in DynamicSources())
        {
            if (!Alive(n) || _dynId.ContainsKey(n) || KindOf(n) is not { } kind) continue;
            while (_dyn.ContainsKey(_dynNext) || _dynNext == 0) _dynNext = (ushort)((_dynNext + 1) & 0x7FFF);
            _dyn[_dynNext] = (n, kind);
            _dynId[n] = _dynNext;
            _dynNext = (ushort)((_dynNext + 1) & 0x7FFF);
        }
    }

    void WriteSpawns(NetWriter w, int peer)
    {
        // Tarama TickCreatures'ta saniyede bir (es ve bolum turu basina degil: 220 yaratikta pahali).
        var l = _dyn.OrderBy(kv => kv.Key).ToList();
        w.VarU((uint)l.Count);
        foreach (var (id, (_, kind)) in l) { w.U16(id); w.Str(kind); }
    }

    void ReadSpawns(NetReader r)
    {
        int n = (int)r.VarU();
        var want = new Dictionary<ushort, string>(n);
        for (int i = 0; i < n; i++) { ushort id = r.U16(); want[id] = r.Str(); }
        foreach (var (id, (node, kind)) in _dyn.ToList())
        {
            if (want.TryGetValue(id, out var k) && k == kind && GodotObject.IsInstanceValid(node)) continue;
            if (GodotObject.IsInstanceValid(node)) node.QueueFree();
            _dyn.Remove(id);
            _dynId.Remove(node);
            _creatureLerp.Remove((ushort)(id | DynBit));
        }
        foreach (var (id, kind) in want)
        {
            if (_dyn.ContainsKey(id)) continue;
            var node = MakePuppet(kind);
            if (node == null) continue;
            node.Visible = false; // ilk kare konumu koyunca gorunur
            _dyn[id] = (node, kind);
            _dynId[node] = id;
        }
    }

    /// <summary>İstemci: türden kukla kurar (yapay zekâsı kapalı, klibi oynar).</summary>
    Node3D MakePuppet(string kind)
    {
        Node3D n = null;
        if (kind is "z0" or "z1")
        {
            var def = kind == "z1" ? D.Data.Creature(D.Data.Kabarma.FirstNight.Enemy) : D.Data.Creature("zipzip");
            var z = new Zipzip(def, D, 1f, 1f, swollen: kind == "z1") { Name = "NetZipzip" };
            D.AddChild(z);
            n = z;
        }
        else if (kind.Length > 2 && kind[1] == ':' && D.Wildlife != null)
        {
            var def = D.Data.Creatures.Creatures.FirstOrDefault(c => c.Id == kind[2..]);
            if (def != null) n = D.Wildlife.Spawn(def, Vector3.Zero, display: false, count: 1).FirstOrDefault();
        }
        if (n != null) Puppet(n);
        return n;
    }

    Node3D DynNode(ushort id) => _dyn.TryGetValue((ushort)(id & ~DynBit), out var e) && GodotObject.IsInstanceValid(e.node) ? e.node : null;

    void WriteDynamicFrames(List<CreatureFrame> list, NetActor to, double now)
    {
        var me = new Vector3(to.Last.X, to.Last.Y, to.Last.Z);
        foreach (var (id, (n, _)) in _dyn)
        {
            if (!Alive(n)) continue;
            int key = id | DynBit;
            float hz = n.GlobalPosition.DistanceTo(me) <= Cfg.CreatureNearRadius ? Cfg.CreatureNearHz : Cfg.CreatureFarHz;
            if (_creatureNext.TryGetValue((to.Peer, key), out var next) && now < next - 1e-3) continue;
            _creatureNext[(to.Peer, key)] = now + 1.0 / hz;
            var p = n.GlobalPosition;
            list.Add(new CreatureFrame { Id = (ushort)key, X = p.X, Y = p.Y, Z = p.Z, Yaw = n.GlobalRotation.Y, State = (byte)(n.Visible ? 1 : 0) });
        }
    }

    // ---------------------------------------------------------------- vurus

    /// <summary>Capa.Weapons.WeaponHit: istemcide kuklaya vurus host'a gider (true: yerelde hasar verme, yalniz his).</summary>
    public bool ForwardHit(Zipzip z, float dmg, bool crit, Vector3 dir, float knock, bool weak, float burn, float freeze)
    {
        if (IsHost) return false;
        if (!_dynId.TryGetValue(z, out var id)) return true; // tanimsiz kukla: yerelde de hasar yok
        Forward("hit", w =>
        {
            w.U16(id); w.F32(dmg); w.U8((byte)((crit ? 1 : 0) | (weak ? 2 : 0)));
            w.F32(dir.X); w.F32(dir.Z); w.F32(knock); w.F32(burn); w.F32(freeze);
        });
        return true;
    }

    /// <summary>
    /// Wildlife.ProjectileHit: istemcide mermi dogrusu host'a gider; yerelde kuklaya hasarsiz bakilir (mermi durur, his
    /// oynar). Host'ta ve tek oyuncuda null (yerel kural isler).
    /// </summary>
    public bool? ForwardWildHit(Vector3 from, Vector3 to, float radius, float damage, ISet<Node3D> already)
    {
        if (IsHost || D.Wildlife == null) return null;
        Forward("whit", w => { w.F32(from.X); w.F32(from.Y); w.F32(from.Z); w.F32(to.X); w.F32(to.Y); w.F32(to.Z); w.F32(radius); w.F32(damage); });
        return Wildlife.Strike(D.Wildlife.Creatures, D.Wildlife.Swarms, from, to, radius, 0f, already) != null;
    }

    void HostWildHit(NetActor a, NetReader r)
    {
        var from = new Vector3(r.F32(), r.F32(), r.F32());
        var to = new Vector3(r.F32(), r.F32(), r.F32());
        float radius = Mathf.Clamp(r.F32(), 0f, 1f), damage = Mathf.Clamp(r.F32(), 0f, 200f); // hile payi: tek mermi
        if (D.Wildlife == null || !NetRules.InRange(a, from.X, from.Y, from.Z, 80f) || from.DistanceTo(to) > 20f) { Reject(a, "whit"); return; }
        Wildlife.Strike(D.Wildlife.Creatures, D.Wildlife.Swarms, from, to, radius, damage);
    }

    void HostHit(NetActor a, NetReader r)
    {
        ushort id = r.U16();
        float dmg = r.F32();
        byte fl = r.U8();
        var dir = new Vector3(r.F32(), 0, r.F32());
        float knock = r.F32(), burn = r.F32(), freeze = r.F32();
        if (!_dyn.TryGetValue(id, out var e) || e.node is not Zipzip z || !Alive(z)) return;
        // Hile payi: menzil (silah menzili ag payiyla) ve tek vurusun ust siniri (kartli en buyuk hasarin cok ustu degil).
        var p = z.GlobalPosition;
        if (!NetRules.InRange(a, p.X, p.Y, p.Z, 80f)) { Reject(a, "hit range"); return; }
        dmg = Mathf.Clamp(dmg, 0f, z.MaxHp);
        z.Hit(dmg, (fl & 1) != 0, dir.Normalized(), Mathf.Clamp(knock, 0f, 50f), (fl & 2) != 0);
        if (burn > 0) z.BurnT = Mathf.Max(z.BurnT, Mathf.Min(burn, 10f));
        if (freeze > 0) z.FreezeT = Mathf.Max(z.FreezeT, Mathf.Min(freeze, 10f));
    }
}
