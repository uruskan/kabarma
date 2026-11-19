using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Omuz ustu ucuncu sahis oyuncu (PLAN §4). Govde: uretilen kemikli model, yoksa koddan tulumlu govde.
/// Parcalar: Player.Anim.cs (model, klip secimi), Player.Grip.cs (tutulan nesneler: el soketi, nisan, araba),
/// Player.Camera.cs (kamera: normal ve yakin omuz kipi, carpisma, ayar dosyasi), Player.Stamina.cs
/// (depar ve dayaniklilik), Player.Hooks.cs (olaylar -> vizor ve pozlar), PlayerRig/PlayerBody/PlayerPoses (kodla
/// katmanlar), Visor.cs. Burada: hareket, kamera, nisan, hasar, yere serilme.
/// </summary>
public partial class Player : CharacterBody3D
{
    public Node3D Yaw { get; private set; }
    public Node3D Pitch { get; private set; }
    public Node3D Arm { get; private set; }
    public Camera3D Cam { get; private set; }
    public Node3D Visual { get; private set; }
    public Node3D Muzzle { get; private set; }
    public Node3D BackpackPoint { get; private set; }
    Node3D _capa;

    public float WalkSpeed = 5f, SprintSpeed = 8f, JumpVelocity = 6.5f, MaxHp = 100f;
    public float SpeedMult = 1f;
    /// <summary>Yemek hiz eki (Kozde Misir +0,05); Director.ApplyFoodEffects yazar.</summary>
    public float FoodSpeed;
    /// <summary>Dolap (uye pasifi): hiz eki (Kacak +0,08) ve suzulme (Gidak Kostumlu). Director.Dolap.cs ayarlar.</summary>
    public float MemberSpeed;
    public bool Glide;
    /// <summary>Zirh puani (GDD 16.1), Kalkan Jeneratoru kalkani ve en yuksegi; Director.ApplyArmor yazar (kusam, M4).</summary>
    public float ArmorPoints, Shield, ShieldMax, ShieldIdle;
    public float Hp;
    public bool Down { get; private set; }
    float _downT;
    bool _dead;
    Vector3 _prevVel;
    float _prevYaw;
    public Vector3 RespawnPoint;
    float _shake;
    float _recoil, _recoilVel;
    Vector3 _knock;
    float _gravity = 18f;

    // Bot ve sinamalar icin: girisi gecersiz kilar.
    public Vector3? MoveOverride;
    /// <summary>Panel acik (Director.ReadInput her kare yazar): hareket ve ziplama girdisi panele gider (kolda Ⓐ = ziplama ve onay).</summary>
    public bool UiCapture;
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
        LoadGrips(); // Player.Grip.cs
        BuildCapa();
        BackpackPoint = new Node3D { Position = new Vector3(0, 1.1f, -0.4f) };
        Visual.AddChild(BackpackPoint);

        BuildCamera(); // Player.Camera.cs: iki kip (normal, yakin omuz), carpisma, camera.json
        // Oyun acilinca fare dogrudan kilitlenir; Esc birakir, tiklama geri kilitler.
        if (!Main.Args.ContainsKey("bot")) Input.MouseMode = Input.MouseModeEnum.Captured;
        HookEvents(); // Player.Hooks.cs
    }

    public override void _Notification(int what)
    {
        // Pencere odagi geri gelince (alt-tab) fareyi tekrar kilitle.
        if (what == NotificationApplicationFocusIn && !Main.Args.ContainsKey("bot")) Input.MouseMode = Input.MouseModeEnum.Captured;
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
        // Model grips.json "capa"da; tutamak ve namlu modelin icindeki adli dugumler (pipeline/grip_markers.py olcer).
        // Capa dugumunun cercevesi namlu dugumudur: koku namlu ucu, +Z namlu ekseni, +Y ust. Tutamak bu cercevede.
        var def = _capaDef;
        var golden = def != null && !string.IsNullOrEmpty(def.Model) ? Assets.Load(def.Model) : null;
        var muzzle = golden?.FindChild(def.MuzzleNode, true, false) as Node3D;
        var grip = def?.Right != null && !string.IsNullOrEmpty(def.Right.Node) ? golden?.FindChild(def.Right.Node, true, false) as Node3D : null;
        if (golden != null && (muzzle == null || grip == null))
        {
            GD.PushError($"Capa modelinde '{def.MuzzleNode}' ya da '{def.Right?.Node}' dugumu yok ({def.Model}): pipeline/grip_markers.py kosun. Yedek model kullaniliyor.");
            golden.QueueFree();
            golden = null;
        }
        if (golden != null)
        {
            golden.Transform = Rel(golden, muzzle).AffineInverse();
            _capa.AddChild(golden);
            Muzzle = muzzle;
            _capaGripNode = grip;
            _capaGripR = golden.Transform * Rel(golden, grip) * Frame(def.Right);
        }
        else
        {
            _capa.AddChild(kit.Instance());
            Muzzle = new Node3D { Position = new Vector3(0, 0, 0.68f) }; // yedek modelin namlusu ve tutamagi (yukaridaki kit)
            _capa.AddChild(Muzzle);
            _capaGripR = Frame(new[] { 0f, -0.18f, -0.02f }, new[] { 0f, 0.955f, 0.296f }, new[] { 0f, -0.296f, 0.955f });
        }
        Visual.AddChild(_capa);
    }

    /// <summary>Alt dugumun kok dugume gore donusumu (agaca eklenmeden once de calisir).</summary>
    static Transform3D Rel(Node3D root, Node3D n)
    {
        var t = Transform3D.Identity;
        for (var c = n; c != null && c != root; c = c.GetParent() as Node3D) t = c.Transform * t;
        return t;
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseMotion wm && LookLocked) { Wheel?.Invoke(wm.Relative); return; }
        // Basliksiz kipte imlec yok, kilitlenemez: fare hareketi yine bakistir (--test=input).
        if (e is InputEventMouseMotion mm && (Input.MouseMode == Input.MouseModeEnum.Captured || DisplayServer.GetName() == "headless"))
        {
            Yaw.RotateY(-mm.Relative.X * MouseSens);
            var r = Pitch.Rotation;
            r.X = Mathf.Clamp(r.X - mm.Relative.Y * MouseSens, -1.2f, 0.75f);
            Pitch.Rotation = r;
        }
        // Pencereye tiklayinca fare oyuna kilitlenir (pano tiklanabilir bir sey tasimaz).
        if (e is InputEventMouseButton mb && mb.Pressed && Input.MouseMode != Input.MouseModeEnum.Captured)
            Input.MouseMode = Input.MouseModeEnum.Captured;
        if (e is InputEventKey && e.IsActionPressed("ui_cancel")) Input.MouseMode = Input.MouseModeEnum.Visible; // kolun Ⓑ'si fareyi birakmaz
    }

    /// <summary>Ekranin ortasindan nisan alinan dunya noktasi.</summary>
    public Vector3 AimPoint(out Node hit, float maxDist = 60f)
    {
        hit = null;
        if (AimOverride is Vector3 o) return o;
        var from = Cam.GlobalPosition;
        var dir = -Cam.GlobalBasis.Z;
        // Isin oyuncunun derinliginden baslar: kamera ile oyuncu arasindaki bir govde (yaratik, cit) nisani arkaya cekmez.
        from += dir * Mathf.Max(0f, (Pitch.GlobalPosition - from).Dot(dir));
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
        if (GetParent() is Director rd && !rd.Riv.PlayerHurtAllowed(GlobalPosition)) return; // Cekisme (GDD 22.2 kural 2): tapuda gunduz dokunulmaz
        dmg = AbilitySystem.HurtFilter?.Invoke(this, dmg, from) ?? dmg; if (dmg <= 0) return; // gucler: Atilma, Kalkan Balonu, Dev Mod (kart etkisinden once: kacinilan vurus karti tetiklemez)
        if (GetParent() is Director cd) { dmg = cd.CardGuard(dmg, from); if (dmg <= 0) return; } // kartlar: Kabuk, Dikenli, Ufak Tefek
        if (ArmorPoints > 0f || Shield > 0f) { var (ad, ashield) = Kabarma.Sim.ArmorRules.Absorb(dmg, ArmorPoints, Shield); Shield = ashield; ShieldIdle = 0f; dmg = ad; if (dmg <= 0f) return; } // kusam: zirh takimi ve Kalkan Jeneratoru
        Hp -= dmg;
        _shake = Mathf.Max(_shake, 0.35f);
        var k = (GlobalPosition - from); k.Y = 0;
        _knock += k.Normalized() * 6f + Vector3.Up * 2f;
        _knock = _knock.LimitLength(12f); // kabarma3: kalabalikta ayni karede onlarca vurus oyuncuyu haritadan firlatiyordu (KS 10 olcumu)
        Fx.DamageNumber(GlobalPosition + Vector3.Up * 1.8f, dmg, false);
        Sfx.Play("hit", GlobalPosition, -2f);
        if (Hp <= 0)
        {
            Hp = 0;
            _dead = true;
            StartDown(k.Normalized() * 2.5f + Vector3.Up * 1.5f, RigDef.DownSeconds);
            NetDowned(); // Player.Net.cs (ortak): ortak oyunda 30 sn baygin, sok tokadiyla kalkar
        }
        else
        {
            if (_mode != AnimMode.Action) PlayAction("hurt", 0.45f);
            Visor?.Event("hasar");
        }
    }

    /// <summary>
    /// Hasarsiz yere serilme (GDD §3.2: arac ya da 150 kg'dan agir nesne carpinca 5 sn ragdoll). Sure dolunca
    /// oyuncu bulundugu yerde, yuzustu ya da sirtustu yatisina gore kalkar.
    /// </summary>
    public void Knockdown(Vector3 impulse)
    {
        if (Down) return;
        _dead = false;
        StartDown(impulse, RigDef.KnockdownSeconds);
    }

    void StartDown(Vector3 impulse, float seconds)
    {
        Down = true;
        _downT = seconds;
        _mode = AnimMode.Move;
        _capaOnBack = false; // Capa elde serilir (Player.Grip.cs: nesne hep elden yerlesir)
        if (Rig != null) Rig.StartRagdoll(impulse);
        else Visual.Rotation = new Vector3(-1.4f, Visual.Rotation.Y, 0);
        Visor?.SetDown(true);
    }

    /// <summary>
    /// Yere serilme bitti. Can sifirlanmissa (olum) eski kural: tam canla dogma noktasinda kalkar.
    /// Hasarsiz serilmede bulundugu yerde, ragdoll pozundan kalkis klibine harmanlanarak kalkar.
    /// </summary>
    void EndDown()
    {
        Down = false;
        Visor?.SetDown(false);
        if (Rig == null)
        {
            Visual.Rotation = new Vector3(0, Visual.Rotation.Y, 0);
            if (_dead) { Hp = MaxHp; GlobalPosition = RespawnPoint; }
            return;
        }
        var lying = Rig.StopRagdoll();
        if (_dead)
        {
            Hp = MaxHp;
            GlobalPosition = RespawnPoint;
            BeginGetup(false, false);
        }
        else
        {
            GlobalPosition = GroundBelow(lying.Hips);
            Visual.Rotation = new Vector3(0, Mathf.Atan2(lying.Forward.X, lying.Forward.Z), 0);
            BeginGetup(lying.FaceDown, true);
        }
        _dead = false;
        Velocity = Vector3.Zero;
    }

    Vector3 GroundBelow(Vector3 p)
    {
        var q = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 1.0f, p + Vector3.Down * 3f, 1u);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        return hit.Count > 0 ? (Vector3)hit["position"] + Vector3.Up * 0.02f : p + Vector3.Down * 0.13f;
    }

    public void Shake(float amount) { if (UserSettings.Current.Shake) _shake = Mathf.Max(_shake, amount); } // M6 arayuz: erisilebilirlik ayari ekran sarsintisini kapatir

    /// <summary>Geri tepme yayi (GDD 4): Capa geriye ve yukari teper, yayla yerine doner.</summary>
    public void Recoil(float amount) => _recoilVel += amount * RecoilImpulse; // Player.Feel.cs

    public bool Facing; // arac kullanilirken govde kameraya doner
    public bool Carrying; // Cekim ile yuk tasirken agir yuk yurumesi

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Yaw.GlobalPosition = GlobalPosition;
        if (Down)
        {
            if (Rig != null && Main.Args.ContainsKey("ragtest") && Engine.GetPhysicsFrames() % 20 == 0) GD.Print($"RAG hips={Rig.HipsPosition} body={GlobalPosition} capa={_capa.GlobalPosition}");
            // Bayginken Capa elde kalir: Player.Grip.cs, iskeletin son pozunda (ragdoll ile birlikte serilir).
            SprintGate(false, dt);
            Visor?.Tick(dt, this);
            _downT -= dt;
            if (_downT <= 0) EndDown();
            Velocity = new Vector3(0, Velocity.Y - _gravity * dt, 0);
            MoveAndSlide();
            UpdateCamera(dt);
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
        if (MovementLocked) wish = Vector3.Zero; // kalkis bitene kadar
        if (UiCapture && MoveOverride == null) wish = Vector3.Zero; // panel acik: yon tuslari / sol cubuk paneli gezer (girdi)
        sprint = SprintGate(sprint && wish.LengthSquared() > 0.01f, dt); // Player.Stamina.cs: GDD §3.2
        float speed = (sprint ? SprintSpeed : WalkSpeed) * SpeedMult * (1f + FoodSpeed + MemberSpeed);
        var v = Velocity;
        var target = wish * speed;
        float accel = IsOnFloor() ? 14f : 4f;
        v.X = Mathf.MoveToward(v.X, target.X, accel * speed * dt);
        v.Z = Mathf.MoveToward(v.Z, target.Z, accel * speed * dt);
        if (!IsOnFloor()) { v.Y -= _gravity * dt; if (Glide && v.Y < -1.6f && Input.IsActionPressed("jump") && MoveOverride == null) v.Y = -1.6f; } // dolap: Gidak Kostumlu suzulur
        else if (Input.IsActionJustPressed("jump") && MoveOverride == null && !MovementLocked && !UiCapture) { v.Y = JumpVelocity; OnJump(); }
        v += _knock;
        _knock = _knock.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 8f));
        Velocity = v;
        MoveAndSlide();

        // Govde yonu: alet kullanirken nisan yonune, yururken hareket yonune; poz ve kalkista donmez.
        var flat = new Vector3(Velocity.X, 0, Velocity.Z);
        _aimCache = AimPoint(out _);
        float targetYaw = Visual.Rotation.Y;
        if (ArmsPosed) { } // Player.Anim.cs: hareket grubundan bir pozda (comelme) govde nisana doner
        else if (Facing || AimOverride != null)
        {
            var d = _aimCache - GlobalPosition; d.Y = 0;
            if (d.LengthSquared() > 0.01f) targetYaw = Mathf.Atan2(d.X, d.Z);
        }
        else if (flat.LengthSquared() > 0.3f) targetYaw = Mathf.Atan2(flat.X, flat.Z);
        Visual.Rotation = new Vector3(0, Mathf.LerpAngle(Visual.Rotation.Y, targetYaw, Mathf.Min(1f, dt * 12f)), 0);

        UpdateAnim(dt, flat); // Player.Anim.cs

        // Capa geri tepme yayi: x'' = -k x - c x' (Capa'nin yeri Player.Grip.cs'te, iskeletin son pozunda).
        _recoilVel += (-RecoilStiffness * _recoil - RecoilDamping * _recoilVel) * dt; // feel.json recoil (Player.Feel.cs)
        _recoil += _recoilVel * dt;

        float yawRate = Mathf.AngleDifference(_prevYaw, Visual.Rotation.Y) / dt;
        _prevYaw = Visual.Rotation.Y;
        var accelW = (Velocity - _prevVel) / dt; accelW.Y = 0;
        _prevVel = Velocity;
        UpdateGrips(); // Player.Grip.cs: ellerin hedefleri
        if (Rig != null) Rig.ArmBlend = _grips.ArmBlend;
        Rig?.Update(dt, Facing || AimOverride != null || _cart != null, _aimCache, Visual, new PlayerRig.BodyState // araba iterken govde burulmaz
        {
            Speed = flat.Length(), Sprint = _sprinting, OnFloor = IsOnFloor(), YawRate = yawRate, Accel = accelW,
            Recoil = _recoil, FloorY = GlobalPosition.Y, Push = PushingCart || _cart != null ? 1f : 0f, Wade = Mathf.Clamp(WadeDepth, 0f, 1f),
            FootIk = _mode is AnimMode.Move or AnimMode.Land or AnimMode.Action, Posed = ArmsPosed,
        });
        Visor?.Tick(dt, this);

        // Kamera sarsintisi
        if (_shake > 0)
        {
            _shake = Mathf.Max(0, _shake - dt * ShakeDecay);
            float s = _shake * _shake;
            Cam.HOffset = (float)GD.RandRange(-1, 1) * s * ShakeOffset;
            Cam.VOffset = (float)GD.RandRange(-1, 1) * s * ShakeOffset;
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
        UpdateCamera(dt); // Player.Camera.cs
    }
}
