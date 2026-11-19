using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Zincir sinamasi, M4 (zincir4): T3 ve T5 urunu. M2 botu bittikten sonra ayni oyunda: istasyonlar Tezgah'ta gercek tarifle
/// uretilir (kuyruk, sure, bedel), kurulur, arastirma Arastirma Masasi kuralindan (Research.Research) gecer, urun istasyon
/// kuyrugundan cikar. DURUST: T3+ ham kaynaklarinin dugumlerini biyom akisi daha kurmadi, Ata ve Kabarma yolu bu sinamanin
/// disinda; bu adimlar "GEVSETILDI" diye gunluge ve rapora yazilir (Relax). Gevsetilenler: ham/ara esya verilir (Sandiga
/// konur, ambardan uretim yoluyla cekilir), Ocak basamagi dogrudan yukseltilir (Ata yerine), Veri verilir (tarama yerine),
/// guc kurali kapali (PowerOk=true; guc kurali Sim testinde ve guc akisinda).
/// </summary>
public partial class ZincirTest
{
    readonly List<string> _relaxed = new();
    readonly List<Inventory> _stock = new();
    float _phaseT;
    int _slot;

    void Relax(string what)
    {
        if (_relaxed.Contains(what)) return;
        _relaxed.Add(what);
        Log("GEVSETILDI " + what);
    }

    /// <summary>Esyayi bir Sandiga koyar (ambardan uretim bunu ceker).</summary>
    void Give(string item, int n, string why)
    {
        Relax($"{item} verildi ({why})");
        foreach (var c in _stock)
        {
            int put = c.Add(item, n);
            n -= put;
            if (n <= 0) return;
        }
        var f = _d.PlaceFurniture(_d.Data.Tool.Placeables.Single(p => p.Id == "sandik"), _d.World.Ground(_d.OcakPos + new Vector3(-3f - _stock.Count * 1.4f, 0, 16f)), 0);
        _stock.Add(f.Storage);
        Give(item, n, why);
    }

    async Task<bool> Until(Func<bool> cond, float seconds)
    {
        _phaseT = 0;
        while (!cond())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _phaseT += (float)GetProcessDeltaTime();
            if (_phaseT > seconds) return cond();
        }
        return true;
    }

    /// <summary>Tarifi istasyonunun kuyruguna koyar (bedel depodan) ve urun gelene dek bekler.</summary>
    async Task<bool> Craft(string recipeId, int count = 1)
    {
        var r = _d.Data.Tool.RecipeFor(recipeId)!;
        var st = _d.Stations.FirstOrDefault(s => s.Def.Station == r.Station);
        if (st == null) { Log($"FAIL {r.Station} kurulu degil ({recipeId})"); return false; }
        int before = _d.StationStore().Count(recipeId);
        for (int i = 0; i < count; i++)
        {
            if (!await Until(() => st.Queue.Enqueue(r, _d.StationStore()), 20f)) { Log($"FAIL {recipeId}: kuyruga girmedi (kilit/malzeme/yakit/sira)"); return false; }
        }
        float secs = r.Seconds * count * 1.5f + 30f; // oyun saniyesi (--fast olceklenmis delta)
        bool ok = await Until(() =>
        {
            if (st.Queue.Watts > 0) st.Queue.PowerOk = () => true; // GEVSETILDI guc (PowerSystem ucu sonradan baglayabilir)
            return _d.StationStore().Count(recipeId) >= before + count * r.Out;
        }, secs);
        if (!ok) Log($"  {recipeId}: kuyruk durumu aktif={st.Queue.Active?.Id} kalan={st.Queue.QueuedTotal} ilerleme={st.Queue.Progress:0.00} bekliyor={st.Queue.Blocked} depoda={_d.StationStore().Count(recipeId)}/{before + count * r.Out} istasyon_gecerli={IsInstanceValid(st)}");
        return ok;
    }

    /// <summary>Istasyonu Tezgah'ta uretip kurar (yoksa); guc isteyense PowerOk acilir (GEVSETILDI).</summary>
    async Task<Station> Ensure(string id)
    {
        var have = _d.Stations.FirstOrDefault(s => s.Def.Id == id);
        if (have != null) return have;
        if (!await Craft(id)) return null;
        _d.StationStore().Remove(id, 1);
        var def = _d.Data.Tool.Placeables.Single(p => p.Id == id);
        var st = _d.PlaceStation(def, _d.World.Ground(_d.OcakPos + new Vector3(12f + 4f * _slot++, 0, -8f)), 0);
        for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (st.Queue.Watts > 0)
        {
            Relax("guc kurali kapali (PowerOk=true; watt kurali Sim testinde, kablolu besleme guc akisinda)");
            st.Queue.PowerOk = () => true;
        }
        return st;
    }

    async Task Zincir4Phase()
    {
        var s = _d.State;
        var rs = s.Research;
        var rec = (string id) => _d.Data.Tool.RecipeFor(id)!;
        Log("--- zincir4: T3 ve T5 urunu ---");

        // Kilitler gercekten kilitli mi.
        Check(!s.QueueFor("tezgah").IsUnlocked(rec("yuksek_firin")), "Yuksek Firin tarifi Ocak T3 gelmeden kilitli");
        Check(!s.QueueFor("tezgah").IsUnlocked(rec("pres")), "Pres tarifi arastirilmadan kilitli");

        // ---- T3 ----
        Relax("Ocak basamagi dogrudan yukseltildi (Ata yolu: boss akisi)");
        rs.SetOcakTier(3);
        Relax("Veri verildi (tarama ve is emri yolu M2 testinde)");
        rs.AddVeri(2000, "test");
        Check(s.QueueFor("tezgah").IsUnlocked(rec("yuksek_firin")), "Ocak T3: Yuksek Firin tarifi acildi");
        Give("brick", 30, "T1 urunu"); Give("iron_ingot", 15, "T2 urunu"); Give("clay", 10, "T1 kaynagi");
        var firin = await Ensure("yuksek_firin");
        Check(firin != null, "T3: Yuksek Firin Tezgah'ta uretildi ve kuruldu");
        if (firin == null) return;
        Give("iron_ingot", 20, "T2 urunu"); Give("charcoal", 30, "T1 urunu (girdi + yakit)");
        Check(await Craft("celik", 10), "T3 urunu: 10 Celik (Yuksek Firin kuyrugu, yakitli)");

        Check(rs.Check("pres", true) == ResearchBlock.Ok, "Pres arastirilabilir (Masa yaninda, Veri yeterli)");
        Check(rs.Research("pres", true) && rs.IsDone("pres"), "Pres arastirildi (Arastirma Masasi kurali)");
        Give("plank", 10, "T2 urunu");
        var pres = await Ensure("pres");
        Check(pres != null, "T3: Pres uretildi ve kuruldu (10 Celik + 10 Kereste)");
        if (pres == null) return;
        Give("kaucuk_sutu", 2, "Sazlik kaynagi: biyom dugumu henuz yok"); Give("kukurt", 1, "Sazlik kaynagi: biyom dugumu henuz yok");
        Check(await Craft("kaucuk"), "T3 urunu: Kaucuk (Pres)");

        // ---- T5 ----
        Relax("Ocak basamagi dogrudan yukseltildi (Ata yolu: boss akisi)");
        rs.SetOcakTier(5);
        bool chain = true;
        foreach (var n in new[] { "kimya_tezgahi", "elektronik_tezgahi", "rafineri", "batarya_tezgahi" }) chain &= rs.Research(n, true);
        Check(chain && rs.IsDone("batarya_tezgahi"), "T5: arastirma zinciri (Kimya, Elektronik, Rafineri, Batarya) Veri ile gecti");
        Give("titan_ingot", 40, "T4 urunu (Titan Cevheri Kanyon'da)"); Give("devre", 20, "T4 urunu"); Give("plastik", 11, "T4 urunu"); Give("cam", 10, "T4 urunu");
        var bat = await Ensure("batarya_tezgahi");
        Check(bat != null, "T5: Batarya Tezgahi uretildi ve kuruldu (20 Titan + 10 Devre + 10 Plastik)");
        if (bat == null) return;
        Give("ayaz_kristali", 3, "Ayaz kaynagi: biyom dugumu henuz yok"); Give("tuz", 1, "Kanyon kaynagi: biyom dugumu henuz yok");
        Check(await Craft("aku_hucresi"), "T5 urunu: Aku Hucresi (Batarya Tezgahi)");
        var kes = await Ensure("kristal_kesici");
        Check(kes != null, "T5: Kristal Kesici uretildi ve kuruldu (Ocak T5 ile)");
        if (kes == null) return;
        Check(await Craft("kristal_lens"), "T5 urunu: Kristal Lens (Kristal Kesici)");
        Check(_d.StationStore().Count("kaucuk") >= 1 && _d.StationStore().Count("aku_hucresi") >= 1 && _d.StationStore().Count("kristal_lens") >= 1, "urunler depoda (Kaucuk, Aku Hucresi, Kristal Lens; 10 Celik Pres'in bedeli oldu)");
        Log($"GEVSETILEN ADIMLAR ({_relaxed.Count}): " + string.Join("; ", _relaxed));
    }
}
