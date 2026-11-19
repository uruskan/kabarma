using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Renk korlugu suzgeci (SANAT-SES §8.3 "uc renk korlugu paleti"): ekranin tamamini (oyun + arayuz) okuyup renk duzeltme matrisinden
/// gecirir (Machado simulasyonunun tersiyle kayip kanal farklari kalan kanallara tasinir: daltonizasyon). Kapaliyken
/// dugum gorunmez ve maliyeti yoktur. Nadirlik ve hasar renklerinin sekille ayrilmasi arayuz tarafinda (ItemTone) ayrica.
/// </summary>
public partial class AccessOverlay : CanvasLayer
{
    const string Code = @"shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform int mode = 0;
uniform float strength = 1.0;
void fragment() {
    vec3 c = texture(screen_texture, SCREEN_UV).rgb;
    // dogrusal olmayan sRGB uzerinde kaba ama yeterli matrisler (daltonizasyon: simulasyon farki kalan kanallara eklenir)
    mat3 sim;
    if (mode == 1) sim = mat3(vec3(0.152, 0.115, -0.004), vec3(1.053, 0.786, -0.048), vec3(-0.205, 0.099, 1.052));      // protanopi
    else if (mode == 2) sim = mat3(vec3(0.367, 0.280, -0.012), vec3(0.861, 0.673, 0.043), vec3(-0.228, 0.047, 0.969)); // deuteranopi
    else sim = mat3(vec3(1.256, -0.078, -0.004), vec3(-0.077, 0.931, 0.691), vec3(-0.179, 0.148, 0.313));             // tritanopi
    vec3 s = clamp(sim * c, 0.0, 1.0);
    vec3 d = c - s;
    vec3 fix = vec3(0.0, d.r * 0.7 + d.b * 0.0, d.r * 0.7);
    if (mode == 3) fix = vec3(d.b * 0.7, d.b * 0.7, 0.0);
    COLOR = vec4(clamp(c + fix * strength, 0.0, 1.0), 1.0);
}";

    ColorRect _rect;
    ShaderMaterial _mat;

    public int Mode { get; private set; }
    /// <summary>Suzgec ekrani kapliyor mu (kapaliyken dugum gorunmez).</summary>
    public bool Active => _rect != null && _rect.Visible;

    public override void _Ready()
    {
        Layer = 120; // arayuzun ve fotograf modu panelinin de ustunde: hepsi suzulur
        _mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        _rect = new ColorRect { Material = _mat, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
        UserSettings.Current.Changed += Apply;
        Apply();
    }

    public override void _ExitTree() => UserSettings.Current.Changed -= Apply;

    public void Apply()
    {
        Mode = UserSettings.Current.ColorBlind;
        _rect.Visible = Mode > 0;
        _mat.SetShaderParameter("mode", Mode);
        _mat.SetShaderParameter("strength", UiTheme.Data.Access.ColorBlindStrength);
    }
}
