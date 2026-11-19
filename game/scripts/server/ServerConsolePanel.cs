using Godot;

namespace Kabarma.Game;

/// <summary>
/// Oyun ici yonetim konsolu (F8; ortak oyunda istemcide): bir satir komut host'a "srv.cmd" olarak gider (yonetici olmak icin
/// 'giris &lt;sifre&gt;'); cevap "srv.reply" bolumuyle gelir ve burada ve Hud'da gorunur. Tek oyuncuda ve host'ta acilmaz.
/// </summary>
public partial class ServerConsolePanel : CanvasLayer
{
    Director _d;
    LineEdit _line;
    Label _log;
    static ServerConsolePanel _open;
    /// <summary>Son host cevabi (arayuz ve sinama; oyun durumu degil).</summary>
    public static string LastReply = "";

    public static void Toggle(Director d)
    {
        if (d?.Net == null || !d.Net.IsClient || !d.Net.Joined) { d?.Hud?.Hint("Yönetim konsolu yalnız bir sunucuya bağlıyken açılır", 2f); return; }
        if (_open != null && IsInstanceValid(_open)) { _open.QueueFree(); _open = null; return; }
        _open = new ServerConsolePanel { Name = "ServerConsole", _d = d, Layer = 61 };
        d.AddChild(_open);
    }

    /// <summary>Cevabi acik panele yazar.</summary>
    public static void Echo(string text)
    {
        LastReply = text;
        if (_open != null && IsInstanceValid(_open)) _open._log.Text += text + "\n";
    }

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var box = new PanelContainer { Theme = UiTheme.Theme };
        box.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = UiTheme.A(UiTheme.Panel, 0.92f), ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 });
        box.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        box.CustomMinimumSize = new Vector2(0, 220);
        AddChild(box);
        var v = new VBoxContainer();
        box.AddChild(v);
        v.AddChild(new Label { Text = "SUNUCU YÖNETİMİ (F8 kapatır) · giris <şifre> · yardim" });
        _log = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        v.AddChild(_log);
        _line = new LineEdit { PlaceholderText = "komut" };
        _line.TextSubmitted += Submit;
        v.AddChild(_line);
        _line.CallDeferred(Control.MethodName.GrabFocus);
    }

    void Submit(string text)
    {
        _line.Text = "";
        if (text.Trim().Length == 0) return;
        _log.Text += "> " + text + "\n";
        _d.Net.Forward("srv.cmd", w => w.Str(text));
        _line.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (_open == this) _open = null;
        if (_d != null && !Main.Args.ContainsKey("bot")) Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
