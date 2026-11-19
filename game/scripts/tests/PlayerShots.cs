using System;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuncu modeli goruntuleri (--test=playershots; oyuncu2 akisi): duz bir zeminde, karakterin yanina konan kamerayla
/// bekleme, yurume, kosu, depar, ziplama, Capa kullanimi (Kazma ve Bicme), kemerde Capa yokken sirttaki Capa ve canta
/// arkadan. progress/oyuncu2/ altina PNG yazar. Pencereli kosulur (--resolution 960x720); bot ya da kayit gerekmez.
/// </summary>
public partial class PlayerShots : Node
{
    static readonly Vector3 Site = new(-400f, 220f, 400f);
    Director _d;
    Player P => _d.Player;
    Camera3D _cam;
    Vector3 _off = new(0f, 1.0f, 3.2f);   // oyuncunun govde uzayinda: +Z onde
    float _lookY = 0.85f;
    string _dir;
    int _n;

    public override void _Ready() => CallDeferred(nameof(Begin));

    async void Begin()
    {
        try { await Run(); }
        catch (Exception e) { GD.Print("PLAYERSHOTS FAIL " + e); }
        GD.Print($"PLAYERSHOTS RESULT shots={_n}");
        GetTree().Quit(_n >= 10 ? 0 : 1);
    }

    async Task Physics(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    Task Seconds(float s) => Physics(Mathf.CeilToInt(s * Engine.PhysicsTicksPerSecond));

    public override void _Process(double delta)
    {
        if (_cam == null || _d?.Player == null) return;
        var vis = P.Visual.GlobalTransform;
        var basis = vis.Basis.Orthonormalized();
        var pos = P.GlobalPosition + basis * _off;
        _cam.GlobalPosition = pos;
        _cam.LookAt(P.GlobalPosition + Vector3.Up * _lookY, Vector3.Up);
        if (!_cam.Current) _cam.MakeCurrent();
    }

    async Task Shot(string name, float wait = 0.15f)
    {
        await Seconds(wait);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng($"{_dir}/{name}.png");
        _n++;
        GD.Print($"PLAYERSHOTS {name}");
    }

    async Task Run()
    {
        while ((_d = GetParent().GetNodeOrNull<Director>("Director")) == null || _d.Player == null || _d.Capa == null) await Physics(1);
        while (!(_d.Pod?.IntroDone ?? true)) await Physics(1);
        await Physics(10);
        _dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/oyuncu2";
        DirAccess.MakeDirRecursiveAbsolute(_dir);

        var floor = new StaticBody3D { Name = "ShotFloor" };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(120, 1, 120) } });
        AddChild(floor);
        floor.GlobalPosition = Site + Vector3.Down * 0.5f;
        var sun = new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0), LightEnergy = 1.2f, ShadowEnabled = true };
        AddChild(sun);
        _cam = new Camera3D { Fov = 40f, Far = 200f };
        AddChild(_cam);
        P.GlobalPosition = Site + Vector3.Up * 0.05f;
        P.Velocity = Vector3.Zero;
        P.Yaw.Rotation = Vector3.Zero;
        P.Pitch.Rotation = Vector3.Zero;
        _d.Capa.Select(Capa.Head.Kazma);
        await Seconds(1.2f);

        var fwd = (P.Visual.GlobalBasis.Z with { Y = 0 }).Normalized();
        // onden
        await Shot("01_bekleme_onden");
        // yandan (kamera karakterin sagina)
        _off = new Vector3(-3.2f, 1.0f, 0f);
        await Shot("02_bekleme_yandan");
        // yurume (yandan), kosu, depar
        P.MoveOverride = fwd;
        await Seconds(0.9f); await Shot("03_yurume_yandan", 0f);
        P.MoveOverride = fwd * 1.0f;
        await Seconds(0.25f); await Shot("03b_yurume_yandan_2", 0f);
        P.MoveOverride = fwd * 1.5f;
        await Seconds(1.0f); await Shot("04_depar_yandan", 0f);
        P.MoveOverride = null;
        P.Velocity = Vector3.Zero;
        P.GlobalPosition = Site + Vector3.Up * 0.05f;
        await Seconds(0.6f);
        // yurume onden
        _off = new Vector3(0.6f, 1.0f, 3.2f);
        P.MoveOverride = fwd;
        await Seconds(0.7f); await Shot("05_yurume_onden", 0f);
        P.MoveOverride = null;
        P.GlobalPosition = Site + Vector3.Up * 0.05f; P.Velocity = Vector3.Zero;
        await Seconds(0.6f);
        // ziplama (yandan)
        _off = new Vector3(-3.4f, 1.1f, 0f);
        var tok = _d.Controls.Bindings("jump", InputDevice.Kbm)[0];
        Input.ParseInputEvent(Controls.DeviceEvent(tok, InputDevice.Kbm, true));
        await Seconds(0.1f);
        Input.ParseInputEvent(Controls.DeviceEvent(tok, InputDevice.Kbm, false));
        await Seconds(0.25f); await Shot("06_ziplama_kalkis", 0f);
        await Seconds(0.25f); await Shot("07_ziplama_havada", 0f);
        await Seconds(1.2f);
        // Capa kullanimi: Kazma, Bicme (onden-yandan), gercek tetik
        _off = new Vector3(-2.2f, 1.2f, 2.2f);
        foreach (var head in new[] { Capa.Head.Kazma, Capa.Head.Bicme })
        {
            _d.Capa.Select(head);
            P.Yaw.GlobalRotation = new Vector3(0, P.Visual.GlobalRotation.Y + Mathf.Pi, 0);
            await Physics(3);
            var ft = _d.Controls.Bindings("fire", InputDevice.Kbm)[0];
            Input.ParseInputEvent(Controls.DeviceEvent(ft, InputDevice.Kbm, true));
            await Seconds(0.55f); await Shot($"08_capa_{head}".ToLowerInvariant(), 0f);
            Input.ParseInputEvent(Controls.DeviceEvent(ft, InputDevice.Kbm, false));
            await Seconds(0.5f);
        }
        // Capa elde: arkadan (canta, kask arkasi)
        _d.Capa.Select(Capa.Head.Kazma);
        P.AimOverride = null;
        _off = new Vector3(0.4f, 1.1f, -3.0f);
        await Shot("09_canta_arkadan_capa_elde", 0.4f);
        // Capa sirtta (kemerde bos yuva)
        _d.State.Belt.Select(_d.Data.Tool.BeltStart.Count + 2);
        await Seconds(0.8f);
        await Shot("10_canta_arkadan_capa_sirtta");
        _off = new Vector3(-2.6f, 1.1f, -1.6f);
        await Shot("11_canta_yan_arka_capa_sirtta");
        _off = new Vector3(-3.2f, 1.0f, 0f);
        await Shot("12_bekleme_yandan_capa_sirtta");
    }
}
