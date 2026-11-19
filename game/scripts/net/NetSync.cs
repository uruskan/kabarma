using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ortak oyunun merkezi katmani (GDD 22.1, TEKNIK 5). Host yetkili: istemci kendi hareketini ve isteklerini gonderir,
/// host Sim'i ve dunyayi yurutur, degisenleri bolum (INetSection) olarak, oyuncu ve yaratiklari anlik goruntuyle yollar.
/// Tek oyuncuda yoktur (Director.Net null) ve hicbir kanca bir sey yapmaz.
/// Parcalar: NetSync.World.cs (kaya, tutam, yapi, yerlestirilebilir, canta; Kazma/Bicme/Insa istekleri),
/// NetSync.Players.cs (oyuncu karesi, avatarlar, duzeltme), NetSync.Downed.cs (bayilma, sok tokadi),
/// NetSync.Creatures.cs, NetSync.Voice.cs. Baglanma rehberi: CLAUDE.md "M2 akislari" → ortak oyun.
/// Komut satiri: --coop=host|client [--connect=IP] [--port=N] [--transport=enet|steam] [--name=Ad].
/// </summary>
public partial class NetSync : Node
{
    public readonly Director D;
    public NetFile Cfg => D.Data.Net;
    public bool IsHost { get; private set; }
    public bool IsClient => !IsHost;
    /// <summary>Bu surecin es kimligi: host 0, istemciler 1..7 (Welcome ile gelir).</summary>
    public byte LocalPeer { get; private set; }
    /// <summary>Istemci: host'a katildi (Welcome alindi). Host: hep true.</summary>
    public bool Joined { get; private set; }
    public string LocalName { get; private set; } = "Oyuncu";
    public ITransport Transport => _tx;
    /// <summary>Kullaniciya durum (panel, oyuncu listesi): "bağlanıyor", "bağlantı koptu"...</summary>
    public string Status { get; private set; } = "";
    public bool Failed { get; private set; }

    ITransport _tx;
    readonly Dictionary<int, byte> _byConn = new();
    readonly Dictionary<byte, int> _conn = new();
    /// <summary>Host: uzak oyuncularin yetkili durumu (canta, son kare). Host'un kendisi burada yok.</summary>
    public readonly Dictionary<byte, NetActor> Actors = new();
    public List<NetRosterEntry> Roster { get; private set; } = new();
    public readonly NetSectionSet Sections = new();
    readonly Dictionary<string, Action<NetActor, NetReader>> _handlers = new();
    readonly List<NetEvent> _events = new();
    double _frameAcc, _sectionAcc, _digestAcc, _lastHeard, _startT;
    uint _tick;

    /// <summary>Sayaclar (oyuncu listesi, sinama).</summary>
    public int SectionsApplied, SectionMismatches, Resyncs, RequestsSent, RequestsRejected, Corrections;
    public event Action RosterChanged;
    /// <summary>Istemci: host'tan sinama mesaji (CoopTest).</summary>
    public event Action<NetMsg, NetReader> TestMessage;
    /// <summary>Host: istemciden sinama mesaji (es, tur, okuyucu).</summary>
    public event Action<byte, NetMsg, NetReader> TestMessageFrom;

    /// <summary>Adanmis sunucu (--server, M5 sunucu akisi): host oyuncu degildir (karakteri gizli, listede sayilmaz).</summary>
    public ServerNet Srv { get; private set; }
    public bool Dedicated => Main.Args.ContainsKey("server");
    /// <summary>Kabul edilen toplam es (host dahil): normal oyunda net.json, adanmis sunucuda server.json maxPlayers + 1.</summary>
    public int SlotLimit => Dedicated ? D.Data.Server.MaxPlayers + 1 : Cfg.MaxPlayers;
    /// <summary>Adanmis sunucunun katilma denetimi (ad, sifre, adres): null kabul, aksi reddetme nedeni (ServerRunner baglar).</summary>
    public Func<string, string, string, string> JoinGate;

    /// <summary>Host: oyuncuyu nedeniyle atar (istemci "host reddetti: neden" gorur). Bulunamazsa false.</summary>
    public bool KickPeer(byte peer, string reason)
    {
        if (!IsHost || !_conn.TryGetValue(peer, out var c)) return false;
        _tx.Send(c, NetPackets.Reject(reason), NetChannel.Reliable);
        _tx.Kick(c);
        return true;
    }

    public string PeerAddress(byte peer) => _conn.TryGetValue(peer, out var c) ? _tx.PeerAddress(c) : "";

    public NetSync(Director d) { D = d; }
    public NetSync() { }

    /// <summary>Gercek saat (sn): TimeScale ve --fast'ten etkilenmez.</summary>
    public static double Now => Time.GetTicksUsec() / 1_000_000.0;

    /// <summary>Komut satirinda --coop varsa katmani kurar; yoksa null (tek oyuncu).</summary>
    public static NetSync TryCreate(Director d)
    {
        if (!Main.Args.TryGetValue("coop", out var role)) return null;
        var n = new NetSync(d) { Name = "Net" };
        d.AddChild(n);
        n.Start(role == "host");
        return n;
    }

    void Start(bool host)
    {
        IsHost = host;
        Joined = host;
        LocalName = Main.Args.TryGetValue("name", out var nm) ? nm : host ? "Host" : $"Oyuncu {OS.GetProcessId() % 1000}";
        int port = Main.Args.TryGetValue("port", out var ps) && int.TryParse(ps, out var pp) ? pp : Cfg.Port;
        string transport = Main.Args.TryGetValue("transport", out var tr) ? tr : "enet";
        string err;
        if (transport == "steam") _tx = SteamTransport.Create(this, host, out err);
        else if (host) _tx = EnetTransport.Host(port, SlotLimit - 1, out err);
        else _tx = EnetTransport.Connect(Main.Args.TryGetValue("connect", out var ip) ? ip : "127.0.0.1", port, out err);
        _startT = _lastHeard = Now;
        RegisterWorld();    // NetSync.World.cs
        RegisterPlayers();  // NetSync.Players.cs
        RegisterDowned();   // NetSync.Downed.cs
        RegisterCreatures(); // NetSync.Creatures.cs
        RegisterVoice();    // NetSync.Voice.cs
        RegisterFarm();     // NetSync.Farm.cs
        RegisterStations(); // NetSync.Stations.cs
        RegisterSpawns();   // NetSync.Spawns.cs
        Srv = new ServerNet(this); // sunucu duyurusu ve yonetici cevabi (server/ServerNet.cs)
        CallDeferred(nameof(RegisterSaveBridge)); // NetSync.Save.cs: butun _Ready'lerden sonra (kayit bolumleri eklenmis olsun)
        if (_tx == null) { Fail(err ?? "taşıma kurulamadı"); return; }
        Status = host ? $"host · {_tx.Address}" : $"bağlanıyor · {_tx.Address}";
        if (host) RebuildRoster();
        GD.Print($"NET {(host ? "host" : "client")} {transport} {_tx.Address} name={LocalName}");
    }

    void Fail(string why)
    {
        if (Failed) return; // ilk neden kalir (Reject, ardindan gelen baglanti kopmasi onu ezmesin)
        Failed = true;
        Status = why;
        GD.PrintErr($"NET fail {why}");
    }

    // ---------------------------------------------------------------- baglanma rehberi API'si

    /// <summary>
    /// Istemcide eylemi host'a istek olarak gonderir ve true doner (cagiran yerel degisikligi yapmadan cikar);
    /// host'ta ve tek oyuncuda false (normal yol calisir). Kanca: <c>if (_d.Net?.Forward("x", w => ...) == true) return;</c>
    /// </summary>
    public bool Forward(string request, Action<NetWriter> payload)
    {
        if (IsHost) return false;
        if (!Joined) return true; // katilmadan dunyaya dokunulmaz
        var w = NetPackets.Begin(NetMsg.Request, 48);
        w.Str(request);
        payload?.Invoke(w);
        _tx.Send(0, w.ToArray(), NetChannel.Reliable);
        RequestsSent++;
        return true;
    }

    /// <summary>Host: bir istegin isleyicisi. Isleyici aktoru (canta, konum) dogrular, sonra sistemin genel girisini cagirir.</summary>
    public void On(string request, Action<NetActor, NetReader> handler) => _handlers[request] = handler;

    /// <summary>Durumu cogaltilacak bir bolum (canta, yapi, tarla...). Host yazar, istemci uygular, ozet esitligi denetlenir.</summary>
    public void Register(INetSection s) => Sections.Register(s);

    /// <summary>Hazir kucuk bolum: iki lambda ile.</summary>
    public void Register(string id, bool perPeer, Action<NetWriter, int> write, Action<NetReader> read) =>
        Sections.Register(new LambdaSection(id, perPeer, write, read));

    sealed class LambdaSection : INetSection
    {
        readonly Action<NetWriter, int> _w;
        readonly Action<NetReader> _r;
        public string Id { get; }
        public bool PerPeer { get; }
        public LambdaSection(string id, bool perPeer, Action<NetWriter, int> w, Action<NetReader> r) { Id = id; PerPeer = perPeer; _w = w; _r = r; }
        public void Write(NetWriter w, int peer) => _w(w, peer);
        public void Read(NetReader r) => _r(r);
    }

    /// <summary>Butun oyuncularin konumu (kendisi dahil): yaratik hedefi, Kabarma olcegi, ses. (es, konum, baygin mi).</summary>
    public IEnumerable<(byte peer, Vector3 pos, bool down)> Players()
    {
        if (!Dedicated) yield return (LocalPeer, D.Player.GlobalPosition, D.Player.Down);
        foreach (var (peer, av) in Avatars) yield return (peer, av.GlobalPosition, av.Down);
    }

    /// <summary>Oyuncu sayisi (Kabarma ve Ata cani ekip boyuna gore olcekler, GDD 22.1).</summary>
    public int PlayerCount => IsHost ? (Dedicated ? 0 : 1) + Actors.Count : Math.Max(1, Roster.Count(e => !(e.Host && e.Name.StartsWith("[Sunucu]"))));

    // ---------------------------------------------------------------- kare

    public override void _Process(double delta)
    {
        if (_tx == null) return;
        double now = Now;
        float dt = (float)delta / Mathf.Max(0.01f, (float)Engine.TimeScale); // gercek saat
        _events.Clear();
        _tx.Poll(_events);
        foreach (var ev in _events)
        {
            try { Handle(ev, now); }
            catch (NetFormatException e) { GD.PushWarning($"NET bozuk mesaj peer={ev.Peer}: {e.Message}"); }
        }
        if (IsClient && !Joined && now - _startT > Cfg.ConnectSeconds && !Failed) Fail("bağlanılamadı (zaman aşımı)");
        if (IsClient && Joined && now - _lastHeard > Cfg.TimeoutSeconds && !Failed) Fail("host'tan ses yok (zaman aşımı)");

        _frameAcc += dt; _sectionAcc += dt; _digestAcc += dt;
        if (_frameAcc >= 1.0 / Cfg.FrameHz)
        {
            _frameAcc %= 1.0 / Cfg.FrameHz;
            _tick++;
            TickFrame(now);                  // NetSync.Players.cs: kendi karem; host: anlik goruntuler
            if (IsHost) TickCreatures(now);  // NetSync.Creatures.cs
        }
        TickWorld(dt);                       // NetSync.World.cs: istemci kazma birikimi
        TickFarm(dt);                        // NetSync.Farm.cs
        TickStations(dt);                    // NetSync.Stations.cs
        TickDowned(dt, now);                 // NetSync.Downed.cs
        TickVoice(dt);                       // NetSync.Voice.cs
        TickSaveBridge(dt);                  // NetSync.Save.cs: kayit bolumleri saniyede bir
        if (IsHost && (_sectionAcc >= 1.0 / Cfg.SectionHz || _saveTick)) { _sectionAcc = 0; SendSections(); }
        if (IsHost && _digestAcc >= Cfg.DigestSeconds) { _digestAcc = 0; SendDigests(); }
    }

    public override void _ExitTree()
    {
        if (_tx == null) return;
        if (IsClient && Joined) _tx.Send(0, new[] { (byte)NetMsg.Bye }, NetChannel.Reliable);
        var none = new List<NetEvent>();
        _tx.Poll(none);
        _tx.Dispose();
        _tx = null;
    }

    void Handle(NetEvent ev, double now)
    {
        switch (ev.Kind)
        {
            case NetEventKind.Connected:
                if (IsClient) { _tx.Send(0, NetPackets.Hello(Cfg.Protocol, Main.Args.TryGetValue("password", out var pw) && pw.Length > 0 ? LocalName + "\u001f" + pw : LocalName, WorldHash()), NetChannel.Reliable); Status = "katılıyor"; }
                return;
            case NetEventKind.Disconnected:
                if (IsHost) DropPeer(ev.Peer, "ayrıldı");
                else { Joined = false; Fail("bağlantı koptu"); }
                return;
        }
        if (ev.Data == null || ev.Data.Length == 0) return;
        var r = new NetReader(ev.Data);
        var msg = (NetMsg)r.U8();
        if (IsHost) HostMessage(ev.Peer, msg, r, now);
        else { _lastHeard = now; ClientMessage(msg, r, now); }
    }

    // ---------------------------------------------------------------- host

    void HostMessage(int conn, NetMsg msg, NetReader r, double now)
    {
        if (msg == NetMsg.Hello) { Accept(conn, r); return; }
        if (!_byConn.TryGetValue(conn, out var peer) || !Actors.TryGetValue(peer, out var a)) return;
        switch (msg)
        {
            case NetMsg.Frame: OnFrame(a, PlayerFrame.Read(r, Cfg.PosPerMetre), now); break; // NetSync.Players.cs
            case NetMsg.Look: OnLook(a, r); break;
            case NetMsg.Ready:
                a.Ready = true;
                Sections.Forget(peer); // butun bolumler bastan (sonradan katilma)
                RebuildRoster();
                GD.Print($"NET peer {peer} ready ({a.Name})");
                break;
            case NetMsg.Request:
            {
                string id = r.Str();
                if (_handlers.TryGetValue(id, out var h))
                {
                    int before = a.Rejected;
                    h(a, r);
                    if (a.Rejected > before) RequestsRejected++;
                }
                else { a.Rejected++; RequestsRejected++; GD.PushWarning($"NET bilinmeyen istek '{id}' peer={peer}"); }
                break;
            }
            case NetMsg.Resync: { string id = r.Str(); Sections.Forget(peer, id); Resyncs++; break; }
            case NetMsg.Voice: OnVoice(peer, r, relay: true); break; // NetSync.Voice.cs
            case NetMsg.Bye: DropPeer(conn, "ayrıldı"); break;
            default: TestMessageFrom?.Invoke(peer, msg, r); break;
        }
    }

    void Accept(int conn, NetReader r)
    {
        int proto = r.U16();
        string name = r.Str();
        string password = "";
        int sep = name.IndexOf('\u001f');
        if (sep >= 0) { password = name[(sep + 1)..]; name = name[..sep]; }
        ulong world = r.U64();
        string why = proto != Cfg.Protocol ? $"sürüm farklı ({proto} ≠ {Cfg.Protocol})"
            : world != WorldHash() ? "dünya farklı (sürüm ya da tohum)"
            : Actors.Count >= SlotLimit - 1 ? "oyun dolu" : JoinGate?.Invoke(name, password, _tx.PeerAddress(conn));
        if (why != null)
        {
            GD.Print($"NET reject conn={conn} {why}");
            _tx.Send(conn, NetPackets.Reject(why), NetChannel.Reliable);
            _tx.Kick(conn);
            return;
        }
        byte peer = 1;
        while (Actors.ContainsKey(peer)) peer++;
        var a = new NetActor(peer, D.Data) { Name = string.IsNullOrWhiteSpace(name) ? $"Oyuncu {peer}" : name };
        Actors[peer] = a;
        _byConn[conn] = peer;
        _conn[peer] = conn;
        _tx.Send(conn, NetPackets.Welcome(peer, D.State.Seed, (float)(Now - _startT)), NetChannel.Reliable);
        SpawnAvatar(peer, a.Name); // NetSync.Players.cs
        RebuildRoster();
        GD.Print($"NET join peer={peer} name={a.Name} players={PlayerCount}");
    }

    void DropPeer(int conn, string why)
    {
        if (!_byConn.Remove(conn, out var peer)) return;
        _conn.Remove(peer);
        Actors.Remove(peer);
        Sections.Forget(peer);
        DespawnAvatar(peer);
        RebuildRoster();
        GD.Print($"NET leave peer={peer} {why}");
    }

    void RebuildRoster()
    {
        var l = new List<NetRosterEntry> { new() { Peer = 0, Name = LocalName, Host = true, Ready = true } };
        l.AddRange(Actors.Values.OrderBy(a => a.Peer).Select(a => new NetRosterEntry { Peer = a.Peer, Name = a.Name, Ready = a.Ready }));
        Roster = l;
        var pkt = NetPackets.Roster(l);
        foreach (var c in _conn.Values) _tx.Send(c, pkt, NetChannel.Reliable);
        RosterChanged?.Invoke();
    }

    /// <summary>Host: bir ese gonder (es kimligiyle).</summary>
    public void SendTo(byte peer, byte[] data, NetChannel ch = NetChannel.Reliable)
    {
        if (IsHost && _conn.TryGetValue(peer, out var c)) _tx.Send(c, data, ch);
    }

    /// <summary>Host: hazir butun istemcilere (biri haric).</summary>
    public void Broadcast(byte[] data, NetChannel ch = NetChannel.Reliable, int except = -1)
    {
        if (!IsHost) return;
        foreach (var (peer, a) in Actors) if (a.Ready && peer != except) SendTo(peer, data, ch);
    }

    /// <summary>Istemci: host'a gonder.</summary>
    public void SendHost(byte[] data, NetChannel ch = NetChannel.Reliable)
    {
        if (IsClient && _tx != null) _tx.Send(0, data, ch);
    }

    void SendSections()
    {
        foreach (var (peer, a) in Actors)
        {
            if (!a.Ready) continue;
            foreach (var s in Sections.All)
                if ((_saveTick || !_volatile.Contains(s.Id) || !Sections.WasSent(s, peer)) && Sections.Changed(s, peer, out var payload, out var hash))
                    SendTo(peer, NetPackets.Section(s.Id, hash, payload));
        }
    }

    void SendDigests()
    {
        foreach (var (peer, a) in Actors) if (a.Ready) SendTo(peer, NetPackets.Digest(Sections.Digest(peer).Where(d => !_volatile.Contains(d.id))));
    }

    /// <summary>Host: bir bolumun hemen gonderilmesini ister (olay aninda, 5 Hz'i beklemeden).</summary>
    public void Flush() { if (IsHost) SendSections(); }

    // ---------------------------------------------------------------- istemci

    void ClientMessage(NetMsg msg, NetReader r, double now)
    {
        switch (msg)
        {
            case NetMsg.Welcome:
                LocalPeer = r.U8();
                ulong seed = r.U64();
                if (seed != D.State.Seed) { Fail($"tohum farklı {seed} ≠ {D.State.Seed}"); return; }
                Joined = true;
                Status = $"bağlı · {_tx.Address}";
                SendHost(new[] { (byte)NetMsg.Ready });
                GD.Print($"NET welcome peer={LocalPeer}");
                break;
            case NetMsg.Reject: Fail("host reddetti: " + r.Str()); break;
            case NetMsg.Roster:
                Roster = NetPackets.ReadRoster(r);
                SyncAvatars(); // NetSync.Players.cs
                RosterChanged?.Invoke();
                break;
            case NetMsg.Snapshot: OnSnapshot(r, now); break;   // NetSync.Players.cs
            case NetMsg.Creatures: ReadCreatures(r, now); break; // NetSync.Creatures.cs: bolunmus anlik goruntu
            case NetMsg.Look: OnLook(null, r); break;
            case NetMsg.Correction: OnCorrection(r); break;
            case NetMsg.Section:
            {
                string id = r.Str();
                ulong hash = r.U64();
                var payload = r.Blob();
                var s = Sections.Get(id);
                if (s == null) { GD.PushWarning($"NET bilinmeyen bolum '{id}'"); break; }
                s.Read(new NetReader(payload));
                SectionsApplied++;
                if (Sections.Hash(s, LocalPeer) != hash) { SectionMismatches++; if (Main.Args.ContainsKey("netdebug")) GD.Print($"NET section {id} uygulandi ama ozet farkli"); }
                break;
            }
            case NetMsg.Digest:
                foreach (var (id, hash) in NetPackets.ReadDigest(r))
                {
                    var s = Sections.Get(id);
                    if (s != null && Sections.Hash(s, LocalPeer) != hash)
                    {
                        var w = NetPackets.Begin(NetMsg.Resync); w.Str(id);
                        SendHost(w.ToArray());
                        Resyncs++;
                        GD.Print($"NET digest mismatch {id}: yeniden esleme");
                    }
                }
                break;
            case NetMsg.Voice: OnVoice(0, r, relay: false); break;
            case NetMsg.Revived: OnRevived(r); break; // NetSync.Downed.cs
            default: TestMessage?.Invoke(msg, r); break;
        }
    }

    /// <summary>
    /// Dunya ozeti: iki taraf ayni dunyayi kurdu mu (tohum, kaya, agac, tutam). Uyusmayan istemci katilamaz
    /// (TEKNIK 4: tanimlayici nesneyi iki tarafta ayni kurmaya yeter; burada kurulmus dunya karsilastirilir).
    /// </summary>
    public ulong WorldHash()
    {
        ulong h = NetHash.Mix(NetHash.Seed, D.State.Seed);
        h = NetHash.Mix(h, (ulong)_rocks.Count);
        foreach (var rk in _rocks) { h = NetHash.Mix(h, (ulong)NetQuant.Pos(rk.InitPos.X, 64)); h = NetHash.Mix(h, (ulong)NetQuant.Pos(rk.InitPos.Z, 64)); h = NetHash.Mix(h, (ulong)rk.Init); }
        h = NetHash.Mix(h, (ulong)D.World.TreePositions.Count);
        h = NetHash.Mix(h, (ulong)_tufts.Count);
        h = NetHash.Mix(h, (ulong)Cfg.Protocol);
        return h;
    }
}
