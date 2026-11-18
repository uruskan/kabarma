using System.Globalization;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuncu kamerasi (camera.json; PLAN §4 Kamera, URETIM §6 karar 21). Birinci sahis yok; iki kip, V (kolda R3) ile:
/// normal (arkada ve ustte; fare tekerlegi yakinlastirir) ve yakin omuz (Astroneer tarzi; kamera omzun hemen
/// arkasinda ve yaninda). Karakter hic gizlenmez.
/// Agac: Yaw (ust duzey, oyuncunun konumunda) -> Pitch (pivot, Height) -> Arm (saga Side) -> Cam (geriye uzaklik).
/// Carpisma: pivottan omuz noktasina, oradan geriye bir kure suruklenir; kamera ilk duran engelin onunde durur
/// (engele girerken hemen, engel kalkinca ReturnRate ile geri acilir). Yaratik, araba gibi hareketli govdeler kamerayi
/// itmez. Nisan, Capa isinlari ve insa onizlemesi ekranin ortasindan (AimPoint) gelir; iki kipte de ayni yol.
/// Secilen kip ve normal kipin uzakligi user://camera_settings.cfg'de; sinama kosularinda okunmaz, yazilmaz.
/// Hata ayiklama: --cam=normal|shoulder, --camzoom=M (kayitli ayari ezer, kaydetmez).
/// </summary>
public partial class Player
{
    public enum CamMode { Normal, Shoulder }

    const string CameraSettingsPath = "user://camera_settings.cfg";
    CameraFile _camDef = new();
    float _camT;                 // 0 normal .. 1 yakin omuz (dogrusal; CameraFile.Ease ile egrilir)
    float _zoom, _zoomTarget;    // normal kipin uzakligi (yumusatilmis, hedef)
    float _boom = -1f;           // carpismayla sinirli gercek uzaklik
    float _lastDist = -1f;       // onceki karede istenen uzaklik (engel yokken)
    SphereShape3D _camBall;
    PhysicsShapeQueryParameters3D _camQuery;
    readonly Godot.Collections.Array<Rid> _camExclude = new();

    public CamMode CameraMode { get; private set; }
    /// <summary>Normal kipin hedef uzakligi (tekerlek bunu degistirir).</summary>
    public float CameraZoom => _zoomTarget;
    /// <summary>Kameranin omuz noktasindan su anki uzakligi (carpisma sonrasi).</summary>
    public float CameraBoom => _boom;
    /// <summary>Kameranin pivottan saga su anki ofseti (carpisma sonrasi).</summary>
    public float CameraSide => Arm?.Position.X ?? 0f;
    public CameraFile CameraDef => _camDef;

    /// <summary>Sinama, goruntu ve bot kosularinda sahibin ayar dosyasi okunmaz ve yazilmaz.</summary>
    static bool CameraTesting => Main.Args.ContainsKey("test") || Main.Args.ContainsKey("bot") || Main.Args.ContainsKey("shots")
                                 || Main.Args.ContainsKey("perf") || Main.Args.ContainsKey("netsync") || Main.Args.ContainsKey("swarm");

    /// <summary>Kamera agaci (_Ready).</summary>
    void BuildCamera()
    {
        _camDef = (GetParent() as Director)?.Data?.Camera ?? new CameraFile();
        _zoom = _zoomTarget = _camDef.ClampZoom(_camDef.Normal.Distance);
        if (!CameraTesting) LoadCameraSettings(CameraSettingsPath);
        if (Main.Args.TryGetValue("cam", out var cm)) CameraMode = cm == "shoulder" ? CamMode.Shoulder : CamMode.Normal;
        if (Main.Args.TryGetValue("camzoom", out var cz) && float.TryParse(cz, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            _zoom = _zoomTarget = _camDef.ClampZoom(z);
        _camT = CameraMode == CamMode.Shoulder ? 1f : 0f;

        Yaw = new Node3D { Name = "CamYaw", TopLevel = true };
        AddChild(Yaw);
        Pitch = new Node3D { Name = "CamPitch" };
        Yaw.AddChild(Pitch);
        Arm = new Node3D { Name = "CamArm" };
        Pitch.AddChild(Arm);
        Cam = new Camera3D { Current = true };
        Cam.AddToGroup("genex_shot");
        Arm.AddChild(Cam);
        Pitch.Rotation = new Vector3(Mathf.DegToRad(_camDef.StartPitchDeg), 0, 0);
        _camBall = new SphereShape3D { Radius = _camDef.CollisionRadius };
        _camQuery = new PhysicsShapeQueryParameters3D { Shape = _camBall, CollisionMask = 1u };
        PlaceCamera(0f, false);
    }

    /// <summary>
    /// Director.Input.cs'ten, menu kapaliyken her kare. wheelBusy: tekerlek baska bir ise yariyor (Insa'da parca
    /// degistirir, cark acik); o zaman yakinlastirmaz.
    /// </summary>
    public void CameraInput(bool wheelBusy)
    {
        if (Input.IsActionJustPressed("camera_mode")) ToggleCameraMode();
        if (CameraMode != CamMode.Normal || wheelBusy) return;
        int n = (Input.IsActionJustPressed("camera_zoom_out") ? 1 : 0) - (Input.IsActionJustPressed("camera_zoom_in") ? 1 : 0);
        if (n != 0) ZoomCamera(n);
    }

    public void ToggleCameraMode() => SetCameraMode(CameraMode == CamMode.Normal ? CamMode.Shoulder : CamMode.Normal);

    public void SetCameraMode(CamMode m)
    {
        if (m == CameraMode) return;
        CameraMode = m;
        SaveCameraSettings();
    }

    /// <summary>Normal kipte tekerlek: notches > 0 uzaklastirir, < 0 yakinlastirir. Yakin omuzda yok sayilir.</summary>
    public void ZoomCamera(int notches)
    {
        if (CameraMode != CamMode.Normal) return;
        _zoomTarget = _camDef.StepZoom(_zoomTarget, notches);
        SaveCameraSettings();
    }

    /// <summary>Normal kipin uzakligini dogrudan koyar (sinama, goruntu); yumusatma yok.</summary>
    public void SetCameraZoom(float distance)
    {
        _zoom = _zoomTarget = _camDef.ClampZoom(distance);
        SaveCameraSettings();
    }

    void SaveCameraSettings()
    {
        if (!CameraTesting) SaveCameraSettings(CameraSettingsPath);
    }

    /// <summary>Kip ve normal kipin uzakligi; sinama kendi dosya yolunu verir.</summary>
    public Error SaveCameraSettings(string path)
    {
        var cfg = new ConfigFile();
        cfg.SetValue("camera", "mode", CameraMode == CamMode.Shoulder ? "shoulder" : "normal");
        cfg.SetValue("camera", "zoom", _zoomTarget);
        return cfg.Save(path);
    }

    public bool LoadCameraSettings(string path)
    {
        var cfg = new ConfigFile();
        if (cfg.Load(path) != Error.Ok) return false;
        CameraMode = cfg.GetValue("camera", "mode", "normal").AsString() == "shoulder" ? CamMode.Shoulder : CamMode.Normal;
        _zoom = _zoomTarget = _camDef.ClampZoom((float)cfg.GetValue("camera", "zoom", _zoomTarget).AsDouble());
        return true;
    }

    /// <summary>Fizik karesinin sonunda: kip gecisi, yakinlastirma, carpisma.</summary>
    void UpdateCamera(float dt)
    {
        float goal = CameraMode == CamMode.Shoulder ? 1f : 0f;
        _camT = Mathf.MoveToward(_camT, goal, dt / Mathf.Max(0.01f, _camDef.TransitionSeconds));
        _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-_camDef.ZoomSmooth * dt));
        PlaceCamera(dt, true);
    }

    void PlaceCamera(float dt, bool collide)
    {
        float s = CameraFile.Ease(_camT);
        CameraPoseDef n = _camDef.Normal, sh = _camDef.Shoulder;
        float side = Mathf.Lerp(n.Side, sh.Side, s);
        float dist = Mathf.Lerp(_zoom, sh.Distance, s);
        Pitch.Position = new Vector3(0, Mathf.Lerp(n.Height, sh.Height, s), 0);
        Cam.Fov = Mathf.Lerp(n.Fov, sh.Fov, s);
        float sideFree = 1f, backFree = 1f;
        if (collide && IsInsideTree())
        {
            var pivot = Pitch.GlobalPosition;
            var b = Pitch.GlobalBasis.Orthonormalized();
            sideFree = Sweep(pivot, b.X * side);
            backFree = Sweep(pivot + b.X * (side * sideFree), b.Z * dist);
        }
        float want = dist * backFree;
        // Engele girerken hemen; engel kalkinca yumusak acilir. Engel yokken (kip gecisi, yakinlastirma) dogrudan izler.
        bool wasBlocked = _boom >= 0f && _lastDist >= 0f && _boom < _lastDist - 0.02f;
        if (_boom < 0f || want <= _boom || !wasBlocked) _boom = want;
        else _boom = Mathf.Lerp(_boom, want, 1f - Mathf.Exp(-_camDef.ReturnRate * dt));
        _lastDist = dist;
        Arm.Position = new Vector3(side * sideFree, 0, 0);
        Cam.Position = new Vector3(0, 0, _boom);
    }

    /// <summary>
    /// Kureyi from'dan motion kadar surukler; carpmadan gidebildigi oran (0..1). Engel yalniz duran dunyadir (arazi,
    /// kaya, yapi, istasyon); yaratik, araba, firlatilan nesne (CharacterBody3D, RigidBody3D) yok sayilir.
    /// </summary>
    float Sweep(Vector3 from, Vector3 motion)
    {
        if (motion.LengthSquared() < 1e-6f) return 1f;
        var space = GetWorld3D().DirectSpaceState;
        _camBall.Radius = _camDef.CollisionRadius;
        _camExclude.Clear();
        _camExclude.Add(GetRid());
        _camQuery.Motion = motion;
        for (int i = 0; i < 4; i++)
        {
            _camQuery.Transform = new Transform3D(Basis.Identity, from);
            _camQuery.Exclude = _camExclude;
            var r = space.CastMotion(_camQuery);
            if (r.Length < 2 || r[0] >= 1f) return 1f;
            _camQuery.Transform = new Transform3D(Basis.Identity, from + motion * r[1]);
            var info = space.GetRestInfo(_camQuery);
            if (info.Count == 0 || !info.TryGetValue("collider_id", out var idv)) return r[0];
            if (GodotObject.InstanceFromId(idv.AsUInt64()) is not (CharacterBody3D or RigidBody3D or PhysicalBone3D) || !info.TryGetValue("rid", out var rid))
                return r[0];
            _camExclude.Add(rid.AsRid());
        }
        return 1f;
    }
}
