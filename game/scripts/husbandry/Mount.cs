using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Binek (GDD 9.5; Sim Mount.cs). Hayvan panelinde "Bin": oyuncu yurur ve hayvan altinda tasinir; oyuncunun hizi
/// binek hizina carpilir (Player.SpeedMult carpan olarak; kartlar ya da safak degistirirse taban yeniden alinir). E ya
/// da zipla ile inilir; Baglama Diregi'nin yaninda inilirse hayvan baglanir. Yol mount_m olcusune ve Surus XP'sine.
/// YER TUTUCU: binme pozu yok (oyuncu akisinin klibi); oyuncu hayvanin ustunde degil yaninda/icinde durur.
/// Ortak oyun: istemcinin istegi host'a gider ("ranch.mount"); host hayvani o oyuncunun konumuna tasir. Istemcinin
/// kendi hizi henuz artmaz (Rider ag esitlemesinde yok; M5).
/// </summary>
public partial class RanchSystem
{
    HusbandryAgent _ride;
    float _rideBase = 1f, _rideMult = 1f;
    Vector3 _ridePrev;

    /// <summary>Bu ucun binicisi (tek oyuncuda 1; ortak oyunda es + 1).</summary>
    int LocalRider => (_d.Net?.LocalPeer ?? 0) + 1;

    public bool Riding => _ride != null && IsInstanceValid(_ride);
    public HusbandryAgent RideAgent => Riding ? _ride : null;

    void RegisterMountNet()
    {
        _d.Net?.On("ranch.mount", (actor, r) =>
        {
            var a = Ranch.ById((int)r.VarU());
            var ag = a == null ? null : AgentOf(a.Id);
            if (ag == null || !Near(actor, ag.Host.GlobalPosition, 4f)) return;
            if (Ranch.Mount.Mount(a, actor.Peer + 1)) GD.Print($"RANCH mounted {a.Species} by peer {actor.Peer}");
        });
        _d.Net?.On("ranch.dismount", (actor, r) =>
        {
            var a = Ranch.Animals.FirstOrDefault(x => x.Rider == actor.Peer + 1);
            if (a != null) Ranch.Mount.Dismount(a, actor.Last.X, actor.Last.Z);
        });
    }

    /// <summary>Panel satiri: binilebilir mi.</summary>
    public bool CanMount(HusbandryAgent ag) => !Riding && ag != null && Ranch.Mount.CanRide(ag.Animal);

    public void MountUp(HusbandryAgent ag)
    {
        if (!CanMount(ag)) return;
        if (_d.Net?.Forward("ranch.mount", w => w.VarU((uint)ag.Animal.Id)) == true) return;
        if (!Ranch.Mount.Mount(ag.Animal, LocalRider)) return;
        _ride = ag;
        _rideBase = _d.Player.SpeedMult;
        float bonus = _d.State.Mastery.Sum("mount_speed");
        _rideMult = Ranch.Mount.Speed(ag.Animal, bonus) / Mathf.Max(0.1f, _d.Player.WalkSpeed);
        _d.Player.SpeedMult = _rideBase * _rideMult;
        _ridePrev = _d.Player.GlobalPosition;
        Fx.Text3D(ag.Host.GlobalPosition + Vector3.Up * (ag.Species.Size + 0.8f), "Bindin", Palette.CoopBlue, 64, 1f, 1.2f);
        Sfx.Play("pickup", ag.Host.GlobalPosition, -4f);
        GD.Print($"RANCH mounted {ag.Species.Id} id={ag.Animal.Id} speed={Ranch.Mount.Speed(ag.Animal, bonus):0.00}");
    }

    public void Dismount()
    {
        if (!Riding) return;
        var ag = _ride;
        _ride = null;
        _d.Player.SpeedMult = _rideBase;
        if (_d.Net?.Forward("ranch.dismount", null) == true) return;
        var p = _d.Player.GlobalPosition;
        // hayvan oyuncunun yanina iner
        var side = _d.Player.GlobalBasis.X; side.Y = 0;
        var at = _d.World.Ground(p + (side.LengthSquared() > 0.01f ? side.Normalized() : Vector3.Right) * (ag.Species.Size * 0.6f + 0.4f));
        ag.Host.GlobalPosition = at;
        var post = Ranch.Mount.Dismount(ag.Animal, at.X, at.Z);
        if (post != null) Fx.Text3D(at + Vector3.Up * (ag.Species.Size + 0.6f), "Bağlandı", Palette.CoopBlue, 64, 1f, 1.2f);
        GD.Print($"RANCH dismounted {ag.Species.Id} id={ag.Animal.Id}{(post != null ? " tied" : "")}");
    }

    /// <summary>Her kare: binilen hayvani oyuncunun altinda tasir, hizi korur, yolu sayar; uzaktaki binicilerin bineklerini (host) tasir.</summary>
    void MountProcess(float dt)
    {
        if (Riding)
        {
            var ag = _ride;
            if (ag.Animal.Rider == 0 || _d.Player.Down) { Dismount(); return; }
            if (Input.IsActionJustPressed("jump") && !_d.Hud.MenuOpen) { Dismount(); return; }
            // kartlar ya da safak hizi degistirdiyse yeni taban
            if (!Mathf.IsEqualApprox(_d.Player.SpeedMult, _rideBase * _rideMult)) _rideBase = _d.Player.SpeedMult;
            _d.Player.SpeedMult = _rideBase * _rideMult;
            CarryUnder(ag, _d.Player.GlobalPosition, _d.Player.GlobalRotation.Y, dt);
            var p = _d.Player.GlobalPosition;
            float moved = new Vector2(p.X - _ridePrev.X, p.Z - _ridePrev.Z).Length();
            _ridePrev = p;
            if (moved > 0 && moved < 5f)
            {
                var (m, xp) = Ranch.Mount.Travel(moved);
                if (m > 0) _d.State.Orders.Report("mount_m", m);
                if (xp > 0) _d.State.Mastery.Earn("ride_100m", xp);
            }
        }
        if (_d.Net == null || _d.Net.IsClient) return;
        // host: uzak binicilerin bineklerini son konumlarina tasir
        foreach (var actor in _d.Net.Actors.Values)
        {
            var a = Ranch.Animals.FirstOrDefault(x => x.Rider == actor.Peer + 1);
            var ag = a == null ? null : AgentOf(a.Id);
            if (ag != null && actor.HasFrame) CarryUnder(ag, new Vector3(actor.Last.X, actor.Last.Y, actor.Last.Z), ag.Host.Rotation.Y, dt);
        }
    }

    void CarryUnder(HusbandryAgent ag, Vector3 at, float yaw, float dt)
    {
        var to = _d.World.Ground(at);
        var d = to - ag.Host.GlobalPosition; d.Y = 0;
        ag.Host.GlobalPosition = to;
        if (d.LengthSquared() > 0.0004f) yaw = Mathf.Atan2(d.X, d.Z);
        ag.Host.Rotation = new Vector3(0, Mathf.LerpAngle(ag.Host.Rotation.Y, yaw, Mathf.Min(1f, dt * 8f)), 0);
        (ag.Host as IHusbandryHost)?.HusbandrySteer(to, d.Length() > 0.01f ? d.Length() / Mathf.Max(dt, 1e-3f) : 0f, true);
        ag.Animal.X = to.X; ag.Animal.Z = to.Z;
    }
}
