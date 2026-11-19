namespace Kabarma.Sim;

// sekiz (M5): 8 oyuncuya cikis (ilgi alani) ve dunya devri (TEKNIK 5.3, 5.4). Duz kurallar; tasima NetSync'te.

/// <summary>Bir istemcinin devir icin adayi: es kimligi, hazir mi, ne kadar suredir katilmis, baygin mi.</summary>
public readonly record struct HandoverCandidate(byte Peer, bool Ready, float JoinedSeconds, bool Down, int Latency = 0);

public static class NetInterest
{
    /// <summary>Iki oyuncu arasi mesafeye gore kare bolgeni (1: her kare, 2: 10 Hz, 4: 5 Hz).</summary>
    public static int Divisor(NetInterestDef d, float distance, bool talkingOrDown)
    {
        if (talkingOrDown && d.AlwaysFullWhenTalkingOrDown) return 1;
        if (distance <= d.NearRadius) return 1;
        return distance <= d.MidRadius ? Math.Max(1, d.MidDivisor) : Math.Max(1, d.FarDivisor);
    }

    /// <summary>Bu tikte (host tik sayaci) kare gonderilir mi. Alici basina farkli evre: oyuncular ayni tikte yigilmasin.</summary>
    public static bool SendThisTick(uint tick, int divisor, byte peer, byte other) => divisor <= 1 || (tick + peer + other) % (uint)divisor == 0;

    /// <summary>Etkin hz (sinama ve gosterge).</summary>
    public static float Hz(NetFile cfg, int divisor) => cfg.FrameHz / Math.Max(1, divisor);

    /// <summary>
    /// Host baska yaratik/oyuncu sayisiyla tum istemcilere yazdigi kare sayisi (saniyede): her alici icin her uzak oyuncunun
    /// kare hizi. En kotu (herkes ayri uzakta) ve en iyi (herkes yakinda) durum, sinama icin.
    /// </summary>
    public static (float best, float worst) FramesPerSecond(NetFile cfg, int players)
    {
        int pairs = Math.Max(0, players - 1) * Math.Max(0, players - 1); // host dahil: her istemciye (n-1) oyuncu
        float full = cfg.FrameHz, far = cfg.FrameHz / Math.Max(1, cfg.Interest.FarDivisor);
        return (pairs * far, pairs * full);
    }
}

public static class NetHandover
{
    /// <summary>
    /// Devralacak istemci: hazir, yeterince suredir katilmis, baygin olmayan; aralarinda en uzun suredir bagli olan
    /// (esitlikte dusuk gecikme, sonra kucuk es kimligi). Uygun aday yoksa null (devir yapilamaz, oturum biter).
    /// </summary>
    public static byte? Successor(NetHandoverDef d, IEnumerable<HandoverCandidate> candidates)
    {
        var ok = candidates.Where(c => c.Ready && !c.Down && c.JoinedSeconds >= d.MinJoinedSeconds)
            .OrderByDescending(c => c.JoinedSeconds).ThenBy(c => c.Latency).ThenBy(c => c.Peer).ToList();
        return ok.Count == 0 ? null : ok[0].Peer;
    }

    /// <summary>Devir mesaji: devralana (role 1) yalniz port; oteki istemcilere (role 2) yeni host'un es kimligi ve adresi.</summary>
    public static byte[] Pack(byte role, byte successor, string address, int port)
    {
        var w = NetPackets.Begin(NetMsg.Handover, 24);
        w.U8(role); w.U8(successor); w.Str(address); w.U16((ushort)port);
        return w.ToArray();
    }

    public static (byte role, byte successor, string address, int port) Unpack(NetReader r) => (r.U8(), r.U8(), r.Str(), r.U16());

    /// <summary>Devirden sonra yeni host'ta es kimlikleri: eski host dusunce 0 bosalir; devralan 0 olur, oteki istemciler yeniden Welcome alir.</summary>
    public static int NewPort(NetHandoverDef d, int oldPort) => oldPort + Math.Max(1, d.PortOffset);
}