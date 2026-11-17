using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Director: aksam, Kabarma gecesi, safak ve rapor evresi (GDD 14-15, PLAN 1.4).</summary>
public partial class Director
{
    int _swollenAtDawn;
    bool _reportShown;
    float _sinceDawn;

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
            Gidak.Swell(true);
            Pofuduk.Swell(true);
            Hud.Say(Data.Scenario.NightLines["gidak"]);
            GD.Print($"NIGHT swell crops={count}");
        };
        n.WaveStarted += (i, w) =>
        {
            if (i == 0) Hud.Say(Data.Scenario.NightLines["wave"]);
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
        Capa.Select(Capa.Head.Tirpan);
        Sfx.Play("siren", null, -2f);
        Sfx.Music(true);
        Hud.Say(Data.Scenario.NightLines["alarm"]);
        Hud.Big("KABARMA", Palette.MayaViolet, 3f);
        GD.Print("NIGHT start");
        Events.RaiseNightStarted();
    }

    void BeginDawn()
    {
        Phase = Act.Dawn;
        _actT = 0;
        // Kalan dusmanlar soner ve kacar.
        foreach (var z in Zipzip.All.ToArray()) { Fx.Burst(z.Center, Palette.MayaTeal, 10, 3f, 0.1f, 0.6f, true, -1f); z.QueueFree(); }
        Zipzip.All.Clear();
        Gidak.Swell(false);
        Pofuduk.Swell(false);
        Sfx.Music(false);
        Pod.BecomeRamp();
        Hud.ShowOffer(null, Night);
        _slowMo = 1f;
        _swollenAtDawn = State.Farm.Plots.Count(p => p.Swollen != null && p.Planted);
        Hud.Big("ŞAFAK", Palette.Warning, 3f);
        GD.Print($"NIGHT dawn kills={Night.Kills} level={Night.Level} maya={Night.Maya:0} swollenLeft={_swollenAtDawn}");
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
        Night.OnKill(z.Def);
        Fx.FlyTo(z.Center, () => Player.BackpackPoint.GlobalPosition, Palette.MayaViolet, 0.1f);
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
        float r = (float)GD.RandRange(22, 28);
        var p = World.Ground(center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
        var z = new Zipzip(def, this, Night.HpMult, Night.DamageMult, swollen: true);
        AddChild(z);
        z.GlobalPosition = p;
        Fx.Burst(p + Vector3.Up * 0.5f, Palette.MayaViolet, 14, 4f, 0.12f, 0.6f, true, -2f);
    }

    /// <summary>Evreye gore kare isi: aksam kararmasi, gece dalgalari, safak ve rapor.</summary>
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
                int due = n.SpawnDue();
                for (int i = 0; i < due; i++) SpawnZipzip();
                if (due == 0 && Zipzip.All.Count(z => !z.Dead) == 0) n.WaveCleared();
                if (n.ShouldAutoPick) ChooseCard(0);
                RefreshOffer();
                break;
            case Act.Dawn:
            case Act.Report:
            case Act.Free:
                // Safak: Maya renkleri soner, pembe-altin sabah, sonra gune doner.
                float since = _sinceDawn += dt;
                World.SetNight(1f - Mathf.Clamp(since / 5f, 0, 1));
                World.SetDawn(Mathf.Clamp(since / 3f, 0, 1) * (1f - Mathf.Clamp((since - 20f) / 30f, 0, 0.6f)));
                if (Phase == Act.Report && _actT > 3.5f && !Hud.ReportOpen && !_reportShown) { _reportShown = true; Hud.ShowReport(State, Night, State.Farm.Plots.Count(p => p.Swollen != null && p.Planted)); GD.Print("REPORT shown"); }
                break;
        }
    }
}
