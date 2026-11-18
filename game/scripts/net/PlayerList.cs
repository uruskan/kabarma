using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyun ici oyuncu listesi (ortak oyun): sag ustte Rust tarzi duz panel. "player_list" (P / Back) basiliyken ya da
/// biri katilinca/ayrilinca 4 sn gorunur. Satir: ad, host isareti, durum (baygin, konusuyor), uzaklik; altta baglanti
/// durumu ve adres. Kayit ve oyuncu durumu tutmaz; NetSync'ten okur.
/// </summary>
public partial class PlayerList : CanvasLayer
{
    readonly Director _d;
    PanelContainer _panel;
    VBoxContainer _rows;
    Label _title, _foot;
    float _showT;
    int _lastCount = -1;

    public PlayerList(Director d) { _d = d; Layer = 40; }
    public PlayerList() { }

    public override void _Ready()
    {
        _panel = new PanelContainer { Theme = UiTheme.Theme };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.A(UiTheme.Panel, 0.92f),
            ContentMarginLeft = UiTheme.Px(14), ContentMarginRight = UiTheme.Px(14), ContentMarginTop = UiTheme.Px(10), ContentMarginBottom = UiTheme.Px(10),
            BorderWidthLeft = UiTheme.Px(3), BorderColor = UiTheme.Teal,
        });
        _panel.AnchorLeft = 1; _panel.AnchorRight = 1;
        _panel.OffsetLeft = -UiTheme.Px(340); _panel.OffsetRight = -UiTheme.Px(16);
        _panel.OffsetTop = UiTheme.Px(16);
        AddChild(_panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiTheme.Px(4));
        _panel.AddChild(box);
        _title = Lbl("", 20, true, UiTheme.Ivory);
        box.AddChild(_title);
        _rows = new VBoxContainer();
        box.AddChild(_rows);
        _foot = Lbl("", 14, false, UiTheme.Muted);
        box.AddChild(_foot);
        _panel.Visible = false;
        if (_d.Net != null) _d.Net.RosterChanged += () => _showT = 4f;
    }

    static Label Lbl(string t, float size, bool bold, Color c)
    {
        var l = new Label { Text = t };
        l.AddThemeFontOverride("font", UiTheme.Font(size, bold, condensed: bold));
        l.AddThemeFontSizeOverride("font_size", UiTheme.Px(size));
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public override void _Process(double delta)
    {
        var net = _d.Net;
        if (net == null) return;
        _showT -= (float)delta;
        bool held = InputMap.HasAction("player_list") && Input.IsActionPressed("player_list");
        bool show = held || _showT > 0 || net.Failed || (net.IsClient && !net.Joined);
        _panel.Visible = show;
        if (!show) return;
        var roster = net.Roster;
        _title.Text = UiTheme.Upper($"Ortak oyun · {roster.Count}/{net.Cfg.MaxPlayers}");
        if (roster.Count != _lastCount || held || _showT > 0)
        {
            _lastCount = roster.Count;
            foreach (var c in _rows.GetChildren()) c.QueueFree();
            foreach (var e in roster)
            {
                bool me = e.Peer == net.LocalPeer;
                string state = "";
                float dist = 0;
                if (!me && net.Avatars.TryGetValue(e.Peer, out var av))
                {
                    dist = av.GlobalPosition.DistanceTo(_d.Player.GlobalPosition);
                    state = av.Down ? "  · baygın" : av.Talking ? "  · konuşuyor" : "";
                }
                else if (me && _d.Player.Down) state = $"  · baygın {_d.Player.DownLeft:0} sn";
                string line = $"{(e.Host ? "★ " : "   ")}{e.Name}{(me ? " (sen)" : "")}{state}{(!me ? $"   {dist:0} m" : "")}{(e.Ready ? "" : "  · katılıyor")}";
                _rows.AddChild(Lbl(line, 16, me, me ? UiTheme.Ivory : UiTheme.Ink));
            }
        }
        _foot.Text = net.Status + (net.IsClient && net.SectionMismatches > 0 ? $" · eşleme {net.SectionMismatches}" : "");
        _foot.AddThemeColorOverride("font_color", net.Failed ? UiTheme.Red : UiTheme.Muted);
    }
}
