using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ana Maya botu, zincir bolumleri (--test=anamaya --anamaya_chain; M4 kabul). Hediyesiz: her esya oyun yolundan gelir
/// (Kazma, Bicme, istasyon kuyrugu, Cekim, E). Bolum 1 (Koca Pofuduk, T1 -> T2), ilk gunun zinciri bitince:
///   1. Hayvan: Pofuduk'u evcillestir ve agila sok (Bot.Hayvan.cs'nin yolu),
///   2. Yun: Pofuduk'un yununu topla (10), Yemlik'i Salgamla doldur,
///   3. Bakir: 40 cevher kaz, Tas Firin'da 20 Bakir Kulce eritip cantaya al (AmMake: tarif yolunu cozer),
///   4. Kabarik: tarladaki kabarik Salgami Cekim'le Ata Tasi'na tasi (5), gecelerde StepNight savunur,
///   5. Adak: Ata Tasi'nda E ile adak sun (Ata gercek adakla gelir; DevSummon yok),
///   6. Dovus: AtaTest'in dovus mantigi (gomulu, bitince geri doner).
/// GEVSETMELER (raporda): zaman TimeScale ile hizlandirilir; tarla ve Yemlik bakimi dogrudan Farm/Ranch kurallariyla
/// (yuruyerek yanina gidilir); yaratiklarla gunduz dovusulmez (can 40 alti yenilenir, sayaci yazilir).
/// </summary>
public partial class Bot
{
    enum AmStage { Hayvan, Yun, Bakir, Kabarik, Adak, Dovus, Bitti }
    AmStage _amStage = AmStage.Hayvan;
    bool _amChainOn;
    float _amNoteT;
    int _amSwollenOffered;
    AtaTest _amFight;
    bool _amFightDone, _amFightOk;
    string _amFightMsg = "";

    /// <summary>Zincir bolumu bitti mi (yuruyus bolumu ona gore baslar).</summary>
    bool AmChainDone => _amStage == AmStage.Bitti;

    void AmNote(string s) { if (_amNoteT <= 0) { GD.Print($"BOT anamaya {s}"); _amNoteT = 5f; } }

    /// <summary>Bir sonraki bolum icin gerekli: tek giris. Her cagri bir kare; bitince true.</summary>
    bool AmChain()
    {
        float dt = (float)GetProcessDeltaTime();
        _amNoteT -= dt;
        _menuT -= dt;
        if (P.Down || P.Hp < 40f) { P.Hp = 100f; _amHeals++; }
        // Gece: Kabarma'yi savun (StepNight), gunduz bolum isi.
        if (_d.Phase == Director.Act.Night || _d.Phase == Director.Act.Dusk && false) { Engine.TimeScale = 1; StepNight(); return false; }
        if (_d.Phase == Director.Act.Dawn) { P.MoveOverride = Vector3.Zero; if (_d.Hud.ReportOpen) { if (_stepT > 1.5f) { CloseReport(); _stepT = 0; } } Engine.TimeScale = 3; return false; }
        Engine.TimeScale = _amStage == AmStage.Dovus ? 1 : 3;
        switch (_amStage)
        {
            case AmStage.Hayvan:
                if (!_hayvanDone) { StepHayvan(); return false; }
                GD.Print("BOT anamaya hayvan tamam (Pofuduk agilda)");
                _amStage = AmStage.Yun; return false;
            case AmStage.Yun: AmYun(); return false;
            case AmStage.Bakir:
                if (AmMake("copper_ingot", 20)) { GD.Print($"BOT anamaya bakir tamam ingots={_d.State.Backpack.Count("copper_ingot")}"); _amStage = AmStage.Kabarik; }
                return false;
            case AmStage.Kabarik: AmKabarik(); return false;
            case AmStage.Adak: AmAdak(); return false;
            case AmStage.Dovus: AmDovus(); return false;
        }
        return true;
    }

    // ---------- 2. Yun ----------

    void AmYun()
    {
        var bag = _d.State.Backpack;
        if (bag.Count("yun") >= 10) { GD.Print($"BOT anamaya yun tamam yun={bag.Count("yun")}"); _amStage = AmStage.Bakir; return; }
        var ag = HayvanPofuduk;
        if (ag == null) { AmNote("yun: Pofuduk yok"); P.MoveOverride = Vector3.Zero; return; }
        var a = ag.Animal;
        AmNote($"yun bekleniyor yun={bag.Count("yun")} pending={a.Pending.Sum(x => x.Count)} yemlik={(FeederFor("turnip") != null)} turnip={bag.Count("turnip")} happy={a.Happiness:0}");
        // Yemlik bos: Hayvan bolumunun doldurma yolu (Salgam hasat + Yemlik).
        if (FeederFor("turnip") == null) { _hayvanDone = false; StepHayvan(); return; }
        if (a.Pending.Count == 0)
        {
            Aim(ag.Host.GlobalPosition + Vector3.Up * 0.5f);
            if (_d.Pens.PenAt(P.GlobalPosition) == null) { var gate = PenGate; if (gate != null) { var (o, i) = GatePoints(gate); if (gate.Open) MoveRouted(i, 0.5f); else if (MoveRouted(o, 0.5f) && _menuT <= 0) { Use(); _menuT = 0.8f; } } return; }
            MoveTo(ag.Host.GlobalPosition, 2.2f, false);
            return;
        }
        Aim(ag.Host.GlobalPosition + Vector3.Up * 0.5f);
        if (MoveTo(ag.Host.GlobalPosition, 1.3f, false) && _menuT <= 0) { Use(); _menuT = 0.6f; }
    }

    // ---------- 3. Tarif yolu: AmMake ----------

    /// <summary>
    /// Cantada n tane `item` olana dek, tarifi cozerek uretir: girdileri (rekursif) toplar ya da uretir, istasyon yoksa uretip
    /// kurar, yakit icin Odun toplar, sirayi E menusuyle doldurur. Ham esya (tarifi olmayan): EnsureItem (Kazma/Bicme).
    /// Kilitli tarif (Ocak basamagi, arastirma) ise engeli yazar (kirilan adim).
    /// </summary>
    bool AmMake(string item, int n)
    {
        var bag = _d.State.Backpack;
        if (bag.Count(item) >= n) return true;
        var r = _d.Data.Tool.RecipeFor(item);
        if (r == null || r.Load) { EnsureItem(item, n); return false; }
        if (!_d.State.Craft.IsUnlocked(r) && !(StationOf(r.Station)?.Queue.IsUnlocked(r) ?? false)) { AmNote($"KILITLI tarif {item}: {r.UnlockOn}"); P.MoveOverride = Vector3.Zero; return false; }
        foreach (var (k, v) in r.In)
            if (bag.Count(k) < v) { AmMake(k, v); return false; }
        var st = StationOf(r.Station);
        if (st == null)
        {
            if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.4f; } return false; }
            if (EnsureCost(r.Station)) PlaceAt(r.Station, _d.OcakPos + new Vector3(-9f - 3f * _amPlaced, 0, -8f));
            if (StationOf(r.Station) != null) _amPlaced++;
            return false;
        }
        int fuel = st.Def.FuelPerJob;
        if (fuel > 0 && st.Def.Fuel.Count > 0 && bag.Count(st.Def.Fuel[0]) < fuel + (r.In.GetValueOrDefault(st.Def.Fuel[0]))) { EnsureItem(st.Def.Fuel[0], fuel + 2); return false; }
        if (st.Queue.Active != null) { WaitAt(st); return false; }
        if (_d.Hud.StationOpen && !_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.4f; } return false; }
        UseStation(st, RecipeIndex(st, item));
        return false;
    }

    int _amPlaced;

    // ---------- 4. Kabarik urun ----------

    void AmKabarik()
    {
        var def = _d.Atalar.Def("koca_pofuduk");
        int have = def.Offering.First(o => o.Swollen).Count - AtaRules.Missing(_d.Atalar.Progress, def, def.Offering.First(o => o.Swollen));
        AmNote($"kabarik sunulan={have}/5 plots={_d.State.Farm.Plots.Count()} swollenPlots={_d.State.Farm.Plots.Count(p => p.Swollen != null)} loose={GetTree().GetNodesInGroup("swollen").Count}");
        if (have >= 5) { _amStage = AmStage.Adak; return; }
        Head(Capa.Head.Cekim);
        var altar = _d.Atalar.AltarPos(def);
        var held = _d.Capa.Held;
        if (held == null)
        {
            var view = _d.FarmView.Views.Where(v => v.Plot.Swollen != null && v.Plot.Planted).OrderBy(v => v.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
            var loose = GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>().Where(s => !s.Sold && !s.Gone && s.CropId == "turnip").OrderBy(s => s.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
            Vector3? target = loose?.GlobalPosition ?? view?.CropTop;
            if (target == null) { AmFarmCare(); return; }
            Aim(_d.World.Ground(target.Value));
            if (MoveTo(target.Value, 4.5f, false) && _stepT > 0.6f) { FirePressed = true; _stepT = 0; }
            return;
        }
        // Tasi: Ata Tasi'nin dibine, birak (AtaSystem._Process sunaga kabul eder).
        var away = P.GlobalPosition - altar; away.Y = 0;
        Aim(altar + Vector3.Up * 0.5f);
        if (MoveTo(altar + away.Normalized() * 3.0f, 0.8f, false) && _stepT > 0.8f) { FirePressed = true; _stepT = 0; }
    }

    /// <summary>Kabarik yoksa tarlayi bakimla: bos kareye Salgam eker, sular (dogrudan Farm kurallari, yanina yuruyerek).</summary>
    void AmFarmCare()
    {
        var farm = _d.State.Farm;
        var bag = _d.State.Backpack;
        P.MoveOverride = Vector3.Zero;
        var plots = farm.Plots.ToList();
        var empty = plots.FirstOrDefault(p => p.Crop == null);
        if (empty != null && bag.Count("turnip_seed") > 0)
        {
            var c = _d.FarmView.CellCenter(empty.X, empty.Y);
            if (MoveTo(c, 2.5f, false)) { farm.Plant(empty.X, empty.Y, "turnip", bag, _d.State.Rng); farm.Water(empty.X, empty.Y); }
            return;
        }
        var dry = plots.FirstOrDefault(p => p.Crop != null && p.Moisture < 40f);
        if (dry != null)
        {
            var c = _d.FarmView.CellCenter(dry.X, dry.Y);
            if (MoveTo(c, 2.5f, false)) farm.Water(dry.X, dry.Y);
            return;
        }
        // Olgun ama kabarmamis ekin: gece sismesini bekle (tarlada dur).
        Aim(_d.FarmCenter() + Vector3.Up);
        MoveTo(_d.FarmCenter() + new Vector3(-2.5f, 0, -2.5f), 1.5f, false);
    }

    // ---------- 5. Adak ----------

    void AmAdak()
    {
        var def = _d.Atalar.Def("koca_pofuduk");
        var sys = _d.Atalar;
        if (sys.Fight != null) { GD.Print("BOT anamaya Ata geldi (gercek adakla)"); _amStage = AmStage.Dovus; return; }
        var altar = sys.AltarPos(def);
        var toArena = (sys.ArenaCenter(def) - altar) with { Y = 0 };
        var spot = altar + toArena.Normalized() * 1.2f;
        Aim(sys.StonePos(def) + Vector3.Up * 2f);
        AmNote($"adak: {sys.OfferingText(def)}");
        if (MoveTo(spot, 1.6f) && _menuT <= 0) { sys.Offer(def); _menuT = 1.5f; }
    }

    // ---------- 6. Dovus ----------

    void AmDovus()
    {
        if (_amFight == null)
        {
            _amFight = new AtaTest { OrderOverride = new[] { "koca_pofuduk" }, StartInFight = true, OnDone = (ok, msg) => { _amFightDone = true; _amFightOk = ok; _amFightMsg = msg; } };
            GetParent().AddChild(_amFight);
            GD.Print("BOT anamaya dovus basladi (AtaTest gomulu)");
        }
        AmNote($"dovus hp={P.Hp:0}");
        if (!_amFightDone) return;
        GD.Print($"BOT anamaya dovus bitti ok={_amFightOk} {_amFightMsg} defeated={_d.Atalar.Progress.IsDefeated("koca_pofuduk")}");
        if (!_amFightOk) { AmFail($"Koca Pofuduk yenilmedi: {_amFightMsg}"); return; }
        _amStage = AmStage.Bitti;
    }
}
