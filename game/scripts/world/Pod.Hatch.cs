using Godot;

namespace Kabarma.Game;

/// <summary>
/// Acilis ani (PLAN 1.4, 0:00): "Inis kapsulunun kapagi altin sarisi bir cayira aciliyor." Kamera kapsulun icinden
/// kapaga bakar, kapak acilir, Suna giris repliklerini soyler, kamera disari suzulup oyuncunun omzuna oturur, kapak
/// arkadan kapanir (safakta rampa olurken Pod.cs yeniden acar). Sureler scenario.json "hatch".
/// </summary>
public partial class Pod
{
    Camera3D _introCam;

    /// <summary>Acilis bitti (oyuncu serbest). Acilis hic baslamadiysa da true.</summary>
    public bool IntroDone { get; private set; } = true;

    public void BeginHatchIntro(Director d)
    {
        var h = d.Data.Scenario.Hatch;
        var p = d.Player;
        IntroDone = false;
        // Oyuncu kapagin onunde, kapsulden cayira (ve krater sutununa) bakar.
        var outward = GlobalBasis.Z with { Y = 0 };
        outward = outward.Normalized();
        var stand = d.World.Ground(GlobalPosition + outward * 3.8f) + Vector3.Up * 0.2f;
        p.GlobalPosition = stand;
        p.RespawnPoint = stand;
        p.Yaw.Rotation = new Vector3(0, Mathf.Atan2(-outward.X, -outward.Z), 0);
        p.MoveOverride = Vector3.Zero;
        p.Visible = false;

        _hatch.Rotation = Vector3.Zero;
        _introCam = new Camera3D { Name = "HatchCam", Fov = 64f };
        AddChild(_introCam);
        _introCam.Position = new Vector3(0, 1.35f, 0.15f);
        _introCam.LookAt(GlobalTransform * new Vector3(0, 1.2f, 12f));
        _introCam.MakeCurrent();

        var tw = CreateTween();
        tw.TweenInterval(h.Delay);
        tw.TweenCallback(Callable.From(() => Sfx.Play("place", GlobalPosition + Vector3.Up, 0f)));
        tw.TweenProperty(_hatch, "rotation:x", Mathf.DegToRad(h.OpenDegrees), h.OpenSeconds).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenCallback(Callable.From(() =>
        {
            Fx.Burst(GlobalTransform * new Vector3(0, 0.4f, 2.6f), Palette.Ground, 22, 3f, 0.1f, 0.9f);
            d.SayIntro();
        }));
        tw.TweenInterval(h.HoldSeconds);
        tw.TweenCallback(Callable.From(() => p.Visible = true));
        Transform3D from = default;
        tw.TweenCallback(Callable.From(() => from = _introCam.GlobalTransform));
        tw.TweenMethod(Callable.From<float>(k =>
        {
            float s = Mathf.SmoothStep(0f, 1f, k);
            _introCam.GlobalTransform = from.InterpolateWith(p.Cam.GlobalTransform, s);
        }), 0f, 1f, h.ExitSeconds);
        tw.TweenCallback(Callable.From(() =>
        {
            if (_introCam.Current) p.Cam.MakeCurrent();
            _introCam.QueueFree();
            if (p.MoveOverride == Vector3.Zero) p.MoveOverride = null;
            IntroDone = true;
            GD.Print("HATCH intro done");
        }));
        tw.TweenProperty(_hatch, "rotation:x", 0f, h.CloseSeconds).SetTrans(Tween.TransitionType.Sine);
    }
}
