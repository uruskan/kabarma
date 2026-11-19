using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Arac menusu ve eylemleri (ICERIK §14): bin, yakit doldur (Bitkisel Yag, Biyoyakit, Aku), eklenti tak/cikar, isik, kasa
/// yukle/indir (Kamyon, Romork, Vinç; Zeplin altina asar), su doldur, hangara park. Her eylem tek giris
/// <see cref="ApplyVehicleAct"/> uzerinden gecer: tek oyuncuda dogrudan, ortak oyunda istemci host'a gonderir
/// (host aktorun cantasindan ve konumundan dogrular; Director.Vehicles.Net.cs).
/// </summary>
public partial class Director
{
    public void OpenVehicleMenu(VehicleNode v)
    {
        var d = v.Def; var s = v.State; var f = Data.Vehicles;
        var items = new List<Hud.MenuItem>();
        string seat = s.Riders.Count == 0 ? Loc.T("dir.seat_driver") : Loc.T("dir.seat_passenger");
        items.Add(new Hud.MenuItem(s.Flipped ? Loc.T("dir.veh_right") : Loc.T("dir.veh_board", seat), () => !(s.Riders.Count >= d.Seats && !s.Flipped), () => { Hud.HideMenu(); BoardVehicle(v); }));
        // Yakit: cantadaki her kabul edilen yakit icin.
        foreach (var (fuel, mult) in d.Fuels.Count > 0 ? d.Fuels : new Dictionary<string, float> { [d.FuelItem] = 1f })
        {
            int have = Bag.Count(fuel);
            string fuelId = fuel;
            items.Add(new Hud.MenuItem(Loc.T("dir.veh_refuel", ItemNames.Of(fuel), have) + (mult != 1 ? Loc.T("dir.veh_refuel_eff", mult.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)) : ""),
                () => have > 0 && Kabarma.Sim.Vehicles.RoomFor(d, s) > 0, () => { DoVehicleAct(v, "fuel", fuelId); OpenVehicleMenu(v); }));
        }
        // Eklentiler: cantadakiler (uyanlar), takililar.
        foreach (var a in f.Attachments.Where(a => Bag.Count(a.Id) > 0 && Kabarma.Sim.Vehicles.Fits(a, d)))
        {
            string id = a.Id;
            items.Add(new Hud.MenuItem(Loc.T("dir.veh_attach", a.Name), () => Kabarma.Sim.Vehicles.FreeMounts(d, s, f, a.Mount) > 0 && !s.Attachments.Contains(id),
                () => { DoVehicleAct(v, "attach", id); OpenVehicleMenu(v); }));
        }
        foreach (var id in s.Attachments.ToList())
        {
            string aid = id;
            items.Add(new Hud.MenuItem(Loc.T("dir.veh_detach", f.Attachment(aid)?.Name ?? aid), () => true, () => { DoVehicleAct(v, "detach", aid); OpenVehicleMenu(v); }));
        }
        if (v.Mods.HasLight) items.Add(new Hud.MenuItem(s.Light ? Loc.T("dir.veh_light_off") : Loc.T("dir.veh_light_on"), () => true, () => { DoVehicleAct(v, "light", ""); OpenVehicleMenu(v); }));
        int cap = Kabarma.Sim.Vehicles.CrateCapacity(d, s, f);
        if (cap > 0)
        {
            float reach = v.HasAttachment("vinc") ? 6f : 3.5f;
            int near = CrateNodesNear(v.GlobalPosition, reach).Count(n => !s.Crates.Contains(n.Crate.Id));
            items.Add(new Hud.MenuItem(Loc.T("dir.veh_load_crates", near, s.Crates.Count, cap), () => near > 0 && s.Crates.Count < cap, () => { DoVehicleAct(v, "loadcrates", ""); OpenVehicleMenu(v); }));
            items.Add(new Hud.MenuItem(Loc.T("dir.veh_unload_crates", s.Crates.Count), () => s.Crates.Count > 0, () => { DoVehicleAct(v, "unloadcrates", ""); OpenVehicleMenu(v); }));
        }
        if (v.HasAttachment("su_tankeri"))
            items.Add(new Hud.MenuItem(Loc.T("dir.veh_fill_water", s.Water, (int)f.Attachment("su_tankeri")!.Params["waterUnits"]), () => World.IsWater(World.Ground(v.GlobalPosition + v.GlobalBasis.Z * -3f)) || World.IsWater(World.Ground(v.GlobalPosition)) || NearWater(v.GlobalPosition, 8f),
                () => { DoVehicleAct(v, "water", ""); OpenVehicleMenu(v); }));
        if (d.Kind == "air")
            items.Add(new Hud.MenuItem(s.Parked ? Loc.T("dir.veh_parked") : Loc.T("dir.veh_park"), () => !s.Parked && s.Altitude < 2.5f, () => { DoVehicleAct(v, "park", ""); OpenVehicleMenu(v); }));
        string info = Loc.T("dir.veh_info", (Kabarma.Sim.Vehicles.TankSeconds(d) > 0 ? s.Fuel / Kabarma.Sim.Vehicles.TankSeconds(d) * 100 : 0).ToString("0", System.Globalization.CultureInfo.InvariantCulture), s.Hp.ToString("0", System.Globalization.CultureInfo.InvariantCulture), d.Hp.ToString("0", System.Globalization.CultureInfo.InvariantCulture), s.Riders.Count, d.Seats)
            + (cap > 0 ? Loc.T("dir.veh_info_crates", s.Crates.Count, cap) : "") + (s.Attachments.Count > 0 ? Loc.T("dir.veh_info_attach", string.Join(", ", s.Attachments.Select(x => f.Attachment(x)?.Name ?? x))) : "")
            + Loc.T("dir.veh_info_mounts", Kabarma.Sim.Vehicles.FreeMounts(d, s, f, "hitch"), d.Hitches, Kabarma.Sim.Vehicles.FreeMounts(d, s, f, "body"), d.BodyMounts)
            + "\n" + Loc.T("dir.menu_close");
        Hud.ShowMenu($"{d.Name} · T{d.Tier}", items, info);
    }

    bool NearWater(Vector3 p, float r)
    {
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.Tau / 12f;
            if (World.IsWater(World.Ground(p + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)))) return true;
        }
        return false;
    }

    Inventory Bag => State.Backpack;

    IEnumerable<CargoCrateNode> CrateNodesNear(Vector3 at, float reach) =>
        GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().Where(n => IsInstanceValid(n) && n.OnCart == null && !n.OnRamp && !n.Held && n.GlobalPosition.DistanceTo(at) <= reach + 1f);

    CargoCrateNode CrateNode(int id) => GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().FirstOrDefault(n => n.Crate.Id == id);

    /// <summary>Kasa aracin ustunde (kamyon yatagi, romork, Zeplin altinda); dugum aracin cocugu olur.</summary>
    void SeatCrate(VehicleNode v, CargoCrateNode n)
    {
        int i = v.State.Crates.IndexOf(n.Crate.Id);
        int col = i % 2, row = i / 2;
        var local = v.Def.Id == "zeplin" ? new Vector3((col - 0.5f) * 0.9f, 0.1f + (row % 3 == 0 ? 0 : 0.7f), -1.5f + row * 0.9f)
            : new Vector3((col - 0.5f) * 0.9f, 1.95f, -1.6f - row * 0.7f);
        if (n.GetParent() != v) n.Reparent(v, false);
        n.Position = local;
        n.Rotation = Vector3.Zero;
    }

    /// <summary>Menu/test girisi: eylemi uygular (tek oyuncu, host) ya da host'a gonderir (istemci).</summary>
    public void DoVehicleAct(VehicleNode v, string op, string arg)
    {
        if (VehicleForward(op, v, arg)) return;
        ApplyVehicleAct(v, Bag, op, arg, Player.GlobalPosition);
    }

    /// <summary>Tek giris: host/tek oyuncu. bag: eylemi yapan oyuncunun cantasi; who konum (menzil host'ta dogrulanir).</summary>
    public string ApplyVehicleAct(VehicleNode v, Inventory bag, string op, string arg, Vector3 who)
    {
        var d = v.Def; var s = v.State; var f = Data.Vehicles;
        string msg = "";
        switch (op)
        {
            case "board": BoardVehicleHost(v, arg); return "";
            case "leave": Kabarma.Sim.Vehicles.Leave(s, int.TryParse(arg, out var rid) ? rid : 1); return "";
            case "fuel":
            {
                int n = Kabarma.Sim.Vehicles.Refuel(d, s, arg, bag.Count(arg));
                if (n > 0) { bag.Remove(arg, n); Hud.Hint($"{d.Name}: +{n} {ItemNames.Of(arg)}", 1.8f); Sfx.Play("pickup", v.GlobalPosition, 0f); }
                else msg = Loc.T("dir.veh_no_fuel_in");
                break;
            }
            case "attach":
            {
                if (bag.Count(arg) <= 0) { msg = Loc.T("dir.veh_no_attachment"); break; }
                string why = Kabarma.Sim.Vehicles.Attach(d, s, f, arg);
                if (why == "") { bag.Remove(arg, 1); v.RefreshAttachments(); Hud.Hint(Loc.T("dir.veh_attached", f.Attachment(arg)?.Name), 1.8f); Sfx.Play("place", v.GlobalPosition, 0f); }
                else msg = why;
                break;
            }
            case "detach":
                if (Kabarma.Sim.Vehicles.Detach(s, arg)) { bag.Add(arg, 1); v.RefreshAttachments(); Hud.Hint(Loc.T("dir.veh_detached", f.Attachment(arg)?.Name), 1.8f); }
                break;
            case "light": s.Light = !s.Light; v.RefreshAttachments(); break;
            case "water":
                if (v.HasAttachment("su_tankeri")) { s.Water = (int)f.Attachment("su_tankeri")!.Params["waterUnits"]; Hud.Hint(Loc.T("dir.veh_tank_full"), 1.5f); }
                break;
            case "park":
                if (d.Kind == "air" && s.Altitude < 2.5f) { s.Parked = true; s.Speed = 0; s.Altitude = 0; }
                break;
            case "loadcrates":
            {
                float reach = v.HasAttachment("vinc") ? 6f : 3.5f;
                foreach (var n in CrateNodesNear(v.GlobalPosition, reach).ToList())
                    if (Kabarma.Sim.Vehicles.LoadCrate(d, s, f, n.Crate.Id)) SeatCrate(v, n);
                break;
            }
            case "unloadcrates":
            {
                foreach (var cid in s.Crates.ToList())
                {
                    var n = CrateNode(cid);
                    Kabarma.Sim.Vehicles.UnloadCrate(s, cid);
                    if (n == null) continue;
                    n.Reparent(this, true);
                    n.GlobalPosition = World.Ground(v.GlobalPosition - v.GlobalBasis.Z * (3.2f + s.Crates.Count * 0.2f) + v.GlobalBasis.X * 1.2f) + Vector3.Up * CargoCrateNode.HalfHeight;
                }
                break;
            }
        }
        if (msg != "") Hud.Hint(msg, 1.8f);
        return msg;
    }
}
