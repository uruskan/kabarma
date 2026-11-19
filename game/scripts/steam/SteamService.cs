using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Kabarma.Sim;
using Steamworks;
using FileAccess = Godot.FileAccess;

namespace Kabarma.Game;

/// <summary>
/// Steam entegrasyonu (M6 steam akisi): basarimlar, bulut ve zengin durum. Director'un cocugu. Steam yoksa (kapali, DLL yok,
/// otomasyon kosusu, adanmis sunucu) Steam cagrisi yapilmaz ve hata gorunmez; yerel defter (steam_state.json) yine yazilir:
/// acilan her basarim aninda deftere girer ve Steam acilinca bekleyenler iletilir. Basarim kimligi achievements.json id'si
/// (Steam API adiyla ayni). Yukleme ya da yayin yapmaz: yalniz calisan Steam istemcisine konusur.
/// </summary>
public partial class SteamService : Node
{
    readonly Director _d;
    SteamFile Cfg => _d.Data.Steam;
    SteamLedger _ledger = new();
    bool _steam, _statsReady, _storeDirty, _initTried;
    float _statsWait, _syncT, _presenceT = 1f;
    int _savesSeen;
    string _lastPresence = "";
    Callback<UserStatsReceived_t> _statsCb;

    /// <summary>Sinama ve rapor sayaclari.</summary>
    public int LedgerWrites, SteamUnlocks, CloudPushes;
    public string Presence => _lastPresence;
    public bool SteamOn => _steam;
    public SteamLedger Ledger => _ledger;

    public SteamService(Director d) { _d = d; Name = "SteamService"; }

    public override void _Ready()
    {
        if (Main.Args.ContainsKey("server")) { QueueFree(); return; } // adanmis sunucuda Steam istemcisi yok
        // Otomasyon kosulari sahibin defterine yazmaz (sahte basarim Steam'e gitmesin): test kokune yazar.
        _automation = Main.Args.Count > 0 && !Main.Args.ContainsKey("menu") && !Main.Args.ContainsKey("steam_on");
        if (_automation) DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_d.Data.Save.TestRoot));
        _ledger = SteamLedger.FromJson(ReadText(LedgerPath));
        // Zaten acik basarimlar deftere gecer (karakter dosyasindan yuklenen oyun; defter baska makineden silinmis olabilir)
        _d.Loaded += SeedFromBook;
        _d.State.Achievements.UnlockedEvent += OnUnlocked;
        SeedFromBook();
        // Steam yalniz gercek oyunda: otomasyon kosularinda ve testlerde acilmaz (Main.Args.Count>0, --menu haric)
        _initTried = _automation;
        _savesSeen = _d.Saver?.SavesWritten ?? 0;
    }

    bool _automation;
    /// <summary>Defter yolu: oyunda steam.json ledgerFile, otomasyon kosusunda test koku.</summary>
    public string LedgerPath => _automation ? _d.Data.Save.TestRoot.TrimEnd('/') + "/steam_state.json" : Cfg.LedgerFile;

    string ReadText(string path) => FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : "";

    void WriteLedger()
    {
        try
        {
            using var f = FileAccess.Open(LedgerPath, FileAccess.ModeFlags.Write);
            f?.StoreString(_ledger.ToJson());
            LedgerWrites++;
        }
        catch (Exception) { /* yazilamadi: sonraki acilista yine denenir */ }
    }

    void SeedFromBook()
    {
        bool any = false;
        foreach (var id in _d.State.Achievements.Unlocked) any |= _ledger.Record(id, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (any) WriteLedger();
    }

    void OnUnlocked(Achievement a)
    {
        if (_ledger.Record(a.Id, DateTimeOffset.UtcNow.ToUnixTimeSeconds())) WriteLedger(); // yerel kayit: Steam olsun olmasin
        _syncT = 0;
        GD.Print($"STEAM ledger {a.Id} steam={(_steam ? "acik" : "kapali")}");
    }

    // ---------------------------------------------------------------- kare

    public override void _Process(double delta)
    {
        float dt = (float)delta / Mathf.Max(0.01f, (float)Engine.TimeScale);
        if (!_initTried) { _initTried = true; TryInit(); }
        if (!_steam) return;
        SteamBoot.Pump();
        if (!_statsReady)
        {
            _statsWait += dt;
            if (_statsWait > Cfg.Achievements.StatsWaitSeconds) _statsReady = true; // geri cagirim gelmedi: yine de dene
        }
        _syncT -= dt;
        if (_syncT <= 0) { _syncT = Cfg.Achievements.SyncSeconds; SyncAchievements(); }
        _presenceT -= dt;
        if (_presenceT <= 0) { _presenceT = Cfg.Presence.Seconds; UpdatePresence(); }
        int saves = _d.Saver?.SavesWritten ?? 0;
        if (saves != _savesSeen) { _savesSeen = saves; PushCloud(); }
    }

    void TryInit()
    {
        try
        {
            if (!SteamBoot.Ensure((uint)_d.Data.Net.Steam.AppId)) { GD.Print($"STEAM kapali ({SteamBoot.Error}): yerel defterle devam"); return; }
            _steam = true;
            _statsCb = Callback<UserStatsReceived_t>.Create(_ => _statsReady = true);
            GD.Print("STEAM servis acik (basarim, bulut, zengin durum)");
        }
        catch (Exception e) { _steam = false; GD.Print($"STEAM atlandi: {e.GetType().Name}"); }
    }

    // ---------------------------------------------------------------- basarimlar

    void SyncAchievements()
    {
        if (!_statsReady) return;
        try
        {
            foreach (var id in _ledger.Pending().ToList())
            {
                string api = Cfg.Achievements.ApiPrefix + id;
                if (SteamUserStats.GetAchievement(api, out bool have) && !have && SteamUserStats.SetAchievement(api)) { SteamUnlocks++; _storeDirty = true; }
                _ledger.MarkSynced(id); // zaten acik ya da gonderildi; tanimsiz API adi da bir daha denenmez
                WriteLedger();
            }
            if (_storeDirty) { SteamUserStats.StoreStats(); _storeDirty = false; }
        }
        catch (Exception e) { GD.Print($"STEAM basarim iletilemedi: {e.GetType().Name}"); }
    }

    // ---------------------------------------------------------------- zengin durum

    void UpdatePresence()
    {
        try
        {
            var p = _d.Player.GlobalPosition;
            string biome = _d.State.BiomeMap.BiomeAt(p.X, p.Z);
            int ks = _d.State.UnlockedKs, players = _d.Net?.PlayerCount ?? 1;
            bool night = _d.NightActive;
            string text = SteamPresence.Text(Cfg, biome, ks, players, night);
            if (text == _lastPresence) return;
            _lastPresence = text;
            SteamFriends.SetRichPresence("status", text);
            SteamFriends.SetRichPresence("biome", SteamPresence.BiomeName(Cfg, biome));
            SteamFriends.SetRichPresence("ks", ks.ToString());
            SteamFriends.SetRichPresence("players", players.ToString());
            SteamFriends.SetRichPresence("steam_player_group_size", players.ToString());
        }
        catch (Exception) { /* Steam gitti: sessizce */ }
    }

    // ---------------------------------------------------------------- bulut

    string SaveDir => _d.SaveRoot == null ? null : ProjectSettings.GlobalizePath(_d.SaveRoot).Replace('\\', '/').TrimEnd('/');

    /// <summary>Her basarili kayittan sonra: yereldeki daha yeni dosyalari bulutta gunceller.</summary>
    void PushCloud()
    {
        if (!Cfg.Cloud.Enabled || SaveDir == null || _d.SaveRoot.StartsWith(_d.Data.Save.TestRoot, StringComparison.Ordinal)) return;
        try
        {
            if (!SteamRemoteStorage.IsCloudEnabledForAccount() || !SteamRemoteStorage.IsCloudEnabledForApp()) return;
            foreach (var path in Directory.EnumerateFiles(SaveDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(SaveDir, path).Replace('\\', '/');
                var fi = new FileInfo(path);
                if (!SteamCloudRules.Eligible(rel, fi.Length, Cfg.Cloud)) continue;
                long local = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds();
                string name = "saves/" + rel;
                long cloud = SteamRemoteStorage.FileExists(name) ? SteamRemoteStorage.GetFileTimestamp(name) : 0;
                if (SteamCloudRules.Decide(local, cloud, _ledger.CloudPushed.GetValueOrDefault(rel)) != CloudAction.Push) continue;
                var bytes = File.ReadAllBytes(path);
                if (SteamRemoteStorage.FileWrite(name, bytes, bytes.Length)) { _ledger.CloudPushed[rel] = local; CloudPushes++; }
            }
            WriteLedger();
        }
        catch (Exception e) { GD.Print($"STEAM bulut yazilamadi: {e.GetType().Name}"); }
    }

    /// <summary>
    /// Acilista (ana menu kurulmadan once): bulutta olan ya da daha yeni olan kayit dosyalari yerele iner. Steam yoksa ya da
    /// bulut kapaliysa bir sey yapmaz. Otomasyon kosularinda cagrilmaz (Main).
    /// </summary>
    public static int PullOnBoot()
    {
        int pulled = 0;
        try
        {
            var data = GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
            if (!data.Steam.Cloud.Enabled) return 0;
            if (!SteamBoot.Ensure((uint)data.Net.Steam.AppId)) return 0;
            if (!SteamRemoteStorage.IsCloudEnabledForAccount() || !SteamRemoteStorage.IsCloudEnabledForApp()) return 0;
            string dir = ProjectSettings.GlobalizePath(data.Save.Root).Replace('\\', '/').TrimEnd('/');
            var ledger = SteamLedger.FromJson(FileAccess.FileExists(data.Steam.LedgerFile) ? FileAccess.GetFileAsString(data.Steam.LedgerFile) : "");
            int n = SteamRemoteStorage.GetFileCount();
            for (int i = 0; i < n; i++)
            {
                string name = SteamRemoteStorage.GetFileNameAndSize(i, out int size);
                if (!name.StartsWith("saves/") || size <= 0 || size > data.Steam.Cloud.MaxFileBytes) continue;
                string rel = name["saves/".Length..];
                string path = dir + "/" + rel;
                long local = File.Exists(path) ? new DateTimeOffset(new FileInfo(path).LastWriteTimeUtc).ToUnixTimeSeconds() : 0;
                long cloud = SteamRemoteStorage.GetFileTimestamp(name);
                if (SteamCloudRules.Decide(local, cloud, ledger.CloudPushed.GetValueOrDefault(rel)) != CloudAction.Pull) continue;
                var buf = new byte[size];
                if (SteamRemoteStorage.FileRead(name, buf, size) != size) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, buf);
                File.SetLastWriteTimeUtc(path, DateTimeOffset.FromUnixTimeSeconds(cloud).UtcDateTime);
                ledger.CloudPushed[rel] = cloud;
                pulled++;
            }
            if (pulled > 0)
            {
                using var f = FileAccess.Open(data.Steam.LedgerFile, FileAccess.ModeFlags.Write);
                f?.StoreString(ledger.ToJson());
                GD.Print($"STEAM bulut: {pulled} dosya indi");
            }
        }
        catch (Exception e) { GD.Print($"STEAM bulut indirilemedi: {e.GetType().Name}"); }
        return pulled;
    }
}
