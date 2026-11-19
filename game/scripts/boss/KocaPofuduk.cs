using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Koca Pofuduk (ICERIK §4.1, GDD §18.1). Yer tutucu model: Pofuduk modeli (pofuduk.glb, iskelet ve klipler) olceklenir
/// ve koddan suslenir: yun tac, bakir can cingirakli tasma, isiyan gozler; asama 2'de govdeyi saran yun zirhi (kirkildikca
/// tutamlar duser), asama 3'te ciplak (yun yok, govde incelir). Uretim istegi raporda.
/// </summary>
public partial class KocaPofuduk : AtaActor
{
    CreatureRig _rig;
    Swellable _sw;
    Node3D _frame, _decor;
    readonly List<MeshInstance3D> _wool = new();
    Aabb _box;
    float _baseScale;

    protected override void BuildModel()
    {
        _frame = new Node3D { Name = "Frame" };
        Visual.AddChild(_frame);
        var def = D.Data.Creature("pofuduk");
        _rig = CreatureRig.Load(def, D.Poses, _frame);
        _box = new Aabb(new Vector3(-0.5f, 0, -0.6f), new Vector3(1f, 1f, 1.2f));
        if (_rig != null)
        {
            _sw = new Swellable(_rig.Model, def.Swell);
            _sw.Snap(true); // Ata hep kabarik
            var meshes = _rig.Model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
            if (meshes.Count > 0)
            {
                _box = meshes.Select(m => PoseLibrary.RelTo(m, _frame) * m.GetAabb()).Aggregate((a, b) => a.Merge(b));
            }
            _rig.Play("walk", 0f);
        }
        else
        {
            var kit = new MeshKit();
            kit.Pillow(new Vector3(1f, 0.8f, 1.2f), MeshKit.At(0, 0.6f, 0), Palette.OffWhite, 0.6f);
            _frame.AddChild(kit.Instance());
            _box = new Aabb(new Vector3(-0.5f, 0.2f, -0.6f), new Vector3(1f, 0.8f, 1.2f));
        }
        // Modelin boyu Def.Height olacak sekilde olcek (veride scale yaklasik; kutu olcer).
        _baseScale = _box.Size.Y > 0.05f ? Def.Height / _box.Size.Y : Def.Scale;
        _frame.Scale = Vector3.One * _baseScale;

        _decor = new Node3D { Name = "Sus" };
        _frame.AddChild(_decor);
        // Model kutusu gorunen yunden buyuk (goruntude govdenin tepesi kutunun ~%70'i): susler gorunen govdeye gore.
        float top = _box.Position.Y + _box.Size.Y * 0.68f, front = _box.GetCenter().Z + _box.Size.Z * 0.36f, w = _box.Size.X;
        var kitD = new MeshKit();
        // Yun tac: tepede bes buyuk tutam ve altin bir halka
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.Tau;
            kitD.Sphere(w * 0.13f, MeshKit.At(Mathf.Cos(a) * w * 0.18f, top + w * 0.02f, Mathf.Sin(a) * w * 0.18f + _box.GetCenter().Z * 0.3f), Palette.OffWhite, 9, 6);
        }
        kitD.Cylinder(w * 0.2f, w * 0.24f, w * 0.06f, MeshKit.At(0, top - w * 0.01f, _box.GetCenter().Z * 0.3f), Palette.Warning, 14);
        // Tasma ve can cingiragi (bakir)
        var neck = new Vector3(0, _box.Position.Y + _box.Size.Y * 0.36f, front);
        kitD.Add(new TorusMesh { InnerRadius = w * 0.34f, OuterRadius = w * 0.38f, Rings = 24, RingSegments = 6 }, MeshKit.At(new Vector3(0, neck.Y + w * 0.12f, _box.GetCenter().Z), new Vector3(0.12f, 0, 0)), Palette.HoldingRed.Darkened(0.2f)); // yatay tasma
        kitD.Sphere(w * 0.09f, MeshKit.At(neck + new Vector3(0, -w * 0.28f, w * 0.12f)), Palette.Wood.Lerp(Palette.Warning, 0.5f), 10, 6);
        kitD.Cylinder(w * 0.03f, w * 0.05f, w * 0.06f, MeshKit.At(neck + new Vector3(0, -w * 0.2f, w * 0.1f)), Palette.Wood.Lerp(Palette.Warning, 0.5f), 8);
        _decor.AddChild(kitD.Instance());
        // Isiyan gozler (Ata isareti)
        var eyeMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Palette.MayaTeal, EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 3f };
        foreach (float sx in new[] { -1f, 1f })
            _decor.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = w * 0.045f, Height = w * 0.09f }, MaterialOverride = eyeMat, Position = new Vector3(sx * w * 0.16f, _box.Position.Y + _box.Size.Y * 0.52f, front), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        // Yun zirhi (asama 2): govdeyi saran tutamlar; zirh dustukce azalir.
        var woolMat = new StandardMaterial3D { AlbedoColor = Palette.OffWhite.Lerp(Palette.Fog, 0.3f), Roughness = 1f };
        var rng = new RandomNumberGenerator { Seed = 7 };
        for (int i = 0; i < 22; i++)
        {
            float a = rng.Randf() * Mathf.Tau, h = rng.RandfRange(0.15f, 0.62f);
            var at = new Vector3(Mathf.Cos(a) * _box.Size.X * 0.4f, _box.Position.Y + _box.Size.Y * h, _box.GetCenter().Z + Mathf.Sin(a) * _box.Size.Z * 0.38f);
            var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = w * rng.RandfRange(0.14f, 0.22f), Height = w * 0.32f, RadialSegments = 10, Rings = 6 }, MaterialOverride = woolMat, Position = at, Visible = false };
            _frame.AddChild(m);
            _wool.Add(m);
        }
    }

    protected override Color VolleyColor => Palette.OffWhite;
    protected override float VolleySize => 0.5f;

    public override void OnPhase(int phase, bool quiet = false)
    {
        base.OnPhase(phase, quiet);
        foreach (var m in _wool) m.Visible = phase == 1;
        // Asama 3: ciplak; tac ve yun gider, govde incelir ve pembelesir gibi (hizli).
        bool naked = phase >= 2;
        _decor.GetChild<Node3D>(0).Visible = !naked;
        _frame.Scale = new Vector3(_baseScale * (naked ? 0.82f : 1f), _baseScale * (naked ? 0.92f : 1f), _baseScale * (naked ? 0.9f : 1f));
        if (!quiet && phase == 1) { Fx.Burst(Center, Palette.OffWhite, 60, 6f, 0.25f, 1.2f); D.Hud.Say(Loc.T("ata.say_wool")); }
        if (!quiet && phase == 2) { Fx.Burst(Center, Palette.OffWhite, 80, 8f, 0.3f, 1.4f); D.Hud.Say(Loc.T("ata.say_bare")); }
    }

    public override void OnSheared(float before, float after, Vector3 at)
    {
        var def = Phase;
        int keep = def.Armor > 0 ? Mathf.CeilToInt(_wool.Count * after / def.Armor) : 0;
        int shown = 0;
        foreach (var m in _wool)
        {
            if (!m.Visible) continue;
            if (shown++ < keep) continue;
            m.Visible = false;
            Fx.Burst(m.GlobalPosition, Palette.OffWhite, 8, 4f, 0.16f, 0.8f);
        }
        Sfx.Play("bicme_supurme", at, 2f, 0.8f);
        Fx.Text3D(Center + Vector3.Up * Def.Height * 0.55f, after <= 0 ? Loc.T("ata.wool_sheared") : Loc.T("ata.armor_pct", (after * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture)), Palette.OffWhite, 56, 1f, 0.9f);
    }

    public override void OnStunned()
    {
        base.OnStunned();
        _rig?.Play("rest", 0.2f);
    }

    protected override void OnHurt() => _sw?.HitJiggle();

    protected override void WalkAnim(float dt, float speed)
    {
        _rig?.Play("walk", 0.2f, Mathf.Clamp(speed / 2.5f, 0.6f, 3f));
        _stepAcc += dt * speed;
        if (_stepAcc > 1.8f)
        {
            _stepAcc = 0;
            _sw?.StepJiggle();
            Sfx.Play("kabarik_gumleme", GlobalPosition, -6f, 0.45f);
        }
        _sw?.Tick(dt);
    }

    protected override void IdleAnim(float dt, bool tense)
    {
        _rig?.Play(tense ? "idle" : "eat", 0.25f, tense ? 2f : 1f);
        _sw?.Tick(dt);
    }

    float _stepAcc;
}
