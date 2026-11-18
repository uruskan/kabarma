using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kamera sinamasi (--test=camera; oyuncu akisi; PLAN §4 Kamera, URETIM §6 karar 21). Oyun (Director) kurulur, kapsul
/// acilisi beklenir, oyuncu dunyanin cok ustunde sinama icin kurulan duz bir zemine konur (cevre belirlenimci olsun).
///  1. Normal kip: kamera omuz noktasindan verideki uzaklikta, pivotun saginda.
///  2. Tekerlek, gercek girdi yolundan (input.json baglamasi -> Director.ReadInput -> Player.CameraInput): uzaklastirir,
///     yakinlastirir, sinirda durur; Insa basligindayken yakinlastirmaz (tekerlek orada parcayi degistirir).
///  3. V (camera_mode) yakin omuza gecer; gecis sonunda kamera omuz uzakliginda; bu kipte tekerlek bir sey yapmaz.
///     Kolun R3'u normal kipe geri alir.
///  4. Nisan, iki kipte, AimOverride olmadan (kameranin ortasindan): Kazma ve Civi ile tetik basili; ekranin ortasi hedefi
///     vurur, namludan nisan noktasina isin hedefe takilir, namlu nisana bakar. Kamera ile oyuncu arasindaki bir
///     yaratik govdesi nisani arkaya cekmez.
///  5. Carpisma, iki kipte: arkadaki duvar kamerayi one ceker, duvar kalkinca kamera yumusakca geri acilir; yandaki
///     duvar omuz ofsetini kisaltir; arkadaki yaratik govdesi (CharacterBody3D) kamerayi itmez.
///  6. Ayar dosyasi: kip ve uzaklik ayri yuvada (user://test/camera_settings.cfg) kaydedilip geri okunur; sahibin
///     user://camera_settings.cfg dosyasina dokunulmaz.
/// Sonuc: "RESULT ok ..." ya da "RESULT fail ...", cikis kodu 0/1.
/// </summary>
public partial class CameraTest : Node
{
    const double TimeoutSeconds = 150;
    const string OwnSettings = "user://camera_settings.cfg";
    const string TestSettings = "user://test/camera_settings.cfg";
    static readonly Vector3 Site = new(400f, 220f, 400f); // dunyanin cok ustu: arazi, kaya, yaratik yok
    Director _d;
    Player P => _d.Player;
    CameraFile C => P.CameraDef;
    readonly List<string> _fails = new();
    ulong _start;
    bool _done;
    int _checks;

    public override void _Ready()
    {
        _start = Time.GetTicksUsec();
        CallDeferred(nameof(Begin));
    }

    async void Begin()
    {
        try { await Run(); }
        catch (Exception e) { _fails.Add("istisna: " + e.Message); GD.Print(e.ToString()); }
        Finish();
    }

    public override void _Process(double delta)
    {
        if (!_done && (Time.GetTicksUsec() - _start) / 1e6 > TimeoutSeconds) { _fails.Add($"zaman asimi {TimeoutSeconds} sn"); Finish(); }
    }

    void Finish()
    {
        if (_done) return;
        _done = true;
        GD.Print(_fails.Count == 0 ? $"RESULT ok camera checks={_checks}" : $"RESULT fail camera ({_fails.Count}/{_checks}) {string.Join("; ", _fails)}");
        GetTree().Quit(_fails.Count == 0 ? 0 : 1);
    }

    void Check(bool ok, string what)
    {
        _checks++;
        if (!ok) { _fails.Add(what); GD.Print("CAMERA FAIL " + what); }
    }

    void Near(float got, float want, float tol, string what) => Check(Mathf.Abs(got - want) <= tol, $"{what}: {got:0.000} (beklenen {want:0.000} +-{tol})");

    async Task Physics(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    Task Seconds(float s) => Physics(Mathf.CeilToInt(s * Engine.PhysicsTicksPerSecond));

    async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Eylemin verideki ilk baglamasina bas-birak (gercek aygit olayi).</summary>
    async Task Tap(string action, InputDevice dev)
    {
        var tok = _d.Controls.Bindings(action, dev)[0];
        Input.ParseInputEvent(Controls.DeviceEvent(tok, dev, true));
        await Frames(2);
        Input.ParseInputEvent(Controls.DeviceEvent(tok, dev, false));
        await Frames(2);
    }

    void Fire(bool down) => Input.ParseInputEvent(Controls.DeviceEvent(_d.Controls.Bindings("fire", InputDevice.Kbm)[0], InputDevice.Kbm, down));

    StaticBody3D Box(Vector3 size, Vector3 at)
    {
        var b = new StaticBody3D { Name = "CamTestBox" };
        b.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(b);
        b.GlobalPosition = at;
        return b;
    }

    /// <summary>Yaratik yerine: duz bir CharacterBody3D (kamera bunu engel saymamali).</summary>
    CharacterBody3D Creature(Vector3 at)
    {
        var b = new CharacterBody3D { Name = "CamTestCreature" };
        b.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.4f } });
        AddChild(b);
        b.GlobalPosition = at;
        return b;
    }

    async Task Run()
    {
        while ((_d = GetParent().GetNodeOrNull<Director>("Director")) == null || _d.Player == null || _d.Capa == null || _d.Controls == null)
            await Physics(1);
        while (!(_d.Pod?.IntroDone ?? true)) await Physics(1);
        await Physics(10);
        bool ownExisted = FileAccess.FileExists(OwnSettings);
        ulong ownTime = ownExisted ? FileAccess.GetModifiedTime(OwnSettings) : 0;
        Check(P.CameraMode == Player.CamMode.Normal, "baslangic kipi normal degil (sinamada ayar dosyasi okunmamali)");

        Box(new Vector3(80, 1, 80), Site + Vector3.Down * 0.5f);
        P.GlobalPosition = Site + Vector3.Up * 0.05f;
        P.Velocity = Vector3.Zero;
        P.AimOverride = null;
        P.Yaw.Rotation = Vector3.Zero;
        P.Pitch.Rotation = Vector3.Zero;
        _d.Capa.Select(Capa.Head.Kazma);
        await Seconds(1.5f); // dogma yerindeki engelden (kapsul, arazi) yumusak acilma biter

        // 1. Normal kip
        float d0 = C.ClampZoom(C.Normal.Distance);
        Pose("normal", d0, C.Normal.Side, C.Normal.Fov);

        // 2. Tekerlek
        await Tap("camera_zoom_out", InputDevice.Kbm);
        Near(P.CameraZoom, C.StepZoom(d0, 1), 0.001f, "tekerlek asagi uzaklastirmadi");
        await Tap("camera_zoom_in", InputDevice.Kbm);
        await Tap("camera_zoom_in", InputDevice.Kbm);
        Near(P.CameraZoom, C.StepZoom(d0, -1), 0.001f, "tekerlek yukari yakinlastirmadi");
        int span = Mathf.CeilToInt((C.ZoomMax - C.ZoomMin) / C.WheelStep) + 2;
        for (int i = 0; i < span; i++) await Tap("camera_zoom_out", InputDevice.Kbm);
        Near(P.CameraZoom, C.ZoomMax, 0.001f, "en uzakta durmadi");
        await Seconds(0.8f);
        Near(P.CameraBoom, C.ZoomMax, 0.05f, "en uzak kamera");
        for (int i = 0; i < span; i++) await Tap("camera_zoom_in", InputDevice.Kbm);
        Near(P.CameraZoom, C.ZoomMin, 0.001f, "en yakinda durmadi");
        await Seconds(0.8f);
        Near(P.CameraBoom, C.ZoomMin, 0.05f, "en yakin kamera");
        _d.Capa.Select(Capa.Head.Insa);
        await Physics(2);
        if (_d.Capa.Current == Capa.Head.Insa)
        {
            await Tap("camera_zoom_out", InputDevice.Kbm);
            Near(P.CameraZoom, C.ZoomMin, 0.001f, "Insa basligindayken tekerlek yakinlastirdi (parca degistirmeli)");
        }
        else Check(false, "Insa secilemedi");
        _d.Capa.Select(Capa.Head.Kazma);
        P.SetCameraZoom(d0);
        await Physics(5);

        // 3. Yakin omuz (V), tekerlek bu kipte yok
        await Tap("camera_mode", InputDevice.Kbm);
        Check(P.CameraMode == Player.CamMode.Shoulder, "V yakin omuza gecirmedi");
        await Seconds(C.TransitionSeconds + 0.3f);
        Pose("yakin omuz", C.Shoulder.Distance, C.Shoulder.Side, C.Shoulder.Fov);
        float z = P.CameraZoom;
        await Tap("camera_zoom_in", InputDevice.Kbm);
        await Tap("camera_zoom_out", InputDevice.Kbm);
        await Tap("camera_zoom_out", InputDevice.Kbm);
        Near(P.CameraZoom, z, 0.001f, "yakin omuzda tekerlek uzakligi degistirdi");

        // 4-5. Yakin omuzda nisan ve carpisma
        _d.State.Capa.Unlock(_d.Data.Tool.Nailgun.Id);
        await Aim("yakin omuz");
        await Collide("yakin omuz", C.Shoulder.Distance, C.Shoulder.Side);

        // Kolun R3'u normal kipe geri alir
        await Tap("camera_mode", InputDevice.Pad);
        Check(P.CameraMode == Player.CamMode.Normal, "R3 normal kipe almadi");
        await Seconds(C.TransitionSeconds + 0.3f);
        Pose("normal (geri)", d0, C.Normal.Side, C.Normal.Fov);
        await Aim("normal");
        await Collide("normal", d0, C.Normal.Side);

        // En uzak normal kipte nisan (isin oyuncunun derinliginden baslar)
        P.SetCameraZoom(C.ZoomMax);
        await Seconds(0.5f);
        await Aim("normal en uzak");
        P.SetCameraZoom(d0);

        // 6. Ayar dosyasi, ayri yuvada
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://test"));
        P.SetCameraMode(Player.CamMode.Shoulder);
        P.SetCameraZoom(3.3f);
        Check(P.SaveCameraSettings(TestSettings) == Error.Ok, "ayar kaydedilemedi");
        P.SetCameraMode(Player.CamMode.Normal);
        P.SetCameraZoom(6f);
        Check(P.LoadCameraSettings(TestSettings), "ayar okunamadi");
        Check(P.CameraMode == Player.CamMode.Shoulder, "geri okunan kip yakin omuz degil");
        Near(P.CameraZoom, 3.3f, 0.001f, "geri okunan uzaklik");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(TestSettings));
        Check(FileAccess.FileExists(OwnSettings) == ownExisted && (!ownExisted || FileAccess.GetModifiedTime(OwnSettings) == ownTime),
              "sahibin user://camera_settings.cfg dosyasi degisti");
        Check(P.GlobalPosition.DistanceTo(Site) < 1.5f, $"oyuncu sinama zemininden kaydi ({P.GlobalPosition})");
    }

    /// <summary>Kameranin pivota gore yeri ve gorus acisi.</summary>
    void Pose(string mode, float dist, float side, float fov)
    {
        var local = P.Pitch.GlobalTransform.AffineInverse() * P.Cam.GlobalPosition;
        Near(P.CameraBoom, dist, 0.03f, $"{mode}: uzaklik");
        Near(local.Z, dist, 0.03f, $"{mode}: kamera pivotun arkasinda");
        Near(local.X, side, 0.02f, $"{mode}: omuz ofseti");
        Near(P.Cam.Fov, fov, 0.1f, $"{mode}: gorus acisi");
        GD.Print($"CAMERA {mode} boom={P.CameraBoom:0.00} side={P.CameraSide:0.00} fov={P.Cam.Fov:0.0} cam_local={local}");
    }

    /// <summary>
    /// Ekranin ortasindaki hedefe Kazma ve Civi ile nisan (AimOverride yok). Kamera ile oyuncu arasinda bir yaratik
    /// govdesi durur: nisani arkaya cekmemeli, kamerayi da itmemeli.
    /// </summary>
    async Task Aim(string mode)
    {
        var cam = P.Cam.GlobalTransform;
        var dir = -cam.Basis.Z;
        var target = Box(new Vector3(3f, 3f, 0.4f), cam.Origin + dir * (P.CameraBoom + 12f));
        var pivotDepth = (P.Pitch.GlobalPosition - cam.Origin).Dot(dir);
        var between = Creature(cam.Origin + dir * (pivotDepth * 0.5f));
        float boom = P.CameraBoom;
        await Physics(3);
        Near(P.CameraBoom, boom, 0.03f, $"{mode}: kamera ile oyuncu arasindaki yaratik kamerayi itti");
        foreach (var head in new[] { Capa.Head.Kazma, Capa.Head.Civi })
        {
            _d.Capa.Select(head);
            await Physics(2);
            Check(_d.Capa.Current == head, $"{mode}: {head} secilemedi");
            Fire(true);
            await Physics(45);
            var aim = P.AimPoint(out var hit, 60f);
            Check(hit == target, $"{mode}/{head}: ekranin ortasi hedefi vurmuyor (hit={(hit as Node)?.Name})");
            var m = P.Muzzle.GlobalPosition;
            var q = PhysicsRayQueryParameters3D.Create(m, aim + (aim - m).Normalized() * 0.1f);
            q.Exclude = new Godot.Collections.Array<Rid> { P.GetRid() };
            var r = P.GetWorld3D().DirectSpaceState.IntersectRay(q);
            Check(r.Count > 0 && (Node)r["collider"] == target, $"{mode}/{head}: namludan nisan noktasina isin hedefe varmiyor");
            float err = Mathf.RadToDeg(P.Muzzle.GlobalBasis.Z.Normalized().AngleTo((aim - m).Normalized()));
            Check(err < 4f, $"{mode}/{head}: namlu nisana bakmiyor ({err:0.0} derece)");
            GD.Print($"CAMERA aim {mode}/{head} hit={(hit as Node)?.Name} muzzleErr={err:0.0}deg aim={aim}");
            Fire(false);
            await Physics(5);
        }
        _d.Capa.Select(Capa.Head.Kazma);
        target.QueueFree();
        between.QueueFree();
        await Physics(3);
    }

    /// <summary>Arkadaki ve yandaki duvar; arkadaki yaratik.</summary>
    async Task Collide(string mode, float dist, float side)
    {
        var pivot = P.Pitch.GlobalPosition;
        var b = P.Pitch.GlobalBasis.Orthonormalized();
        float wallAt = Mathf.Max(dist * 0.5f, 0.75f); // oyuncu kapsulunun (0,42 m) arkasinda
        var wall = Box(new Vector3(8f, 6f, 0.3f), pivot + b.Z * wallAt);
        await Physics(3);
        float limit = wallAt - 0.15f - C.CollisionRadius + 0.02f;
        Check(P.CameraBoom <= limit, $"{mode}: arkadaki duvar kamerayi one cekmedi (boom={P.CameraBoom:0.00} > {limit:0.00})");
        Check(P.CameraBoom >= 0f, $"{mode}: kamera omuz noktasinin onune gecti");
        GD.Print($"CAMERA wall {mode} at={wallAt:0.00} boom={P.CameraBoom:0.00} limit={limit:0.00}");
        wall.QueueFree();
        await Physics(3);
        Check(P.CameraBoom < dist - 0.05f, $"{mode}: duvar kalkinca kamera bir anda geri sicradi (yumusak acilmali; boom={P.CameraBoom:0.00})");
        await Seconds(1.0f);
        Near(P.CameraBoom, dist, 0.03f, $"{mode}: duvar kalkinca kamera geri acilmadi");

        var sideWall = Box(new Vector3(0.2f, 6f, 8f), pivot + b.X * 0.6f); // yuzu pivotun 0,5 m saginda (kapsul 0,42)
        await Physics(3);
        float sideLimit = 0.5f - C.CollisionRadius + 0.02f;
        Check(P.CameraSide <= sideLimit, $"{mode}: yandaki duvar omuz ofsetini kisaltmadi (side={P.CameraSide:0.00} > {sideLimit:0.00})");
        sideWall.QueueFree();
        await Physics(3);
        Near(P.CameraSide, side, 0.02f, $"{mode}: yan duvar kalkinca omuz ofseti");
        await Seconds(1.0f);

        var cr = Creature(pivot + b.X * side + b.Z * (dist * 0.6f));
        await Physics(3);
        Near(P.CameraBoom, dist, 0.03f, $"{mode}: arkadaki yaratik govdesi kamerayi itti");
        cr.QueueFree();
        await Physics(3);
    }
}
