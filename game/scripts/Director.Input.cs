using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: oyuncu girisi (menu tuslari, baslik, kemer, cark, kart, E). Eylemler ve baglamalari input.json'da
/// (Controls). Oyun kolu: sag cubukla bakis, sol tampon basili + sag cubukla baslik carki, ✚ ve Ⓐ ile menu.
/// Bot dogrudan kipte (--test=bot) tetigi kendisi verir; girdi kipinde (--test=input) her sey sentetik olaydan gelir.
/// </summary>
public partial class Director
{
    bool _menuClosedThisFrame;
    bool _menuWasOpen;
    int _menuSel;
    ulong _lookUsec;
    /// <summary>Son karede sag cubuk bakisinin kullandigi zaman adimi (gercek sn x --fast); girdi surucusu nisanda kullanir.</summary>
    public float LookDt { get; private set; } = 1f / 60f;

    /// <summary>Girdi eslemesi, son aygit, glif (Main kurar).</summary>
    public Controls Controls { get; set; }
    /// <summary>Tus atama paneli (F2 / Gorunum).</summary>
    public ControlsPanel ControlsPanel { get; private set; }
    /// <summary>Acik menudeki imlecin satiri (oyun kolu ✚ ile tasir).</summary>
    public int MenuCursorIndex => _menuSel;

    /// <summary>--test=input: botu girdi kipinde kurar; kararlari sentetik klavye-fare ya da kol olaylarina doner.</summary>
    public void StartInputDriver(InputDevice device)
    {
        Bot = new Bot(this, device) { Name = "Bot" };
        AddChild(Bot);
    }

    /// <summary>Karenin girisi. Tetik durumunu verir; menu acikken tetik yok.</summary>
    void ReadInput(out bool fire, out bool firePressed)
    {
        // Giris
        fire = Input.IsActionPressed("fire");
        firePressed = Input.IsActionJustPressed("fire");
        if (Bot != null && !Bot.UsesInput) { fire = Bot.Fire; firePressed = Bot.FirePressed; Bot.FirePressed = false; }
        if (Input.IsActionJustPressed("controls_panel")) ToggleControlsPanel();
        if (ControlsPanel != null && ControlsPanel.Visible) { fire = false; firePressed = false; return; }
        PadLook();
        if (Hud.MenuOpen)
        {
            if (!_menuWasOpen) _menuSel = 0;
            for (int i = 0; i < 9; i++) if (Input.IsActionJustPressed($"head_{i + 1}")) Hud.ActivateMenu(i);
            // Menu imleci: ✚ / ok tuslari tasir, Ⓐ / Enter onaylar (oyun koluyla sayi tusu olmadan secim).
            int n = Hud.MenuCount;
            if (n > 0)
            {
                if (Input.IsActionJustPressed("ui_down")) _menuSel = (_menuSel + 1) % n;
                if (Input.IsActionJustPressed("ui_up")) _menuSel = (_menuSel + n - 1) % n;
                _menuSel = Mathf.Clamp(_menuSel, 0, n - 1);
                // Hasat Raporu ustte: Ⓐ / Enter once raporu kapatir (Director._Process), ayni basis menude satin almaz.
                if (Input.IsActionJustPressed("ui_accept") && !Hud.ReportOpen) Hud.ActivateMenu(_menuSel);
                Hud.MenuCursor(_menuSel);
            }
            if (Input.IsActionJustPressed("interact") || Input.IsActionJustPressed("ui_cancel") || Input.IsActionJustPressed("bag")) { Hud.HideMenu(); _menuClosedThisFrame = true; }
            fire = false; firePressed = false;
        }
        else
        {
            bool alt = Input.IsKeyPressed(Key.Alt);
            for (int i = 0; i < 8; i++)
                if (Input.IsActionJustPressed($"head_{i + 1}")) { if (alt) UseBelt(i); else Capa.Select((Capa.Head)i); }
            if (Input.IsActionJustPressed("bag")) OpenBagMenu();
            // Baslik carki: Q / LB basili tut, fareyle ya da sag cubukla sec, birak
            if (Input.IsActionJustPressed("head_wheel")) { Hud.OpenWheel((int)Capa.Current); Player.LookLocked = true; }
            if (Hud.WheelOpen && Input.IsActionJustReleased("head_wheel")) { int sel = Hud.CloseWheel(); Player.LookLocked = false; if (sel >= 0) Capa.Select((Capa.Head)sel); }
            if (Capa.Current == Capa.Head.Insa)
            {
                if (Input.IsActionJustPressed("build_next")) Capa.CyclePlaceable(1);
                if (Input.IsActionJustPressed("build_prev")) Capa.CyclePlaceable(-1);
                if (Input.IsActionJustPressed("build_rotate")) Capa.RotateGhost();
            }
            else if (!Hud.WheelOpen)
            {
                int n = Capa.HeadNames.Length;
                if (Input.IsActionJustPressed("build_next")) Capa.Select((Capa.Head)(((int)Capa.Current + 1) % n));
                if (Input.IsActionJustPressed("build_prev")) Capa.Select((Capa.Head)(((int)Capa.Current + n - 1) % n));
            }
            // Kartlar (kolda ✚ sol/yukari/sag): menu acikken ✚ menuyu tasir, kart secmez.
            if (Input.IsActionJustPressed("card_1")) ChooseCard(0);
            if (Input.IsActionJustPressed("card_2")) ChooseCard(1);
            if (Input.IsActionJustPressed("card_3")) ChooseCard(2);
        }
        _menuWasOpen = Hud.MenuOpen;
        if (Input.IsActionJustPressed("interact") && !Hud.MenuOpen && !_menuClosedThisFrame) Interact();
        _menuClosedThisFrame = false;
    }

    /// <summary>
    /// Sag cubuk: kamera (gercek zamanla; vurus duraklamasi ve kart yavaslamasi bakisi yavaslatmaz) ya da cark acikken
    /// baslik secimi. Fare bakisi Player._Input'ta.
    /// </summary>
    void PadLook()
    {
        ulong now = Time.GetTicksUsec();
        // Gercek zaman x --fast (oyuncuda 1): sinamanin hizlandirilmis zamaninda da bakis ayni oranda doner.
        float rdt = _lookUsec == 0 ? 0f : Mathf.Min(0.1f, (now - _lookUsec) / 1e6f) * SpeedScale;
        _lookUsec = now;
        if (rdt > 0f) LookDt = rdt;
        if (Controls == null) return;
        var stick = Input.GetVector("look_left", "look_right", "look_up", "look_down");
        if (stick == Vector2.Zero) return;
        var lk = Controls.Data.Look;
        if (Hud.WheelOpen) { Hud.WheelMotion(stick * lk.WheelStickGain); return; }
        if (Player.LookLocked || Player.Down) return;
        float mag = Mathf.Min(1f, stick.Length());
        var v = stick / stick.Length() * Mathf.Pow(mag, lk.Curve);
        Player.Yaw.RotateY(-v.X * lk.PadYawSpeed * rdt);
        var r = Player.Pitch.Rotation;
        r.X = Mathf.Clamp(r.X - v.Y * lk.PadPitchSpeed * rdt, lk.PitchMin, lk.PitchMax);
        Player.Pitch.Rotation = r;
    }

    void ToggleControlsPanel()
    {
        if (Controls == null) return;
        if (ControlsPanel == null) { ControlsPanel = new ControlsPanel(); ControlsPanel.Init(Controls); AddChild(ControlsPanel); }
        ControlsPanel.Toggle();
    }

    public void Interact()
    {
        if (InteractEconomy()) return; // Director.Economy.cs: El Arabasi, Sandik, Uyku Tulumu
        var st = Stations.Where(s => s.InReach(Player.GlobalPosition)).OrderBy(s => s.GlobalPosition.DistanceTo(Player.GlobalPosition)).FirstOrDefault();
        if (st != null) { OpenStationMenu(st); return; }
        if (Pod.IsRamp && new Vector2(Player.GlobalPosition.X - Pod.PadCenter.X, Player.GlobalPosition.Z - Pod.PadCenter.Z).Length() < 4.5f) { OpenSellMenu(); return; }
        if (PenInteract()) return; // Director.Pen.cs: Gidak'a bugday, kapi, Yemlik, Folluk, yerdeki yumurta
        if (Gidak.Visible && !Gidak.Swollen && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < 3f && State.Backpack.Remove("wheat", 1))
            Gidak.Feed();
    }
}
