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
    /// <summary>Fareyle secilen satir menu imlecine de gecer (Uretim paneli, Hud.Craft.cs).</summary>
    public void SyncMenuCursor(int i) => _menuSel = i;

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
        if (Hud.CraftTyping) { fire = false; firePressed = false; _menuWasOpen = true; return; } // Uretim arama kutusu: tuslar yaziya gider
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
            // Karar 23: 1-8 ve tekerlek (kolda RB / LT) kemeri secer; secili yuvadaki esya eldedir (Director.Belt.cs).
            // Ctrl ya da Alt basiliyken tekerlek kamerayi yakinlastirir (input.json look.zoomModifiers).
            bool zoomMod = ZoomModifierHeld();
            for (int i = 0; i < 8; i++)
                if (Input.IsActionJustPressed($"head_{i + 1}")) SelectBelt(i);
            if (!zoomMod && !Hud.WheelOpen)
            {
                if (Input.IsActionJustPressed("belt_next")) ScrollBelt(1);
                if (Input.IsActionJustPressed("belt_prev")) ScrollBelt(-1);
            }
            if (Input.IsActionJustPressed("bag")) OpenBagMenu();
            // Baslik carki: yalniz Capa eldeyken; Q / LB basili tut, fareyle ya da sag cubukla sec, birak.
            if (Input.IsActionJustPressed("head_wheel") && Capa.InHand && !Hud.WheelOpen) { Hud.OpenWheel((int)Capa.Current); Player.LookLocked = true; }
            if (Hud.WheelOpen && Hud.OpenWheelKind == Hud.WheelKind.Heads && Input.IsActionJustReleased("head_wheel")) { int sel = Hud.CloseWheel(); Player.LookLocked = false; if (sel >= 0) Capa.Select((Capa.Head)sel); }
            // Insa Plani: sag tik (kolda LB) basili parca carki; birakinca parca secilir.
            if (Input.IsActionJustPressed("build_wheel") && Capa.PlanInHand && !Hud.WheelOpen) OpenPieceWheel();
            if (Hud.WheelOpen && Hud.OpenWheelKind == Hud.WheelKind.Pieces && Input.IsActionJustReleased("build_wheel"))
            {
                int sel = Hud.CloseWheel(); Player.LookLocked = false;
                var pieces = Capa.PlanPieces;
                if (sel >= 0 && sel < pieces.Count) Capa.SelectPiece(pieces[sel]);
            }
            if (Input.IsActionJustPressed("build_rotate") && Capa.CurrentPlaceable != "") Capa.RotateGhost();
            if (Input.IsActionJustPressed("build_rotate") && Capa.HeldItem == Data.Power.CableItem) Power?.ToggleMode(); // guc: Kablo eldeyken R guc/sinyal
            // Kartlar (kolda ✚ sol/yukari/sag): menu acikken ✚ menuyu tasir, kart secmez.
            if (Input.IsActionJustPressed("card_1")) ChooseCard(0);
            if (Input.IsActionJustPressed("card_2")) ChooseCard(1);
            if (Input.IsActionJustPressed("card_3")) ChooseCard(2);
            Player.CameraInput(!zoomMod || Hud.WheelOpen); // oyuncu akisi: V / R3 kip; Ctrl / Alt + tekerlek yakinlastirir
        }
        _menuWasOpen = Hud.MenuOpen;
        // Panel acikken (ya da bu kare kapandiysa) yon tuslari / sol cubuk ve Ⓐ paneli surer, oyuncuyu degil.
        Player.UiCapture = Hud.MenuOpen || Hud.CraftTyping || _menuClosedThisFrame;
        if (!PlanInteract()) // Insa Plani: E basili yukselt / onar / sok carki (kisa basis olagan E)
            if (Input.IsActionJustPressed("interact") && !Hud.MenuOpen && !_menuClosedThisFrame) Interact();
        _menuClosedThisFrame = false;
    }

    bool ZoomModifierHeld()
    {
        if (Controls == null) return false;
        foreach (var k in Controls.Data.Look.ZoomModifiers)
        {
            var key = OS.FindKeycodeFromString(k);
            if (key != Key.None && Input.IsKeyPressed(key)) return true;
        }
        return false;
    }

    void OpenPieceWheel()
    {
        var pieces = Capa.PlanPieces;
        if (pieces.Count == 0 || Build == null) return;
        var slices = pieces.Select(id => new Hud.WheelSlice("insa", id, Build.NameOf(id), $"{Build.CostOf(id)} Odun", State.Backpack.Count("wood") >= Build.CostOf(id))).ToList();
        Hud.OpenWheel(Hud.WheelKind.Pieces, slices, Mathf.Max(0, pieces.IndexOf(Capa.PlanPiece)));
        Player.LookLocked = true;
    }

    // ---------- Insa Plani: E basili (GDD 7 "Yerinde yukseltme") ----------

    Node3D _planTarget;
    float _planHeld;
    bool _planHolding;

    /// <summary>Insa Plani eldeyken E: hedef parca / yerlestirilebilirse basili tutma carki acar. E'yi isledi ise true.</summary>
    bool PlanInteract()
    {
        if (!_planHolding && (!Capa.PlanInHand || Build == null || Hud.MenuOpen || _menuClosedThisFrame || !Input.IsActionJustPressed("interact"))) return false;
        if (!_planHolding)
        {
            _planTarget = Build.TargetAt(Player, Player.AimPoint(out _, 60f));
            if (_planTarget == null) return false; // hedef yok: olagan E
            _planHolding = true; _planHeld = 0f;
            return true;
        }
        if (!IsInstanceValid(_planTarget)) { _planHolding = false; if (Hud.WheelOpen) Hud.CancelWheel(); return true; }
        if (Input.IsActionPressed("interact"))
        {
            _planHeld += (float)GetProcessDeltaTime();
            if (_planHeld >= Data.Tool.PlanHoldSeconds && !Hud.WheelOpen)
            {
                var acts = Build.PlanActions(_planTarget);
                var slices = acts.Select((a, i) => new Hud.WheelSlice(i == 2 ? "kazma" : "insa", null, a.title, a.text, a.ok)).ToList();
                Hud.OpenWheel(Hud.WheelKind.Plan, slices, acts[1].ok ? 1 : 2);
                Player.LookLocked = true;
            }
            if (Hud.WheelOpen && Hud.OpenWheelKind == Hud.WheelKind.Plan) Build.MarkPlanTarget(_planTarget, Hud.WheelSelection == 2);
            return true;
        }
        // Birakildi
        _planHolding = false;
        if (Hud.WheelOpen && Hud.OpenWheelKind == Hud.WheelKind.Plan)
        {
            int sel = Hud.CloseWheel(); Player.LookLocked = false;
            if (sel >= 0) Build.RunPlanAction(_planTarget, sel);
            else Build.MarkPlanTarget(null, false);
        }
        else if (_planHeld < Data.Tool.PlanHoldSeconds) Interact(); // kisa basis: olagan E (kapi ac / kapat, istasyon)
        return true;
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

    /// <summary>
    /// E'nin istasyonu: erisimdeki istasyonlardan nisangahin baktigi (kamera yonune en dar aciyla; tool.json
    /// stationFocusDegrees icinde), yoksa en yakini. Yan yana iki istasyonda (Tezgah ile Tas Firin 3,5 m) oyuncu baktigini acar.
    /// </summary>
    Station StationInFocus()
    {
        var p = Player.GlobalPosition;
        var near = Stations.Where(s => IsInstanceValid(s) && s.InReach(p)).ToList();
        if (near.Count <= 1) return near.FirstOrDefault();
        var cam = Player.Cam.GlobalPosition;
        var fwd = -Player.Cam.GlobalBasis.Z;
        float limit = Mathf.DegToRad(Data.Tool.StationFocusDegrees);
        var looked = near.Select(s => (s, a: fwd.AngleTo(s.GlobalPosition + Vector3.Up * 0.8f - cam)))
            .Where(x => x.a <= limit).OrderBy(x => x.a).Select(x => x.s).FirstOrDefault();
        return looked ?? near.OrderBy(s => s.GlobalPosition.DistanceTo(p)).First();
    }

    public void Interact()
    {
<<<<<<< HEAD
        if (ExploreInteract()) return; // Director.Explore.cs: Seyir Kulesi, mancinik, yuva (dunya2)
=======
        if (InteractVehicle()) return; // Director.Vehicles.cs: Cekirge ATV'ye bin / in (yaratik2)
>>>>>>> refs/heads/main
        if (InteractEconomy()) return; // Director.Economy.cs: El Arabasi, Sandik, Uyku Tulumu
        if (InteractPower()) return; // power/Director.Power.cs: guc parcasi ayari, Dugme
        var st = StationInFocus();
        // Gidak istasyondan yakinsa E Gidak'a gider (istasyonun dibinde dolasan Gidak beslenebilsin).
        bool gidakFirst = st != null && Gidak.Visible && !Gidak.Tame
            && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < st.GlobalPosition.DistanceTo(Player.GlobalPosition);
        if (st != null && !gidakFirst) { OpenStationMenu(st); return; }
        if (Pod.IsRamp && new Vector2(Player.GlobalPosition.X - Pod.PadCenter.X, Player.GlobalPosition.Z - Pod.PadCenter.Z).Length() < 4.5f) { OpenSellMenu(); return; }
        if (PenInteract()) return; // Director.Pen.cs: Gidak'a bugday, kapi, Yemlik, Folluk, yerdeki yumurta
        if (Gidak.Visible && !Gidak.Swollen && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < 3f && State.Backpack.Remove("wheat", 1))
            Gidak.Feed();
    }
}
