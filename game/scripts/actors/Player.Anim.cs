using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuncu: uretilen modelin yuklenmesi, iskelet/rig baglantisi ve klip secimi (SANAT-SES §3.1).
/// Klipler game/data/player_anims.json'dan: kimlikle (tam adla) secilir; yer tutucu klip yerine yedegi oynar
/// (ornegin walk_b henuz yoksa walk geriye). Kesilen klipler (jump_start/air/land) ve kodla pozlar
/// (plant, eat, sleep, kalkis yer tutuculari) burada kitapliga eklenir.
/// </summary>
public partial class Player
{
    AnimationPlayer _anim;
    Skeleton3D _skel;
    public PlayerRig Rig { get; private set; }
    public Visor Visor { get; private set; }
    PlayerAnimsFile _clips = new();
    PlayerRigDef RigDef => _clips.Rig;
    int _handBone = -1;
    float _bob;

    enum AnimMode { Move, Air, Land, Action, Pose, Getup }
    AnimMode _mode = AnimMode.Move;
    float _modeT, _modeLen, _airT;
    bool _jumped, _poseLocks;
    string _cur = "", _curId = "";
    float _curSign = 1f, _idleT;

    /// <summary>El Arabasi'ni itiyor (el arabasi kodu ayarlar): ust govde itme katmani (push_cart).</summary>
    public bool PushingCart;
    /// <summary>Suyun dizden yukari derinligi, 0..1 (su kodu ayarlar): suda yurume katmani (wade).</summary>
    public float WadeDepth;
    /// <summary>Kodla poz ya da kalkis oynuyor (govde yonu ve nisan donmaz).</summary>
    public bool Posed => _mode is AnimMode.Pose or AnimMode.Getup;
    /// <summary>Kalkis bitene kadar yurume yok.</summary>
    public bool MovementLocked => _mode == AnimMode.Getup || (_mode == AnimMode.Pose && _poseLocks);
    /// <summary>Su an oynayan klibin kimligi (yer tutucu cozulmeden once).</summary>
    public string CurrentClip => _curId;

    bool TryLoadModel()
    {
        if (GetParent() is Director d && d.Data != null) _clips = d.Data.PlayerAnims;
        string path = string.IsNullOrEmpty(_clips.Model) ? "res://assets/models/player/player.glb" : _clips.Model;
        if (!ResourceLoader.Exists(path)) return false;
        var scene = GD.Load<PackedScene>(path);
        if (scene == null) return false;
        var inst = scene.Instantiate<Node3D>();
        Visual.AddChild(inst);
        // Uretilen model 1,6 m boyunda, ayaklar y=0'da teslim edildi (assets/models/player/README.md).
        var aabb = Aabb(inst);
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
        if (_skel != null) _handBone = _skel.FindBone("RightHand");
        _anim = inst.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (_anim != null)
        {
            SetupClips();
            Play("idle", 0);
            GD.Print($"PLAYER model anims: {string.Join(",", _anim.GetAnimationList())}");
        }
        return true;
    }

    void SetupRig()
    {
        Rig = new PlayerRig(this, _skel, RigDef);
        Rig.Exclude(GetRid());
        Rig.PoseReady += OnPoseReady;
        if (GetParent() is Director d && d.Data != null) Visor = Visor.Attach(_skel, d.Data.Visor);
    }

    /// <summary>Kitapliga dongu ayarlari, kesilen klipler ve kodla pozlar.</summary>
    void SetupClips()
    {
        var lib = _anim.GetAnimationLibrary("");
        if (lib == null || !_anim.HasAnimation("idle")) return;
        var idle = _anim.GetAnimation("idle");
        string first = idle.GetTrackCount() > 0 ? idle.TrackGetPath(0).ToString() : "";
        string prefix = first.Contains(':') ? first[..first.IndexOf(':')] : "";
        foreach (var c in _clips.Clips)
            if (c.Loop && _anim.HasAnimation(c.Id)) _anim.GetAnimation(c.Id).LoopMode = Animation.LoopModeEnum.Linear;
        float standY = 0f;
        int hipsTrack = idle.FindTrack(new NodePath($"{prefix}:Hips"), Animation.TrackType.Position3D);
        if (hipsTrack >= 0) standY = idle.PositionTrackInterpolate(hipsTrack, 0).Y;
        foreach (var c in _clips.Clips.Where(c => c.Source == "cut" && _anim.HasAnimation(c.From)))
            lib.AddAnimation(c.Id, Cut(_anim.GetAnimation(c.From), c.Start, c.End, c.Loop, c.FlattenHips ? standY : float.NaN));
        if (_skel == null || prefix.Length == 0) return;
        var poses = new PlayerPoses(_skel, prefix, idle);
        void Add(string id, System.Func<Animation> make) { if (!_anim.HasAnimation(id)) lib.AddAnimation(id, make()); }
        Add("plant", poses.Plant);
        Add("eat", poses.Eat);
        Add("sleep", poses.Sleep);
        Add("getup_front_pose", poses.GetupFront);
        Add("getup_back_pose", poses.GetupBack);
    }

    /// <summary>Bir klibin [t0, t1] araligini 30 kare/sn ornekleyerek yeni klip yapar. hipsY sayi ise kalca yuksekligi sabitlenir.</summary>
    static Animation Cut(Animation src, float t0, float t1, bool loop, float hipsY)
    {
        t1 = Mathf.Min(t1, (float)src.Length);
        var a = new Animation { Length = Mathf.Max(0.05f, t1 - t0), LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None };
        for (int i = 0; i < src.GetTrackCount(); i++)
        {
            var type = src.TrackGetType(i);
            if (type is not (Animation.TrackType.Position3D or Animation.TrackType.Rotation3D or Animation.TrackType.Scale3D)) continue;
            var path = src.TrackGetPath(i);
            int n = a.AddTrack(type);
            a.TrackSetPath(n, path);
            bool hips = type == Animation.TrackType.Position3D && path.ToString().EndsWith(":Hips");
            for (float t = t0; t <= t1 + 1e-4f; t += 1f / 30f)
            {
                float u = Mathf.Min(t, t1), k = u - t0;
                switch (type)
                {
                    case Animation.TrackType.Position3D:
                        var p = src.PositionTrackInterpolate(i, u);
                        if (hips && !float.IsNaN(hipsY)) p.Y = hipsY;
                        a.PositionTrackInsertKey(n, k, p); break;
                    case Animation.TrackType.Rotation3D: a.RotationTrackInsertKey(n, k, src.RotationTrackInterpolate(i, u)); break;
                    default: a.ScaleTrackInsertKey(n, k, src.ScaleTrackInterpolate(i, u)); break;
                }
            }
        }
        return a;
    }

    /// <summary>Klip kimligini oynatilacak animasyona cevirir: yer tutucu -> yedek, olmayan -> idle.</summary>
    (string name, float speed) Resolve(string id)
    {
        var (name, s) = _clips.Resolve(id);
        if (_anim != null && !_anim.HasAnimation(name)) return (_anim.HasAnimation("idle") ? "idle" : "", 1f);
        return (name, s);
    }

    float ClipSeconds(string id)
    {
        var (name, s) = Resolve(id);
        if (name.Length == 0) return 0f;
        float sp = (_clips.Clip(id)?.Speed ?? 1f) * Mathf.Abs(s);
        return (float)_anim.GetAnimation(name).Length / Mathf.Max(0.05f, sp);
    }

    /// <summary>Klibi oynatir. rate: hiz carpani (yuruyuste govde hizi / klibin adim hizi).</summary>
    void Play(string id, double blend = 0.2, float rate = 1f, bool restart = false)
    {
        if (_anim == null) return;
        var (name, s) = Resolve(id);
        if (name.Length == 0) return;
        float sign = s < 0 ? -1f : 1f;
        float clip = (_clips.Clip(id)?.Speed ?? 1f) * Mathf.Abs(s);
        _anim.SpeedScale = Mathf.Max(0.05f, rate * clip);
        _curId = id;
        if (_cur == name && Mathf.IsEqualApprox(_curSign, sign) && !restart) return;
        _cur = name; _curSign = sign;
        _anim.Play(name, blend, sign, sign < 0);
    }

    /// <summary>Tek seferlik eylem klibi (hurt, cheer, wave...). Hareket edince kesilir.</summary>
    public void PlayAction(string which, float maxSeconds = 0f)
    {
        if (_anim == null || Down || _mode == AnimMode.Getup) return;
        Play(which, 0.12, 1f, true);
        _mode = AnimMode.Action; _modeT = 0f;
        float len = ClipSeconds(which);
        _modeLen = maxSeconds > 0 ? Mathf.Min(maxSeconds, len) : len;
    }

    /// <summary>
    /// Kodla poz (plant, eat, sleep): seconds &gt; 0 ise o kadar, yoksa dongulu klip hareket edilene kadar,
    /// dongusuz klip bir kez. lockMove: sure bitene kadar yurume yok.
    /// </summary>
    public void PlayPose(string id, float seconds = 0f, bool lockMove = false)
    {
        if (_anim == null || Down || _mode == AnimMode.Getup) return;
        Play(id, 0.3, 1f, true);
        _mode = AnimMode.Pose; _modeT = 0f; _poseLocks = lockMove;
        bool loop = _clips.Clip(id)?.Loop ?? false;
        _modeLen = seconds > 0 ? seconds : loop ? 0f : ClipSeconds(id);
        if (id == "sleep") _capaOnBack = true; // pozlarda Capa elde kalir (Player.Grip.cs), uykuda sirta gecer
    }

    /// <summary>Ziplama basladi (Player._PhysicsProcess).</summary>
    void OnJump()
    {
        _jumped = true; _airT = 0f;
        if (_mode is AnimMode.Pose or AnimMode.Action) _mode = AnimMode.Move;
        _mode = AnimMode.Air; _modeT = 0f;
        Play("jump_start", 0.08, 1f, true);
    }

    /// <summary>Kalkis: yuzustu ya da sirtustu klip; ragdoll'dan geliyorsa son pozdan harmanlanir.</summary>
    void BeginGetup(bool faceDown, bool fromRagdoll)
    {
        string id = faceDown ? "getup_front" : "getup_back";
        Play(id, 0, 1f, true);
        if (fromRagdoll) Rig?.BlendFromRagdoll(RigDef.GetupBlend);
        _mode = AnimMode.Getup; _modeT = 0f; _modeLen = ClipSeconds(id);
        Visor?.Event("kalkis");
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

    /// <summary>Hareket yonunun govdeye gore sekiz dilimi: f, fl, l, bl, b, br, r, fr (sol = +X).</summary>
    string Sector(Vector3 flat)
    {
        var l = Visual.GlobalBasis.Orthonormalized().Inverse() * flat;
        float a = Mathf.RadToDeg(Mathf.Atan2(l.X, l.Z));
        float m = Mathf.Abs(a);
        string side = a > 0 ? "l" : "r";
        if (m < 22.5f) return "f";
        if (m < 67.5f) return "f" + side;
        if (m < 112.5f) return side;
        if (m < 157.5f) return "b" + side;
        return "b";
    }

    /// <summary>Govde animasyonu: model yoksa kodla sekme/egilme, varsa klip secimi.</summary>
    void UpdateAnim(float dt, Vector3 flat)
    {
        float sp = flat.Length();
        _bob += dt * (4f + sp * 1.6f);
        if (_anim == null)
        {
            Visual.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_bob)) * 0.06f * Mathf.Min(1f, sp / 3f), 0);
            Visual.Rotation = new Vector3(Mathf.Min(sp / 8f, 1f) * 0.12f, Visual.Rotation.Y, Mathf.Sin(_bob) * 0.04f * Mathf.Min(1f, sp / 3f));
            return;
        }
        _modeT += dt;
        bool floor = IsOnFloor();
        switch (_mode)
        {
            case AnimMode.Getup:
                if (_modeT < _modeLen) return;
                _mode = AnimMode.Move; break;
            case AnimMode.Pose:
                if (_modeLen > 0 ? _modeT < _modeLen && (_poseLocks || sp < 0.8f) : sp < 0.8f) return;
                _mode = AnimMode.Move; _capaOnBack = false; break;
            case AnimMode.Action:
                if (_modeT < _modeLen && sp < 1.5f) return;
                _mode = AnimMode.Move; break;
        }
        if (!floor)
        {
            _airT += dt;
            if (_mode != AnimMode.Air) { _mode = AnimMode.Air; _modeT = 0f; }
            if (_jumped && _modeT < ClipSeconds("jump_start")) return;
            Play(_airT > RigDef.FallAfter && Velocity.Y < 0 ? "fall" : "jump_air", 0.15);
            return;
        }
        if (_mode == AnimMode.Air)
        {
            bool big = _airT > 0.3f;
            _jumped = false; _airT = 0f;
            if (big && sp < 1f) { _mode = AnimMode.Land; _modeT = 0f; _modeLen = ClipSeconds("jump_land"); Play("jump_land", 0.08, 1f, true); return; }
            _mode = AnimMode.Move;
        }
        if (_mode == AnimMode.Land)
        {
            if (_modeT < _modeLen && sp < 1f) return;
            _mode = AnimMode.Move;
        }

        // Yurume: hiz ve yon. Nisan alirken govde nisana bakar, ayaklar yone gore klip secer.
        if (sp < 0.4f)
        {
            _idleT += dt;
            if (_idleT > 12f) { _idleT = 0f; PlayAction("idle_2", 6f); return; }
            Play("idle", 0.25);
            return;
        }
        _idleT = 0f;
        string gait = Carrying && _anim.HasAnimation("carry") ? "carry" : _sprinting && sp > 6.5f ? "sprint" : sp > 4.2f ? "run" : "walk";
        string id = gait;
        if ((Facing || AimOverride != null) && gait is "walk" or "run")
        {
            string dir = Sector(flat);
            if (dir != "f") id = gait + "_" + dir;
        }
        float refSp = _clips.Clip(id)?.RefSpeed ?? 0f;
        if (refSp <= 0) refSp = _clips.Clip(gait)?.RefSpeed ?? 3.2f;
        Play(id, 0.2, Mathf.Clamp(sp / refSp, 0.6f, 1.6f));
    }
}
