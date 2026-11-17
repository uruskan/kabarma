using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: aksam, Kabarma gecesi, safak ve rapor evresi (GDD 14-15, PLAN 1.4). Ilk gece senaryonun "night" adimidir;
/// F1 panelinden yeniden oynanan Kucuk Kabarma (URETIM 6 M1 Takvim) ayni akisi kullanir, rapor safakta acilir.
/// </summary>
public partial class Director
{
    int _swollenAtDawn;
    bool _reportShown;
    float _sinceDawn;
    bool _replay;   // F1'den yeniden oynanan gece
    bool _wasDown;

    /// <summary>Safaktan sonra isigin gun saatine birakilmasina kadar gecen sure (safak efekti).</summary>
    const float DawnBlendSeconds = 6f;

    // ---------- Gece ----------

    void BeginDusk()
    {
        Phase = Act.Dusk;
        _actT = 0;
    }

    void BeginNight()
    {
        Phase = Act.Night;
        _actT = 0;
        var n = State.StartNight();
        n.SwellNow += () =>
        {
            int count = State.Farm.SwellMature(n.Ks, 0).Count;
            Hud.Say(Data.Scenario.NightLines["swell"]);
            if (Gidak.Visible) n.RecordAnimalSwell();
            Gidak.Swell(true);
            Pofuduk.Swell(true);
            Hud.Say(Data.Scenario.NightLines["gidak"]);
            GD.Print($"NIGHT swell crops={count}");
        };
        n.WaveStarted += (i, w) =>
        {
            if (i == 0) Hud.Say(Data.Scenario.NightLines["wave"]);
            if (i == 1) DebugShot($"kabarma_pano_gece{State.NightsStarted}", 2.5f);
            Hud.Big($"DALGA {i + 1}", Palette.MayaTeal, 2f);
            GD.Print($"NIGHT wave {i + 1} count={w.Count}");
        };
        n.LevelUp += lv =>
        {
            Sfx.Play("levelup", null, -2f);
            Fx.Burst(Player.GlobalPosition + Vector3.Up * 0.6f, Palette.MayaViolet, 24, 3.5f, 0.07f, 0.7f, true, -1f);
            if (lv == 1) Hud.Say(Data.Scenario.NightLines["level"]);
        };
        n.CardTaken += (c, r) => { Hud.Big($"{c.Name} {(r > 1 ? "· Rütbe " + r : "")}", Palette.MayaViolet, 1.6f); GD.Print($"NIGHT card {c.Id} rank={r}"); Events.RaiseCardPicked(c.Id); };
        n.DawnNow += BeginDawn;
        n.OzCollected += OnOzCollected; // Director.Oz.cs (M1 karari 19)
        ClearOzViews();
        _wasDown = Player.Down;
        Capa.Select(Capa.Head.Tirpan);
        Sfx.Play("siren", null, -2f);
        Sfx.Music(true);
        Hud.Say(Data.Scenario.NightLines["alarm"]);
        Hud.Big("KABARMA", Palette.MayaViolet, 3f);
        GD.Print($"NIGHT start n={State.NightsStarted}{(_replay ? " replay" : "")}");
        Events.RaiseNightStarted();
    }

    void BeginDawn()
    {
        // Ilk gece: rapor kabarik urun satilinca (PLAN 1.4 sirasi). Yeniden oynanan gece: rapor safakta (GDD 14.1).
        Phase = _replay ? Act.Report : Act.Dawn;
        _actT = 0;
        _sinceDawn = 0;
        // Kalan dusmanlar soner ve kacar.
        foreach (var z in Zipzip.All.ToArray()) { Fx.Burst(z.Center, Palette.MayaTeal, 10, 3f, 0.1f, 0.6f, true, -1f); z.QueueFree(); }
        Zipzip.All.Clear();
        Gidak.Swell(false);
        Pofuduk.Swell(false);
        ClearOzViews(); // Sim safakta yerde kalan Oz'u saydi; ucuslar zaten basladi
        Sfx.Music(false);
        Hud.ShowOffer(null, Night);
        _shownOffer = null;
        _slowMo = 1f;
        Player.SpeedMult = 1f; // GDD 15: kartlar safakta soner (Cevik dahil)
        Hud.Big("ŞAFAK", Palette.Warning, 3f);
        GD.Print($"NIGHT dawn kills={Night.Kills} level={Night.Level} maya={Night.Maya:0} cards={Night.CardsCount} statsBase={Night.Stats.IsBase}{(_replay ? " replay" : "")}");
        if (_replay) { Events.RaiseDawn(); return; }
        Pod.BecomeRamp();
        _swollenAtDawn = State.Farm.Plots.Count(p => p.Swollen != null && p.Planted);
        GD.Print($"NIGHT swollenLeft={_swollenAtDawn}");
        Events.RaiseDawn(); // senaryo: "night" adimi biter
        if (_swollenAtDawn == 0)
        {
            // Satacak kabarik urun kalmadi: satis adimi bos gecer, rapor yine gelir.
            Hud.Say("Tarlada kabarık bir şey kalmamış. Kooperatif bunu not etti. Bir dahaki sefere tarlanın başında dur.");
            State.Scenario.Report("sell");
        }
    }

    public void OnZipzipKilled(Zipzip z)
    {
        Events.RaiseCreatureKilled(z.Def.Id, z.HasSac);
        if (Night == null || Night.Phase == NightPhase.Dawn) return;
        var oz = Night.OnKill(z.Def, z.GlobalPosition.X, z.GlobalPosition.Z); // Oz yere duser (M1 karari 19)
        DropOz(oz, z.Center);
        RefreshOffer();
    }

    CardOffer _shownOffer;
    void RefreshOffer()
    {
        if (Night == null) return;
        if (Night.Offer != _shownOffer)
        {
            _shownOffer = Night.Offer;
            Hud.ShowOffer(_shownOffer, Night);
            if (_shownOffer != null) DebugShot($"kart_teklifi_gece{State.NightsStarted}", 0.5f);
        }
        // Tek oyuncuda secim sirasinda zaman %30'a yavaslar (GDD 15).
        _slowMo = _shownOffer != null ? Data.Cards.SlowMoScale : 1f;
    }

    public void ChooseCard(int i)
    {
        if (Night?.Offer == null) return;
        Night.Choose(i);
        RefreshOffer();
    }

    void SpawnZipzip()
    {
        var def = Data.Creature(Data.Kabarma.FirstNight.Enemy);
        var center = FarmCenter();
        float a = (float)GD.RandRange(0, Mathf.Tau);
        a = WaveAngle(a); // Director.Pen.cs: ilk gece dalgalar agilin yonunden girer (GDD 24)
        float r = (float)GD.RandRange(22, 28);
        var p = World.Ground(center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
        var z = new Zipzip(def, this, Night.HpMult, Night.DamageMult, swollen: true);
        AddChild(z);
        z.GlobalPosition = p;
        Fx.Burst(p + Vector3.Up * 0.5f, Palette.MayaViolet, 14, 4f, 0.12f, 0.6f, true, -2f);
    }

    /// <summary>
    /// F1 paneli (URETIM 6 M1 Takvim: his denemesi icin): Kucuk Kabarma'yi yeniden baslatir. Ilk gece ve raporu
    /// bitmeden, bir gece surerken ya da bir panel acikken calismaz. Saat gece boyunca durur.
    /// </summary>
    public bool RestartKucukKabarma()
    {
        if (Phase != Act.Free || NightActive || Hud.ReportOpen)
        {
            Hud.Hint("Küçük Kabarma şimdi başlamaz: ilk gece ve Hasat Raporu bitmeli", 2.5f);
            GD.Print($"REPLAY refused phase={Phase}");
            return false;
        }
        if (Hud.MenuOpen) Hud.HideMenu();
        _replay = true;
        _reportShown = false;
        _sinceDawn = 0;
        _shownOffer = null;
        BeginDusk();
        GD.Print("REPLAY kucuk kabarma");
        return true;
    }

    void ShowHarvestReport()
    {
        _reportShown = true;
        if (Hud.MenuOpen) Hud.HideMenu(); // rapor acik bir istasyon menusunun ya da envanterin ustune binmesin
        var r = State.BuildReport();
        bool first = !State.Orders.DefterOpen;
        State.Orders.OpenDefter(); // PLAN 1.4: ilk raporla Defter acilir
        Hud.ShowReport(r, first);
        DebugShot($"hasat_raporu_gece{r.NightNumber}", 0.8f, holdBot: true);
        GD.Print($"REPORT shown night={r.NightNumber} kills={r.Kills} maya={r.Maya:0} level={r.Level} cards={string.Join(",", r.Cards.Select(c => c.Id + ":" + c.Rank))} swollen={r.SwollenTotal} eaten={r.CropsEaten} sold={r.SoldCredits} defter={first}");
    }

    /// <summary>Evreye gore kare isi: aksam kararmasi, gece dalgalari, safak, rapor ve gun dongusu.</summary>
    void TickPhase(float dt)
    {
        switch (Phase)
        {
            case Act.Dusk:
                World.SetDay(Mathf.Clamp(_actT / 6f, 0, 1));
                if (_actT > 6f) BeginNight();
                break;
            case Act.Night:
                var n = Night;
                World.SetNight(Mathf.Clamp(_actT / Data.Kabarma.FirstNight.AlarmSeconds, 0, 1));
                n.Tick(dt);
                if (n.Phase == NightPhase.Dawn) break;
                Player.SpeedMult = n.Stats.MoveSpeed(1f); // Cevik: hiz +%8 / rutbe (ICERIK 8.1)
                n.CollectNear(Player.GlobalPosition.X, Player.GlobalPosition.Z); // Miknatis yaricapi buyutur (M1 karari 19)
                if (Player.Down && !_wasDown) n.RecordPlayerDown();
                _wasDown = Player.Down;
                int due = n.SpawnDue();
                for (int i = 0; i < due; i++) SpawnZipzip();
                if (due == 0 && Zipzip.All.Count(z => !z.Dead) == 0) n.WaveCleared();
                if (n.ShouldAutoPick) ChooseCard(0);
                RefreshOffer();
                break;
            case Act.Dawn:
            case Act.Report:
            case Act.Free:
                // Safak: Maya renkleri soner, pembe-altin sabah; sonra isik gun saatine birakilir.
                float since = _sinceDawn += dt;
                if (State.Calendar.Running && since > DawnBlendSeconds) ApplyClockLight(); // Director.Calendar.cs
                else
                {
                    World.SetNight(1f - Mathf.Clamp(since / 5f, 0, 1));
                    World.SetDawn(Mathf.Clamp(since / 3f, 0, 1) * (1f - Mathf.Clamp((since - 20f) / 30f, 0, 0.6f)));
                }
                if (Phase == Act.Report && _actT > 3.5f && !Hud.ReportOpen && !_reportShown) ShowHarvestReport();
                TickCalendar(dt); // Director.Calendar.cs
                break;
        }
    }
}
