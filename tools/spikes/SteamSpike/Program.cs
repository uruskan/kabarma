// M0 adim 3 (TEKNIK.md §1.2): Steamworks.NET ile Steam baslatma, lobi, Networking Sockets ile iki surec arasi mesaj.
// Kullanim: SteamSpike lobby | SteamSpike server | SteamSpike client
// Gecme olcutu: uygulama kimligi 480 ile yerelde mesaj gidip geliyor.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;

const ushort Port = 27020;
const int Pings = 20;
string mode = args.Length > 0 ? args[0] : "lobby";

if (!SteamAPI.Init())
{
    Console.WriteLine("STEAM init FAIL (Steam acik ve oturum acik mi?)");
    return 3;
}
var id = SteamUser.GetSteamID().m_SteamID.ToString();
Console.WriteLine($"STEAM init ok app={SteamUtils.GetAppID()} user=...{id[^4..]} mode={mode}");
SteamNetworkingUtils.InitRelayNetworkAccess();
// Iki surec ayni Steam hesabinda: sertifika hazir olana kadar bekle (Steam'in imzali sertifikasi).
var auth = Stopwatch.StartNew();
ESteamNetworkingAvailability av;
while ((av = SteamNetworkingSockets.GetAuthenticationStatus(out _)) != ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current && auth.ElapsedMilliseconds < 15000) Pump(20);
Console.WriteLine($"STEAM net auth={av} in {auth.ElapsedMilliseconds} ms");

int exit = mode switch
{
    "lobby" => Lobby(),
    "server" => Server(),
    "client" => Client(),
    _ => 2,
};
SteamAPI.Shutdown();
return exit;

static void Pump(int ms = 10) { SteamAPI.RunCallbacks(); Thread.Sleep(ms); }

static int Lobby()
{
    bool done = false; bool ok = false;
    var cr = CallResult<LobbyCreated_t>.Create((r, fail) =>
    {
        ok = !fail && r.m_eResult == EResult.k_EResultOK;
        Console.WriteLine($"LOBBY created ok={ok} result={r.m_eResult}");
        if (ok)
        {
            var lobby = new CSteamID(r.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobby, "game", "kabarma-m0");
            Console.WriteLine($"LOBBY data game={SteamMatchmaking.GetLobbyData(lobby, "game")} members={SteamMatchmaking.GetNumLobbyMembers(lobby)}");
            SteamMatchmaking.LeaveLobby(lobby);
        }
        done = true;
    });
    cr.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePrivate, 4));
    var sw = Stopwatch.StartNew();
    while (!done && sw.ElapsedMilliseconds < 15000) Pump();
    Console.WriteLine(done ? "RESULT lobby " + (ok ? "PASS" : "FAIL") : "RESULT lobby TIMEOUT");
    return ok ? 0 : 1;
}

static SteamNetworkingIPAddr Loopback()
{
    var a = new SteamNetworkingIPAddr();
    a.Clear();
    a.SetIPv4(0x7F000001, Port);
    return a;
}

static void Send(HSteamNetConnection c, string text)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    var h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    try { SteamNetworkingSockets.SendMessageToConnection(c, h.AddrOfPinnedObject(), (uint)bytes.Length, Constants.k_nSteamNetworkingSend_Reliable, out _); }
    finally { h.Free(); }
}

static List<string> Receive(HSteamNetConnection c)
{
    var list = new List<string>();
    var msgs = new IntPtr[32];
    int n = SteamNetworkingSockets.ReceiveMessagesOnConnection(c, msgs, msgs.Length);
    for (int i = 0; i < n; i++)
    {
        var m = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgs[i]);
        var buf = new byte[m.m_cbSize];
        Marshal.Copy(m.m_pData, buf, 0, m.m_cbSize);
        list.Add(Encoding.UTF8.GetString(buf));
        SteamNetworkingMessage_t.Release(msgs[i]);
    }
    return list;
}

static int Server()
{
    var addr = Loopback();
    var listen = SteamNetworkingSockets.CreateListenSocketIP(ref addr, 0, null);
    Console.WriteLine($"SERVER listening 127.0.0.1:{Port} socket={listen.m_HSteamListenSocket}");
    HSteamNetConnection conn = default;
    bool connected = false, closed = false;
    int echoed = 0;
    using var cb = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(s =>
    {
        var st = s.m_info.m_eState;
        if (st == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
        {
            SteamNetworkingSockets.AcceptConnection(s.m_hConn);
            conn = s.m_hConn;
        }
        else if (st == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected) { connected = true; Console.WriteLine("SERVER client connected"); }
        else if (st is ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer or ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
        {
            closed = true;
            SteamNetworkingSockets.CloseConnection(s.m_hConn, 0, "bye", false);
        }
    });
    var sw = Stopwatch.StartNew();
    while (!closed && sw.ElapsedMilliseconds < 30000)
    {
        Pump(2);
        if (!connected) continue;
        foreach (var msg in Receive(conn)) { Send(conn, "pong:" + msg); echoed++; }
    }
    SteamNetworkingSockets.CloseListenSocket(listen);
    Console.WriteLine($"RESULT server echoed={echoed} {(echoed >= Pings ? "PASS" : "FAIL")}");
    return echoed >= Pings ? 0 : 1;
}

static int Client()
{
    var addr = Loopback();
    var conn = SteamNetworkingSockets.ConnectByIPAddress(ref addr, 0, null);
    bool connected = false;
    using var cb = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(s =>
    {
        if (s.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected) connected = true;
        if (s.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            Console.WriteLine($"CLIENT problem: {s.m_info.m_szEndDebug}");
    });
    var sw = Stopwatch.StartNew();
    while (!connected && sw.ElapsedMilliseconds < 10000) Pump(2);
    if (!connected) { Console.WriteLine("RESULT client connect FAIL"); return 1; }
    Console.WriteLine($"CLIENT connected in {sw.ElapsedMilliseconds} ms");
    int got = 0;
    var rtts = new List<double>();
    for (int i = 0; i < Pings; i++)
    {
        var t = Stopwatch.StartNew();
        Send(conn, $"ping {i}");
        bool back = false;
        while (!back && t.ElapsedMilliseconds < 2000)
        {
            Pump(1);
            foreach (var m in Receive(conn)) if (m == $"pong:ping {i}") { back = true; got++; rtts.Add(t.Elapsed.TotalMilliseconds); }
        }
    }
    SteamNetworkingSockets.CloseConnection(conn, 0, "done", true);
    for (int i = 0; i < 20; i++) Pump(5);
    Console.WriteLine($"RESULT client pongs={got}/{Pings} rtt_avg_ms={(rtts.Count > 0 ? rtts.Average() : -1):0.00} {(got == Pings ? "PASS" : "FAIL")}");
    return got == Pings ? 0 : 1;
}
