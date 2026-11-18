using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// --test=menu: oyuncunun yolu, dugmelerin kendi Pressed geri cagrilariyla (OYNA.bat'in ilk gordugu akis):
/// Ana menu -> Yeni oyun -> Yuva 1 -> oyun -> Esc menusu "Ana menu" (kaydeder) -> Devam (yukler) -> pencere kapatma
/// bildirimi "cikis" kaydi yazar. Yalniz user://test/menu_* altina yazar ve sonunda siler.
/// </summary>
public partial class MenuFlowTest : Node
{
    readonly Main _main;
    int _stage;
    float _t, _wait;
    string _root;
    Director _first;
    readonly System.Collections.Generic.List<string> _fail = new();

    public MenuFlowTest(Main main) { _main = main; Name = "MenuFlowTest"; ProcessMode = ProcessModeEnum.Always; }

    public static void Prepare()
    {
        Main.Args["menu"] = "1";
        Main.Args.TryAdd("mute", "1");
        Sfx.Muted = true;
        Main.Args["save_root"] = $"user://test/menu_{OS.GetProcessId()}_{Time.GetTicksUsec()}";
    }

    public override void _Ready() => _root = Main.Args["save_root"];

    MainMenu Menu => _main.GetChildren().OfType<MainMenu>().FirstOrDefault(m => !m.IsQueuedForDeletion());

    bool Press(RustMenu m, string prefix)
    {
        var b = m?.FindButton(prefix);
        if (b == null || b.Disabled) return false;
        GD.Print($"MENUTEST press '{b.Text}'");
        b.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt; _wait += dt;
        if (_t > 240f) { Finish($"zaman asimi (asama {_stage})"); return; }
        var d = _main.Game;
        switch (_stage)
        {
            case 0: // menu acildi: Devam kapali (kayit yok), Yeni oyun -> Yuva 1
                if (Menu == null || _wait < 0.5f) break;
                if (Menu.FindButton("DEVAM") is { Disabled: false }) _fail.Add("kayit yokken Devam acik");
                if (!Press(Menu, "YENİ OYUN")) { Finish("Yeni oyun dugmesi yok"); return; }
                if (!Press(Menu, "YUVA 1")) { Finish("Yuva 1 satiri yok"); return; }
                _stage = 1; _wait = 0;
                break;
            case 1: // oyun kuruldu, acilis bitti, kaydedilebilir
                if (d == null || !d.CanSave(out _)) break;
                _first = d;
                var pm = d.GetChildren().OfType<PauseMenu>().FirstOrDefault();
                if (pm == null) { Finish("Esc menusu yok"); return; }
                if (!Press(pm, "ANA MENÜ")) { Finish("Ana menu dugmesi yok"); return; }
                _stage = 2; _wait = 0;
                break;
            case 2: // menuye donuldu, kayit yazilmis: Devam acik
                if (Menu == null || _wait < 0.5f) break;
                if (_main.Game != null) _fail.Add("ana menude oyun hala acik");
                if (!Press(Menu, "DEVAM")) { Finish("kayittan sonra Devam kapali"); return; }
                _stage = 3; _wait = 0;
                break;
            case 3: // yuklendi
                if (d == null || d == _first) { if (_wait > 20f) Finish("Devam oyunu kurmadi"); break; }
                if (d.LastRestore == null) { if (_wait > 20f) Finish("Devam kaydi yuklemedi"); break; }
                if (d.LastRestore.Missing.Count > 0) _fail.Add($"eksik bolum: {string.Join(",", d.LastRestore.Missing)}");
                if (!d.Pod.IntroDone) _fail.Add("yuklenen oyunda kapsul acilisi oynuyor");
                _stage = 4; _wait = 0;
                break;
            case 4: // pencere kapatma bildirimi cikis kaydi yazar
                if (_wait < 1f) break;
                int before = d.Saver.SavesWritten;
                _main.Notification((int)NotificationWMCloseRequest);
                if (d.Saver.SavesWritten != before + 1) _fail.Add($"kapatmada kayit yazilmadi ({d.Saver.LastError})");
                else
                {
                    var m = d.Store.Read(d.SaveSlot).Manifest;
                    if (m.Reason != "cikis") _fail.Add($"kapatma kaydinin nedeni '{m.Reason}'");
                }
                Finish(null);
                break;
        }
    }

    void Finish(string fail)
    {
        if (fail != null) _fail.Add(fail);
        if (_root != null && _root.StartsWith("user://test/menu_"))
        {
            var dir = ProjectSettings.GlobalizePath(_root);
            try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); } catch { }
        }
        string res = _fail.Count == 0 ? "RESULT ok menu new->esc->menu->devam->close" : "RESULT fail " + string.Join("; ", _fail);
        GD.Print(res);
        SetProcess(false);
        GetTree().Quit(_fail.Count == 0 ? 0 : 1);
    }
}
