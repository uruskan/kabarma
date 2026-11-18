using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Uzak oyuncunun gorunusu: oyuncu modeli (yoksa koddan tulumlu govde), ad etiketi, Capa isini, baygin pozu. Kareler
/// <see cref="NetFile.InterpDelay"/> geriden ara degerlenir; oyuncu durunca son kareye tam oturur (senkron olcutu).
/// Yetki tasimaz: fizik, hasar ve istek yok; yalniz gosterir.
/// </summary>
public partial class NetAvatar : Node3D
{
    readonly NetSync _net;
    public byte Peer { get; }
    public string PlayerName { get => _label?.Text ?? _name; set { _name = value; if (_label != null) _label.Text = value; } }
    string _name;
    public string Held { get; set; } = "";
    public bool Down { get; private set; }
    public bool Talking { get; private set; }
    public bool Slapping { get; private set; }
    public PlayerFrame Latest { get; private set; }
    public bool HasFrame { get; private set; }

    readonly List<(double t, PlayerFrame f)> _buf = new();
    Node3D _visual, _model;
    AnimationPlayer _anim;
    Label3D _label;
    MeshInstance3D _beam;
    StandardMaterial3D _beamMat;
    string _clip = "";
    PlayerAnimsFile _clips;

    public NetAvatar(NetSync net, byte peer, string name) { _net = net; Peer = peer; _name = name; }
    public NetAvatar() { }

    public override void _Ready()
    {
        _clips = _net.D.Data.PlayerAnims;
        _visual = new Node3D { Name = "Visual" };
        AddChild(_visual);
        string path = string.IsNullOrEmpty(_clips.Model) ? "res://assets/models/player/player.glb" : _clips.Model;
        if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path) is PackedScene scene)
        {
            _model = scene.Instantiate<Node3D>();
            _visual.AddChild(_model);
            var aabb = Bounds(_model);
            if (aabb.Size.Y > 0.5f && aabb.Size.Y < 5f) { float s = 1.6f / aabb.Size.Y; _model.Scale = Vector3.One * s; _model.Position = new Vector3(0, -aabb.Position.Y * s, 0); }
            _anim = _model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
            if (_anim != null) foreach (var c in _clips.Clips) if (c.Loop && _anim.HasAnimation(c.Id)) _anim.GetAnimation(c.Id).LoopMode = Animation.LoopModeEnum.Linear;
        }
        else
        {
            // Yer tutucu: koddan tulumlu govde (Player.BuildProceduralBody ile ayni renkler).
            var kit = new MeshKit();
            kit.Pillow(new Vector3(0.62f, 0.9f, 0.45f), MeshKit.At(0, 0.6f, 0), Palette.CoopBlue, 0.5f);
            kit.Sphere(0.42f, MeshKit.At(0, 1.33f, 0.02f), Palette.OffWhite, 16, 8);
            kit.Pillow(new Vector3(0.58f, 0.36f, 0.2f), MeshKit.At(0, 1.33f, 0.33f), Palette.NightDeep.Lerp(Palette.CoopBlue, 0.25f), 0.45f);
            _visual.AddChild(kit.Instance());
        }
        _label = new Label3D
        {
            Text = _name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, 2.15f, 0),
            FontSize = 42, OutlineSize = 10, Modulate = Palette.OffWhite, NoDepthTest = true, PixelSize = 0.006f,
        };
        AddChild(_label);
        _beamMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, EmissionEnergyMultiplier = 2f };
        _beam = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.08f, Height = 1f, RadialSegments = 8, Rings = 1 }, MaterialOverride = _beamMat, TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_beam);
        Visible = false; // ilk kare gelene kadar
    }

    static Aabb Bounds(Node3D root)
    {
        Aabb? box = null;
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var b = mi.Transform * mi.GetAabb();
            box = box == null ? b : box.Value.Merge(b);
        }
        return box ?? new Aabb();
    }

    public void Push(PlayerFrame f, double now)
    {
        if (_buf.Count > 0 && NetRules.Snap(_buf[^1].f.X, _buf[^1].f.Y, _buf[^1].f.Z, f.X, f.Y, f.Z, _net.Cfg)) _buf.Clear(); // isinlandi: ara degerleme yok
        _buf.Add((now, f));
        while (_buf.Count > 32) _buf.RemoveAt(0);
        Latest = f;
        HasFrame = true;
        Down = f.Has(NetFrameFlags.Down);
        Talking = f.Has(NetFrameFlags.Talking);
        Slapping = f.Has(NetFrameFlags.Slapping);
    }

    public override void _Process(double delta)
    {
        if (_buf.Count == 0) return;
        Visible = true;
        double t = NetSync.Now - _net.Cfg.InterpDelay;
        PlayerFrame a = _buf[0].f, b = a;
        float k = 0;
        if (t >= _buf[^1].t) a = b = _buf[^1].f;
        else
        {
            for (int i = _buf.Count - 1; i > 0; i--)
            {
                if (_buf[i - 1].t <= t)
                {
                    a = _buf[i - 1].f; b = _buf[i].f;
                    double span = _buf[i].t - _buf[i - 1].t;
                    k = span > 1e-6 ? (float)((t - _buf[i - 1].t) / span) : 1f;
                    break;
                }
            }
        }
        var pos = new Vector3(Mathf.Lerp(a.X, b.X, k), Mathf.Lerp(a.Y, b.Y, k), Mathf.Lerp(a.Z, b.Z, k));
        GlobalPosition = pos;
        _visual.Rotation = new Vector3(Down ? -1.4f : 0f, Mathf.LerpAngle(a.Yaw, b.Yaw, k), 0);
        float speed = new Vector2(b.X - a.X, b.Z - a.Z).Length() / Mathf.Max(0.001f, 1f / _net.Cfg.FrameHz);
        PlayClip(b, speed);
        UpdateBeam(b);
        _label.Modulate = Down ? Palette.Warning : Talking ? Palette.MayaTeal : Palette.OffWhite;
        _label.Text = _name + (Down ? "  (baygın: E basılı tut)" : "") + (Talking ? "  🔊" : "");
    }

    void PlayClip(PlayerFrame f, float speed)
    {
        if (_anim == null) return;
        string id = f.Anim < _clips.Clips.Count ? _clips.Clips[f.Anim].Id : speed > 0.3f ? (f.Has(NetFrameFlags.Sprint) ? "run" : "walk") : "idle";
        var (name, _) = _clips.Resolve(id);
        if (!_anim.HasAnimation(name)) name = _anim.HasAnimation("idle") ? "idle" : "";
        if (name.Length == 0 || name == _clip) return;
        _clip = name;
        _anim.Play(name, 0.2);
    }

    void UpdateBeam(PlayerFrame f)
    {
        bool on = f.Has(NetFrameFlags.Firing) && !Down;
        _beam.Visible = on;
        if (!on) return;
        int head = f.Head;
        var color = head < Capa.HeadColors.Length ? Capa.HeadColors[head] : Palette.Warning;
        var from = GlobalPosition + Vector3.Up * 1.1f + new Vector3(Mathf.Sin(f.Yaw), 0, Mathf.Cos(f.Yaw)) * 0.5f;
        var dir = new Vector3(Mathf.Sin(f.Yaw) * Mathf.Cos(f.Pitch), Mathf.Sin(f.Pitch), Mathf.Cos(f.Yaw) * Mathf.Cos(f.Pitch)).Normalized();
        float len = 5f;
        _beam.GlobalPosition = from + dir * len * 0.5f;
        var side = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up.Cross(dir).Normalized();
        _beam.GlobalBasis = new Basis(side, dir * len, side.Cross(dir));
        _beamMat.AlbedoColor = new Color(color, 0.6f);
        _beamMat.Emission = color;
    }
}
