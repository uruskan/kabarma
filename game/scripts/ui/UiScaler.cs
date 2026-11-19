using System;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// <see cref="UiTheme.Scale"/>'i pencereye uydurur (<see cref="Automatic"/>), oyuncunun tercihiyle carpar: Ctrl + arti /
/// eksi, Ctrl + 0 sifirlar; tercih user://ui.cfg'de (oyun kaydi degil). Basvuru: Hollow-Universe App/UiScaler.cs.
/// Arayuz ekranin gercek piksel boyunda kurulur ve rasterlanir, gerdirilmez.
/// </summary>
public partial class UiScaler : Node
{
    const string SettingsPath = "user://ui.cfg";
    float _preference = 1f;

    public static UiScaler Instance { get; private set; }
    /// <summary>Kullanicinin arayuz boyu tercihi (1 = otomatik olcek).</summary>
    public float Preference => _preference;

    /// <summary>Ayarlar menusu: tercihi dogrudan ayarlar (Ctrl +/- ile ayni sinirlar), yazar ve uygular.</summary>
    public void SetPreference(float p)
    {
        var s = UiTheme.Data.Scale;
        _preference = Math.Clamp(p, s.PreferenceMin, s.PreferenceMax);
        var cfg = new ConfigFile();
        cfg.Load(SettingsPath);
        cfg.SetValue("ui", "scale", _preference);
        cfg.Save(SettingsPath);
        Apply();
    }

    public override void _Ready()
    {
        Instance = this;
        var s = UiTheme.Data.Scale;
        var cfg = new ConfigFile();
        if (cfg.Load(SettingsPath) == Error.Ok)
            _preference = Math.Clamp((float)cfg.GetValue("ui", "scale", 1f).AsDouble(), s.PreferenceMin, s.PreferenceMax);
        GetTree().Root.SizeChanged += Apply;
        Apply();
    }

    public override void _ExitTree()
    {
        if (GetTree()?.Root != null) GetTree().Root.SizeChanged -= Apply;
    }

    /// <summary>Olcegi pencere boyu ve tercihten yeniden hesaplar.</summary>
    public void Apply()
    {
        var size = GetTree().Root.Size;
        // Kural Sim'de (UiScale.For, UiTests): 1080p'de 1, 1440p'de 1,35, 720p'de 0,8; yirmide bire yuvarli.
        UiTheme.SetScale(Kabarma.Sim.UiScale.For(UiTheme.Data, size.X, size.Y, _preference));
    }

    public static float Largest(float width, float height) => Kabarma.Sim.UiScale.Largest(UiTheme.Data, width, height);
    public static float Automatic(float width, float height) => Kabarma.Sim.UiScale.Automatic(UiTheme.Data, width, height);

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, CtrlPressed: true } key) return;
        var s = UiTheme.Data.Scale;
        float p = _preference;
        switch (key.PhysicalKeycode)
        {
            case Key.Equal: case Key.KpAdd: p *= s.Step; break;
            case Key.Minus: case Key.KpSubtract: p /= s.Step; break;
            case Key.Key0: case Key.Kp0: p = 1f; break;
            default: return;
        }
        var size = GetTree().Root.Size;
        float most = Math.Max(1f, Largest(size.X, size.Y) / Automatic(size.X, size.Y));
        _preference = Math.Clamp(p, s.PreferenceMin, Math.Min(s.PreferenceMax, most));
        var cfg = new ConfigFile();
        cfg.Load(SettingsPath);
        cfg.SetValue("ui", "scale", _preference);
        cfg.Save(SettingsPath);
        Apply();
        GetViewport().SetInputAsHandled();
    }
}
