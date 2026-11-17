using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Vurus hissi ayarlari (GDD 4 "Vurus hissi olcutleri", SANAT-SES §8.3) ve F1 ayar paneli.
/// Panel: carpanlar icin surgu, kare hizi ve girisin ekrana yansima suresi (olcut: en fazla 50 ms).
/// </summary>
public static class Feel
{
    public static float HitStop = 1f;
    public static float Shake = 1f;
    public static float Recoil = 1f;
    public static float DamageNumbers = 1f;
}

public partial class FeelPanel : CanvasLayer
{
    PanelContainer _panel;
    Label _stats;
    readonly List<double> _latency = new();
    ulong _pendingInputUsec;
    bool _awaitDraw;
    public double LastLatencyMs { get; private set; } = -1;
    public double MedianLatencyMs => _latency.Count == 0 ? -1 : _latency.OrderBy(x => x).ElementAt(_latency.Count / 2);
    public int Samples => _latency.Count;

    public override void _Ready()
    {
        Layer = 50;
        _panel = new PanelContainer { Visible = false, Position = new Vector2(24, 160) };
        var sb = new StyleBoxFlat { BgColor = new Color(Palette.NightDeep, 0.9f), ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 };
        sb.SetCornerRadiusAll(12);
        _panel.AddThemeStyleboxOverride("panel", sb);
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        v.AddChild(new Label { Text = "F1 · His ayarları" });
        Slider(v, "Vuruş duraklaması", () => Feel.HitStop, x => Feel.HitStop = x);
        Slider(v, "Kamera sarsıntısı", () => Feel.Shake, x => Feel.Shake = x);
        Slider(v, "Geri tepme", () => Feel.Recoil, x => Feel.Recoil = x);
        Slider(v, "Hasar sayısı boyu", () => Feel.DamageNumbers, x => Feel.DamageNumbers = x);
        _stats = new Label();
        v.AddChild(_stats);
        _panel.AddChild(v);
        AddChild(_panel);
        RenderingServer.FramePostDraw += OnPostDraw;
    }

    public override void _ExitTree() => RenderingServer.FramePostDraw -= OnPostDraw;

    static void Slider(VBoxContainer parent, string name, System.Func<float> get, System.Action<float> set)
    {
        var h = new HBoxContainer();
        var l = new Label { Text = name, CustomMinimumSize = new Vector2(170, 0) };
        var s = new HSlider { MinValue = 0, MaxValue = 2, Step = 0.05, Value = get(), CustomMinimumSize = new Vector2(130, 0) };
        var val = new Label { Text = $"×{get():0.00}" };
        s.ValueChanged += x => { set((float)x); val.Text = $"×{x:0.00}"; };
        h.AddChild(l); h.AddChild(s); h.AddChild(val);
        parent.AddChild(h);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.F1)
        {
            _panel.Visible = !_panel.Visible;
            Input.MouseMode = _panel.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        }
        // Gecikme olcumu: atis girisi anindan, tepkiyi iceren karenin cizimi bitene kadar.
        if (e.IsActionPressed("fire")) _pendingInputUsec = Time.GetTicksUsec();
    }

    /// <summary>Oyun girise gorsel tepki verdiginde (atis, isin) cagrilir.</summary>
    public void MarkReaction()
    {
        if (_pendingInputUsec == 0) return;
        _awaitDraw = true;
    }

    /// <summary>Bot ve sinamalar icin: girisi kodla isaretler.</summary>
    public void MarkInput() => _pendingInputUsec = Time.GetTicksUsec();

    void OnPostDraw()
    {
        if (!_awaitDraw || _pendingInputUsec == 0) return;
        LastLatencyMs = (Time.GetTicksUsec() - _pendingInputUsec) / 1000.0;
        _latency.Add(LastLatencyMs);
        if (_latency.Count > 200) _latency.RemoveAt(0);
        _pendingInputUsec = 0;
        _awaitDraw = false;
    }

    public override void _Process(double delta)
    {
        if (!_panel.Visible) return;
        _stats.Text = $"FPS {Engine.GetFramesPerSecond():0}   ·   girdi→tepki son {LastLatencyMs:0.0} ms, medyan {MedianLatencyMs:0.0} ms ({Samples} örnek)";
    }
}
