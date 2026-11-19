using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ana menu (Rust tarzi): Devam, Yeni oyun, Yukle (yuvalar: tarih ve oyun gunuyle), Ortak oyun (ortak akisi doldurur),
/// Ayarlar, Cikis. Yalniz argumansiz acilista (OYNA.bat) ya da --menu ile gelir; otomasyon kosulari menuyu atlar.
/// </summary>
public partial class MainMenu : RustMenu
{
    readonly Main _main;
    readonly GameData _data;
    readonly SaveStore _store;
    string _confirm; // uzerine yazma onayi bekleyen yuva
    /// <summary>Acilista gosterilecek uyari (yukleme basarisiz).</summary>
    public string Notice { get; set; }

    public MainMenu(Main main, GameData data)
    {
        _main = main;
        _data = data;
        _store = new SaveStore(ProjectSettings.GlobalizePath(main.MenuRoot(data)), data.Save);
        Name = "MainMenu";
    }

    public override void _Ready()
    {
        UiTheme.Load(_data.Ui);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Build("Kabarma", "Kooperatif çiftliği · Altın Çayır", dimBackground: false);
        var latest = _store.Latest();
        Item("Devam", () => Load(latest), latest != null, "kayıt yok");
        Item("Yeni oyun", ShowNew);
        Item("Yükle", ShowLoad, _store.List().Any(s => s.Exists));
        bool coop = _main.CoopAvailable;
        Item("Ortak oyun", () => _main.OpenCoop(this), coop, "yakında");
        Item("Sunucular", () => ServerListPanel.Open(this)); // adanmis sunucu listesi (server/ServerListPanel.cs)
        Item("Ayarlar", ShowSettings);
        Item("Çıkış", () => GetTree().Quit());
        if (Notice != null) { ClearContent(); Header("Uyarı", Notice); }
        else if (latest != null) ShowLatest(latest); else ShowNew();
        FocusFirst();
        // Gorsel sinama (--menu_auto=load): Yukle panelini gosterir, sonra en son kaydi acar.
        if (Main.Args.GetValueOrDefault("menu_auto") == "load" && latest != null)
        {
            GetTree().CreateTimer(1.0).Timeout += ShowLoad;
            GetTree().CreateTimer(3.5).Timeout += () => Load(latest);
        }
    }

    void ShowLatest(string slot)
    {
        ClearContent();
        Header("Son kayıt");
        var info = _store.List().First(s => s.Slot == slot);
        Row(SlotLine(info), () => Load(slot));
    }

    void ShowNew()
    {
        ClearContent();
        Header("Yeni oyun", "Dünya ayarını seç, sonra bir yuva. Dolu yuvada eski kayıt yedeklenir, sonra üzerine yazılır.");
        var presets = _data.Save.WorldPresets;
        _preset ??= _data.Save.DefaultPreset;
        var cur = presets.FirstOrDefault(p => p.Id == _preset);
        if (presets.Count > 0)
            Row($"Dünya ayarı: {cur?.Name ?? _preset}   ·   {cur?.Note}   (tıkla: değiştir)", () =>
            {
                int i = presets.FindIndex(p => p.Id == _preset);
                _preset = presets[(i + 1) % presets.Count].Id;
                ShowNew();
            });
        foreach (var s in _store.List())
        {
            var slot = s.Slot;
            if (_confirm == slot)
                Row($"{SlotLine(s)}   ·   ÜZERİNE YAZ?  (tekrar tıkla)", () => { _confirm = null; _main.StartGame(null, slot, _preset); });
            else
                Row(SlotLine(s), () => { if (s.Exists) { _confirm = slot; ShowNew(); } else _main.StartGame(null, slot, _preset); });
        }
    }

    string _preset;

    void ShowLoad()
    {
        ClearContent();
        Header("Yükle");
        foreach (var s in _store.List()) { var slot = s.Slot; Row(SlotLine(s), () => Load(slot), s.Exists && s.Manifest != null); }
    }

    void Load(string slot)
    {
        if (slot == null) return;
        SaveGame g;
        try { g = _store.Read(slot, out var from); GD.Print($"MENU load {slot} from {from}"); }
        catch (SaveException e) { ClearContent(); Header("Yüklenemedi", e.Message); return; }
        _main.StartGame(g, slot);
    }
}
