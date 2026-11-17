using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Oyuncunun kodla yazilan klipleri (SANAT-SES §3.1 "Kodla poz", URETIM §2.2 D/E poz yontemi): anahtar pozlar
/// ve yumusatma -> Godot Animation. Poz, model uzayinda (Y yukari, +Z govdenin onu, sag el -X) kemik YONLERIYLE
/// yazilir: "sag uyluk ileri", "kaval asagi". Her kemik, temel pozdaki (dinlenme ya da bir klibin ilk karesi)
/// yonunden istenen yone en kisa yay ile doner; yonu yazilmayan kemik ebeveyniyle birlikte doner.
/// Boylar iskeletten olculur (uyluk, kaval, kalca yuksekligi); pozlar baska boydaki govdeye de oturur.
///
/// M1'in 5 kodla klibi: plant (diz cokup ekme), eat (yeme), sleep (uyuma) burada tam govde klibi;
/// push_cart (el arabasi itme) ve wade (suda yurume) yurume ustune katman, PlayerBody'de.
/// Ayrica kalkis klipleri gelene kadar yer tutucu: getup_front_pose, getup_back_pose.
/// </summary>
public sealed class PlayerPoses
{
    readonly Skeleton3D _skel;
    readonly string _prefix; // "Armature/Skeleton3D" gibi; iz yolu "<prefix>:<kemik>"
    readonly Animation _base; // temel poz (idle'in ilk karesi); null ise dinlenme pozu
    readonly int _root;
    readonly Dictionary<string, int> _idx = new();

    // Olculen boylar (m)
    public readonly float Thigh, Shin, HipDrop, Ankle, StandHips;

    public PlayerPoses(Skeleton3D skel, string trackPrefix, Animation baseAnim)
    {
        _skel = skel; _prefix = trackPrefix; _base = baseAnim;
        for (int b = 0; b < skel.GetBoneCount(); b++) _idx[skel.GetBoneName(b)] = b;
        _root = Find("Hips");
        _order = Order(skel);
        Vector3 G(string n) => skel.GetBoneGlobalRest(Find(n)).Origin;
        Thigh = G("LeftUpLeg").DistanceTo(G("LeftLeg"));
        Shin = G("LeftLeg").DistanceTo(G("LeftFoot"));
        HipDrop = G("Hips").Y - G("LeftUpLeg").Y;
        Ankle = G("LeftFoot").Y;
        StandHips = G("Hips").Y;
    }

    int Find(string n) => _idx.TryGetValue(n, out var i) ? i : -1;

    readonly int[] _order;

    /// <summary>Kemikler, ebeveyn cocuktan once gelecek sirada.</summary>
    public static int[] Order(Skeleton3D skel)
    {
        var list = new List<int>();
        var queue = new Queue<int>(skel.GetParentlessBones());
        while (queue.Count > 0)
        {
            int b = queue.Dequeue();
            list.Add(b);
            foreach (int c in skel.GetBoneChildren(b)) queue.Enqueue(c);
        }
        return list.ToArray();
    }

    /// <summary>Bir anahtar poz: zaman, kemik yonleri (model uzayi), kalca konumu (model uzayi; null = temeldeki).</summary>
    public sealed class Key
    {
        public float T;
        public Vector3? Hips;
        public readonly Dictionary<string, Vector3> Dir = new();
        public Key(float t, Vector3? hips = null) { T = t; Hips = hips; }
        public Key D(string bone, float x, float y, float z) { Dir[bone] = new Vector3(x, y, z); return this; }
        /// <summary>Sol ve sag icin ayna (sag = -X).</summary>
        public Key Both(string side, float x, float y, float z) { Dir["Left" + side] = new Vector3(x, y, z); Dir["Right" + side] = new Vector3(-x, y, z); return this; }
        public Key Copy(float t) { var k = new Key(t, Hips); foreach (var (b, d) in Dir) k.Dir[b] = d; return k; }
    }

    /// <summary>Temel pozdaki bir kemigin yerel donusu (klip izi varsa ilk karesi, yoksa dinlenme).</summary>
    Quaternion BaseRot(int b)
    {
        if (_base != null)
        {
            int t = _base.FindTrack(new NodePath($"{_prefix}:{_skel.GetBoneName(b)}"), Animation.TrackType.Rotation3D);
            if (t >= 0) return _base.RotationTrackInterpolate(t, 0);
        }
        return _skel.GetBoneRest(b).Basis.GetRotationQuaternion();
    }

    Vector3 BasePos(int b)
    {
        if (_base != null)
        {
            int t = _base.FindTrack(new NodePath($"{_prefix}:{_skel.GetBoneName(b)}"), Animation.TrackType.Position3D);
            if (t >= 0) return _base.PositionTrackInterpolate(t, 0);
        }
        return _skel.GetBoneRest(b).Origin;
    }

    /// <summary>Kemigin yonunu veren cocuk (kalca omurgaya, omurga boyuna bakar; bacaklar ve canta degil).</summary>
    public static readonly Dictionary<string, string> Chain = new()
    {
        ["Hips"] = "Spine02", ["Spine02"] = "Spine01", ["Spine01"] = "Spine", ["Spine"] = "neck", ["neck"] = "Head", ["Head"] = "head_end",
        ["LeftShoulder"] = "LeftArm", ["LeftArm"] = "LeftForeArm", ["LeftForeArm"] = "LeftHand",
        ["RightShoulder"] = "RightArm", ["RightArm"] = "RightForeArm", ["RightForeArm"] = "RightHand",
        ["LeftUpLeg"] = "LeftLeg", ["LeftLeg"] = "LeftFoot", ["LeftFoot"] = "LeftToeBase",
        ["RightUpLeg"] = "RightLeg", ["RightLeg"] = "RightFoot", ["RightFoot"] = "RightToeBase",
    };

    /// <summary>Kemigin cocuk yonu, kendi yerel uzayinda (zincirdeki cocugun dinlenme konumu).</summary>
    public static Vector3 LocalDir(Skeleton3D skel, int b)
    {
        if (Chain.TryGetValue(skel.GetBoneName(b), out var child))
        {
            int c = skel.FindBone(child);
            if (c >= 0 && skel.GetBoneRest(c).Origin.LengthSquared() > 1e-8f) return skel.GetBoneRest(c).Origin.Normalized();
        }
        return Vector3.Up;
    }

    Vector3 LocalDir(int b) => LocalDir(_skel, b);

    /// <summary>Anahtar pozu her kemigin yerel donusune cevirir (ebeveynden cocuga).</summary>
    public Quaternion[] Solve(Key k)
    {
        int n = _skel.GetBoneCount();
        var local = new Quaternion[n];
        var global = new Basis[n];
        foreach (int b in _order)
        {
            int p = _skel.GetBoneParent(b);
            var baseLocal = new Basis(BaseRot(b));
            var g = p >= 0 ? global[p] * baseLocal : baseLocal;
            if (k.Dir.TryGetValue(_skel.GetBoneName(b), out var want) && want.LengthSquared() > 1e-6f)
            {
                var cur = (g * LocalDir(b)).Normalized();
                g = new Basis(Arc(cur, want.Normalized())) * g;
            }
            global[b] = g.Orthonormalized();
            var l = p >= 0 ? global[p].Inverse() * global[b] : global[b];
            local[b] = l.GetRotationQuaternion().Normalized();
        }
        return local;
    }

    /// <summary>a'dan b'ye en kisa yay.</summary>
    public static Quaternion Arc(Vector3 a, Vector3 b)
    {
        float d = a.Dot(b);
        if (d > 0.99999f) return Quaternion.Identity;
        if (d < -0.99999f)
        {
            var axis = a.Cross(Vector3.Right);
            if (axis.LengthSquared() < 1e-6f) axis = a.Cross(Vector3.Forward);
            return new Quaternion(axis.Normalized(), Mathf.Pi);
        }
        var c = a.Cross(b);
        return new Quaternion(c.X, c.Y, c.Z, 1f + d).Normalized();
    }

    /// <summary>Anahtarlardan klip kurar. Son anahtar null ise temel poza (idle'in ilk karesi) doner.</summary>
    public Animation Build(IList<Key> keys, float length, bool loop)
    {
        var anim = new Animation { Length = length, LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None };
        var solved = new List<(float t, Quaternion[] q, Vector3 hips)>();
        foreach (var k in keys) solved.Add((k.T, Solve(k), k.Hips ?? BasePos(_root)));
        for (int b = 0; b < _skel.GetBoneCount(); b++)
        {
            int tr = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(tr, new NodePath($"{_prefix}:{_skel.GetBoneName(b)}"));
            anim.TrackSetInterpolationType(tr, Animation.InterpolationType.Cubic);
            foreach (var (t, q, _) in solved) anim.RotationTrackInsertKey(tr, t, q[b]);
        }
        int hp = anim.AddTrack(Animation.TrackType.Position3D);
        anim.TrackSetPath(hp, new NodePath($"{_prefix}:{_skel.GetBoneName(_root)}"));
        anim.TrackSetInterpolationType(hp, Animation.InterpolationType.Cubic);
        foreach (var (t, _, h) in solved) anim.PositionTrackInsertKey(hp, t, h);
        return anim;
    }

    Key Stand(float t) => new(t); // temel poz: idle'in ilk karesi

    // ---------------- M1 kodla klipler ----------------

    /// <summary>Diz cokup ekme: sag diz yerde, sol ayak onde; sol el yere iki kez tohum bastirir. Dongulu.</summary>
    public Animation Plant()
    {
        float hipY = HipDrop + Thigh + 0.05f;               // sag uyluk dik, diz yerde
        // Sol uyluk: diz, kaval dik durunca ayak bilegi yere gelecek yukseklikte.
        float kneeDy = (Ankle + Shin) - (hipY - HipDrop);
        float sy = Mathf.Clamp(kneeDy / Thigh, -0.95f, 0.95f);
        var hips = new Vector3(0, hipY, -0.05f);
        Key Pose(float t, float handY) => new Key(t, hips)
            .D("Hips", 0, 1, 0.15f).D("Spine02", 0, 1, 0.45f).D("Spine01", 0, 1, 0.65f).D("Spine", 0, 1, 0.75f)
            .D("neck", 0, 1, 0.6f).D("Head", 0, 0.75f, 0.85f)
            .D("RightUpLeg", -0.05f, -1, 0.04f).D("RightLeg", 0, -0.08f, -1)
            .D("LeftUpLeg", 0.08f, sy, Mathf.Sqrt(1 - sy * sy)).D("LeftLeg", 0, -1, 0.05f)
            .D("LeftArm", 0.15f, -0.75f, 0.65f).D("LeftForeArm", -0.05f, handY, 0.7f)        // sol el yere uzanir
            .D("RightArm", -0.3f, -0.8f, 0.45f).D("RightForeArm", -0.1f, -0.25f, 1f);       // sag el Capa'yi tutar, dizde
        var keys = new List<Key> { Pose(0f, -0.55f), Pose(0.55f, -0.95f), Pose(0.85f, -0.45f), Pose(1.2f, -0.95f), Pose(1.6f, -0.5f), Pose(2.4f, -0.55f) };
        return Build(keys, 2.4f, true);
    }

    /// <summary>Yeme: sol el agza (vizore) iki kez gelir, bas hafif egilir. Tek sefer, idle'a doner.</summary>
    public Animation Eat()
    {
        Key Up(float t, float nod) => new Key(t).D("LeftArm", 0.1f, -0.45f, 0.9f).D("LeftForeArm", -0.45f, 0.8f, 0.35f).D("Head", 0, 1, nod).D("neck", 0, 1, nod * 0.5f);
        Key Down(float t) => new Key(t).D("LeftArm", 0.15f, -0.6f, 0.75f).D("LeftForeArm", -0.15f, 0.1f, 1f).D("Head", 0, 1, 0.12f);
        var keys = new List<Key> { Stand(0f), Up(0.45f, 0.1f), Up(0.75f, 0.32f), Down(1.05f), Up(1.4f, 0.1f), Up(1.7f, 0.32f), Down(2.0f), Stand(2.5f) };
        return Build(keys, 2.5f, false);
    }

    /// <summary>Uyuma: sirtustu yatar (bas -Z'de), gogus nefesle iner kalkar. Dongulu.</summary>
    public Animation Sleep()
    {
        var hips = new Vector3(0, 0.13f, 0);
        Key Lie(float t, float chest) => new Key(t, hips)
            .D("Hips", 0, 0.02f, -1).D("Spine02", 0, chest * 0.5f, -1).D("Spine01", 0, chest, -1).D("Spine", 0, chest, -1)
            .D("neck", 0, 0.3f, -1).D("Head", 0.08f, 0.25f, -1)
            .Both("UpLeg", 0.1f, 0.02f, 1).Both("Leg", 0.06f, 0.05f, 1)
            .Both("Arm", 0.3f, -0.05f, 1).Both("ForeArm", 0.1f, 0.25f, 1);
        var keys = new List<Key> { Lie(0f, 0.04f), Lie(1.8f, 0.14f), Lie(3.6f, 0.04f) };
        return Build(keys, 3.6f, true);
    }

    /// <summary>Yuzustu yatistan kalkis (getup_front gelene kadar yer tutucu): kollarla it, dizler, tek diz, ayakta.</summary>
    public Animation GetupFront()
    {
        float kneelY = HipDrop + Thigh + 0.05f;
        var keys = new List<Key>
        {
            new Key(0f, new Vector3(0, 0.15f, 0)).D("Hips", 0, 0, 1).D("Spine02", 0, -0.04f, 1).D("Spine01", 0, -0.02f, 1).D("Spine", 0, 0, 1)
                .D("neck", 0, 0.2f, 1).D("Head", 0, 0.4f, 1).Both("UpLeg", 0.08f, 0, -1).Both("Leg", 0.05f, 0.08f, -1)
                .Both("Arm", 0.85f, -0.3f, -0.15f).Both("ForeArm", 0.1f, -0.45f, 0.65f),
            new Key(0.55f, new Vector3(0, 0.3f, 0.02f)).D("Hips", 0, 0.3f, 1).D("Spine02", 0, 0.45f, 1).D("Spine01", 0, 0.55f, 1).D("Spine", 0, 0.6f, 1)
                .D("neck", 0, 0.6f, 1).D("Head", 0, 0.7f, 1).Both("UpLeg", 0.06f, -0.25f, -1).Both("Leg", 0.04f, 0, -1)
                .Both("Arm", 0.18f, -1, 0.1f).Both("ForeArm", 0.06f, -1, 0.12f),
            new Key(1.05f, new Vector3(0, kneelY, -0.08f)).D("Hips", 0, 1, 0.25f).D("Spine02", 0, 1, 0.35f).D("Spine01", 0, 1, 0.35f).D("Spine", 0, 1, 0.3f)
                .Both("UpLeg", 0.06f, -1, 0.05f).Both("Leg", 0, -0.1f, -1)
                .Both("Arm", 0.2f, -0.85f, 0.45f).Both("ForeArm", 0.05f, -0.5f, 0.85f),
            new Key(1.55f, new Vector3(0, kneelY + 0.02f, -0.05f)).D("Hips", 0, 1, 0.2f).D("Spine02", 0, 1, 0.45f).D("Spine01", 0, 1, 0.5f).D("Spine", 0, 1, 0.45f)
                .D("RightUpLeg", -0.05f, -1, 0.04f).D("RightLeg", 0, -0.08f, -1).D("LeftUpLeg", 0.08f, -0.1f, 1).D("LeftLeg", 0, -1, 0.05f)
                .D("LeftArm", 0.2f, -0.8f, 0.5f).D("LeftForeArm", 0, -0.4f, 0.9f).D("RightArm", -0.25f, -0.85f, 0.3f),
            new Key(2.0f, new Vector3(0, StandHips - 0.12f, 0)).D("Hips", 0, 1, 0.1f).D("Spine02", 0, 1, 0.3f).D("Spine01", 0, 1, 0.25f)
                .D("RightUpLeg", -0.06f, -0.9f, 0.3f).D("RightLeg", 0, -1, -0.3f).D("LeftUpLeg", 0.06f, -0.8f, 0.5f).D("LeftLeg", 0, -1, -0.25f),
            Stand(2.4f),
        };
        return Build(keys, 2.4f, false);
    }

    /// <summary>Sirtustu yatistan kalkis (getup_back gelene kadar yer tutucu): dogrul, dizleri cek, comel, ayakta.</summary>
    public Animation GetupBack()
    {
        var keys = new List<Key>
        {
            new Key(0f, new Vector3(0, 0.13f, 0)).D("Hips", 0, 0.02f, -1).D("Spine02", 0, 0.03f, -1).D("Spine01", 0, 0.04f, -1).D("Spine", 0, 0.04f, -1)
                .D("neck", 0, 0.3f, -1).D("Head", 0, 0.3f, -1).Both("UpLeg", 0.1f, 0.02f, 1).Both("Leg", 0.06f, 0.05f, 1)
                .Both("Arm", 0.35f, -0.05f, 1).Both("ForeArm", 0.12f, 0.2f, 1),
            new Key(0.6f, new Vector3(0, 0.13f, 0)).D("Hips", 0, 1, -0.35f).D("Spine02", 0, 1, -0.15f).D("Spine01", 0, 1, 0).D("Spine", 0, 1, 0.05f)
                .Both("UpLeg", 0.1f, 0.03f, 1).Both("Leg", 0.05f, 0.02f, 1)
                .Both("Arm", 0.3f, -0.8f, -0.55f).Both("ForeArm", 0.1f, -1, -0.15f),
            new Key(1.1f, new Vector3(0, 0.15f, 0)).D("Hips", 0, 1, 0.1f).D("Spine02", 0, 1, 0.35f).D("Spine01", 0, 1, 0.4f).D("Spine", 0, 1, 0.4f)
                .Both("UpLeg", 0.12f, 0.7f, 0.7f).Both("Leg", 0.02f, -0.9f, 0.35f)
                .Both("Arm", 0.2f, -0.5f, 0.85f).Both("ForeArm", 0.08f, -0.15f, 1),
            new Key(1.7f, new Vector3(0, 0.45f, 0.12f)).D("Hips", 0, 1, 0.45f).D("Spine02", 0, 1, 0.6f).D("Spine01", 0, 1, 0.55f).D("Spine", 0, 1, 0.45f)
                .Both("UpLeg", 0.14f, -0.3f, 1).Both("Leg", 0.02f, -1, -0.1f)
                .Both("Arm", 0.2f, -0.5f, 0.9f).Both("ForeArm", 0.05f, -0.2f, 1),
            new Key(2.2f, new Vector3(0, StandHips - 0.1f, 0.05f)).D("Hips", 0, 1, 0.15f).D("Spine02", 0, 1, 0.3f).D("Spine01", 0, 1, 0.25f)
                .Both("UpLeg", 0.08f, -0.85f, 0.4f).Both("Leg", 0, -1, -0.25f),
            Stand(2.6f),
        };
        return Build(keys, 2.6f, false);
    }
}
