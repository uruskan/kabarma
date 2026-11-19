using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// ENet tasimasi: Godot'un ENetConnection'i dogrudan (ham paket, 3 kanal). Gelistirme, yerel ag ve adanmis sunucuya
/// dogrudan baglanma (TEKNIK 5.1). M0 adim 5 (NetSyncTest) ayni kutuphaneyle 0,000 mm senkron olctu.
/// </summary>
public sealed class EnetTransport : ITransport
{
    readonly ENetConnection _c = new();
    readonly Dictionary<int, ENetPacketPeer> _peers = new();
    readonly Dictionary<ulong, int> _ids = new();
    ENetPacketPeer _server;
    int _next = 1;
    bool _connected;

    public string Name => "enet";
    public bool IsHost { get; private set; }
    public bool Up => IsHost || _connected;
    public string Address { get; private set; } = "";
    public const int Channels = 3;

    EnetTransport() { }

    public static EnetTransport Host(int port, int maxPeers, out string error)
    {
        var t = new EnetTransport { IsHost = true, Address = $"0.0.0.0:{port}" };
        var err = t._c.CreateHostBound("*", port, maxPeers, Channels);
        error = err == Error.Ok ? null : $"ENet host {port}: {err}";
        if (error != null) return null;
        t._c.Compress(ENetConnection.CompressionMode.RangeCoder);
        return t;
    }

    public static EnetTransport Connect(string address, int port, out string error)
    {
        var t = new EnetTransport { Address = $"{address}:{port}" };
        var err = t._c.CreateHost(1, Channels);
        error = err == Error.Ok ? null : $"ENet client: {err}";
        if (error != null) return null;
        t._c.Compress(ENetConnection.CompressionMode.RangeCoder);
        t._server = t._c.ConnectToHost(address, port, Channels);
        if (t._server == null) { error = $"ENet {address}:{port} baglanamadi"; return null; }
        return t;
    }

    static int Flags(NetChannel ch) => ch == NetChannel.Reliable ? (int)ENetPacketPeer.FlagReliable : ch == NetChannel.Voice ? (int)ENetPacketPeer.FlagUnsequenced : 0;

    public void Send(int peer, byte[] data, NetChannel channel)
    {
        var p = IsHost ? _peers.GetValueOrDefault(peer) : _server;
        if (p == null || !p.IsActive()) return;
        p.Send((int)channel, data, Flags(channel));
    }

    public void Poll(List<NetEvent> into)
    {
        for (int guard = 0; guard < 4096; guard++)
        {
            var ev = _c.Service(0);
            var type = (ENetConnection.EventType)(int)ev[0];
            if (type == ENetConnection.EventType.None) break;
            var p = ev[1].As<ENetPacketPeer>();
            int ch = ev.Count > 3 ? (int)ev[3] : 0;
            switch (type)
            {
                case ENetConnection.EventType.Connect:
                    if (IsHost)
                    {
                        int id = _next++;
                        _peers[id] = p;
                        _ids[p.GetInstanceId()] = id;
                        into.Add(new NetEvent(NetEventKind.Connected, id, null, 0));
                    }
                    else { _connected = true; into.Add(new NetEvent(NetEventKind.Connected, 0, null, 0)); }
                    break;
                case ENetConnection.EventType.Disconnect:
                    if (IsHost)
                    {
                        if (p != null && _ids.Remove(p.GetInstanceId(), out int id)) { _peers.Remove(id); into.Add(new NetEvent(NetEventKind.Disconnected, id, null, 0)); }
                    }
                    else { _connected = false; into.Add(new NetEvent(NetEventKind.Disconnected, 0, null, 0)); }
                    break;
                case ENetConnection.EventType.Receive:
                    var data = p.GetPacket();
                    int from = IsHost ? (p != null && _ids.TryGetValue(p.GetInstanceId(), out int f) ? f : -1) : 0;
                    if (from >= 0) into.Add(new NetEvent(NetEventKind.Data, from, data, ch));
                    break;
                case ENetConnection.EventType.Error:
                    GD.PushWarning("NET enet service error");
                    return;
            }
        }
        _c.Flush();
        BytesSent += (long)_c.PopStatistic(ENetConnection.HostStatistic.SentData);
        BytesReceived += (long)_c.PopStatistic(ENetConnection.HostStatistic.ReceivedData);
    }

    public long BytesSent { get; private set; }
    public long BytesReceived { get; private set; }

    public string PeerAddress(int peer) => IsHost && _peers.TryGetValue(peer, out var p) ? p.GetRemoteAddress() : "";

    public void Kick(int peer)
    {
        if (IsHost && _peers.TryGetValue(peer, out var p)) p.PeerDisconnectLater();
    }

    public void Dispose()
    {
        foreach (var p in _peers.Values) p.PeerDisconnectNow();
        _server?.PeerDisconnectNow();
        _c.Flush();
        _c.Destroy();
    }
}
