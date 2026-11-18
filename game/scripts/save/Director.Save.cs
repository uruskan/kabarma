using System;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director'un kayit defteri (TEKNIK 9.1): Sim bolumleri (GameState [SaveSection]) + Godot katmaninin bolumleri
/// (Director.SaveSections.cs). Godot'ta durum tutan bir sistem kendi kurulumunda bir satirla baglanir:
/// <code>d.Save.Add(SaveSection.Json("pen.gates", 1, CaptureGates, ApplyGates, SaveOrder.Spawn));</code>
/// Bolum Godot tipi yazmaz (Vector3 yerine float[]); dunyadaki ornekler EntityRecord ve State.Ids ile anilir.
/// Statik degil: her Director (her oyun) kendi defterini kurar.
/// </summary>
public partial class Director
{
    SaveRegistry _save;

    /// <summary>Ilk eriside Sim bolumleriyle kurulur (State hazir olmali).</summary>
    public SaveRegistry Save => _save ??= new SaveRegistry().AddRange(State.SaveSections());

    /// <summary>Kurulmadan once verilirse oyun bu kayittan acilir (Main.StartGame). Kapsul acilisi atlanir.</summary>
    public SaveGame PendingLoad { get; set; }
    /// <summary>Kayit klasoru (user://...). Null: bu oyun kayit yazmaz (otomasyon kosulari).</summary>
    public string SaveRoot { get; set; }
    /// <summary>Oyunun yuvasi (yuva1..N).</summary>
    public string SaveSlot { get; set; }
    /// <summary>Yuklenen kaydin dokumu (sinama ve gunluk).</summary>
    public RestoreReport LastRestore { get; private set; }
    /// <summary>Kayittan acildiysa kayit uygulandiktan hemen sonra (ayni karede) bir kez.</summary>
    public event Action Loaded;
    /// <summary>Kapsul acilisi oynatilmaz (yuklenen oyun).</summary>
    public bool SkipIntro => PendingLoad != null;
    public SaveService Saver { get; private set; }
    /// <summary>Yeni oyunun dunya ayari (GDD 25; menude secilir). Null: save.json defaultPreset.</summary>
    public string NewPreset { get; set; }

    /// <summary>Director._Ready sonunda bir kez: bolumleri kaydeder, otomatik kaydi kurar, bekleyen kaydi uygular.</summary>
    void InitSave()
    {
        RegisterGodotSections();
        if (PendingLoad == null && !string.IsNullOrEmpty(NewPreset)) State.Settings.Preset = NewPreset;
        SaveRoot ??= Main.Args.TryGetValue("save_root", out var r) ? r : null;
        SaveSlot ??= Main.Args.TryGetValue("save_slot", out var sl) ? sl : Data.Save.DefaultSlot;
        // Otomasyon kosulari (komut satiri argumanli) sahibin kaydina hic yazmaz: yalniz test kokune.
        bool automation = Main.Args.Count > 0 && !Main.Args.ContainsKey("menu");
        if (SaveRoot != null && automation && !SaveRoot.StartsWith(Data.Save.TestRoot, StringComparison.Ordinal))
        {
            GD.PushWarning($"SAVE kapali: otomasyon kosusu {SaveRoot} altina yazamaz (yalniz {Data.Save.TestRoot})");
            SaveRoot = null;
        }
        Saver = new SaveService(this) { Name = "SaveService" };
        AddChild(Saver);
        if (Bot == null && (!automation || Main.Args.ContainsKey("pausemenu"))) AddChild(new PauseMenu(this)); // Esc: Kaydet ve Cikis (bot ve sinama kosularinda yok)
        if (PendingLoad != null) CallDeferred(nameof(ApplyPendingLoad));
    }

    /// <summary>Kurulumdan sonraki ilk karede: kaydi yerine koy, gorunumleri yenile.</summary>
    void ApplyPendingLoad()
    {
        var g = PendingLoad;
        if (g == null) return;
        try
        {
            LastRestore = Save.Restore(g);
        }
        catch (Exception e)
        {
            GD.PushError($"LOAD basarisiz: {e.Message}");
            LastRestore = null;
            Loaded?.Invoke();
            return;
        }
        FarmView.Rebuild();
        Saver.OnLoaded(g.Manifest);
        GD.Print($"LOAD ok day={g.Manifest.Day} {LastRestore}");
        Loaded?.Invoke();
    }

    /// <summary>
    /// Kayit simdi alinabilir mi: gece degil, rapor ve menu kapali, oyuncu ayakta, elde yuk yok, araba itilmiyor, acilis
    /// bitti. Gece durumu (KabarmaNight) kaydedilmez (durum_denetimi.md bulgu 4).
    /// </summary>
    public bool CanSave(out string why)
    {
        why = Phase is not (Act.Day or Act.Free) ? "gece" :
            NightActive ? "gece" :
            Hud.ReportOpen ? "rapor acik" :
            Player.Down ? "oyuncu yerde" :
            Capa.Held != null ? "elde yuk var" :
            Carts.Any(c => IsInstanceValid(c) && c.Pushed) ? "araba itiliyor" :
            !Pod.IntroDone ? "acilis" : "";
        return why == "";
    }

    /// <summary>Bildirimle birlikte dunyanin anlik goruntusu (dosyasiz; ortak oyunda da kullanilir).</summary>
    public SaveGame CaptureWorld(string reason = "")
    {
        var m = State.Manifest(SaveSlot ?? "", reason);
        m.Game = ProjectSettings.GetSetting("application/config/version", "dev").AsString();
        if (string.IsNullOrEmpty(m.Game)) m.Game = "dev";
        m.SavedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        m.PlaySeconds = Saver?.PlaySeconds ?? 0;
        return Save.Capture(SaveScope.World, m);
    }

    public SaveStore Store => SaveRoot == null ? null : new SaveStore(ProjectSettings.GlobalizePath(SaveRoot), Data.Save);
}
