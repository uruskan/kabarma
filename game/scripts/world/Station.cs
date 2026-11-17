using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Uretim istasyonu (GDD 11): Tezgah, Tas Firin, Paketleme Tezgahi. Malzeme cantadan alinir,
/// sira oyuncu uzaktayken de islenir; durum istasyonun ustunde yazar (menu yerine dunya, SANAT-SES §8.1).
/// </summary>
public partial class Station : Node3D
{
    public PlaceableDef Def { get; }
    public StationQueue Queue { get; }
    readonly Director _d;
    Label3D _label;
    TasFirin _oven;
    float _bounce;

    public Station(Director d, PlaceableDef def)
    {
        _d = d; Def = def;
        Queue = d.State.Stations[def.Station!];
        Name = def.Id;
    }

    /// <summary>Gorunus: Tas Firin kendi sinifini kullanir (kapak ve duman kodla hareket eder), digerleri modeli yukler.</summary>
    public static Node3D MakeVisual(PlaceableDef def)
    {
        if (def.Id == "ocak") return new Ocak();
        if (def.Id == "tas_firin") return new TasFirin();
        var m = def.Model != null ? Assets.Load(def.Model) : null;
        if (m != null) return m;
        // Yedek: koddan kutu
        var kit = new MeshKit();
        kit.Pillow(new Vector3(1.4f, 0.9f, 0.8f), MeshKit.At(0, 0.45f, 0), Palette.Wood, 0.3f);
        return kit.Instance();
    }

    public override void _Ready()
    {
        var vis = MakeVisual(Def);
        AddChild(vis);
        _oven = vis as TasFirin;
        // Carpisma: modelin kapsayan kutusu
        var aabb = new Aabb(); bool first = true;
        foreach (var mi in vis.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var b = RelTransform(vis, mi) * mi.GetAabb();
            aabb = first ? b : aabb.Merge(b); first = false;
        }
        if (!first)
        {
            var body = new StaticBody3D();
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = aabb.Size * new Vector3(0.9f, 1f, 0.9f) }, Position = aabb.GetCenter() });
            AddChild(body);
        }
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 12, PixelSize = 0.006f,
            Modulate = Palette.OffWhite, OutlineModulate = Palette.NightDeep, NoDepthTest = true,
            Position = new Vector3(0, (first ? 1.2f : aabb.End.Y) + 0.45f, 0),
        };
        AddChild(_label);
        Queue.Produced += (r, n) =>
        {
            if (!IsInsideTree()) return;
            _bounce = 1f;
            Fx.FlyTo(GlobalPosition + Vector3.Up * 1.2f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Warning, 0.14f);
            Fx.Text3D(GlobalPosition + Vector3.Up * 1.6f, $"+{n} {r.Name}", Palette.Warning, 48, 1.2f, 1.4f);
            Sfx.Play("pickup", GlobalPosition, 0f);
            _d.OnProduced(r);
        };
    }

    static Transform3D RelTransform(Node3D root, Node3D n)
    {
        var xf = Transform3D.Identity;
        Node cur = n;
        while (cur != null && cur != root) { if (cur is Node3D n3) xf = n3.Transform * xf; cur = cur.GetParent(); }
        return xf;
    }

    public bool InReach(Vector3 p) => new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() < 3.2f;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Queue.Tick(dt, _d.State.Backpack);
        if (_oven != null) _oven.Working = Queue.Active != null;
        bool near = InReach(_d.Player.GlobalPosition);
        _label.Text = Queue.Active != null
            ? $"{Def.Name}\n{Queue.Active.Name} {(int)(Queue.Progress * 100)}%  ·  sırada {Queue.Queued}"
            : near ? $"{Def.Name}\nE: üret" : Def.Name;
        _label.Modulate = near ? Palette.Warning : Palette.OffWhite;
        if (_bounce > 0)
        {
            _bounce = Mathf.Max(0, _bounce - dt * 4f);
            float k = Mathf.Sin(_bounce * Mathf.Pi) * 0.06f;
            GetChild<Node3D>(0).Scale = new Vector3(1 + k, 1 - k, 1 + k);
        }
    }
}
