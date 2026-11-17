using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Vurus hissi ayarlari (GDD 4 "Vurus hissi olcutleri", SANAT-SES 7 ve 8.3): data/feel.json + oyuncunun ayari
/// (user://feel_tuning.json, yalnizca degisen sayilar). Director'un ornegi; statik durum degil (TEKNIK 9).
/// Sinamalarda (--test, --bot, --netsync, --swarm) oyuncu ayari okunmaz ve yazilmaz; --feel_file=YOL baska dosya verir.
/// </summary>
public sealed class FeelTuning
{
    public const string UserPath = "user://feel_tuning.json";

    public JsonObject Defaults { get; }
    public JsonObject Doc { get; private set; }
    public FeelFile Data { get; private set; }
    public string FilePath { get; }
    public bool UserEnabled { get; }
    public event Action Changed;

    public FeelSource Src(string id) => Data.Source(id);
    public FeelMaster Master => Data.Master;

    FeelTuning(JsonObject defaults, string path, bool userEnabled)
    {
        Defaults = defaults;
        FilePath = path;
        UserEnabled = userEnabled;
        Doc = (JsonObject)defaults.DeepClone();
        Data = GameData.ParseFeel(Doc);
        if (userEnabled) LoadUser();
    }

    public static FeelTuning Load()
    {
        var a = Main.Args;
        bool explicitFile = a.TryGetValue("feel_file", out var file);
        bool testRun = a.ContainsKey("test") || a.ContainsKey("bot") || a.ContainsKey("netsync") || a.ContainsKey("swarm");
        return new FeelTuning(GameData.ParseDoc(FileAccess.GetFileAsString("res://data/feel.json")), explicitFile ? file : UserPath, explicitFile || !testRun);
    }

    /// <summary>Sinama icin: verilen dosyaya yazan ve ondan okuyan ayar (oyuncunun dosyasi degil).</summary>
    public static FeelTuning ForFile(string path) =>
        new(GameData.ParseDoc(FileAccess.GetFileAsString("res://data/feel.json")), path, true);

    /// <summary>Oyuncu ayarini varsayilanin ustune bindirir. Dosya yoksa false.</summary>
    public bool LoadUser()
    {
        if (!UserEnabled || !FileAccess.FileExists(FilePath)) return false;
        try
        {
            Doc = FeelFile.Overlay(Defaults, JsonNode.Parse(FileAccess.GetFileAsString(FilePath)));
            Data = GameData.ParseFeel(Doc);
            GD.Print($"FEEL user {FilePath}");
            Changed?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            GD.PrintErr($"FEEL {FilePath} okunamadi: {e.Message}");
            return false;
        }
    }

    /// <summary>Yalnizca varsayilandan farkli sayilari yazar. Sonucu metin olarak verir (panel durum satiri).</summary>
    public string Save()
    {
        if (!UserEnabled) return "Sınamada kayıt kapalı";
        using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        if (f == null) return $"Yazılamadı: {FileAccess.GetOpenError()}";
        f.StoreString(FeelFile.Diff(Defaults, Doc).ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return "Kaydedildi: " + ProjectSettings.GlobalizePath(FilePath);
    }

    public void Reset()
    {
        Doc = (JsonObject)Defaults.DeepClone();
        Data = GameData.ParseFeel(Doc);
        Changed?.Invoke();
    }

    public void Set(JsonObject section, string key, double value)
    {
        section[key] = JsonValue.Create(value);
        Data = GameData.ParseFeel(Doc);
        Changed?.Invoke();
    }
}

/// <summary>Bir girdinin ekrana yansimasi (F1 paneli ve gecikme sinamasi).</summary>
public readonly record struct ReactionSample(string Head, ulong InputUsec, ulong InputFrame, ulong ReactFrame, ulong EndUsec, bool Drawn)
{
    /// <summary>Girdiden tepkili karenin cizimine (ya da basliksiz kosuda kare sonuna) kadar gecen sure.</summary>
    public double Ms => (EndUsec - InputUsec) / 1000.0;
    public ulong Frames => ReactFrame - InputFrame;
}

/// <summary>
/// F1 ayar paneli (SANAT-SES 8.2): feel.json'un butun sayilari (bolum bolum surgu), Kaydet / Varsayilan / Yukle,
/// kare hizi, baslik basina girdi-tepki gecikmesi (olcut: en fazla 50 ms) ve son vurus duraklamasi.
/// Baska akislar dugme ekler: AddButton(ad, eylem).
/// </summary>
public partial class FeelPanel : CanvasLayer
{
    static readonly (string Key, string Title)[] Known =
    {
        ("master", "Ana çarpanlar (erişilebilirlik)"), ("hitStop", "Vuruş duraklaması"), ("shake", "Kamera sarsıntısı"),
        ("recoil", "Geri tepme yayı"), ("damageNumbers", "Hasar sayıları"), ("squash", "Ezilip esneme"), ("screenFlash", "Ekran parlaması"),
    };
    const int MaxSamples = 100;
    const ulong StaleInputFrames = 30;

    Director _d;
    PanelContainer _panel;
    Label _stats, _status;
    OptionButton _section;
    VBoxContainer _rows;
    HBoxContainer _extra;
    readonly List<(JsonObject Obj, JsonObject Def, string Title)> _sections = new();
    readonly Dictionary<string, List<double>> _latency = new();
    bool _headless;

    ulong _pendingInputUsec, _pendingFrame;
    bool _awaitDraw;
    string _reactHead;
    ulong _reactFrame;

    public bool Open => _panel?.Visible == true;
    public double LastLatencyMs { get; private set; } = -1;
    /// <summary>Tepki ekrana yansidi (sinama abone olur).</summary>
    public event Action<ReactionSample> Reacted;

    public override void _Ready()
    {
        Layer = 50;
        // Kare sonu: basliksiz kosuda cizim olmadigi icin tepkili karenin sonu burada okunur (en son islenen dugum).
        ProcessPriority = 100000;
        _d = GetParent<Director>();
        _headless = DisplayServer.GetName() == "headless";
        _panel = new PanelContainer { Visible = false, Position = new Vector2(24, 120) };
        var sb = new StyleBoxFlat { BgColor = new Color(Palette.NightDeep, 0.92f), ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 };
        sb.SetCornerRadiusAll(12);
        _panel.AddThemeStyleboxOverride("panel", sb);
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(470, 0) };
        v.AddChild(new Label { Text = "F1 · His ayarları (feel.json)" });
        _stats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
        v.AddChild(_stats);
        var c = _d.Feel.Data.Criteria;
        v.AddChild(new Label { Text = $"Ölçüt: girdi→tepki ≤ {c.MaxLatencyMs:0} ms · duraklama {c.HitStopMinMs:0}–{c.HitStopMaxMs:0} ms", Modulate = new Color(1, 1, 1, 0.7f) });

        _section = new OptionButton();
        _section.ItemSelected += i => BuildRows((int)i);
        v.AddChild(_section);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(460, 380), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_rows);
        v.AddChild(scroll);

        var buttons = new HBoxContainer();
        buttons.AddChild(Button("Kaydet", () => _status.Text = _d.Feel.Save()));
        buttons.AddChild(Button("Varsayılana dön", () => { _d.Feel.Reset(); Rebuild(); _status.Text = "Varsayılan (kaydedilmedi)"; }));
        buttons.AddChild(Button("Dosyadan yükle", () => { _status.Text = _d.Feel.LoadUser() ? "Yüklendi" : "Kayıtlı ayar yok"; Rebuild(); }));
        v.AddChild(buttons);
        _extra = new HBoxContainer();
        v.AddChild(_extra);
        _status = new Label { Text = _d.Feel.UserEnabled ? ProjectSettings.GlobalizePath(_d.Feel.FilePath) : "Sınama: oyuncu ayarı kapalı", Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.Arbitrary, CustomMinimumSize = new Vector2(460, 0) };
        v.AddChild(_status);
        _panel.AddChild(v);
        AddChild(_panel);
        Rebuild();
        RenderingServer.FramePostDraw += OnPostDraw;
    }

    public override void _ExitTree() => RenderingServer.FramePostDraw -= OnPostDraw;

    static Button Button(string text, Action a) { var b = new Button { Text = text }; b.Pressed += a; return b; }

    /// <summary>Panele dugme ekler (ornegin gecehud: "Kucuk Kabarma'yi yeniden baslat").</summary>
    public Button AddButton(string text, Action action)
    {
        var b = Button(text, action);
        _extra.AddChild(b);
        return b;
    }

    /// <summary>Bolum listesi feel.json agacindan kurulur: her ust duzey nesne ve her kaynak bir bolum (butun sayilar, tanim geregi).</summary>
    void Rebuild()
    {
        int keep = Math.Max(0, _section.Selected);
        _sections.Clear();
        _section.Clear();
        var doc = _d.Feel.Doc;
        var defs = _d.Feel.Defaults;
        foreach (var (key, node) in doc)
        {
            if (key.StartsWith('_') || key == "criteria" || node is not JsonObject obj) continue;
            if (key == "sources")
            {
                foreach (var (id, src) in obj)
                    if (src is JsonObject so) Add(so, (JsonObject)((JsonObject)defs["sources"])[id], "Kaynak: " + id);
                continue;
            }
            Add(obj, (JsonObject)defs[key], Known.FirstOrDefault(k => k.Key == key).Title ?? key);
        }
        _section.Select(Math.Min(keep, _section.ItemCount - 1));
        BuildRows(_section.Selected);

        void Add(JsonObject o, JsonObject def, string title)
        {
            if (!o.Any(kv => FeelFile.IsNumber(kv.Value))) return;
            _sections.Add((o, def, title));
            _section.AddItem(title);
        }
    }

    void BuildRows(int index)
    {
        foreach (var ch in _rows.GetChildren()) ch.QueueFree();
        if (index < 0 || index >= _sections.Count) return;
        var (obj, def, _) = _sections[index];
        foreach (var (key, node) in obj.ToList())
        {
            if (!FeelFile.IsNumber(node)) continue;
            double val = FeelFile.Num(node!);
            double d0 = def.TryGetPropertyValue(key, out var dn) && FeelFile.IsNumber(dn) ? FeelFile.Num(dn!) : val;
            var (min, max, step) = RangeFor(key, d0);
            var h = new HBoxContainer();
            h.AddChild(new Label { Text = key, CustomMinimumSize = new Vector2(190, 0), ClipText = true });
            var s = new HSlider { MinValue = min, MaxValue = Math.Max(max, val), Step = step, Value = val, CustomMinimumSize = new Vector2(180, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            var l = new Label { Text = Fmt(val, step), CustomMinimumSize = new Vector2(70, 0) };
            var target = obj; var k = key;
            s.ValueChanged += x => { _d.Feel.Set(target, k, x); l.Text = Fmt(x, step); };
            h.AddChild(s); h.AddChild(l);
            _rows.AddChild(h);
        }
    }

    /// <summary>Surgu araligi varsayilan degerden: desibel -40..+12, sayac ve boyutlar tam sayi adim, gerisi 0..3 kat.</summary>
    static (double min, double max, double step) RangeFor(string key, double d0)
    {
        if (key.EndsWith("Db")) return (-40, 12, 0.5);
        if (key == "timeScale") return (0, 1, 0.01);
        double max = d0 > 0 ? d0 * 3 : 1;
        bool whole = Math.Abs(d0 - Math.Round(d0)) < 1e-9 && d0 >= 2;
        return (0, max, whole ? 1 : Math.Max(0.001, Math.Round(max / 300, 3)));
    }

    static string Fmt(double x, double step) => step >= 1 ? $"{x:0}" : step >= 0.01 ? $"{x:0.00}" : $"{x:0.000}";

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.F1)
        {
            _panel.Visible = !_panel.Visible;
            Input.MouseMode = _panel.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        }
        // Gecikme olcumu: atis girisinin oyuna geldigi andan tepkiyi iceren karenin cizimine kadar.
        if (e.IsActionPressed("fire") && !e.IsEcho() && _pendingInputUsec == 0 && !Open) Pending(Time.GetTicksUsec());
    }

    void Pending(ulong usec) { _pendingInputUsec = usec; _pendingFrame = Engine.GetProcessFrames(); _awaitDraw = false; }

    /// <summary>Sinamalar icin: girisi kodla isaretler (giris olayi kuyruga konduğu an).</summary>
    public void MarkInput() => Pending(Time.GetTicksUsec());

    /// <summary>Girdi bir eyleme donusmedi (silah beklemede): olcum ornegi degil, bekleyen giris dusurulur.</summary>
    public void CancelInput() { _pendingInputUsec = 0; _awaitDraw = false; }

    /// <summary>Oyun girise gorsel tepki verdi (isin, puskurtme, mermi, tutma, yerlestirme).</summary>
    public void MarkReaction() => MarkReaction(null);

    public void MarkReaction(string head)
    {
        if (_pendingInputUsec == 0 || _awaitDraw) return;
        _awaitDraw = true;
        _reactHead = head ?? "?";
        _reactFrame = Engine.GetProcessFrames();
    }

    void OnPostDraw()
    {
        if (_awaitDraw && !_headless) Complete(Time.GetTicksUsec(), true);
    }

    void Complete(ulong endUsec, bool drawn)
    {
        var s = new ReactionSample(_reactHead, _pendingInputUsec, _pendingFrame, _reactFrame, endUsec, drawn);
        _pendingInputUsec = 0;
        _awaitDraw = false;
        LastLatencyMs = s.Ms;
        if (!_latency.TryGetValue(s.Head, out var list)) _latency[s.Head] = list = new();
        list.Add(s.Ms);
        if (list.Count > MaxSamples) list.RemoveAt(0);
        Reacted?.Invoke(s);
    }

    public double Median(string head) => _latency.TryGetValue(head, out var l) && l.Count > 0 ? l.OrderBy(x => x).ElementAt(l.Count / 2) : -1;

    public override void _Process(double delta)
    {
        // Basliksiz: cizim yok; tepkili karenin sonu burasi (ProcessPriority en sonda).
        if (_awaitDraw && _headless) Complete(Time.GetTicksUsec(), false);
        // Tepkisiz kalan giris eskir (yanlis uzun ornek olmasin).
        if (_pendingInputUsec != 0 && !_awaitDraw && Engine.GetProcessFrames() - _pendingFrame > StaleInputFrames) _pendingInputUsec = 0;
        if (!Open) return;
        var lines = new List<string> { $"FPS {Engine.GetFramesPerSecond():0} · son duraklama {_d.LastHitStopMs:0} ms (istenen {_d.LastHitStopRequestedMs:0})" };
        for (int i = 0; i < Capa.HeadNames.Length; i++)
        {
            string id = Capa.HeadId((Capa.Head)i);
            int n = _latency.TryGetValue(id, out var l) ? l.Count : 0;
            lines.Add(n == 0 ? $"{Capa.HeadNames[i]}: —" : $"{Capa.HeadNames[i]}: son {l[^1]:0.0} · medyan {Median(id):0.0} ms ({n})");
        }
        _stats.Text = string.Join("\n", lines);
    }
}
