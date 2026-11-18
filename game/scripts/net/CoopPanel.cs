using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// "Ortak oyun" paneli (GDD 22.1; Rust tarzi duz panel): oyun kur (ENet ya da Steam lobisi), katil (Steam daveti ya da
/// IP), Steam davet penceresi, ayril. Secim oyunu --coop bayraklariyla yeniden baslatir (Main.Args + sahneyi yeniden
/// yukle): ayni Director yolu, tek oyuncuyla ayni kod. Ana menu (kayit akisi) bunu <see cref="Open"/> ile acar; oyun
/// icinde F4 (coop_panel; kolda Back).
/// </summary>
public partial class CoopPanel : CanvasLayer
{
    Director _d;
    LineEdit _name, _ip, _port, _lobby;
    Label _status;
    GameData _data;

    public static CoopPanel Open(Node parent, Director d = null)
    {
        var existing = parent.GetTree().Root.FindChild("CoopPanel", true, false) as CoopPanel;
        if (existing != null) { existing.QueueFree(); return null; } // ikinci basis kapatir
        var p = new CoopPanel { Name = "CoopPanel", _d = d, Layer = 60 };
        parent.AddChild(p);
        return p;
    }

    public override void _Ready()
    {
        _data = _d?.Data ?? GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        Input.MouseMode = Input.MouseModeEnum.Visible;
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
        panel.CustomMinimumSize = new Vector2(UiTheme.Px(520), 0);
        panel.GrowHorizontal = Control.GrowDirection.Both; panel.GrowVertical = Control.GrowDirection.Both;
        AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", UiTheme.Px(8));
        panel.AddChild(v);

        v.AddChild(Lbl(UiTheme.Upper("Ortak oyun"), 30, true, UiTheme.Ivory));
        v.AddChild(Lbl($"1–{_data.Net.MaxPlayers} oyuncu · dünya host'ta kalır · çanta kişisel", 15, false, UiTheme.Muted));
        _name = Field(v, "Adın", Main.Args.TryGetValue("name", out var nm) ? nm : SteamBoot.Ready ? SteamBoot.PersonaName : "Oyuncu");

        v.AddChild(Header("Kur"));
        var hostRow = Row(v);
        hostRow.AddChild(Btn("Steam lobisi kur", () => Restart("host", "steam")));
        hostRow.AddChild(Btn("Yerel ağda kur (ENet)", () => Restart("host", "enet")));
        if (_d?.Net?.Transport is SteamTransport st && _d.Net.IsHost) v.AddChild(Btn("Steam'den arkadaş davet et", st.Invite));

        v.AddChild(Header("Katıl"));
        _lobby = Field(v, "Steam lobi no (davetle gelince kendiliğinden)", Main.Args.GetValueOrDefault("connect_lobby", ""));
        v.AddChild(Btn("Steam davetiyle katıl", () => Restart("client", "steam")));
        var ipRow = Row(v);
        _ip = new LineEdit { Text = Main.Args.GetValueOrDefault("connect", "127.0.0.1"), PlaceholderText = "IP", CustomMinimumSize = new Vector2(UiTheme.Px(220), 0) };
        _port = new LineEdit { Text = Main.Args.GetValueOrDefault("port", _data.Net.Port.ToString()), PlaceholderText = "port", CustomMinimumSize = new Vector2(UiTheme.Px(90), 0) };
        ipRow.AddChild(_ip); ipRow.AddChild(_port);
        ipRow.AddChild(Btn("IP ile katıl (ENet)", () => Restart("client", "enet")));

        var bottom = Row(v);
        if (_d?.Net != null) bottom.AddChild(Btn("Ayrıl (tek oyuncu)", Leave));
        bottom.AddChild(Btn("Kapat", QueueFree));
        _status = Lbl(_d?.Net?.Status ?? "Tek oyuncu", 15, false, UiTheme.Muted);
        v.AddChild(_status);
    }

    static Label Lbl(string t, float size, bool bold, Color c)
    {
        var l = new Label { Text = t, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontOverride("font", UiTheme.Font(size, bold, condensed: bold));
        l.AddThemeFontSizeOverride("font_size", UiTheme.Px(size));
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    static Label Header(string t) => Lbl(UiTheme.Upper(t), 18, true, UiTheme.Teal);

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

    static Button Btn(string text, System.Action onPress)
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

    void Restart(string role, string transport)
    {
        var a = Main.Args;
        a["coop"] = role;
        a["transport"] = transport;
        a["name"] = string.IsNullOrWhiteSpace(_name.Text) ? "Oyuncu" : _name.Text.Trim();
        if (role == "client" && transport == "enet") { a["connect"] = _ip.Text.Trim(); a["port"] = _port.Text.Trim(); }
        if (role == "client" && transport == "steam" && ulong.TryParse(_lobby.Text.Trim(), out _)) a["connect_lobby"] = _lobby.Text.Trim();
        if (role == "host" && transport == "enet") a["port"] = _port.Text.Trim();
        _status.Text = role == "host" ? "Kuruluyor…" : "Bağlanılıyor…";
        GetTree().ReloadCurrentScene();
    }

    void Leave()
    {
        foreach (var k in new[] { "coop", "transport", "connect", "connect_lobby" }) Main.Args.Remove(k);
        GetTree().ReloadCurrentScene();
    }

    public override void _Process(double delta)
    {
        if (_d?.Net != null) _status.Text = _d.Net.Status;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { QueueFree(); GetViewport().SetInputAsHandled(); }
    }

    public override void _ExitTree()
    {
        if (_d != null && !Main.Args.ContainsKey("bot")) Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
