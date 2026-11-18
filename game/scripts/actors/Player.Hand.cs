using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Elde tutulan esya ile oyuncu arasindaki tek dikis (URETIM 6 M1 karari 23; kemer akisi). Player.Grip.cs'in ic mantigina
/// dokunmaz: Capa elde degilken sirta gecer (Grip'in kendi "sirtta" durumu, uyku pozuyla ayni yol) ve elde bir nesne
/// (Meshale, Insa Plani) varsa sag elin soketine oturur (grips.json items: modelPos, modelRotDeg, right). Meshale elde isik
/// verir (placeables.json mesale.lightRange).
/// </summary>
public partial class Player
{
    bool _capaStowed;
    Node3D _handItem;
    string _handItemId;
    OmniLight3D _handLight;

    /// <summary>Elde Capa yok (kemerde baska yuva secili): Capa sirtta.</summary>
    public bool CapaStowed => _capaStowed;

    /// <summary>Director her kare cagirir: elde tutulan esyanin kimligi (kemerin secili yuvasi; null: bos el).</summary>
    public void SetHeldItem(string id, PlaceableDef torch)
    {
        bool stow = id != PlanTools.Capa;
        if (stow) _capaOnBack = true;                 // uyku pozunun da kullandigi yol (Player.Grip.cs)
        else if (_capaStowed) _capaOnBack = false;    // Capa ele geri geldi
        _capaStowed = stow;

        string show = id != null && _grips.Items.ContainsKey(id) && id != PlanTools.Capa ? id : null;
        if (show != _handItemId) BuildHandItem(show);
        if (_handItem == null || Rig == null) return;
        var def = _grips.Items[_handItemId];
        var grip = def.Right != null ? Frame(def.Right) : Transform3D.Identity;
        _handItem.GlobalTransform = SocketWorld(Rig.HandWorld, _sockR) * grip.AffineInverse();
        if (_handLight != null && torch != null) _handLight.OmniRange = torch.LightRange;
    }

    void BuildHandItem(string id)
    {
        _handItem?.QueueFree();
        _handItem = null; _handLight = null; _handItemId = id;
        if (id == null || !_grips.Items.TryGetValue(id, out var def)) return;
        _handItem = new Node3D { Name = "HandItem", TopLevel = true };
        var model = new Node3D
        {
            Position = new Vector3(def.ModelPos[0], def.ModelPos[1], def.ModelPos[2]),
            RotationDegrees = new Vector3(def.ModelRotDeg[0], def.ModelRotDeg[1], def.ModelRotDeg[2]),
        };
        _handItem.AddChild(model);
        if (!string.IsNullOrEmpty(def.Model) && ResourceLoader.Exists(def.Model) && ResourceLoader.Load<PackedScene>(def.Model) is PackedScene ps)
            model.AddChild(ps.Instantiate());
        else model.AddChild(HandPlaceholder(id));
        if (id == "mesale")
        {
            _handLight = new OmniLight3D { LightColor = new Color("FFB070"), LightEnergy = 1.6f, ShadowEnabled = false, Position = new Vector3(0, 0.38f, 0) };
            model.AddChild(_handLight);
        }
        Visual.AddChild(_handItem);
    }

    /// <summary>Modeli olmayan el esyasi icin kodla cizim: Meshale (sap ve alev), Insa Plani (rulo kagit).</summary>
    static Node3D HandPlaceholder(string id)
    {
        var root = new Node3D();
        if (id == "mesale")
        {
            root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.018f, Height = 0.42f, RadialSegments = 8 }, Position = new Vector3(0, 0.12f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.Wood } });
            root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.05f, Height = 0.12f, RadialSegments = 8, Rings = 4 }, Position = new Vector3(0, 0.36f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("FFB070"), EmissionEnabled = true, Emission = new Color("FF8A3D"), EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        }
        else
        {
            root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.035f, Height = 0.36f, RadialSegments = 10 }, RotationDegrees = new Vector3(0, 0, 90), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("C9D9E4") } });
            root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.037f, BottomRadius = 0.037f, Height = 0.05f, RadialSegments = 10 }, RotationDegrees = new Vector3(0, 0, 90), MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.CoopBlue } });
        }
        return root;
    }
}
