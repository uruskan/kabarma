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
        var coop = d.State.Coop;
        if (def.Id == "yemlik") { Feeder = new Feeder { Food = d.Data.Pen.Animals[0].Food, Capacity = d.Data.Pen.FeederCapacity }; coop.Feeders.Add(Feeder); }
        if (def.Id == "folluk") { Nest = new Nest { Slots = d.Data.Pen.NestSlots }; coop.Nests.Add(Nest); }
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
        int n = CoopRules.Fill(Feeder, _d.State.Backpack);
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
        _label.Text = Feeder != null ? $"{Def.Name}\n{ItemNames.Of(Feeder.Food)} {Feeder.Amount}/{Feeder.Capacity}"
            : Nest != null ? $"{Def.Name}\nYumurta {Nest.Eggs}/{Nest.Slots}"
            : Kasa == null && Def.Id == "ahsap_kapi" ? $"{Def.Name}\nKapı Kasası'na oturur" : Def.Name;
        _label.Visible = near || Feeder != null || Nest != null;
        _label.Modulate = near ? Palette.Warning : Palette.OffWhite;
        if (_bounce > 0)
        {
            _bounce = Mathf.Max(0, _bounce - dt * 4f);
            float k = Mathf.Sin(_bounce * Mathf.Pi) * 0.07f;
            _visual.Scale = new Vector3(1 + k, 1 - k, 1 + k);
        }
    }
}
