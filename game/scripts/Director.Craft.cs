using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: Uretim (GDD 5 "Uretim paneli", URETIM 6 M1 karari 23; Rust mantigi). El tarifleri her yerde el kuyruguna
/// (State.Craft) girer; istasyon tarifleri yalniz o istasyonun yakininda, o istasyonun sirasina (Station.Queue: yakit, yuk
/// urunu, gorunus). Iptal malzemeyi geri verir. Eksik malzemenin kaynagi pusulada gosterilir (Director.Compass.cs).
/// Panel: Hud.Craft.cs. Uretim Capa'dan yapilmaz.
/// </summary>
public partial class Director
{
    bool _craftWired;

    /// <summary>Oyuncunun yakinindaki istasyonlarin turleri (craft.json stationReach).</summary>
    public HashSet<string> NearStations()
    {
        var p = Player.GlobalPosition;
        return Stations.Where(s => IsInstanceValid(s) && s.InReach(p)).Select(s => s.Def.Station).Where(x => x != null).ToHashSet();
    }

    /// <summary>Yakindaki en yakin o turden istasyon; yoksa null.</summary>
    public Station NearStation(string station)
    {
        var p = Player.GlobalPosition;
        return Stations.Where(s => IsInstanceValid(s) && s.Def.Station == station && s.InReach(p)).OrderBy(s => s.GlobalPosition.DistanceTo(p)).FirstOrDefault();
    }

    /// <summary>Tarifi bu adette siraya koyar. Olmazsa nedenini ipucuyla soyler ve false doner.</summary>
    public bool Craft(RecipeDef r, int count)
    {
        count = Mathf.Max(1, count);
        if (TryResearch(r)) return !ResearchLocked(r); // zincir: arastirmayla kilitli satir Masa yaninda arastirilir (Director.Zincir.cs)
        IItemStore inv = StoreFor(r); // zincir: istasyon tarifi canta + tapudaki Sandiklar (GDD 5 ambardan uretim)
        string need = CraftRules.NeedStation(r, NearStations());
        if (need != null) { Hud.Hint(Loc.T("dir.hint_need_nearby", ItemNames.Of(need)), 1.5f); Sfx.Play("hit", null, -12f); return false; }
        var miss = CraftRules.Missing(r, count, inv);
        if (miss.Count > 0)
        {
            var (item, have, n) = miss[0];
            Hud.Hint(Loc.T("dir.hint_not_enough", ItemNames.Of(item), have, n), 1.5f);
            Sfx.Play("hit", null, -12f);
            return false;
        }
        if (Net?.ForwardCraft(r, count) == true) return true; // ortak: istemcide host'a gider (NetSync.Stations.cs)
        if (CraftRules.IsHand(r))
        {
            if (!State.Craft.Enqueue(r, count, inv)) { Hud.Hint(!State.Craft.IsUnlocked(r) ? Loc.T("dir.hint_recipe_locked") : Loc.T("dir.hint_queue_full"), 1.5f); Sfx.Play("hit", null, -12f); return false; }
        }
        else
        {
            var st = NearStation(r.Station);
            int queued = 0;
            for (int i = 0; i < count; i++) if (st != null && st.Queue.Enqueue(r, inv)) queued++; else break;
            if (queued == 0)
            {
                Hud.Hint(st == null ? Loc.T("dir.hint_need_station", ItemNames.Of(r.Station)) : !st.Queue.IsUnlocked(r) ? LockText(r) : !st.Queue.HasFuel(r, inv) ? Loc.T("dir.hint_no_fuel", ItemNames.Of(st.Queue.FuelFor(r, inv) ?? "wood")) : Loc.T("dir.hint_station_queue_full", st.Def.Name), 1.6f);
                Sfx.Play("hit", null, -12f);
                return false;
            }
        }
        Sfx.Play("place", null, -6f);
        GD.Print($"QUEUED {r.Id} x{count}");
        return true;
    }

    /// <summary>El kuyrugundaki isi iptal eder; malzeme geri doner.</summary>
    public void CancelCraft(int index)
    {
        var jobs = State.Craft.Jobs;
        if (index < 0 || index >= jobs.Count) return;
        var name = jobs[index].Recipe.Name;
        if (Net?.ForwardCraftCancel(index) == true) return;
        var back = State.Craft.Cancel(index, State.Backpack);
        Hud.Hint(Loc.T("dir.hint_cancelled_refund", name), 1.4f);
        Sfx.Play("place", null, -10f);
        GD.Print($"CANCELLED {name} back={string.Join(",", back.Select(kv => kv.Key + ":" + kv.Value))}");
    }

    /// <summary>Istasyonun sirasini iptal eder; malzeme ve yakit geri doner.</summary>
    public void CancelStation(Station st)
    {
        if (st == null || st.Queue.Active == null) return;
        var name = st.Queue.Active.Name;
        if (Net?.ForwardStationCancel(st) == true) return;
        st.Queue.Cancel(StationStore()); // zincir: malzeme cantaya, sigmazsa Sandiklara
        Hud.Hint(Loc.T("dir.hint_cancelled_refund", name), 1.4f);
        Sfx.Play("place", null, -10f);
    }

    void TickCraft(float dt)
    {
        if (!_craftWired)
        {
            _craftWired = true;
            State.Craft.Produced += (r, n) =>
            {
                Fx.Text3D(Player.GlobalPosition + Vector3.Up * 2.1f, $"+{n} {r.Name}", Palette.Warning, 44, 1.0f, 1.2f);
                Sfx.Play("pickup", null, -2f);
                OnProduced(r); // Director.Menus.cs: Events.RaiseProduced, PRODUCED satiri
            };
        }
        if (NetClient) return; // ortak: el kuyrugu host'ta isler, "craft" bolumuyle doner
        State.Craft.Tick(dt, State.Backpack);
    }
}
