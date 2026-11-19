using System.Text;

namespace Kabarma.Sim;

// Adanmis sunucu yonetimi (TEKNIK §5.5, §12; M5 sunucu akisi): yasak listesi, katilma denetimi ve yonetim komutlari.
// Motorsuz ve saf: komutlar IServerHost'a konusur (Godot katmani: game/scripts/server/ServerHost.cs).

/// <summary>Yasak kaydi: ad (kucuk harf) ya da adres; UntilUtc 0 ise kalici.</summary>
public sealed class BanEntry
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Reason { get; set; } = "";
    /// <summary>Bitis (unix sn); 0 = kalici.</summary>
    public long UntilUtc { get; set; }
    public string By { get; set; } = "";
}

/// <summary>Kalici sunucu durumu (user://server_runtime.json): yasaklar ve calisirken degisen sifre.</summary>
public sealed class ServerRuntime
{
    public List<BanEntry> Bans { get; set; } = new();
    /// <summary>Null: server.json'daki sifre gecerli; bos dize: sifre kaldirildi.</summary>
    public string? Password { get; set; }
}

public sealed record ServerPlayer(int Peer, string Name, string Address, bool Admin);

/// <summary>Yonetim komutlarinin dayandigi sunucu arayuzu.</summary>
public interface IServerHost
{
    IReadOnlyList<ServerPlayer> Players();
    /// <summary>Oyuncuyu atar (baglantisini keser). Bulunamazsa false.</summary>
    bool Kick(int peer, string reason);
    /// <summary>Dunyayi kaydeder; alinamazsa neden.</summary>
    bool Save(out string why);
    void Announce(string text);
    ServerRuntime Runtime { get; }
    /// <summary>Gecerli giris sifresi (bos: yok).</summary>
    string Password { get; set; }
    long NowUnix { get; }
    string StatusLine();
    /// <summary>Kaydedip kapanir.</summary>
    void Shutdown();
    /// <summary>Calisma durumu degisti (yasak, sifre): diske yaz.</summary>
    void Persist();
}

public static class ServerAdmin
{
    public static readonly string[] Commands = { "yardim", "oyuncular", "at", "yasakla", "yasak_kaldir", "yasaklar", "kaydet", "duyuru", "sifre", "durum", "kapat" };

    public const string Help =
        "yardim | oyuncular | at <no|ad> [neden] | yasakla <no|ad> [dakika(0=kalici)] [neden] | yasak_kaldir <ad> | yasaklar | kaydet | duyuru <metin> | sifre [yeni|-] | durum | kapat";

    /// <summary>Katilma denetimi (Hello): yasak, sifre. Null: kabul; aksi halde reddedilme nedeni.</summary>
    public static string? CheckJoin(IServerHost h, string name, string password, string address)
    {
        var ban = FindBan(h.Runtime, name, address, h.NowUnix);
        if (ban != null) return ban.UntilUtc == 0 ? $"yasaklısın ({ban.Reason})" : $"yasaklısın, {Math.Max(1, (ban.UntilUtc - h.NowUnix + 59) / 60)} dk sonra gel ({ban.Reason})";
        if (h.Password.Length > 0 && password != h.Password) return "sunucu şifresi yanlış";
        if (h.Players().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) return "bu ad kullanımda";
        return null;
    }

    public static BanEntry? FindBan(ServerRuntime rt, string name, string address, long now)
    {
        rt.Bans.RemoveAll(b => b.UntilUtc != 0 && b.UntilUtc <= now);
        string n = name.Trim().ToLowerInvariant();
        return rt.Bans.FirstOrDefault(b => (b.Name.Length > 0 && b.Name == n) || (b.Address.Length > 0 && address.Length > 0 && b.Address == address));
    }

    /// <summary>Hedef: es numarasi ya da ad (buyuk-kucuk harf farksiz tam ya da tek eslesen onek).</summary>
    public static ServerPlayer? Find(IServerHost h, string key)
    {
        var all = h.Players();
        if (int.TryParse(key, out int peer)) return all.FirstOrDefault(p => p.Peer == peer);
        var exact = all.FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        var pre = all.Where(p => p.Name.StartsWith(key, StringComparison.OrdinalIgnoreCase)).ToList();
        return pre.Count == 1 ? pre[0] : null;
    }

    /// <summary>Bir komut satirini calistirir; donen metin konsola ya da yoneticiye gider ("HATA:" onekli ise basarisiz).</summary>
    public static string Execute(IServerHost h, string line)
    {
        line = (line ?? "").Trim();
        if (line.Length == 0) return "";
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string cmd = parts[0].ToLowerInvariant();
        string rest = parts.Length > 1 ? parts[1].Trim() : "";
        switch (cmd)
        {
            case "yardim": case "?": return Help;
            case "durum": return h.StatusLine();
            case "oyuncular":
            {
                var l = h.Players();
                if (l.Count == 0) return "oyuncu yok";
                var sb = new StringBuilder($"{l.Count} oyuncu:");
                foreach (var p in l) sb.Append($"\n  {p.Peer}  {p.Name}{(p.Admin ? " (yönetici)" : "")}  {p.Address}");
                return sb.ToString();
            }
            case "at":
            {
                var (key, tail) = Split1(rest);
                if (key.Length == 0) return "HATA: at <no|ad> [neden]";
                var p = Find(h, key);
                if (p == null) return $"HATA: oyuncu yok: {key}";
                string why = tail.Length > 0 ? tail : "yönetici attı";
                return h.Kick(p.Peer, why) ? $"atıldı: {p.Name}" : $"HATA: atılamadı: {p.Name}";
            }
            case "yasakla":
            {
                var (key, tail) = Split1(rest);
                if (key.Length == 0) return "HATA: yasakla <no|ad> [dakika(0=kalici)] [neden]";
                var (minTxt, reason) = Split1(tail);
                int minutes = 0;
                if (minTxt.Length > 0 && !int.TryParse(minTxt, out minutes)) { reason = tail; minutes = 0; }
                var p = Find(h, key);
                string name = p?.Name ?? key, addr = p?.Address ?? "";
                if (reason.Length == 0) reason = "yönetici yasakladı";
                h.Runtime.Bans.RemoveAll(b => b.Name == name.ToLowerInvariant());
                h.Runtime.Bans.Add(new BanEntry { Name = name.ToLowerInvariant(), Address = addr, Reason = reason, UntilUtc = minutes > 0 ? h.NowUnix + minutes * 60L : 0, By = "yönetici" });
                h.Persist();
                if (p != null) h.Kick(p.Peer, $"yasaklandın: {reason}");
                return $"yasaklandı: {name}{(minutes > 0 ? $" ({minutes} dk)" : " (kalıcı)")}";
            }
            case "yasak_kaldir":
            {
                if (rest.Length == 0) return "HATA: yasak_kaldir <ad>";
                int n = h.Runtime.Bans.RemoveAll(b => b.Name == rest.ToLowerInvariant());
                if (n > 0) h.Persist();
                return n > 0 ? $"yasak kaldırıldı: {rest}" : $"HATA: yasak yok: {rest}";
            }
            case "yasaklar":
            {
                h.Runtime.Bans.RemoveAll(b => b.UntilUtc != 0 && b.UntilUtc <= h.NowUnix);
                if (h.Runtime.Bans.Count == 0) return "yasak yok";
                return $"{h.Runtime.Bans.Count} yasak:" + string.Concat(h.Runtime.Bans.Select(b => $"\n  {b.Name}  {(b.UntilUtc == 0 ? "kalıcı" : $"{Math.Max(1, (b.UntilUtc - h.NowUnix + 59) / 60)} dk")}  {b.Reason}"));
            }
            case "kaydet": return h.Save(out var why2) ? "kaydedildi" : $"HATA: kaydedilemedi: {why2}";
            case "duyuru":
                if (rest.Length == 0) return "HATA: duyuru <metin>";
                h.Announce(rest);
                return "duyuruldu";
            case "sifre":
                if (rest.Length == 0) return h.Password.Length > 0 ? "sunucu şifreli" : "sunucu şifresiz";
                h.Password = rest == "-" ? "" : rest;
                h.Runtime.Password = h.Password;
                h.Persist();
                return rest == "-" ? "şifre kaldırıldı" : "şifre değişti";
            case "kapat":
                h.Shutdown();
                return "kapanıyor";
            default:
                return $"HATA: bilinmeyen komut '{cmd}' (yardim)";
        }
    }

    static (string head, string tail) Split1(string s)
    {
        s = s.Trim();
        int i = s.IndexOf(' ');
        return i < 0 ? (s, "") : (s[..i], s[(i + 1)..].Trim());
    }
}
