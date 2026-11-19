using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Atalarin ortak oyuna baglanmasi (CLAUDE.md "Ortak oyuna baglanma"). Host yetkili:
///   "ata.state" : Ata ilerlemesi ve suren dovus (AtaProgress, AtaFight: can, evre, konum) bolumu; istemci uygular.
///   "ata.offer" : istemci adak sunar / Kabarik Ata cagirir; host aktorun cantasindan dusurur (Maya dahil), menzili dogrular.
///   "ata.hit", "ata.weak", "ata.shear" : istemcinin vurusu host'a gider (menzil ve hasar siniri dogrulanir).
/// Istemcide Ata kuklasi yalniz yurur ve temas hasari verir (AtaActor.PuppetTick); saldiri efektleri, yardimcilar ve
/// ozel mekanikler (yutma, kok dalgasi) host'ta isler: bilinen bosluk (rapor).
/// </summary>
public partial class AtaSystem
{
    const float MaxNetDamage = 400f, MaxNetReach = 160f;
    int _clientPhase = -1;

    sealed class AtaNetSection : INetSection
    {
        readonly ISaveSection _s;
        readonly Director _d;
        public AtaNetSection(ISaveSection s, Director d) { _s = s; _d = d; }
        public string Id => "ata.state";
        public bool PerPeer => false;
        public void Write(NetWriter w, int peer) => w.Blob(_s.Write());
        public void Read(NetReader r)
        {
            var blob = r.Blob();
            if (!_d.NetClient) return;
            _s.Read(blob);
            _s.AfterRestore();
        }
    }

    void BindNet()
    {
        var net = _d.Net;
        if (net == null) return;
        var sec = _d.Save.Sections.FirstOrDefault(s => s.Id == "atalar");
        if (sec != null) net.Register(new AtaNetSection(sec, _d));
        net.On("ata.offer", (actor, r) =>
        {
            string id = r.Str(); int ks = r.U8();
            var def = Data.Atalar.FirstOrDefault(a => a.Id == id);
            var me = new Vector3(actor.Last.X, actor.Last.Y, actor.Last.Z);
            if (def == null || !_stones.ContainsKey(id) || me.DistanceTo(AltarPos(def)) > def.Arena.AltarReach + 8f || !Data.Kabarik.Ks.Contains(ks)) { actor.Rejected++; return; }
            HostOffer(def, Progress.IsDefeated(def.Id), actor.Bag, ks, false);
        });
        net.On("ata.hit", (actor, r) =>
        {
            var kind = (AtaHitKind)r.U8(); float amt = r.F32(); var type = (DamageType)r.U8(); var procs = (StatusProc)r.U8(); bool crit = r.Bool();
            if (!NetValid(actor, amt)) return;
            if (kind == AtaHitKind.Inside || kind == AtaHitKind.Burn) kind = AtaHitKind.Weapon; // istemci yutulmaz, yanik host'ta
            Damage(new CombatHit(amt, type, CombatTeam.Player) { Procs = procs, Crit = crit }, kind);
        });
        net.On("ata.weak", (actor, r) =>
        {
            int idx = r.VarI(); float amt = r.F32(); var type = (DamageType)r.U8(); var procs = (StatusProc)r.U8(); bool crit = r.Bool();
            if (!NetValid(actor, amt)) return;
            DamageWeakPoint(idx, new CombatHit(amt, type, CombatTeam.Player) { Procs = procs, Crit = crit });
        });
        net.On("ata.shear", (actor, r) =>
        {
            var at = new Vector3(r.F32(), r.F32(), r.F32());
            if (!NetValid(actor, 1f)) return;
            Shear(at);
        });
    }

    /// <summary>
    /// Sinama (--test=coop): host bir istemci aktorunun cantasiyla gercek adak yolundan (HostOffer: canta dusme, cagirma) Koca
    /// Pofuduk'u indirir; istemciler dovusu ve kuklayi gormeli. Dovus basladiysa true.
    /// </summary>
    public bool CoopProbe(Inventory bag)
    {
        var def = Data.Get("koca_pofuduk");
        if (_d.NetClient || Fight != null || Progress.IsDefeated(def.Id) || !_stones.ContainsKey(def.Id)) return false;
        foreach (var o in def.Offering.Where(o => !o.Swollen)) bag.Add(o.Item, o.Count);
        foreach (var o in def.Offering.Where(o => o.Swollen)) for (int i = 0; i < o.Count; i++) AtaRules.OfferSwollen(Progress, def, o.Item);
        HostOffer(def, false, bag, KabarikKs, false);
        return Fight != null;
    }

    /// <summary>Istemci vurusu makul mu: dovus suruyor, hasar sinirli, aktor Ata'ya yakin.</summary>
    bool NetValid(NetActor actor, float amount)
    {
        if (Fight == null || Actor == null || amount <= 0 || amount > MaxNetDamage) { actor.Rejected++; return false; }
        var me = new Vector3(actor.Last.X, actor.Last.Y, actor.Last.Z);
        if (me.DistanceTo(Actor.GlobalPosition) > MaxNetReach) { actor.Rejected++; return false; }
        return true;
    }

    /// <summary>Istemci: dovus durumundan Ata kuklasini kurar, evre degisince gosterir, bitince kaldirir.</summary>
    void ClientTick(float dt, Player p)
    {
        if (Fight == null)
        {
            SetSwell(1f);
            _clientPhase = -1;
            if (Actor != null)
            {
                var a = Actor;
                Targets.Remove(a);
                foreach (var m in Targets.OfType<AtaMinion>().ToList()) m.Poof();
                Targets.Clear();
                Actor = null;
                Bar?.Hide();
                if (IsInstanceValid(a)) a.OnDefeated();
            }
            foreach (var def in Data.Atalar) if (_stones.ContainsKey(def.Id)) AltarTick(def, p, dt);
            return;
        }
        var def0 = Data.Get(Fight.AtaId);
        if (!_stones.ContainsKey(def0.Id)) Place(def0.Id); // gecici arena: dovus baslamissa kurulur
        if (Actor == null || !IsInstanceValid(Actor)) { SpawnActor(def0, arriving: false); _clientPhase = Fight.Phase; return; }
        if (Fight.Phase != _clientPhase) { _clientPhase = Fight.Phase; Actor.OnPhase(Fight.Phase); }
        ColdTick(def0, dt);
        SetSwell(AtaRules.PlayerSwell(Fight, def0));
    }
}
