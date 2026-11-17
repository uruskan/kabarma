using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// M1'in 16 ekran goruntusu (SANAT-SES §9): 4 yer imi x 4 gun saati, 1920 x 1080, arayuz acik. Pencereli calisir
/// (basliksiz cizim yoktur). --test=shots:
///  - Bot ilk gunu oynar (gercek us: Ocak, tarla, istasyonlar). Ilk gecenin 2. dalgasinda 4 "kabarma_gecesi" goruntusu,
///    bot zincirin sonuna varinca sabah, ogle, gunbatimi (12 goruntu); ardindan Deck cozunurlugunde (1280 x 800) ogle
///    goruntuleri (arayuz okunurlugu). Her cekimde oyun cok yavaslatilir, kamera yer imine gecer, birkac kare beklenir.
///  - --shots_quick: bot yok; dunya kurulunca 16 goruntu hemen (ayar denemesi icin, us bos).
/// Cikti: progress/m1/dunya/shots/&lt;yer imi&gt;_&lt;saat&gt;.png, deck/ alt klasoru; "SHOTS RESULT ok|fail".
/// </summary>
public partial class WorldShots : Node
{
    Director _d;
    readonly bool _quick = Main.Args.ContainsKey("shots_quick");
    readonly Queue<(string bm, string time, string dir)> _queue = new();
    readonly List<string> _saved = new();
    Camera3D _cam;
    int _wait;
    float _t, _settle;
    bool _nightDone, _dayDone, _deckQueued, _finished;
    float _savedSpeed = 1f;
    string _outDir;

    public override void _Ready()
    {
        _outDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/m1/dunya/shots";
        DirAccess.MakeDirRecursiveAbsolute(_outDir + "/deck");
    }

    World W => _d.World;

    public override void _Process(double delta)
    {
        _t += (float)(delta / Mathf.Max(0.0001, Engine.TimeScale));
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d == null || _finished) return;

        if (_queue.Count > 0) { Capture(); return; }
        if (_cam != null && _cam.Current) { _d.Player.Cam.MakeCurrent(); _d.SpeedScale = _savedSpeed; if (_deckQueued && _dayDone) Finish(); return; }

        if (_quick)
        {
            if (_t < 3f) return;
            if (!_dayDone) { foreach (var tm in World.ShotTimes) Enqueue(tm, ""); _dayDone = true; _nightDone = true; return; }
        }
        else
        {
            var n = _d.Night;
            if (!_nightDone && n != null && n.Phase == NightPhase.Waves && n.WaveIndex == 1 && n.PhaseTime > 4f)
            {
                _nightDone = true;
                Enqueue("kabarma_gecesi", "");
                GD.Print($"SHOTS night wave 2 t={_t:0}");
                return;
            }
            bool end = _d.Bot != null && _d.Bot.Done;
            if (!_dayDone && _nightDone && end)
            {
                _settle += (float)delta;
                if (_settle < 2f) return;
                _dayDone = true;
                foreach (var tm in new[] { "sabah", "ogle", "gunbatimi" }) Enqueue(tm, "");
                return;
            }
            if (_t > 1500f) { GD.Print($"SHOTS RESULT fail timeout night={_nightDone} day={_dayDone} saved={_saved.Count}"); GetTree().Quit(1); _finished = true; return; }
        }
        if (_dayDone && _nightDone && !_deckQueued)
        {
            _deckQueued = true;
            DisplayServer.WindowSetSize(new Vector2I(1280, 800));
            Enqueue("ogle", "deck");
        }
    }

    void Enqueue(string time, string sub)
    {
        foreach (var b in W.Biomes.World.Bookmarks) _queue.Enqueue((b.Id, time, sub));
        _savedSpeed = _d.SpeedScale;
        _d.SpeedScale = 0.001f; // eylem durur, cizim surer
        _wait = 0;
    }

    void Capture()
    {
        var (bm, time, sub) = _queue.Peek();
        if (_wait == 0)
        {
            W.ForceTime(time);
            _cam = W.ShowBookmark(W.Bookmark(bm), _cam);
        }
        _wait++;
        if (_wait < 12) return; // golge, SSAO ve isik kupu otursun
        var img = GetViewport().GetTexture().GetImage();
        var path = $"{_outDir}/{(sub == "" ? "" : sub + "/")}{bm}_{time}.png";
        img.SavePng(path);
        _saved.Add(path);
        GD.Print($"SHOTS saved {path} {img.GetWidth()}x{img.GetHeight()}");
        _queue.Dequeue();
        _wait = 0;
        if (_queue.Count == 0) W.ForceTime(null);
    }

    void Finish()
    {
        _finished = true;
        int main = _saved.Count(p => !p.Contains("/deck/"));
        bool ok = main == 16;
        GD.Print($"SHOTS RESULT {(ok ? "ok" : "fail")} main={main} deck={_saved.Count - main} quick={_quick}");
        GetTree().Quit(ok ? 0 : 1);
    }
}
