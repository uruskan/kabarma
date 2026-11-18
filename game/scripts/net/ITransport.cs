using System;
using System.Collections.Generic;
using Kabarma.Sim;

namespace Kabarma.Game;

public enum NetEventKind { Connected, Disconnected, Data }

/// <summary>Tasimadan gelen olay. Peer: tasimanin baglanti kimligi (istemcide host hep 0).</summary>
public readonly record struct NetEvent(NetEventKind Kind, int Peer, byte[] Data, int Channel);

/// <summary>
/// Tasima (TEKNIK 5.1): ENet (gelistirme, yerel ag, dogrudan IP) ve Steam Networking Sockets (davet, lobi, relay) ayni
/// arayuzun arkasinda. Ust katman (NetSync) yalniz bunu gorur; Godot'un ust duzey RPC'si kullanilmaz.
/// </summary>
public interface ITransport : IDisposable
{
    string Name { get; }
    bool IsHost { get; }
    /// <summary>Istemci: host'a bagli mi. Host: dinliyor mu.</summary>
    bool Up { get; }
    /// <summary>Kanal 0 guvenilir sirali, 1 guvenilmez sirali, 2 guvenilmez (ses).</summary>
    void Send(int peer, byte[] data, NetChannel channel);
    /// <summary>Gelenleri toplar ve bekleyenleri gonderir. Her karede bir kez.</summary>
    void Poll(List<NetEvent> into);
    void Kick(int peer);
    /// <summary>Kullaniciya gosterilecek adres (IP:port ya da Steam lobisi).</summary>
    string Address { get; }
    /// <summary>Kablodan giden ve gelen toplam bayt (ENet: protokol ek yuku dahil; olcum, TEKNIK 5.3).</summary>
    long BytesSent { get; }
    long BytesReceived { get; }
}
