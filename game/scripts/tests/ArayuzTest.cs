using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// M6 arayuz sinamasi (--test=arayuz). 1) Ayarlar saklanir ve yuklenir (ayri dosya). 2) Ayarlar sayfasi: her satir var, tiklayinca
/// ayar degisir. 3) Renk korlugu suzgeci, altyazi (aci/kapali, boy), ekran sarsintisi. 4) Paneller tek dilde (yuvarlak kose yok).
/// 5) Fotograf modu: serbest kamera, arayuz gizleme, poz, vizor yuzu, ifade, cikista geri donus; pencereli kosuda PNG cekimi.
/// Pencereli kosuda goruntuler progress/m6/arayuz/ altina yazilir.
/// </summary>
public partial class ArayuzTest : Node
{
    static readonly Vector3 Site = new(-400f, 220f, 400f);
    Director _d;
    readonly List<string> _fails = new();
    int _checks, _shots;
    bool _window;
    string _dir;
    PauseMenu _menu;

    public override void _Ready() { _window = DisplayServer.GetName() != "headless"; CallDeferred(nameof(Begin)); }

    async void Begin()
    {
        try { await Run(); } catch (Exception e) { _fails.Add("istisna: " + e.Message); GD.Print(e.ToString()); }
        GD.Print(_fails.Count == 0 ? $"RESULT ok arayuz checks={_checks} shots={_shots}" : $"RESULT fail arayuz ({_fails.Count}/{_checks}) {string.Join("; ", _fails)}");
        GetTree().Quit(_fails.Count == 0 ? 0 : 1);
    }

    void Check(bool ok, string what) { _checks++; if (!ok) { _fails.Add(what); GD.Print("ARAYUZ FAIL " + what); } }
    async Task Seconds(float s) { int n = Mathf.CeilToInt(s * Engine.PhysicsTicksPerSecond); for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    async Task Shot(string name)
    {
        await Seconds(0.3f);
        if (!_window) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{name}.png");
        _shots++;
    }

    /// <summary>Panelin kendi stilinde yuvarlak kose yok (ic odak cizgileri Godot varsayilanidir, sayilmaz).</summary>
    static bool Square(Control c) => c.GetThemeStylebox("panel") is not StyleBoxFlat f || (f.CornerRadiusTopLeft == 0 && f.CornerRadiusTopRight == 0 && f.CornerRadiusBottomLeft == 0 && f.CornerRadiusBottomRight == 0);

    async Task Run()
    {
        while ((_d = GetParent().GetNodeOrNull<Director>("Director")) == null || _d.Player == null || _d.Photo == null) await Seconds(0.05f);
        while (!(_d.Pod?.IntroDone ?? true)) await Seconds(0.05f);
        await Seconds(0.3f);
        _dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/m6/arayuz";
        DirAccess.MakeDirRecursiveAbsolute(_dir);
        var floor = new StaticBody3D();
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(160, 1, 160) } });
        AddChild(floor);
        floor.GlobalPosition = Site + Vector3.Down * 0.5f;
        _d.Player.GlobalPosition = Site + Vector3.Up * 0.05f;
        _d.Player.Velocity = Vector3.Zero;
        await Seconds(0.8f);

        // ---- 1) ayarlar saklanir ----
        var tmp = UserSettings.Detached("user://test_settings.cfg");
        tmp.SetColorBlind(2); tmp.SetShake(false); tmp.SetSubtitles(false); tmp.SetSubtitleSize(3);
        var back = UserSettings.Detached("user://test_settings.cfg");
        Check(back.ColorBlind == 2 && !back.Shake && !back.Subtitles && back.SubtitleSize == 3, "ayarlar saklanip geri yuklenmedi");
        back.SetColorBlind(0); back.SetShake(true); back.SetSubtitles(true); back.SetSubtitleSize(1);
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://test_settings.cfg"));
        var us = UserSettings.Current;
        _d.Access.Apply();

        // ---- 2) ayarlar sayfasi ----
        _menu = new PauseMenu(_d);
        AddChild(_menu);
        _menu.OpenSettingsPage();
        await Seconds(0.2f);
        foreach (var row in RustMenu.SettingsRows) Check(_menu.FindButton(row) != null, $"ayar satiri yok: {row}");
        var cb = _menu.FindButton("Renk körlüğü");
        int cb0 = us.ColorBlind;
        cb?.EmitSignal(BaseButton.SignalName.Pressed);
        await Seconds(0.2f);
        Check(us.ColorBlind == (cb0 + 1) % UiTheme.Data.Access.ColorBlindModes.Count, "renk korlugu satiri ayari degistirmedi");
        _menu.FindButton("Ekran sarsıntısı")?.EmitSignal(BaseButton.SignalName.Pressed);
        await Seconds(0.2f);
        Check(!us.Shake, "sarsinti satiri ayari kapatmadi");
        _menu.FindButton("Ekran sarsıntısı")?.EmitSignal(BaseButton.SignalName.Pressed);
        await Seconds(0.1f);
        Check(us.Shake, "sarsinti satiri geri acmadi");
        Check(_menu.FindButton("Tuş atamaları") != null && !_menu.FindButton("Tuş atamaları").Disabled == (_d.ControlsPanel != null), "tus atamalari satiri");
        if (_window) { _menu.Visible = true; await Shot("01_ayarlar_sayfasi"); }
        _menu.Visible = false;

        // ---- 3) renk korlugu, altyazi, sarsinti ----
        us.SetColorBlind(0);
        await Seconds(0.1f);
        Check(!_d.Access.Active && _d.Access.Mode == 0, "suzgec kapaliyken gorunmemeli");
        if (_window) await Shot("02_renk_normal");
        foreach (int m in new[] { 1, 2, 3 })
        {
            us.SetColorBlind(m);
            await Seconds(0.15f);
            Check(_d.Access.Mode == m, $"suzgec modu {m}");
            if (_window) await Shot($"02_renk_korlugu_{m}");
        }
        us.SetColorBlind(0);
        int baseSize = _d.Hud.SubtitleFontSize;
        us.SetSubtitleSize(3);
        await Seconds(0.1f);
        Check(_d.Hud.SubtitleFontSize > baseSize, $"altyazi boyu buyumedi ({baseSize}->{_d.Hud.SubtitleFontSize})");
        us.SetSubtitleSize(1);
        _d.Hud.Say("Altyazı denemesi: Suna konuşuyor.");
        await Seconds(0.6f);
        Check(_d.Hud.SubtitleBoxVisible, "altyazi acikken Suna kutusu gorunmeli");
        if (_window) await Shot("03_altyazi_acik");
        us.SetSubtitles(false);
        _d.Hud.Say("Altyazı kapalıyken görünmemeli.");
        await Seconds(0.8f);
        Check(!_d.Hud.SubtitleBoxVisible, "altyazi kapaliyken Suna kutusu gorunmemeli");
        us.SetSubtitles(true);
        us.SetShake(false);
        _d.Player.Shake(5f);
        us.SetShake(true);

        // ---- 4) tek panel dili ----
        Check(Square(_d.Hud.SubtitleBox), "Suna kutusu yuvarlak kose");
        foreach (var n in new[] { "FeelPanel", "ControlsPanel" })
        {
            var node = _d.GetNodeOrNull(n) ?? _d.GetNodeOrNull<Node>($"/root/Main/{n}");
            if (node is CanvasLayer cl)
            {
                foreach (var pc in cl.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()) GD.Print($"ARAYUZ dbg {n} panel {pc.Name} r={(pc.GetThemeStylebox("panel") as StyleBoxFlat)?.CornerRadiusTopLeft}");
                Check(Square(cl.FindChildren("*", "PanelContainer", true, false).OfType<Control>().FirstOrDefault() ?? new Control()), $"{n}: yuvarlak kose");
            }
        }
        var audio = GetTree().Root.FindChildren("AudioPanel", "CanvasLayer", true, false).FirstOrDefault();
        if (audio != null) Check(Square(audio.FindChildren("*", "PanelContainer", true, false).OfType<Control>().First()), "AudioPanel: yuvarlak kose");
        if (_window)
        {
            if (audio != null) { audio.GetChild<Control>(0).Visible = true; await Shot("04_panel_ses"); audio.GetChild<Control>(0).Visible = false; }
            _d.FeelPanel?.GetType();
        }

        // ---- 4b) ogretici (M6 ogretici) ----
        Events.RaiseStationPlaced("bant_istasyonu");           // otomasyon tetikleyicisi (kimlikte "bant")
        _d.TutorialFire("biome", "sis_korusu");                // ikinci satir: bekleme suresine takilir
        await Seconds(1.5f);
        Check(_d.TutorialSeen.Contains("istasyon_ilk") && _d.TutorialSeen.Contains("otomasyon_ilk"), "istasyon ve otomasyon ogreticisi tetiklenmedi");
        Check(_d.Hud.SubtitleBoxVisible, "ogretici satiri Suna kutusunda gorunmeli");
        int pend = _d.TutorialPending;
        Events.RaiseStationPlaced("bant_istasyonu");
        _d.TutorialFire("biome", "sis_korusu");
        Check(_d.TutorialPending == pend, "ogretici ikinci kez siraya girmemeli");
        Check(_d.Data.Suna.Text("tut_otomasyon_ilk", "en") != _d.Data.Suna.Text("tut_otomasyon_ilk", "tr"), "EN metni TR'den farkli olmali");

        // ---- 5) fotograf modu ----
        var ph = _d.Photo;
        Check(!ph.Active, "fotograf modu kapali baslamali");
        ph.Enter();
        await Seconds(0.3f);
        Check(ph.Active && ph.Camera != null && ph.Camera.Current, "fotograf kamerasi aktif degil");
        GD.Print($"ARAYUZ dbg uicapture={_d.Player.UiCapture} hudVisible={_d.Hud.Visible}");
        Check(_d.Player.UiCapture && !_d.Hud.Visible, "oyuncu girdisi kapali, oyun arayuzu gizli olmali");
        var p0 = ph.Camera.GlobalPosition;
        ph.MoveCamera(p0 + new Vector3(500, 0, 0));
        Check(ph.Camera.GlobalPosition.DistanceTo(_d.Player.GlobalPosition) <= UiTheme.Data.Access.PhotoRange + 0.01f, "kamera menzil siniri");
        ph.MoveCamera(_d.Player.GlobalPosition + new Vector3(0.7f, 1.4f, 3.2f));
        ph.Camera.LookAt(_d.Player.GlobalPosition + Vector3.Up * 1.1f);
        string pose0 = ph.PoseId;
        ph.CyclePose(1);
        await Seconds(0.3f);
        Check(ph.PoseId != pose0 && _d.Player.CurrentClip == ph.PoseId, $"poz degismedi ({pose0}->{ph.PoseId}, klip {_d.Player.CurrentClip})");
        string face0 = ph.FaceStyle;
        ph.CycleFace(1);
        Check(ph.FaceStyle != face0, "vizor yuzu degismedi");
        ph.CycleExpr(1);
        await Seconds(0.3f);
        Check(ph.Expression.Length > 0 && _d.Player.Visor?.Expression == ph.Expression, "ifade secimi vizore gitmedi");
        if (_window)
        {
            _d.Hud.Visible = true; await Shot("05_foto_modu_panel_ve_arayuz"); _d.Hud.Visible = false;
            ph.CyclePose(1); ph.CycleFace(2); await Shot("05_foto_modu_panel");
        }
        ph.Shoot();
        await Seconds(0.8f);
        if (_window) { Check(ph.LastPhoto.Length > 0 && FileAccess.FileExists(ph.LastPhoto), "fotograf dosyasi yazilmadi"); }
        Check(!_d.Hud.Visible, "cekimden sonra arayuz eski durumunda (gizli)");
        ph.Exit();
        await Seconds(0.3f);
        Check(!ph.Active && _d.Hud.Visible && !_d.Player.UiCapture && _d.Player.Cam.Current, "cikista arayuz, girdi ve oyuncu kamerasi geri gelmedi");
        if (_window) await Shot("06_foto_modundan_cikis");
    }
}
