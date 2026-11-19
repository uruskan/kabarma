using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Insa sistemi sinamasi (res://scenes/build_test.tscn). Oyunun geri kalani olmadan: arazi + gokyuzu (World),
/// 28 parcanin yukleme/ucgen denetimi, sonra BuildSystem'in nisan-yuva API'si ile kendiliginden iki katli bir ev
/// (temeller, kapi kasasi, pencereli duvarlar, zemin, merdiven, besik cati, sundurma), cit, aciklik (span) denemesi
/// ve desteksiz kalan parcalarin dusmesi. Ekran goruntuleri progress/m1/'e yazilir, sonra cikar.
/// Calistir: Godot --path game --resolution 1600x900 res://scenes/build_test.tscn
/// Sinama olarak: Godot --headless --path game -- --test=build [--test_out=KLASOR] (basliksizda goruntu atlanir; tools/test.ps1).
/// </summary>
public partial class BuildTest : Node3D
{
    World _world;
    BuildSystem _bs;
    Camera3D _cam;
    float _y0;
    int _ok, _fail;
    readonly List<string> _log = new();
    string _outDir;

    public override async void _Ready()
    {
        // --test_out=KLASOR: sinama kosulari kanit klasorunu (progress/m1) degistirmesin diye baska yere yazar.
        _outDir = Main.Args.TryGetValue("test_out", out var outArg) ? outArg.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m1/";
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        _world = new World { Name = "World" };
        AddChild(_world);
        _cam = new Camera3D { Fov = 60f, Far = 600f };
        AddChild(_cam);
        _cam.MakeCurrent();

        _bs = new BuildSystem();
        AddChild(_bs);
        _bs.Init(this, p => _world.HeightAt(p.X, p.Z));
        if (_bs.Kit == null) { GD.PrintErr("BUILD kit yuklenemedi"); GetTree().Quit(1); return; }
        _bs.SetActive(true);
        _bs.Fell += (p, b) => Log($"FELL {p.Def.Id} span>{_bs.Kit.MaxSpan}");

        CheckKit();
        await Frames(2);

        try
        {
            await BuildHouse();
            await BuildGarden();
            await Shots();
            await SpanDemo();
            await AllShapes();
            await YapiTests(); // BuildTest.Yapi.cs (M2): yuk siniri, yukseltme, plan modu, Plan Defteri, cokme
        }
        catch (Exception e)
        {
            GD.PrintErr("BUILD test hata: " + e);
            _fail++;
        }
        Log($"BUILD RESULT placed={_ok} failed={_fail} pieces={_bs.Pieces.Count}");
        FileAccess.Open(_outDir + "build_test.log", FileAccess.ModeFlags.Write)?.StoreString(string.Join("\n", _log) + "\n");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    void Log(string s)
    {
        GD.Print(s);
        _log.Add(s);
    }

    // ---------------------------------------------------------------- denetim

    void CheckKit()
    {
        int bad = 0;
        foreach (var d in _bs.Kit.Pieces)
        {
            var scene = d.Scene;
            if (scene == null) { Log($"KIT {d.Id}: GLB YUKLENEMEDI"); bad++; continue; }
            var n = scene.Instantiate<Node3D>();
            int tris = 0, surfaces = 0;
            string mat = "";
            foreach (var mi in n.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    surfaces++;
                    var arr = mi.Mesh.SurfaceGetArrays(s);
                    var idx = arr[(int)Mesh.ArrayType.Index].AsInt32Array();
                    tris += idx.Length > 0 ? idx.Length / 3 : arr[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length / 3;
                    mat = mi.Mesh.SurfaceGetMaterial(s)?.ResourcePath ?? "(gomulu)";
                }
            bool inBudget = tris >= 200 && tris <= 1500;
            if (!inBudget) bad++;
            Log($"KIT {d.Id,-24} tris={tris,5} json={d.Tris,5} surfaces={surfaces} sockets={d.Sockets.Length,2} colliders={d.Colliders.Length} mat={mat}{(inBudget ? "" : "  BUTCE DISI")}");
            n.Free();
        }
        Log($"KIT CHECK pieces={_bs.Kit.Pieces.Count} problems={bad}");
        if (bad > 0) _fail++;
    }

    // ---------------------------------------------------------------- yardimcilar

    async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    async Task Physics() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

    /// <summary>Oyuncu gibi: parcayi sec, hedef noktaya nisan al, gerekirse R ile dondur, yerlestir.</summary>
    async Task<BuildPiece> Place(string id, Vector3 target, Vector3 from, Func<Transform3D, bool> want = null, bool show = false)
    {
        _bs.Select(id);
        for (int i = 0; i < 8; i++)
        {
            _bs.UpdateAim(from, target - from);
            var c = _bs.Current;
            if (c != null && c.Valid && (want == null || want(c.Xf))) break;
            _bs.Rotate();
        }
        var cur = _bs.Current;
        if (show) return null;
        var piece = _bs.TryPlace();
        if (piece == null)
        {
            _fail++;
            Log($"PLACE FAIL {id} @{target} mode={cur?.Mode} reason='{cur?.Reason}' xf={cur?.Xf.Origin}");
        }
        else
        {
            _ok++;
            _placedIds.Add(id);
            Log($"PLACE {id,-16} {cur.Mode,-6} at {Fmt(piece.GlobalPosition)} yaw={Mathf.RoundToInt(Mathf.RadToDeg(piece.GlobalRotation.Y))} span={piece.Span}");
        }
        await Physics();
        return piece;
    }

    static string Fmt(Vector3 v) => $"({v.X:0.00},{v.Y:0.00},{v.Z:0.00})";

    static Func<Transform3D, bool> Facing(Vector3 d) => xf => xf.Basis.Z.Normalized().Dot(d) > 0.9f;
    static Func<Transform3D, bool> RightTo(Vector3 d) => xf => xf.Basis.X.Normalized().Dot(d) > 0.9f;

    float H(float x, float z) => _world.HeightAt(x, z);

    /// <summary>Kenar ortasina duvar: ic taraftan yukaridan bakarak.</summary>
    Task<BuildPiece> Wall(string id, Vector3 mid, Vector3 outward, float level)
    {
        var target = new Vector3(mid.X, level, mid.Z) - outward * 0.45f;
        return Place(id, target, target + Vector3.Up * 6f - outward * 2f, Facing(outward));
    }

    Task<BuildPiece> Pillar(Vector3 corner, float level)
    {
        var inward = new Vector3(-Mathf.Sign(corner.X), 0, -Mathf.Sign(corner.Z)) * 0.25f;
        var target = new Vector3(corner.X, level, corner.Z) + inward;
        return Place("sutun", target, target + new Vector3(0, 6, 1));
    }

    /// <summary>Duvar ustunden iceri dogru zemin/cati.</summary>
    Task<BuildPiece> OnWallTop(string id, Vector3 mid, Vector3 outward, float top, Func<Transform3D, bool> want = null)
    {
        var target = new Vector3(mid.X, top, mid.Z);
        return Place(id, target, target + outward * 3f + Vector3.Up * 2f, want);
    }

    // ---------------------------------------------------------------- ev

    async Task BuildHouse()
    {
        // 1) ilk temel: arazide serbest (ground), sonra yuvalara oturan 5 temel (2x2 ev + 2 sundurma)
        var f0 = await Place("kare_temel", new Vector3(-1.5f, H(-1.5f, -1.5f), -1.5f), new Vector3(-1.5f, 8, 4));
        _y0 = f0?.GlobalPosition.Y ?? 0.35f;
        foreach (var c in new[] { new Vector2(1.5f, -1.5f), new Vector2(-1.5f, 1.5f), new Vector2(1.5f, 1.5f), new Vector2(-1.5f, 4.5f), new Vector2(1.5f, 4.5f) })
            await Place("kare_temel", new Vector3(c.X, H(c.X, c.Y), c.Y), new Vector3(c.X, 9, c.Y + 5));
        float y0 = _y0, y1 = _y0 + 3f, y2 = _y0 + 6f;

        // 2) zemin kat: kose sutunlari + 8 duvar (on: kapi kasasi + pencereli)
        foreach (var c in Corners) await Pillar(c, y0);
        await GroundStorey(y0, upper: false);
        await Shot("build_step1_walls.png", new Vector3(13, 7.5f, 14), new Vector3(0, 1.8f, 0));

        // 3) ust kat zeminleri (duvar ustunden) + merdiven (sundurma hucresinde, eve dogru yukselir)
        await OnWallTop("kare_zemin", new Vector3(-1.5f, 0, -3), Vector3.Forward, y1);
        await OnWallTop("kare_zemin", new Vector3(1.5f, 0, -3), Vector3.Forward, y1);
        await OnWallTop("kare_zemin", new Vector3(-1.5f, 0, 3), Vector3.Back, y1);
        await OnWallTop("kare_zemin", new Vector3(1.5f, 0, 3), Vector3.Back, y1);
        var st = new Vector3(1.5f, y0, 4.5f);
        await Place("merdiven", st, st + new Vector3(0, 6, 3), Facing(Vector3.Back));

        // 4) sundurma: parmaklik, sutunlar, kirisler, duz cati
        await Wall("parmaklik", new Vector3(-3, 0, 4.5f), Vector3.Left, y0);
        await Pillar(new Vector3(-3, 0, 6), y0);
        var p2 = new Vector3(0, y0, 6) + new Vector3(-0.25f, 0, -0.25f);
        await Place("sutun", p2, p2 + new Vector3(0, 6, 1));
        var pt = new Vector3(-3, y1, 6);
        await Place("kiris", pt, pt + new Vector3(-3, 2, 0.4f), RightTo(Vector3.Right) );
        await Place("kiris", pt, pt + new Vector3(0, 2, 3), xf => Mathf.Abs(xf.Basis.X.Dot(Vector3.Back)) > 0.9f);
        await OnWallTop("duz_cati", new Vector3(-1.5f, 0, 6), Vector3.Back, y1);
        await Shot("build_step2_floor_stairs.png", new Vector3(10, 9.5f, 13), new Vector3(0, 2.5f, 1.5f));

        // 5) ust kat: kose sutunlari + 8 duvar (merdivenin vardigi yerde kapi kasasi)
        foreach (var c in Corners) await Pillar(c, y1);
        await GroundStorey(y1, upper: true);

        // 6) besik cati: alin ucgenleri, egimli catilar (on ve arka), mahya kapaklari
        foreach (var z in new[] { -1.5f, 1.5f })
            foreach (var x in new[] { -3f, 3f })
            {
                var t = new Vector3(x, y2, z);
                var ridgeDir = new Vector3(0, 0, -Mathf.Sign(z));
                await Place("ucgen_duvar", t, t + new Vector3(Mathf.Sign(x) * 3, 2, 0), RightTo(ridgeDir));
            }
        foreach (var x in new[] { -1.5f, 1.5f })
        {
            await OnWallTop("egimli_cati", new Vector3(x, 0, 3), Vector3.Back, y2, Facing(Vector3.Back));
            await OnWallTop("egimli_cati", new Vector3(x, 0, -3), Vector3.Forward, y2, Facing(Vector3.Forward));
        }
        foreach (var x in new[] { -1.5f, 1.5f })
        {
            var t = new Vector3(x, y2 + 3.4f, 0);
            await Place("cati_tepesi", t, t + new Vector3(0, 3, 4));
        }
    }

    static readonly Vector3[] Corners = { new(-3, 0, -3), new(3, 0, -3), new(-3, 0, 3), new(3, 0, 3) };

    async Task GroundStorey(float level, bool upper)
    {
        await Wall("duvar", new Vector3(-1.5f, 0, -3), Vector3.Forward, level);
        await Wall("duvar", new Vector3(1.5f, 0, -3), Vector3.Forward, level);
        await Wall(upper ? "pencereli_duvar" : "duvar", new Vector3(-3, 0, -1.5f), Vector3.Left, level);
        await Wall(upper ? "duvar" : "pencereli_duvar", new Vector3(-3, 0, 1.5f), Vector3.Left, level);
        await Wall(upper ? "duvar" : "pencereli_duvar", new Vector3(3, 0, -1.5f), Vector3.Right, level);
        await Wall(upper ? "pencereli_duvar" : "duvar", new Vector3(3, 0, 1.5f), Vector3.Right, level);
        await Wall(upper ? "pencereli_duvar" : "kapi_kasasi", new Vector3(-1.5f, 0, 3), Vector3.Back, level);
        await Wall(upper ? "kapi_kasasi" : "pencereli_duvar", new Vector3(1.5f, 0, 3), Vector3.Back, level);
    }

    // ---------------------------------------------------------------- bahce citi

    async Task BuildGarden()
    {
        float z = 10.5f;
        await Place("cit", new Vector3(-4.5f, H(-4.5f, z), z), new Vector3(-4.5f, 8, z + 4));
        await Place("cit", new Vector3(-1.5f, H(-1.5f, z), z), new Vector3(-1.5f, 8, z + 4));
        await Place("bahce_kapisi", new Vector3(1.5f, H(1.5f, z), z), new Vector3(1.5f, 8, z + 4));
        await Place("cit", new Vector3(4.5f, H(4.5f, z), z), new Vector3(4.5f, 8, z + 4));
    }

    // ---------------------------------------------------------------- ekran goruntuleri

    async Task Shot(string file, Vector3 camPos, Vector3 lookAt)
    {
        _bs.SetActive(false);
        _cam.GlobalPosition = camPos;
        _cam.LookAt(lookAt);
        await Frames(6);
        SaveShot(file);
        _bs.SetActive(true);
    }

    /// <summary>Ekran goruntusunu yazar. Basliksiz kosuda goruntu yoktur: not dusulur, sinama ayni adimlarla surer.</summary>
    void SaveShot(string file)
    {
        if (DisplayServer.GetName() == "headless") { Log($"SHOT {file} (basliksiz: atlandi)"); return; }
        GetViewport().GetTexture().GetImage().SavePng(_outDir + file);
        Log($"SHOT {file}");
    }

    async Task Shots()
    {
        // olcek icin oyuncu modeli (1,6 m) ve kova
        var pl = Assets.Load("res://assets/models/player/player.glb");
        if (pl != null)
        {
            AddChild(pl);
            pl.GlobalPosition = new Vector3(-1.2f, _y0, 7.6f);
            pl.RotationDegrees = new Vector3(0, 20, 0);
            var anim = pl.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
            var idle = anim?.GetAnimationList().FirstOrDefault(a => a.ToLower().Contains("idle"));
            if (idle != null) anim.Play(idle);
        }
        var kova = Assets.Load("res://assets/models/kova/kova.glb");
        if (kova != null)
        {
            AddChild(kova);
            kova.GlobalPosition = new Vector3(-2.6f, _y0, 5.4f);
        }
        await Shot("build_house_front.png", new Vector3(11, 5.5f, 19), new Vector3(0, 3.6f, 1));
        await Shot("build_house_back.png", new Vector3(-13, 8, -12), new Vector3(0, 4, 0));
        await Shot("build_house_closeup.png", new Vector3(2.2f, 2.4f, 9.6f), new Vector3(-1.0f, 1.7f, 4.6f));
        await Shot("build_house_far.png", new Vector3(26, 11, 30), new Vector3(0, 3, 2));
    }

    // ---------------------------------------------------------------- aciklik ve dusme

    async Task SpanDemo()
    {
        float x0 = 16f;
        var f = await Place("kare_temel", new Vector3(x0, H(x0, 0), 0), new Vector3(x0, 9, 5));
        if (f == null) return;
        float y = f.GlobalPosition.Y;
        var wall = await Wall("duvar", new Vector3(x0 + 1.5f, 0, 0), Vector3.Right, y);
        await OnWallTop("kare_zemin", new Vector3(x0 + 1.5f, 0, 0), Vector3.Left, y + 3);
        for (int i = 1; i <= 2; i++)
        {
            var t = new Vector3(x0 + 1.5f + 3f * i, y + 3, 0);
            await Place("kare_zemin", t, t + new Vector3(-3, 2, 0.5f));
        }
        // 4. parca acikligi asar: gecersiz (kirmizi) hayalet
        var t4 = new Vector3(x0 + 1.5f + 9f - 0.35f, y + 3, 0);
        _bs.Select("kare_zemin");
        _bs.UpdateAim(t4 + new Vector3(-3, 2, 0.5f), new Vector3(3, -2, -0.5f));
        var c = _bs.Current;
        Log($"SPAN 4th floor: valid={c?.Valid} reason='{c?.Reason}' span={c?.Span}");
        if (c == null || c.Valid || !c.Reason.StartsWith("açıklık")) _fail++;
        await ShotGhost("build_ghost_invalid.png", new Vector3(x0 + 9, y + 9, 14), new Vector3(x0 + 6, y + 2, 0));
        // gecerli hayalet: temelin diger kenarina duvar
        var tw = new Vector3(x0 - 1.5f + 0.45f, y, 0);
        _bs.Select("pencereli_duvar");
        _bs.UpdateAim(tw + new Vector3(2, 6, 0), new Vector3(-2, -6, 0));
        Log($"GHOST wall: valid={_bs.Current?.Valid} mode={_bs.Current?.Mode}");
        await ShotGhost("build_ghost_valid.png", new Vector3(x0 - 6, y + 5, 9), new Vector3(x0, y + 1.5f, 0));
        // duvari sok: ustundeki 3 zemin desteksiz kalir ve duser
        int before = _bs.Pieces.Count;
        _bs.Remove(wall);
        Log($"REMOVE wall: pieces {before} -> {_bs.Pieces.Count}");
        if (before - _bs.Pieces.Count != 4) _fail++;
        _bs.SetActive(false);
        _cam.GlobalPosition = new Vector3(x0 + 6, y + 6, 16);
        _cam.LookAt(new Vector3(x0 + 5, y + 1.5f, 0));
        for (int i = 0; i < 24; i++) await Physics();
        await Frames(2);
        SaveShot("build_fall.png");
    }

    // ---------------------------------------------------------------- 26 seklin hepsi (ICERIK §10.1)

    readonly HashSet<string> _placedIds = new();

    /// <summary>Aday nisanlardan gecerli hayalet veren ilkinde yerlestirir (hicbiri gecerli degilse sonuncuyu dener: basarisizlik yazilir).</summary>
    async Task<BuildPiece> PlaceFirstValid(string id, (Vector3 target, Vector3 from)[] tries)
    {
        foreach (var (target, from) in tries)
        {
            _bs.Select(id);
            for (int i = 0; i < 8; i++)
            {
                _bs.UpdateAim(from, target - from);
                if (_bs.Current != null && _bs.Current.Valid) return await Place(id, target, from);
                _bs.Rotate();
            }
            Log($"TRY {id} @{target}: {_bs.Current?.Reason}");
        }
        var last = tries[^1];
        return await Place(id, last.target, last.from);
    }

    /// <summary>
    /// Evde ve citte kullanilmayan 13 parca (11 sekil; Makine Platformu 3 boy): her biri oyuncu gibi nisanla oturtulur.
    /// Sonunda kitteki 28 parcanin her biri en az bir kez kurulmus olmali (URETIM §6 M1: 26 sekil kitte ve kurulabilir).
    /// </summary>
    async Task AllShapes()
    {
        _bs.SetActive(true);
        float x0 = -16f, z0 = -2f;
        // Temel: yarim duvar, alcak duvar, rampa, ucgen zemin ve cati kosesi buna oturur.
        var f = await Place("kare_temel", new Vector3(x0, H(x0, z0), z0), new Vector3(x0, 9, z0 + 5));
        if (f != null)
        {
            float y = f.GlobalPosition.Y;
            var c = new Vector3(x0, 0, z0);
            await Wall("yarim_duvar", c + new Vector3(0, 0, -1.5f), Vector3.Forward, y);
            await Wall("alcak_duvar", c + new Vector3(1.5f, 0, 0), Vector3.Right, y);
            var wall = await Wall("duvar", c + new Vector3(0, 0, 1.5f), Vector3.Back, y);
            if (wall != null) await OnWallTop("cati_kosesi", c + new Vector3(0, 0, 1.5f), Vector3.Back, y + 3f);
            var cell = new Vector3(x0, y, z0);
            await PlaceFirstValid("rampa", new[] { (cell, cell + new Vector3(-3, 6, 0)), (cell, cell + new Vector3(3, 6, 0)), (cell, cell + new Vector3(0, 6, -3)) });
            // ucgen zemin: temelin bati kenarina, kenar kenara
            var e = new Vector3(x0 - 1.5f - 0.6f, y, z0);
            await Place("ucgen_zemin", e, e + new Vector3(-3, 3, 0.5f));
        }
        // Araziye serbest oturanlar
        await Place("ucgen_temel", new Vector3(x0 - 7f, H(x0 - 7f, z0), z0), new Vector3(x0 - 7f, 9, z0 + 5));
        await Place("kazik_temel", new Vector3(x0, H(x0, z0 - 8f), z0 - 8f), new Vector3(x0, 9, z0 - 3f));
        float sx = x0 - 7f, sz = z0 - 9f;
        var sur = await Place("sur_duvari", new Vector3(sx, H(sx, sz), sz), new Vector3(sx, 8, sz + 4));
        if (sur != null) await Place("sur_kapisi", new Vector3(sx + 3f, H(sx + 3f, sz), sz), new Vector3(sx + 3f, 8, sz + 4));
        // Kule tabani genis ve duz yer ister: birkac aday noktadan ilk uygununa (arazi sinanmiyor, oturma sinaniyor).
        var spots = new[] { new Vector2(x0 - 10f, z0 + 4f), new Vector2(x0 - 14f, z0), new Vector2(x0 + 2f, z0 + 9f), new Vector2(x0 - 8f, z0 - 16f), new Vector2(x0 - 22f, z0 + 4f) };
        await PlaceFirstValid("kule_tabani", spots.Select(s => (new Vector3(s.X, H(s.X, s.Y), s.Y), new Vector3(s.X, 10, s.Y + 6))).ToArray());
        await Place("makine_platformu_kucuk", new Vector3(x0 - 14f, H(x0 - 14f, z0 - 8f), z0 - 8f), new Vector3(x0 - 14f, 10, z0 - 2f));
        await Place("makine_platformu_orta", new Vector3(x0 - 4f, H(x0 - 4f, z0 + 10f), z0 + 10f), new Vector3(x0 - 4f, 12, z0 + 17f));
        await Place("makine_platformu_buyuk", new Vector3(x0 - 16f, H(x0 - 16f, z0 + 14f), z0 + 14f), new Vector3(x0 - 16f, 14, z0 + 23f));
        _bs.SetActive(false);
        _cam.GlobalPosition = new Vector3(x0 + 10, 14, z0 + 22);
        _cam.LookAt(new Vector3(x0 - 8, 1, z0));
        await Frames(6);
        SaveShot("build_all_shapes.png");
        var missing = _bs.Kit.Pieces.Select(p => p.Id).Where(id => !_placedIds.Contains(id) && _bs.Data.Build.SetMaterialOf(id) == null).ToList(); // Cam seti parcalari KaleTest'te (M4)
        Log($"SHAPES placed={_placedIds.Count}/{_bs.Kit.Pieces.Count}{(missing.Count > 0 ? " missing=" + string.Join(",", missing) : "")}");
        if (missing.Count > 0) _fail++;
    }

    async Task ShotGhost(string file, Vector3 camPos, Vector3 lookAt)
    {
        _cam.GlobalPosition = camPos;
        _cam.LookAt(lookAt);
        await Frames(6);
        SaveShot(file);
    }
}
