using Godot;

namespace Kabarma.Game;

/// <summary>
/// Pano, dayaniklilik cubugu (SANAT-SES §8.1 "can ve dayaniklilik", URETIM §6 M1 karari 11). Can cubugunun hemen
/// altinda ince bir cubuk; dolu ve depar yokken solar, harcanirken belirir. Tukenince (depar 20'ye kadar kapali,
/// GDD §3.2) uyari sarisi yanip soner. Kural Sim'de: GameState.Stamina.
/// </summary>
public partial class Hud
{
    ProgressBar _stamina;
    StyleBoxFlat _staminaFill;
    float _staminaAlpha;

    /// <summary>Can cubugunun altina dayaniklilik cubugunu ekler (Hud._Ready).</summary>
    void BuildStamina()
    {
        _stamina = new ProgressBar
        {
            CustomMinimumSize = new Vector2(280, 10), MaxValue = 100, Value = 100, ShowPercentage = false,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _staminaFill = Box(Palette.Plant, 6); _staminaFill.ContentMarginTop = _staminaFill.ContentMarginBottom = 0;
        _stamina.AddThemeStyleboxOverride("fill", _staminaFill);
        var bg = Box(new Color(Palette.NightDeep, 0.7f), 6); bg.ContentMarginTop = bg.ContentMarginBottom = 0;
        _stamina.AddThemeStyleboxOverride("background", bg);
        _bottomLeft.AddChild(_stamina);
        _bottomLeft.MoveChild(_stamina, _hp.GetIndex() + 1);
    }

    /// <summary>Cubugu gunceller (Hud._Process).</summary>
    void UpdateStamina(float dt)
    {
        var s = _d?.State?.Stamina;
        if (_stamina == null || s == null) return;
        _stamina.MaxValue = s.Max;
        _stamina.Value = s.Value;
        bool full = s.Value >= s.Max - 0.01f && !s.Sprinting;
        _staminaAlpha = Mathf.MoveToward(_staminaAlpha, full ? 0.25f : 1f, dt * (full ? 1.5f : 6f));
        var c = s.Exhausted ? Palette.Warning.Lerp(Palette.Accent, 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() * 0.012f)) : Palette.Plant;
        _staminaFill.BgColor = c;
        _stamina.Modulate = new Color(1, 1, 1, _staminaAlpha);
    }
}
