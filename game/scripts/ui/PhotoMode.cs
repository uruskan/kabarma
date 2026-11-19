using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Fotograf modu (SANAT-SES §8.2; M6 arayuz): P ya da Esc menusundeki "Fotoğraf modu". Serbest kamera (WASD, Q/E asagi-yukari,
/// Shift hizli, fare bakis, tekerlek gorus acisi), arayuzu gizleme (U oyun arayuzu, H bu panel), poz secimi (← →: bekleme, el sallama,
/// zafer, comelme, uyku, yeme, kosu, ziplama), vizor yuzu (↑ ↓: 12 kozmetik yuz) ve ifade ([ ]: nötr, yorgun, mutlu...), Enter ile
/// cekim: PNG user://photos/ altina yazilir (panel ve arayuz cekimde gizlenir). Esc / P cikar ve her sey geri gelir. Dunya akmaya
/// devam eder (animasyon gorunsun); oyuncu yerinde durur.
/// </summary>
public partial class PhotoMode : CanvasLayer
{
    readonly Director _d;
    Camera3D _cam;
    PanelContainer _panel;
    Label _info, _toast;
    float _yaw, _pitch, _toastT;
    int _pose, _face, _expr;
    bool _hudHidden = true, _panelHidden, _shooting;
    List<string> _faces = new(), _exprs = new();
    Input.MouseModeEnum _mouseWas;

    public PhotoMode(Director d) { _d = d; Name = "PhotoMode"; Layer = 80; ProcessMode = ProcessModeEnum.Always; Visible = false; }

    public bool Active { get; private set; }
    public Camera3D Camera => _cam;
    public string LastPhoto { get; private set; } = "";
    public bool HudHidden => _hudHidden;
    public string PoseId => Poses.Count == 0 ? "" : Poses[_pose % Poses.Count];
    public string FaceStyle => _faces.Count == 0 ? "dot" : _faces[_face % _faces.Count];
    public string Expression => _expr == 0 ? "" : _exprs[(_expr - 1) % _exprs.Count];
    List<string> Poses => UiTheme.Data.Access.PhotoPoses;

    public override void _Ready()
    {
        _faces = _d.Data.Cosmetics.Cosmetics.Where(c => c.Slot == "vizor").Select(c => c.Look.GetValueOrDefault("style", "dot")).ToList();
        _exprs = _d.Data.Visor.Expressions.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        _panel = UiPanels.Skin(new PanelContainer { Position = new Vector2(T.Px(24), T.Px(24)) });
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(T.Px(440), 0) };
        v.AddChild(UiPanels.Title("Fotoğraf modu", 30));
        _info = UiPanels.Text("", 18, T.Ink);
        v.AddChild(_info);
        v.AddChild(UiPanels.Text("WASD · Q/E: kamera  ·  Shift: hızlı  ·  fare: bakış  ·  tekerlek: görüş açısı\n← →: poz  ·  ↑ ↓: vizör yüzü  ·  [ ]: ifade\nU: oyun arayüzü  ·  H: bu panel  ·  Enter: çek  ·  Esc: çık", 15, T.Muted));
        _panel.AddChild(v);
        AddChild(_panel);
        _toast = UiPanels.Title("", 34, T.Warning);
        _toast.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _toast.Position = new Vector2(T.Px(700), T.Px(780));
        AddChild(_toast);
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (Active)
        {
            switch (k.Keycode)
            {
                case Key.Escape: case Key.P: Exit(); break;
                case Key.Enter: case Key.KpEnter: Shoot(); break;
                case Key.Left: CyclePose(-1); break;
                case Key.Right: CyclePose(1); break;
                case Key.Up: CycleFace(1); break;
                case Key.Down: CycleFace(-1); break;
                case Key.Bracketleft: CycleExpr(-1); break;
                case Key.Bracketright: CycleExpr(1); break;
                case Key.H: _panelHidden = !_panelHidden; Refresh(); break;
                case Key.U: SetHud(_hudHidden); break;
                default: return;
            }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (k.Keycode != Key.P) return;
        var h = _d.Hud;
        if (h == null || h.MenuOpen || h.WheelOpen || h.ReportOpen || h.CraftTyping || _d.Player == null || _d.Player.Down) return;
        Enter();
        GetViewport().SetInputAsHandled();
    }

    public override void _Input(InputEvent e)
    {
        if (!Active) return;
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            float s = UiTheme.Data.Access.PhotoLook;
            _yaw -= mm.Relative.X * s;
            _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * s, -1.5f, 1.5f);
        }
        else if (e is InputEventMouseButton { Pressed: true } mb)
        {
            var a = UiTheme.Data.Access;
            if (mb.ButtonIndex == MouseButton.WheelUp) _cam.Fov = Mathf.Clamp(_cam.Fov - a.PhotoFovStep, a.PhotoFovMin, a.PhotoFovMax);
            else if (mb.ButtonIndex == MouseButton.WheelDown) _cam.Fov = Mathf.Clamp(_cam.Fov + a.PhotoFovStep, a.PhotoFovMin, a.PhotoFovMax);
            Refresh();
        }
    }

    public override void _Process(double delta)
    {
        if (_toastT > 0) { _toastT -= (float)delta; if (_toastT <= 0) _toast.Text = ""; }
        if (!Active || _cam == null) return;
        _d.Player.UiCapture = true; // Director her karede panel durumundan yazar; fotograf modunda oyuncu yerinde kalir
        var a = UiTheme.Data.Access;
        var b = new Basis(Vector3.Up, _yaw) * new Basis(Vector3.Right, _pitch);
        _cam.GlobalBasis = b;
        var dir = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) dir += -b.Z;
        if (Input.IsKeyPressed(Key.S)) dir += b.Z;
        if (Input.IsKeyPressed(Key.A)) dir += -b.X;
        if (Input.IsKeyPressed(Key.D)) dir += b.X;
        if (Input.IsKeyPressed(Key.E)) dir += Vector3.Up;
        if (Input.IsKeyPressed(Key.Q)) dir += Vector3.Down;
        float speed = a.PhotoSpeed * (Input.IsKeyPressed(Key.Shift) ? a.PhotoFastMult : 1f);
        if (dir != Vector3.Zero) MoveCamera(_cam.GlobalPosition + dir.Normalized() * speed * (float)delta);
    }

    /// <summary>Serbest kamerayi oyuncudan PhotoRange'den uzaga gotürmez.</summary>
    public void MoveCamera(Vector3 p)
    {
        var c = _d.Player.GlobalPosition;
        var off = p - c;
        float max = UiTheme.Data.Access.PhotoRange;
        if (off.Length() > max) p = c + off.Normalized() * max;
        _cam.GlobalPosition = p;
    }

    // ---------------- giris, cikis ----------------

    public void Enter()
    {
        if (Active || _d.Player == null) return;
        Active = true;
        var a = UiTheme.Data.Access;
        var from = _d.Player.Cam?.GlobalTransform ?? _d.Player.GlobalTransform;
        _cam = new Camera3D { Fov = a.PhotoFovDefault, Far = _d.Player.Cam?.Far ?? 800f };
        _d.AddChild(_cam);
        _cam.GlobalTransform = from;
        var e = from.Basis.GetEuler();
        _yaw = e.Y; _pitch = e.X;
        _cam.MakeCurrent();
        _d.Player.UiCapture = true;
        _mouseWas = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Visible = true;
        _hudHidden = true; _panelHidden = false;
        SetHud(false);
        _pose = 0; _face = Math.Max(0, _faces.IndexOf(CurrentEquippedFace())); _expr = 0;
        ApplyPose();
        Refresh();
    }

    string CurrentEquippedFace() => _d.Data.Cosmetics.Get(_d.State.Wardrobe.Equipped("vizor"))?.Look.GetValueOrDefault("style", "dot") ?? "dot";

    public void Exit()
    {
        if (!Active) return;
        Active = false;
        Visible = false;
        _cam?.QueueFree();
        _cam = null;
        _d.Player.Cam?.MakeCurrent();
        _d.Player.UiCapture = false;
        _d.Player.Visor?.Hold(null);
        _d.ApplyLook(); // takili vizor yuzu geri
        Input.MouseMode = _mouseWas == Input.MouseModeEnum.Visible ? Input.MouseModeEnum.Captured : _mouseWas;
        SetHud(true);
    }

    void SetHud(bool visible)
    {
        _hudHidden = !visible;
        if (_d.Hud != null) _d.Hud.Visible = visible;
    }

    // ---------------- poz, yuz, ifade ----------------

    public void CyclePose(int d) { if (Poses.Count == 0) return; _pose = (_pose + d + Poses.Count) % Poses.Count; ApplyPose(); Refresh(); }
    public void CycleFace(int d) { if (_faces.Count == 0) return; _face = (_face + d + _faces.Count) % _faces.Count; _d.Player.Visor?.SetStyle(FaceStyle); Refresh(); }
    public void CycleExpr(int d) { _expr = (_expr + d + _exprs.Count + 1) % (_exprs.Count + 1); _d.Player.Visor?.Hold(_expr == 0 ? null : Expression); Refresh(); }

    void ApplyPose()
    {
        string id = PoseId;
        if (id.Length == 0) return;
        _d.Player.PlayPose(id, 600f, lockMove: true);
    }

    void Refresh()
    {
        if (_panel == null) return;
        _panel.Visible = !_panelHidden && !_shooting;
        _info.Text = $"Poz: {PoseId}  ·  Vizör yüzü: {FaceStyle}  ·  İfade: {(Expression.Length == 0 ? "otomatik" : Expression)}  ·  Görüş: {_cam?.Fov:0}°" +
                     $"\nOyun arayüzü: {(_hudHidden ? "gizli" : "açık")}";
    }

    // ---------------- cekim ----------------

    public async void Shoot()
    {
        if (!Active || _shooting) return;
        _shooting = true;
        bool hud = !_hudHidden;
        Refresh();
        _toast.Text = "";
        if (_d.Hud != null) _d.Hud.Visible = false;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        string dir = $"user://{UiTheme.Data.Access.PhotoFolder}";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        string path = $"{dir}/foto_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        img.SavePng(path);
        LastPhoto = ProjectSettings.GlobalizePath(path);
        GD.Print($"PHOTO {LastPhoto} {img.GetWidth()}x{img.GetHeight()}");
        if (_d.Hud != null) _d.Hud.Visible = hud;
        _shooting = false;
        Refresh();
        _toast.Text = T.Upper("Fotoğraf kaydedildi");
        _toastT = 2f;
    }
}
