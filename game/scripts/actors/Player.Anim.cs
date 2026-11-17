using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Oyuncu: uretilen modelin yuklenmesi, iskelet/rig baglantisi ve animasyon secimi (SANAT-SES §3.1).</summary>
public partial class Player
{
    AnimationPlayer _anim;
    Skeleton3D _skel;
    public PlayerRig Rig { get; private set; }
    string _animHurt, _animCarry, _animPush, _animKneel, _animCheer, _animWave;
    float _actionT;
    int _handBone = -1;
    string _animIdle, _animWalk, _animRun, _animJump;
    float _bob;

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

    /// <summary>Govde animasyonu: model yoksa kodla sekme/egilme, varsa yurume/kosma/tasima klibi secimi.</summary>
    void UpdateAnim(float dt, Vector3 flat)
    {
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
    }
}
