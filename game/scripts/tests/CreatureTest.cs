using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=creatures: yaratik gorunusu sinamasi (M1 "Gidak ve Zipzip, normal ve kabarmis", M0 denemesi 4).
/// Her iskeletli tur icin, oyunla ayni yoldan (CreatureRig + PoseLibrary + Swellable):
///  - model ve iskelet var, kemik eslemesinin hepsi bulunuyor;
///  - 8 klip yuvasinin hepsi bir klibe bagli; poz klipleri kemikleri gercekten donduruyor, dongu dikissiz;
///  - ucgen sayisi sinif butcesinde (Zipzip <= 5.000);
///  - butun yuzeyler ortak sisme shader'inda, sisme sekansi 1'e variyor;
///  - ileri yon duzeltmesi sonrasi sol kemikler +X'te (karakterin solu).
/// Cikis: RESULT ok/fail, kod 0/1.
/// </summary>
public partial class CreatureTest : Node3D
{
    readonly List<string> _fail = new();
    int _clips, _posed;

    public override void _Ready() => CallDeferred(nameof(Run));

    void Run()
    {
        var data = GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        var poses = new PoseLibrary(data);
        var species = data.Creatures.Creatures.Where(c => c.Rig != null).ToList();
        foreach (var def in species) Check(def, poses);
        if (!species.Any(s => s.Id == "zipzip") || !species.Any(s => s.Id == "gidak")) _fail.Add("zipzip ya da gidak iskeletsiz");
        string res = _fail.Count == 0 ? "ok" : "fail";
        GD.Print($"RESULT {res} creatures={species.Count} clips={_clips} posed={_posed}" + (_fail.Count > 0 ? " :: " + string.Join(" | ", _fail) : ""));
        GetTree().Quit(_fail.Count == 0 ? 0 : 1);
    }

    void Fail(string id, string msg) { _fail.Add($"{id}: {msg}"); GD.Print($"CREATURE {id} FAIL {msg}"); }

    void Check(CreatureDef def, PoseLibrary poses)
    {
        var frame = new Node3D { Name = "Frame_" + def.Id };
        AddChild(frame);
        var rig = CreatureRig.Load(def, poses, frame);
        if (rig == null) { Fail(def.Id, $"model yuklenemedi {def.Rig!.Model}"); return; }
        var skel = rig.Skeleton;
        if (skel == null) { Fail(def.Id, "iskelet yok"); return; }
        int mapped = def.Rig!.Bones.Count(kv => PoseLibrary.FindBone(skel, kv.Value) >= 0);
        if (mapped != def.Rig.Bones.Count) Fail(def.Id, $"kemik eslemesi {mapped}/{def.Rig.Bones.Count}");

        // Ucgen butcesi (SANAT-SES §4)
        int tris = 0;
        foreach (var mi in rig.Model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var arr = mi.Mesh.SurfaceGetArrays(s);
                var idx = arr[(int)Mesh.ArrayType.Index].AsInt32Array();
                tris += idx.Length > 0 ? idx.Length / 3 : arr[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length / 3;
            }
        if (def.Rig.TriBudget > 0 && tris > def.Rig.TriBudget) Fail(def.Id, $"ucgen {tris} > {def.Rig.TriBudget}");

        // Sol / sag: karakterin solu +X (ileri yon duzeltmesi ve kemik adlari tutarli mi)
        var skelXf = PoseLibrary.RelTo(skel, frame);
        int sidePairs = 0;
        foreach (var (role, bone) in def.Rig.Bones)
        {
            if (!role.EndsWith("_l")) continue;
            if (!def.Rig.Bones.TryGetValue(Poses.MirrorRole(role), out var other)) continue;
            int a = PoseLibrary.FindBone(skel, bone), b = PoseLibrary.FindBone(skel, other);
            if (a < 0 || b < 0) continue;
            float xl = (skelXf * skel.GetBoneGlobalRest(a).Origin).X, xr = (skelXf * skel.GetBoneGlobalRest(b).Origin).X;
            sidePairs++;
            if (xl <= xr) Fail(def.Id, $"{role} solda degil (x {xl:0.000} <= {xr:0.000})");
        }

        // Klipler
        var pl = rig.Player;
        var srcs = new List<string>();
        foreach (var slot in Poses.Slots)
        {
            if (!rig.SlotAnim.TryGetValue(slot, out var anim) || pl == null || !pl.HasAnimation(anim)) { Fail(def.Id, $"{slot} klibi yok"); continue; }
            _clips++;
            bool isPose = anim.StartsWith(PoseLibrary.LibName + "/");
            srcs.Add($"{slot}={(isPose ? "poz" : anim)}{(def.Rig.Placeholder.Contains(slot) ? "*" : "")}");
            var a = pl.GetAnimation(anim);
            float max = 0;
            Quaternion[] first = null, last = null;
            int steps = 12;
            for (int i = 0; i <= steps; i++)
            {
                float len = (float)a.Length;
                float t = len * i / steps;
                if (i == steps) t = len - 0.001f;
                pl.Play(anim, 0); pl.Seek(t, true);
                var cur = Enumerable.Range(0, skel.GetBoneCount()).Select(skel.GetBonePoseRotation).ToArray();
                if (i == 0) first = cur;
                if (i == steps) last = cur;
                for (int bi = 0; bi < cur.Length; bi++)
                    max = Mathf.Max(max, Mathf.RadToDeg(skel.GetBoneRest(bi).Basis.GetRotationQuaternion().AngleTo(cur[bi])));
            }
            if (max < 5f) Fail(def.Id, $"{slot} kemik oynatmiyor (en cok {max:0.0} derece)");
            if (isPose)
            {
                _posed++;
                if (Poses.Loops(slot) && first != null && last != null)
                {
                    float seam = first.Zip(last, (q1, q2) => Mathf.RadToDeg(q1.AngleTo(q2))).Max();
                    if (seam > 4f) Fail(def.Id, $"{slot} dongu dikisi {seam:0.0} derece");
                }
            }
        }

        // Ortak sisme shader'i (SANAT-SES §4): butun yuzeyler, sisme sekansi
        var sw = new Swellable(rig.Model, def.Swell);
        int surfaces = 0, shared = 0;
        foreach (var mi in rig.Model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++) { surfaces++; if (Swellable.UsesShader(mi.GetSurfaceOverrideMaterial(s))) shared++; }
        if (surfaces == 0 || shared != surfaces) Fail(def.Id, $"sisme shader'i {shared}/{surfaces} yuzeyde");
        sw.Swell(true);
        for (int i = 0; i < 60; i++) sw.Tick(def.Swell.Seconds / 30f);
        if (Mathf.Abs(sw.Amount - 1f) > 0.02f) Fail(def.Id, $"sisme {sw.Amount:0.00}");
        sw.Swell(false);
        for (int i = 0; i < 60; i++) sw.Tick(def.Swell.Seconds / 30f);
        if (sw.Amount > 0.02f) Fail(def.Id, $"sonme {sw.Amount:0.00}");

        GD.Print($"CREATURE {def.Id} plan={def.Rig.Plan} bones={skel.GetBoneCount()} mapped={mapped}/{def.Rig.Bones.Count} tris={tris}/{def.Rig.TriBudget} sides={sidePairs} shader={shared}/{surfaces} {poses.Notes.GetValueOrDefault(def.Id)} clips: {string.Join(" ", srcs)}");
    }
}
