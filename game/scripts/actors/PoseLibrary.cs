using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kodla yazilan poz kutuphanesi (URETIM §2.2 E.3, SANAT-SES §4). Egriler game/data/poses.json'da govde plani basina
/// bir kez yazilir; burada o plani kullanan her turun iskeletine aktarilir:
///  - Donusler karakter ekseninde (+X sol, +Y yukari, +Z ileri) verilir; kemigin dinlenme yonuyle eslenip yerel donuse
///    cevrilir (R_yerel = dinlenme * G^-1 * R * G). Boylece ayni egri sol/sag ve farkli iskeletlerde ayni hareketi yapar.
///  - Konum ofseti iskelet boyuyla olceklenir (kemik uzunluklarina gore aktarim).
///  - Tur basina dinlenme duzeltmesi (creatures.json restFix) her klibe eklenir.
/// Kurulan klipler "pose" adli ayri bir AnimationLibrary'ye girer; modelin kendi klipleri ezilmez.
/// Onbellek bu ornekte durur (statik yok, TEKNIK §9); Director.Creatures.cs bir tane tutar.
/// </summary>
public sealed class PoseLibrary
{
    public const string LibName = "pose";
    const float Fps = 30f;
    readonly GameData _data;
    readonly Dictionary<string, AnimationLibrary> _cache = new();
    public readonly Dictionary<string, string> Notes = new(); // tur -> eslenen roller (sinama ciktisi)

    public PoseLibrary(GameData data) { _data = data; }

    /// <summary>Veri (koddan govdeler icin: bodies.json).</summary>
    public GameData Data => _data;

    /// <summary>Iskeletteki kemik adini bulur: aynen, ':' -> '_' ile, ya da harf/rakam karsilastirmasiyla (ice aktarma adi degistirebilir).</summary>
    public static int FindBone(Skeleton3D skel, string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        int i = skel.FindBone(name);
        if (i >= 0) return i;
        i = skel.FindBone(name.Replace(":", "_"));
        if (i >= 0) return i;
        string Key(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var k = Key(name);
        for (int b = 0; b < skel.GetBoneCount(); b++) if (Key(skel.GetBoneName(b)) == k) return b;
        return -1;
    }

    /// <summary>node'un ancestor uzayindaki donusumu (agac disinda da calisir).</summary>
    public static Transform3D RelTo(Node3D node, Node3D ancestor)
    {
        var t = Transform3D.Identity;
        Node n = node;
        while (n != null && n != ancestor)
        {
            if (n is Node3D n3) t = n3.Transform * t;
            n = n.GetParent();
        }
        return t;
    }

    static Quaternion Euler(System.Numerics.Vector3 deg) =>
        Basis.FromEuler(new Vector3(Mathf.DegToRad(deg.X), Mathf.DegToRad(deg.Y), Mathf.DegToRad(deg.Z)), EulerOrder.Yxz).GetRotationQuaternion();

    /// <summary>
    /// Turun kodla kurulan klipleri (8 yuvanin hepsi; hangisinin kullanilacagini CreatureRig secer).
    /// frame: karakter cercevesi (+Z ileri) olan dugum; skeleton onun altinda olmali.
    /// </summary>
    public AnimationLibrary For(CreatureDef def, Skeleton3D skel, Node animRoot, Node3D frame)
    {
        if (_cache.TryGetValue(def.Id, out var cached)) return cached;
        var rig = def.Rig!;
        var plan = _data.Plan(rig.Plan);
        var lib = new AnimationLibrary();
        string path = animRoot.GetPathTo(skel).ToString();

        // Karakter -> iskelet ekseni
        var k = RelTo(skel, frame).Basis.Orthonormalized().GetRotationQuaternion();
        var kInv = k.Inverse();
        Quaternion ToSkel(Quaternion rc) => kInv * rc * k;
        var upS = (kInv * Vector3.Up).Normalized();
        float lo = float.MaxValue, hi = float.MinValue;
        for (int b = 0; b < skel.GetBoneCount(); b++)
        {
            float h = skel.GetBoneGlobalRest(b).Origin.Dot(upS);
            lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
        }
        float span = Mathf.Max(0.001f, hi - lo);

        var roleBone = new Dictionary<string, int>();
        foreach (var role in plan.Roles)
        {
            if (!rig.Bones.TryGetValue(role, out var bn)) continue;
            int bi = FindBone(skel, bn);
            if (bi >= 0) roleBone[role] = bi;
            else GD.PrintErr($"POSE {def.Id}: {role} -> '{bn}' iskelette yok");
        }
        Notes[def.Id] = $"{roleBone.Count}/{rig.Bones.Count} rol, iskelet boyu {span:0.000}";

        // Dinlenme duzeltmesi (sol yazilmissa sag aynalanir)
        var fix = new Dictionary<string, Quaternion>();
        foreach (var (role, v) in rig.RestFix)
        {
            var e = new System.Numerics.Vector3(v[0], v[1], v[2]);
            fix[role] = Euler(e);
            var other = Poses.MirrorRole(role);
            if (other != role && !rig.RestFix.ContainsKey(other)) fix[other] = Euler(Poses.MirrorRot(e));
        }

        int moveBone = roleBone.TryGetValue(plan.MoveRole, out var mb) ? mb : -1;
        foreach (var slot in Poses.Slots)
        {
            if (!plan.Clips.TryGetValue(slot, out var clip)) continue;
            var tracks = Poses.Expand(clip);
            var anim = new Animation { Length = clip.Length, LoopMode = clip.Loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None };
            int frames = Mathf.Max(1, Mathf.CeilToInt(clip.Length * Fps));
            var driven = new HashSet<int>();
            foreach (var (role, bi) in roleBone)
            {
                bool has = tracks.TryGetValue(role, out var tr);
                bool hasFix = fix.TryGetValue(role, out var fq);
                if (!has && !hasFix) continue;
                driven.Add(bi);
                var restQ = skel.GetBoneRest(bi).Basis.GetRotationQuaternion();
                var g = skel.GetBoneGlobalRest(bi).Basis.Orthonormalized().GetRotationQuaternion();
                var gInv = g.Inverse();
                int ti = anim.AddTrack(Animation.TrackType.Rotation3D);
                anim.TrackSetPath(ti, $"{path}:{skel.GetBoneName(bi)}");
                int n = has && tr!.Keys.Count > 1 ? frames : 0;
                for (int f = 0; f <= n; f++)
                {
                    float t = n == 0 ? 0f : Mathf.Min(clip.Length, f / Fps);
                    var rc = has ? Euler(Poses.Sample(tr!, t, clip.Length, clip.Loop)) : Quaternion.Identity;
                    if (hasFix) rc = rc * fq;
                    var local = restQ * (gInv * ToSkel(rc) * g);
                    anim.RotationTrackInsertKey(ti, t, local.Normalized());
                }
            }
            // Konum: moveRole kemigi, iskelet boyu biriminde.
            if (moveBone >= 0)
            {
                var move = Poses.ToTrack(clip.Move);
                var rest = skel.GetBoneRest(moveBone).Origin;
                int parent = skel.GetBoneParent(moveBone);
                var pInv = parent >= 0 ? skel.GetBoneGlobalRest(parent).Basis.Inverse() : Basis.Identity;
                int ti = anim.AddTrack(Animation.TrackType.Position3D);
                anim.TrackSetPath(ti, $"{path}:{skel.GetBoneName(moveBone)}");
                int n = move.Keys.Count > 1 ? frames : 0;
                for (int f = 0; f <= n; f++)
                {
                    float t = n == 0 ? 0f : Mathf.Min(clip.Length, f / Fps);
                    var o = Poses.Sample(move, t, clip.Length, clip.Loop);
                    var os = kInv * new Vector3(o.X, o.Y, o.Z) * span;
                    anim.PositionTrackInsertKey(ti, t, rest + pInv * os);
                }
                driven.Add(moveBone);
            }
            // Surulmeyen kemikler dinlenmede tutulur: onceki klipten (ornegin modelin kosusundan) poz kalmasin.
            for (int b = 0; b < skel.GetBoneCount(); b++)
            {
                if (driven.Contains(b)) continue;
                int ti = anim.AddTrack(Animation.TrackType.Rotation3D);
                anim.TrackSetPath(ti, $"{path}:{skel.GetBoneName(b)}");
                anim.RotationTrackInsertKey(ti, 0, skel.GetBoneRest(b).Basis.GetRotationQuaternion());
                if (b == moveBone) continue;
                int pi = anim.AddTrack(Animation.TrackType.Position3D);
                anim.TrackSetPath(pi, $"{path}:{skel.GetBoneName(b)}");
                anim.PositionTrackInsertKey(pi, 0, skel.GetBoneRest(b).Origin);
            }
            lib.AddAnimation(slot, anim);
        }
        _cache[def.Id] = lib;
        return lib;
    }
}

/// <summary>
/// Bir turun gorunusu: modeli yukler, ileri yonunu duzeltir, iskelete poz kutuphanesini ekler ve 8 klip yuvasini
/// (bekleme, yurume, kosu, iki saldiri, darbe alma, yeme, dinlenme) modelin kendi klibine ya da poz klibine baglar.
/// </summary>
public sealed class CreatureRig
{
    public CreatureDef Def { get; }
    public Node3D Model { get; private set; }
    public Node3D Yaw { get; private set; }
    public Skeleton3D Skeleton { get; private set; }
    public AnimationPlayer Player { get; private set; }
    public readonly Dictionary<string, string> SlotAnim = new();
    public string Slot { get; private set; }
    /// <summary>Koddan govde ise tanimi (Maya kesesi, yer tutucu isareti); Tripo modelinde null.</summary>
    public BodyDef Body { get; private set; }

    CreatureRig(CreatureDef def) { Def = def; }

    /// <summary>Modeli frame'in altina kurar. Model yoksa null (cagiran koddan kurulan yedege duser).</summary>
    public static CreatureRig Load(CreatureDef def, PoseLibrary poses, Node3D frame, string fallbackModel = null)
    {
        var rig = def.Rig;
        // "code:<kimlik>": koddan parca govde (bodies.json, CreatureBody); Tripo modeli gelene kadar yer tutucu.
        var body = rig != null && CreatureBody.IsCode(rig.Model) ? poses?.Data.Body(rig.Model[CreatureBody.Prefix.Length..]) : null;
        var model = rig == null ? null : CreatureBody.IsCode(rig.Model) ? (body != null ? CreatureBody.Build(body, def.Id) : null) : Assets.Load(rig.Model);
        bool fallback = false;
        if (model == null && fallbackModel != null) { model = Assets.Load(fallbackModel); fallback = true; }
        if (model == null) return null;
        var r = new CreatureRig(def);
        r.Yaw = new Node3D { Name = "Yaw", RotationDegrees = new Vector3(0, fallback ? 0 : rig?.Yaw ?? 0, 0) };
        frame.AddChild(r.Yaw);
        r.Yaw.AddChild(model);
        r.Model = model;
        r.Body = body;
        r.Skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        r.Player = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (r.Skeleton == null || rig == null || poses == null || fallback) return r;
        if (r.Player == null) { r.Player = new AnimationPlayer { Name = "AnimationPlayer" }; model.AddChild(r.Player); }
        var root = r.Player.GetNode(r.Player.RootNode);
        r.Player.AddAnimationLibrary(PoseLibrary.LibName, poses.For(def, r.Skeleton, root, frame));
        var own = r.Player.GetAnimationList().Where(n => !n.StartsWith(PoseLibrary.LibName + "/")).ToList();
        foreach (var slot in Poses.Slots)
        {
            var src = rig.ClipSource(slot);
            string anim = null;
            if (src.StartsWith("glb:"))
            {
                var want = src[4..].ToLowerInvariant();
                anim = own.FirstOrDefault(n => n.ToLowerInvariant() == want) ?? own.FirstOrDefault(n => n.ToLowerInvariant().Contains(want));
                if (anim == null) GD.PrintErr($"RIG {def.Id}: {slot} icin '{src}' modelde yok; poz klibi kullaniliyor");
                else if (Poses.Loops(slot)) r.Player.GetAnimation(anim).LoopMode = Animation.LoopModeEnum.Linear;
            }
            r.SlotAnim[slot] = anim ?? $"{PoseLibrary.LibName}/{slot}";
        }
        return r;
    }

    public bool Animated => Player != null && SlotAnim.Count > 0;

    /// <summary>Yuvayi oynatir; ayni yuva zaten oynuyorsa yalniz hizi gunceller (restart: bastan).</summary>
    public void Play(string slot, float blend = 0.15f, float speed = 1f, bool restart = false)
    {
        if (!Animated || !SlotAnim.TryGetValue(slot, out var anim)) return;
        Player.SpeedScale = speed;
        if (!restart && Slot == slot && Player.IsPlaying()) return;
        Slot = slot;
        Player.Play(anim, blend);
        if (restart) Player.Seek(0, true);
    }

    /// <summary>Sinama: yuvayi surekli oynatir; dongusuz klip bitince bastan alir.</summary>
    public void PlayRepeat(string slot)
    {
        if (Slot != slot) { Play(slot, 0.1f); return; }
        if (!Poses.Loops(slot) && Done) Play(slot, 0f, 1f, restart: true);
    }

    /// <summary>Dongusuz klip bitti mi (saldiri, darbe).</summary>
    public bool Done => !Animated || !Player.IsPlaying() || (Player.CurrentAnimationLength > 0 && Player.CurrentAnimationPosition >= Player.CurrentAnimationLength - 0.01);

    public void Seek(float t) { if (Animated) Player.Seek(t, true); }
}
