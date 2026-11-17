using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: toplanan kaynaklar (URETIM 6 M1 kapsami, "Kaynaklar" ve "Capa" satirlari). Yer tutucularin yerine gelenler:
///  - Kazma (Rock): tas (altin kaya), gol kiyisindaki kil yataklari (koddan yumru, kredi yok), bakir damari (bakir modeli).
///  - Bicme (Tuft): ot ve cali Lif, yabani bugday Bugday verir; geri gelmez.
/// Dugumler ve verimler data/biomes.json "nodes" ve "decor" satirlarinda. Arayuz MVP ile ayni: Rocks, Rock.Item,
/// Tuft, Tufts, NearestTuft, TakeTuft (Capa.Kazma.cs, Capa.Bicme.cs ve bot buna dayanir).
/// </summary>
public partial class World
{
    readonly List<Rock> _rocks = new();
    public IReadOnlyList<Rock> Rocks => _rocks;
    public void RemoveRock(Rock r) => _rocks.Remove(r);

    /// <summary>Bicme ile toplanan tutam: ot ve cali Lif, yabani bugday Bugday verir; geri gelmez (URETIM 6 M1, Capa).</summary>
    public sealed class Tuft
    {
        public Vector3 Position;
        public string Item;
        public int Left;
        /// <summary>Kendi dugumu olan tutam (bos olabilir: cogu tutam bir MultiMesh ornegidir).</summary>
        public Node3D Visual;
        /// <summary>Tutamin turu (biomes.json decor kimligi) ve ilk verimi.</summary>
        public string Kind;
        public int Yield;
        internal MultiMesh Mm;
        internal int Index;
        internal Transform3D Local;
    }

    readonly List<Tuft> _tufts = new();
    public IReadOnlyList<Tuft> Tufts => _tufts;

    void AddTufts(DecorDef def, Dictionary<int, (DecorChunkRef chunk, int index, Transform3D local)> chunks)
    {
        foreach (var (_, (chunk, index, local)) in chunks.OrderBy(kv => kv.Key))
        {
            _tufts.Add(new Tuft
            {
                Position = chunk.Origin + local.Origin, Item = def.Harvest, Left = def.Yield, Yield = def.Yield, Kind = def.Id,
                Mm = chunk.Mm, Index = index, Local = local,
            });
        }
    }

    /// <summary>Nisan noktasina en yakin dolu tutam (yoksa null).</summary>
    public Tuft NearestTuft(Vector3 aim, float within) =>
        _tufts.Where(t => t.Left > 0 && new Vector2(t.Position.X - aim.X, t.Position.Z - aim.Z).Length() < within)
            .OrderBy(t => t.Position.DistanceTo(aim)).FirstOrDefault();

    /// <summary>Tutamdan bir verim alir; tutam kuculur, bitince kaybolur (geri gelmez).</summary>
    public int TakeTuft(Tuft t, int n)
    {
        int got = Mathf.Min(n, t.Left);
        t.Left -= got;
        if (t.Mm != null)
        {
            float k = t.Left <= 0 ? 0f : 0.45f + 0.55f * t.Left / Mathf.Max(1, t.Yield);
            t.Mm.SetInstanceTransform(t.Index, new Transform3D(t.Local.Basis.Scaled(Vector3.One * k), t.Local.Origin));
        }
        if (t.Left <= 0 && t.Visual != null && IsInstanceValid(t.Visual)) t.Visual.QueueFree();
        return got;
    }

    /// <summary>Kazilacak dugumler: tas kayalari ussun cevresinde, kil yataklari gol kiyisinda, bakir damarlari dogu yamacinda.</summary>
    void BuildResources()
    {
        // Kendi tohumu: dekor ve ot degisse de kayalarin birimleri ayni kalir.
        var rng = new RandomNumberGenerator { Seed = (ulong)(T.Seed + 23) };
        ulong seed = 1000;
        foreach (var node in Biome.Nodes)
        {
            var spots = node.Positions.Select(p => new Vector3(p[0], 0, p[1])).ToList();
            spots.AddRange(ShoreSpots(node));
            foreach (var s in spots)
            {
                int units = node.Units.Count >= 2 ? rng.RandiRange(node.Units[0], node.Units[1]) : 1;
                float size = rng.RandfRange(node.Size[0], node.Size.Count > 1 ? node.Size[1] : node.Size[0]);
                var rock = new Rock(units, size, seed++, node.Item, node.Model) { Name = $"{node.Id}_{seed}" };
                AddChild(rock);
                rock.GlobalPosition = Ground(s) + Vector3.Down * (node.Item == "clay" ? 0.05f : 0.2f);
                _rocks.Add(rock);
            }
        }
    }

    /// <summary>Kil yataklari: golun Ussa bakan kiyisina, kiyi cizgisinin hemen disina dizilir.</summary>
    IEnumerable<Vector3> ShoreSpots(NodeDef node)
    {
        if (node.ShoreCount <= 0 || !HasLake) yield break;
        float from = node.ShoreArc.Count > 0 ? node.ShoreArc[0] : 0f, to = node.ShoreArc.Count > 1 ? node.ShoreArc[1] : 360f;
        for (int i = 0; i < node.ShoreCount; i++)
        {
            float a = Mathf.DegToRad(Mathf.Lerp(from, to, node.ShoreCount == 1 ? 0.5f : i / (float)(node.ShoreCount - 1)));
            float r = ShoreRadius(a) + node.ShoreOffset;
            yield return new Vector3(Lake.Center[0] + Mathf.Cos(a) * r, 0, Lake.Center[1] + Mathf.Sin(a) * r);
        }
    }
}
