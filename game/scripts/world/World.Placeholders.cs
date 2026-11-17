using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU kaynaklar (URETIM 3 isareti): M1 kapsamindaki Kil, Bakir Cevheri, Lif ve yabani Bugday'in
/// gecici kaynaklari. Akislar (ekonomi, ilkgun) bu kaynaklarla gercek oyun yolundan kaynak toplar.
/// dunya akisi bunlari gercekleriyle degistirir: gol kiyisindaki kil yataklari, bakir damari, ot/cali/yabani bugday
/// (verimler biomes.json'a). Sayilar burada bilerek sabit: bu dosya yer tutucudur, veri ailesi dunya akisindadir.
/// </summary>
public partial class World
{
    /// <summary>Bicme ile toplanan tutam: ot ve cali Lif, yabani bugday Bugday verir; geri gelmez (URETIM 6 M1, Capa).</summary>
    public sealed class Tuft
    {
        public Vector3 Position;
        public string Item;
        public int Left;
        public Node3D Visual;
    }

    readonly List<Tuft> _tufts = new();
    public IReadOnlyList<Tuft> Tufts => _tufts;

    void BuildPlaceholderNodes()
    {
        int i = 0;
        // Kil: ussun bati yakasinda iki yumru. Bakir: dogu yakasinda iki damar.
        foreach (var (s, item) in new[] {
            (new Vector3(-24, 0, 12), "clay"), (new Vector3(-27, 0, 16), "clay"),
            (new Vector3(25, 0, -10), "copper_ore"), (new Vector3(28, 0, -14), "copper_ore") })
        {
            var rock = new Rock(8, 1.0f, (ulong)(5000 + i++), item);
            AddChild(rock);
            rock.GlobalPosition = Ground(s) + Vector3.Down * 0.2f;
            _rocks.Add(rock);
        }
        // Lif (ot ve cali) ve yabani bugday tutamlari.
        var spots = new (Vector3 p, string item)[] {
            (new(-8, 0, -12), "fiber"), (new(-11, 0, -14), "fiber"), (new(-6, 0, -16), "fiber"), (new(10, 0, -12), "fiber"),
            (new(13, 0, -15), "fiber"), (new(-20, 0, -4), "fiber"),
            (new(20, 0, 14), "wheat"), (new(23, 0, 17), "wheat"), (new(18, 0, 19), "wheat") };
        foreach (var (p, item) in spots)
        {
            var kit = new MeshKit();
            var c = item == "wheat" ? Palette.Warning : Palette.Plant;
            for (int k = 0; k < 5; k++)
            {
                float a = k * 1.256f;
                kit.Cylinder(0.04f, 0.02f, 0.9f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.18f, 0.45f, Mathf.Sin(a) * 0.18f), new Vector3(Mathf.Cos(a) * 0.25f, 0, Mathf.Sin(a) * 0.25f)), c.Lerp(Palette.Shadow, k * 0.05f), 5);
            }
            var v = kit.Instance();
            AddChild(v);
            v.GlobalPosition = Ground(p);
            _tufts.Add(new Tuft { Position = v.GlobalPosition, Item = item, Left = 4, Visual = v });
        }
    }

    /// <summary>Nisan noktasina en yakin dolu tutam (yoksa null).</summary>
    public Tuft NearestTuft(Vector3 aim, float within) =>
        _tufts.Where(t => t.Left > 0 && new Vector2(t.Position.X - aim.X, t.Position.Z - aim.Z).Length() < within)
            .OrderBy(t => t.Position.DistanceTo(aim)).FirstOrDefault();

    /// <summary>Tutamdan bir verim alir; bitince tutam kaybolur (geri gelmez).</summary>
    public int TakeTuft(Tuft t, int n)
    {
        int got = Mathf.Min(n, t.Left);
        t.Left -= got;
        if (t.Left <= 0 && IsInstanceValid(t.Visual)) t.Visual.QueueFree();
        return got;
    }
}
