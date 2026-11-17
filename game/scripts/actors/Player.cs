using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Omuz ustu ucuncu sahis oyuncu (PLAN §4). Govde: uretilen kemikli model, yoksa koddan tulumlu govde.</summary>
public partial class Player : CharacterBody3D
{
    public Node3D Yaw { get; private set; }
    public Node3D Pitch { get; private set; }
    public SpringArm3D Arm { get; private set; }
    public Camera3D Cam { get; private set; }
    public Node3D Visual { get; private set; }
    public Node3D Muzzle { get; private set; }
    public Node3D BackpackPoint { get; private set; }
    Node3D _capa;
    AnimationPlayer _anim;
    Skeleton3D _skel;
    public PlayerRig Rig { get; private set; }
    string _animHurt, _animCarry, _animPush, _animKneel, _animCheer, _animWave;
    float _actionT;
    int _handBone = -1;
    string _animIdle, _animWalk, _animRun, _animJump;

    public float WalkSpeed = 5f, SprintSpeed = 8f, JumpVelocity = 6.5f, MaxHp = 100f;
    public float SpeedMult = 1f;
    public float Hp;
    public bool Down { get; private set; }
    float _downT;
    public Vector3 RespawnPoint;
    float _shake;
    float _recoil, _recoilVel;
    Vector3 _knock;
    float _bob;
    float _gravity = 18f;

    // Bot ve sinamalar icin: girisi gecersiz kilar.
    public Vector3? MoveOverride;
    public Vector3? AimOverride;
    public bool MouseCaptured;
    public bool LookLocked;                       // baslik carki acikken kamera donmez
    public System.Action<Vector2> Wheel;          // carka fare hareketi

    const float MouseSens = 0.0025f;

    public override void _Ready()
    {
        Hp = MaxHp;
        var col = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.42f, Height = 1.6f }, Position = new Vector3(0, 0.8f, 0) };
        AddChild(col);
        FloorSnapLength = 0.5f;
        FloorMaxAngle = Mathf.DegToRad(47f); // yapi setinin merdiveni 45 derece

        Visual = new Node3D { Name = "Visual" };
        AddChild(Visual);
        if (!TryLoadModel()) BuildProceduralBody();
        BuildCapa();
        BackpackPoint = new Node3D { Position = new Vector3(0, 1.1f, -0.4f) };
        Visual.AddChild(BackpackPoint);

        Yaw = new Node3D { Name = "CamYaw", TopLevel = true };
        AddChild(Yaw);
        Pitch = new Node3D { Position = new Vector3(0, 1.55f, 0) };
        Yaw.AddChild(Pitch);
        Arm = new SpringArm3D { SpringLength = 4.2f, Margin = 0.3f, Position = new Vector3(0.7f, 0, 0) };
        Arm.AddExcludedObject(GetRid());
        Pitch.AddChild(Arm);
        Cam = new Camera3D { Fov = 68f, Current = true };
        Cam.AddToGroup("genex_shot");
        Arm.AddChild(Cam);
        Pitch.Rotation = new Vector3(-0.22f, 0, 0);
        // Oyun acilinca fare dogrudan kilitlenir; Esc birakir, tiklama geri kilitler.
        if (!Main.Args.ContainsKey("bot")) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Notification(int what)
    {
        // Pencere odagi geri gelince (alt-tab) fareyi tekrar kilitle.
        if (what == NotificationApplicationFocusIn && !Main.Args.ContainsKey("bot")) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    bool TryLoadModel()
    {
        const string path = "res://assets/models/player/player.glb";
        if (!ResourceLoader.Exists(path)) return false;
        var scene = GD.Load<PackedScene>(path);
        if (scene == null) return false;
        var inst = scene.Instantiate<Node3D>();
        Visual.AddChild(inst);
        // Boyu 1,6 m'ye oturt.
        var aabb = Aabb(inst);
        // Uretilen model 1,6 m boyunda, ayaklar y=0'da teslim edildi (assets/models/player/README.md).
        // Iskelet olcegi (0,01) yuzunden AABB yanlis cikabilir: yalnizca makul bir olcum varsa duzelt.
        if (aabb.Size.Y > 0.5f && aabb.Size.Y < 5f)
        {
            float s = 1.6f / aabb.Size.Y;
            inst.Scale = Vector3.One * s;
            inst.Position = new Vector3(0, -aabb.Position.Y * s, 0);
        }
        TuneMaterials(inst);
        _skel = inst.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (_skel != null) CallDeferred(nameof(SetupRig));
        if (_skel != null && Main.Args.ContainsKey("bones")) GD.Print("BONES " + string.Join(",", Enumerable.Range(0, _skel.GetBoneCount()).Select(b => $"{b}:{_skel.GetBoneName(b)}<{_skel.GetBoneParent(b)}")) + $" skelScale={_skel.GlobalTransform.Basis.Scale}");
        if (_skel != null)
            for (int b = 0; b < _skel.GetBoneCount(); b++)
                if (_skel.GetBoneName(b).ToLower().Replace("_", "").Contains("righthand")) { _handBone = b; break; }
        _anim = inst.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (_anim != null)
        {
            var names = _anim.GetAnimationList();
            string Find(params string[] keys) => names.FirstOrDefault(n => keys.Any(k => n.ToLower().Contains(k)));
            _animIdle = Find("idle", "stand");
            _animWalk = Find("walk");
            _animRun = Find("run", "jog");
            _animJump = Find("jump");
            _animHurt = Find("hit_reaction", "hit reaction", "hurt");
            _animCarry = Find("carry");
            _animPush = Find("push_and_walk", "push");
            _animKneel = Find("crouch_pick", "pick_up", "kneel");
            _animCheer = Find("victory_cheer", "cheer", "victory");
            _animWave = Find("wave");
            if (_animCarry != null) _anim.GetAnimation(_animCarry).LoopMode = Animation.LoopModeEnum.Linear;
            foreach (var n in new[] { _animIdle, _animWalk, _animRun })
                if (n != null) _anim.GetAnimation(n).LoopMode = Animation.LoopModeEnum.Linear;
            if (_animIdle != null) _anim.Play(_animIdle);
            GD.Print($"PLAYER model anims: {string.Join(",", names)}");
        }
        return true;
    }

    void SetupRig()
    {
        Rig = new PlayerRig(this, _skel);
        Rig.Exclude(GetRid());
    }

    /// <summary>Tek seferlik eylem klibi (el sallama, sevinme, cömelme...). Hareket ederken kesilir.</summary>
    public void PlayAction(string which)
    {
        if (_anim == null) return;
        string n = which switch { "hurt" => _animHurt, "cheer" => _animCheer, "wave" => _animWave, "kneel" => _animKneel, _ => null };
        if (n == null) return;
        _anim.Play(n, 0.12);
        _actionT = (float)_anim.GetAnimation(n).Length;
    }

    /// <summary>Uretilen dokuda tulum koyu lacivert cikti (#132C47); paletteki kooperatif mavisine yaklastir.</summary>
    static void TuneMaterials(Node3D root)
    {
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                if (mi.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D sm) continue;
                var m = (StandardMaterial3D)sm.Duplicate();
                m.AlbedoColor = new Color(1.45f, 1.5f, 1.6f);
                m.Roughness = 0.85f;
                m.MetallicSpecular = 0.2f;
                mi.SetSurfaceOverrideMaterial(i, m);
            }
        }
    }

    static Aabb Aabb(Node3D root)
    {
        Aabb box = new();
        bool first = true;
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var b = mi.GetAabb();
            var xf = root.GlobalTransform.AffineInverse() * mi.GlobalTransform;
            if (!mi.IsInsideTree()) xf = RelXf(root, mi);
            var tb = xf * b;
            box = first ? tb : box.Merge(tb);
            first = false;
        }
        return box;
    }

    static Transform3D RelXf(Node3D root, Node3D n)
    {
        var xf = Transform3D.Identity;
        Node cur = n;
        while (cur != null && cur != root)
        {
            if (cur is Node3D n3) xf = n3.Transform * xf;
            cur = cur.GetParent();
        }
        return xf;
    }

    /// <summary>Yedek govde: "tulumlu" (SANAT-SES §3). Kafa govdenin ucte biri, boyun yok, buyuk eller ve botlar.</summary>
    void BuildProceduralBody()
    {
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.36f, 0.3f, 0.5f), MeshKit.At(-0.17f, 0.15f, 0.05f), Palette.DarkGrey, 0.45f);
        kit.Pillow(new Vector3(0.36f, 0.3f, 0.5f), MeshKit.At(0.17f, 0.15f, 0.05f), Palette.DarkGrey, 0.45f);
        kit.Pillow(new Vector3(0.62f, 0.5f, 0.45f), MeshKit.At(0, 0.5f, 0), Palette.CoopBlue, 0.5f);
        kit.Pillow(new Vector3(0.72f, 0.5f, 0.5f), MeshKit.At(0, 0.88f, 0), Palette.CoopBlue, 0.5f);
        kit.Pillow(new Vector3(0.74f, 0.08f, 0.52f), MeshKit.At(0, 0.68f, 0), Palette.Warning, 0.4f);
        kit.Pillow(new Vector3(0.22f, 0.48f, 0.22f), MeshKit.At(new Vector3(-0.45f, 0.82f, 0.02f), new Vector3(0, 0, -0.18f)), Palette.CoopBlue, 0.5f);
        kit.Pillow(new Vector3(0.22f, 0.48f, 0.22f), MeshKit.At(new Vector3(0.45f, 0.82f, 0.1f), new Vector3(-0.7f, 0, 0.18f)), Palette.CoopBlue, 0.5f);
        kit.Sphere(0.15f, MeshKit.At(-0.5f, 0.56f, 0.02f), Palette.DarkGrey, 8, 4);
        kit.Sphere(0.15f, MeshKit.At(0.5f, 0.7f, 0.33f), Palette.DarkGrey, 8, 4);
        kit.Sphere(0.42f, MeshKit.At(0, 1.33f, 0.02f), Palette.OffWhite, 16, 8);
        kit.Pillow(new Vector3(0.58f, 0.36f, 0.2f), MeshKit.At(0, 1.33f, 0.33f), Palette.NightDeep.Lerp(Palette.CoopBlue, 0.25f), 0.45f);
        kit.Pillow(new Vector3(0.12f, 0.12f, 0.05f), MeshKit.At(-0.12f, 1.36f, 0.44f), Palette.MayaTeal, 0.5f);
        kit.Pillow(new Vector3(0.12f, 0.12f, 0.05f), MeshKit.At(0.12f, 1.36f, 0.44f), Palette.MayaTeal, 0.5f);
        kit.Pillow(new Vector3(0.62f, 0.66f, 0.34f), MeshKit.At(0, 0.95f, -0.38f), Palette.OffWhite.Lerp(Palette.Shadow, 0.1f), 0.45f);
        kit.Pillow(new Vector3(0.5f, 0.12f, 0.36f), MeshKit.At(0, 1.2f, -0.4f), Palette.Warning, 0.4f);
        kit.Cylinder(0.025f, 0.03f, 0.35f, MeshKit.At(0.2f, 1.75f, -0.02f), Palette.DarkGrey, 6);
        kit.Sphere(0.06f, MeshKit.At(0.2f, 1.94f, -0.02f), Palette.Accent, 6, 4);
        var mi = kit.Instance();
        Visual.AddChild(mi);
    }

    void BuildCapa()
    {
        _capa = new Node3D { Position = new Vector3(0.42f, 0.9f, 0.4f) };
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.2f, 0.24f, 0.7f), MeshKit.At(0, 0, 0.15f), Palette.OffWhite, 0.4f);
        kit.Pillow(new Vector3(0.24f, 0.12f, 0.3f), MeshKit.At(0, 0.15f, 0.05f), Palette.CoopBlue, 0.4f);
        kit.Cylinder(0.11f, 0.13f, 0.18f, MeshKit.At(new Vector3(0, 0, 0.55f), new Vector3(Mathf.Pi / 2, 0, 0)), Palette.Warning, 10);
        kit.Pillow(new Vector3(0.1f, 0.24f, 0.12f), MeshKit.At(new Vector3(0, -0.18f, -0.02f), new Vector3(0.3f, 0, 0)), Palette.DarkGrey, 0.4f);
        var golden = Assets.Load("res://assets/models/capa/capa.glb");
        if (golden != null)
        {
            // Tripo modeli +X'e bakar; Capa dugumunde namlu +Z. Model 0,56 m.
            golden.RotationDegrees = new Vector3(0, -90, 0);
            golden.Position = new Vector3(0, -0.08f, 0.12f);
            _capa.AddChild(golden);
        }
        else _capa.AddChild(kit.Instance());
        Muzzle = new Node3D { Position = new Vector3(0, 0, golden != null ? 0.42f : 0.68f) };
        _capa.AddChild(Muzzle);
        Visual.AddChild(_capa);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseMotion wm && LookLocked) { Wheel?.Invoke(wm.Relative); return; }
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            Yaw.RotateY(-mm.Relative.X * MouseSens);
            var r = Pitch.Rotation;
            r.X = Mathf.Clamp(r.X - mm.Relative.Y * MouseSens, -1.2f, 0.75f);
            Pitch.Rotation = r;
        }
        // Pencereye tiklayinca fare oyuna kilitlenir (pano tiklanabilir bir sey tasimaz).
        if (e is InputEventMouseButton mb && mb.Pressed && Input.MouseMode != Input.MouseModeEnum.Captured)
            Input.MouseMode = Input.MouseModeEnum.Captured;
        if (e.IsActionPressed("ui_cancel")) Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    /// <summary>Ekranin ortasindan nisan alinan dunya noktasi.</summary>
    public Vector3 AimPoint(out Node hit, float maxDist = 60f)
    {
        hit = null;
        if (AimOverride is Vector3 o) return o;
        var from = Cam.GlobalPosition;
        var dir = -Cam.GlobalBasis.Z;
        var q = PhysicsRayQueryParameters3D.Create(from, from + dir * maxDist);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        q.CollideWithAreas = true;
        var res = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (res.Count > 0)
        {
            hit = (Node)res["collider"];
            return (Vector3)res["position"];
        }
        return from + dir * maxDist;
    }

    public Vector3 AimDir => (AimPoint(out _) - Muzzle.GlobalPosition).Normalized();

    public void Hurt(float dmg, Vector3 from)
    {
        if (Down) return;
        Hp -= dmg;
        _shake = Mathf.Max(_shake, 0.35f);
        var k = (GlobalPosition - from); k.Y = 0;
        _knock += k.Normalized() * 6f + Vector3.Up * 2f;
        Fx.DamageNumber(GlobalPosition + Vector3.Up * 1.8f, dmg, false);
        Sfx.Play("hit", GlobalPosition, -2f);
        if (Hp <= 0)
        {
            Hp = 0;
            Down = true;
            _downT = 3f;
            if (Rig != null) Rig.StartRagdoll(k.Normalized() * 2.5f + Vector3.Up * 1.5f);
            else Visual.Rotation = new Vector3(-1.4f, Visual.Rotation.Y, 0);
        }
        else if (_animHurt != null && _actionT <= 0) { _anim.Play(_animHurt, 0.08); _actionT = 0.45f; }
    }

    public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

    /// <summary>Geri tepme yayi (GDD 4): Capa geriye ve yukari teper, yayla yerine doner.</summary>
    public void Recoil(float amount) => _recoilVel += amount * 9f;

    public bool Facing; // arac kullanilirken govde kameraya doner
    public bool Carrying; // Cekim ile yuk tasirken agir yuk yurumesi

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Yaw.GlobalPosition = GlobalPosition;
        if (Down)
        {
            if (Rig != null && Main.Args.ContainsKey("ragtest") && Engine.GetPhysicsFrames() % 20 == 0) GD.Print($"RAG hips={Rig.HipsPosition} body={GlobalPosition}");
            // Bayginken Capa elde kalir (ragdoll ile birlikte serilir).
            if (_skel != null && _handBone >= 0)
            {
                var handDown = _skel.GlobalTransform * _skel.GetBoneGlobalPose(_handBone);
                _capa.GlobalTransform = new Transform3D(handDown.Basis.Orthonormalized(), handDown.Origin);
            }
            _downT -= dt;
            if (_downT <= 0)
            {
                Down = false;
                Hp = MaxHp;
                Rig?.StopRagdoll();
                GlobalPosition = RespawnPoint;
                Visual.Rotation = new Vector3(0, Visual.Rotation.Y, 0);
            }
            Velocity = new Vector3(0, Velocity.Y - _gravity * dt, 0);
            MoveAndSlide();
            return;
        }

        Vector3 wish;
        bool sprint = Input.IsActionPressed("sprint");
        if (MoveOverride is Vector3 mo) { wish = mo; sprint = mo.Length() > 1.01f; wish = wish.LimitLength(1f); }
        else
        {
            var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            var basis = Yaw.GlobalBasis;
            wish = (basis.X * input.X + basis.Z * input.Y);
            wish.Y = 0;
            wish = wish.LimitLength(1f);
        }
        float speed = (sprint ? SprintSpeed : WalkSpeed) * SpeedMult;
        var v = Velocity;
        var target = wish * speed;
        float accel = IsOnFloor() ? 14f : 4f;
        v.X = Mathf.MoveToward(v.X, target.X, accel * speed * dt);
        v.Z = Mathf.MoveToward(v.Z, target.Z, accel * speed * dt);
        if (!IsOnFloor()) v.Y -= _gravity * dt;
        else if (Input.IsActionJustPressed("jump") && MoveOverride == null) { v.Y = JumpVelocity; if (_animJump != null) _anim.Play(_animJump, 0.1); }
        v += _knock;
        _knock = _knock.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 8f));
        Velocity = v;
        MoveAndSlide();

        // Govde yonu: alet kullanirken nisan yonune, yururken hareket yonune.
        var flat = new Vector3(Velocity.X, 0, Velocity.Z);
        float targetYaw = Visual.Rotation.Y;
        if (Facing || AimOverride != null)
        {
            var d = AimPoint(out _) - GlobalPosition; d.Y = 0;
            if (d.LengthSquared() > 0.01f) targetYaw = Mathf.Atan2(d.X, d.Z);
        }
        else if (flat.LengthSquared() > 0.3f) targetYaw = Mathf.Atan2(flat.X, flat.Z);
        Visual.Rotation = new Vector3(0, Mathf.LerpAngle(Visual.Rotation.Y, targetYaw, Mathf.Min(1f, dt * 12f)), 0);

        // Yay katmani: yururken sekme ve egilme (kodla animasyon, SANAT-SES §3.1).
        float sp = flat.Length();
        _bob += dt * (4f + sp * 1.6f);
        if (_anim == null)
        {
            Visual.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_bob)) * 0.06f * Mathf.Min(1f, sp / 3f), 0);
            Visual.Rotation = new Vector3(Mathf.Min(sp / 8f, 1f) * 0.12f, Visual.Rotation.Y, Mathf.Sin(_bob) * 0.04f * Mathf.Min(1f, sp / 3f));
        }
        else if (IsOnFloor() && (_actionT -= dt) <= 0)
        {
            string walk = Carrying && _animCarry != null ? _animCarry : _animWalk;
            string want = sp > 6f && _animRun != null && !Carrying ? _animRun : sp > 0.6f && walk != null ? walk : _animIdle;
            if (want != null && _anim.CurrentAnimation != want) _anim.Play(want, 0.2);
            if (want == _animWalk) _anim.SpeedScale = Mathf.Clamp(sp / 3.2f, 0.6f, 1.6f);
            else _anim.SpeedScale = 1f;
        }

        Rig?.Update(dt, Facing || AimOverride != null, AimPoint(out _), Visual);

        // Capa sag elde: konum elden (kol IK'si eli nisana goturur), yon nisandan.
        if (_skel != null && _handBone >= 0)
        {
            var hand = _skel.GlobalTransform * _skel.GetBoneGlobalPose(_handBone).Origin;
            var fwd = Facing || AimOverride != null ? (AimPoint(out _) - hand).Normalized() : Visual.GlobalBasis.Z;
            if (fwd.LengthSquared() < 0.01f) fwd = Visual.GlobalBasis.Z;
            // yay: x'' = -k x - c x'
            float dtp = (float)GetPhysicsProcessDeltaTime();
            _recoilVel += (-180f * _recoil - 18f * _recoilVel) * dtp;
            _recoil += _recoilVel * dtp;
            _capa.GlobalPosition = hand + fwd * (0.12f - _recoil * 0.18f) + Vector3.Up * _recoil * 0.05f;
            _capa.GlobalBasis = Basis.LookingAt(-fwd, Vector3.Up).Rotated(Basis.LookingAt(-fwd, Vector3.Up).X, _recoil * 0.6f);
        }

        // Kamera sarsintisi
        if (_shake > 0)
        {
            _shake = Mathf.Max(0, _shake - dt * 2.5f);
            float s = _shake * _shake;
            Cam.HOffset = (float)GD.RandRange(-1, 1) * s * 0.4f;
            Cam.VOffset = (float)GD.RandRange(-1, 1) * s * 0.4f;
        }
        else { Cam.HOffset = 0; Cam.VOffset = 0; }

        // Bot: kamera nisan noktasina doner, oyun goruntusu dogru kalir.
        if (AimOverride is Vector3 aim)
        {
            var d = aim - Pitch.GlobalPosition;
            float yaw = Mathf.Atan2(-d.X, -d.Z);
            Yaw.Rotation = new Vector3(0, Mathf.LerpAngle(Yaw.Rotation.Y, yaw, Mathf.Min(1f, dt * 6f)), 0);
            float pitch = Mathf.Atan2(d.Y, new Vector2(d.X, d.Z).Length());
            Pitch.Rotation = new Vector3(Mathf.Lerp(Pitch.Rotation.X, Mathf.Clamp(pitch - 0.12f, -0.9f, 0.4f), Mathf.Min(1f, dt * 6f)), 0, 0);
        }
    }
}
