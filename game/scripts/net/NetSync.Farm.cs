using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Tarla (GDD 8): surme, ekme ve sulama istemcide hemen gorunur (tahmin) ve host'a da gider; host ayni kurali kendi
/// tarlasinda uygular, tarla kayit koprusuyle ("save:farm", 1 Hz) herkese doner ve istemcinin tahminini duzeltir.
/// Tohum host'taki kisisel cantadan duser (canta bolumu). Su deposu kisiseldir (player.pack) ve istemcide kalir.
/// </summary>
public partial class NetSync
{
    void RegisterFarm()
    {
        On("till", HostTill);
        On("water", HostWater);
    }

    /// <summary>Capa.Toprak: istemci yerelde de surer/eker (tahmin); host'a istek. Host'ta ve tek oyuncuda bir sey yapmaz.</summary>
    public void ForwardTill(Vector3 aim)
    {
        if (IsClient) Forward("till", w => w.Pos(aim.X, aim.Y, aim.Z, Cfg.PosPerMetre));
    }

    /// <summary>Capa.Sulama: istemci kendi deposundan harcar ve yerelde islatir (tahmin); host'ta tarla islanir.</summary>
    public void ForwardWater(Vector3 aim)
    {
        if (IsClient && _waterSend <= 0) { _waterSend = 0.1f; Forward("water", w => w.Pos(aim.X, aim.Y, aim.Z, Cfg.PosPerMetre)); }
    }

    float _waterSend;

    void TickFarm(float dt) => _waterSend -= dt;

    void HostTill(NetActor a, NetReader r)
    {
        var (x, y, z) = r.Pos(Cfg.PosPerMetre);
        var aim = new Vector3(x, y, z);
        if (!NetRules.InRange(a, x, y, z, T.TillRange + 2f)) { Reject(a, "till range"); return; }
        // Capa.Toprak.TillOrPlant ile ayni kosullar: tapu icinde, Ocak'a ve kapsule cok yakin degil, egim siniri (Farm.Till).
        if (D.Ocak == null || aim.DistanceTo(D.OcakPos) > Ocak.ClaimRadius) { Reject(a, "till claim"); return; }
        if (aim.DistanceTo(D.OcakPos) < T.TillClearOcak || aim.DistanceTo(D.Pod.GlobalPosition) < T.TillClearPod) return;
        var (cx, cy) = FarmView.Cell(aim);
        var farm = D.State.Farm;
        var plot = farm.Get(cx, cy);
        if (plot == null)
        {
            var w = D.World;
            float px = cx + 0.5f, pz = cy + 0.5f;
            float dx = w.HeightAt(px + 0.5f, pz) - w.HeightAt(px - 0.5f, pz);
            float dz = w.HeightAt(px, pz + 0.5f) - w.HeightAt(px, pz - 0.5f);
            farm.Till(cx, cy, Mathf.RadToDeg(Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz))));
        }
        else if (plot.Crop == null) farm.Plant(cx, cy, "turnip", a.Bag);
    }

    void HostWater(NetActor a, NetReader r)
    {
        var (x, y, z) = r.Pos(Cfg.PosPerMetre);
        if (!NetRules.InRange(a, x, y, z, T.WaterRange + 2f)) { Reject(a, "water range"); return; }
        var farm = D.State.Farm;
        foreach (var plot in farm.Plots)
        {
            var c = D.FarmView.CellCenter(plot.X, plot.Y);
            if (new Vector2(c.X - x, c.Z - z).Length() > 1.1f || plot.Moisture > 92f) continue; // Capa.Sulama ile ayni yaricap ve esik
            farm.Water(plot.X, plot.Y);
        }
    }
}
