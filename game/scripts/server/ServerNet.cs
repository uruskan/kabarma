using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Sunucu mesajlarinin iki yani (M5 sunucu akisi): "srv.msg" (duyuru ve karsilama; herkese) ve "srv.reply" (yonetici komutunun
/// cevabi; es basina). Ayni sinif her ortak oyun surecinde (NetSync.Start) kurulur: host yazar (ServerHost doldurur), istemci
/// okur ve Hud'a/konsola basar. Normal (adanmamis) oyunda bos kalir: motd yok, duyuru yok.
/// </summary>
public sealed class ServerNet
{
    readonly NetSync _net;
    // host tarafi
    public uint MsgSeq;
    public string MsgText = "", Motd = "", ServerName = "";
    public readonly Dictionary<byte, (uint seq, string text)> Replies = new();
    // istemci tarafi
    bool _first = true;
    uint _seen, _replySeen;

    public ServerNet(NetSync net)
    {
        _net = net;
        net.Register("srv.msg", false,
            (w, peer) => { w.VarU(MsgSeq); w.Str(MsgText); w.Str(Motd); w.Str(ServerName); },
            r =>
            {
                uint seq = r.VarU(); string text = r.Str(), motd = r.Str(), name = r.Str();
                if (_net.IsHost) return;
                if (_first) { _first = false; _seen = seq; if (motd.Length > 0) _net.D.Hud?.Say($"{name}: {motd}"); return; }
                if (seq != _seen) { _seen = seq; _net.D.Hud?.Say($"[Sunucu] {text}"); }
            });
        net.Register("srv.reply", true,
            (w, peer) => { var v = Replies.GetValueOrDefault((byte)peer); w.VarU(v.seq); w.Str(v.text ?? ""); },
            r =>
            {
                uint seq = r.VarU(); string text = r.Str();
                if (_net.IsHost || seq == _replySeen) return;
                _replySeen = seq;
                foreach (var line in text.Split('\n')) _net.D.Hud?.Say(line);
                ServerConsolePanel.Echo(text);
            });
    }
}
