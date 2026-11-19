namespace Kabarma.Sim;

/// <summary>
/// Ortak oyun protokolu (TEKNIK 5): her paket bir <see cref="NetMsg"/> baytiyla baslar. Surum <see cref="NetFile.Protocol"/>;
/// uyusmayan istemci Hello'da geri cevrilir. Kanallar: 0 guvenilir sirali (istek, bolum, olay), 1 guvenilmez (anlik
/// goruntu, oyuncu karesi), 2 guvenilmez (ses).
/// </summary>
public enum NetMsg : byte
{
    /// <summary>istemci→host: surum, ad, dunya ozeti.</summary>
    Hello = 1,
    /// <summary>host→istemci: es kimligi, tohum.</summary>
    Welcome,
    /// <summary>host→istemci: neden (surum, dunya, dolu).</summary>
    Reject,
    /// <summary>host→herkes: oyuncu listesi.</summary>
    Roster,
    /// <summary>istemci→host: kendi oyuncusunun karesi (TEKNIK 5.2: sahibi yetkili, host denetler).</summary>
    Frame,
    /// <summary>host→istemci: oteki oyuncular ve yaratiklar.</summary>
    Snapshot,
    /// <summary>istemci→host: eylem istegi (<see cref="NetReq"/>).</summary>
    Request,
    /// <summary>host→istemci: bir bolumun tamami (katilma, degisim, yeniden esleme).</summary>
    Section,
    /// <summary>host→istemci: bolum ozetleri (TEKNIK 5.3: 10 sn).</summary>
    Digest,
    /// <summary>istemci→host: ozeti tutmayan bolumu yeniden iste.</summary>
    Resync,
    /// <summary>host→istemci: kabul edilmeyen konum (hile denetimi), istemci buraya doner.</summary>
    Correction,
    /// <summary>istemci→host→herkes: eldeki esya, baslik (degisince).</summary>
    Look,
    /// <summary>ses cercevesi (kanal 2).</summary>
    Voice,
    /// <summary>host→baygin istemci: sok tokadiyla kalktin.</summary>
    Revived,
    /// <summary>istemci→host: hazirim (katilma bitti) / sinama raporu.</summary>
    Ready,
    /// <summary>sinama: istemci son durum raporu.</summary>
    Report,
    /// <summary>sinama: host son durumu; istemciler durur.</summary>
    Final,
    /// <summary>kibar ayrilma.</summary>
    Bye,
    /// <summary>host→herkes: anlik olay (efekt, ses) — yalniz gorunus, durum degil.</summary>
    Fx,
    /// <summary>host→istemci: anlik goruntuye sigmayan yaratik kareleri (paket MTU altinda kalsin; TEKNIK 5.3).</summary>
    Creatures,
    /// <summary>host→istemci: dunya devri (M5 sekiz). Devralana "sen host olacaksin" (port), ota istemcilere "yeni host sunda" (es, port).</summary>
    Handover,
}

/// <summary>Istemci istekleri (TEKNIK 5.5: mesafe, bekleme suresi ve envanter host'ta dogrulanir).</summary>
public enum NetReq : byte
{
    Mine = 1,
    Mow,
    Place,
    Remove,
    Repair,
    PlaceItem,
    Till,
    Plant,
    Water,
    ChestMove,
    StationQueue,
    ReviveHold,
    Card,
    Eat,
}

public enum NetChannel : byte { Reliable = 0, State = 1, Voice = 2 }

[Flags]
public enum NetFrameFlags : byte
{
    None = 0,
    Sprint = 1,
    Down = 2,
    Firing = 4,
    OnFloor = 8,
    Talking = 16,
    Carrying = 32,
    Pushing = 64,
    Slapping = 128,
}

/// <summary>
/// Oyuncu karesi, 16 bayt (TEKNIK 5.3): es (1) + konum 3×24 bit (9) + govde yonu (2) + bakis egimi (1) +
/// animasyon kipi (1) + Capa basligi (1) + bayraklar (1). Eldeki esya degisince ayri <see cref="NetMsg.Look"/> ile gider.
/// </summary>
public struct PlayerFrame
{
    public byte Peer;
    public float X, Y, Z;
    public float Yaw;
    public float Pitch;
    public byte Anim;
    public byte Head;
    public NetFrameFlags Flags;

    public const int Size = 16;

    public void Write(NetWriter w, int perMetre)
    {
        w.U8(Peer);
        w.Pos(X, Y, Z, perMetre);
        w.Angle16(Yaw);
        w.I8(NetQuant.Pitch(Pitch));
        w.U8(Anim);
        w.U8(Head);
        w.U8((byte)Flags);
    }

    public static PlayerFrame Read(NetReader r, int perMetre)
    {
        var f = new PlayerFrame { Peer = r.U8() };
        (f.X, f.Y, f.Z) = r.Pos(perMetre);
        f.Yaw = r.Angle16();
        f.Pitch = NetQuant.Unpitch(r.I8());
        f.Anim = r.U8();
        f.Head = r.U8();
        f.Flags = (NetFrameFlags)r.U8();
        return f;
    }

    public bool Has(NetFrameFlags f) => (Flags & f) != 0;
}

/// <summary>Yaratik karesi, 13 bayt: kimlik (2) + konum (9) + yon (1) + durum (1). TEKNIK 5.3'te 10 B yaziyordu; 1 cm olcutu icin 24 bit konum.</summary>
public struct CreatureFrame
{
    public ushort Id;
    public float X, Y, Z;
    public float Yaw;
    public byte State;

    public const int Size = 13;

    public void Write(NetWriter w, int perMetre)
    {
        w.U16(Id);
        w.Pos(X, Y, Z, perMetre);
        w.Angle8(Yaw);
        w.U8(State);
    }

    public static CreatureFrame Read(NetReader r, int perMetre)
    {
        var f = new CreatureFrame { Id = r.U16() };
        (f.X, f.Y, f.Z) = r.Pos(perMetre);
        f.Yaw = r.Angle8();
        f.State = r.U8();
        return f;
    }
}

/// <summary>Oyuncu listesi satiri.</summary>
public sealed class NetRosterEntry
{
    public byte Peer;
    public string Name = "";
    public bool Host;
    public bool Ready;

    public void Write(NetWriter w) { w.U8(Peer); w.Str(Name); w.Bool(Host); w.Bool(Ready); }
    public static NetRosterEntry Read(NetReader r) => new() { Peer = r.U8(), Name = r.Str(), Host = r.Bool(), Ready = r.Bool() };
}

/// <summary>Mesaj kurucu yardimcilari (ilk bayt mesaj turu).</summary>
public static class NetPackets
{
    public static NetWriter Begin(NetMsg m, int capacity = 64)
    {
        var w = new NetWriter(capacity);
        w.U8((byte)m);
        return w;
    }

    public static byte[] Hello(int protocol, string name, ulong worldHash)
    {
        var w = Begin(NetMsg.Hello);
        w.U16((ushort)protocol); w.Str(name); w.U64(worldHash);
        return w.ToArray();
    }

    public static byte[] Welcome(byte peer, ulong seed, float hostTime)
    {
        var w = Begin(NetMsg.Welcome);
        w.U8(peer); w.U64(seed); w.F32(hostTime);
        return w.ToArray();
    }

    public static byte[] Reject(string reason)
    {
        var w = Begin(NetMsg.Reject);
        w.Str(reason);
        return w.ToArray();
    }

    public static byte[] Roster(IEnumerable<NetRosterEntry> list)
    {
        var w = Begin(NetMsg.Roster);
        var l = list.ToList();
        w.U8((byte)l.Count);
        foreach (var e in l) e.Write(w);
        return w.ToArray();
    }

    public static List<NetRosterEntry> ReadRoster(NetReader r)
    {
        int n = r.U8();
        var l = new List<NetRosterEntry>(n);
        for (int i = 0; i < n; i++) l.Add(NetRosterEntry.Read(r));
        return l;
    }

    public static byte[] Section(string id, ulong hash, ReadOnlySpan<byte> payload)
    {
        var w = Begin(NetMsg.Section, payload.Length + 24);
        w.Str(id); w.U64(hash); w.Blob(payload);
        return w.ToArray();
    }

    public static byte[] Digest(IEnumerable<(string id, ulong hash)> items)
    {
        var w = Begin(NetMsg.Digest);
        var l = items.ToList();
        w.U8((byte)l.Count);
        foreach (var (id, h) in l) { w.Str(id); w.U64(h); }
        return w.ToArray();
    }

    public static List<(string id, ulong hash)> ReadDigest(NetReader r)
    {
        int n = r.U8();
        var l = new List<(string, ulong)>(n);
        for (int i = 0; i < n; i++) l.Add((r.Str(), r.U64()));
        return l;
    }

    public static byte[] Frame(PlayerFrame f, int perMetre)
    {
        var w = Begin(NetMsg.Frame, 24);
        f.Write(w, perMetre);
        return w.ToArray();
    }
}
