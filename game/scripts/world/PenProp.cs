using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Istasyon olmayan yerlestirilebilir (ICERIK 10.2): Yemlik (icine yem konur), Folluk (agildaki Gidak buraya
/// yumurtlar, 6 yuva), Ahsap Kapi (Kapi Kasasi'na oturur, E ile acilir). Modeller yapi kitinin kalas dilinden
/// (pipeline/placeables_kit.py); yoksa koddan kutu. Kural durumu Sim'de (Feeder, Nest).
/// </summary>
public partial class PenProp : Node3D
{
    public PlaceableDef Def { get; }
    public Feeder Feeder { get; }
    public Nest Nest { get; }
    /// <summary>Ahsap Kapi'nin oturdugu kasa (yoksa null).</summary>
    public BuildPiece Kasa { get; set; }
    public bool DoorOpen { get; private set; }
    readonly Director _d;
    readonly PenSystem _pens;
    Node3D _visual, _hinge;
    CollisionShape3D _doorCol;
    Label3D _label;
    float _bounce;

    public PenProp(Director d, PenSystem pens, PlaceableDef def)
    {
        _d = d; _pens = pens; Def = def;
        Name = def.Id;
        // M2: Sim nesneleri Ranch'tan (kimlikli, konumlu; agil uyeligi konumdan). Konum PenSystem.PlaceProp'ta SyncSim ile.
        var ranch = d.State.Ranch;
        if (def.Id == "yemlik") { Feeder = ranch.AddFeeder(0, 0); Feeder.Food = d.Data.Pen.Animals[0].Food; }
        if (def.Id == "folluk") Nest = ranch.AddNest(0, 0);
        if (def.Id == "suluk") Trough = ranch.AddTrough(0, 0);
        if (def.Id == "kasinma_fircasi") Brush = ranch.AddBrush(0, 0);
        if (def.Id == "ari_kovani") Hive = ranch.AddHive(0, 0);
        if (def.Id == d.Data.Husbandry.Combat.Flag) Flag = ranch.AddFlag(0, 0); // M3: Savas Agili Bayragi
        if (def.Id == d.Data.Husbandry.Combat.Post) Post = ranch.AddPost(0, 0); // M3: Baglama Diregi
    }

    public WarFlag Flag { get; }
    public TiePost Post { get; }

    public Trough Trough { get; }
    public Brush Brush { get; }
    public Hive Hive { get; }

    /// <summary>Sim nesnesinin konumunu dugumun konumuna esitler.</summary>
    public void SyncSim()
    {
        float x = GlobalPosition.X, z = GlobalPosition.Z;
        if (Feeder != null) { Feeder.X = x; Feeder.Z = z; }
        if (Nest != null) { Nest.X = x; Nest.Z = z; }
        if (Trough != null) { Trough.X = x; Trough.Z = z; }
        if (Brush != null) { Brush.X = x; Brush.Z = z; }
        if (Hive != null) { Hive.X = x; Hive.Z = z; }
        if (Flag != null) { Flag.X = x; Flag.Z = z; }
        if (Post != null) { Post.X = x; Post.Z = z; }
    }

    public override void _Ready()
    {
        _visual = Station.MakeVisual(Def);
        if (Def.Id == "ahsap_kapi")
        {
            // Kapi kanadi mentesede doner: model kanadin sol kenarina gore kayik (kasa acikligi 1,5 m).
            _hinge = new Node3D { Name = "Hinge", Position = new Vector3(-0.75f, 0, 0) };
            AddChild(_hinge);
            _visual.Position = new Vector3(0.75f, 0, 0);
            _hinge.AddChild(_visual);
            var body = new StaticBody3D();
            _doorCol = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.5f, 2.3f, 0.1f) }, Position = new Vector3(0.75f, 1.15f, 0) };
            body.AddChild(_doorCol);
            _hinge.AddChild(body);
        }
        else
        {
            AddChild(_visual);
            var aabb = new Aabb(); bool first = true;
            foreach (var mi in _visual.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                var b = mi.GlobalTransform * mi.GetAabb();
                b = new Aabb(b.Position - GlobalPosition, b.Size);
                aabb = first ? b : aabb.Merge(b); first = false;
            }
            if (!first)
            {
                var body = new StaticBody3D();
                body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = aabb.Size * new Vector3(0.9f, 1f, 0.9f) }, Position = aabb.GetCenter() });
                AddChild(body);
            }
        }
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 36, OutlineSize = 10, PixelSize = 0.006f,
            Modulate = Palette.OffWhite, OutlineModulate = Palette.NightDeep, NoDepthTest = true,
            Position = new Vector3(0, Def.Id == "ahsap_kapi" ? 2.7f : 1.3f, 0),
        };
        AddChild(_label);
    }

    /// <summary>Ahsap Kapi'yi kasaya oturtur: kasanin acikligina hizalanir, gecit kapali olur.</summary>
    public void FitInto(BuildPiece kasa, PenSystem.Gate gate)
    {
        Kasa = kasa;
        gate.Door = this;
        GlobalTransform = kasa.GlobalTransform;
        DoorOpen = false;
        _pens.SyncDoor(kasa);
    }

    public void ToggleDoor()
    {
        if (Kasa == null || _hinge == null) return;
        DoorOpen = !DoorOpen;
        _hinge.CreateTween().TweenProperty(_hinge, "rotation:y", DoorOpen ? Mathf.DegToRad(_d.Data.Pen.GateOpenDegrees) : 0f, _d.Data.Pen.GateSwingSeconds)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _doorCol.SetDeferred(CollisionShape3D.PropertyName.Disabled, DoorOpen);
        _pens.SyncDoor(Kasa);
        Sfx.Play("place", GlobalPosition + Vector3.Up, -4f);
        GD.Print($"DOOR {(DoorOpen ? "open" : "closed")}");
    }

    /// <summary>Yemlik'e cantadan yem koyar.</summary>
    public void Fill()
    {
        var food = _pens.FoodFor(this);
        if (food == null) return;
        int n = HusbandryRules.Fill(Feeder, _d.State.Backpack, food); // M2: bos Yemlik konan yemin turunu alir
        if (n <= 0) return;
        Bounce();
        Fx.FlyTo(_d.Player.BackpackPoint.GlobalPosition, () => GlobalPosition + Vector3.Up * 0.6f, Palette.Warning, 0.1f);
        Sfx.Play("place", GlobalPosition, -2f);
        GD.Print($"FEEDER +{n} {Feeder.Food} -> {Feeder.Amount}");
    }

    /// <summary>Folluk'taki yumurtalari cantaya alir.</summary>
    public void Collect()
    {
        var product = _d.Data.Pen.Animals[0].Product;
        int n = CoopRules.Collect(Nest, _d.State.Backpack, product);
        if (n <= 0) return;
        Bounce();
        Fx.FlyTo(GlobalPosition + Vector3.Up * 0.4f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.OffWhite, 0.12f);
        Sfx.Play("pickup", GlobalPosition, -2f);
        Events.RaiseGathered(product, n);
        GD.Print($"NEST collected {n} eggs={_d.State.Eggs}");
    }

    public void Bounce() => _bounce = 1f;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool near = new Vector2(_d.Player.GlobalPosition.X - GlobalPosition.X, _d.Player.GlobalPosition.Z - GlobalPosition.Z).Length() < _d.Data.Pen.InteractRange + 1f;
        _label.Text = Feeder != null ? (Feeder.Amount == 0 ? Loc.T("ranch.label_empty", Def.Name) : $"{Def.Name}\n{ItemNames.Of(Feeder.Food)} {Feeder.Amount}/{Feeder.Capacity}")
            : Nest != null ? Loc.T("ranch.label_eggs", Def.Name, Nest.Eggs, Nest.Slots)
            : Trough != null ? Loc.T("ranch.label_water", Def.Name, Trough.Water.ToString("0", System.Globalization.CultureInfo.InvariantCulture), Trough.Capacity)
            : Hive != null ? Loc.T("ranch.label_hive", Def.Name, (Hive.AnimalId == 0 ? Loc.T("ranch.empty") : Loc.T("ranch.hive_settled")))
            : Post != null ? Loc.T("ranch.label_hive", Def.Name, (Post.AnimalId == 0 ? Loc.T("ranch.empty") : Loc.T("ranch.animal_tied")))
            : Flag != null ? Loc.T("ranch.label_war_pen", Def.Name)
            : Post != null ? Loc.T("ranch.label_hive", Def.Name, (Post.AnimalId == 0 ? Loc.T("ranch.empty") : Loc.T("ranch.animal_tied")))
            : Flag != null ? Loc.T("ranch.label_war_pen", Def.Name)
            : Kasa == null && Def.Id == "ahsap_kapi" ? Loc.T("ranch.label_gate_frame", Def.Name) : Def.Name;
        _label.Visible = near || Feeder != null || Nest != null || Trough != null || Hive != null;
        _label.Modulate = near ? Palette.Warning : Palette.OffWhite;
        if (_bounce > 0)
        {
            _bounce = Mathf.Max(0, _bounce - dt * 4f);
            float k = Mathf.Sin(_bounce * Mathf.Pi) * 0.07f;
            _visual.Scale = new Vector3(1 + k, 1 - k, 1 + k);
        }
    }
}
