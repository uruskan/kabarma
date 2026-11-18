using System;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Pencere boyu bir cizim katmani; cizimi sahibi yapar (pano dolaysiz kipte cizer: Hud.Paint.cs). Fareyi almaz: tiklama
/// Hud._Input'ta tasarim geometrisiyle cozulur. Basvuru: Hollow-Universe UI/Painter.cs.
/// </summary>
public partial class UiPainter : Control
{
    public Action<UiPainter> Paint { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Draw() => Paint?.Invoke(this);
}
