using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Cekisme dunyasinin Godot katmani (GDD 22.2; M5 cekisme). Kurallar Sim'de (RivalryRules); burada dunyadan girdi:
/// cevrimici oyuncular, gece/gunduz, yapi parcalari, ekinlerin kabarmasi. Yetki host'ta: yapi hasari, baskin, yagma ve
/// enkaz kurma yalniz host'ta isler; istemci istek yollar. Durum "rivalry" ag bolumu ve kayit bolumuyle tasinir.
/// Tek oyuncuda ve Cekisme kapaliyken hicbir sey yapmaz (State.Rivalry.Enabled false).
/// </summary>
public sealed class RivalrySystem
{
    readonly Director _d;
    KabarmaNight _wiredNight;
    float _presenceT;
    int _lastDay;
    bool _filterSet;

    public RivalrySystem(Director d) { _d = d; }

    RivalryState S => _d.State.Rivalry;
    RivalryFile F => _d.Data.Rivalry;
    public bool Enabled => S.Enabled;
    public RivalryState State => S;
    /// <summary>Sinama: gece/gunduz zorlanir (null: gercek takvim).</summary>
    public bool? NightOverride { get; set; }
    public bool Night => NightOverride ?? _d.State.Calendar.IsNight;

    /// <summary>
    /// Kalici oyuncu kimligi (kayitli takimlar icin): host "p0"; istemci karakter adi ("n:" + ad). Es numarasi her oturumda
    /// yeniden verildigi icin takimlar ona baglanmaz.
    /// </summary>
    public static string KeyOf(NetActor a) => "n:" + a.Name;
    public const string HostKey = GameState.LocalPlayer;

    /// <summary>Cevrimici oyuncularin kalici kimlikleri (host + istemciler).</summary>
    HashSet<string> OnlineKeys()
    {
        var online = new HashSet<string> { HostKey };
        if (_d.Net != null) foreach (var a in _d.Net.Actors.Values) online.Add(KeyOf(a));
        return online;
    }

    /// <summary>Takimi olmayan cevrimici oyuncular en az dolu takima girer (en cok PlayersPerTeamMax). Donen: yeni uye sayisi.</summary>
    public int EnsureMembers()
    {
        int added = 0;
        foreach (var key in OnlineKeys())
        {
            if (S.TeamOf(key) != null) continue;
            var t = S.Teams.Where(x => x.Players.Count < F.PlayersPerTeamMax).OrderBy(x => x.Players.Count).ThenBy(x => x.Id, System.StringComparer.Ordinal).FirstOrDefault();
            if (t == null) break;
            t.Players.Add(key);
            added++;
            GD.Print($"RIVALRY join {key} -> {t.Id}");
        }
        return added;
    }

    /// <summary>
    /// Dunya ayari "Cekisme" (host ya da tek oyuncu): teamCount takim, Ocak'lar OcakPos'tan 600 m aralikli halkada. Takim 0 dunya
    /// Ocak'inin tapusudur. Uyeler cevrimici oyunculardan sirayla dagitilir (EnsureMembers).
    /// </summary>
    public string AutoStart(int teamCount, bool dedicated = false)
    {
        if (_d.NetClient) return "istemci kuramaz";
        teamCount = Mathf.Clamp(teamCount, F.TeamsMin, F.TeamsMax);
        var home = _d.OcakPos;
        float step = F.OcakMinDistance + 100f;
        float plot = _d.State.Research.ClaimRadius > 0 ? _d.State.Research.ClaimRadius : F.PlotRadiusFallback;
        var teams = new List<(string, string, float, float, float, string[])>();
        for (int i = 0; i < teamCount; i++)
        {
            float ang = i * Mathf.Pi / 2f;
            teams.Add(("t" + (i + 1), "Takım " + (i + 1), home.X + Mathf.Cos(ang) * step * (i == 0 ? 0 : 1), home.Z + Mathf.Sin(ang) * step * (i == 0 ? 0 : 1), plot, new string[0]));
        }
        // Takim bos baslar; Start dogrulamasi oyuncu ister, o yuzden once uyeleri ekleyip sonra kur.
        var s = RivalryRules.New(F, dedicated, _d.State.Calendar.Running ? _d.State.Calendar.Day : 1);
        foreach (var t in teams) RivalryRules.AddTeam(F, s, t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
        _d.State.ReplaceRivalry(s);
        EnsureMembers();
        _filterSet = false;
        _d.Net?.Flush();
        GD.Print($"RIVALRY auto start teams={teamCount} dedicated={dedicated}");
        return null;
    }

    /// <summary>Cekisme dunyasini kurar (dunya ayari). Donen: null ya da reddetme nedeni (takim sayisi, Ocak araligi).</summary>
    public string Start(bool dedicated, IEnumerable<(string id, string name, float x, float z, float plot, string[] players)> teams)
    {
        var s = RivalryRules.New(F, dedicated, _d.State.Calendar.Running ? _d.State.Calendar.Day : 1);
        foreach (var t in teams) RivalryRules.AddTeam(F, s, t.id, t.name, t.x, t.z, t.plot, t.players);
        var why = RivalryRules.ValidateSetup(F, s);
        if (why != null) return why;
        _d.State.ReplaceRivalry(s);
        _filterSet = false;
        _d.Net?.Flush();
        GD.Print($"RIVALRY start teams={s.Teams.Count} dedicated={dedicated} season={s.Season.Days}");
        return null;
    }

    // ------------------------------------------------------------------ sorgular

    public RivalryVerdict Verdict(Vector3 at, string attackerTeam, string shape = null, bool player = false) =>
        RivalryRules.CanHit(F, S, Night, at.X, at.Z, attackerTeam ?? "", shape, player);

    /// <summary>Acik arazide bir oyuncuya oyuncu vurusu serbest mi (kural 3).</summary>
    public bool PlayerHitAllowed(Vector3 at, string attackerPlayer)
    {
        var t = S.TeamOf(attackerPlayer);
        return Verdict(at, t?.Id ?? "", null, true) == RivalryVerdict.Allowed;
    }

    /// <summary>Acik arazide olen oyuncunun cantasindan rastgele 3 yigin katile gecer (kural 3, host).</summary>
    public int OnPvpKill(Inventory victim, Inventory killer, Vector3 at)
    {
        if (!Enabled || _d.NetClient || !S.OpenPvp || RivalryRules.PlotAt(S, at.X, at.Z) != null) return 0;
        var moved = RivalryRules.PvpLoot(F, victim, killer, _d.State.Rng);
        GD.Print($"RIVALRY pvp loot stacks={moved.Count}");
        _d.Net?.Flush();
        return moved.Count;
    }

    // ------------------------------------------------------------------ yapi hasari (kural 6)

    /// <summary>
    /// Gecede bir takim yapi parcasina vurur (host). Karar Sim'den; yikilan parca enkaz planina cevrilir ve sokulur.
    /// Donen: karar. Hasar BuildBridge.Damage'in yuzde-10 tabanini asar (baskinda yikilir).
    /// </summary>
    public RivalryVerdict RaidPiece(BuildPiece bp, float dmg, string attackerTeam)
    {
        if (!Enabled || _d.NetClient || bp == null || !GodotObject.IsInstanceValid(bp) || bp.S == null) return RivalryVerdict.NotRivalry;
        var at = bp.GlobalPosition;
        var v = RivalryRules.CanHit(F, S, Night, at.X, at.Z, attackerTeam ?? "", bp.Def.Id);
        if (v != RivalryVerdict.Allowed) return v;
        bp.Hp -= dmg;
        if (bp.Hp > 0) { _d.Net?.Flush(); return v; }
        var owner = RivalryRules.PlotAt(S, at.X, at.Z);
        // Cokenler de enkaz olur: sokmeden once bagli bilesenin ortusenini grafikten bul.
        var graph = _d.Build.System.Graph;
        var snapshot = graph.Pieces.Values.ToDictionary(p => p.Id, p => p);
        RivalryRules.AddRuin(S, bp.S, owner?.Id ?? "");
        int before = graph.Pieces.Count;
        _d.Build.System.Remove(bp);
        foreach (var gone in snapshot.Values.Where(p => p.Id != bp.S.Id && !graph.Pieces.ContainsKey(p.Id)))
            RivalryRules.AddRuin(S, gone, owner?.Id ?? "");
        GD.Print($"RIVALRY raid destroyed {bp.Def.Id} team={owner?.Id} pieces {before}->{graph.Pieces.Count} ruins={S.Ruins.Count}");
        _d.Net?.Flush();
        return v;
    }

    /// <summary>Enkaz planini tek tusla kurar (kural 6): bedelin %50'si cantadan; gunduz, kendi takimin. Donen: null ya da neden.</summary>
    public string Rebuild(int ruinId, string team, Inventory bag)
    {
        if (!Enabled) return "Cekisme yok";
        var r = S.Ruins.FirstOrDefault(x => x.Id == ruinId);
        if (r == null) return "enkaz yok";
        if (r.Team != team) return "baska takimin enkazi";
        if (Night) return "gece kurulmaz";
        var cost = RivalryRules.RebuildCost(F, _d.Data.Build, r);
        foreach (var kv in cost) if (bag.Count(kv.Key) < kv.Value) return $"eksik: {kv.Key}";
        var xf = new Transform3D(Basis.FromEuler(new Vector3(0, r.Yaw, 0)), new Vector3(r.Pos[0], r.Pos[1], r.Pos[2]));
        var piece = _d.Build.System.PlaceAt(r.Shape, xf, out var why, r.Material);
        if (piece == null) return why ?? "kurulamadi";
        foreach (var kv in cost) bag.Remove(kv.Key, kv.Value);
        S.Ruins.Remove(r);
        GD.Print($"RIVALRY rebuild {r.Shape} team={team} cost={string.Join(",", cost.Select(k => k.Key + k.Value))}");
        return null;
    }

    /// <summary>Oyuncu eylemi: enkaz kur. Istemcide host'a gider.</summary>
    public string RequestRebuild(int ruinId)
    {
        if (_d.Net?.Forward("rivalry_rebuild", w => w.VarU((uint)ruinId)) == true) return null;
        var team = S.TeamOf(GameState.LocalPlayer)?.Id ?? "";
        return Rebuild(ruinId, team, _d.State.Backpack);
    }

    /// <summary>
    /// Acik Silo yagmasi (kural 5): gece, hedef takimin Ocak''indan 8 m icinde, yagmaci baska takimdan. Yalniz hedefin silosu bosalir.
    /// Donen: (esya, Maya).
    /// </summary>
    public (int items, int maya) LootSilo(string targetTeam, string looterTeam, Vector3 at, Inventory bag)
    {
        if (!Enabled || !Night) return (0, 0);
        var target = S.Team(targetTeam);
        if (target == null || new Vector2(at.X - target.OcakX, at.Z - target.OcakZ).Length() > 8f) return (0, 0);
        var r = RivalryRules.LootSilo(S, targetTeam, looterTeam, bag);
        if (r.items + r.maya > 0) { GD.Print($"RIVALRY silo looted items={r.items} maya={r.maya} from={targetTeam} by={looterTeam}"); _d.Net?.Flush(); }
        return r;
    }

    /// <summary>Konuma 8 m icinde Ocak''i olan rakip takim (yoksa null).</summary>
    string RivalAt(Vector3 at, string myTeam) =>
        S.Teams.Where(t => t.Id != myTeam && (t.Silo.Count > 0 || t.SiloMaya > 0) && new Vector2(at.X - t.OcakX, at.Z - t.OcakZ).Length() <= 8f)
            .Select(t => t.Id).FirstOrDefault();

    // ------------------------------------------------------------------ oyun yolu kapilari (kural 2, 3, 5, 6)

    /// <summary>
    /// BuildBridge.Damage kapisi: Cekisme acikken yapi hasari kurala gore (gunduz yok, gece yikilir; vurulan takim yerine
    /// saldiran takim attackerTeam, yabani/Kabaran bos). Donen: true = hasar Cekisme tarafindan islendi (cagiran birakir).
    /// </summary>
    public bool TryStructureHit(BuildPiece bp, float amount, string attackerTeam, out float hp)
    {
        hp = bp?.Hp ?? 0;
        if (!Enabled || _d.NetClient) return false;
        RaidPiece(bp, amount, attackerTeam ?? "");
        hp = bp != null && GodotObject.IsInstanceValid(bp) ? bp.Hp : 0;
        return true;
    }

    /// <summary>Oyuncu hasari (Player.Hurt): tapuda gunduz dokunulmaz (kural 2). Donen: hasar islesin mi.</summary>
    public bool PlayerHurtAllowed(Vector3 at)
    {
        if (!Enabled) return true;
        var v = Verdict(at, "", null, false);
        return v is RivalryVerdict.Allowed or RivalryVerdict.NotRivalry;
    }

    /// <summary>Tarladaki kabarik urunu almak (Cekim): kendi tapunda serbest, rakibin urunu yalniz gece ve yagmalanabilirken (kural 5).</summary>
    public bool CropGrabAllowed(Vector3 at, string team)
    {
        if (!Enabled) return true;
        var plot = RivalryRules.PlotAt(S, at.X, at.Z);
        if (plot == null || plot.Id == team) return true;
        return RivalryRules.Lootable(F, S, Night, "swollen_crop", at.X, at.Z, team);
    }

    /// <summary>Yerel oyuncunun takimi (tek oyuncu / host).</summary>
    public string LocalTeam => S.TeamOf(HostKey)?.Id ?? "";

    // ------------------------------------------------------------------ gece ve gunun akisi

    /// <summary>
    /// Safakta (Night.DawnNow): Kazan devrildiyse takimlar muhurlenir (kural 6; tek ortak Kabarma oldugundan hepsi); gecenin
    /// Maya'si cevrimici takimlara yazilir; rampadaki kasalar ihracata hazir sayilir (kural 5); Silo dolar, puan islenir.
    /// </summary>
    public void OnDawn(float nightMaya, bool kazanToppled)
    {
        if (!Enabled || _d.NetClient) return;
        if (kazanToppled) foreach (var t in S.Teams) RivalryRules.ToppleKazan(S, t.Id);
        int crates = _d.State.Cargo.Pad.Count;
        if (crates > 0 && S.Teams.Count > 0) RivalryRules.MarkExportReady(S.Teams[0], "kargo_kasasi", crates); // rampa dunya Ocak''inin (takim 0)
        foreach (var t in S.Teams) if (!t.Offline) RivalryRules.AddMaya(S, t.Id, nightMaya);
        RivalryRules.Dawn(F, S);
        GD.Print($"RIVALRY dawn toppled={kazanToppled} silo_maya={S.SiloMayaTotal} " + string.Join(" ", RivalryRules.Leaderboard(S).Select(t => $"{t.Id}={t.Score}")));
        _d.Net?.Flush();
    }

    float _siloT, _memberT;

    /// <summary>Director.Tick'ten: uyelik, cevrimici durumu, kabarma suzgeci, silo yagmasi, sezon, Kabarma kancasi.</summary>
    public void Tick(float dt)
    {
        if (!Enabled) return;
        if (!_filterSet)
        {
            _filterSet = true;
            _d.State.Farm.SwellFilter = p => RivalryRules.SwellAllowed(S, p.X + 0.5f, p.Y + 0.5f);
        }
        if (_d.State.Night != null && !ReferenceEquals(_d.State.Night, _wiredNight))
        {
            _wiredNight = _d.State.Night;
            var n = _wiredNight;
            n.DawnNow += () => OnDawn(n.Maya, n.EndReason == KabarmaEnd.KazanToppled);
        }
        // Yagma: yerel oyuncu rakibin Ocak''inin yaninda, gece (istemcide host''a istek).
        _siloT -= dt;
        if (_siloT <= 0 && Night)
        {
            _siloT = 1f;
            var me = _d.Player.GlobalPosition;
            var team = _d.NetClient ? "" : LocalTeam;
            var target = _d.NetClient ? S.Teams.Select(t => t).Where(t => new Vector2(me.X - t.OcakX, me.Z - t.OcakZ).Length() <= 8f && (t.Silo.Count > 0 || t.SiloMaya > 0)).Select(t => t.Id).FirstOrDefault() : RivalAt(me, team);
            if (target != null && _d.Net?.Forward("rivalry_silo", w => w.Str(target)) != true) LootSilo(target, team, me, _d.State.Backpack);
        }
        if (_d.NetClient) return;
        _presenceT -= dt;
        if (_presenceT <= 0)
        {
            _presenceT = 1f;
            bool changed = EnsureMembers() > 0;
            if (RivalryRules.UpdatePresence(S, OnlineKeys()) > 0) changed = true;
            if (changed)
            {
                GD.Print("RIVALRY presence " + string.Join(" ", S.Teams.Select(t => $"{t.Id}:{(t.Offline ? "offline" : "online")}[{t.Players.Count}]")));
                _d.Net?.Flush();
            }
        }
        int day = _d.State.Calendar.Running ? _d.State.Calendar.Day : 0;
        if (day != _lastDay)
        {
            _lastDay = day;
            RivalryRules.ClearSeals(S);
            if (RivalryRules.TickSeason(S, day)) { GD.Print($"RIVALRY season over winner={S.Season.Winner}"); _d.Net?.Flush(); }
        }
    }

    // ------------------------------------------------------------------ --test=coop adimi

    /// <summary>Yikilan parca sayisi (CoopTest temel sayisini buna gore ayarlar).</summary>
    public int ProbeRuined { get; private set; }

    /// <summary>
    /// --test=coop: iki takim (host + 1. bot / 2. ve 3. bot), host'un kurdugu bir temel A takiminin tapusunda. Gercek oyun yolu:
    /// BuildBridge.Damage (yaratik/baskin hasari) gunduz B takimi icin hasar yapmaz, gece parcayi yikar ve enkaz plani kalir;
    /// A'nin silosu B'nin botundan yagmalanir, kendi (B) silosu ve A'nin bu yagmadaki payi dokunulmaz. Saat NightOverride ile zorlanir.
    /// Donen: "ok ..." ya da "fail ...".
    /// </summary>
    public string CoopProbe()
    {
        var piece = _d.Build?.System.Pieces.FirstOrDefault(p => p.Def.Id == "kare_temel");
        if (piece == null || _d.Net == null) return "fail no piece/net";
        var at = piece.GlobalPosition;
        var peers = _d.Net.Actors.Values.OrderBy(x => x.Peer).ToList();
        var a = new List<string> { HostKey }; var b = new List<string>();
        for (int i = 0; i < peers.Count; i++) (i == 0 ? a : b).Add(KeyOf(peers[i]));
        string why = Start(false, new[]
        {
            ("a", "Takim A", at.X, at.Z, 30f, a.ToArray()),
            ("b", "Takim B", at.X, at.Z + 600f, 30f, b.ToArray()),
        });
        if (why != null) return "fail start: " + why;
        int ruins0 = S.Ruins.Count;
        NightOverride = false;
        _d.Build.Damage(piece, 99999f, "b");
        if (!GodotObject.IsInstanceValid(piece) || piece.Hp <= 0) { NightOverride = null; return "fail day: parca gunduz hasar aldi"; }
        var player = _d.Player.GlobalPosition;
        if (PlayerHurtAllowed(at)) { NightOverride = null; return "fail day: oyuncu gunduz tapuda hasar alabiliyor"; }
        NightOverride = true;
        var self = RaidPiece(piece, 99999f, "a");
        if (self != RivalryVerdict.Friendly) { NightOverride = null; return $"fail friendly verdict={self}"; }
        _d.Build.Damage(piece, 99999f, "b"); // gece: gercek hasar yolu parcayi yikar
        bool gone = !GodotObject.IsInstanceValid(piece) || _d.Build.System.Pieces.All(p => p != piece);
        if (!gone || S.Ruins.Count < ruins0 + 1) { NightOverride = null; return $"fail night: gone={gone} ruins={S.Ruins.Count}"; } // dayanan parcalar da coker (kural 6)
        ProbeRuined = S.Ruins.Count - ruins0;
        // Silo: A'nin silosu dolar, B'nin ilk botu (istemci aktoru) A'nin Ocak'inda yagmalar; B'nin kendi silosu dolu kalir.
        var ta = S.Team("a")!; var tb = S.Team("b")!;
        ta.Silo["turnip"] = 5; ta.SiloMaya = 7; tb.Silo["turnip"] = 3;
        var looter = peers.FirstOrDefault(x => b.Contains(KeyOf(x)));
        if (looter != null)
        {
            int t0 = looter.Bag.Count("turnip"), m0 = looter.Bag.Count("maya");
            var wrong = LootSilo("b", "b", new Vector3(tb.OcakX, 0, tb.OcakZ), looter.Bag); // kendi silosu
            var ok = LootSilo("a", "b", new Vector3(ta.OcakX, 0, ta.OcakZ), looter.Bag);
            if (wrong != (0, 0) || ok != (5, 7) || looter.Bag.Count("turnip") != t0 + 5 || looter.Bag.Count("maya") != m0 + 7 || tb.Silo.GetValueOrDefault("turnip") != 3 || ta.Silo.Count != 0)
            { NightOverride = null; return $"fail silo wrong={wrong} ok={ok} b_turnip={tb.Silo.GetValueOrDefault("turnip")}"; }
            looter.Bag.Remove("turnip", 5); looter.Bag.Remove("maya", 7); // sinama artigi
        }
        tb.Silo.Clear();
        NightOverride = null;
        _d.Net.Flush();
        return $"ok teams=2 day=blocked night=destroyed ruins={S.Ruins.Count} silo=a_to_b";
    }

    // ------------------------------------------------------------------ ag

    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>InitNet'ten sonra bir kez: durum bolumu ve istekler (host yetkili).</summary>
    public void BindNet()
    {
        var net = _d.Net;
        if (net == null) return;
        net.Register("rivalry", false,
            (w, peer) => w.Str(JsonSerializer.Serialize(S, Json)),
            r => _d.State.ReplaceRivalry(JsonSerializer.Deserialize<RivalryState>(r.Str(), Json)));
        net.On("rivalry_rebuild", (actor, r) =>
        {
            int id = (int)r.VarU();
            var team = S.TeamOf(KeyOf(actor))?.Id ?? "";
            var why = Rebuild(id, team, actor.Bag);
            if (why == null) net.Flush(); else GD.Print($"RIVALRY rebuild rejected: {why}");
        });
        net.On("rivalry_silo", (actor, r) =>
        {
            string target = r.Str();
            var team = S.TeamOf(KeyOf(actor))?.Id ?? "";
            var at = actor.HasFrame ? new Vector3(actor.Last.X, actor.Last.Y, actor.Last.Z) : Vector3.Zero;
            LootSilo(target, team, at, actor.Bag);
        });
    }
}