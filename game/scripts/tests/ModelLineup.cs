using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Model vitrini (gorsel-model akisi): oyundaki her modeli oyunun kurdugu yolla (sinifiyla ya da Assets.Load ile) ayni
/// isikta, yaninda oyuncu modeliyle (SANAT-SES §3: 1,6 m) tek tek cizer. Once/sonra karsilastirmasi icin.
///   --test=lineup [--lineup_tag=once] [--lineup_only=kapsul,ocak] [--lineup_size=1280x720]
/// Cikti: progress/gorsel/model/&lt;tag&gt;/&lt;id&gt;.png ve sheet.png (6 sutunluk temas foyu); "LINEUP RESULT ok n=..".
/// Pencereli calisir (basliksiz cizim yok).
/// </summary>
public partial class ModelLineup : Node3D
{
    /// <summary>Vitrin satiri: kimlik, kurucu, modelin on yonu (Tripo +X, kit ve sinif +Z), beklenecek kare.</summary>
    sealed record Entry(string Id, Func<Node3D> Make, Vector3 Front, int Frames = 10, Action<Node3D> After = null);

    const string M = "res://assets/models/";
    static readonly Vector3 PX = Vector3.Right, PZ = Vector3.Back;

    readonly Queue<Entry> _queue = new();
    readonly List<(string id, Image img)> _shots = new();
    Camera3D _cam;
    Node3D _current, _scale;
    Entry _entry;
    int _frame;
    string _outDir;
    GameData _data;

    public override void _Ready()
    {
        string tag = Main.Args.TryGetValue("lineup_tag", out var t) ? t : "once";
        _outDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/gorsel/model/" + tag;
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        var size = new Vector2I(1280, 720);
        if (Main.Args.TryGetValue("lineup_size", out var s) && s.Split('x') is { Length: 2 } wh) size = new Vector2I(int.Parse(wh[0]), int.Parse(wh[1]));
        DisplayServer.WindowSetSize(size);
        GetViewport().Scaling3DScale = 1f;
        _data = GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        BuildStudio();
        var only = Main.Args.TryGetValue("lineup_only", out var o) ? o.Split(',').ToHashSet() : null;
        foreach (var e in Entries())
            if (only == null || only.Contains(e.Id)) _queue.Enqueue(e);
        _scale = Assets.Load(M + "player/player.glb");
        if (_scale != null) AddChild(_scale);
    }

    IEnumerable<Entry> Entries()
    {
        PlaceableDef P(string id) => _data.Tool.Placeables.First(p => p.Id == id);
        Func<Node3D> G(string path) => () => Assets.Load(M + path);
        // Uzay kapsulu ve us
        yield return new("kapsul", () => new Pod(), PZ);
        yield return new("kapsul_rampa", () => new Pod(), PZ, 90, n => ((Pod)n).BecomeRamp());
        yield return new("ocak", () => new Ocak(), PX, 30);
        yield return new("rampa_pisti", G("props/rampa_pisti.glb"), PZ);
        yield return new("kargo_roketi", G("props/kargo_roketi.glb"), PZ);
        if (Main.Args.ContainsKey("lineup_deneme"))
        {
            // Ayri kapak dali (Pod.Model.cs) icin: govdenin kendi 'hatch' parcasi cikarilir; kapsul.json hatch gecici dosyayi gosterir.
            yield return new("kapak_deneme_kapali", PodWithoutHatch, PZ, 20);
            yield return new("kapak_deneme_acik", PodWithoutHatch, PZ, 90, n => ((Pod)n).BecomeRamp());
        }
        // Istasyonlar ve yerlestirilebilirler
        yield return new("tezgah", () => PlaceableVisuals.Make(P("tezgah")), PX);
        yield return new("tas_firin", () => new TasFirin { Working = false }, PX);
        yield return new("ocakbasi", () => new OcakbasiVisual(), PZ, 20);
        yield return new("paketleme", () => PlaceableVisuals.Make(P("paketleme")), PX);
        yield return new("sandik", () => PlaceableVisuals.Make(P("sandik")), PZ);
        yield return new("uyku_tulumu", () => PlaceableVisuals.Make(P("uyku_tulumu")), PZ);
        yield return new("mesale", () => PlaceableVisuals.Make(P("mesale")), PZ, 20);
        yield return new("ahsap_kapi", () => PlaceableVisuals.Make(P("ahsap_kapi")), PZ);
        yield return new("yemlik", () => PlaceableVisuals.Make(P("yemlik")), PZ);
        yield return new("folluk", () => PlaceableVisuals.Make(P("folluk")), PZ);
        yield return new("kargo_kasasi", G("placeables/kargo_kasasi.glb"), PZ);
        // Aletler ve arac
        yield return new("capa", G("capa/capa.glb"), PX);
        yield return new("kova", G("kova/kova.glb"), PX);
        yield return new("el_arabasi", G("el_arabasi/el_arabasi.glb"), PX);
        // Kaynaklar ve ekin
        yield return new("kaya", G("kaya/kaya.glb"), PX);
        yield return new("bakir", G("bakir/bakir.glb"), PX);
        yield return new("kil_yatagi", G("kil_yatagi/kil_yatagi.glb"), PX);
        yield return new("salgam", G("salgam/salgam.glb"), PX);
        // Kutuphaneden alinan kayalar (F:\Assets, pipeline/lib_import.py)
        foreach (var id in new[] { "kaya", "kaya_duz", "kaya_dik" })
            yield return new("lib_" + id, G($"lib/{id}/{id}.glb"), PX);
        // Dekor
        foreach (var id in new[] { "agac", "turuncu_agac", "cali", "ot", "bugday", "cicek", "egrelti", "kaya_duz", "kaya_dik" })
            yield return new(id, G($"{id}/{id}.glb"), PX);
        // Yaratiklar ve oyuncu
        // Yaratiklar oyundaki gibi: creatures.json modeli, rig boyuna (height) olceklenir
        foreach (var c in _data.Creatures.Creatures)
        {
            var def = c;
            yield return new(def.Id, () => Sized(Assets.Load(def.Rig.Model), def.Rig.Height), PX);
        }
        yield return new("oyuncu", G("player/player.glb"), PZ);
        // Yapi kiti (temsilci parcalar)
        foreach (var id in new[] { "duvar", "pencereli_duvar", "kapi_kasasi", "kare_temel", "egimli_cati", "merdiven", "cit", "sur_duvari", "kule_tabani", "makine_platformu_kucuk" })
            yield return new("kit_" + id, G($"build/wood/{id}.glb"), PZ);
    }

    static Node3D PodWithoutHatch()
    {
        var p = new Pod();
        if (p.FindChild("hatch", true, false) is Node h) { h.GetParent().RemoveChild(h); h.Free(); }
        return p;
    }

    static Node3D Sized(Node3D n, float height)
    {
        if (n == null) return null;
        var holder = new Node3D();
        holder.AddChild(n);
        float top = 0f;
        foreach (var mi in n.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            top = Mathf.Max(top, (mi.Transform * mi.GetAabb()).End.Y);
        if (top > 0.001f) n.Scale = Vector3.One * (height / top);
        return holder;
    }

    void BuildStudio()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("5b8fcf"), SkyHorizonColor = new Color("c9d6e0"),
            GroundBottomColor = new Color("4b4a3c"), GroundHorizonColor = new Color("b7b8a6"), SunAngleMax = 20f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky, RadianceSize = Sky.RadianceSizeEnum.Size256 },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 0.75f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            SsaoEnabled = true, SsaoRadius = 1.0f, SsaoIntensity = 1.2f,
            GlowEnabled = true, GlowIntensity = 0.5f, GlowHdrThreshold = 1.2f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        var sun = new DirectionalLight3D
        {
            LightColor = new Color("fff1dc"), LightEnergy = 1.15f, ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits, DirectionalShadowMaxDistance = 40f,
        };
        AddChild(sun);
        sun.LookAtFromPosition(Vector3.Zero, -World.Dir(42f, 220f), Vector3.Up);
        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(400, 400) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("6f7a52"), Roughness = 1f },
        };
        AddChild(ground);
        _cam = new Camera3D { Fov = 38f, Far = 600f };
        AddChild(_cam);
        _cam.MakeCurrent();
    }

    public override void _Process(double delta)
    {
        if (_entry == null)
        {
            if (_queue.Count == 0) { Finish(); return; }
            _entry = _queue.Dequeue();
            _current = _entry.Make();
            if (_current == null) { GD.Print($"LINEUP skip {_entry.Id} (yok)"); _entry = null; return; }
            AddChild(_current);
            _entry.After?.Invoke(_current);
            _frame = 0;
            return;
        }
        _frame++;
        if (_frame == 3) Frame();
        if (_frame < _entry.Frames + 3) return;
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng($"{_outDir}/{_entry.Id}.png");
        _shots.Add((_entry.Id, img));
        GD.Print($"LINEUP saved {_entry.Id}");
        _current.QueueFree();
        _entry = null;
    }

    /// <summary>Kamerayi modelin kapsayan kutusuna gore yerlestirir; olcek mankenini yanina koyar. Isik kubbesi (Ocak) gizlenir.</summary>
    void Frame()
    {
        bool first = true;
        var box = new Aabb();
        foreach (var mi in _current.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mi.Mesh is SphereMesh { IsHemisphere: true }) { mi.Visible = false; continue; }
            if (!mi.IsVisibleInTree()) continue;
            var b = mi.GlobalTransform * mi.GetAabb();
            box = first ? b : box.Merge(b);
            first = false;
        }
        if (first) box = new Aabb(new Vector3(-0.5f, 0, -0.5f), Vector3.One);
        // temeller kokun altina iner (ust yuz y=0): vitrinde yere oturt
        if (box.Position.Y < -0.05f) { _current.Position += Vector3.Up * -box.Position.Y; box.Position = new Vector3(box.Position.X, 0, box.Position.Z); }
        var dir = _entry.Front.Rotated(Vector3.Up, Mathf.DegToRad(-35f));
        var side = dir.Cross(Vector3.Up).Normalized();
        if (_scale != null)
        {
            float off = Mathf.Max(box.Size.X, box.Size.Z) * 0.5f + 0.7f;
            _scale.Position = new Vector3(box.GetCenter().X, 0, box.GetCenter().Z) - side * off;
            _scale.LookAt(_scale.Position + dir, Vector3.Up);
            box = box.Merge(new Aabb(_scale.Position + new Vector3(-0.3f, 0, -0.3f), new Vector3(0.6f, 1.8f, 0.6f)));
        }
        var c = box.GetCenter();
        float r = box.Size.Length() * 0.5f;
        float dist = r / Mathf.Sin(Mathf.DegToRad(_cam.Fov * 0.5f)) * 0.95f;
        var view = (dir * Mathf.Cos(Mathf.DegToRad(20f)) + Vector3.Up * Mathf.Sin(Mathf.DegToRad(20f))).Normalized();
        _cam.LookAtFromPosition(c + view * dist, c, Vector3.Up);
    }

    void Finish()
    {
        SetProcess(false);
        const int cols = 6, w = 426, h = 240;
        int rows = Mathf.Max(1, (_shots.Count + cols - 1) / cols);
        var sheet = Image.CreateEmpty(cols * w, rows * h, false, Image.Format.Rgb8);
        for (int i = 0; i < _shots.Count; i++)
        {
            var im = (Image)_shots[i].img.Duplicate();
            im.Convert(Image.Format.Rgb8);
            im.Resize(w, h, Image.Interpolation.Lanczos);
            sheet.BlitRect(im, new Rect2I(0, 0, w, h), new Vector2I(i % cols * w, i / cols * h));
        }
        sheet.SavePng($"{_outDir}/sheet.png");
        System.IO.File.WriteAllLines($"{_outDir}/sheet_order.txt", _shots.Select((s, i) => $"{i / cols + 1}.{i % cols + 1} {s.id}"));
        GD.Print($"LINEUP RESULT ok n={_shots.Count} dir={_outDir}");
        GetTree().Quit(0);
    }
}
