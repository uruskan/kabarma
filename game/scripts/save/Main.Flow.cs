using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyun akisi (TEKNIK 9): ana menu -> oyun (yeni ya da kayittan) -> Esc menusunden ana menuye; pencere kapanirken kayit.
/// Ayni surecte yeni oyun ve yukleme temiz calissin diye her gecisten once Godot statikleri sifirlanir (GameStatics).
/// Otomasyon kosulari (komut satiri argumanli) menuyu gormez; Main._Ready eskisi gibi dogrudan Director kurar.
/// </summary>
public partial class Main
{
    MainMenu _menu;
    string _menuNotice;
    GameData _menuData;

    /// <summary>Main._Ready'den: argumansiz acilista (ya da --menu) ana menuyu kurar. Kurduysa true.</summary>
    bool ShowMainMenu()
    {
        if (Args.Count > 0 && !Args.ContainsKey("menu")) return false;
        OpenMainMenu();
        return true;
    }

    void OpenMainMenu()
    {
        _menuData ??= GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        _menu = new MainMenu(this, _menuData) { Notice = _menuNotice };
        _menuNotice = null;
        AddChild(_menu);
        GD.Print("MENU main");
    }

    /// <summary>Menuden oyuna: <paramref name="load"/> null ise yeni oyun. Oyun bu yuvaya kaydeder.</summary>
    public void StartGame(SaveGame load, string slot, string preset = null)
    {
        if (_menu != null) { RemoveChild(_menu); _menu.QueueFree(); _menu = null; }
        EndGame();
        var data = _menuData ?? GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        _dir = new Director { Name = "Director", Controls = _controls, PendingLoad = load, SaveRoot = MenuRoot(data), SaveSlot = slot, NewPreset = preset };
        if (Args.TryGetValue("fast", out var f)) _dir.SpeedScale = float.Parse(f, System.Globalization.CultureInfo.InvariantCulture);
        AddChild(_dir);
        GD.Print($"GAME start slot={slot} load={(load != null ? load.Manifest.Day.ToString() : "-")}");
    }

    /// <summary>Menunun kayit koku: sahibin user://saves; --save_root yalniz test kokunu (gorsel sinama) gosterebilir.</summary>
    public string MenuRoot(GameData data) => Args.TryGetValue("save_root", out var r) && r.StartsWith(data.Save.TestRoot, System.StringComparison.Ordinal) ? r : data.Save.Root;

    /// <summary>Esc menusu "Ana menu": oyunu kapatir (kayit cagirandan once alinmis olur), menuyu acar.</summary>
    public void ReturnToMenu()
    {
        EndGame();
        OpenMainMenu();
    }

    /// <summary>Yukleme basarisiz: oyunu kapatir, menude nedeni gosterir.</summary>
    public void ReturnToMenuWith(string notice)
    {
        _menuNotice = notice;
        ReturnToMenu();
    }

    /// <summary>Sinama ve ortak oyun icin: oyunu ayni surecte bir kayittan bastan kurar (sahne bastan yuklenir).</summary>
    public Director Reload(SaveGame load, string root, string slot)
    {
        EndGame();
        _dir = new Director { Name = "Director", Controls = _controls, PendingLoad = load, SaveRoot = root, SaveSlot = slot };
        if (Args.TryGetValue("fast", out var f)) _dir.SpeedScale = float.Parse(f, System.Globalization.CultureInfo.InvariantCulture);
        AddChild(_dir);
        return _dir;
    }

    /// <summary>
    /// Oyunu agactan hemen cikarir (_ExitTree simdi kosar: eski Director'un Events.Clear'i yenisinin aboneliklerini silmesin),
    /// sonra statikleri sifirlar.
    /// </summary>
    void EndGame()
    {
        if (_dir != null && IsInstanceValid(_dir))
        {
            if (_dir.GetParent() == this) RemoveChild(_dir);
            _dir.QueueFree();
        }
        _dir = null;
        GameStatics.Reset(GetTree());
    }

    public Director Game => _dir;

    // ---------- Ortak oyun girisi (ortak akisi doldurur) ----------

    /// <summary>Ortak akisi kendi Main partial'inda uygular: ok = true ise menudeki "Ortak oyun" acilir.</summary>
    partial void CoopEntry(ref bool ok);
    /// <summary>Ortak akisi kendi Main partial'inda uygular: lobi/davet ekranini acar.</summary>
    partial void CoopOpen(MainMenu menu);

    public bool CoopAvailable { get { bool ok = false; CoopEntry(ref ok); return ok; } }
    public void OpenCoop(MainMenu menu) => CoopOpen(menu);

    // ---------- Cikista kayit ----------

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest) return;
        var s = _dir?.Saver;
        if (s == null || !s.Enabled) return;
        _dir.ReleaseForSave(); // elde yuk, itilen araba birakilir
        if (!s.SaveNow("cikis", quiet: true)) GD.Print($"SAVE cikista alinamadi ({s.LastError}): son kayit gecerli"); // gece suruyorsa
    }
}
