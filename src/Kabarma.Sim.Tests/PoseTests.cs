using System.Numerics;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Poz kutuphanesi (URETIM §2.2 E.3, SANAT-SES §4): her plan 8 klip, egriler duzgun, turler planlarina bagli.</summary>
public class PoseTests
{
    static readonly GameData D = TestData.Load();

    [Fact]
    public void Every_plan_has_the_eight_clips_with_valid_keys()
    {
        Assert.NotEmpty(D.Poses.Plans);
        foreach (var (planId, plan) in D.Poses.Plans)
        {
            Assert.Contains(plan.MoveRole, plan.Roles);
            foreach (var slot in Poses.Slots)
            {
                Assert.True(plan.Clips.TryGetValue(slot, out var clip), $"{planId}: {slot} klibi yok");
                Assert.True(clip!.Length > 0, $"{planId}.{slot}: uzunluk");
                Assert.Equal(Poses.Loops(slot), clip.Loop);
                Assert.Contains(clip.Mirror, new[] { "none", "same", "phase" });
                foreach (var (role, keys) in clip.Tracks.Select(kv => (kv.Key, kv.Value)).Append(("move", clip.Move)))
                {
                    if (role != "move") Assert.True(plan.Roles.Contains(role), $"{planId}.{slot}: bilinmeyen rol {role}");
                    foreach (var k in keys)
                    {
                        Assert.Equal(4, k.Count);
                        Assert.True(k[0] >= 0 && (clip.Loop ? k[0] < clip.Length : k[0] <= clip.Length), $"{planId}.{slot}.{role}: t={k[0]} aralik disi");
                    }
                }
            }
        }
    }

    [Fact]
    public void Every_clip_moves_something()
    {
        foreach (var (planId, plan) in D.Poses.Plans)
            foreach (var (slot, clip) in plan.Clips)
            {
                var tracks = Poses.Expand(clip);
                float max = 0;
                for (float t = 0; t <= clip.Length; t += clip.Length / 24f)
                    foreach (var tr in tracks.Values)
                    {
                        var v = Poses.Sample(tr, t, clip.Length, clip.Loop);
                        max = MathF.Max(max, MathF.Max(MathF.Abs(v.X), MathF.Max(MathF.Abs(v.Y), MathF.Abs(v.Z))));
                    }
                Assert.True(max >= 5f, $"{planId}.{slot}: en buyuk donus {max:0.0} derece");
            }
    }

    [Fact]
    public void Sampler_passes_through_keys_and_loops_without_a_seam()
    {
        foreach (var plan in D.Poses.Plans.Values)
            foreach (var clip in plan.Clips.Values)
                foreach (var (role, tr) in Poses.Expand(clip))
                {
                    foreach (var (t, v) in tr.Keys)
                        Assert.True(Vector3.Distance(v, Poses.Sample(tr, t, clip.Length, clip.Loop)) < 0.01f, $"{role} t={t}");
                    if (!clip.Loop) continue;
                    var a = Poses.Sample(tr, clip.Length - 0.001f, clip.Length, true);
                    var b = Poses.Sample(tr, 0f, clip.Length, true);
                    Assert.True(Vector3.Distance(a, b) < 1.5f, $"{role}: dongu dikisi {Vector3.Distance(a, b):0.00} derece");
                }
    }

    [Fact]
    public void Phase_mirror_shifts_the_right_side_half_a_cycle()
    {
        var walk = D.Plan("bird").Clips["walk"];
        var tr = Poses.Expand(walk);
        for (float t = 0; t < walk.Length; t += 0.05f)
        {
            var l = Poses.Sample(tr["thigh_l"], t + walk.Length / 2, walk.Length, true);
            var r = Poses.Sample(tr["thigh_r"], t, walk.Length, true);
            Assert.True(Vector3.Distance(Poses.MirrorRot(l), r) < 0.01f);
        }
        // Acik yazilmis sag iz aynalamaya ezilmez (kosuda kanatlar birlikte cirpar).
        var run = Poses.Expand(D.Plan("bird").Clips["run"]);
        Assert.Equal(-45f, Poses.Sample(run["wing_r"], 0.09f, 0.36f, true).Z, 2);
    }

    [Fact]
    public void Every_rigged_creature_maps_to_its_plan()
    {
        var rigged = D.Creatures.Creatures.Where(c => c.Rig != null).ToList();
        Assert.Contains(rigged, c => c.Id == "gidak");
        Assert.Contains(rigged, c => c.Id == "zipzip");
        foreach (var c in rigged)
        {
            var rig = c.Rig!;
            Assert.True(D.Poses.Plans.ContainsKey(rig.Plan), $"{c.Id}: plan {rig.Plan}");
            var plan = D.Plan(rig.Plan);
            foreach (var role in rig.Bones.Keys.Concat(rig.RestFix.Keys).Concat(rig.Ragdoll.Keys))
                Assert.True(plan.Roles.Contains(role), $"{c.Id}: {role} rolu {rig.Plan} planinda yok");
            Assert.True(rig.Bones.ContainsKey(plan.MoveRole), $"{c.Id}: konum kemigi {plan.MoveRole} eslenmemis");
            foreach (var slot in rig.Clips.Keys.Concat(rig.Placeholder)) Assert.Contains(slot, Poses.Slots);
            foreach (var slot in Poses.Slots)
            {
                var src = rig.ClipSource(slot);
                Assert.True(src == "pose" || src.StartsWith("glb:"), $"{c.Id}.{slot}: {src}");
            }
            foreach (var fix in rig.RestFix.Values) Assert.Equal(3, fix.Count);
        }
    }

    [Fact]
    public void Zipzip_is_a_small_creature_and_its_walk_is_marked_placeholder()
    {
        // SANAT-SES §4: Zipzip kucuk sinif, en cok 5.000 ucgen; yuruyus hazir klipten gelmeli, poz yuruyusu yer tutucu.
        var z = D.Creature("zipzip").Rig!;
        Assert.Equal(5000, z.TriBudget);
        Assert.Equal("pose", z.ClipSource("walk"));
        Assert.Contains("walk", z.Placeholder);
    }
}
