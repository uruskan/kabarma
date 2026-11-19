using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director, ogretici (M6 ogretici; data/suna.json "tutorial"): ilk gunden Ana Maya'ya her yeni sistem ilk gorundugunde Suna kisa bir
/// yonlendirme soyler. Tetikleyiciler: biyom, istasyon, otomasyon, arac, Ata, Kabarma Seviyesi, Tuzuk, Husumet (Cekisme). Her tetikleyici
/// bir kez calar ("gorulenler" kayitta, SaveOrder.Spawn bolumu "tutorial"); satirlar siraya girer ve en az cooldownSeconds arayla soylenir.
/// Ilk gunun senaryosu (scenario.json) bundan ayridir. Ses yoksa satir altyazi olarak kutuda kalir (Hud.Voice).
/// </summary>
public partial class Director
{
    readonly HashSet<string> _tutSeen = new();
    readonly Queue<TutorialDef> _tutQueue = new();
    float _tutT, _tutCool;

    /// <summary>Gorulen ogretici kimlikleri (sinama).</summary>
    public IReadOnlyCollection<string> TutorialSeen => _tutSeen;
    public int TutorialPending => _tutQueue.Count;

    /// <summary>Suna replik dili: --lang ya da tr (dil akisi gelince oyuncu ayari buraya baglanir).</summary>
    public string SunaLang => Main.Args.TryGetValue("lang", out var l) && l == "en" ? "en" : "tr";

    void InitOgretici()
    {
        Events.StationPlaced += id => { TutorialFire("station", ""); if (IsAutomation(id)) TutorialFire("automation", ""); };
        Events.PiecePlaced += id => { if (IsAutomation(id)) TutorialFire("automation", ""); };
        Save.Add(SaveSection.Json("tutorial", 1,
            () => _tutSeen.OrderBy(x => x, System.StringComparer.Ordinal).ToList(),
            (List<string> l) => { _tutSeen.Clear(); foreach (var id in l ?? new()) _tutSeen.Add(id); }, SaveOrder.Spawn));
        AddChild(new TutorialTicker(this) { Name = "TutorialTicker" });
    }

    bool IsAutomation(string id) => Data.Suna.AutomationIds.Any(a => id.Contains(a, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>Tetikleyici goruldu: eslesen, daha once soylenmemis ogretici siraya girer.</summary>
    public void TutorialFire(string trigger, string arg)
    {
        foreach (var t in Data.Suna.Tutorial.Where(t => t.Trigger == trigger && (t.Arg.Length == 0 || t.Arg == arg) && !_tutSeen.Contains(t.Id)))
        {
            _tutSeen.Add(t.Id);
            _tutQueue.Enqueue(t);
        }
    }

    /// <summary>Saniyede bir: yoklanan tetikleyiciler ve siradaki satirin soylenmesi.</summary>
    internal void TutorialTick(float dt)
    {
        _tutCool -= dt;
        _tutT += dt;
        if (_tutT >= 1f)
        {
            _tutT = 0f;
            Poll();
        }
        if (_tutQueue.Count > 0 && _tutCool <= 0f && Hud != null && !Hud.MenuOpen && Pod?.IntroDone != false)
        {
            var t = _tutQueue.Dequeue();
            SayLine(t.Line);
            GD.Print($"TUTORIAL {t.Id}");
            _tutCool = Data.Suna.CooldownSeconds;
        }
    }

    void Poll()
    {
        if (Player == null || World?.Map == null) return;
        var p = Player.GlobalPosition;
        TutorialFire("biome", World.Map.BiomeAt(p.X, p.Z));
        if (Vehicles.Count > 0) TutorialFire("vehicle", "");
        if (Atalar?.Fight != null) TutorialFire("ata", "");
        if (NightActive && State.Night != null)
        {
            TutorialFire("ks", State.Night.Ks.ToString());
            foreach (var t in Data.Suna.Tutorial.Where(t => t.Trigger == "ks" && int.TryParse(t.Arg, out var min) && State.Night.Ks >= min && !_tutSeen.Contains(t.Id)).ToList())
            { _tutSeen.Add(t.Id); _tutQueue.Enqueue(t); }
            if (State.TuzukFor(State.Night.Ks, KabarmaKind.Full).Any) TutorialFire("tuzuk", "");
        }
        if (State.Holding?.State.Camps.Any(c => c.Husumet > 0) == true) TutorialFire("husumet", "");
    }
}

/// <summary>Ogretici zamanlayicisi (Director._Process'e dokunmamak icin ayri dugum).</summary>
public partial class TutorialTicker : Node
{
    readonly Director _d;
    public TutorialTicker(Director d) { _d = d; }
    public override void _Process(double delta) => _d.TutorialTick((float)delta);
}
