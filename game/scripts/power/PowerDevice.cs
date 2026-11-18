using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kurulmus guc ya da sinyal parcasinin gorunusu (GDD 11.1, 11.2). Durum Sim'de (PowerNode, kimlikle); bu dugum yalniz
/// gosterir: Ruzgar Gulu ve Hayvan Carki uretimle doner, lamba beslenince yanar, dugme ve plaka basilinca iner,
/// yakindayken ustunde durum yazar.
/// </summary>
public partial class PowerDevice : Node3D
{
    public int NodeId { get; }
    public PlaceableDef Def { get; }
    public PowerDeviceDef PDef { get; }
    readonly Director _d;
    Node3D _rotor, _knob;
    MeshInstance3D _bulb;
    StandardMaterial3D _bulbMat;
    OmniLight3D _light;
    Label3D _label;
    float _spin, _knobY, _bounce;

    public PowerDevice(Director d, PlaceableDef def, PowerDeviceDef pdef, int nodeId)
    {
        _d = d; Def = def; PDef = pdef; NodeId = nodeId;
        Name = $"{def.Id}_{nodeId}";
    }

    PowerNode Node => _d.State.Power.Get(NodeId);

    public Vector3 SocketWorld => GlobalTransform * PowerVisuals.Socket(Def.Id);

    public override void _Ready()
    {
        Callable.From(() => { if (IsInstanceValid(this) && IsInsideTree()) _d.World?.ClearGrass(GlobalPosition, Def.Radius + 0.2f); }).CallDeferred();
        var vis = PowerVisuals.Make(Def.Id);
        AddChild(vis);
        _rotor = vis.GetNodeOrNull<Node3D>("rotor");
        _knob = vis.GetNodeOrNull<Node3D>("knob");
        if (_knob != null) _knobY = _knob.Position.Y;
        _bulb = vis.GetNodeOrNull<MeshInstance3D>("bulb");
        _bulbMat = _bulb?.MaterialOverride as StandardMaterial3D;
        if (_bulb != null)
        {
            _light = new OmniLight3D { LightColor = new Color("FFE2A8"), LightEnergy = 0f, OmniRange = Mathf.Max(1f, PDef.LightRange), Position = _bulb.Position, ShadowEnabled = false, Name = "Light" };
            vis.AddChild(_light);
        }
        if (Def.Id != "basinc_plakasi" && PlaceableVisuals.Bounds(vis, out var aabb))
        {
            var body = new StaticBody3D();
            var size = aabb.Size * new Vector3(0.6f, 1f, 0.6f);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = aabb.GetCenter() });
            AddChild(body);
        }
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 34, OutlineSize = 12, PixelSize = 0.006f,
            Modulate = Palette.OffWhite, OutlineModulate = Palette.NightDeep, NoDepthTest = true, Visible = false,
            Position = new Vector3(0, PowerVisuals.Socket(Def.Id).Y + 0.7f, 0),
        };
        AddChild(_label);
    }

    public bool InReach(Vector3 p) => new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() < Def.Radius + 1.8f;

    public void Bounce() => _bounce = 1f;

    /// <summary>Kisa durum (etiket ve E paneli).</summary>
    public string Status()
    {
        var n = Node;
        if (n == null) return "";
        var net = _d.State.Power;
        var st = net.StatsOf(NodeId);
        switch (PDef.Kind)
        {
            case "generator":
                return PDef.Source == "animal" && !n.Occupied ? $"0 / {PDef.Watts:0} W · içinde hayvan yok" : $"{n.OutputW:0} / {PDef.Watts:0} W";
            case "battery":
                return $"{n.StoredWh:0} / {PDef.CapacityWh:0} Wh";
            case "consumer":
                if (!n.On) return "kapalı";
                if (n.SignalWired && n.Signal <= 0) return $"sinyal bekliyor · {PDef.Watts:0} W";
                return n.Powered ? $"yanıyor · {PDef.Watts:0} W" : n.Shed ? "güç yetmiyor" : net.UsedSockets(NodeId) == 0 ? "kablo yok" : "güç yok";
            case "switch":
                bool closed = n.SignalWired ? n.Signal > 0 : n.On;
                return (closed ? "kapalı (iletir)" : "açık (keser)") + (n.SignalWired ? $" · sinyal {n.Signal}" : "");
            case "button":
            case "plate":
                return $"sinyal {n.Signal}";
        }
        return st == null ? "kablo yok" : $"{st.ServedW:0} / {st.SupplyW:0} W";
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var n = Node;
        if (n == null) return;
        if (_rotor != null)
        {
            float k = PDef.Watts > 0 ? n.OutputW / PDef.Watts : 0f;
            _spin = Mathf.MoveToward(_spin, k * (PDef.Source == "animal" ? 3f : 9f), dt * 3f);
            if (PDef.Source == "animal") _rotor.RotateObjectLocal(Vector3.Right, _spin * dt);
            else _rotor.RotateObjectLocal(Vector3.Back, _spin * dt);
        }
        if (_bulb != null)
        {
            bool lit = n.Powered;
            float e = Mathf.MoveToward(_light.LightEnergy, lit ? 1.8f : 0f, dt * 6f);
            _light.LightEnergy = e;
            if (_bulbMat != null) _bulbMat.EmissionEnergyMultiplier = e * 2f;
        }
        if (_knob != null)
        {
            float target = n.Signal > 0 ? _knobY - (PDef.Kind == "plate" ? 0.04f : 0f) : _knobY;
            var p = _knob.Position;
            p.Y = Mathf.MoveToward(p.Y, target, dt * 0.5f);
            if (PDef.Kind == "button") p.Z = Mathf.MoveToward(p.Z, n.Signal > 0 ? 0.08f : 0.12f, dt * 0.6f);
            _knob.Position = p;
        }
        if (_bounce > 0)
        {
            _bounce = Mathf.Max(0, _bounce - dt * 4f);
            float k = Mathf.Sin(_bounce * Mathf.Pi) * 0.06f;
            GetChild<Node3D>(0).Scale = new Vector3(1 + k, 1 - k, 1 + k);
        }
        bool near = InReach(_d.Player.GlobalPosition);
        _label.Visible = near;
        if (near)
        {
            string act = PDef.Kind == "button" ? "E: bas" : "E: ayar";
            _label.Text = $"{Def.Name}\n{Status()}\n{act}";
            _label.Modulate = n.Shed ? Palette.HoldingRed : Palette.OffWhite;
        }
    }
}
