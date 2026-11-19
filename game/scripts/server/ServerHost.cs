using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// ServerAdmin'in dayandigi sunucu (IServerHost): oyuncu listesi NetSync.Actors'tan, atma NetSync.KickPeer, kayit SaveService,
/// duyuru "srv.msg" bolumu (istemci Hud.Say), yonetici istegi "srv.cmd" ve cevap bolumu "srv.reply" (peri basina).
/// Yasak listesi ve sifre diske yazilir (server.json runtimeFile) ve yeniden baslayinca korunur.
/// </summary>
public sealed class ServerHost : IServerHost
{
    readonly ServerRunner _r;
    readonly Director _d;
    readonly ServerFile _cfg;
    readonly string _dir;
    readonly HashSet<byte> _admins = new();
    readonly DateTime _started = DateTime.UtcNow;

    public ServerRuntime Runtime { get; private set; } = new();
    public string Password { get; set; }
    public long NowUnix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    NetSync Net => _d.Net;

    public ServerHost(ServerRunner r, Director d, ServerFile cfg, string dir)
    {
        _r = r; _d = d; _cfg = cfg; _dir = dir;
        string path = dir + cfg.RuntimeFile;
        if (FileAccess.FileExists(path))
        {
            try { Runtime = JsonSerializer.Deserialize<ServerRuntime>(FileAccess.GetFileAsString(path)) ?? new(); }
            catch (Exception e) { GD.PushWarning($"SERVER runtime okunamadi: {e.Message}"); }
        }
        Password = Main.Args.ContainsKey("password") ? cfg.Password : Runtime.Password ?? cfg.Password;
    }

    public IReadOnlyList<ServerPlayer> Players() =>
        Net.Actors.Values.OrderBy(a => a.Peer).Select(a => new ServerPlayer(a.Peer, a.Name, Net.PeerAddress(a.Peer), _admins.Contains(a.Peer))).ToList();

    public bool Kick(int peer, string reason)
    {
        bool ok = Net.KickPeer((byte)peer, reason);
        if (ok) _admins.Remove((byte)peer);
        return ok;
    }

    public bool Save(out string why)
    {
        var s = _d.Saver;
        if (s == null || !s.Enabled) { why = "kayıt kapalı"; return false; }
        bool ok = s.SaveNow("elle", quiet: true);
        why = ok ? "" : s.LastError ?? "alınamadı";
        return ok;
    }

    public void Announce(string text)
    {
        Net.Srv.MsgSeq++;
        Net.Srv.MsgText = text;
        Net.Flush();
        _d.Hud?.Say(text);
    }

    public string StatusLine()
    {
        var cal = _d.State.Calendar;
        return $"calisiyor oyuncu={Net.Actors.Count}/{_cfg.MaxPlayers} gun={cal.Day} gece={(_d.NightActive ? 1 : 0)} kayit={_d.Saver?.SavesWritten ?? 0} sure={(int)(DateTime.UtcNow - _started).TotalSeconds}s sifre={(Password.Length > 0 ? "var" : "yok")}";
    }

    public void Shutdown() => _r.Close();

    public void Persist()
    {
        try
        {
            using var f = FileAccess.Open(_dir + _cfg.RuntimeFile, FileAccess.ModeFlags.Write);
            f?.StoreString(JsonSerializer.Serialize(Runtime, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) { GD.PushWarning($"SERVER runtime yazilamadi: {e.Message}"); }
    }

    // ---------------------------------------------------------------- ag

    public void BindNet()
    {
        Net.Srv.Motd = _cfg.Motd; Net.Srv.ServerName = _cfg.Name; // bolumler ServerNet'te (iki yan)
        Net.On("srv.cmd", (actor, r) =>
        {
            string line = r.Str();
            if (line.Length > 300) { actor.Rejected++; return; }
            string res = Remote(actor.Peer, line);
            var old = Net.Srv.Replies.GetValueOrDefault(actor.Peer);
            Net.Srv.Replies[actor.Peer] = (old.seq + 1, res);
            Net.Flush();
        });
    }

    /// <summary>Oyun ici yonetici komutu: once 'giris &lt;sifre&gt;'; sonra ServerAdmin komutlari.</summary>
    string Remote(byte peer, string line)
    {
        line = line.Trim();
        var parts = line.Split(' ', 2);
        if (parts[0].ToLowerInvariant() is "giris" or "login")
        {
            if (_cfg.AdminPassword.Length == 0) return "HATA: oyun içi yönetim kapalı (sunucuda admin_password yok)";
            if (parts.Length < 2 || parts[1].Trim() != _cfg.AdminPassword) { ServerRunner.Log($"yonetici girisi reddedildi peer={peer}"); return "HATA: şifre yanlış"; }
            _admins.Add(peer);
            ServerRunner.Log($"yonetici girisi peer={peer}");
            return "yönetici oldun (yardim)";
        }
        if (!_admins.Contains(peer)) return "HATA: yönetici değilsin: giris <şifre>";
        string res = ServerAdmin.Execute(this, line);
        ServerRunner.Log($"komut [oyun:{peer}] {line} -> {res.Replace('\n', '|')}");
        return res;
    }
}
