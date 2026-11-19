using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>sekiz (M5): 8 oyuncuya cikis (ilgi alani), dunya devri ve telsiz (TEKNIK 5.3, 5.4).</summary>
public class NetEightTests
{
    static NetFile Cfg() => TestData.Load().Net;

    [Fact]
    public void Interest_thins_far_players_but_never_talking_or_downed_ones()
    {
        var c = Cfg();
        Assert.Equal(8, c.MaxPlayers);
        Assert.Equal(1, NetInterest.Divisor(c.Interest, 10f, false));
        Assert.Equal(c.Interest.MidDivisor, NetInterest.Divisor(c.Interest, 150f, false));
        Assert.Equal(c.Interest.FarDivisor, NetInterest.Divisor(c.Interest, 900f, false));
        Assert.Equal(1, NetInterest.Divisor(c.Interest, 900f, true));       // konusan / baygin hep tam hiz
        Assert.Equal(20f, NetInterest.Hz(c, 1));
        Assert.Equal(5f, NetInterest.Hz(c, c.Interest.FarDivisor));          // uzak 5 Hz
        // bir bolen icinde her karenin 1/bolen'inde gonderilir
        int sent = Enumerable.Range(0, 400).Count(t => NetInterest.SendThisTick((uint)t, 4, 1, 2));
        Assert.Equal(100, sent);
        Assert.Equal(400, Enumerable.Range(0, 400).Count(t => NetInterest.SendThisTick((uint)t, 1, 1, 2)));
        // 8 oyuncu: en iyi/en kotu durumda host'un yazdigi kare/sn, en kotu 7*7*20 = 980
        var (best, worst) = NetInterest.FramesPerSecond(c, 8);
        Assert.Equal(980f, worst);
        Assert.Equal(245f, best);
        Assert.True(best < worst);
    }

    [Fact]
    public void Successor_is_the_longest_connected_ready_standing_client()
    {
        var d = Cfg().Handover;
        var list = new[]
        {
            new HandoverCandidate(3, true, 120f, false), new HandoverCandidate(1, true, 300f, true),   // 1 baygin
            new HandoverCandidate(2, true, 300f, false, 40), new HandoverCandidate(4, false, 900f, false), // 4 hazir degil
            new HandoverCandidate(5, true, 2f, false),                                                      // 5 cok yeni
        };
        Assert.Equal((byte)2, NetHandover.Successor(d, list));
        Assert.Equal((byte)3, NetHandover.Successor(d, list.Where(c => c.Peer != 2)));
        Assert.Null(NetHandover.Successor(d, list.Where(c => c.Peer is 4 or 5 or 1)));
        Assert.Null(NetHandover.Successor(d, System.Array.Empty<HandoverCandidate>()));
        // esitlikte dusuk gecikme, sonra kucuk es
        Assert.Equal((byte)6, NetHandover.Successor(d, new[] { new HandoverCandidate(7, true, 50f, false, 80), new HandoverCandidate(6, true, 50f, false, 20) }));
        Assert.Equal((byte)2, NetHandover.Successor(d, new[] { new HandoverCandidate(4, true, 50f, false, 20), new HandoverCandidate(2, true, 50f, false, 20) }));
    }

    [Fact]
    public void Handover_message_round_trips_and_port_moves()
    {
        var d = Cfg().Handover;
        Assert.Equal(27016, NetHandover.NewPort(d, 27015));
        var bytes = NetHandover.Pack(2, 3, "192.168.1.20", 27016);
        var r = new NetReader(bytes);
        Assert.Equal(NetMsg.Handover, (NetMsg)r.U8());
        Assert.Equal(((byte)2, (byte)3, "192.168.1.20", 27016), NetHandover.Unpack(r));
    }

    [Fact]
    public void Radio_reaches_beyond_proximity_range_at_radio_gain()
    {
        var v = Cfg().Voice;
        Assert.True(NetVoice.Gain(v.MaxDistance * 3, false, v) == 0f);        // yakinlik sesi menzil disinda duyulmaz
        Assert.Equal(v.RadioGain, NetVoice.Gain(v.MaxDistance * 3, true, v));  // telsiz konumsuz kanal: mesafeden bagimsiz
        Assert.Equal(v.RadioGain, NetVoice.Gain(0f, true, v));
    }
}