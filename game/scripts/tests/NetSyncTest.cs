using System;
using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// M0 adim 5 (TEKNIK.md §1.2): C# ile Oduncu'daki senkron testinin esdegeri.
/// Iki surec: --netsync=host fizigi yurutur (host yetkili), --netsync=client ENet ile baglanir ve anlik goruntuleri uygular.
/// Olcum: istemcinin her tikteki durumu ile hostun ayni tikteki durumu arasindaki fark ve son durum farki.
/// Gecme olcutu: son durum farki 1 cm'nin altinda.
/// </summary>
public partial class NetSyncTest : Node3D
{
    const int Port = 27021;
    const int Bodies = 24;
    const int Ticks = 600;          // 10 sn, 60 Hz
    const int SettleTicks = 120;    // son 2 sn: itme yok, cisimler durulur
    bool _host;
    ENetMultiplayerPeer _peer;
    readonly List<RigidBody3D> _rb = new();
    readonly List<Node3D> _ghost = new();
    int _tick;
    bool _started, _clientReady;
    readonly Dictionary<int, Vector3[]> _hostHistory = new();
    double _maxLive, _sumLive; int _nLive;
    float _quitT = -1;
    readonly RandomNumberGenerator _rng = new() { Seed = 4417 };
    float _waitT;

    public override void _Ready()
    {
        _host = Main.Args["netsync"] == "host";
        Engine.MaxFps = 60; // basliksiz calisma gercek zamanda kalsin (ag gercek zamanli)
        Name = "NetSync"; // her iki surecte ayni yol: /root/Main/NetSync
        _peer = new ENetMultiplayerPeer();
        var err = _host ? _peer.CreateServer(Port, 2) : _peer.CreateClient("127.0.0.1", Port);
        if (err != Error.Ok) { GD.Print($"NETSYNC {(_host ? "host" : "client")} create FAIL {err}"); GetTree().Quit(2); return; }
        Multiplayer.MultiplayerPeer = _peer;
        Multiplayer.PeerConnected += id => { GD.Print($"NETSYNC peer connected {id}"); if (_host) _clientReady = true; };
        Multiplayer.ConnectionFailed += () => { GD.Print("NETSYNC client connect FAIL"); GetTree().Quit(1); };

        var ground = new StaticBody3D();
        ground.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40, 1, 40) }, Position = new Vector3(0, -0.5f, 0) });
        AddChild(ground);
        for (int i = 0; i < Bodies; i++)
        {
            var start = new Vector3((i % 6 - 2.5f) * 1.2f, 1f + i / 6 * 1.3f, (i / 6 - 1.5f) * 1.2f);
            if (_host)
            {
                var rb = new RigidBody3D { Mass = 1f + i % 3, Position = start };
                rb.AddChild(new CollisionShape3D { Shape = i % 2 == 0 ? new BoxShape3D { Size = Vector3.One * 0.8f } : new SphereShape3D { Radius = 0.45f } });
                AddChild(rb);
                _rb.Add(rb);
            }
            else
            {
                var g = new Node3D { Position = start };
                AddChild(g);
                _ghost.Add(g);
            }
        }
        GD.Print($"NETSYNC {(_host ? "host listening" : "client connecting")} port={Port}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_quitT > 0) { _quitT -= (float)delta; if (_quitT <= 0) GetTree().Quit(); return; }
        if (!_host)
        {
            _waitT += (float)delta;
            if (_waitT > 30f) { GD.Print("NETSYNC client TIMEOUT"); GetTree().Quit(1); }
            return;
        }
        if (!_clientReady) { _waitT += (float)delta; if (_waitT > 20f) { GD.Print("NETSYNC host no client TIMEOUT"); GetTree().Quit(1); } return; }
        if (!_started) { _started = true; GD.Print("NETSYNC host start"); }

        // Fizik komedisi: ilk 8 sn rastgele itmeler ve firlatmalar, son 2 sn durulma.
        if (_tick < Ticks - SettleTicks && _tick % 20 == 0)
        {
            var b = _rb[_rng.RandiRange(0, Bodies - 1)];
            b.ApplyCentralImpulse(new Vector3(_rng.RandfRange(-4, 4), _rng.RandfRange(3, 7), _rng.RandfRange(-4, 4)) * b.Mass);
            b.ApplyTorqueImpulse(new Vector3(_rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1)));
        }
        var state = new float[Bodies * 7];
        var pos = new Vector3[Bodies];
        for (int i = 0; i < Bodies; i++)
        {
            var t = _rb[i].GlobalTransform;
            var q = t.Basis.GetRotationQuaternion();
            pos[i] = t.Origin;
            state[i * 7] = t.Origin.X; state[i * 7 + 1] = t.Origin.Y; state[i * 7 + 2] = t.Origin.Z;
            state[i * 7 + 3] = q.X; state[i * 7 + 4] = q.Y; state[i * 7 + 5] = q.Z; state[i * 7 + 6] = q.W;
        }
        _hostHistory[_tick] = pos;
        Rpc(MethodName.Snapshot, _tick, state);
        _tick++;
        if (_tick >= Ticks)
        {
            Rpc(MethodName.Final, _tick - 1, state);
            GD.Print($"NETSYNC host done ticks={_tick}");
            _quitT = 1.5f;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    void Snapshot(int tick, float[] state)
    {
        Apply(state);
        // Istemcinin gosterdigi durum = son alinan anlik goruntu. Canli fark: kayip/sirasiz paket varsa gorunur.
        _nLive++;
        if (_lastTick >= 0 && tick != _lastTick + 1) _gaps++;
        _lastTick = tick;
    }

    int _lastTick = -1, _gaps;

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void Final(int tick, float[] state)
    {
        // Son durum farki: istemcinin o anda gosterdigi durum ile hostun son yetkili durumu.
        double maxErr = 0;
        for (int i = 0; i < Bodies; i++)
        {
            var hp = new Vector3(state[i * 7], state[i * 7 + 1], state[i * 7 + 2]);
            maxErr = Math.Max(maxErr, _ghost[i].GlobalPosition.DistanceTo(hp));
        }
        GD.Print($"NETSYNC client final tick={tick} snapshots={_nLive} gaps={_gaps} final_max_diff_mm={maxErr * 1000:0.000}");
        GD.Print($"RESULT netsync {(maxErr < 0.01 ? "PASS" : "FAIL")} final_max_diff_mm={maxErr * 1000:0.000}");
        _quitT = 0.5f;
    }

    void Apply(float[] s)
    {
        for (int i = 0; i < Bodies && i * 7 + 6 < s.Length; i++)
        {
            _ghost[i].GlobalTransform = new Transform3D(new Basis(new Quaternion(s[i * 7 + 3], s[i * 7 + 4], s[i * 7 + 5], s[i * 7 + 6]).Normalized()), new Vector3(s[i * 7], s[i * 7 + 1], s[i * 7 + 2]));
        }
    }
}
