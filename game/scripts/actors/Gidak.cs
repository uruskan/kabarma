using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Gidak: urkek otcul. Bugday verilince yumurtlar; Kabarma'da midilli boyuna gelip Zipzip tekmeler (PLAN 1.4).
/// Gorunus: kus iskeleti (pipeline/rig_bird.py, kredisiz), 8 klibin hepsi PoseLibrary'nin kus planindan, ortak sisme
/// shader'i (Swellable). Tekme: Gidak.Kick.cs. Agil davranisi (ilkgun, Gidak.Pen.cs) PenSteer ile baglanir.
/// </summary>
public partial class Gidak : Node3D
{
    public CreatureDef Def;
    Node3D _visual;
    Director _dir;
    Vector3 _wanderTo;
    float _wanderT, _kickT, _layT = -1, _t;
    float _scale = 1f, _targetScale = 1f;
    public bool Swollen;
    readonly List<ShaderMaterial> _liveMats = new();
    public int Fed;
    CreatureRig _rig;
    Swellable _sw;
    float _kickAnimT, _stillT, _peckT, _stepAcc;
    /// <summary>Sinama: yerinde durur, DisplaySlot klibini oynatir (Director.Creatures.cs gorunus goruntuleri).</summary>
    public bool Display;
    public string DisplaySlot = "idle";

    public Gidak(CreatureDef def, Director dir)
    {
        Def = def; _dir = dir;
        _visual = new Node3D();
        AddChild(_visual);
        // M0 denemesi 4 / M1: kus iskeletli model (gidak_rig.glb); yoksa iskeletsiz eski model; o da yoksa koddan govde.
        _rig = CreatureRig.Load(def, dir?.Poses, _visual, "res://assets/models/gidak/gidak.glb");
        var model = _rig?.Model;
        if (model != null)
        {
            var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
            Aabb box = new(); bool first = true;
            foreach (var mi in meshes)
            {
                var b = PoseLibrary.RelTo(mi, model) * mi.GetAabb();
                box = first ? b : box.Merge(b); first = false;
            }
            float h = Mathf.Max(0.01f, box.Size.Y);
            float s = (def.Rig?.Height ?? 0.75f) / h; // Gidak ~75 cm
            model.Scale = Vector3.One * s;
            model.Position = new Vector3(-box.GetCenter().X * s, -box.Position.Y * s, -box.GetCenter().Z * s);
            _sw = new Swellable(model, def.Swell);
        }
        else
        {
            var kit = new MeshKit();
            kit.Sphere(0.32f, MeshKit.At(new Vector3(0, 0.38f, 0), Vector3.Zero, new Vector3(1f, 1.15f, 1.1f)), Palette.OffWhite, 12, 8);
            kit.Pillow(new Vector3(0.12f, 0.2f, 0.12f), MeshKit.At(0, 0.8f, 0.05f), Palette.Accent, 0.5f);
            kit.Cylinder(0.0f, 0.07f, 0.14f, MeshKit.At(new Vector3(0, 0.55f, 0.33f), new Vector3(Mathf.Pi / 2, 0, 0)), Palette.Warning, 6);
            _visual.AddChild(kit.Instance());
        }
        _rig?.Play("idle", 0f);
        AddToGroup("gidak");
    }

    /// <summary>Gorunus ve klipler (sinama ve baglanan partial'lar icin).</summary>
    public CreatureRig Rig => _rig;
    public Swellable Look => _sw;

    public override void _Ready()
    {
        _wanderTo = GlobalPosition;
    }

    // ---------- ilkgun baglantisi: agil (Gidak.Pen.cs) ----------
    /// <summary>
    /// Gunduz, kabarmamisken her kare, varsayilan dolasmadan once. handled = true ise Gidak goal'e speed ile yurur
    /// (klipler kendiliginden). Agilda tutma, kapidan sokma, izleme burada.
    /// </summary>
    partial void PenSteer(float dt, ref Vector3 goal, ref float speed, ref bool handled);

    public void Feed()
    {
        Fed++;
        Fx.Burst(GlobalPosition + Vector3.Up * 1f, Palette.Accent, 6, 1.5f, 0.1f, 0.8f, true, -2f);
        Fx.Text3D(GlobalPosition + Vector3.Up * 1.2f, "♥", Palette.Accent, 96, 1.2f, 1.2f);
        _layT = 2.5f;
        _rig?.Play("eat", 0.1f, 1f, restart: true);
        _peckT = 1.2f;
    }

    public void Swell(bool on)
    {
        Swollen = on;
        _targetScale = on ? Def.SwollenScale : 1f;
        if (on) { Sfx.Play("swell", GlobalPosition, 0f); Fx.Burst(GlobalPosition + Vector3.Up, Palette.MayaTeal, 20, 4f, 0.12f, 0.8f, true, 0f); }
        // Ortak sisme shader'i (SANAT-SES §4): normal yonunde sisme, yay titremesi, Maya damarlari.
        _sw?.Swell(on);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (_sw != null) _scale = Mathf.Lerp(1f, Def.SwollenScale, _sw.Tick(dt));
        else _scale = Mathf.Lerp(_scale, _targetScale, Mathf.Min(1f, dt * 2.5f));
        float wob = Swollen && _sw == null ? 1f + Mathf.Sin(_t * 8f) * 0.03f : 1f;
        _visual.Scale = new Vector3(_scale * wob, _scale / wob, _scale * wob);
        if (Display)
        {
            _kickAnimT -= dt;
            if (_kickAnimT <= 0) _rig?.PlayRepeat(DisplaySlot);
            return;
        }

        if (_layT > 0)
        {
            _layT -= dt;
            if (_layT <= 0)
            {
                _dir.State.Backpack.Add("egg", 1);
                Events.RaiseGathered("egg", 1);
                var egg = new MeshKit().Sphere(0.12f, MeshKit.At(new Vector3(0, 0.12f, 0), Vector3.Zero, new Vector3(1, 1.3f, 1)), Palette.OffWhite, 10, 6).Instance();
                GetParent().AddChild(egg);
                egg.GlobalPosition = GlobalPosition - GlobalBasis.Z * 0.35f;
                Fx.Text3D(GlobalPosition + Vector3.Up * 1.1f, "+1 Yumurta", Palette.OffWhite, 56, 1f, 1.4f);
                Sfx.Play("pickup", GlobalPosition, -4f);
            }
        }

        Vector3 goal;
        float speed = Def.Speed;
        _kickAnimT -= dt;
        if (Swollen)
        {
            // Koc Gidak: en yakin Zipzip'e kosar ve citin ustunden tekmeler (Gidak.Kick.cs).
            var z = Zipzip.All.Where(e => !e.Dead && !e.Airborne).OrderBy(e => e.GlobalPosition.DistanceSquaredTo(GlobalPosition)).FirstOrDefault();
            goal = z?.GlobalPosition ?? _dir.FarmCenter() + new Vector3(3, 0, 3);
            // Oyuncunun kamerasini kapatmasin: oyuncuya 3 m'den fazla yaklasmaz.
            var toP = GlobalPosition - _dir.Player.GlobalPosition; toP.Y = 0;
            if (toP.Length() < 3.5f) goal = GlobalPosition + toP.Normalized() * 2f;
            speed *= Def.SwollenSpeedMult;
            _kickT -= dt;
            if (z != null && z.GlobalPosition.DistanceTo(GlobalPosition) < Def.KickRange + z.Radius && _kickT <= 0)
            {
                _kickT = Def.KickInterval;
                Kick(z);
            }
        }
        else
        {
            _wanderT -= dt;
            var p = _dir.Player;
            goal = _wanderTo;
            bool penned = false;
            PenSteer(dt, ref goal, ref speed, ref penned);
            if (!penned)
            {
                // Bugdayin varsa ve henuz beslemediysen pesinden gelir (PLAN 1.4, 6:00).
                bool lured = Fed == 0 && _layT < 0 && _dir.State.Backpack.Count("wheat") > 0 && p.GlobalPosition.DistanceTo(GlobalPosition) < 7f;
                if (lured) _wanderTo = p.GlobalPosition + (GlobalPosition - p.GlobalPosition).Normalized() * 2.4f;
                else if (_wanderT <= 0)
                {
                    _wanderT = (float)GD.RandRange(2.5, 5.0);
                    var home = _dir.GidakHome;
                    _wanderTo = home + new Vector3((float)GD.RandRange(-5, 5), 0, (float)GD.RandRange(-5, 5));
                }
                goal = _wanderTo;
            }
        }
        var d = goal - GlobalPosition; d.Y = 0;
        bool moving = d.Length() > 0.4f;
        if (moving)
        {
            var step = d.Normalized() * Mathf.Min(d.Length(), speed * dt);
            GlobalPosition = _dir.World.Ground(GlobalPosition + step);
            float yaw = Mathf.Atan2(d.X, d.Z);
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(1f, dt * 8f)), 0);
            _stepAcc += step.Length();
            if (_stepAcc > 0.25f * _scale) { _stepAcc = 0; _sw?.StepJiggle(); }
            if (_rig == null)
            {
                // Paytak yuruyus: kodla salinim (iskeletsiz yedek)
                _visual.Rotation = new Vector3(Mathf.Lerp(_visual.Rotation.X, 0, dt * 6f), 0, Mathf.Sin(_t * 12f) * 0.12f);
                _visual.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_t * 12f)) * 0.05f * _scale, 0);
            }
        }
        else if (_rig == null)
        {
            _visual.Rotation = new Vector3(Mathf.Lerp(_visual.Rotation.X, Mathf.Sin(_t * 2f) > 0.8f ? 0.5f : 0f, dt * 6f), 0, 0); // gagalama
            _visual.Position = Vector3.Zero;
        }
        TickClips(dt, moving, speed);
    }

    /// <summary>Klip secimi: tekme > yurume/kosu > gagalama (yeme) > bekleme; uzun durunca dinlenme.</summary>
    void TickClips(float dt, bool moving, float speed)
    {
        if (_rig == null || _kickAnimT > 0) return;
        _peckT -= dt;
        if (moving)
        {
            _stillT = 0;
            bool run = Swollen || speed > Def.Speed * 1.2f;
            // Adim hizi gidilen hiza ve boya gore (kabarmis Gidak daha buyuk adim atar).
            float rate = speed / Mathf.Max(0.01f, Def.Speed * (run ? Def.SwollenSpeedMult : 1f)) / Mathf.Max(1f, _scale * 0.6f);
            _rig.Play(run ? "run" : "walk", 0.15f, Mathf.Clamp(rate, 0.5f, 1.6f));
            return;
        }
        _stillT += dt;
        if (_peckT > 0) { _rig.Play("eat", 0.15f); return; }
        if (!Swollen && _stillT > 1.5f && Mathf.PosMod(_t, 9f) < 2.4f) { _rig.Play("eat", 0.2f); return; }
        if (!Swollen && _stillT > 12f) { _rig.Play("rest", 0.3f); return; }
        _rig.Play("idle", 0.2f);
    }
}
