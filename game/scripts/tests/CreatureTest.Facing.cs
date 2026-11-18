using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=creatures, yon (sahibin geri bildirimi: bazi hayvanlar yan yan kosuyor):
///  1) Her iskeletli turun modelinin onu (dinlenme duruslu iskelette bas - kuyruk; kuyruksuz cift ayaklida ayak ucu - bilek)
///     aktorun +Z ekseniyle 20 dereceden az aciyla ortusur. Aktorler (WildCreature, Zipzip, Gidak, Pofuduk) donusu +Z'yi
///     hiz yonune cevirerek verir; bu yuzden gövde basina fark rig.yaw ile tek yerde (creatures.json) duzeltilir.
///  2) Dolasan her yabani turde (WildCreature) hareket sirasinda aktorun +Z'si hiz yonune 20 dereceden az sapar
///     (donus ani degil yumusak oldugu icin ortanca aci olculur).
/// </summary>
public partial class CreatureTest
{
    const float MaxFaceDeg = 20f;

    /// <summary>Rolden iskelet kemigi: creatures.json rig.bones.</summary>
    static bool BoneRest(CreatureRig rig, string role, Node3D frame, out Vector3 at)
    {
        at = default;
        if (rig.Skeleton == null || !rig.Def.Rig!.Bones.TryGetValue(role, out var name)) return false;
        int i = PoseLibrary.FindBone(rig.Skeleton, name);
        if (i < 0) return false;
        at = frame.GlobalTransform.AffineInverse() * (rig.Skeleton.GlobalTransform * rig.Skeleton.GetBoneGlobalRest(i).Origin);
        return true;
    }

    /// <summary>Modelin onu, frame uzayinda yatay vektor; bulunamazsa null.</summary>
    static Vector3? RestFront(CreatureRig rig, Node3D frame)
    {
        Vector3 f;
        if (BoneRest(rig, "head", frame, out var head) && BoneRest(rig, "tail", frame, out var tail)) f = head - tail;
        else if (BoneRest(rig, "foot_l", frame, out var foot) && BoneRest(rig, "calf_l", frame, out var calf)) f = foot - calf;
        else if (BoneRest(rig, "foot_l", frame, out foot) && BoneRest(rig, "shin_l", frame, out var shin)) f = foot - shin;
        else
        {
            // Yedek (kuyrugu olmayan ya da ayak ucu bilegin altinda duran modeller: Zipzip, Pofuduk): sol-sag kemik cifti.
            // Model +Z'ye bakarken sol +X'tedir; on = (sol - sag) x Yukari.
            f = default;
            foreach (var pair in new[] { "thigh", "upperarm", "back", "front", "wing" })
                if (BoneRest(rig, pair + "_l", frame, out var l) && BoneRest(rig, pair + "_r", frame, out var r) && (l - r).Length() > 0.02f)
                {
                    f = (l - r).Cross(Vector3.Up);
                    break;
                }
            if (f == default) return null;
        }
        f.Y = 0;
        return f.Length() < 0.02f ? null : f.Normalized();
    }

    void CheckFacing(GameData data, PoseLibrary poses)
    {
        int n = 0;
        foreach (var def in data.Creatures.Creatures.Where(c => c.Rig != null))
        {
            var frame = new Node3D { Name = $"Facing_{def.Id}" };
            AddChild(frame);
            var rig = CreatureRig.Load(def, poses, frame);
            if (rig?.Skeleton == null) { frame.QueueFree(); continue; }
            var f = RestFront(rig, frame);
            if (f == null) { Fail(def.Id, "modelin on yonu bulunamadi (bas-kuyruk ya da sol-sag kemik cifti yok)"); frame.QueueFree(); continue; }
            n++;
            float deg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(f.Value.Dot(Vector3.Back), -1f, 1f))); // +Z = Godot Back
            GD.Print($"FACING {def.Id} yaw={def.Rig!.Yaw} on-sapma={deg:0}");
            if (deg > MaxFaceDeg) Fail(def.Id, $"modelin onu +Z'ye bakmiyor ({deg:0} derece; rig.yaw={def.Rig.Yaw}, dogrusu {def.Rig.Yaw + SignedYaw(f.Value):0})");
            frame.QueueFree();
        }
        if (n < 20) _fail.Add($"yon kontrolu {n} turde < 20");

        // Hareket: dolasan yabani turlerde hiz yonu ile aktor +Z'si.
        int moved = 0;
        foreach (var def in data.Creatures.Creatures.Where(c => c.Wild != null && c.Rig != null && c.Wild.WalkSpeedMult > 0))
        {
            var world = new FlatWorld { IsNight = def.Wild!.Active == "night" };
            var c = NewWild(def, poses, world, new Vector3(3000 + moved * 40f, 0, 3000));
            if (!c.Loaded) continue;
            var angles = new List<float>();
            var last = c.GlobalPosition;
            for (float t = 0; t < 25f; t += Dt)
            {
                c.Tick(Dt);
                var p = c.GlobalPosition; var v = p - last; v.Y = 0; last = p;
                if (v.Length() < 0.004f) continue;
                var fw = c.GlobalTransform.Basis.Z; fw.Y = 0;
                angles.Add(Mathf.RadToDeg(fw.Normalized().AngleTo(v.Normalized())));
            }
            c.QueueFree();
            if (angles.Count < 10) continue; // durgun tur (gunduzcu gece uyur vb.)
            moved++;
            angles.Sort();
            float med = angles[angles.Count / 2];
            GD.Print($"FACING {def.Id} hareket ortanca-sapma={med:0} ornek={angles.Count}");
            if (med > MaxFaceDeg) Fail(def.Id, $"hareket yonune donmuyor (ortanca sapma {med:0} derece)");
        }
        if (moved < 8) _fail.Add($"hareketli yabani tur {moved} < 8");
    }

    /// <summary>f vektorunun +Z'den isaretli sapmasi, rig.yaw'a eklenecek duzeltme (derece, Godot Y donusu).</summary>
    static float SignedYaw(Vector3 f) => Mathf.RadToDeg(Mathf.Atan2(f.X, f.Z)) * -1f;
}
