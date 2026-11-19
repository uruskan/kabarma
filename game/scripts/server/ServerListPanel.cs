using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Sunucu listesi (ana menu "Sunucular"; M5 sunucu akisi): kayitli sunucular (user://servers.json: ad, IP, port; sifre
/// saklanmaz), Steam sunucu tarayicisi (Steam aciksa) ve ENet dogrudan IP baglantisi. Baglan, Ortak oyun panelindeki gibi
/// --coop=client bayraklariyla sahneyi yeniden baslatir (ayni Director yolu); sifre --password ile Hello'ya gider.
/// </summary>
public partial class ServerListPanel : CanvasLayer
{
    GameData _data;
    LineEdit _name, _ip, _port, _pass, _player;
    VBoxContainer _list;
    Label _status;
    readonly SteamServerBrowser _steam = new();
    List<ServerEntry> _saved = new();
    int _shown = -1;

    public static ServerListPanel Open(Node parent)
    {
        var existing = parent.GetTree().Root.FindChild("ServerListPanel", true, false) as ServerListPanel;
        if (existing != null) { existing.QueueFree(); return null; }
        var p = new ServerListPanel { Name = "ServerListPanel", Layer = 60 };
        parent.AddChild(p);
        return p;
    }

    public override void _Ready()
    {
        _data = GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        Input.MouseMode = Input.MouseModeEnum.Visible;
        LoadSaved();
        var shade = new ColorRect { Color = UiTheme.A(UiTheme.Shade, 0.6f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);
        var panel = new PanelContainer { Theme = UiTheme.Theme };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.Panel, BorderWidthTop = UiTheme.Px(4), BorderColor = UiTheme.Teal,
            ContentMarginLeft = UiTheme.Px(24), ContentMarginRight = UiTheme.Px(24), ContentMarginTop = UiTheme.Px(18), ContentMarginBottom = UiTheme.Px(18),
        });
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(UiTheme.Px(680), 0);
        panel.GrowHorizontal = Control.GrowDirection.Both; panel.GrowVertical = Control.GrowDirection.Both;
        AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", UiTheme.Px(8));
        panel.AddChild(v);
        v.AddChild(Lbl(UiTheme.Upper(Loc.T("menu.servers")), 30, true, UiTheme.Ivory));
        v.AddChild(Lbl(Loc.T("server.desc"), 15, false, UiTheme.Muted));
        _player = Field(v, Loc.T("menu.your_name"), Main.Args.TryGetValue("name", out var nm) ? nm : SteamBoot.Ready ? SteamBoot.PersonaName : Loc.T("menu.player"));

        v.AddChild(Lbl(UiTheme.Upper(Loc.T("server.list")), 18, true, UiTheme.Teal));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, UiTheme.Px(190)), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_list);
        v.AddChild(scroll);
        var listRow = Row(v);
        listRow.AddChild(Btn(Loc.T("server.steam_refresh"), () => { _steam.Refresh(_data); _shown = -1; }));
        listRow.AddChild(Btn(Loc.T("menu.close"), QueueFree));

        v.AddChild(Lbl(UiTheme.Upper(Loc.T("server.direct")), 18, true, UiTheme.Teal));
        var ipRow = Row(v);
        _name = new LineEdit { PlaceholderText = Loc.T("server.ph_name"), CustomMinimumSize = new Vector2(UiTheme.Px(180), 0) };
        _ip = new LineEdit { Text = "127.0.0.1", PlaceholderText = "IP", CustomMinimumSize = new Vector2(UiTheme.Px(160), 0) };
        _port = new LineEdit { Text = _data.Net.Port.ToString(), PlaceholderText = "port", CustomMinimumSize = new Vector2(UiTheme.Px(80), 0) };
        _pass = new LineEdit { PlaceholderText = Loc.T("server.ph_pass"), Secret = true, CustomMinimumSize = new Vector2(UiTheme.Px(120), 0) };
        foreach (var c in new Control[] { _name, _ip, _port, _pass }) ipRow.AddChild(c);
        var go = Row(v);
        go.AddChild(Btn(Loc.T("server.connect"), () => Connect(_ip.Text.Trim(), _port.Text.Trim(), _pass.Text)));
        go.AddChild(Btn(Loc.T("server.add"), AddSaved));
        _status = Lbl("", 15, false, UiTheme.Muted);
        v.AddChild(_status);
        _steam.Refresh(_data); // Steam aciksa kendiliginden doldurur
    }

    // ---------------------------------------------------------------- liste

    string ListPath => _data.Server.Ping.ListFile;

    void LoadSaved()
    {
        try { if (FileAccess.FileExists(ListPath)) _saved = JsonSerializer.Deserialize<List<ServerEntry>>(FileAccess.GetFileAsString(ListPath)) ?? new(); }
        catch (Exception) { _saved = new(); }
    }

    void Persist()
    {
        using var f = FileAccess.Open(ListPath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(_saved));
    }

    void AddSaved()
    {
        if (!int.TryParse(_port.Text.Trim(), out int port) || _ip.Text.Trim().Length == 0) { _status.Text = Loc.T("server.need_ip"); return; }
        string name = _name.Text.Trim().Length > 0 ? _name.Text.Trim() : $"{_ip.Text.Trim()}:{port}";
        _saved.RemoveAll(s => s.Host == _ip.Text.Trim() && s.Port == port);
        _saved.Add(new ServerEntry { Name = name, Host = _ip.Text.Trim(), Port = port, Source = "kayitli" });
        Persist();
        _shown = -1;
        _status.Text = Loc.T("server.added");
    }

    void Rebuild()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        var all = _saved.Concat(_steam.Entries).ToList();
        if (all.Count == 0) _list.AddChild(Lbl(Loc.T("server.empty"), 15, false, UiTheme.Muted));
        foreach (var s in all)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", UiTheme.Px(8));
            var name = Lbl($"{s.Name}", 17, true, UiTheme.Ivory);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(name);
            row.AddChild(Lbl(s.Source == "steam" ? Loc.T(s.Password ? "server.steam_info_pw" : "server.steam_info", s.Players, s.MaxPlayers, s.Ping) : $"{s.Host}:{s.Port}", 14, false, UiTheme.Muted));
            var e = s;
            row.AddChild(Btn(Loc.T("server.connect"), () => Connect(e.Host, e.Port.ToString(), e.Password ? _pass.Text : "")));
            if (s.Source != "steam") row.AddChild(Btn(Loc.T("menu.delete"), () => { _saved.Remove(e); Persist(); _shown = -1; }));
            _list.AddChild(row);
        }
    }

    // ---------------------------------------------------------------- baglan

    void Connect(string ip, string port, string password)
    {
        if (ip.Length == 0 || !int.TryParse(port, out _)) { _status.Text = Loc.T("server.need_ip"); return; }
        var a = Main.Args;
        a["coop"] = "client"; a["transport"] = "enet"; a["connect"] = ip; a["port"] = port;
        a["name"] = string.IsNullOrWhiteSpace(_player.Text) ? Loc.T("menu.player") : _player.Text.Trim();
        if (password.Length > 0) a["password"] = password; else a.Remove("password");
        _status.Text = Loc.T("menu.connecting");
        GetTree().ReloadCurrentScene();
    }

    public override void _Process(double delta)
    {
        _steam.Tick((float)delta);
        int sig = _saved.Count * 1000 + _steam.Entries.Count;
        if (sig != _shown) { _shown = sig; Rebuild(); }
        if (_steam.Status.Length > 0 && _status.Text.Length == 0) _status.Text = _steam.Status;
        else if (_steam.Busy) _status.Text = _steam.Status;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { QueueFree(); GetViewport().SetInputAsHandled(); }
    }

    public override void _ExitTree() { _steam.Cancel(); }

    // ---------------------------------------------------------------- arayuz yardimcilari (CoopPanel ile ayni dil)

    static Label Lbl(string t, float size, bool bold, Color c)
    {
        var l = new Label { Text = t, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontOverride("font", UiTheme.Font(size, bold, condensed: bold));
        l.AddThemeFontSizeOverride("font_size", UiTheme.Px(size));
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    static HBoxContainer Row(Container parent)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", UiTheme.Px(8));
        parent.AddChild(h);
        return h;
    }

    static LineEdit Field(Container parent, string label, string value)
    {
        parent.AddChild(Lbl(label, 14, false, UiTheme.Muted));
        var e = new LineEdit { Text = value };
        parent.AddChild(e);
        return e;
    }

    static Button Btn(string text, Action onPress)
    {
        var b = new Button { Text = UiTheme.Upper(text), FocusMode = Control.FocusModeEnum.All };
        b.AddThemeFontOverride("font", UiTheme.Condensed);
        b.AddThemeFontSizeOverride("font_size", UiTheme.Px(17));
        var normal = new StyleBoxFlat { BgColor = UiTheme.HudSlot, ContentMarginLeft = UiTheme.Px(12), ContentMarginRight = UiTheme.Px(12), ContentMarginTop = UiTheme.Px(6), ContentMarginBottom = UiTheme.Px(6) };
        var hover = (StyleBoxFlat)normal.Duplicate(); hover.BgColor = UiTheme.PanelHover;
        var focus = (StyleBoxFlat)normal.Duplicate(); focus.BgColor = UiTheme.Selected; focus.BorderWidthLeft = UiTheme.Px(3); focus.BorderColor = UiTheme.SelectedEdge;
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("focus", focus);
        b.AddThemeStyleboxOverride("pressed", focus);
        b.Pressed += onPress;
        return b;
    }
}
