using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Sesli sohbet (GDD 22.1, TEKNIK 5.4): NetVoiceNode.cs yakalar ve calar; burada yalniz ag yolu.</summary>
public partial class NetSync
{
    bool _talking;
    /// <summary>Ses dugumu (yakalama, cozme, konumlu calma). Basliksiz kipte de kurulur; mikrofon yoksa yalniz calar.</summary>
    public NetVoiceNode Voice { get; private set; }

    void RegisterVoice()
    {
        Voice = new NetVoiceNode(this) { Name = "Voice" };
        AddChild(Voice);
    }

    void TickVoice(float dt) => _talking = Voice?.Talking == true;

    /// <summary>Kendi ses cercevemi gonder (NetVoiceNode cagirir).</summary>
    public void SendVoice(byte[] packet)
    {
        if (IsHost) Broadcast(packet, NetChannel.Voice);
        else SendHost(packet, NetChannel.Voice);
    }

    void OnVoice(byte from, NetReader r, bool relay)
    {
        var (peer, radio, seq, codec, data) = NetVoice.Read(r);
        if (relay) { peer = from; Broadcast(NetVoice.Packet(peer, radio, seq, codec, data), NetChannel.Voice, except: from); }
        Voice?.Receive(peer, radio, seq, codec, data);
    }
}
