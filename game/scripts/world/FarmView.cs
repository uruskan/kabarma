using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Tarlanin gorunusu: surulmus kareler, filizler, olgun salgamlar, kabarik salgamlar.</summary>
public partial class FarmView : Node3D
{
    public Farm Farm { get; private set; }
    World _world;
    GameData _data;
    readonly Dictionary<(int, int), PlotView> _views = new();
    public IEnumerable<PlotView> Views => _views.Values;

    public void Init(Farm farm, World world, GameData data)
    {
        Farm = farm; _world = world; _data = data;
        farm.Tilled += p => { var v = new PlotView(p, this); _views[(p.X, p.Y)] = v; AddChild(v); v.GlobalPosition = CellCenter(p.X, p.Y); };
        farm.Planted += p => View(p)?.OnPlanted();
        farm.Watered += p => View(p)?.OnWatered();
        farm.Swelled += p => View(p)?.OnSwelled();
        farm.Eaten += p => View(p)?.OnEaten();
    }

    PlotView View(Plot p) => _views.GetValueOrDefault((p.X, p.Y));
    public PlotView View(int x, int y) => _views.GetValueOrDefault((x, y));

    public static (int x, int y) Cell(Vector3 world) => (Mathf.FloorToInt(world.X), Mathf.FloorToInt(world.Z));
    public Vector3 CellCenter(int x, int y) => _world.Ground(new Vector3(x + 0.5f, 0, y + 0.5f));

    public PlotView NearestPlanted(Vector3 from, bool swollenOnly = false)
    {
        PlotView best = null; float bd = float.MaxValue;
        foreach (var v in _views.Values)
        {
            if (!v.Plot.Planted || (swollenOnly && v.Plot.Swollen == null)) continue;
            if (v.Claimed) continue;
            float d = v.GlobalPosition.DistanceSquaredTo(from);
            if (d < bd) { bd = d; best = v; }
        }
        return best;
    }
}

public partial class PlotView : Node3D
{
    public Plot Plot { get; }
    readonly FarmView _owner;
    MeshInstance3D _soil;
    Node3D _crop;
    MeshInstance3D _cropMesh;
    ShaderMaterial _cropMat;
    ShaderMaterial _soilMat;
    float _swellT = -1;
    float _targetScale = 1f;
    public bool Claimed;          // bir Zipzip bunu yiyor
    public SwollenCrop Lifted;     // tarladan sokulmus kabarik urun
    float _flash;

    public PlotView(Plot p, FarmView owner)
    {
        Plot = p; _owner = owner;
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.94f, 0.22f, 0.94f), MeshKit.At(0, 0.0f, 0), Palette.Soil, 0.3f, 10, 4);
        kit.Pillow(new Vector3(0.7f, 0.08f, 0.16f), MeshKit.At(0, 0.1f, -0.22f), Palette.Soil.Lerp(Palette.Wood, 0.3f), 0.4f, 8, 4);
        kit.Pillow(new Vector3(0.7f, 0.08f, 0.16f), MeshKit.At(0, 0.1f, 0.22f), Palette.Soil.Lerp(Palette.Wood, 0.3f), 0.4f, 8, 4);
        _soilMat = MeshKit.NewLiveMaterial();
        _soilMat.SetShaderParameter("rim", 0f);
        _soil = kit.Instance(_soilMat);
        AddChild(_soil);
        Fx.Burst(Position, Palette.Soil.Lerp(Palette.Ground, 0.3f), 10, 3f, 0.08f, 0.5f);
    }

    public override void _Ready() => Fx.Burst(GlobalPosition + Vector3.Up * 0.1f, Palette.Soil.Lerp(Palette.Ground, 0.3f), 12, 3.5f, 0.09f, 0.5f);

    public void OnPlanted()
    {
        _crop = new Node3D();
        AddChild(_crop);
        var kit = new MeshKit();
        // Salgam: beyaz govde, mor omuz, tiknaz yapraklar.
        kit.Sphere(0.3f, MeshKit.At(new Vector3(0, 0.18f, 0), Vector3.Zero, new Vector3(1f, 0.95f, 1f)), Palette.TurnipBody, 12, 8);
        kit.Sphere(0.27f, MeshKit.At(new Vector3(0, 0.3f, 0), Vector3.Zero, new Vector3(1f, 0.55f, 1f)), Palette.TurnipTop, 12, 6);
        kit.Cylinder(0.0f, 0.06f, 0.16f, MeshKit.At(new Vector3(0, -0.06f, 0), new Vector3(Mathf.Pi, 0, 0)), Palette.TurnipBody, 6);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Tau / 4f + 0.4f;
            var basis = new Vector3(Mathf.Cos(a) * 0.45f, 0, Mathf.Sin(a) * 0.45f);
            kit.Pillow(new Vector3(0.14f, 0.5f, 0.07f), MeshKit.At(new Vector3(Mathf.Sin(a) * 0.1f, 0.62f, Mathf.Cos(a) * 0.1f), new Vector3(Mathf.Cos(a) * 0.4f, a, -Mathf.Sin(a) * 0.4f)), Palette.Plant.Lerp(Palette.Shadow, i % 2 * 0.12f), 0.5f, 8, 4);
        }
        _cropMat = MeshKit.NewLiveMaterial();
        _cropMesh = kit.Instance(_cropMat);
        _crop.AddChild(_cropMesh);
        _crop.Scale = Vector3.One * 0.15f;
        Fx.Burst(GlobalPosition + Vector3.Up * 0.2f, Palette.Plant, 8, 2.5f, 0.06f, 0.4f);
    }

    public void OnWatered()
    {
        _flash = 0.5f;
        Fx.Burst(GlobalPosition + Vector3.Up * 0.3f, new Color("8CC8E8"), 10, 2.5f, 0.06f, 0.5f);
    }

    public void OnSwelled()
    {
        _swellT = (float)GD.RandRange(0.0, 1.5); // hepsi ayni anda degil, dalga gibi
        _targetScale = Plot.Swollen?.Scale ?? 2.6f;
    }

    public void OnEaten()
    {
        if (_crop != null)
        {
            Fx.Burst(_crop.GlobalPosition + Vector3.Up * 0.5f, Palette.TurnipBody, 22, 5f, 0.12f, 0.6f);
            Fx.Burst(_crop.GlobalPosition + Vector3.Up * 0.5f, Palette.TurnipTop, 12, 4f, 0.1f, 0.6f);
            _crop.QueueFree();
            _crop = null;
        }
    }

    /// <summary>Kabarik urunu tarladan sok, yuk nesnesi yap.</summary>
    public SwollenCrop Lift()
    {
        if (_crop == null || Plot.Swollen == null || Lifted != null) return null;
        var size = _owner.Farm.Uproot(Plot.X, Plot.Y);
        if (size == null) return null;
        var mesh = _cropMesh;
        _crop.RemoveChild(mesh);
        var sc = new SwollenCrop(Plot.Crop!, size, mesh, _cropMat, _crop.Scale.X);
        GetParent().GetParent().AddChild(sc);
        sc.GlobalPosition = _crop.GlobalPosition;
        _crop.QueueFree();
        _crop = null;
        Lifted = sc;
        return sc;
    }

    public Vector3 CropTop => GlobalPosition + Vector3.Up * (0.4f * (_crop?.Scale.Y ?? 0.3f) + 0.2f);
    public float CropScale => _crop?.Scale.X ?? 0f;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_flash > 0) { _flash -= dt; _soilMat.SetShaderParameter("flash", Mathf.Max(0, _flash) * 0.3f); }
        // Islak toprak koyulasir: nem gorunur (menu yerine dunya, SANAT-SES §8.1)
        float wet = Mathf.Clamp(Plot.Moisture / 100f, 0, 1);
        ModulateSoil(wet);
        if (_crop == null) return;

        if (_swellT >= 0 && Plot.Swollen != null)
        {
            _swellT += dt;
            float k = Mathf.Clamp((_swellT - 1.5f) / 2.2f, 0, 1);
            if (k > 0 && k < 0.05f && !_soundDone) { _soundDone = true; Sfx.Play("swell", GlobalPosition, -4f); }
            // Elastik sisme: hedefe asar, geri yaylanir.
            float e = k >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * k) * Mathf.Cos(k * 10.5f);
            float s = Mathf.Lerp(1f, _targetScale, e);
            _crop.Scale = new Vector3(s, s * (1f + Mathf.Sin(_swellT * 3f) * 0.02f), s);
            _cropMat.SetShaderParameter("swell", Mathf.Min(1f, k * 1.5f));
            _cropMat.SetShaderParameter("glow", 0.25f + k * 0.85f);
            return;
        }
        float g = Plot.Growth;
        float scale = Plot.Stage switch
        {
            CropStage.Seed => Mathf.Lerp(0.15f, 0.3f, g / 0.2f),
            CropStage.Sprout => Mathf.Lerp(0.3f, 0.6f, (g - 0.2f) / 0.4f),
            CropStage.Young => Mathf.Lerp(0.6f, 0.9f, (g - 0.6f) / 0.4f),
            _ => 1f,
        };
        float cur = _crop.Scale.X;
        float ns = Mathf.Lerp(cur, scale, Mathf.Min(1f, dt * 4f));
        _crop.Scale = new Vector3(ns, ns, ns);
        // Olgunluk paltisi
        _cropMat.SetShaderParameter("flash", Plot.Stage == CropStage.Mature ? 0.05f + 0.05f * Mathf.Sin(Time.GetTicksMsec() * 0.004f) : 0f);
    }

    bool _soundDone;
    float _lastWet = -1;
    void ModulateSoil(float wet)
    {
        if (Mathf.Abs(wet - _lastWet) < 0.02f) return;
        _lastWet = wet;
        // Islak toprak koyulasir (kose rengi ayni, ton carpani degisir).
        _soilMat.SetShaderParameter("tint", Colors.White.Lerp(new Color(0.62f, 0.6f, 0.68f), wet));
    }
}

/// <summary>Kabarik urun: yuk nesnesi. Cantaya girmez, Cekim isiniyla tasinir (GDD 5).</summary>
public partial class SwollenCrop : Node3D
{
    public string CropId { get; }
    public SizeClassDef Size { get; }
    public bool Held;
    public bool Sold;
    readonly ShaderMaterial _mat;
    readonly Node3D _pivot;
    float _t;
    public float Radius { get; }

    public SwollenCrop(string crop, SizeClassDef size, MeshInstance3D mesh, ShaderMaterial mat, float scale)
    {
        CropId = crop; Size = size; _mat = mat;
        _pivot = new Node3D();
        AddChild(_pivot);
        _pivot.AddChild(mesh);
        _pivot.Scale = Vector3.One * scale;
        Radius = 0.35f * scale;
        AddToGroup("swollen");
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _mat.SetShaderParameter("glow", Held ? 1.1f : 0.8f);
        _pivot.Rotation = Held ? new Vector3(Mathf.Sin(_t * 2f) * 0.12f, _t * 0.6f, Mathf.Cos(_t * 1.7f) * 0.1f) : Vector3.Zero;
    }
}
