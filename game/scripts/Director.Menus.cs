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
        GD.Print($"PRODUCED {r.Id} civi={State.Backpack.Count("civi")} ingot={State.Backpack.Count("copper_ingot")} soup={State.Backpack.Count("sebze_corbasi")}");
    }

    // ---------- Menuler (istasyon, canta, satis) ----------

    public bool MenuOpen => Hud.MenuOpen;

    /// <summary>Istasyon menusu: tarifler veri sirasiyla (kilitli tarif soluk). Paketleme Tezgahi'nin kendi menusu var (kasa doldurma).</summary>
    public void OpenStationMenu(Station st)
    {
        if (st.Def.Station == "paketleme") { OpenPackingMenu(st); return; }
        var items = new System.Collections.Generic.List<Hud.MenuItem>();
        var def = st.Def;
        string fuelText = def.FuelPerJob > 0 && def.Fuel.Count > 0 ? $" + {def.FuelPerJob} {ItemNames.Of(def.Fuel[0])} (yakıt)" : "";
        foreach (var r in st.Queue.Recipes)
        {
            var rr = r;
            string cost = r.In.Count == 0 ? "—" : string.Join(" + ", r.In.Select(kv => $"{kv.Value} {ItemNames.Of(kv.Key)}"));
            string label = st.Queue.IsUnlocked(r)
                ? $"{r.Name} ×{r.Out}   ←   {cost}{fuelText}   ({r.Seconds:0.#} sn){(r.Load ? "  · dünyaya çıkar" : "")}"
                : $"{r.Name}   (kilitli: ilk {ItemNames.Of(r.UnlockOn)} ile açılır)";
            items.Add(new Hud.MenuItem(label, () => st.Queue.CanMake(rr, State.Backpack), () =>
            {
                if (st.Queue.Enqueue(rr, State.Backpack)) { Sfx.Play("place", st.GlobalPosition, -4f); GD.Print($"QUEUED {rr.Id}"); }
                else Hud.Hint(!st.Queue.IsUnlocked(rr) ? "Tarif kilitli" : !st.Queue.HasFuel(rr, State.Backpack) ? "Yakıt yok: Odun" : "Malzeme yetmiyor ya da istasyon başka bir şey üretiyor", 1.5f);
            }));
        }
        Hud.ShowMenu(st.Def.Name, items, "{act:menu_pick}: sıraya ekle  ·  {act:interact} / {act:ui_cancel}: kapat");
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

    /// <summary>Tab: envanter izgarasi (Hud.Inventory.cs; canta, kemer, 3 yemek yuvasi; ikinci sayfa Defter).</summary>
    public void OpenBagMenu() => Hud.OpenInventory();

    /// <summary>Kargo Rampasi (rampanin dibinde E). Mal cantadan satilmaz: kasayla ya da kabarik urun olarak gider (GDD 13).</summary>
    public void OpenSellMenu() => OpenRampMenu(); // Director.Economy.cs

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

    /// <summary>
    /// Cekim'le rampaya birakilan yuk nesnesi (Capa.Cekim). Kargo kasasi rampaya yuklenir (E ile gonderilir);
    /// kabarik urun bos rampadan tek basina hemen firlatilir. Rampada kasa varken kabarik urun alinmaz (GDD 13).
    /// </summary>
    public void SellCrop(SwollenCrop s)
    {
        if (s.Sold) return;
        if (s is CargoCrateNode crate) { LoadCrateOnRamp(crate); return; }
        if (!State.Cargo.CanShipSwollen)
        {
            Hud.Hint("Rampada kasa var: önce kasaları gönder", 2f);
            var g = World.Ground(Pod.PadCenter + (Player.GlobalPosition - Pod.PadCenter).Normalized() * 3.5f);
            s.CreateTween().TweenProperty(s, "global_position", g + Vector3.Up * s.Radius * 0.6f, 0.35f);
            return;
        }
        bool instant = !State.Cargo.FirstSaleDone && Data.Economy.Ramp.FirstSaleInstant;
        s.Sold = true;
        int v = State.Sell(s.CropId, s.Size);
        var tw = s.CreateTween();
        tw.TweenProperty(s, "global_position", Pod.PadCenter + Vector3.Up * s.Radius, 0.3f);
        tw.TweenProperty(s, "scale", Vector3.One * 0.01f, 0.4f).SetDelay(0.2f);
        tw.TweenCallback(Callable.From(s.QueueFree));
        Pod.Launch();
        Sfx.Play("rocket", Pod.PadCenter, 2f);
        if (!instant) Hud.Big($"Gönderildi: {v} Kredi ({Data.Economy.Ramp.PayoutSeconds:0} sn)", Palette.Warning, 3.5f);
        Fx.Burst(Pod.PadCenter + Vector3.Up, Palette.Warning, 40, 8f, 0.12f, 1.2f, true, 6f);
        GD.Print($"SOLD {s.CropId} {s.Size.Id} +{v} credits={State.Credits} instant={instant}");
        Events.RaiseCropShipped(s.CropId, s.Size.Id, v);
    }
}
