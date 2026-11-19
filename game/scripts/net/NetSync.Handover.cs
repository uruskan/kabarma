using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// sekiz (M5): dunyayi devretme (GDD 22.1, TEKNIK 5.4). Host ayrilirken ya da isteyince: en uygun hazir istemci (NetHandover.Successor)
/// ayni surecte host olur (dunya Sim durumu kayit koprusuyle zaten onda), oteki istemciler yeni host'a yeniden baglanir (Hello, Welcome,
/// bolumler bastan). Kural Sim'de (NetInterest.cs: NetHandover); burada tasima. Yalniz ENet (adres istemcinin uzak IP'sinden alinir); Steam
/// tasimasinda lobi sahibi degisikligi Steam'in isidir. Uc uca sinama (cok surecli devir) yok: yalniz kurallar ve mesaj Sim testinde.
/// </summary>
public partial class NetSync
{
    readonly Dictionary<byte, double> _readyAt = new();
    int _port;
    /// <summary>Devir suruyor (eski host kapanmadan once).</summary>
    public bool HandingOver { get; private set; }

    /// <summary>Host: devralacak istemciyi secer ve devri baslatir. Eski host da yeni host'a istemci olarak baglanir. Olmuyorsa false (neden Status'ta).</summary>
    public bool Handover()
    {
        if (!IsHost || HandingOver) return false;
        if (_tx is not EnetTransport enet) { Status = "devir yalnız ENet'te"; return false; }
        var cands = Actors.Values.Select(a => new HandoverCandidate(a.Peer, a.Ready, (float)(Now - _readyAt.GetValueOrDefault(a.Peer, Now)),
            (a.Last.Flags & NetFrameFlags.Down) != 0)).ToList();
        var succ = NetHandover.Successor(Cfg.Handover, cands);
        if (succ == null || !_conn.TryGetValue(succ.Value, out int conn)) { Status = "devralacak uygun oyuncu yok"; return false; }
        string ip = enet.RemoteAddress(conn);
        if (string.IsNullOrEmpty(ip)) { Status = "devralanın adresi bilinmiyor"; return false; }
        int port = NetHandover.NewPort(Cfg.Handover, _port);
        HandingOver = true;
        SendTo(succ.Value, NetHandover.Pack(1, succ.Value, "", port));
        foreach (var (peer, a) in Actors) if (a.Ready && peer != succ.Value) SendTo(peer, NetHandover.Pack(2, succ.Value, ip, port));
        GD.Print($"NET handover -> peer {succ.Value} {ip}:{port}");
        // Mesajlar gitsin diye kisa sure sonra kendimiz de istemci olarak yeni host'a baglaniriz.
        GetTree().CreateTimer(0.5).Timeout += () => JoinHost(ip, port);
        return true;
    }

    void OnHandover(NetReader r)
    {
        var (role, succ, address, port) = NetHandover.Unpack(r);
        if (role == 1) PromoteToHost(port);
        else JoinHost(address, port);
    }

    void ClearPeers()
    {
        foreach (var peer in Avatars.Keys.ToList()) DespawnAvatar(peer);
        Actors.Clear(); _byConn.Clear(); _conn.Clear(); _readyAt.Clear();
        Roster = new List<NetRosterEntry>();
    }

    /// <summary>Devralan: dinleyici acar, host olur. Dunya Sim durumu kayit koprusuyle zaten bu sureçte.</summary>
    void PromoteToHost(int port)
    {
        _tx?.Dispose();
        _tx = EnetTransport.Host(port, Cfg.MaxPlayers - 1, out var err);
        if (_tx == null) { Fail(err ?? "devir: dinleyici kurulamadı"); return; }
        ClearPeers();
        IsHost = true; Joined = true; LocalPeer = 0; Failed = false; HandingOver = false; _port = port;
        _startT = _lastHeard = Now;
        RebuildRoster();
        Status = $"host (devralındı) · {_tx.Address}";
        GD.Print($"NET promoted to host {_tx.Address}");
    }

    /// <summary>Istemci (ya da eski host): yeni host'a baglanir; Hello/Welcome yeniden yapilir.</summary>
    void JoinHost(string address, int port)
    {
        _tx?.Dispose();
        ClearPeers();
        IsHost = false; Joined = false; Failed = false; HandingOver = false; _port = port;
        _tx = EnetTransport.Connect(address, port, out var err);
        if (_tx == null) { Fail(err ?? "devir: yeni host'a bağlanılamadı"); return; }
        _startT = _lastHeard = Now;
        Status = $"yeni host'a bağlanıyor · {_tx.Address}";
        GD.Print($"NET rejoin {_tx.Address}");
    }
}