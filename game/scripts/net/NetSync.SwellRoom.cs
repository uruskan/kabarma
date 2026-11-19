using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kabartma Odasi ortak oyunda (gece4, GDD 14.10): istemcinin cantasi host'ta durur, bu yuzden eylem host'a gider
/// (Director.KabartmaOdasi.cs). Host: aktor istasyonun erisiminde mi, hedef istasyonun yaricapinda mi, guc, Kabarma ve Maya
/// (SwellRoom, aktorun cantasi) dogrulanir; hayvanda RanchSystem.SwellAnimal. Istek: "kabartma" [tur: 0 kare, 1 hayvan],
/// kare: F32 x, F32 z (hucre merkezi); hayvan: VarU hayvan kimligi.
/// </summary>
public partial class NetSync
{
    void RegisterSwellRoom() => On("kabartma", HostSwellRoom);

    void HostSwellRoom(NetActor a, NetReader r)
    {
        byte kind = r.U8();
        var p = ActorPos(a);
        float reach = T.StationReach + 1.5f;
        var st = D.Stations.FirstOrDefault(s => GodotObject.IsInstanceValid(s) && s.Def.Station == "kabartma_odasi"
            && new Vector2(s.GlobalPosition.X - p.X, s.GlobalPosition.Z - p.Z).Length() < reach);
        if (st == null) { Reject(a, "kabartma station"); return; }
        bool powered = st.Queue.PowerOk == null || st.Queue.PowerOk();
        var def = D.Data.Tuzuk.SwellRoom;
        if (kind == 0)
        {
            float x = r.F32(), z = r.F32();
            var plot = D.State.Farm.Plots.FirstOrDefault(q => System.Math.Abs(q.X + 0.5f - x) < 0.6f && System.Math.Abs(q.Y + 0.5f - z) < 0.6f);
            if (plot == null || new Vector2(plot.X + 0.5f - st.GlobalPosition.X, plot.Y + 0.5f - st.GlobalPosition.Z).Length() > def.Radius + 1f) { Reject(a, "kabartma plot"); return; }
            if (SwellRoom.UsePlot(D.State, a.Bag, powered, plot) != SwellRoomResult.Ok) Reject(a, "kabartma rule");
            return;
        }
        int id = (int)r.VarU();
        var animal = D.Ranch?.Ranch.ById(id);
        var near = animal == null ? null : D.Ranch.AgentOf(id);
        if (animal == null || near == null || near.Host.GlobalPosition.DistanceTo(st.GlobalPosition) > def.Radius + 1f) { Reject(a, "kabartma animal"); return; }
        if (SwellRoom.UseAnimal(D.State, a.Bag, powered, true) != SwellRoomResult.Ok) { Reject(a, "kabartma rule"); return; }
        if (!D.Ranch.SwellAnimal(animal, def.AnimalSeconds)) { a.Bag.Add(MayaVault.Item, def.MayaCost); Reject(a, "kabartma target"); } // hayvan uygun degil: bedel iade
    }
}