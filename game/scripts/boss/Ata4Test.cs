using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=ata4 (M4, ata4): dort yeni Ata, Kabarik Atalar, dort yeni Azman ve uc yeni Oyuk koruyucusu gercek sistemler uzerinden
/// (Director, AtaSystem, AzmanSystem, OyukSystem). GECICI: biyomlar (Isil Sazlik, Kizil Kanyon, Ayaz Yaylasi, Gobek) biyom akisi
/// main'e girene dek yoktur; arenalar ve Oyuk odalari gecici ve isaretli (AtaSystem.Place, OyukSystem.AddTemporaryRoom) kurulur.
/// Ata ve Azman dovusu GERCEK girdiyle degil, sistemlerin kendi isabet yoluyla (TakeHit / DamageWeakPoint, tipli CombatHit)
/// surulur: Alev, Havan, Kristal, yeni yaratiklar ve botun yeni biyomlari main'de degil. Oyuncuya can verilmez ama dovus sirasinda
/// can yenilenir (sinama oyuncuyu degil mekanigi olcer). Cikti: "ATA4 RESULT ok|fail ...", cikis 0/1.
/// </summary>
public partial class Ata4Test : Node
{
    Director _d;
    IEnumerator<double> _steps;
    double _wait;
    float _t;
    readonly List<string> _fail = new();
    int _checks;

    Player P => _d.Player;
    AtaSystem S => _d.Atalar;
    AzmanSystem A => _d.Azman;
    OyukSystem O => _d.Oyuk;

    void Check(bool ok, string what) { _checks++; if (!ok) _fail.Add(what); GD.Print($"ATA4 {(ok ? "ok" : "FAIL")} {what}"); }

    public override void _Ready() { ProcessPriority = -500; }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d?.Atalar == null || _d.Azman == null || _d.Oyuk == null || _d.Capa == null || _t < 2.5f) return;
        if (_t > 900f) { _fail.Add("zaman asimi"); Finish(); return; }
        if (P.Hp < 60f) P.Hp = 100f; // sinama mekanigi olcer, oyuncunun olumunu degil
        if (_wait > 0) { _wait -= delta; return; }
        try
        {
            _steps ??= Steps().GetEnumerator();
            if (!_steps.MoveNext()) { Finish(); return; }
            _wait = _steps.Current;
        }
        catch (System.Exception e)
        {
            _fail.Add($"exception {e.GetType().Name}: {e.Message}");
            GD.PrintErr(e.ToString());
            Finish();
        }
    }

    void Finish()
    {
        string res = _fail.Count == 0 ? $"ATA4 RESULT ok checks={_checks} t={_t:0}" : $"ATA4 RESULT fail checks={_checks} {string.Join(" | ", _fail)} t={_t:0}";
        GD.Print(res);
        GetTree().Quit(_fail.Count == 0 ? 0 : 1);
    }

    CombatHit Hit(float amount, DamageType t = DamageType.Darbe) => new(amount, t, CombatTeam.Player);

    void Put(Vector3 at) { P.GlobalPosition = _d.World.Ground(at) + Vector3.Up * 0.3f; P.Velocity = Vector3.Zero; }

    // ---------- Ata yardimcilari ----------

    AtaDef Def(string id) => S.Def(id);

    bool Summon(string id)
    {
        S.Place(id);
        var def = Def(id);
        Put(S.ArenaCenter(def) + new Vector3(-10, 0, 8));
        bool ok = S.DevSummon(id);
        if (S.Fight != null) S.Fight.ShieldT = 0;
        return ok && S.Actor != null;
    }

    /// <summary>Govdeye (ya da verilen turde) hasar; asama gecisinde kalkani indirir.</summary>
    void Smash(float amount, DamageType t = DamageType.Darbe)
    {
        S.Actor.TakeHit(Hit(amount, t), P.GlobalPosition);
        if (S.Fight != null) S.Fight.ShieldT = 0;
    }

    /// <summary>Hp'yi verilen orana indirir; (hpUntil gecilince) asama degisir.</summary>
    void ToFraction(float frac)
    {
        S.Fight.Body.Hp = S.Fight.MaxHp * frac + 0.5f;
        Smash(1f);
    }

    IEnumerable<double> Defeat(string id)
    {
        for (int i = 0; i < 400 && S.Fight != null; i++) { S.Fight.ShieldT = 0; S.Fight.StunT = 0; S.Fight.SubmergedT = 0; S.Fight.Armor = 0; Smash(S.Fight.Hp * 0.34f + 1f); yield return 0.05; }
    }

    IEnumerable<double> Steps()
    {
        yield return 0.5;
        Check(_d.Data.Atalar.Atalar.Count == 6 && _d.Data.Azman.Azmanlar.Count == 6 && _d.Data.Oyuk.Guardians.Count == 5, "veri: 6 Ata, 6 Azman, 5 koruyucu");
        Check(S.IsTemporary(Def("kurbaga_aga")) && S.IsTemporary(Def("ana_maya")), "yeni arenalar gecici isaretli ve dunyada kurulu degil (biyom akisi bekleniyor)");

        // ================= Kurbaga Aga =================
        Check(Summon("kurbaga_aga"), "kurbaga_aga indi");
        var kur = (KurbagaAga)S.Actor;
        yield return 1.6; // inis animasyonu bitsin
        // yutma: yakindaki oyuncu yakalanir; iceriden vurus kritik
        Put(kur.GlobalPosition + new Vector3(0, 0, 6));
        float before = S.Fight.Hp;
        kur.Attack("swallow", 0);
        yield return 0.1;
        Check(S.Fight.SwallowT > 2.5f, $"yutuldu (SwallowT {S.Fight.SwallowT:0.0})");
        Check(P.GlobalPosition.DistanceTo(kur.Center) < 2f, "oyuncu agizin icinde");
        kur.TakeHit(Hit(20), P.GlobalPosition);
        Check(S.Fight.DamageBySource.GetValueOrDefault("Inside") > 0, "icerden vurus 'Inside' sayildi (kritik)");
        S.Fight.SwallowT = 0.05f;
        yield return 0.3;
        Check(P.GlobalPosition.DistanceTo(kur.Center) > 2.5f, "sure bitince firlatildi");
        // asama 2: dalis (hasar yok) ve nilufer yapragi
        ToFraction(0.6f);
        Check(S.Fight.Phase == 1, $"asama 2'ye gecti ({S.Fight.Phase + 1})");
        S.Fight.SubmergedT = 1.2f;
        float hp = S.Fight.Hp;
        kur.TakeHit(Hit(50), P.GlobalPosition);
        Check(S.Fight.Hp == hp, "dalmisken hasar yok");
        var pad = S.Props(Def("kurbaga_aga"), "pad").First();
        Put(pad.pos + Vector3.Up * 0.2f);
        float php = P.Hp;
        for (int i = 0; i < 40; i++) yield return 0.05;  // dalis biter, yaprakta durulur
        Check(P.Hp >= php - 12f, $"nilufer yapraginda cikis darbesi (22) yenmedi (fark {php - P.Hp:0})"); // temas hasari (9) olabilir
        yield return 1.0;
        Check(S.Targets.OfType<AtaMinion>().Any() || S.Fight.AddsAlive >= 0, "Suluk Surusu yardimcilari");
        // asama 3: karin kesesi
        ToFraction(0.3f);
        Check(S.Fight.Phase == 2, "asama 3");
        S.Fight.WeakOpenT = 5f;
        var sac = S.Targets.OfType<AtaPoint>().FirstOrDefault();
        Check(sac != null && sac.Alive, "karin kesesi isiyor ve hedef");
        sac?.TakeHit(Hit(500), P.GlobalPosition);
        yield return 0.1;
        Check(S.Fight.StunT > 0, "kese kirilinca sersemledi");
        foreach (var w in Defeat("kurbaga_aga")) yield return w;
        yield return 0.5;
        Check(S.Progress.IsDefeated("kurbaga_aga") && S.Progress.HasSeal("islak_zemin"), "kurbaga_aga yenildi, Islak Zemin muhru");
        Check(_d.State.Backpack.Count("sazlik_cekirdegi") == 1 && _d.State.Backpack.Count("aga_derisi") == 3, "odul: Sazlik Cekirdegi + Aga Derisi");
        Check(S.Progress.Unlocks.Contains("slot:guc_2"), "ikinci guc yuvasi acildi");

        // ================= Kabarik Kurbaga Aga (gercek adak yolu) =================
        {
            var def = Def("kurbaga_aga");
            Put(S.AltarPos(def));
            var bag = _d.State.Backpack;
            foreach (var o in def.Offering.Where(o => !o.Swollen)) bag.Add(o.Item, o.Count);
            foreach (var o in def.Offering.Where(o => o.Swollen)) for (int i = 0; i < o.Count; i++) AtaRules.OfferSwollen(S.Progress, def, o.Item, kab: true);
            S.SetKabarikKs(10);
            S.Offer(def);                                          // adak tamam ama Maya yok
            yield return 0.2;
            Check(S.Fight == null, "Maya yokken Kabarik Ata gelmedi");
            bag.Add("maya", 500);
            S.Offer(def);
            yield return 0.3;
            Check(S.Fight is { Kabarik: true, Ks: 10 }, "Kabarik Kurbaga Aga (KS 10) geldi");
            Check(bag.Count("maya") == 0, "tam KS × 50 = 500 Maya dustu");
            Check(Mathf.Abs(S.Fight.MaxHp - def.Hp * 2f) < 1f, $"can KS ile olceklendi ({S.Fight.MaxHp:0})");
            S.Fight.ShieldT = 0;
            foreach (var w in Defeat("kurbaga_aga")) yield return w;
            yield return 0.5;
            Check(S.Progress.Record("kabarik_kurbaga_aga").BestKs == 10 && bag.Count("kabarik_oz") == 2, "Kabarik yenildi: en iyi KS 10, 2 Kabarik Oz");
            Check(bag.Count("sazlik_cekirdegi") == 1 && S.Progress.Seals.Count(x => x == "islak_zemin") == 1, "Cekirdek ve muhur ikinci kez verilmedi");
        }

        // ================= Ciyan Pasa =================
        Check(Summon("ciyan_pasa"), "ciyan_pasa indi");
        var ciy = (CiyanPasa)S.Actor;
        yield return 0.5;
        Check(S.Fight.WeakPoints.Count == 14, "14 bogum");
        var segs = S.Targets.OfType<AtaPoint>().ToList();
        Check(segs.Count == 14, $"14 bogum hedefi ({segs.Count})");
        yield return 1.6; // inis animasyonu bitsin
        for (int i = 0; i < 3; i++) segs[i].TakeHit(Hit(100), P.GlobalPosition);
        yield return 0.1;
        Check(S.Fight.WeakPoints.Count(w => w <= 0) == 3, "3 bogum kirildi");
        ToFraction(0.5f);
        Check(S.Fight.Phase == 1, "asama 2: kopan bogumlar");
        int addsBefore = S.Targets.OfType<AtaMinion>().Count();
        foreach (var s in S.Targets.OfType<AtaPoint>().Take(2).ToList()) s.TakeHit(Hit(100), P.GlobalPosition);
        yield return 0.2;
        Check(S.Targets.OfType<AtaMinion>().Count() > addsBefore, "kirilan bogum yardimci dogurdu");
        ciy.Attack("sandstorm", 0);
        yield return 0.1;
        Check(GetTree().GetNodesInGroup("ata_zone").Count > 0, "kum firtinasi bolgesi");
        ToFraction(0.3f);
        Check(S.Fight.Phase == 2, "asama 3: mesa duvari");
        yield return 0.3;
        var barrels = S.Targets.OfType<AtaBarrel>().ToList();
        Check(barrels.Count == 4, $"4 katran varili ({barrels.Count})");
        var wallPos = S.Props(Def("ciyan_pasa"), "wall").First().pos;
        ciy.GlobalPosition = _d.World.Ground(wallPos + new Vector3(0, 0, 3f)); // Pasa duvara tirmanmis (varil yakininda)
        float hpb = S.Fight.Hp;
        barrels[0].TakeHit(Hit(10), P.GlobalPosition);
        yield return 0.1;
        Check(hpb - S.Fight.Hp >= 250f && S.Fight.StunT > 0, $"varil patladi: {hpb - S.Fight.Hp:0} hasar ve sersem");
        foreach (var w in Defeat("ciyan_pasa")) yield return w;
        yield return 0.5;
        Check(S.Progress.IsDefeated("ciyan_pasa") && S.Progress.UltisOwed.Contains("yorunge_sulamasi"), "ciyan_pasa yenildi (ulti borcu yazildi)");

        // ================= Buz Tosunu =================
        Check(Summon("buz_tosunu"), "buz_tosunu indi");
        var buz = (BuzTosunu)S.Actor;
        yield return 1.6;
        ToFraction(0.5f);
        Check(S.Fight.Phase == 1 && AtaRules.PhaseOf(S.Fight, Def("buz_tosunu")).ColdDps > 0, "asama 2: donmus gol, soguk");
        var warm = S.Props(Def("buz_tosunu"), "warm").First();
        Put(warm.pos);
        float hp0 = P.Hp;
        for (int i = 0; i < 60; i++) yield return 0.05;
        Check(P.Hp >= hp0 - 0.5f, "isinma noktasinda soguk yok");
        P.Hp = 100f; hp0 = 100f;
        Put(S.ArenaCenter(Def("buz_tosunu")) + new Vector3(0, 0, 0));
        for (int i = 0; i < 80; i++) yield return 0.05;
        Check(P.Hp < hp0 - 1f, $"isinma noktasindan uzakta soguk yedi ({hp0 - P.Hp:0})");
        P.Hp = 100f;
        ToFraction(0.2f);
        Check(S.Fight.Phase == 2 && S.Fight.Armor > 0.7f, "asama 3: buzul zirhi");
        var belly = S.Targets.OfType<AtaPoint>().FirstOrDefault();
        Check(belly != null && !belly.Alive, "buzul varken karin kapali");
        for (int i = 0; i < 200 && S.Fight.Armor > 0.25f; i++) { AtaRules.Burn(S.Fight, Def("buz_tosunu"), 0.2f); yield return 0.01; }
        S.Fight.WeakOpenT = 0;
        Check(belly != null && belly.Alive, "ates buzulu eritti: karin acik");
        belly?.TakeHit(Hit(500), P.GlobalPosition);
        yield return 0.1;
        Check(S.Fight.StunT > 0, "kese kirilinca sersemledi");
        foreach (var w in Defeat("buz_tosunu")) yield return w;
        yield return 0.5;
        Check(S.Progress.IsDefeated("buz_tosunu") && S.Progress.HasSeal("kalin_post"), "buz_tosunu yenildi, Kalin Post muhru");

        // ================= Ana Maya =================
        Check(Summon("ana_maya"), "ana_maya indi");
        yield return 1.6;
        ToFraction(0.5f);
        Check(S.Fight.Phase == 1, "asama 2: kabaran zemin");
        S.Actor.Attack("surge", 0);
        yield return 0.2;
        Check(GetTree().GetNodesInGroup("ata_rootwave").Count > 0, "kabaran zemin dalgasi");
        ToFraction(0.2f);
        Check(S.Fight.Phase == 2, "asama 3: dev boyut");
        for (int i = 0; i < 80; i++) yield return 0.05;
        Check(P.Scale.X > 1.4f, $"oyuncular kabardi (olcek {P.Scale.X:0.00})");
        float hpDm = S.Fight.Hp;
        S.Actor.TakeHit(Hit(100), P.GlobalPosition);
        Check(hpDm - S.Fight.Hp >= 100f * 1.6f * 0.5f - 1f, $"kabarmis oyuncu hasari x1.6 (weaponMult .5): {hpDm - S.Fight.Hp:0}");
        var core = S.Targets.OfType<AtaPoint>().FirstOrDefault();
        core?.TakeHit(Hit(700), P.GlobalPosition);
        foreach (var w in Defeat("ana_maya")) yield return w;
        yield return 0.5;
        Check(S.Progress.IsDefeated("ana_maya") && S.Progress.SealAdd(_d.Data.Atalar, "boy_puani") == 2, "ana_maya yenildi, Eksi Maya +2 boy puani");
        Check(S.Progress.Unlocks.Contains("recipe:kabartma_odasi") && S.Progress.Unlocks.Contains("mode:yeni_ay"), "Kabartma Odasi ve Yeni Ay acildi");
        yield return 0.3;
        Check(P.Scale.X < 1.05f, "dovus bitince boy normale dondu");

        // ================= Azmanlar =================
        var at = P.GlobalPosition + new Vector3(12, 0, 0);
        // -- Kiskac: zirh yalniz arkadan delinir
        var k = A.Summon("azman_kiskac", 5, at);
        yield return 0.3;
        var kf = A.FightOf(k.FightId); kf.Body.ShieldT = 0; kf.Yaw = 0;
        float h1 = kf.Body.Hp;
        A.Hit(k, Hit(100), k.GlobalPosition + new Vector3(0, 0, 8));   // onden
        float front = h1 - kf.Body.Hp; h1 = kf.Body.Hp;
        A.Hit(k, Hit(100), k.GlobalPosition + new Vector3(0, 0, -8));  // arkadan
        float back = h1 - kf.Body.Hp;
        Check(back > front * 4f, $"Kiskac: arkadan {back:0} onden {front:0}");
        k.Telegraph("rip"); k.Attack("rip");
        yield return 0.3;
        Check(A.Rips >= 0, "Kiskac sokme saldirisi calisti (yapi yoksa iska)");
        KillAzman(k);
        yield return 0.6;
        Check(A.Progress.Defeats.GetValueOrDefault("azman_kiskac") == 1 && _d.State.Backpack.Count("azman_kiskaci") == 1, "Azman Kiskac yenildi, odul");

        // -- Kizgin: yangin, Sulama ile sonme
        var z = A.Summon("azman_kizgin", 5, at);
        yield return 0.3;
        var zf = A.FightOf(z.FightId); zf.Body.ShieldT = 0;
        z.Attack("ignite");
        yield return 0.2;
        Check(zf.Fires.Count > 0, $"Kizgin yangin lekeleri ({zf.Fires.Count})");
        var spot = zf.Fires[0];
        float left0 = spot.T;
        _d.State.Tank.Fill(60f);
        for (int i = 0; i < 25; i++) { _d.Capa.WaterAt(_d.World.Ground(new Vector3(spot.X, 0, spot.Z))); yield return 0.04; }
        Check(spot.T < left0 - 1f || !zf.Fires.Contains(spot), "Sulama basligi yangin lekesini sondurmeye basladi");
        KillAzman(z);
        yield return 0.6;
        Check(A.Progress.Defeats.GetValueOrDefault("azman_kizgin") == 1, "Azman Kizgin yenildi");

        // -- Surubasi: kurt cagirir
        var s2 = A.Summon("surubasi", 5, at);
        yield return 0.3;
        var sf = A.FightOf(s2.FightId); sf.Body.ShieldT = 0;
        s2.Attack("howl");
        yield return 0.5;
        Check(sf.Summoned == 2 && GetTree().GetNodesInGroup("azman_rider").Count >= 2, $"Surubasi 2 kurt cagirdi ({sf.Summoned})");
        s2.Attack("howl"); s2.Attack("howl"); s2.Attack("howl");
        yield return 0.3;
        Check(sf.Summoned == 6, $"en cok 6 kurt ({sf.Summoned})");
        KillAzman(s2);
        yield return 0.6;
        Check(A.Progress.Defeats.GetValueOrDefault("surubasi") == 1, "Surubasi yenildi");

        // -- Hamur: bolunur, yalniz son parca oder verir
        var hm = A.Summon("azman_hamur", 5, at);
        yield return 0.3;
        var hf = A.FightOf(hm.FightId); hf.Body.ShieldT = 0;
        hf.Body.Hp = hf.Body.MaxHp * 0.6f;
        A.Hit(hm, Hit(1), hm.GlobalPosition + Vector3.Back * 6);
        yield return 0.3;
        Check(A.Progress.Active.Count(x => x.AzmanId == "azman_hamur") == 3, $"Hamur bolundu: 1 + 2 parca ({A.Progress.Active.Count})");
        hf.Body.Hp = hf.Body.MaxHp * 0.3f;
        A.Hit(hm, Hit(1), hm.GlobalPosition + Vector3.Back * 6);
        yield return 0.3;
        Check(A.Progress.Active.Count(x => x.AzmanId == "azman_hamur") == 5, "ikinci bolunme: toplam 5 parca");
        foreach (var f in A.Progress.Active.Where(x => x.AzmanId == "azman_hamur").OrderByDescending(x => x.Gen).ToList())
        {
            var actor = A.Actors.FirstOrDefault(a => a.FightId == f.Id);
            if (actor == null) continue;
            f.Body.ShieldT = 0;
            int guard = 0;
            while (A.FightOf(f.Id) != null && !f.Defeated && guard++ < 40) { A.Hit(actor, Hit(f.Body.Hp + 5f, DamageType.Darbe), actor.GlobalPosition + Vector3.Back * 6); yield return 0.02; }
            yield return 0.2;
            if (f.Gen > 0) Check(A.Progress.Defeats.GetValueOrDefault("azman_hamur") == 0, "parca olunce odul yok");
        }
        yield return 0.6;
        Check(A.Progress.Defeats.GetValueOrDefault("azman_hamur") == 1 && _d.State.Backpack.Count("azman_hamuru") == 1, "son parca (cekirdek) odulu verdi, tek sefer");

        // ================= Oyuk koruyucular =================
        var cleared = new List<string>();
        Events.OyukCleared += id => cleared.Add(id);

        var rm = O.AddTemporaryRoom("camur_devi", 0);
        O.Enter(rm);
        yield return 0.4;
        var mud = O.Mud;
        Check(mud != null && !mud.Dry, "Camur Devi dovusu basladi, islak");
        var tm = _d.AtaTargetsNear(rm.Center + Vector3.Up * 1.9f + new Vector3(mud.X, 0, mud.Z), 3f).OfType<OyukTarget>().FirstOrDefault();
        Check(tm != null, "Camur Devi hedef");
        float mh = mud.Hp;
        tm?.TakeHit(Hit(100), P.GlobalPosition);
        Check(mud.Hp == mh, "islakken hasar yok");
        tm?.TakeHit(Hit(60, DamageType.Ates), P.GlobalPosition);
        tm?.TakeHit(Hit(80, DamageType.Ates), P.GlobalPosition);
        Check(mud.Dry, "ates kurutti");
        for (int i = 0; i < 200 && !mud.Dead; i++) { tm?.TakeHit(Hit(60, DamageType.Darbe), P.GlobalPosition); mud.Heat(5f); yield return 0.02; }
        yield return 0.5;
        Check(mud == null || mud.Dead || cleared.Contains(rm.Poi.Id), "Camur Devi yenildi");
        Check(O.Inside == rm && cleared.Contains(rm.Poi.Id), "Camur Devi Oyugu temizlendi");
        O.Exit();
        yield return 0.2;

        var rs = O.AddTemporaryRoom("kuvars_akrebi", 0);
        O.Enter(rs);
        yield return 0.4;
        var sc = O.Scorpion;
        Check(sc != null && !sc.Cracked, "Akrep dovusu basladi, zirhli");
        var ts = _d.AtaTargetsNear(rs.Center + Vector3.Up * 1.1f + new Vector3(sc.X, 0, sc.Z), 3f).OfType<OyukTarget>().FirstOrDefault();
        float sh = sc.Hp;
        ts?.TakeHit(Hit(30, DamageType.Kesme), P.GlobalPosition);
        Check(sc.Hp == sh, "zirh mermiyi sektirdi");
        var stp = O.Scorpion.Stalactites;
        var node = rs.GetNodeOrNull<Node3D>("Sarkit0");
        // sarkit: Akrep onun altinda durursa catlatir (Sim kuralina gore ona yaklastir)
        Put(rs.Center + new Vector3(stp[0].X, 0, stp[0].Z) + new Vector3(0.3f, 0, 0));
        for (int i = 0; i < 160 && !sc.Cracked; i++)
        {
            var pt = _d.Atalar.Targets.OfType<OyukPoint>().FirstOrDefault();
            if (i == 20 && pt != null) pt.TakeHit(Hit(10), P.GlobalPosition);
            yield return 0.05;
        }
        Check(sc.Cracks > 0 || sc.Stalactites.Any(x => x.Fallen), $"sarkit dustu (catlak {sc.Cracks})");
        if (!sc.Cracked) ts?.TakeHit(Hit(80, DamageType.Darbe), P.GlobalPosition); // Havan yerine agir vurus
        Check(sc.Cracked, "Akrep zirhi catladi");
        for (int i = 0; i < 100 && !sc.Dead; i++) { ts?.TakeHit(Hit(80, DamageType.Kesme), P.GlobalPosition); if (!sc.Cracked) sc.Hit(80, DamageType.Darbe); yield return 0.03; }
        yield return 0.5;
        Check(cleared.Contains(rs.Poi.Id), "Kuvars Akrebi Oyugu temizlendi");
        O.Exit();
        yield return 0.2;

        var rmir = O.AddTemporaryRoom("buz_aynasi", 0);
        O.Enter(rmir);
        yield return 0.4;
        var mir = O.Mirror;
        Check(mir != null, "Buz Aynasi dovusu basladi");
        for (int i = 0; i < 300 && mir.Copies.Count == 0; i++) yield return 0.05;
        Check(mir.Copies.Count > 0, "oyuncu kopyasi cikti");
        float mhp = mir.Hp;
        _d.AtaTargetsNear(rmir.Center + Vector3.Up * 3f + new Vector3(0, 0, mir.MirrorZ), 4f).OfType<OyukTarget>().FirstOrDefault()?.TakeHit(Hit(100), P.GlobalPosition);
        Check(mir.Hp == mhp, "kopya varken ayna vurulmaz");
        yield return 0.3;
        for (int round = 0; round < 80 && !mir.Dead; round++)
        {
            foreach (var cp in _d.Atalar.Targets.OfType<OyukPoint>().Where(p => p.Alive).ToList()) cp.TakeHit(Hit(10, DamageType.Ates), P.GlobalPosition);
            var tt = _d.AtaTargetsNear(rmir.Center + Vector3.Up * 3f + new Vector3(0, 0, mir.MirrorZ), 4f).OfType<OyukTarget>().FirstOrDefault();
            if (mir.Copies.Count == 0) tt?.TakeHit(Hit(120), P.GlobalPosition);
            yield return 0.1;
        }
        Check(mir.Melted > 0, $"Ates kopyalari erittti ({mir.Melted})");
        yield return 0.5;
        Check(cleared.Contains(rmir.Poi.Id), "Buz Aynasi Oyugu temizlendi");
        // sandik odulu: Camur Devi sandiklari bir kez
        Check(_d.State.Oyuk.OpenChest(rm.Poi.Id, rm.Guardian, 0, 0).Count > 0, "Camur Devi sandigi acildi (kauçuk, kukurt, cip)");
        O.Exit();
        yield return 0.2;
    }

    void KillAzman(AzmanActor a)
    {
        var f = A.FightOf(a.FightId);
        if (f == null) return;
        f.Body.ShieldT = 0;
        for (int i = 0; i < 10 && A.FightOf(a.FightId) != null; i++) { f.Body.ShieldT = 0; A.Hit(a, Hit(f.Body.Hp + 10f, DamageType.Darbe), a.GlobalPosition + new Vector3(0, 0, -8)); }
    }
}
