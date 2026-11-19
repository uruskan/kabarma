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
        Build("Kabarma", Loc.T("menu.subtitle"), dimBackground: false);
        var latest = _store.Latest();
        Item(Loc.T("menu.continue"), () => Load(latest), latest != null, Loc.T("menu.no_save"));
        Item(Loc.T("menu.new_game"), ShowNew);
        Item(Loc.T("menu.load"), ShowLoad, _store.List().Any(s => s.Exists));
        bool coop = _main.CoopAvailable;
        Item(Loc.T("menu.coop"), () => _main.OpenCoop(this), coop, Loc.T("menu.soon"));
        Item(Loc.T("menu.servers"), () => ServerListPanel.Open(this)); // adanmis sunucu listesi (server/ServerListPanel.cs)
        Item(Loc.T("settings.title"), ShowSettings);
        Item(Loc.T("menu.quit"), () => GetTree().Quit());
        if (Notice != null) { ClearContent(); Header(Loc.T("menu.notice"), Notice); }
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
        Header(Loc.T("menu.latest_save"));
        var info = _store.List().First(s => s.Slot == slot);
        Row(SlotLine(info), () => Load(slot));
    }

    void ShowNew()
    {
        ClearContent();
        Header(Loc.T("menu.new_game"), Loc.T("menu.new_game_note"));
        var presets = _data.Save.WorldPresets;
        _preset ??= _data.Save.DefaultPreset;
        var cur = presets.FirstOrDefault(p => p.Id == _preset);
        if (presets.Count > 0)
            Row(Loc.T("menu.world_preset", cur?.Name ?? _preset, cur?.Note), () =>
            {
                int i = presets.FindIndex(p => p.Id == _preset);
                _preset = presets[(i + 1) % presets.Count].Id;
                ShowNew();
            });
        Row(Loc.T("menu.newmoon_row", _newMoon.Count == 0 ? Loc.T("menu.newmoon_none") : Loc.T("menu.newmoon_n", _newMoon.Count)), ShowNewMoon);
        foreach (var s in _store.List())
        {
            var slot = s.Slot;
            if (_confirm == slot)
                Row(Loc.T("menu.overwrite", SlotLine(s)), () => { _confirm = null; _main.StartGame(null, slot, _preset, _newMoon.ToList()); });
            else
                Row(SlotLine(s), () => { if (s.Exists) { _confirm = slot; ShowNew(); } else _main.StartGame(null, slot, _preset, _newMoon.ToList()); });
        }
    }

    readonly System.Collections.Generic.HashSet<string> _newMoon = new();

    /// <summary>Yeni Ay (GDD 14.8): dünya kurulurken 12 tüzük maddesinden istenenler KS 1'den açık olur (her biri Maya kazancını %5 artırır).</summary>
    void ShowNewMoon()
    {
        ClearContent();
        Header(Loc.T("menu.newmoon"), Loc.T("menu.newmoon_note", (_data.Tuzuk.NewMoon.MayaPerClause * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
        foreach (var c in _data.Tuzuk.Clauses)
        {
            var id = c.Id;
            Row($"[{(_newMoon.Contains(id) ? "x" : " ")}] {c.Name}   ·   {c.Note}", () => { if (!_newMoon.Remove(id)) _newMoon.Add(id); ShowNewMoon(); });
        }
        Row(Loc.T("menu.newmoon_ok", _newMoon.Count), ShowNew);
    }

    string _preset;

    void ShowLoad()
    {
        ClearContent();
        Header(Loc.T("menu.load"));
        foreach (var s in _store.List()) { var slot = s.Slot; Row(SlotLine(s), () => Load(slot), s.Exists && s.Manifest != null); }
    }

    void Load(string slot)
    {
        if (slot == null) return;
        SaveGame g;
        try { g = _store.Read(slot, out var from); GD.Print($"MENU load {slot} from {from}"); }
        catch (SaveException e) { ClearContent(); Header(Loc.T("menu.load_failed"), e.Message); return; }
        _main.StartGame(g, slot);
    }
}
