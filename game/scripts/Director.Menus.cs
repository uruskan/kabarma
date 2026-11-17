using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Director: istasyonlar (kurma, uretim), menuler (istasyon, canta, kemer) ve Kargo Rampasi satisi.</summary>
public partial class Director
{
    public Station PlaceStation(PlaceableDef def, Vector3 at, float yaw)
    {
        var st = new Station(this, def);
        AddChild(st);
        st.GlobalPosition = at;
        st.Rotation = new Vector3(0, yaw, 0);
        Stations.Add(st);
        Sfx.Play("place", at, 2f);
        Fx.Burst(at + Vector3.Up * 0.3f, Palette.Ground, 26, 5f, 0.12f, 0.8f);
        GD.Print($"PLACED {def.Id} at {at}");
        Events.RaiseStationPlaced(def.Id);
        return st;
    }

    public void OnProduced(RecipeDef r)
    {
        Events.RaiseProduced(r.Id, r.Out);
        GD.Print($"PRODUCED {r.Id} civi={State.Backpack.Count("civi")} soup={State.Backpack.Count("salgam_corbasi")} crates={State.Backpack.Count("salgam_kasasi")}");
    }

    // ---------- Menuler (istasyon, canta, satis) ----------

    public bool MenuOpen => Hud.MenuOpen;

    public void OpenStationMenu(Station st)
    {
        var items = new System.Collections.Generic.List<Hud.MenuItem>();
        foreach (var r in st.Queue.Recipes)
        {
            var rr = r;
            string cost = string.Join(" + ", r.In.Select(kv => $"{kv.Value} {ItemNames.Of(kv.Key)}"));
            items.Add(new Hud.MenuItem($"{r.Name} ×{r.Out}   ←   {cost}   ({r.Seconds:0.#} sn)", () => StationQueue.CanAfford(rr, State.Backpack), () =>
            {
                if (st.Queue.Enqueue(rr, State.Backpack)) { Sfx.Play("place", st.GlobalPosition, -4f); GD.Print($"QUEUED {rr.Id}"); }
                else Hud.Hint("Malzeme yetmiyor ya da istasyon başka bir şey üretiyor", 1.5f);
            }));
        }
        Hud.ShowMenu(st.Def.Name, items, "1–9: sıraya ekle  ·  E / Esc: kapat");
    }

    /// <summary>Kemer yuvasini kullanir: yemekse yer, degilse ne oldugunu soyler.</summary>
    public void UseBelt(int slot)
    {
        var id = State.Belt.At(slot);
        if (id == null) return;
        Hud.FlashBelt(slot);
        var food = Data.Tool.Foods.FirstOrDefault(f => f.Id == id);
        if (food != null) { EatFood(food); return; }
        if (id == "turnip_seed") { Capa.Select(Capa.Head.Toprak); Hud.Hint("Toprak başlığı: sürülmüş kareye ek", 1.2f); return; }
        if (id == "civi") { Capa.Select(Capa.Head.Civi); return; }
        Hud.Hint($"{ItemNames.Of(id)} ×{State.Backpack.Count(id)}", 1f);
    }

    public bool EatFood(FoodDef ff)
    {
        if (State.Backpack.Count(ff.Id) <= 0) { Hud.Hint($"{ff.Name} yok", 1f); return false; }
        if (!State.Food.Eat(ff)) { Hud.Hint("Üç yemek yuvası dolu", 1.2f); return false; }
        State.Backpack.Remove(ff.Id, 1);
        Sfx.Play("pickup", null, -2f);
        Hud.Hint($"{ff.Name}: doydun, güçlendin", 1.5f);
        GD.Print($"ATE {ff.Id}");
        Events.RaiseAte(ff.Id);
        return true;
    }

    public void OpenBagMenu()
    {
        var items = new System.Collections.Generic.List<Hud.MenuItem>();
        foreach (var f in Data.Tool.Foods)
        {
            var ff = f;
            items.Add(new Hud.MenuItem($"Ye: {f.Name}  (+{f.MaxHp:0} can, +{f.Regen:0.#}/sn, {f.Seconds / 60f:0} dk)  ·  elde {State.Backpack.Count(f.Id)}", () => State.Backpack.Count(ff.Id) > 0, () => EatFood(ff)));
        }
        foreach (var g in State.Backpack.Contents.Select(c => c.id).Distinct().Take(8 - items.Count))
        {
            var id = g;
            items.Add(new Hud.MenuItem($"{ItemNames.Of(id)} ×{State.Backpack.Count(id)}   ·   {(State.Belt.Contains(id) ? "kemerde (çıkar)" : "kemere tak")}", () => true, () => { State.Belt.Toggle(id); OpenBagMenu(); }));
        }
        var lines = State.Backpack.Contents.GroupBy(c => c.id).Select(g => $"{ItemNames.Of(g.Key)} {g.Sum(c => c.count)}");
        Hud.ShowMenu($"Sırt Çantası · {State.Backpack.UsedSlots}/{State.Backpack.Slots} yuva", items, string.Join("   ·   ", lines) + $"\nYemek yuvaları: {string.Join(", ", State.Food.Active.Select(a => $"{a.food.Name} {a.left / 60f:0.0} dk"))}");
    }

    public void OpenSellMenu()
    {
        var items = new System.Collections.Generic.List<Hud.MenuItem>();
        foreach (var kv in Data.Tool.Prices)
        {
            var id = kv.Key;
            int have = id == "egg" ? State.Eggs : State.Backpack.Count(id);
            items.Add(new Hud.MenuItem($"Sat: {ItemNames.Of(id)} ×{have}   ({kv.Value} Kredi/adet)", () => (id == "egg" ? State.Eggs : State.Backpack.Count(id)) > 0, () =>
            {
                int n = id == "egg" ? State.Eggs : State.Backpack.Count(id);
                int v = State.SellItem(id, n);
                if (v > 0) { Events.RaiseSold(id, n, v); Pod.Launch(); Sfx.Play("rocket", Pod.PadCenter, 0f); Hud.Big($"+{v} Kredi", Palette.Warning, 2.5f); GD.Print($"SOLD {id} x{n} +{v}"); OpenSellMenu(); }
            }));
        }
        Hud.ShowMenu("Kargo Rampası · Kooperatif alım fiyatları", items, "Kabarık ürünleri Çekim başlığıyla rampaya taşı.  ·  E / Esc: kapat");
    }

    public void PlaceOcak(Vector3 at)
    {
        Ocak = new Ocak { Name = "Ocak" };
        AddChild(Ocak);
        Ocak.GlobalPosition = at;
        Player.RespawnPoint = at + new Vector3(0, 0.3f, -2.5f);
        Sfx.Play("place", at, 2f);
        Fx.Burst(at + Vector3.Up * 0.3f, Palette.Ground, 30, 5f, 0.12f, 0.8f);
        Fx.Burst(at + Vector3.Up * 1f, Palette.Warning, 16, 3f, 0.1f, 1f, true, -1f);
        Events.RaiseStationPlaced("ocak");
    }

    // ---------- Satis ----------

    public void SellCrop(SwollenCrop s)
    {
        if (s.Sold) return;
        s.Sold = true;
        int v = State.Sell(s.CropId, s.Size);
        var tw = s.CreateTween();
        tw.TweenProperty(s, "global_position", Pod.PadCenter + Vector3.Up * s.Radius, 0.3f);
        tw.TweenProperty(s, "scale", Vector3.One * 0.01f, 0.4f).SetDelay(0.2f);
        tw.TweenCallback(Callable.From(s.QueueFree));
        Pod.Launch();
        Sfx.Play("rocket", Pod.PadCenter, 2f);
        Hud.Big($"+{v} Kredi", Palette.Warning, 3.5f);
        Fx.Burst(Pod.PadCenter + Vector3.Up, Palette.Warning, 40, 8f, 0.12f, 1.2f, true, 6f);
        GD.Print($"SOLD {s.CropId} {s.Size.Id} +{v} credits={State.Credits}");
        Events.RaiseCropShipped(s.CropId, s.Size.Id, v);
    }
}
