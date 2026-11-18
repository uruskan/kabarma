using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// M2 yapi sinamasi (--test=build'in devami; GDD §7): hayalet rengi zorlanmayi gosterir, ahsap kule yuk sinirinda durur,
/// Kerpic-Tas taban bir kat daha tasir, dort malzemeye yerinde yukseltme (model ve can), plan modu (hayalet, malzeme
/// gelince kurulur, alti bitmeden kurulmaz), Plan Defteri (kaydet, baska yere hayalet olarak koy), ayak sokulunce cokme.
/// Kanit: progress/m2/yapi/ (basliksizda goruntu atlanir).
/// </summary>
public partial class BuildTest
{
    string _m2Dir;

    void Check(bool ok, string what)
    {
        Log($"{(ok ? "OK  " : "FAIL")} yapi: {what}");
        if (!ok) _fail++;
    }

    void ShotM2(string file)
    {
        if (DisplayServer.GetName() == "headless") { Log($"SHOT {file} (basliksiz: atlandi)"); return; }
        // --m2_out=KLASOR: M2 kaniti ayri yere (M1 goruntuleri --test_out'a gider, progress/m1 bozulmaz).
        _m2Dir ??= Main.Args.TryGetValue("m2_out", out var m2) ? m2.TrimEnd('/', '\\') + "/" : Main.Args.TryGetValue("test_out", out var o) ? o.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m2/yapi/";
        DirAccess.MakeDirRecursiveAbsolute(_m2Dir);
        GetViewport().GetTexture().GetImage().SavePng(_m2Dir + file);
        Log($"SHOT {file}");
    }

    async Task Look(Vector3 camPos, Vector3 at, int frames = 6)
    {
        _cam.GlobalPosition = camPos;
        _cam.LookAt(at);
        await Frames(frames);
    }

    /// <summary>Duz yer arar: temel o noktada araziye oturabiliyorsa (hayalet gecerli) temel kurar.</summary>
    async Task<BuildPiece> FoundationNear(float x, float z)
    {
        foreach (var (dx, dz) in new[] { (0f, 0f), (4f, 0f), (-4f, 0f), (0f, 4f), (0f, -4f), (8f, 4f), (-8f, -4f), (8f, -8f) })
        {
            var t = new Vector3(x + dx, H(x + dx, z + dz), z + dz);
            _bs.Select("kare_temel");
            _bs.UpdateAim(t + new Vector3(0, 9, 5), new Vector3(0, -9, -5));
            if (_bs.Current is { Valid: true, Mode: "ground" }) return await Place("kare_temel", t, t + new Vector3(0, 9, 5));
        }
        Check(false, $"duz yer bulunamadi ({x},{z})");
        return null;
    }

    async Task YapiTests()
    {
        _bs.SetActive(true);
        await TowerAndUpgrade();
        await PlanModeAndBook();
        _bs.SetActive(false);
    }

    // ---------------------------------------------------------------- kule: yuk siniri, renk, yukseltme, cokme

    async Task TowerAndUpgrade()
    {
        var f = await FoundationNear(34f, -6f);
        if (f == null) return;
        float y = f.GlobalPosition.Y;
        var c = f.GlobalPosition;
        var wood = _bs.Data.Build.Material("wood");
        int expect = (int)(wood.Capacity / (_bs.UnitsOf("duvar") * wood.WeightPerUnit));
        var walls = new System.Collections.Generic.List<BuildPiece>();
        for (int i = 0; i < expect; i++)
        {
            var w = await Wall("duvar", c + new Vector3(1.5f, 0, 0), Vector3.Right, y + 3f * i);
            if (w == null) break;
            walls.Add(w);
        }
        Check(walls.Count == expect, $"ahsap kule {walls.Count}/{expect} duvar (yuk siniri {wood.Capacity})");
        Check(walls.All(w => w.Span == 0), "dusey yigin acikliksiz");
        // Bir sonraki: asiri yuk, kirmizi hayalet.
        var top = new Vector3(c.X + 1.5f, y + 3f * walls.Count, c.Z) - Vector3.Right * 0.45f;
        _bs.Select("duvar");
        for (int i = 0; i < 4 && !(_bs.Current?.Reason ?? "").StartsWith("aşırı"); i++) { _bs.UpdateAim(top + Vector3.Up * 6f - Vector3.Right * 2f, -Vector3.Up * 6f + Vector3.Right * 2f); _bs.Rotate(); }
        var cur = _bs.Current;
        Log($"TOWER next: valid={cur?.Valid} reason='{cur?.Reason}' load={cur?.Report?.Load:0.#}/{cur?.Report?.Capacity:0.#}");
        Check(cur != null && !cur.Valid && cur.Reason.StartsWith("aşırı yük"), "kule sinirinda bir sonraki duvar asiri yuk");
        Check(_bs.GhostColor(cur) == ColorFrom(_bs.Data.Build.Ghost.Bad), "asiri yukte hayalet kirmizi");
        await Look(c + new Vector3(14, y + 10, 16), c + new Vector3(0, 10, 0));
        ShotM2("yapi_kule_siniri.png");

        // Zorlanma rengi: yeni temelin ustunde rahat duvar yesil, sinira yakin yuk sariya kayar.
        Check(walls.Count > 0 && walls[0].Load > walls[^1].Load, $"alt duvar daha cok tasir ({walls.FirstOrDefault()?.Load:0.#} > {walls.LastOrDefault()?.Load:0.#})");

        // Tabani Kerpic-Tas yap: bir kat daha cikar.
        if (walls.Count == 0) return;
        Check(_bs.Upgrade(walls[0], "kerpic_tas", out var rep), $"taban Kerpic-Tas ({rep?.Reason})");
        Check(Mathf.IsEqualApprox(walls[0].Hp, _bs.Data.Build.Material("kerpic_tas").Hp), $"yukseltmede can {walls[0].Hp}");
        var extra = await Wall("duvar", c + new Vector3(1.5f, 0, 0), Vector3.Right, y + 3f * walls.Count);
        Check(extra != null, "Kerpic-Tas tabanla bir kat daha");
        if (extra != null) walls.Add(extra);
        // Tepeyi betona yukseltmek alttakini ezer: reddedilir.
        Check(!_bs.Upgrade(walls[^1], "mayali_beton", out var heavy) && heavy != null && heavy.Reason.StartsWith("aşırı"), $"tepeye beton reddedildi ({heavy?.Reason})");

        // Dort malzeme yan yana (yeni temelde, dort duvar): model ve can.
        var g = await FoundationNear(34f, 8f);
        if (g != null)
        {
            var gc = g.GlobalPosition;
            var sides = new[] { Vector3.Forward, Vector3.Right, Vector3.Back, Vector3.Left };
            var mats = _bs.Data.Build.Materials.Select(m => m.Id).ToArray();
            for (int i = 0; i < 4; i++)
            {
                var w = await Wall(i == 2 ? "pencereli_duvar" : "duvar", gc + sides[i] * 1.5f, sides[i], gc.Y);
                if (w == null) continue;
                for (int k = 1; k <= i; k++) _bs.Upgrade(w, mats[k], out _);
                Check(w.Material == mats[i], $"duvar {i}: {w.Material} can {w.Hp}/{_bs.MaxHp(w)}");
            }
            await Look(gc + new Vector3(8, 6, 9), gc + Vector3.Up * 1.2f);
            ShotM2("yapi_malzemeler.png");
            await Look(gc + new Vector3(-8, 6, -9), gc + Vector3.Up * 1.2f);
            ShotM2("yapi_malzemeler_arka.png");
        }

        // Ayagi sok: kule temeli gidince butun duvarlar cokerek duser (toz, kirinti, ses).
        int fell = 0;
        void OnFell(BuildPiece p, RigidBody3D b) => fell++;
        _bs.Fell += OnFell;
        _bs.Remove(f);
        _bs.Fell -= OnFell;
        Check(fell == walls.Count, $"temel sokulunce {fell}/{walls.Count} duvar coktu");
        await Look(c + new Vector3(16, y + 8, 18), c + new Vector3(0, 6, 0), 2);
        for (int i = 0; i < 30; i++) await Physics();
        ShotM2("yapi_cokme.png");
    }

    static Color ColorFrom(float[] c) => new(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f);

    // ---------------------------------------------------------------- plan modu, Plan Defteri

    async Task PlanModeAndBook()
    {
        _bs.PlanMode = true;
        var f = await FoundationNear(52f, -6f);
        _bs.PlanMode = false;
        if (f == null) return;
        Check(f.Planned && f.Span == int.MaxValue, "plan temeli dengeye katilmaz");
        var c = f.GlobalPosition;
        _bs.PlanMode = true;
        var w1 = await Wall("duvar", c + Vector3.Forward * 1.5f, Vector3.Forward, c.Y);
        var w2 = await Wall("kapi_kasasi", c + Vector3.Back * 1.5f, Vector3.Back, c.Y);
        var w3 = await Wall("duvar", c + Vector3.Left * 1.5f, Vector3.Left, c.Y);
        var w4 = await Wall("pencereli_duvar", c + Vector3.Right * 1.5f, Vector3.Right, c.Y);
        var roof = await OnWallTop("duz_cati", c + Vector3.Forward * 1.5f, Vector3.Forward, c.Y + 3f);
        _bs.PlanMode = false;
        var plan = new[] { f, w1, w2, w3, w4, roof };
        Check(plan.All(p => p != null && p.Planned), "plan modu: temel, 4 duvar, cati hayalet");
        if (plan.Any(p => p == null)) return;
        await Look(c + new Vector3(9, 7, 10), c + Vector3.Up * 1.5f);
        ShotM2("yapi_plan_hayalet.png");
        // Alti kurulmadan duvar kurulmaz; temel, sonra duvarlar ve cati.
        Check(!_bs.CompletePlan(w1, out var r1) && r1.Reason == "destek yok", $"alti plan iken duvar kurulmaz ({r1?.Reason})");
        foreach (var p in plan)
            Check(_bs.CompletePlan(p, out var r), $"plan tamam: {p.Def.Id} ({r?.Reason})");
        Check(plan.All(p => !p.Planned && p.Span != int.MaxValue), "plan parcalari kuruldu ve dengede");
        await Frames(4);
        ShotM2("yapi_plan_kuruldu.png");

        // Plan Defteri: bu barinagi kaydet, baska yere 90° donuk hayalet olarak koy.
        var bp = _bs.Capture(w1, "Barınak");
        Check(bp != null && bp.Pieces.Count == plan.Length, $"Plan Defteri: {bp?.Pieces.Count} parca kaydedildi");
        if (bp == null) return;
        _bs.BeginBlueprint(bp);
        (int k, int n) placed = (0, bp.Pieces.Count);
        foreach (var (dx, dz) in new[] { (0f, 14f), (6f, 14f), (-6f, 16f), (10f, 20f), (-10f, 10f) })
        {
            var t = new Vector3(c.X + dx, H(c.X + dx, c.Z + dz), c.Z + dz);
            for (int i = 0; i < 6; i++) _bs.Rotate();
            _bs.UpdateAim(t + new Vector3(0, 9, 5), new Vector3(0, -9, -5));
            if (!(_bs.Current?.Valid ?? false)) continue;
            await Look(t + new Vector3(10, 8, 11), t + Vector3.Up * 1.5f);
            ShotM2("yapi_defter_hayalet.png");
            placed = _bs.PlaceBlueprint();
            break;
        }
        _bs.EndBlueprint();
        Log($"BLUEPRINT {placed.k}/{placed.n}");
        Check(placed.k == placed.n, $"Plan Defteri yerlestirme {placed.k}/{placed.n}");
        var placedPieces = _bs.Pieces.Where(p => p.Planned).ToList();
        Check(placedPieces.Count >= placed.k, "defterden konanlar plan hayaleti");
        if (placedPieces.Count > 0)
        {
            var at = placedPieces[0].GlobalPosition;
            await Look(at + new Vector3(10, 8, 11), at + Vector3.Up * 1.5f);
            ShotM2("yapi_defter_konuldu.png");
        }
    }
}
