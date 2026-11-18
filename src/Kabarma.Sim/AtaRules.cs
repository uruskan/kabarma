namespace Kabarma.Sim;

/// <summary>Hasar kaynagi: silah, zayif noktaya silah, Bicme (kirkma), firlatilan yuk (boynuz parcasi), yanik.</summary>
public enum AtaHitKind { Weapon, Weak, Shear, Thrown, Burn }

/// <summary>
/// Ata kurallari (GDD §18, §18.1), motordan bagimsiz ve saf: durum AtaFight / AtaProgress'te, sayilar AtalarFile'da.
/// Adak, cagirma, hasar (zirh, sersemleme, asama gecisi), zaman (saldiri, yardimci), yenilgi ve odul.
/// </summary>
public static class AtaRules
{
    /// <summary>GDD §18: can carpani 1 + k × (n − 1).</summary>
    public static float TeamHpMult(AtalarFile data, int teamSize) => 1f + data.TeamHpPerExtra * (Math.Max(1, teamSize) - 1);

    /// <summary>Adagin bu bileseninden daha kac tane gerekiyor.</summary>
    public static int Missing(AtaProgress p, AtaDef def, AtaOfferingDef o)
    {
        p.Record(def.Id).Offered.TryGetValue(o.Item, out int have);
        return Math.Max(0, o.Count - have);
    }

    public static bool OfferingComplete(AtaProgress p, AtaDef def) => def.Offering.All(o => Missing(p, def, o) == 0);

    /// <summary>
    /// Cantadan adak sunar (etkilesim): eksik her canta bileseninden cantada ne varsa sunaga gecer. Kabarik urun
    /// (Swollen) cantadan gelmez, OfferSwollen ile. Sunulan toplam adet doner.
    /// </summary>
    public static int OfferFromBag(AtaProgress p, AtaDef def, Inventory bag)
    {
        if (p.Active != null || p.IsDefeated(def.Id)) return 0; // M2: yenilen Ata yeniden cagrilmaz (Kabarik Ata M4)
        int moved = 0;
        var rec = p.Record(def.Id);
        foreach (var o in def.Offering.Where(o => !o.Swollen))
        {
            int n = Math.Min(Missing(p, def, o), bag.Count(o.Item));
            if (n <= 0 || !bag.Remove(o.Item, n)) continue;
            rec.Offered[o.Item] = rec.Offered.GetValueOrDefault(o.Item) + n;
            moved += n;
        }
        return moved;
    }

    /// <summary>Kabarik urun sunaga birakildi (Cekim). Adakta bu urun eksikse kabul eder.</summary>
    public static bool OfferSwollen(AtaProgress p, AtaDef def, string crop)
    {
        if (p.Active != null || p.IsDefeated(def.Id)) return false;
        var o = def.Offering.FirstOrDefault(x => x.Swollen && x.Item == crop);
        if (o == null || Missing(p, def, o) == 0) return false;
        var rec = p.Record(def.Id);
        rec.Offered[crop] = rec.Offered.GetValueOrDefault(crop) + 1;
        return true;
    }

    /// <summary>
    /// Ata'yi indirir: adak tamamsa (ya da force: F1 gelistirici cagirmasi, GDD §18.1 M2 sinamasi) adak harcanir, dovus
    /// baslar. Baska dovus suruyorsa null.
    /// </summary>
    public static AtaFight? Summon(AtaProgress p, AtalarFile data, AtaDef def, int teamSize, bool force = false)
    {
        if (p.Active != null || p.IsDefeated(def.Id)) return null; // Kabarik Ata (KS ile yeniden cagirma) M4
        if (!force && !OfferingComplete(p, def)) return null;
        p.Record(def.Id).Offered.Clear();
        float hp = def.Hp * TeamHpMult(data, teamSize);
        var f = new AtaFight { AtaId = def.Id, TeamSize = Math.Max(1, teamSize), ShieldT = data.ArriveSeconds };
        f.Body = CombatBody.Of(hp, CombatTeam.Wild, def.Id, mass: def.Mass);
        EnterPhase(f, def, 0);
        f.AttackT = def.Phases[0].AttackEvery + data.ArriveSeconds;
        f.AddT = def.Phases[0].AddEvery;
        p.Active = f;
        return f;
    }

    static void EnterPhase(AtaFight f, AtaDef def, int phase)
    {
        var ph = def.Phases[phase];
        f.Phase = phase;
        f.Armor = ph.Armor;
        f.WeakPoints = Enumerable.Repeat(ph.WeakPointHp, ph.WeakPoints).ToList();
        f.ShardsDropped = ph.Shards;
        f.WeakOpenT = 0;
        f.AttackIndex = 0;
        f.TelegraphT = 0;
        f.Pending = "";
        f.StunT = 0;
        f.AddT = ph.AddEvery;
        Status.Clear(f.Body); // kabarir: yanik, donma, zehir soner
    }

    public static AtaPhaseDef PhaseOf(AtaFight f, AtaDef def) => def.Phases[Math.Clamp(f.Phase, 0, def.Phases.Count - 1)];

    /// <summary>
    /// Hasar (savas sozlesmesi, Combat.Apply): asamanin WeaponMult'u silaha (Weapon, Weak), sersemken StunMult; sonra
    /// ortak azaltma: tur profili (combat.json, Ata kimligi) → zirh. Zirh yalniz silah hasarini azaltir: Armor payi
    /// GDD §16.1 zirh puanina cevrilir. Tur durumu (yanik, donma, sok) govdeye eklenir. Kalkan (inis, asama gecisi)
    /// suresince hasar yok. Uygulanan hasar doner; komutlar cmds'e, alan etkileri areas'a eklenir.
    /// </summary>
    public static float Damage(AtaFight f, AtalarFile data, CombatFile combat, AtaDef def, float amount, AtaHitKind kind,
        List<AtaCommand>? cmds = null, DamageType type = DamageType.Darbe, StatusProc procs = StatusProc.None, List<CombatArea>? areas = null)
    {
        if (f.Defeated || f.ShieldT > 0 || amount <= 0) return 0;
        var ph = PhaseOf(f, def);
        bool weapon = kind is AtaHitKind.Weapon or AtaHitKind.Weak;
        float amt = amount * (weapon ? ph.WeaponMult : 1f) * (f.StunT > 0 ? Math.Max(1f, ph.StunMult) : 1f);
        if (string.IsNullOrEmpty(f.Body.Profile)) f.Body.Profile = def.Id;
        f.Body.Armor = weapon ? Combat.ArmorPointsFor(combat, f.Armor) : 0f;
        var r = Combat.Apply(combat, f.Body, new CombatHit(amt, type, CombatTeam.Player) { Procs = procs, IgnoreArmor = !weapon }, areas);
        f.DamageBySource[kind.ToString()] = f.DamageBySource.GetValueOrDefault(kind.ToString()) + r.Damage;
        CheckPhase(f, data, def, cmds);
        return r.Damage;
    }

    /// <summary>
    /// Durumlarin zamani (Combat.Tick): yanik ve zehir hasari islenir (asama esigi buradan da gecilir), yanik zirhi yer
    /// (Koca Pofuduk asama 2). Islenen surekli hasar doner.
    /// </summary>
    public static float TickStatus(AtaFight f, AtalarFile data, CombatFile combat, AtaDef def, float dt, List<AtaCommand>? cmds = null, List<CombatArea>? areas = null)
    {
        if (f.Defeated) return 0;
        if (Status.Has(f.Body, StatusKind.Burn)) Burn(f, def, dt);
        if (f.ShieldT > 0) return 0; // kalkanda durum yok: asama gecisi durumlari siler, inis isabet almaz
        float dealt = Combat.Tick(combat, f.Body, dt, areas);
        if (dealt > 0)
        {
            f.DamageBySource["Status"] = f.DamageBySource.GetValueOrDefault("Status") + dealt;
            CheckPhase(f, data, def, cmds);
        }
        return dealt;
    }

    static void CheckPhase(AtaFight f, AtalarFile data, AtaDef def, List<AtaCommand>? cmds)
    {
        if (f.Hp <= 0)
        {
            f.Defeated = true;
            cmds?.Add(new AtaCommand(AtaCue.Defeated, Phase: f.Phase));
            return;
        }
        // Bir vurus birden cok esigi gecse de asamalar sirayla gelir (her gecis kalkanla).
        var ph = PhaseOf(f, def);
        if (f.Phase < def.Phases.Count - 1 && f.Hp / f.MaxHp <= ph.HpUntil)
        {
            EnterPhase(f, def, f.Phase + 1);
            f.ShieldT = data.PhaseShiftSeconds;
            f.AttackT = PhaseOf(f, def).AttackEvery * 0.5f + data.PhaseShiftSeconds;
            cmds?.Add(new AtaCommand(AtaCue.PhaseChanged, Phase: f.Phase));
        }
    }

    /// <summary>Bicme vurusu (GDD §18.1 Koca Pofuduk asama 2): zirhi kirkar. Kalan zirh doner.</summary>
    public static float Shear(AtaFight f, AtaDef def)
    {
        var ph = PhaseOf(f, def);
        if (f.Defeated || ph.ShearPerHit <= 0) return f.Armor;
        f.Armor = Math.Max(0, f.Armor - ph.ShearPerHit);
        return f.Armor;
    }

    /// <summary>Yanik zirhi yer (saniyede BurnPerSecond).</summary>
    public static void Burn(AtaFight f, AtaDef def, float dt)
    {
        var ph = PhaseOf(f, def);
        if (ph.BurnPerSecond > 0) f.Armor = Math.Max(0, f.Armor - ph.BurnPerSecond * dt);
    }

    /// <summary>Hucum sutuna toslandi (Koca Pofuduk asama 3): sersemler. Asamada sersemleme yoksa false.</summary>
    public static bool Stun(AtaFight f, AtaDef def, List<AtaCommand>? cmds = null)
    {
        var ph = PhaseOf(f, def);
        if (f.Defeated || ph.StunSeconds <= 0) return false;
        f.StunT = ph.StunSeconds;
        f.TelegraphT = 0; f.Pending = "";
        cmds?.Add(new AtaCommand(AtaCue.Stunned, Phase: f.Phase));
        return true;
    }

    /// <summary>
    /// Zayif noktaya (kovan) hasar: kovanin cani duser; kirilinca Ata sersemler (ve kovan hasari govdeye de islenir:
    /// zayif vurus). Kovan kirildiysa true.
    /// </summary>
    public static bool HitWeakPoint(AtaFight f, AtalarFile data, CombatFile combat, AtaDef def, int index, float amount, List<AtaCommand>? cmds = null,
        DamageType type = DamageType.Darbe, StatusProc procs = StatusProc.None)
    {
        if (f.Defeated || index < 0 || index >= f.WeakPoints.Count || f.WeakPoints[index] <= 0) return false;
        if (!WeakOpen(f, def)) return false;
        f.WeakPoints[index] = Math.Max(0, f.WeakPoints[index] - amount);
        bool broke = f.WeakPoints[index] <= 0;
        if (broke)
        {
            cmds?.Add(new AtaCommand(AtaCue.WeakPointBroken, Count: index, Phase: f.Phase));
            Stun(f, def, cmds);
        }
        Damage(f, data, combat, def, amount, AtaHitKind.Weak, cmds, type, procs);
        return broke;
    }

    /// <summary>Zayif noktalar vurulabilir mi: asamada varsa ve (pencere tanimliysa) pencere aciksa.</summary>
    public static bool WeakOpen(AtaFight f, AtaDef def)
    {
        var ph = PhaseOf(f, def);
        return ph.WeakPoints > 0 && (ph.WeakWindow <= 0 || f.WeakOpenT > 0);
    }

    /// <summary>
    /// Zaman: kalkan ve sersemleme azalir; saldiri sirasi ve sezdirme; yardimci dalgasi. Sersemken saldiri ve yardimci yok.
    /// Zayif nokta penceresi (Dede: kok dalgasindan sonra kovanlar isir) sersemken de akar.
    /// </summary>
    public static List<AtaCommand> Tick(AtaFight f, AtaDef def, float dt)
    {
        var cmds = new List<AtaCommand>();
        if (f.Defeated) return cmds;
        f.Time += dt;
        var ph = PhaseOf(f, def);
        if (f.WeakOpenT > 0) f.WeakOpenT = Math.Max(0, f.WeakOpenT - dt);
        if (f.ShieldT > 0) { f.ShieldT = Math.Max(0, f.ShieldT - dt); return cmds; }
        if (f.StunT > 0) { f.StunT = Math.Max(0, f.StunT - dt); return cmds; }
        if (!Status.CanAct(f.Body)) return cmds; // savas: donma kilidi ya da sok sersemi saldiriyi durdurur

        if (f.TelegraphT > 0)
        {
            f.TelegraphT -= dt;
            if (f.TelegraphT <= 0)
            {
                cmds.Add(new AtaCommand(AtaCue.Attack, f.Pending, ph.VolleyCount, f.Phase));
                if (ph.WeakWindow > 0) f.WeakOpenT = ph.WeakWindow;
                f.Pending = "";
                f.TelegraphT = 0;
            }
        }
        else if (ph.Attacks.Count > 0)
        {
            f.AttackT -= dt;
            if (f.AttackT <= 0)
            {
                f.Pending = ph.Attacks[f.AttackIndex % ph.Attacks.Count];
                f.AttackIndex++;
                f.TelegraphT = Math.Max(0.01f, ph.Telegraph);
                f.AttackT = ph.AttackEvery;
                cmds.Add(new AtaCommand(AtaCue.Telegraph, f.Pending, 0, f.Phase));
            }
        }

        if (ph.AddCount > 0 && ph.AddEvery > 0)
        {
            f.AddT -= dt;
            if (f.AddT <= 0)
            {
                f.AddT = ph.AddEvery;
                int n = Math.Min(ph.AddCount, Math.Max(0, ph.AddMax - f.AddsAlive));
                if (n > 0) { f.AddsAlive += n; cmds.Add(new AtaCommand(AtaCue.SpawnAdds, "", n, f.Phase)); }
            }
        }
        return cmds;
    }

    /// <summary>
    /// Yenilgi: odul canta, muhur, ulti ve acilanlar ilerlemeye yazilir; dovus kapanir. Cantaya sigmayan odul adedi doner
    /// (Godot katmani yere birakir).
    /// </summary>
    public static int Reward(AtaProgress p, AtaDef def, Inventory bag)
    {
        var rec = p.Record(def.Id);
        rec.Defeats++;
        int left = 0;
        foreach (var it in def.Reward.Items) left += it.Count - bag.Add(it.Item, it.Count);
        if (!string.IsNullOrEmpty(def.Reward.Seal) && !p.Seals.Contains(def.Reward.Seal)) p.Seals.Add(def.Reward.Seal);
        if (!string.IsNullOrEmpty(def.Reward.Ulti) && !p.Ultis.Contains(def.Reward.Ulti)) p.Ultis.Add(def.Reward.Ulti);
        foreach (var u in def.Reward.Unlocks) if (!p.Unlocks.Contains(u)) p.Unlocks.Add(u);
        p.Active = null;
        return left;
    }

    /// <summary>Oyuncu arenadan kacti ya da herkes yenildi: dovus biter, Ata cekilir; adak geri gelmez (M2).</summary>
    public static void Abandon(AtaProgress p) => p.Active = null;
}
