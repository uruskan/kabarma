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

    protected override System.Action OpenControlsPanel => _d.ControlsPanel == null ? null : () => { Close(); _d.ControlsPanel.Toggle(); };

    public override void _Ready()
    {
        Build(Loc.T("menu.paused"), null, dimBackground: true);
        Item(Loc.T("menu.resume"), Close);
        Item(Loc.T("menu.save"), () => { _d.Saver.SaveNow("elle", quiet: true); ShowStatus(); }, _d.Saver.Enabled, Loc.T("menu.save_disabled"));
        Item(Loc.T("menu.photo"), () => { Close(); _d.Photo?.Enter(); });
        Item(Loc.T("settings.title"), ShowSettings);
        Item(Loc.T("menu.main_menu"), () => Leave(toDesktop: false), _d.GetParent() is Main);
        Item(Loc.T("menu.to_desktop"), () => Leave(toDesktop: true));
        if (Main.Args.ContainsKey("showpause")) GetTree().CreateTimer(2.0, processAlways: true).Timeout += ShotAndQuit; // gorsel sinama
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsEcho()) return;
        bool pause = InputMap.HasAction("pause_menu") && e.IsActionPressed("pause_menu");
        if (Visible && (pause || e.IsActionPressed("ui_cancel"))) { Close(); GetViewport().SetInputAsHandled(); return; }
        if (!pause) return; // kolun B'si (ui_cancel) menu acmaz; Esc ve Start (pause_menu) acar
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
        if (!s.Enabled) Header(Loc.T("menu.save_off"), Loc.T("menu.save_off_note"));
        else if (!_d.CanSave(out var why)) Header(Loc.T("menu.slot_title", slot), Loc.T("menu.cant_save_now", why));
        else Header(Loc.T("menu.slot_title", slot), s.SavesWritten > 0 && s.LastError == null ? Loc.T("menu.saved_n", s.SavesWritten) : Loc.T("menu.save_hint"));
        if (s.LastError != null && s.Enabled) Content.AddChild(Text(Loc.T("menu.last_error", s.LastError), 18, UiTheme.Red));
    }

    void Leave(bool toDesktop, bool force = false)
    {
        if (_d.Saver.Enabled && !force)
        {
            _d.ReleaseForSave();
            if (!_d.Saver.SaveNow("cikis", quiet: true))
            {
                // Gece surerken kayit alinmaz: oyuncu kaydedilmeden cikacagini bilsin ve onaylasin.
                ClearContent();
                Header(Loc.T("menu.save_failed"), Loc.T("menu.save_failed_note", _d.Saver.LastError, _d.Saver.SavesWritten > 0 ? Loc.T("menu.last_this_session") : Loc.T("menu.last_prev_auto")));
                Row(Loc.T("menu.quit_anyway"), () => Leave(toDesktop, force: true));
                Row(Loc.T("menu.cancel"), ShowStatus);
                return;
            }
        }
        GetTree().Paused = false;
        if (toDesktop) { GetTree().Quit(); return; }
        if (_d.GetParent() is Main m) m.CallDeferred(Main.MethodName.ReturnToMenu);
    }
}
