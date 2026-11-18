using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Uretim istasyonu (GDD 11, ICERIK §11): Tezgah, Ocakbasi, Tas Firin, Paketleme Tezgahi. Malzeme ve yakit cantadan alinir,
/// sira oyuncu uzaktayken de islenir; durum istasyonun ustunde yazar (menu yerine dunya, SANAT-SES §8.1).
/// Yuk urunu (El Arabasi, kargo kasasi) cantaya degil istasyonun onune cikar.
/// </summary>
public partial class Station : Node3D
{
    public PlaceableDef Def { get; }
    public StationQueue Queue { get; }
    readonly Director _d;
    Label3D _label;
    TasFirin _oven;
    OcakbasiVisual _grill;
    float _bounce;

    public Station(Director d, PlaceableDef def)
    {
        _d = d; Def = def;
        Queue = d.State.QueueFor(def.Station!); // tarifi olmayan istasyon (Kulucka, Arastirma Masasi) da kuyruk alir
        Name = def.Id;
    }

    /// <summary>Gorunus: Tas Firin ve Ocakbasi kendi siniflari (kodla hareket eden parcalar), digerleri model (PlaceableVisuals).</summary>
    public static Node3D MakeVisual(PlaceableDef def) => ZincirVisuals.Make(def) ?? PlaceableVisuals.Make(def); // zincir istasyonlari: koddan yer tutucu (Station.Visuals.cs)

    /// <summary>Yuk urununun cikacagi nokta: istasyonun onu (+Z), yere oturmus.</summary>
    public Vector3 OutputPoint => _d.World.Ground(GlobalPosition + GlobalBasis.Z * 1.9f);

    public override void _Ready()
    {
        Callable.From(() => { if (IsInstanceValid(this) && IsInsideTree()) _d.World?.ClearGrass(GlobalPosition, Def.Radius + 0.3f); }).CallDeferred(); // altindaki ot kalkar (gorsel-dunya; konum AddChild'dan sonra verilir)
        var vis = MakeVisual(Def);
        AddChild(vis);
        _oven = vis as TasFirin;
        _grill = vis as OcakbasiVisual;
        // Carpisma: modelin kapsayan kutusu
        bool any = PlaceableVisuals.Bounds(vis, out var aabb);
        if (any)
        {
            var body = new StaticBody3D();
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = aabb.Size * new Vector3(0.9f, 1f, 0.9f) }, Position = aabb.GetCenter() });
            AddChild(body);
        }
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 12, PixelSize = 0.006f,
            Modulate = Palette.OffWhite, OutlineModulate = Palette.NightDeep, NoDepthTest = true,
            Position = new Vector3(0, (any ? aabb.End.Y : 1.2f) + 0.45f, 0),
        };
        AddChild(_label);
        Queue.Produced += OnProduced;
    }

    public override void _ExitTree() => Queue.Produced -= OnProduced;

    void OnProduced(RecipeDef r, int n)
    {
        if (!IsInsideTree() || _d.Stations.FirstOrDefault(s => s.Queue == Queue) != this) return; // ayni turden ikinci istasyon: ilki gosterir
        _bounce = 1f;
        if (r.Load) _d.SpawnLoad(r, this);
        else Fx.FlyTo(GlobalPosition + Vector3.Up * 1.2f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Warning, 0.14f);
        Fx.Text3D(GlobalPosition + Vector3.Up * 1.6f, $"+{n} {r.Name}", Palette.Warning, 48, 1.2f, 1.4f);
        Sfx.Play("pickup", GlobalPosition, 0f);
        _d.OnProduced(r);
    }

    /// <summary>Yakinlik: istasyon tarifleri ve E menusu bu yaricapta (craft.json stationReach).</summary>
    public bool InReach(Vector3 p) => new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() < _d.Data.Tool.StationReach;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_d.Stations.FirstOrDefault(s => s.Queue == Queue) == this) Queue.Tick(dt, _d.StationStore()); // ambardan uretim: urun cantaya, dolarsa tapudaki Sandiklara (GDD 5)
        bool working = Queue.Active != null;
        if (_oven != null) _oven.Working = working;
        if (_grill != null) _grill.Working = working;
        bool near = InReach(_d.Player.GlobalPosition);
        string fuel = Def.FuelPerJob > 0 && Def.Fuel.Count > 0 ? $"  ·  yakıt: {ItemNames.Of(Def.Fuel[0])} {_d.State.Backpack.Count(Def.Fuel[0])}" : "";
        string packing = Def.Station == "paketleme" && _d.State.Cargo.Open != null ? $"\nkasa: {_d.State.Cargo.Open.Items.UsedSlots}/{_d.State.Cargo.Open.Items.Slots} yığın · {_d.State.Cargo.Value(_d.State.Cargo.Open)} Kredi" : "";
        _label.Text = working
            ? $"{Def.Name}\n{Queue.Active.Name} {(int)(Queue.Progress * 100)}%  ·  sırada {Queue.Queued}{QueueTail()}"
            : near ? $"{Def.Name}{fuel}\nE: üretim{packing}" : Def.Name + packing;
        _label.Modulate = near ? Palette.Warning : Palette.OffWhite;
        if (_bounce > 0)
        {
            _bounce = Mathf.Max(0, _bounce - dt * 4f);
            float k = Mathf.Sin(_bounce * Mathf.Pi) * 0.06f;
            GetChild<Node3D>(0).Scale = new Vector3(1 + k, 1 - k, 1 + k);
        }
    }
}
