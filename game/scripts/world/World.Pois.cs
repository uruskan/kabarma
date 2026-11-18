using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: kesif noktalari (ICERIK §17 "M2 dunyasindaki T1-T2 noktalari", dunya2). Yerler Sim'de (PoiPlacer, tohumdan);
/// burada arazi duzlestirmesi, gorunus ve toplanan kisimlar:
///  - Seyir Kulesi: model + tepesi (Explorer tirmanir) + dibinde mancinik duragi;
///  - Enkaz: Kazma ile Hurda veren Rock (Item "scrap"); Yabani Bahce: Bicme ile tohum veren gorunmez tutamlar;
///  - Yuva: E ile alinan yumurta (Explorer); Ata Tasi: yalniz yer ve duzlestirme (tas ve arena boss akisinda).
/// Kalan/yenilenme durumu Sim'de (GameState.Explore), Explorer esitler.
/// </summary>
public partial class World
{
    /// <summary>Noktanin gorunus dugumu (yoksa yok).</summary>
    public readonly Dictionary<string, Node3D> PoiNodes = new();
    /// <summary>Enkaz kayalari ve Bahce tutamlari (Explorer kalan birimi buradan okur ve yeniler).</summary>
    public readonly Dictionary<string, Rock> PoiRocks = new();
    public readonly Dictionary<string, List<Tuft>> PoiTufts = new();

    public PoiTypeDef PoiType(string type) => Biomes.World.PoiTypes.TryGetValue(type, out var t) ? t : new PoiTypeDef { Name = type };
    public Vector3 PoiPos(Poi p) => Ground(new Vector3(p.X, 0, p.Z));

    /// <summary>Seyir Kulesi'nin tepe platformu (model yuksekligi, olcek 1).</summary>
    public const float TowerHeight = 14f;
    public Vector3 TowerTop(Poi p) => PoiPos(p) + Vector3.Up * (TowerHeight + 0.2f);
    /// <summary>Kulenin dibindeki mancinik duragi (merdivenin karsi yuzunde, 4 m).</summary>
    public Vector3 TowerCatapult(Poi p) => Ground(new Vector3(p.X + 3f, 0, p.Z - 4f));
    /// <summary>Merdivenin dibi (model: merdiven +Z yuzunde, gen_kesif.py).</summary>
    public Vector3 TowerLadder(Poi p) => Ground(new Vector3(p.X, 0, p.Z + 2.3f));

    /// <summary>Nokta duzlestirmesinin icinde mi (bitki ve dugum konmaz).</summary>
    public bool InPoiClear(Vector3 p)
    {
        if (Pois == null) return false;
        foreach (var poi in Pois)
        {
            float r = PoiType(poi.Type).Flatten;
            if (r <= 0) continue;
            float dx = p.X - poi.X, dz = p.Z - poi.Z;
            if (dx * dx + dz * dz < (r + 1.5f) * (r + 1.5f)) return true;
        }
        return false;
    }

    /// <summary>TEKNIK §7.2 "kesif noktasi duzlestirmesi": yaricap icinde merkez yuksekligine, 1,8 kat yaricapa dek yumusak.</summary>
    void FlattenPois()
    {
        foreach (var poi in Pois)
        {
            float r = PoiType(poi.Type).Flatten;
            if (r <= 0) continue;
            int cx = Mathf.RoundToInt(poi.X + Cells / 2f), cz = Mathf.RoundToInt(poi.Z + Cells / 2f);
            if (cx < 0 || cz < 0 || cx > Cells || cz > Cells) continue;
            // Merkez: yaricap icindeki ortalama (tepe ya da cukur ortasina dusmesin).
            float sum = 0; int n = 0;
            int ri = Mathf.CeilToInt(r);
            for (int x = cx - ri; x <= cx + ri; x++)
            for (int z = cz - ri; z <= cz + ri; z++)
                if (x >= 0 && z >= 0 && x <= Cells && z <= Cells) { sum += _h[x, z]; n++; }
            float target = sum / Mathf.Max(1, n);
            int ro = Mathf.CeilToInt(r * 1.8f);
            for (int x = cx - ro; x <= cx + ro; x++)
            for (int z = cz - ro; z <= cz + ro; z++)
            {
                if (x < 0 || z < 0 || x > Cells || z > Cells) continue;
                // Ussun duzlugu (M1 yuksekligi, dunya sinamasi) hic degismez.
                if (new Vector2(x - Cells / 2f, z - Cells / 2f).Length() <= T.BaseFlatOuter + 1f) continue;
                float d = new Vector2(x - Cells / 2f - poi.X, z - Cells / 2f - poi.Z).Length();
                float k = 1f - Mathf.SmoothStep(r, r * 1.8f, d);
                if (k > 0) _h[x, z] = Mathf.Lerp(_h[x, z], target, k);
            }
        }
    }

    void BuildPois()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(T.Seed + 71) };
        foreach (var poi in Pois)
        {
            var type = PoiType(poi.Type);
            var at = PoiPos(poi);
            Node3D vis = string.IsNullOrEmpty(type.Model) ? null : Assets.Load(type.Model);
            if (vis != null)
            {
                vis.Name = "Poi_" + poi.Id;
                AddChild(vis);
                vis.GlobalPosition = at;
                vis.Rotation = new Vector3(0, rng.Randf() * Mathf.Tau, 0);
                PoiNodes[poi.Id] = vis;
            }
            switch (poi.Type)
            {
                case "seyir_kulesi":
                    if (vis != null) { vis.Rotation = Vector3.Zero; AddTowerBody(vis); }
                    if (type.Catapult && Assets.Load("res://assets/models/placeables/mancinik.glb") is { } cat)
                    {
                        cat.Name = "PoiCatapult_" + poi.Id;
                        AddChild(cat);
                        cat.GlobalPosition = TowerCatapult(poi);
                        cat.Rotation = new Vector3(0, Mathf.Pi * 0.75f, 0);
                    }
                    break;
                case "enkaz":
                {
                    if (vis != null) { vis.QueueFree(); PoiNodes.Remove(poi.Id); }
                    var rock = SpawnPoiRock(poi, Exploration.Loot(poi, type).Values.Sum());
                    if (rock != null) PoiNodes[poi.Id] = rock;
                    break;
                }
                case "yabani_bahce":
                {
                    var list = new List<Tuft>();
                    int k = 0;
                    foreach (var (item, count) in Exploration.Loot(poi, type))
                    {
                        float a = k++ * 2.4f;
                        var tp = Ground(new Vector3(poi.X + Mathf.Cos(a) * 1.2f, 0, poi.Z + Mathf.Sin(a) * 1.2f));
                        var t = new Tuft { Position = tp, Item = item, Left = count, Yield = count, PerHit = Mathf.Max(1, type.PerHit), Kind = "yabani_bahce" };
                        _tufts.Add(t);
                        list.Add(t);
                    }
                    PoiTufts[poi.Id] = list;
                    break;
                }
            }
        }
        GD.Print($"WORLD pois {Pois.Count}: " + string.Join(" ", Pois.Select(p => $"{p.Id}@{p.X:0},{p.Z:0}")));
    }

    /// <summary>Kule govdesi: dort ayak + tepe platformu carpismasi (oyuncu altindan gecemez, tepede durur).</summary>
    void AddTowerBody(Node3D vis)
    {
        var body = new StaticBody3D { Name = "TowerBody" };
        vis.AddChild(body);
        foreach (var (x, z) in new[] { (-1.6f, -1.6f), (1.6f, -1.6f), (-1.6f, 1.6f), (1.6f, 1.6f) })
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.4f, TowerHeight, 0.4f) }, Position = new Vector3(x, TowerHeight / 2f, z) });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4.6f, 0.3f, 4.6f) }, Position = new Vector3(0, TowerHeight, 0) });
    }

    /// <summary>Enkaz kayasini (yeniden) kurar: units birim; eskisi varsa kaldirilir. units 0 ise kurmaz.</summary>
    public Rock SpawnPoiRock(Poi poi, int units)
    {
        if (PoiRocks.TryGetValue(poi.Id, out var old) && IsInstanceValid(old)) { _rocks.Remove(old); old.QueueFree(); }
        PoiRocks.Remove(poi.Id);
        if (units <= 0) return null;
        var type = PoiType(poi.Type);
        var rock = new Rock(units, 1f, (ulong)(5000 + Pois.IndexOf(poi)), Exploration.Loot(poi, type).Keys.First(), type.Model) { Name = "Enkaz_" + poi.Id };
        AddChild(rock);
        rock.GlobalPosition = PoiPos(poi) + Vector3.Down * 0.1f;
        _rocks.Add(rock);
        PoiRocks[poi.Id] = rock;
        return rock;
    }

    /// <summary>Bahce tutamlarini doldurur (yenilenme gunu geldi ya da kayittan kalan birim).</summary>
    public void RefillTufts(string poiId, Dictionary<string, int> left)
    {
        if (!PoiTufts.TryGetValue(poiId, out var list)) return;
        foreach (var t in list) t.Left = left.TryGetValue(t.Item, out var n) ? n : 0;
    }
}
