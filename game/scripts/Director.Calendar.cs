using System.Globalization;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: gun dongusu ve takvim (GDD 2; URETIM 6 M1 Takvim). Saat Sim'de (GameState.Calendar); ilk Kabarma'nin
/// safagindan sonra isler, normal gece Kabarma'sizdir, Uyku Tulumu (Events.Slept) normal geceyi atlatir.
/// Gun saatinin isigi dunya akisinindir: burada yalnizca var olan World.SetDay/SetDawn ile kaba bir yer tutucu
/// (ApplyClockLight); dunya'nin 4 gun saati ve normal gece isigi gelince bu tek metot onunla degisir.
/// </summary>
public partial class Director
{
    /// <summary>Director._Ready'den bir kez (WireScenario'dan sonra).</summary>
    void WireNightAndDay()
    {
        var cal = State.Calendar;
        cal.Morning += day =>
        {
            Hud.Big($"GÜN {day}", Palette.Warning, 2.5f);
            GD.Print($"CALENDAR morning day={day}");
        };
        cal.Dusk += day =>
        {
            Hud.Big("GECE", Palette.CoopBlue.Lerp(Palette.OffWhite, 0.4f), 2.5f);
            Hud.Hint("Normal gece: Kabarma yok. Uyku Tulumu'nda uyursan sabah olur.", 4f);
            GD.Print($"CALENDAR dusk day={day}");
        };
        Events.Slept += OnSlept;
        State.Orders.StepDone += (chain, i) =>
        {
            var st = chain.Def.Steps[i];
            Hud.Big($"DEFTER · {st.Goal}", Palette.CoopBlue.Lerp(Palette.OffWhite, 0.3f), 2.2f);
            Sfx.Play("levelup", null, -10f);
            GD.Print($"ORDER {chain.Def.Id} step={i + 1} done");
        };
        DebugHudArgs();
    }

    /// <summary>Uyku (GDD 2): normal gece atlanir; gunduz ve Kabarma gecesinde olmaz.</summary>
    void OnSlept()
    {
        var cal = State.Calendar;
        if (State.Sleep()) { GD.Print($"SLEPT night skipped -> day {cal.Day}"); return; }
        string why = !cal.Running || NightActive ? "Kabarma gecesi uyuyarak atlanmaz." : "Henüz gece değil. Uyku geceyi atlatır.";
        Hud.Hint(why, 2.5f);
        GD.Print($"SLEPT ignored running={cal.Running} night={cal.IsNight} kabarma={NightActive}");
    }

    void TickCalendar(float dt)
    {
        if (State.Calendar.Running) State.Calendar.Tick(dt);
    }

    /// <summary>Ekin isigi (GDD 8.2: karanlikta x0): takvim islerken normal gecede buyume durur.</summary>
    bool ClockDaylight => !State.Calendar.IsNight;

    /// <summary>
    /// YER TUTUCU isik: gunun ilk parcasinda safak tonu soner, son gunduz parcasinda gun batimi artar, gece gun batimi
    /// tonunda kalir. Normal gecenin gercek isigi dunya akisinin (URETIM 6 M1 karar 7).
    /// </summary>
    void ApplyClockLight()
    {
        var c = State.Calendar;
        var parts = Data.Calendar.Parts;
        World.SetNight(0f);
        if (c.IsNight || parts.Count == 0) { World.SetDawn(0f); World.SetDay(1f); return; }
        float f = c.DaylightFraction;
        int idx = parts.FindIndex(p => f < p.Until);
        if (idx < 0) idx = parts.Count - 1;
        float start = idx == 0 ? 0f : parts[idx - 1].Until, end = parts[idx].Until;
        float k = end > start ? Mathf.Clamp((f - start) / (end - start), 0, 1) : 1f;
        World.SetDawn(idx == 0 ? 1f - k : 0f);
        World.SetDay(idx == parts.Count - 1 ? k : 0f);
    }

    /// <summary>
    /// Goruntu ve sinama yardimcilari (komut satiri):
    ///   --hudopen=bag:S | defter:S   S. oyun saniyesinde envanteri ya da Defter'i acar (3 sn sonra kapar)
    ///   --clock=K                     takvim islerken saati gunun K kesrine alir (0..1)
    ///   --replay_at=S                 S. saniyede F1 dugmesinin yaptigini yapar (Kucuk Kabarma yeniden)
    /// </summary>
    void DebugHudArgs()
    {
        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        if (Main.Args.TryGetValue("hudopen", out var ho))
        {
            var p = ho.Split(':');
            int page = p[0] == "defter" ? 1 : 0;
            GetTree().CreateTimer(p.Length > 1 ? F(p[1]) : 3f).Timeout += () =>
            {
                Hud.OpenInventory(page);
                GetTree().CreateTimer(3.0).Timeout += () => Hud.HideMenu();
            };
        }
        if (Main.Args.TryGetValue("clock", out var ck))
            State.Calendar.Morning += _ => { if (State.Calendar.Seconds == 0) State.Calendar.SetTimeOfDay(F(ck)); };
        if (Main.Args.ContainsKey("gecetest")) AddChild(new GeceHudTest(this) { Name = "GeceHudTest" }); // --test=gece
        if (Main.Args.TryGetValue("replay_at", out var ra))
            GetTree().CreateTimer(F(ra)).Timeout += () => RestartKucukKabarma();
    }
}
