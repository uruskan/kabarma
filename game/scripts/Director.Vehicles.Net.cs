using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Araclar, ortak oyun (CLAUDE.md "Ortak oyuna baglanma"). Host yetkili: arac durumu "vehicles" bolumuyle herkese yazilir
/// (konum, can, yakit, koltuklar, eklentiler, kasa kimlikleri); istemci yalniz gorunumu kurar. Istemcinin eylemleri
/// ("vact": bin, in, yakit, eklenti, isik, kasa, su, park) host'a gider; host aktorun cantasindan ve konumundan dogrular
/// (menzil), sonra Director.ApplyVehicleAct'i cagirir. Surucu istemci girdisini ("vin") saniyede NetInputHz kez gonderir;
/// host aracı simule eder. Koltuk kimligi: host'ta 1, istemcide 100 + es (Sim VehicleState.Riders).
/// </summary>
public partial class Director
{
    readonly Dictionary<int, (VehicleInput input, ulong at)> _remoteInput = new();
    float _inputSendT;

    void BindVehicleNet()
    {
        var net = Net;
        if (net == null) return;
        net.Register("vehicles", false, WriteVehicles, ReadVehicles);
        net.On("vact", HostVehicleAct);
        net.On("vin", HostVehicleInput);
        GD.Print($"VEHICLE net bound host={net.IsHost}");
    }

    /// <summary>Istemcide eylemi host'a gonderir (true); host ve tek oyuncuda false.</summary>
    bool VehicleForward(string op, VehicleNode v, string arg) =>
        Net?.Forward("vact", w => { w.VarU((uint)v.State.Id); w.Str(op); w.Str(arg); }) == true;

    void WriteVehicles(NetWriter w, int peer)
    {
        w.VarU((uint)_nextVehicleId);
        var list = Vehicles.Where(IsInstanceValid).OrderBy(v => v.State.Id).ToList();
        w.VarU((uint)list.Count);
        foreach (var v in list)
        {
            var s = v.State;
            w.VarU((uint)s.Id); w.Str(s.Vehicle);
            w.F32(s.X); w.F32(s.Y); w.F32(s.Z); w.F32(s.Yaw); w.F32(s.Speed); w.F32(s.Fuel); w.F32(s.Hp); w.F32(s.Altitude);
            w.Bool(s.Flipped); w.Bool(s.Parked); w.Bool(s.Light); w.VarU((uint)s.Water);
            w.VarU((uint)s.Riders.Count); foreach (var r in s.Riders) w.VarU((uint)r);
            w.VarU((uint)s.Attachments.Count); foreach (var a in s.Attachments) w.Str(a);
            w.VarU((uint)s.Crates.Count); foreach (var c in s.Crates) w.VarU((uint)c);
        }
    }

    void ReadVehicles(NetReader r)
    {
        if (Net == null || Net.IsHost) return;
        _nextVehicleId = (int)r.VarU();
        int n = (int)r.VarU();
        var seen = new HashSet<int>();
        for (int i = 0; i < n; i++)
        {
            var st = new VehicleState { Id = (int)r.VarU(), Vehicle = r.Str() };
            st.X = r.F32(); st.Y = r.F32(); st.Z = r.F32(); st.Yaw = r.F32(); st.Speed = r.F32(); st.Fuel = r.F32(); st.Hp = r.F32(); st.Altitude = r.F32();
            st.Flipped = r.Bool(); st.Parked = r.Bool(); st.Light = r.Bool(); st.Water = (int)r.VarU();
            int rc = (int)r.VarU(); for (int k = 0; k < rc; k++) st.Riders.Add((int)r.VarU());
            int ac = (int)r.VarU(); for (int k = 0; k < ac; k++) st.Attachments.Add(r.Str());
            int cc = (int)r.VarU(); for (int k = 0; k < cc; k++) st.Crates.Add((int)r.VarU());
            seen.Add(st.Id);
            var node = Vehicles.FirstOrDefault(v => IsInstanceValid(v) && v.State.Id == st.Id);
            if (node == null)
            {
                if (Data.Vehicles.Vehicles.FirstOrDefault(d => d.Id == st.Vehicle) is not { } def) continue;
                SpawnVehicle(def, st);
                continue;
            }
            var cur = node.State;
            bool attachChanged = !cur.Attachments.SequenceEqual(st.Attachments) || cur.Light != st.Light;
            cur.X = st.X; cur.Y = st.Y; cur.Z = st.Z; cur.Yaw = st.Yaw; cur.Speed = st.Speed; cur.Fuel = st.Fuel; cur.Hp = st.Hp; cur.Altitude = st.Altitude;
            cur.Flipped = st.Flipped; cur.Parked = st.Parked; cur.Water = st.Water;
            cur.Riders.Clear(); cur.Riders.AddRange(st.Riders);
            cur.Crates.Clear(); cur.Crates.AddRange(st.Crates);
            cur.Attachments.Clear(); cur.Attachments.AddRange(st.Attachments); cur.Light = st.Light;
            if (attachChanged) node.RefreshAttachments();
            foreach (var cid in cur.Crates) { var cn = CrateNode(cid); if (cn != null && cn.GetParent() != node) SeatCrate(node, cn); }
        }
        foreach (var v in Vehicles.Where(v => IsInstanceValid(v) && !seen.Contains(v.State.Id)).ToList()) { Vehicles.Remove(v); v.QueueFree(); }
        // Koltuk: host beni surucu ya da yolcu yapti mi.
        var mine = Vehicles.FirstOrDefault(v => IsInstanceValid(v) && v.State.Riders.Contains(MyRiderId));
        if (mine != null && Riding != mine) EnterVehicle(mine);
        else if (mine == null && Riding != null) { Riding = null; Player.MoveOverride = null; }
    }

    void HostVehicleAct(NetActor actor, NetReader r)
    {
        int id = (int)r.VarU(); string op = r.Str(), arg = r.Str();
        var v = Vehicles.FirstOrDefault(x => IsInstanceValid(x) && x.State.Id == id);
        if (v == null || !NetRules.InRange(actor, v.GlobalPosition.X, v.GlobalPosition.Y, v.GlobalPosition.Z, Data.Vehicles.Rules.ReachMeters + 6f)) { GD.Print($"VEHICLE net reject {op} {id}"); return; }
        int rider = 100 + actor.Peer;
        switch (op)
        {
            case "board": Kabarma.Sim.Vehicles.Board(v.Def, v.State, rider); break;
            case "leave": if (v.State.Altitude <= 2.5f) Kabarma.Sim.Vehicles.Leave(v.State, rider); break;
            default: ApplyVehicleAct(v, actor.Bag, op, arg, v.GlobalPosition); break;
        }
        Net.Flush();
    }

    /// <summary>Host: surucu istemcinin son girdisi.</summary>
    void HostVehicleInput(NetActor actor, NetReader r)
    {
        int id = (int)r.VarU();
        var input = new VehicleInput { Throttle = r.F32(), Steer = r.F32(), Brake = r.Bool(), Lift = r.F32() };
        var v = Vehicles.FirstOrDefault(x => IsInstanceValid(x) && x.State.Id == id);
        if (v == null || v.State.Riders.Count == 0 || v.State.Riders[0] != 100 + actor.Peer) return;
        _remoteInput[id] = (input, Time.GetTicksMsec());
    }

    /// <summary>Istemci surucu: girdiyi host'a gonderir (NetInputHz).</summary>
    void SendVehicleInput(float dt)
    {
        if (Net == null || !Net.IsClient || Riding == null) return;
        _inputSendT += dt;
        if (_inputSendT < 1f / Mathf.Max(1f, Data.Vehicles.Rules.NetInputHz)) return;
        _inputSendT = 0;
        if (Riding.State.Riders.Count == 0 || Riding.State.Riders[0] != MyRiderId) return; // yolcu girdi gondermez
        var i = LocalVehicleInput(Riding);
        int id = Riding.State.Id;
        Net.Forward("vin", w => { w.VarU((uint)id); w.F32(i.Throttle); w.F32(i.Steer); w.Bool(i.Brake); w.F32(i.Lift); });
    }

    void BoardVehicleHost(VehicleNode v, string arg)
    {
        int rider = int.TryParse(arg, out var x) ? x : 1;
        Kabarma.Sim.Vehicles.Board(v.Def, v.State, rider);
    }
}
