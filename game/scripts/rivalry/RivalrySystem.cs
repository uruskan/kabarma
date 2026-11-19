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
    public bool Night => _d.State.Calendar.IsNight;

    /// <summary>Oyuncu kimligi: host "p0", istemci "p" + es numarasi.</summary>
    public static string PlayerId(byte peer) => "p" + peer;

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
    public RivalryVerdict RaidPiece(BuildPiece bp, float dmg, string attackerTeam, bool? nightOverride = null)
    {
        if (!Enabled || _d.NetClient || bp == null || !GodotObject.IsInstanceValid(bp) || bp.S == null) return RivalryVerdict.NotRivalry;
        var at = bp.GlobalPosition;
        var v = RivalryRules.CanHit(F, S, nightOverride ?? Night, at.X, at.Z, attackerTeam ?? "", bp.Def.Id);
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

    /// <summary>Acik Silo'dan yagma (kural 5): gece, kendi disindaki takimin silosundan; menzil 8 m. Donen: (esya, Maya).</summary>
    public (int items, int maya) LootSilo(string looterTeam, Vector3 at, Inventory bag)
    {
        if (!Enabled || !Night) return (0, 0);
        if (!S.Teams.Any(t => t.Id != looterTeam && new Vector2(at.X - t.OcakX, at.Z - t.OcakZ).Length() <= 8f)) return (0, 0);
        var r = RivalryRules.LootSilo(S, looterTeam, bag);
        if (r.items + r.maya > 0) { GD.Print($"RIVALRY silo looted items={r.items} maya={r.maya} by={looterTeam}"); _d.Net?.Flush(); }
        return r;
    }

    // ------------------------------------------------------------------ gece ve gunun akisi

    /// <summary>Safakta (Director.Night): gecenin Maya'si her takima yazilir, Silo dolar, sezon puani artar.</summary>
    public void OnDawn(float nightMaya)
    {
        if (!Enabled || _d.NetClient) return;
        foreach (var t in S.Teams) if (!t.Offline) RivalryRules.AddMaya(S, t.Id, t.KazanToppled ? nightMaya * 0.5f : nightMaya);
        RivalryRules.Dawn(F, S);
        GD.Print($"RIVALRY dawn silo_maya={S.SiloMaya} " + string.Join(" ", RivalryRules.Leaderboard(S).Select(t => $"{t.Id}={t.Score}")));
        _d.Net?.Flush();
    }

    /// <summary>Director.Tick'ten: cevrimici durumu, kabarma suzgeci, sezon, Kabarma kancasi.</summary>
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
            n.DawnNow += () => OnDawn(n.Maya);
        }
        if (_d.NetClient) return;
        _presenceT -= dt;
        if (_presenceT <= 0)
        {
            _presenceT = 1f;
            var online = new HashSet<string> { GameState.LocalPlayer };
            if (_d.Net != null) foreach (var peer in _d.Net.Actors.Keys) online.Add(PlayerId(peer));
            if (RivalryRules.UpdatePresence(S, online) > 0)
            {
                GD.Print("RIVALRY presence " + string.Join(" ", S.Teams.Select(t => $"{t.Id}:{(t.Offline ? "offline" : "online")}")));
                _d.Net?.Flush();
            }
        }
        int day = _d.State.Calendar.Running ? _d.State.Calendar.Day : 0;
        if (day != _lastDay)
        {
            _lastDay = day;
            if (RivalryRules.TickSeason(S, day)) { GD.Print($"RIVALRY season over winner={S.Season.Winner}"); _d.Net?.Flush(); }
        }
    }

    // ------------------------------------------------------------------ --test=coop adimi

    /// <summary>Yikilan parca sayisi (CoopTest temel sayisini buna gore ayarlar).</summary>
    public int ProbeRuined { get; private set; }

    /// <summary>
    /// --test=coop: iki takim (host + 1. bot / 2. ve 3. bot), host'un kurdugu bir temel A takiminin tapusunda. Gunduz B'nin baskini
    /// reddedilir, gece (saat zorlanarak) temel yikilir ve enkaz plani kalir; "rivalry" bolumu istemcilere gider (Judge ozeti karsilastirir).
    /// Donen: "ok ..." ya da "fail ...".
    /// </summary>
    public string CoopProbe()
    {
        var piece = _d.Build?.System.Pieces.FirstOrDefault(p => p.Def.Id == "kare_temel");
        if (piece == null || _d.Net == null) return "fail no piece/net";
        var at = piece.GlobalPosition;
        var peers = _d.Net.Actors.Keys.OrderBy(k => k).ToList();
        var a = new List<string> { GameState.LocalPlayer }; var b = new List<string>();
        for (int i = 0; i < peers.Count; i++) (i == 0 ? a : b).Add(PlayerId(peers[i]));
        string why = Start(false, new[]
        {
            ("a", "Takim A", at.X, at.Z, 30f, a.ToArray()),
            ("b", "Takim B", at.X, at.Z + 600f, 30f, b.ToArray()),
        });
        if (why != null) return "fail start: " + why;
        int ruins0 = S.Ruins.Count;
        var day = RaidPiece(piece, 99999f, "b", false);
        if (day != RivalryVerdict.DayProtected || !GodotObject.IsInstanceValid(piece) || piece.Hp <= 0) return $"fail day verdict={day}";
        var self = RaidPiece(piece, 99999f, "a", true);
        if (self != RivalryVerdict.Friendly) return $"fail friendly verdict={self}";
        var night = RaidPiece(piece, 99999f, "b", true);
        if (night != RivalryVerdict.Allowed || S.Ruins.Count < ruins0 + 1) // dayanan parcalar da coker (kural 6): 1 ya da daha cok enkaz return $"fail night verdict={night} ruins={S.Ruins.Count}";
        ProbeRuined = S.Ruins.Count - ruins0;
        _d.Net.Flush();
        return $"ok teams=2 day={day} night={night} ruins={S.Ruins.Count}";
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
            var team = S.TeamOf(PlayerId(actor.Peer))?.Id ?? "";
            var why = Rebuild(id, team, actor.Bag);
            if (why == null) net.Flush(); else GD.Print($"RIVALRY rebuild rejected: {why}");
        });
        net.On("rivalry_silo", (actor, r) =>
        {
            var team = S.TeamOf(PlayerId(actor.Peer))?.Id ?? "";
            var at = actor.HasFrame ? new Vector3(actor.Last.X, actor.Last.Y, actor.Last.Z) : Vector3.Zero;
            LootSilo(team, at, actor.Bag);
        });
    }
}
