using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: yerdeki Oz (URETIM 6 M1 karari 19). Sim kurali KabarmaNight'ta: Oz oldurulen dusmanin yerine duser,
/// toplama yaricapi (Miknatis carpar) icinde toplanir, dalga sonunda ve safakta kalan Oz oyuncuya ucar. Burada yalniz
/// gorunus: yerde suzulen mor tane ve toplaninca cantaya ucus.
/// </summary>
public partial class Director
{
    readonly Dictionary<int, Node3D> _ozViews = new();
    int _ozPicked, _ozAuto; // gunluk icin: yaricapta toplanan / dalga sonunda ucan

    /// <summary>Oldurmede: Oz tanesi dusmanin yerinde belirir.</summary>
    void DropOz(OzMote m, Vector3 at)
    {
        var kit = new MeshKit();
        kit.Pillow(Vector3.One * 0.32f, MeshKit.At(0, 0, 0), Palette.MayaViolet, 0.5f, 8, 4);
        var mi = kit.Instance();
        var root = new Node3D { Name = $"Oz{m.Id}" };
        root.AddChild(mi);
        AddChild(root);
        var ground = World.Ground(new Vector3(m.X, 0, m.Z));
        root.GlobalPosition = ground + Vector3.Up * 0.45f;
        // Dusmanin govdesinden yere suzulur, sonra hafifce yukarida salinir.
        mi.Position = at - root.GlobalPosition;
        var tw = mi.CreateTween();
        tw.TweenProperty(mi, "position", Vector3.Zero, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(mi, "position", Vector3.Up * 0.12f, 0.6f).SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(mi, "position", Vector3.Zero, 0.6f).SetTrans(Tween.TransitionType.Sine);
        tw.SetLoops();
        _ozViews[m.Id] = root;
    }

    /// <summary>Sim Oz'u saydi: tane cantaya ucar (yaricap icinde ya da dalga sonunda).</summary>
    void OnOzCollected(OzMote m, bool auto)
    {
        Vector3 from;
        if (_ozViews.Remove(m.Id, out var v) && IsInstanceValid(v)) { from = v.GlobalPosition; v.QueueFree(); }
        else from = World.Ground(new Vector3(m.X, 0, m.Z)) + Vector3.Up * 0.45f;
        Fx.FlyTo(from, () => Player.BackpackPoint.GlobalPosition, Palette.MayaViolet, 0.12f);
        if (auto) _ozAuto++; else _ozPicked++;
        if (!auto) Sfx.Play("pickup", Player.GlobalPosition, -12f);
    }

    /// <summary>Gece bitince (safak, yeniden baslatma) yerde gorunus kalmasin.</summary>
    void ClearOzViews()
    {
        if (_ozPicked + _ozAuto > 0) GD.Print($"NIGHT oz picked={_ozPicked} auto={_ozAuto}");
        _ozPicked = _ozAuto = 0;
        foreach (var v in _ozViews.Values) if (IsInstanceValid(v)) v.QueueFree();
        _ozViews.Clear();
    }
}
