using Godot;

namespace Kabarma.Game;

/// <summary>Kazilabilir kaya: Kazma isiniyla kemirilir, her birim tas cantaya emilir (PLAN 1.4, 0:30).</summary>
public partial class Rock : Node3D
{
    public int Stones { get; private set; }
    /// <summary>Kazinca cantaya giren esya: "stone", "clay", "copper_ore" (URETIM 6 M1 kapsami, Kazma).</summary>
    public string Item { get; }
    readonly int _max;
    readonly float _size;
    float _wear;
    Node3D _mesh;
    ShaderMaterial _mat;
    float _flash;

    public Rock(int stones, float size, ulong seed, string item = "stone")
    {
        Stones = _max = stones;
        _size = size;
        Item = item;
        var r = new RandomNumberGenerator { Seed = seed };
        // M0 denemesi #1: Tripo'dan gelen dokulu kaya (uretim hattinda 3.879 ucgen, 1K doku). Yoksa koddan kit.
        // Kil ve bakir YER TUTUCU: koddan, renkle ayrilir (dunya akisi kil yatagi ve bakir damariyla degistirir).
        var model = item == "stone" ? Assets.Load("res://assets/models/kaya/kaya.glb") : null;
        if (model != null)
        {
            _mesh = new Node3D();
            model.Scale = Vector3.One * size;
            model.Rotation = new Vector3(0, r.Randf() * Mathf.Tau, 0);
            _mesh.AddChild(model);
            AddChild(_mesh);
        }
        else
        {
            var kit = new MeshKit();
            var (body0, body1, vein) = item switch
            {
                "clay" => (Palette.Soil, Palette.SoilWet, Palette.Soil.Lerp(Palette.Accent, 0.3f)),
                "copper_ore" => (Palette.Rock, Palette.RockDark, Palette.Accent),
                _ => (Palette.Rock, Palette.RockDark, Palette.Warning.Lerp(Palette.Rock, 0.4f)),
            };
            kit.Pillow(new Vector3(1.8f, 1.3f, 1.5f) * size, MeshKit.At(new Vector3(0, 0.55f * size, 0), new Vector3(0, r.Randf() * 3f, 0.1f)), body0, 0.5f, 10, 6);
            kit.Pillow(new Vector3(1.0f, 0.9f, 1.0f) * size, MeshKit.At(new Vector3(0.7f * size, 0.35f * size, 0.4f * size), new Vector3(0.2f, r.Randf() * 3f, 0)), body1, 0.5f, 8, 5);
            kit.Pillow(new Vector3(0.45f, 0.35f, 0.4f) * size, MeshKit.At(new Vector3(-0.3f * size, 1.15f * size, 0.1f)), vein, 0.5f, 8, 4);
            _mat = MeshKit.NewLiveMaterial();
            _mesh = kit.Instance(_mat);
            AddChild(_mesh);
        }
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.85f * size }, Position = new Vector3(0, 0.6f * size, 0) });
        AddChild(body);
        AddToGroup("rocks");
    }

    public Vector3 Center => GlobalPosition + new Vector3(0, 0.6f * _size, 0);
    public float Radius => 1.0f * _size;

    /// <returns>Bu karede kopan tas sayisi.</returns>
    public int Mine(float amount)
    {
        if (Stones <= 0) return 0;
        _wear += amount;
        _flash = 0.6f;
        int got = 0;
        while (_wear >= 1f && Stones > 0) { _wear -= 1f; Stones--; got++; }
        float k = 0.45f + 0.55f * Stones / (float)_max;
        _mesh.Scale = Vector3.One * k;
        return got;
    }

    public override void _Process(double delta)
    {
        if (_flash > 0) { _flash = Mathf.Max(0, _flash - (float)delta * 3f); _mat?.SetShaderParameter("flash", _flash * 0.4f); }
        if (Stones <= 0 && _flash <= 0) QueueFree();
        // titreme
        if (_flash > 0) _mesh.Position = new Vector3(Mathf.Sin(Time.GetTicksMsec() * 0.09f), 0, Mathf.Cos(Time.GetTicksMsec() * 0.11f)) * 0.04f * _flash;
    }
}
