using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Director: yaratiklar. Poz kutuphanesinin tek ornegi (klip onbellegi; statik yok, TEKNIK §9), cayirdaki Zipzip
/// suruleri ve gorunus sinamasi:
///   --creatureview=normal|swollen [--clip=yuva]  Zipzip, Gidak, Pofuduk yan yana, sabit kamera (normal ve kabarmis).
///   --creatureview=both [--clip=yuva]            her tur normal ve kabarmis esiyle yan yana.
///   --creatureview=herd                          cayirdaki en yakin Zipzip surusune bakan kamera.
///   --creatureview=farm                          tarlaya yukaridan bakan kamera (--skip=night ile gece dalgasi).
///   --creatureview=gidak                         Gidak'i yandan izleyen kamera (--skip=night: tekme yayi).
///   --creatureview=kick                          kabarmis Gidak, kabarmis Zipzip'i citin ustunden atar (3 sn'de bir).
///   --creatureview=crop                          olgun Salgam tarlasina gelen gunduz surusu.
/// </summary>
public partial class Director
{
    PoseLibrary _poses;
    public PoseLibrary Poses => _poses ??= new PoseLibrary(Data);
    public ZipzipMeadow Meadow { get; private set; }

    void InitCreatures()
    {
        Meadow = new ZipzipMeadow(this) { Name = "ZipzipMeadow" };
        AddChild(Meadow);
        if (Main.Args.TryGetValue("creatureview", out var view))
        {
            if (view is "normal" or "swollen" or "both" or "kick") Meadow.ProcessMode = ProcessModeEnum.Disabled;
            GetTree().CreateTimer(view == "herd" ? 3.0 : 0.3).Timeout += () => CreatureView(view);
        }
    }

    void CreatureView(string view)
    {
        var cam = new Camera3D { Fov = 45f, Name = "CreatureCam" };
        AddChild(cam);
        Main.Args.TryGetValue("clip", out var clip);
        clip ??= "idle";
        if (view == "herd")
        {
            var h = Meadow.Herds.Where(x => !x.Leaving).OrderBy(x => x.Center.DistanceTo(Player.GlobalPosition)).FirstOrDefault();
            var c0 = h?.Center ?? OcakPos;
            var side = ((Player.GlobalPosition - c0) with { Y = 0 }).Normalized().Rotated(Vector3.Up, 1.2f);
            // Kamera suruyu izler: uyelerin ortasina yandan bakar.
            void Follow()
            {
                if (!IsInstanceValid(cam)) return;
                var live = h?.Members.Where(IsInstanceValid).ToList();
                var c = live is { Count: > 0 } ? live.Aggregate(Vector3.Zero, (s, z) => s + z.GlobalPosition) / live.Count : c0;
                cam.GlobalPosition = c + side * 6.5f + Vector3.Up * 2.2f;
                cam.LookAt(c + Vector3.Up * 0.35f);
            }
            Follow();
            GetTree().ProcessFrame += Follow;
            cam.MakeCurrent();
            GD.Print($"CREATUREVIEW herd at={c0.X:0.0},{c0.Z:0.0} members={h?.Members.Count ?? 0}");
            return;
        }
        if (view == "gidak")
        {
            // Gidak'i yandan izler (gece: tekme ve ucan Zipzip).
            void Track()
            {
                if (!IsInstanceValid(cam) || !Gidak.Visible) return;
                var g = Gidak.GlobalPosition;
                cam.GlobalPosition = g + new Vector3(9f, 5f, 3f);
                cam.LookAt(g + new Vector3(0, 1.5f, 0));
            }
            Track();
            GetTree().ProcessFrame += Track;
            cam.MakeCurrent();
            return;
        }
        if (view == "farm")
        {
            var c = FarmCenter();
            cam.GlobalPosition = c + new Vector3(9f, 7.5f, 11f);
            cam.LookAt(c + new Vector3(-2f, 0, -2f));
            cam.MakeCurrent();
            return;
        }
        if (view == "both") { CreaturePairs(cam, clip); return; }
        if (view == "kick") { KickDemo(cam); return; }
        if (view == "crop") { CropDemo(cam); return; }
        bool swollen = view == "swollen";
        var center = World.Ground(PodPos + new Vector3(-9f, 0, 9f));
        float gap = swollen ? 2.6f : 1.1f;
        var right = new Vector3(1, 0, 0);
        var camPos = center + new Vector3(0, swollen ? 2.4f : 1.0f, swollen ? 7.5f : 3.2f);
        Vector3 Spot(int i) => World.Ground(center + right * (i - 1) * gap);
        float Yaw(Vector3 p) => Mathf.RadToDeg(Mathf.Atan2(camPos.X - p.X, camPos.Z - p.Z));

        var z = new Zipzip(Data.Creature("zipzip"), this, 1f, 1f, swollen) { Display = true, Mode = ZipzipMode.Display, DisplaySlot = clip, Name = "ViewZipzip" };
        AddChild(z); z.GlobalPosition = Spot(0); z.RotationDegrees = new Vector3(0, Yaw(Spot(0)) - 25f, 0);
        var g = new Gidak(Data.Creature("gidak"), this) { Display = true, DisplaySlot = clip, Name = "ViewGidak" };
        AddChild(g); g.GlobalPosition = Spot(1); g.RotationDegrees = new Vector3(0, Yaw(Spot(1)) - 25f, 0);
        var p = new Pofuduk(this, Spot(2)) { Display = true, DisplaySlot = clip, Name = "ViewPofuduk" };
        AddChild(p); p.RotationDegrees = new Vector3(0, Yaw(Spot(2)) - 25f, 0);
        if (swollen) { g.Swell(true); p.Swell(true); }
        cam.GlobalPosition = camPos;
        cam.LookAt(center + Vector3.Up * (swollen ? 1.0f : 0.35f));
        cam.MakeCurrent();
        GD.Print($"CREATUREVIEW {view} clip={clip}");
    }

    /// <summary>
    /// Tekme sahnesi: kabarmis Gidak, citin icindeki kabarmis Zipzip'i citin ustunden atar (PLAN §1.4). Cit, yapi
    /// kitinin gercek Cit parcasi (1,3 m). Oyundaki tekme yolu (Gidak.Kick -> Zipzip.Launch) kullanilir; 3 sn'de bir.
    /// </summary>
    void KickDemo(Camera3D cam)
    {
        var center = World.Ground(PodPos + new Vector3(-9f, 0, 9f));
        for (int k = -1; k <= 1; k++)
        {
            var fence = Assets.Load("res://assets/models/build/wood/cit.glb");
            if (fence != null) { AddChild(fence); fence.GlobalPosition = World.Ground(center + new Vector3(k * 2f, 0, 0)); }
        }
        var g = new Gidak(Data.Creature("gidak"), this) { Display = true, DisplaySlot = "idle", Name = "KickGidak" };
        AddChild(g); g.GlobalPosition = World.Ground(center + new Vector3(0, 0, -4.4f)); g.Swell(true);
        var z = new Zipzip(Data.Creature("zipzip"), this, 10f, 1f, swollen: true) { Display = true, Mode = ZipzipMode.Display, DisplaySlot = "idle", Name = "KickZipzip" };
        AddChild(z);
        var start = World.Ground(center + new Vector3(0, 0, -2.1f));
        z.GlobalPosition = start; z.RotationDegrees = new Vector3(0, 180, 0);
        void Again()
        {
            if (!IsInstanceValid(z) || !IsInstanceValid(g)) return;
            if (!z.Airborne) { z.GlobalPosition = start; z.Hp = z.MaxHp; }
            g.KickNow(z);
            GetTree().CreateTimer(3.0).Timeout += Again;
        }
        GetTree().CreateTimer(1.5).Timeout += Again;
        // Egik yandan bakis: cit boyu ve yay birlikte gorunsun.
        cam.GlobalPosition = center + new Vector3(11.5f, 3.4f, -5.5f);
        cam.LookAt(center + new Vector3(0, 1.8f, 1.5f));
        cam.MakeCurrent();
        GD.Print("CREATUREVIEW kick");
    }

    /// <summary>
    /// Olgun ekine gelen gunduz surusu (GDD §8.1): 3 x 3 Salgam kurallar uzerinden olgunlasir (SkipToNight'taki yol),
    /// oyuncu kacma yaricapinin disinda bekler, tarladan 15 m uzakta bir suru kurulur; kamera suruyle tarlanin arasina bakar.
    /// </summary>
    void CropDemo(Camera3D cam)
    {
        var f = State.Farm;
        for (int x = 3; x < 6; x++) for (int y = -1; y < 2; y++) { f.Till(x, y); f.Plant(x, y, "turnip", State.Backpack); f.Water(x, y); }
        for (int i = 0; i < 100000 && f.MatureCount < 9; i++) f.Tick(1f, true);
        var c = FarmCenter();
        Player.GlobalPosition = World.Ground(c + new Vector3(0, 0, -30f)) + Vector3.Up * 0.3f;
        var h = Meadow.SpawnAt(3, World.Ground(c + new Vector3(13f, 0, 7f)));
        void Follow()
        {
            if (!IsInstanceValid(cam)) return;
            var live = h.Members.Where(IsInstanceValid).ToList();
            var hc = live.Count > 0 ? live.Aggregate(Vector3.Zero, (s, z) => s + z.GlobalPosition) / live.Count : h.Center;
            var mid = (hc + c) * 0.5f;
            cam.GlobalPosition = mid + new Vector3(-3.5f, 5.5f, 11f);
            cam.LookAt(mid + Vector3.Up * 0.3f);
        }
        Follow();
        GetTree().ProcessFrame += Follow;
        cam.MakeCurrent();
        GD.Print($"CREATUREVIEW crop mature={f.MatureCount}");
    }

    /// <summary>Her tur normal ve kabarmis yan yana: Zipzip, Gidak, Pofuduk (M1 teslimi "normal ve kabarmis").</summary>
    void CreaturePairs(Camera3D cam, string clip)
    {
        var center = World.Ground(PodPos + new Vector3(-9f, 0, 9f));
        // Normal ve kabarmis eslerin arasi kabarmis boyun yarisi kadar; turler arasi daha genis.
        float[] xs = { -5.4f, -3.6f, -0.8f, 1.2f, 3.9f, 5.8f };
        var camPos = center + new Vector3(0, 2.6f, 10.5f);
        int i = 0;
        foreach (bool sw in new[] { false, true })
        {
            var p = World.Ground(center + new Vector3(xs[i], 0, 0)); i++;
            var z = new Zipzip(Data.Creature("zipzip"), this, 1f, 1f, sw) { Display = true, Mode = ZipzipMode.Display, DisplaySlot = clip, Name = "ViewZipzip" + (sw ? "Swollen" : "") };
            AddChild(z); z.GlobalPosition = p; z.RotationDegrees = new Vector3(0, Mathf.RadToDeg(Mathf.Atan2(camPos.X - p.X, camPos.Z - p.Z)) - 20f, 0);
        }
        foreach (bool sw in new[] { false, true })
        {
            var p = World.Ground(center + new Vector3(xs[i], 0, 0)); i++;
            var g = new Gidak(Data.Creature("gidak"), this) { Display = true, DisplaySlot = clip, Name = "ViewGidak" + (sw ? "Swollen" : "") };
            AddChild(g); g.GlobalPosition = p; g.RotationDegrees = new Vector3(0, Mathf.RadToDeg(Mathf.Atan2(camPos.X - p.X, camPos.Z - p.Z)) - 20f, 0);
            if (sw) g.Swell(true);
        }
        foreach (bool sw in new[] { false, true })
        {
            var p = World.Ground(center + new Vector3(xs[i], 0, 0)); i++;
            var f = new Pofuduk(this, p) { Display = true, DisplaySlot = clip, Name = "ViewPofuduk" + (sw ? "Swollen" : "") };
            AddChild(f); f.RotationDegrees = new Vector3(0, Mathf.RadToDeg(Mathf.Atan2(camPos.X - p.X, camPos.Z - p.Z)) - 20f, 0);
            if (sw) f.Swell(true);
        }
        cam.GlobalPosition = camPos;
        cam.LookAt(center + Vector3.Up * 1.0f);
        cam.MakeCurrent();
        GD.Print($"CREATUREVIEW both clip={clip}");
    }
}
