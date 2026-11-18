using Godot;

namespace Kabarma.Game;

/// <summary>
/// Oyun ici Esc menusu (Rust tarzi): Devam, Kaydet, Ayarlar, Ana menu (kaydeder), Masaustune cik (kaydeder). Acikken
/// oyun duraklar. Baska bir pencere (envanter, menu, cark, rapor, tus paneli) acikken Esc onu kapatir, bu menu acilmaz.
/// </summary>
public partial class PauseMenu : RustMenu
{
    readonly Director _d;
    Input.MouseModeEnum _mouseWas;

    public PauseMenu(Director d)
    {
        _d = d;
        Name = "PauseMenu";
        ProcessMode = ProcessModeEnum.Always;
        Layer = 60;
        Visible = false;
    }

    public bool Open => Visible;

    public override void _Ready()
    {
        Build("Duraklatıldı", null, dimBackground: true);
        Item("Devam et", Close);
        Item("Kaydet", () => { _d.Saver.SaveNow("elle", quiet: true); ShowStatus(); }, _d.Saver.Enabled, "bu oyunda kayıt kapalı");
        Item("Ayarlar", ShowSettings);
        Item("Ana menü", () => Leave(toDesktop: false), _d.GetParent() is Main);
        Item("Masaüstüne çık", () => Leave(toDesktop: true));
        if (Main.Args.ContainsKey("showpause")) GetTree().CreateTimer(2.0, processAlways: true).Timeout += ShotAndQuit; // gorsel sinama
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed("ui_cancel") || e.IsEcho()) return;
        if (Visible) { Close(); GetViewport().SetInputAsHandled(); return; }
        var h = _d.Hud;
        if (h == null || h.MenuOpen || h.WheelOpen || h.ReportOpen || h.CraftTyping || (_d.ControlsPanel?.Visible ?? false)) return;
        OpenMenu();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>--showpause: menuyu acar, goruntuyu progress/shots/ altina yazar, cikar (oyun duraklayinca Main --shots calismaz).</summary>
    void ShotAndQuit()
    {
        OpenMenu();
        GetTree().CreateTimer(1.0, processAlways: true).Timeout += () =>
        {
            var dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/shots";
            DirAccess.MakeDirRecursiveAbsolute(dir);
            string pre = Main.Args.TryGetValue("shot_prefix", out var p) ? p : "pause";
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/{pre}_esc.png");
            GD.Print($"SCREENSHOT {dir}/{pre}_esc.png");
            GetTree().Quit();
        };
    }

    void OpenMenu()
    {
        _mouseWas = Input.MouseMode;
        Visible = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        ShowStatus();
        FocusFirst();
    }

    void Close()
    {
        Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = _mouseWas == Input.MouseModeEnum.Visible ? Input.MouseModeEnum.Captured : _mouseWas;
    }

    void ShowStatus()
    {
        ClearContent();
        var s = _d.Saver;
        string slot = _d.SaveSlot ?? "-";
        if (!s.Enabled) Header("Kayıt kapalı", "Bu oyun bir sınama ya da bot koşusu; kayıt yazılmaz.");
        else if (!_d.CanSave(out var why)) Header($"Yuva: {slot}", $"Şu an kaydedilemez: {why}. Uygun ilk anda otomatik kaydedilir.");
        else Header($"Yuva: {slot}", s.SavesWritten > 0 && s.LastError == null ? $"Bu oturumda {s.SavesWritten} kez kaydedildi." : "Kaydet, oyunu bu yuvaya yazar.");
        if (s.LastError != null && s.Enabled) Content.AddChild(Text($"Son kayıt hatası: {s.LastError}", 18, UiTheme.Red));
    }

    void Leave(bool toDesktop)
    {
        if (_d.Saver.Enabled) _d.Saver.SaveNow("cikis", quiet: true);
        GetTree().Paused = false;
        if (toDesktop) { GetTree().Quit(); return; }
        if (_d.GetParent() is Main m) m.CallDeferred(Main.MethodName.ReturnToMenu);
    }
}
