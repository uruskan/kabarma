using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kargo kasasi, dunyadaki yuk nesnesi (GDD §5, §13): 25 kg, cantaya girmez, Cekim basligiyla ya da El Arabasi'yla tasinir.
/// Cekim bugun kabarik urun tipiyle (SwollenCrop) calistigi icin kasa onun alt sinifidir: tutulur, tasinir, birakilir;
/// rampaya birakilinca Director.SellCrop onu rampaya yukler (satmaz). Agirlik Size.Kg'de (Cekim'in 80 kg siniri icin).
/// </summary>
public partial class CargoCrateNode : SwollenCrop
{
    const float VisualScale = 1.6f; // SwollenCrop.Radius = 0,3 × olcek = 0,48 m (tutma ve rampa mesafeleri icin)
    /// <summary>Kasanin yarim yuksekligi (0,6 m kasa): yere, rampaya, araba kasasina oturtma bununla (Radius kutu icin fazla).</summary>
    public const float HalfHeight = 0.3f;
    public CargoCrate Crate { get; }
    /// <summary>Rampada yuklu mu (firlatilmayi bekliyor).</summary>
    public bool OnRamp;
    /// <summary>El Arabasi'nin kasasinda mi.</summary>
    public ElArabasi OnCart;
    Label3D _label;
    readonly Director _d;

    public CargoCrateNode(Director d, CargoCrate crate)
        : base("kargo_kasasi", new SizeClassDef { Id = "kasa", Kg = (int)crate.Kg, PriceMult = 0, Scale = 1f }, MakeMesh(), null, VisualScale)
    {
        _d = d;
        Crate = crate;
        Name = $"kasa_{crate.Id}";
        AddToGroup("cargo_crates");
    }

    static Node3D MakeMesh()
    {
        var root = new Node3D();
        var glb = Assets.Load($"{PlaceableVisuals.Dir}/kargo_kasasi.glb");
        if (glb != null)
        {
            glb.Scale = Vector3.One / VisualScale;
            glb.Position = new Vector3(0, -0.3f / VisualScale, 0);
            root.AddChild(glb);
            return root;
        }
        // Yedek: kalas kasa (0,8 × 0,6 × 0,6 m), kooperatif mavisi serit
        float s = 1f / VisualScale;
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.8f, 0.58f, 0.6f) * s, MeshKit.At(0, 0, 0), Palette.Wood, 0.2f);
        for (int i = -1; i <= 1; i++)
            kit.Pillow(new Vector3(0.84f, 0.07f, 0.64f) * s, MeshKit.At(0, i * 0.2f * s, 0), Palette.Wood.Lerp(Palette.Shadow, 0.25f), 0.2f);
        kit.Pillow(new Vector3(0.3f, 0.3f, 0.02f) * s, MeshKit.At(0, 0, 0.31f * s), Palette.CoopBlue, 0.3f);
        root.AddChild(kit.Instance());
        return root;
    }

    public override void _Ready()
    {
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 30, OutlineSize = 10, PixelSize = 0.006f,
            Modulate = Palette.Warning, OutlineModulate = Palette.NightDeep, NoDepthTest = true, Position = new Vector3(0, 0.7f, 0),
        };
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        bool near = _d.Player.GlobalPosition.DistanceTo(GlobalPosition) < 4f;
        _label.Visible = (near || Held) && OnCart == null; // arabadayken arabanin etiketi yuku yazar
        if (_label.Visible) _label.Text = Loc.T("world.crate_label", Crate.Items.UsedSlots, _d.State.Cargo.Value(Crate), Crate.Kg.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
    }
}
