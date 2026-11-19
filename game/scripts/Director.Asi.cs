using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// tarim4 (M4): yemek ozel etkilerinin oyuncuya uygulanmasi (foods.json effects) ve Asi Tezgahi'nin menusu (ICERIK 11:
/// gen aktarma, 200 W). Kural Sim'de (FoodBuffs.Effect, Farm.Graft); burada yalniz baglanti ve menu.
/// Uygulananlar: Dayaniklilik eki, depar bedeli, yenilenme, hiz. Henuz baglanmayanlar: hasar, sicak/soguk/zehir direnci,
/// gece gorusu, tasima, Gosteri, Maya yanigi, ulti dolumu, masa (listesi docs/kimlikler-m4.md degil, notlar tarim4-notlar.md).
/// </summary>
public partial class Director
{
    void ApplyFoodEffects()
    {
        var f = State.Food;
        Player.FoodSpeed = f.Effect("speed");
        var s = State.Stamina;
        s.BonusMax = f.BonusStamina;
        s.DrainMult = Mathf.Max(0.1f, 1f + f.Effect("sprintCost"));
        s.RegenMult = 1f + 0.25f * f.Effect("staminaRegen");
        if (State.Wardrobe.Flag("sprint_free")) s.DrainMult = 0f; // dolap: Kacak
    }

    // ---------- Asi Tezgahi ----------

    /// <summary>Asi Tezgahi (E): once alici tohum kaydi, sonra verici, sonra gen; sonuc cantaya bir asili tohum.</summary>
    public void OpenGraftMenu(Station st)
    {
        var farm = State.Farm;
        var bag = State.Backpack;
        var items = new List<Hud.MenuItem>();
        bool powered = st.Queue.PowerOk == null || st.Queue.PowerOk();
        var lots = farm.Seeds.Lots.Where(l => l.Count > 0 && bag.Count(Data.Crop(l.Crop).SeedId) > 0).ToList();
        foreach (var lot in lots)
        {
            var l = lot;
            items.Add(new Hud.MenuItem(Loc.T("dir.asi_recv_item", ItemNames.Of(Data.Crop(l.Crop).SeedId), l.Genes, l.Count), () => powered, () => OpenGraftDonor(st, l), Data.Crop(l.Crop).SeedId));
        }
        string foot = powered ? Loc.T("dir.asi_foot_ok", Data.Crops.Genetics.Graft.MaxStep)
            : Loc.T("dir.asi_foot_nopower");
        Hud.ShowMenu(st.Def.Name, items, foot + (lots.Count == 0 ? Loc.T("dir.asi_no_seed") : "") + "  ·  " + Loc.T("dir.menu_close"));
    }

    void OpenGraftDonor(Station st, SeedLot recv)
    {
        var farm = State.Farm;
        var items = new List<Hud.MenuItem>();
        var rd = Data.Crop(recv.Crop);
        foreach (var lot in farm.Seeds.Lots.Where(l => !ReferenceEquals(l, recv) && l.Count > 0 && State.Backpack.Count(Data.Crop(l.Crop).SeedId) > (l.Crop == recv.Crop ? 1 : 0)))
        {
            var l = lot;
            if (Data.Crops.Genetics.Graft.SameFamily && Data.Crop(l.Crop).Family != rd.Family) continue;
            items.Add(new Hud.MenuItem(Loc.T("dir.asi_donor_item", ItemNames.Of(Data.Crop(l.Crop).SeedId), l.Genes, l.Count), () => true, () => OpenGraftGene(st, recv, l), Data.Crop(l.Crop).SeedId));
        }
        Hud.ShowMenu(Loc.T("dir.asi_donor_title"), items, Loc.T("dir.asi_recv_line", ItemNames.Of(rd.SeedId), recv.Genes) + (items.Count == 0 ? Loc.T("dir.asi_no_donor") : ""));
    }

    void OpenGraftGene(Station st, SeedLot recv, SeedLot donor)
    {
        var items = new List<Hud.MenuItem>();
        for (int g = 0; g < Genes.Count; g++)
        {
            int gi = g;
            items.Add(new Hud.MenuItem(Loc.T("dir.asi_gene_item", Genes.Letters[g], recv.Genes[g], donor.Genes[g]), () => donor.Genes[gi] > recv.Genes[gi], () => DoGraft(st, recv, donor, gi)));
        }
        Hud.ShowMenu(Loc.T("dir.asi_gene_title"), items, $"{recv.Genes}  ←  {donor.Genes}");
    }

    /// <summary>Aşıyı yapar (menu ve test ortak): sonuc mesaji ekrana, durum dondurulur.</summary>
    public GraftResult DoGraft(Station st, SeedLot recv, SeedLot donor, int gene)
    {
        // ortak oyun: istemcide istek host'a gider (host aktorun cantasiyla ve tezgah erisimiyle dogrular; NetSync.Stations.cs HostGraft)
        if (Net?.Forward("graft", w =>
        {
            foreach (var lot in new[] { recv, donor }) { w.Str(lot.Crop); w.U8((byte)lot.Genes.V); w.U8((byte)lot.Genes.H); w.U8((byte)lot.Genes.D); w.U8((byte)lot.Genes.M); }
            w.U8((byte)gene);
        }) == true) { Hud.HideMenu(); return GraftResult.Ok; }
        var r = State.Farm.Graft(State.Backpack, recv, donor, gene, out var res);
        if (r == GraftResult.Ok)
        {
            Sfx.Play("place", st.GlobalPosition, -4f);
            Hud.Hint(Loc.T("dir.asi_done", res!.Genes), 2f);
            GD.Print($"GRAFT {res.Crop} {res.Genes}");
            Hud.HideMenu();
        }
        else Hud.Hint(r switch { GraftResult.FamilyMismatch => Loc.T("dir.asi_err_family"), GraftResult.NoGain => Loc.T("dir.asi_err_gain"), _ => Loc.T("dir.asi_err_none") }, 1.5f);
        return r;
    }
}