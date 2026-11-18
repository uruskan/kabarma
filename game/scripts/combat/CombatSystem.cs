using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ortak savasin Godot katmani (GDD §16.1, TEKNIK §3.1). Kural Sim'de (Combat, Status); burada yalniz:
/// hedef kaydi (statik yok, TEKNIK §9), isabeti hedefe iletme, Sim'in dondurdugu alan etkilerini (yanik sicramasi,
/// sok zinciri, Kabarik patlamasi) konuma gore komsulara uygulama ve durum gorunusu (renk, kivilcim).
/// Ata hedefleri AtaSystem'de kayitlidir ve buradan da aranir; yeni sistemler (Azman, kule, tuzak, Kabaran) Add ile girer.
/// </summary>
public partial class CombatSystem : Node
{
    readonly Director _d;
    readonly List<ICombatTarget> _targets = new();
    readonly List<CombatArea> _areas = new();
    /// <summary>Sinama ve rapor: islenen alan etkileri.</summary>
    public int Spreads, Chains, Bursts;

    public CombatSystem(Director d) { _d = d; }

    public CombatFile Data => _d.Data.Combat;

    public IReadOnlyList<ICombatTarget> Targets => _targets;

    public void Add(ICombatTarget t) { if (!_targets.Contains(t)) _targets.Add(t); }
    public void Remove(ICombatTarget t) => _targets.Remove(t);

    /// <summary>Bilinen butun canli hedefler (kayitlilar ve Ata hedefleri).</summary>
    public IEnumerable<ICombatTarget> All()
    {
        _targets.RemoveAll(t => t is Node n && !IsInstanceValid(n));
        foreach (var t in _targets) if (t.Alive) yield return t;
        if (_d.Atalar != null) foreach (var t in _d.Atalar.Targets) if (t.Alive) yield return t;
    }

    /// <summary>Isabet: hedefe iletir, alan etkilerini hedefin konumunda cozer.</summary>
    public CombatResult Hit(ICombatTarget t, CombatHit hit, Vector3 from)
    {
        if (t == null || !t.Alive) return default;
        var r = t.TakeHit(hit, from);
        Resolve(t, 0);
        return r;
    }

    /// <summary>
    /// Hedef kendi Sim kuralinda alan etkisi urettiyse (Combat.Apply/Tick'in areas listesi) burada cozulur.
    /// Sahipler kendi listelerini Areas(...) ile birakir.
    /// </summary>
    public void Areas(ICombatTarget source, List<CombatArea> areas)
    {
        if (areas == null || areas.Count == 0) return;
        _areas.AddRange(areas);
        areas.Clear();
        Resolve(source, 0);
    }

    void Resolve(ICombatTarget source, int depth)
    {
        if (_areas.Count == 0) return;
        var batch = _areas.ToList();
        _areas.Clear();
        if (depth > 3) return; // patlama zinciri sinirli
        var at = source.Center;
        foreach (var a in batch)
        {
            var near = All().Where(t => t != source && t.Body != null && Allowed(a, t.Body) && t.Center.DistanceTo(at) <= a.Radius + t.Radius)
                .OrderBy(t => t.Center.DistanceTo(at)).Take(a.MaxTargets).ToList();
            switch (a.Kind)
            {
                case AreaKind.BurnSpread: Spreads += near.Count; break;
                case AreaKind.ShockChain: Chains += near.Count; break;
                case AreaKind.SwellBurst: Bursts++; Fx.Burst(at, Palette.MayaTeal, 40, 7f, 0.22f, 0.9f, true, -1f); Sfx.Play("kabarik_gumleme", at, 2f); break;
            }
            foreach (var t in near)
            {
                if (a.Kind == AreaKind.ShockChain) Fx.Burst((at + t.Center) * 0.5f, Palette.Warning, 6, 3f, 0.06f, 0.25f, true);
                if (a.Kind == AreaKind.BurnSpread) Fx.Burst(t.Center, Palette.Accent, 6, 2f, 0.08f, 0.4f, true, -3f);
                // Alan etkisi de hedefin kendi kuralindan gecer (Ata asamasi, kalkan): isabet olarak iletilir.
                if (a.Kind == AreaKind.BurnSpread) Combat.ApplyArea(Data, t.Body, a);
                else
                {
                    t.TakeHit(new CombatHit(a.Amount, a.Type, a.Team) { Chained = a.Kind == AreaKind.ShockChain }, at);
                    Resolve(t, depth + 1);
                }
            }
        }
    }

    bool Allowed(in CombatArea a, CombatBody b) => a.Kind == AreaKind.BurnSpread
        ? b.Team == a.Team
        : Data.FriendlyFire || a.Team == CombatTeam.Neutral || b.Team != a.Team;

    /// <summary>
    /// Kayitli bir govdenin zamani (sahibi Ata gibi kendi kuralinda islemiyorsa): Combat.Tick ve alan etkileri.
    /// Islenen surekli hasar doner.
    /// </summary>
    public float TickBody(ICombatTarget t, float dt)
    {
        if (t?.Body == null || t.Body.Dead) return 0;
        var list = new List<CombatArea>();
        float dealt = Combat.Tick(Data, t.Body, dt, list);
        Areas(t, list);
        return dealt;
    }

    /// <summary>Durumun gorunus rengi (govde rengine carpilir): donma mavi, yanik turuncu, zehir yesil, sok sari.</summary>
    public static Color Tint(CombatBody b)
    {
        if (b == null || b.Statuses.Count == 0) return Colors.White;
        if (Status.Has(b, StatusKind.Lock)) return new Color(0.55f, 0.75f, 1f);
        if (Status.Has(b, StatusKind.Freeze)) return new Color(0.75f, 0.88f, 1f);
        if (Status.Has(b, StatusKind.Burn)) return new Color(1f, 0.7f, 0.5f);
        if (Status.Has(b, StatusKind.Poison)) return new Color(0.7f, 1f, 0.6f);
        if (Status.Has(b, StatusKind.Stun)) return new Color(1f, 1f, 0.6f);
        return Colors.White;
    }

    /// <summary>Capa basliginin isabeti: hasar turu combat.json'dan, kart sansi (gece) Procs'a.</summary>
    public CombatHit WeaponHit(WeaponDef w, float amount, bool crit, bool weak, float knock)
    {
        var night = _d.Night;
        var procs = StatusProc.None;
        if (night != null && night.RollBurn()) procs |= StatusProc.Burn;
        if (night != null && night.RollFreeze()) procs |= StatusProc.Freeze;
        return new CombatHit(amount, Data.WeaponType(w.Id), CombatTeam.Player) { Crit = crit, Weak = weak, Knock = knock, Procs = procs };
    }
}
