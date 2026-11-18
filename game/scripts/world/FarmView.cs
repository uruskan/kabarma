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
        farm.Tilled += p => _world.MarkSoil(CellCenter(p.X, p.Y), 0.62f); // zeminde surulmus toprak katmani, ot kalkar (gorsel-dunya)
        farm.Planted += p => View(p)?.OnPlanted();
        farm.Watered += p => View(p)?.OnWatered();
        farm.Swelled += p => View(p)?.OnSwelled();
        farm.Eaten += p => View(p)?.OnEaten();
        // bicilen ekin tarladan kalkar (parca efekti ayni); kalici cali yerinde kalir, Genc evreye doner (tarim M2)
        farm.Harvested += p => { if (p.Planted) View(p)?.OnRegrow(); else View(p)?.OnEaten(); };
        // GDD 8.4: safakta sonme saati baslar; 1 gun icinde toplanmayan kabarik urun soner.
        farm.Deflated += p => View(p)?.OnDeflated();
        Events.Dawn += farm.OnDawn;
        InitTarim(); // FarmView.Tarim.cs: yabani bitkiler, isirik, Korkuluk/kovan taramasi, tohum secimi, bilgi karti
    }

    /// <summary>Vurus hissi (Capa.Feel.cs) icin: tarla Director'un cocugu.</summary>
    internal Capa Capa => (GetParent() as Director)?.Capa;

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
    Node3D _cropMesh;
    ShaderMaterial _cropMat;   // koddan kurulan yedek salgam
    Swellable _cropSw;         // dokulu salgam modeli (M1 altin varlik)

    void SetCrop(float swell = -1, float glow = -1, float flash = -1)
    {
        if (_cropSw != null) { _cropSw.Set(swell: swell, glow: glow, flash: flash, push: swell >= 0 ? 0.03f : -1f, wobble: 0.025f); return; }
        if (_cropMat == null) return;
        if (swell >= 0) _cropMat.SetShaderParameter("swell", swell);
        if (glow >= 0) _cropMat.SetShaderParameter("glow", glow);
        if (flash >= 0) _cropMat.SetShaderParameter("flash", flash);
    }
    ShaderMaterial _soilMat;
    float _swellT = -1;
    float _targetScale = 1f;
    public bool Claimed;          // bir Zipzip bunu yiyor
    public SwollenCrop Lifted;     // tarladan sokulmus kabarik urun
    float _flash;

    public PlotView(Plot p, FarmView owner)
    {
        Plot = p; _owner = owner;
        _soilMat = MeshKit.NewLiveMaterial();
        if (p.Wild) return; // yabani bitki: surulmus toprak yok
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.94f, 0.22f, 0.94f), MeshKit.At(0, 0.0f, 0), Palette.Soil, 0.3f, 10, 4);
        kit.Pillow(new Vector3(0.7f, 0.08f, 0.16f), MeshKit.At(0, 0.1f, -0.22f), Palette.Soil.Lerp(Palette.Wood, 0.3f), 0.4f, 8, 4);
        kit.Pillow(new Vector3(0.7f, 0.08f, 0.16f), MeshKit.At(0, 0.1f, 0.22f), Palette.Soil.Lerp(Palette.Wood, 0.3f), 0.4f, 8, 4);
        _soilMat.SetShaderParameter("rim", 0f);
        _soil = kit.Instance(_soilMat);
        AddChild(_soil);
        // Surme efekti (toprak parcasi, ezilip esneme, ses) Capa.Toprak'ta: feel.json sources.toprak.
    }

    public void OnPlanted()
    {
        if (_crop != null) { _crop.QueueFree(); _crop = null; _cropSw = null; _cropMat = null; }
        _crop = new Node3D();
        AddChild(_crop);
        bool turnip = Plot.Crop is null or "turnip";
        // Salgam altin modeli; Alaca salgam ve diger ekinler koddan (tint alir; FarmView.Meshes.cs, YER TUTUCU)
        var model = turnip && Plot.Alaca == null ? Assets.Load("res://assets/models/salgam/salgam.glb") : null;
        if (!turnip)
        {
            _cropMat = MeshKit.NewLiveMaterial();
            _cropMesh = CropVisuals.Make(Plot.Crop!, _cropMat);
            _cropMesh.Rotation = new Vector3(0, (float)GD.RandRange(0, Mathf.Tau), 0);
        }
        else if (model != null)
        {
            // Yumru yarisi toprakta; her salgam biraz farkli doner.
            model.Position = new Vector3(0, -0.1f, 0);
            model.Rotation = new Vector3(0, (float)GD.RandRange(0, Mathf.Tau), 0);
            _cropMesh = model;
            _cropSw = new Swellable(model, 6f);
        }
        else
        {
            var kit = new MeshKit();
            kit.Sphere(0.3f, MeshKit.At(new Vector3(0, 0.18f, 0), Vector3.Zero, new Vector3(1f, 0.95f, 1f)), Palette.TurnipBody, 12, 8);
            kit.Sphere(0.27f, MeshKit.At(new Vector3(0, 0.3f, 0), Vector3.Zero, new Vector3(1f, 0.55f, 1f)), Palette.TurnipTop, 12, 6);
            _cropMat = MeshKit.NewLiveMaterial();
            _cropMesh = kit.Instance(_cropMat);
        }
        _crop.AddChild(_cropMesh);
        _crop.Scale = Vector3.One * (Plot.Wild ? 1f : 0.15f);
        if (Plot.Alaca != null) _cropMat?.SetShaderParameter("tint", CropVisuals.AlacaTint(Plot.Alaca)); // GDD 8.3 Alaca gorunusu
    }

    /// <summary>Kalici cali bicildi: yerinde kalir, Genc boya iner (tarim M2).</summary>
    public void OnRegrow()
    {
        if (_crop == null) return;
        Fx.Burst(_crop.GlobalPosition + Vector3.Up * 0.5f, CropVisuals.ProduceColor(Plot.Crop), 14, 4f, 0.1f, 0.5f);
        _owner.Capa?.FeelHit("bicme_ekin", _crop.GlobalPosition + Vector3.Up * 0.3f, Palette.Plant, squash: this);
    }

    /// <summary>Gunduz zararlisi isirdi (tarim M2): yaprak parcasi, kucuk sarsinti.</summary>
    public void OnBitten()
    {
        if (_crop == null) return;
        Fx.Burst(_crop.GlobalPosition + Vector3.Up * 0.4f, Palette.Plant, 6, 2.5f, 0.07f, 0.4f);
        _flash = 0.25f;
        // Ekme efekti Capa.Toprak'ta: feel.json sources.toprak_ekim.
    }

    public void OnWatered()
    {
        _flash = 0.5f;
        _owner.Capa?.FeelHit("sulama", GlobalPosition + Vector3.Up * 0.3f, new Color("8CC8E8"), squash: this);
    }

    /// <summary>Kabarik urun toplanmadan sondu (GDD 8.4): normal boya iner, iki kat hasat verir.</summary>
    public void OnDeflated()
    {
        _swellT = -1;
        _soundDone = false;
        SetCrop(swell: 0f, glow: 0f);
        if (_crop != null) _owner.Capa?.FeelHit("kabarik_sonme", _crop.GlobalPosition + Vector3.Up * 0.5f, Palette.MayaTeal, squash: this);
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
            Fx.Burst(_crop.GlobalPosition + Vector3.Up * 0.5f, CropVisuals.ProduceColor(Plot.Crop), 22, 5f, 0.12f, 0.6f);
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
        var sw = _cropSw; var cm = _cropMat;
        var sc = new SwollenCrop(Plot.Crop!, size, mesh, g => { if (sw != null) sw.Set(glow: g); else cm?.SetShaderParameter("glow", g); }, _crop.Scale.X);
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
            SetCrop(swell: Mathf.Min(1f, k * 1.5f), glow: 0.25f + k * 0.85f);
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
        SetCrop(flash: Plot.Stage == CropStage.Mature ? 0.05f + 0.05f * Mathf.Sin(Time.GetTicksMsec() * 0.004f) : 0f);
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

/// <summary>Kabarik urun: yuk nesnesi. Cantaya girmez, Cekim isiniyla tasinir ve firlatilir (GDD 5, 8.4).</summary>
public partial class SwollenCrop : Node3D, ICarryable
{
    public string CropId { get; }
    public SizeClassDef Size { get; }
    public bool Held { get; set; }
    public bool Sold;
    public Node3D Body => this;
    public float Kg => Size.Kg;
    public bool Gone => Sold || IsQueuedForDeletion();
    readonly System.Action<float> _glow;
    readonly Node3D _pivot;
    float _t;
    public float Radius { get; }

    public SwollenCrop(string crop, SizeClassDef size, Node3D mesh, System.Action<float> glow, float scale)
    {
        CropId = crop; Size = size; _glow = glow;
        _pivot = new Node3D();
        AddChild(_pivot);
        _pivot.AddChild(mesh);
        _pivot.Scale = Vector3.One * scale;
        Radius = 0.3f * scale;
        AddToGroup("swollen");
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _glow?.Invoke(Held ? 1.1f : 0.8f);
        _pivot.Rotation = Held ? new Vector3(Mathf.Sin(_t * 2f) * 0.12f, _t * 0.6f, Mathf.Cos(_t * 1.7f) * 0.1f) : Vector3.Zero;
    }
}
