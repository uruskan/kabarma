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
    float _batchAt;
    double _gpuSum;
    int _gpuN;
    string _outDir;

    public override void _Ready()
    {
        // --shots_out=progress/... (depo kokune gore) ciktiyi baska klasore yazar (once/sonra karsilastirmalari).
        var rel = Main.Args.TryGetValue("shots_out", out var o) && !string.IsNullOrEmpty(o) ? o.Replace('\\', '/').TrimEnd('/') : "progress/m1/dunya/shots";
        _outDir = System.IO.Path.IsPathRooted(rel) ? rel : ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../" + rel;
        DirAccess.MakeDirRecursiveAbsolute(_outDir + "/deck");
    }

    World W => _d.World;

    public override void _Process(double delta)
    {
        _t += (float)(delta / Mathf.Max(0.0001, Engine.TimeScale));
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d == null || _finished) return;

        if (_clip != null) { Clip(); return; }
        if (_queue.Count > 0) { Capture(); return; }
        if (_cam != null && _cam.Current) { _d.Player.Cam.MakeCurrent(); _d.SpeedScale = _savedSpeed; if (_deckQueued && _dayDone) Finish(); return; }

        if (_quick)
        {
            if (_t < 3f) return;
            if (!_dayDone)
            {
                // --shots_times=ogle,gunbatimi ve --shots_marks=us,gol: hizli denemede yalniz bu saatler ve yer imleri.
                var times = Main.Args.TryGetValue("shots_times", out var st) ? st.Split(',') : World.ShotTimes;
                foreach (var tm in times) Enqueue(tm, "");
                _dayDone = true; _nightDone = true; _deckQueued |= Main.Args.ContainsKey("shots_times");
                return;
            }
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

    // --shots_clip=YER_IMI[:saat]: yalniz kisa klip (ruzgar salinimi, su), 0,1 sn arayla 40 kare; GIF'e pipeline/clip_gif.py cevirir.
    readonly string _clip = Main.Args.TryGetValue("shots_clip", out var c) ? c : null;
    int _clipFrame;
    double _clipNext;

    void Clip()
    {
        var parts = _clip.Split(':');
        string bm = parts[0], time = parts.Length > 1 ? parts[1] : "ogle";
        if (_t < 3f) return;
        if (_cam == null || !_cam.Current) { W.ForceTime(time); _cam = W.ShowBookmark(W.Bookmark(bm), _cam, _d.OcakPos); _clipNext = _t + 1.0; return; }
        if (_t < _clipNext) return;
        _clipNext = _t + 0.1;
        var dir = $"{_outDir}/clip";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        GetViewport().GetTexture().GetImage().SavePng($"{dir}/{bm}_{time}_{_clipFrame:000}.png");
        if (++_clipFrame >= 40) { _finished = true; GD.Print($"SHOTS RESULT ok clip={bm}:{time} frames={_clipFrame}"); GetTree().Quit(0); }
    }

    void Enqueue(string time, string sub)
    {
        var marks = Main.Args.TryGetValue("shots_marks", out var sm) ? sm.Split(',') : null;
        foreach (var b in W.Biomes.World.Bookmarks)
            if (marks == null || System.Array.IndexOf(marks, b.Id) >= 0) _queue.Enqueue((b.Id, time, sub));
        _savedSpeed = _d.SpeedScale;
        _d.SpeedScale = 0.001f; // eylem durur, cizim surer
        // Insa basligi acik kalirsa hayalet ve ipucu kadraja girer: oyuncunun yapacagi gibi Kazma'ya gecilir.
        _d.HoldCapa(); // karar 23: elde Capa (hayalet yok)
        // Ipucu suresi oyun zamaniyla akar; yavaslatilmis zamanda donar, elle temizlenir.
        _d.Hud.Hint("", 0f);
        _batchAt = _t + 1.0f;
        _wait = 0;
    }

    void Capture()
    {
        var (bm, time, sub) = _queue.Peek();
        if (_wait == 0)
        {
            W.ForceTime(time);
            _cam = W.ShowBookmark(W.Bookmark(bm), _cam, _d.OcakPos);
        }
        _wait++;
        // GPU cizim suresi (gorsel-dunya): yer imi basina son karelerin ortalamasi, kademe karsilastirmasi icin.
        var vrid = GetViewport().GetViewportRid();
        if (_wait == 1) { RenderingServer.ViewportSetMeasureRenderTime(vrid, true); _gpuSum = 0; _gpuN = 0; }
        else if (_wait > 6) { _gpuSum += RenderingServer.ViewportGetMeasuredRenderTimeGpu(vrid); _gpuN++; }
        if (_wait < 12 || _t < _batchAt) return; // golge, SSAO, isik kupu ve arayuz ipucu otursun
        var img = GetViewport().GetTexture().GetImage();
        var path = $"{_outDir}/{(sub == "" ? "" : sub + "/")}{bm}_{time}.png";
        img.SavePng(path);
        _saved.Add(path);
        GD.Print($"SHOTS saved {path} {img.GetWidth()}x{img.GetHeight()} gpu_ms={(_gpuN > 0 ? _gpuSum / _gpuN : 0):0.00} frames={_gpuN}");
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
