using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Ortak oyun kurallari ve protokolu (GDD 22.1, 3.6; TEKNIK 5; net.json).</summary>
public class NetTests
{
    static NetFile Net => TestData.Load().Net;

    [Fact]
    public void Data_matches_docs()
    {
        var n = Net;
        Assert.Equal(8, n.MaxPlayers);              // GDD 22.1: 1-8
        Assert.Equal(20f, n.FrameHz);               // TEKNIK 5.3: oyuncu 20 Hz
        Assert.Equal(10f, n.CreatureNearHz);        // yaratik 120 m icinde 10 Hz
        Assert.Equal(2f, n.CreatureFarHz);
        Assert.Equal(120f, n.CreatureNearRadius);
        Assert.Equal(10f, n.DigestSeconds);         // 10 sn'de bir ozet
        Assert.Equal(30f, n.Downed.Seconds);        // GDD 3.6: 30 sn baygin
        Assert.Equal(1.5f, n.Downed.ReviveHold);    // E basili 1,5 sn
        Assert.Equal(0.3f, n.Downed.ReviveHpFraction, 3); // %30 can
        Assert.Equal(0.4f, n.Downed.SecondWindFraction, 3); // tek oyuncu %40
        Assert.True(NetQuant.PosError(n.PosPerMetre) <= 0.005f, "konum adimi senkron olcutunun (1 cm) yarisindan buyuk olmamali");
    }

    [Fact]
    public void Codec_roundtrips_all_types()
    {
        var w = new NetWriter(4);
        w.U8(250); w.Bool(true); w.I8(-5); w.U16(65000); w.U32(4_000_000_000); w.U64(ulong.MaxValue - 3); w.F32(-1.25f);
        w.I24(-8_000_000); w.I24(123456); w.VarU(300); w.VarI(-77); w.Str("Gıdak ğüşiöç"); w.Blob(new byte[] { 1, 2, 3 });
        var r = new NetReader(w.ToArray());
        Assert.Equal(250, r.U8()); Assert.True(r.Bool()); Assert.Equal(-5, r.I8()); Assert.Equal(65000, r.U16());
        Assert.Equal(4_000_000_000u, r.U32()); Assert.Equal(ulong.MaxValue - 3, r.U64()); Assert.Equal(-1.25f, r.F32());
        Assert.Equal(-8_000_000, r.I24()); Assert.Equal(123456, r.I24()); Assert.Equal(300u, r.VarU()); Assert.Equal(-77, r.VarI());
        Assert.Equal("Gıdak ğüşiöç", r.Str()); Assert.Equal(new byte[] { 1, 2, 3 }, r.Blob());
        Assert.True(r.AtEnd);
        Assert.Throws<NetFormatException>(() => r.U8());
    }

    [Fact]
    public void Truncated_message_throws_instead_of_crashing()
    {
        var b = NetPackets.Hello(1, "uzun bir ad", 42);
        var r = new NetReader(b, 0, b.Length - 3);
        r.U8(); r.U16(); r.Str();
        Assert.Throws<NetFormatException>(() => r.U64());
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(1234.567f, -12.3456f, -2499.999f)]
    [InlineData(-31000.1f, 300.004f, 31000.9f)]
    public void Player_frame_is_16_bytes_and_under_half_cm(float x, float y, float z)
    {
        int q = Net.PosPerMetre;
        var f = new PlayerFrame { Peer = 3, X = x, Y = y, Z = z, Yaw = 2.5f, Pitch = -0.4f, Anim = 7, Head = 2, Flags = NetFrameFlags.Sprint | NetFrameFlags.Firing };
        var w = new NetWriter();
        f.Write(w, q);
        Assert.Equal(PlayerFrame.Size, w.Length); // TEKNIK 5.3: 16 B
        var g = PlayerFrame.Read(new NetReader(w.ToArray()), q);
        Assert.Equal(3, g.Peer);
        Assert.True(NetRules.Dist(x, y, z, g.X, g.Y, g.Z) < 0.005f);
        Assert.InRange(MathF.Abs(g.Yaw - 2.5f), 0f, 0.001f);
        Assert.InRange(MathF.Abs(g.Pitch + 0.4f), 0f, 0.02f);
        Assert.Equal(7, g.Anim); Assert.Equal(2, g.Head);
        Assert.True(g.Has(NetFrameFlags.Firing) && g.Has(NetFrameFlags.Sprint) && !g.Has(NetFrameFlags.Down));
    }

    [Fact]
    public void Creature_frame_roundtrip()
    {
        int q = Net.PosPerMetre;
        var f = new CreatureFrame { Id = 513, X = -40.2f, Y = 3.1f, Z = 77.7f, Yaw = -1f, State = 5 };
        var w = new NetWriter();
        f.Write(w, q);
        Assert.Equal(CreatureFrame.Size, w.Length);
        var g = CreatureFrame.Read(new NetReader(w.ToArray()), q);
        Assert.Equal(513, g.Id);
        Assert.True(NetRules.Dist(f.X, f.Y, f.Z, g.X, g.Y, g.Z) < 0.005f);
        Assert.Equal(5, g.State);
    }

    [Fact]
    public void Angles_wrap()
    {
        Assert.Equal(NetQuant.Angle16(0.5f), NetQuant.Angle16(0.5f + MathF.PI * 2f));
        Assert.Equal(NetQuant.Angle16(-0.5f), NetQuant.Angle16(MathF.PI * 2f - 0.5f));
    }

    [Fact]
    public void Movement_check_accepts_sprint_and_rejects_teleport()
    {
        var n = Net;
        var a = new PlayerFrame { X = 0, Z = 0 };
        var b = new PlayerFrame { X = 0.4f, Z = 0 }; // 0,05 sn'de 8 m/sn
        Assert.True(NetRules.MovementOk(a, 0, b, 0.05, 8f, n));
        var far = new PlayerFrame { X = 30f, Z = 0 };
        Assert.False(NetRules.MovementOk(a, 0, far, 0.05, 8f, n));
        Assert.True(NetRules.MovementOk(a, double.NaN, far, 0.05, 8f, n)); // ilk kare
        var down = far; down.Flags = NetFrameFlags.Down;
        Assert.True(NetRules.MovementOk(a, 0, down, 0.05, 8f, n));        // ragdoll savrulmasi
        var fall = new PlayerFrame { X = 0, Y = -40f, Z = 0 };
        Assert.True(NetRules.MovementOk(a, 0, fall, 0.05, 8f, n));        // dusme serbest
    }

    [Fact]
    public void Actor_cooldown_and_range()
    {
        var d = TestData.Load();
        var a = new NetActor(2, d);
        Assert.False(NetRules.InRange(a, 0, 0, 0, 100)); // kare gelmeden istek yok
        a.Last = new PlayerFrame { X = 1, Y = 0, Z = 1 };
        a.LastTime = 0;
        Assert.True(NetRules.InRange(a, 4, 0, 1, 3.5f));
        Assert.False(NetRules.InRange(a, 9, 0, 1, 3.5f));
        Assert.True(a.Cooldown(NetReq.Mow, 1.0, 0.15f));
        Assert.False(a.Cooldown(NetReq.Mow, 1.1, 0.15f));
        Assert.True(a.Cooldown(NetReq.Mow, 1.16, 0.15f));
        // Baslangic cantasi tek oyuncudakiyle ayni (tool.json startItems).
        var s = new GameState(d);
        Assert.Equal(NetInventory.Totals(s.Backpack), NetInventory.Totals(a.Bag));
    }

    [Fact]
    public void Inventory_section_reconciles_minimally()
    {
        var d = TestData.Load();
        var host = new Inventory(d.Tool.BackpackSlots, d.Tool.StackOf);
        host.Add("stone", 7); host.Add("wood", 60); host.Add("turnip", 3, 2); host.Add("turnip", 1, 3);
        var client = new Inventory(d.Tool.BackpackSlots, d.Tool.StackOf);
        client.Add("wood", 5); client.Add("clay", 4); client.Add("turnip", 2, 2);
        int changes = 0;
        client.Changed += (_, _) => changes++;
        var w = new NetWriter();
        NetInventory.Write(w, host);
        NetInventory.Apply(client, NetInventory.ReadTotals(new NetReader(w.ToArray())));
        Assert.Equal(NetInventory.Totals(host), NetInventory.Totals(client));
        Assert.Equal(0, client.Count("clay"));
        Assert.Equal(1, client.Count("turnip", 3));
        int after = changes;
        NetInventory.Apply(client, NetInventory.ReadTotals(new NetReader(w.ToArray()))); // ayni girdi: degisiklik yok
        Assert.Equal(after, changes);
        // Yuva sirasi ozete girmez.
        var other = new Inventory(d.Tool.BackpackSlots, d.Tool.StackOf);
        other.Add("turnip", 1, 3); other.Add("wood", 60); other.Add("turnip", 3, 2); other.Add("stone", 7);
        var w2 = new NetWriter();
        NetInventory.Write(w2, other);
        Assert.Equal(NetHash.Of(w.Span), NetHash.Of(w2.Span));
    }

    sealed class Counter : INetSection
    {
        public int Value;
        public int Applied;
        public string Id => "counter";
        public bool PerPeer => false;
        public void Write(NetWriter w, int peer) => w.VarI(Value);
        public void Read(NetReader r) { Value = r.VarI(); Applied++; }
    }

    [Fact]
    public void Section_set_sends_only_changes_per_peer_and_digest_matches()
    {
        var set = new NetSectionSet();
        var host = new Counter { Value = 4 };
        set.Register(host);
        Assert.Throws<InvalidOperationException>(() => set.Register(new Counter()));
        Assert.True(set.Changed(host, 1, out var p1, out var h1));
        Assert.False(set.Changed(host, 1, out _, out _));
        Assert.True(set.Changed(host, 2, out _, out _)); // ikinci es de bir kez alir
        host.Value = 5;
        Assert.True(set.Changed(host, 1, out var p2, out var h2));
        Assert.NotEqual(h1, h2);
        set.Forget(1);
        Assert.True(set.Changed(host, 1, out _, out _)); // yeniden esleme
        // Istemci uygular; ayni yazimla ozeti host'unkiyle ayni.
        var client = new Counter();
        var cset = new NetSectionSet();
        cset.Register(client);
        client.Read(new NetReader(p2));
        Assert.Equal(set.Digest(1), cset.Digest(1));
        // Digest paketi gidip gelir.
        var pkt = NetPackets.Digest(set.Digest(1));
        var r = new NetReader(pkt); Assert.Equal((byte)NetMsg.Digest, r.U8());
        Assert.Equal(set.Digest(1), NetPackets.ReadDigest(r));
    }

    [Fact]
    public void Downed_coop_revive_with_slap()
    {
        var def = Net.Downed;
        var s = new DownedState();
        Assert.Equal(DownedRules.Outcome.Downed, DownedRules.OnZeroHp(s, coop: true, def));
        Assert.True(s.Down);
        Assert.Equal(30f, s.Left);
        // 1 sn tut, birak: birikim sifirlanir.
        for (int i = 0; i < 20; i++) Assert.False(DownedRules.Revive(s, 0.05f, holding: true, inRange: true, def));
        DownedRules.Revive(s, 0.05f, holding: false, inRange: true, def);
        Assert.Equal(0f, s.Hold);
        // Uzaktan tutmak saymaz.
        for (int i = 0; i < 40; i++) Assert.False(DownedRules.Revive(s, 0.05f, true, inRange: false, def));
        // 1,5 sn yakinda tut: kalkar, %30 can.
        bool up = false;
        for (int i = 0; i < 30 && !up; i++) up = DownedRules.Revive(s, 0.05f, true, true, def);
        Assert.True(up);
        Assert.False(s.Down);
        Assert.Equal(30f, DownedRules.ReviveHp(100f, def), 3);
    }

    [Fact]
    public void Downed_expires_after_30s_and_single_player_second_wind_once_per_night()
    {
        var def = Net.Downed;
        var s = new DownedState();
        DownedRules.OnZeroHp(s, true, def);
        bool dead = false;
        float t = 0;
        while (!dead && t < 40f) { dead = DownedRules.Tick(s, 0.1f); t += 0.1f; }
        Assert.True(dead);
        Assert.InRange(t, 29.9f, 30.2f);

        var solo = new DownedState();
        Assert.Equal(DownedRules.Outcome.SecondWind, DownedRules.OnZeroHp(solo, false, def));
        Assert.Equal(40f, DownedRules.SecondWindHp(100f, def), 3);
        Assert.Equal(DownedRules.Outcome.Dead, DownedRules.OnZeroHp(solo, false, def));
        DownedRules.OnNightStarted(solo);
        Assert.Equal(DownedRules.Outcome.SecondWind, DownedRules.OnZeroHp(solo, false, def));
    }

    [Fact]
    public void Voice_gain_curve_and_codec()
    {
        var v = Net.Voice;
        Assert.Equal(1f, NetVoice.Gain(0f, false, v));
        Assert.Equal(1f, NetVoice.Gain(v.FullVolumeDistance, false, v));
        Assert.Equal(0f, NetVoice.Gain(v.MaxDistance + 1, false, v));
        float mid = NetVoice.Gain((v.FullVolumeDistance + v.MaxDistance) / 2, false, v);
        Assert.InRange(mid, 0.1f, 0.5f);
        Assert.True(NetVoice.Gain(10, false, v) > NetVoice.Gain(20, false, v)); // azalan
        Assert.Equal(v.RadioGain, NetVoice.Gain(500f, true, v));                // telsiz uzakliktan bagimsiz

        // mu-law: 8 bitte konusma icin yeterli dogruluk (genligin ~%3'u).
        var pcm = new float[v.FrameSamples];
        for (int i = 0; i < pcm.Length; i++) pcm[i] = 0.6f * MathF.Sin(i * 0.07f);
        var back = NetVoice.Decode(NetVoice.Encode(pcm));
        for (int i = 0; i < pcm.Length; i++) Assert.InRange(MathF.Abs(back[i] - pcm[i]), 0f, 0.03f);
        Assert.True(NetVoice.Gate(pcm, v));
        Assert.False(NetVoice.Gate(new float[64], v));

        var pkt = NetVoice.Packet(4, true, 77, NetVoice.CodecMuLaw, NetVoice.Encode(pcm));
        var r = new NetReader(pkt); Assert.Equal((byte)NetMsg.Voice, r.U8());
        var (peer, radio, seq, codec, data) = NetVoice.Read(r);
        Assert.Equal(4, peer); Assert.True(radio); Assert.Equal(77, seq); Assert.Equal(NetVoice.CodecMuLaw, codec);
        Assert.Equal(pcm.Length, data.Length);
        Assert.Equal(16000 * 20 / 1000, v.FrameSamples);
        Assert.Equal(160, NetVoice.Resample(new float[480], 48000, 16000).Length);
    }

    [Fact]
    public void Coop_cards_do_not_slow_time_and_team_cards_take_highest_rank()
    {
        Assert.Equal(1f, NetCards.SelectionTimeScale(true, 0.3f));
        Assert.Equal(0.3f, NetCards.SelectionTimeScale(false, 0.3f));
        var a = new Dictionary<string, int> { ["bereket"] = 2, ["catal"] = 4 };
        var b = new Dictionary<string, int> { ["bereket"] = 3 };
        var team = NetCards.TeamRanks(new[] { a, b }, id => id == "bereket");
        Assert.Equal(3, team["bereket"]); // yigilmaz: 2+3 degil, en yuksek
        Assert.False(team.ContainsKey("catal"));
        var w = new NetWriter(); NetCards.Write(w, a);
        Assert.Equal(a, NetCards.Read(new NetReader(w.ToArray())));
    }

    [Fact]
    public void Roster_roundtrip()
    {
        var list = new[] { new NetRosterEntry { Peer = 0, Name = "Host", Host = true, Ready = true }, new NetRosterEntry { Peer = 3, Name = "Bot 3" } };
        var r = new NetReader(NetPackets.Roster(list));
        Assert.Equal((byte)NetMsg.Roster, r.U8());
        var back = NetPackets.ReadRoster(r);
        Assert.Equal(2, back.Count);
        Assert.True(back[0].Host); Assert.Equal("Bot 3", back[1].Name); Assert.False(back[1].Ready);
    }
}
