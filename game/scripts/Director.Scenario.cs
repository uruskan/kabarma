using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: senaryo adimlarina tepkiler (adim sayaclari, adim baslangici, rapor kapanisi, hasat sayaci, sinama kisayolu).
/// Yeni adim: scenario.json'a adim, WireScenario'ya bir sayac satiri (gerekirse bir OnStepStart satiri), Bot.Xxx.cs'e bir isleyici.
/// </summary>
public partial class Director
{
    /// <summary>
    /// Oyun olaylarini (Events) senaryo adim sayaclarina baglar: adim basina bir satir.
    /// Kurgu dosyasindaki adim kimligi ile buradaki kimlik ayni olmali; yanlis adimin olayi Scenario tarafindan yok sayilir.
    /// (till, plant, water, grow sayaclari yerel durumdan sayilir: Capa.Toprak/Capa.Sulama ve _Process.)
    /// </summary>
    void WireScenario()
    {
        var sc = State.Scenario;
        Events.Gathered += (item, n) => { if (item == "stone") sc.Report("mine", n); };                                  // mine
        Events.StationPlaced += id => { if (id == "ocak") sc.Report("ocak"); };                                          // ocak
        Events.Dawn += () => sc.Report("night");                                                                         // night
        Events.CropShipped += (_, _, _) => sc.Report("sell");                                                            // sell
        Events.StationPlaced += id => { if (id == "tezgah") sc.Report("tezgah_kur"); };                                  // tezgah_kur
        Events.Produced += (item, _) => { if (item == "civi") sc.Report("civi"); };                                      // civi
        Events.Harvested += _ => sc.SetProgress("hasat", State.Backpack.Count("turnip"));                                // hasat
        Events.StationPlaced += id => { if (id == "tas_firin") sc.Report("firin_kur"); };                                // firin_kur
        Events.Ate += _ => sc.Report("corba");                                                                           // corba
        Events.Produced += (item, _) => { if (item == "kargo_kasasi") sc.Report("paketle"); };                          // paketle
        Events.Sold += (item, _, _) => { if (item == "kargo_kasasi") sc.Report("kasa_sat"); };                          // kasa_sat
        Events.Gathered += (item, _) => { if (item == "wood") sc.SetProgress("odun", State.Backpack.Count("wood")); };   // odun
        Events.PiecePlaced += _ => sc.Report("yapi");                                                                    // yapi

        // Adim baslarken: dogru basligi tak, sahneyi hazirla.
        OnStepStart("ocak", () => Capa.Select(Capa.Head.Insa));
        OnStepStart("till", () => Capa.Select(Capa.Head.Toprak));
        OnStepStart("water", () => Capa.Select(Capa.Head.Sulama));
        OnStepStart("grow", () =>
        {
            Gidak.Visible = true;
            Gidak.GlobalPosition = World.Ground(GidakHome + new Vector3(-6, 0, 4));
        });
        OnStepStart("night", BeginDusk);
        OnStepStart("sell", () => Capa.Select(Capa.Head.Cekim));
    }

    /// <summary>Adim <paramref name="id"/> basladiginda (adim degisiminde, Suna satirlarindan sonra) calisir.</summary>
    static void OnStepStart(string id, System.Action react) => Events.StepChanged += (_, to) => { if (to == id) react(); };

    void OnStepChanged(StepDef done, StepDef next)
    {
        Hud.Say(done.Done);
        if (next?.Start != null && done.Id != "sell") Hud.Say(next.Start); // rapor kapaninca okunur
        GD.Print($"STEP {done.Id} -> {next?.Id ?? "end"} t={Time.GetTicksMsec() / 1000f:0.0}");
        Events.RaiseStepChanged(done.Id, next?.Id ?? "end"); // OnStepStart tepkileri burada calisir
        if (done.Id == "sell") { Phase = Act.Report; _actT = 0; }
        if (next == null) GD.Print("CHAIN done");
        CallDeferred(nameof(CatchUp));
    }

    /// <summary>Yeni adim basladiginda onceden yapilmis isi say (ornegin surme sirasinda ekilen kare).</summary>
    void CatchUp()
    {
        var f = State.Farm;
        State.Scenario.SetProgress("till", f.Plots.Count());
        State.Scenario.SetProgress("plant", f.Plots.Count(p => p.Crop != null));
    }

    /// <summary>Sinama kisayolu: gunduz adimlarini kurallar uzerinden hizla yapar, gece basinda birakir.</summary>
    void SkipToNight()
    {
        var sc = State.Scenario;
        for (int i = 0; i < 10; i++) { State.Backpack.Add("stone", 1); sc.Report("mine"); }
        PlaceOcak(World.Ground(new Vector3(0, 0, 0)));
        var f = State.Farm;
        for (int x = 3; x < 6; x++) for (int y = -1; y < 2; y++) { f.Till(x, y); f.Plant(x, y, "turnip", State.Backpack); f.Water(x, y); }
        sc.SetProgress("till", 9); sc.SetProgress("plant", 9); sc.SetProgress("water", 9);
        while (f.MatureCount < 9) f.Tick(1f, true);
        Player.GlobalPosition = World.Ground(new Vector3(1, 0, -5)) + Vector3.Up * 0.3f;
        Player.Yaw.Rotation = new Vector3(0, Mathf.DegToRad(200), 0);
    }

    public int Harvests { get; private set; }
    public void OnHarvest(int n)
    {
        Harvests += n;
        Events.RaiseHarvested(n);
        GD.Print($"HARVEST {n} total={Harvests} turnips={State.Backpack.Count("turnip")}");
    }

    public void CloseReport()
    {
        if (!Hud.ReportOpen) return;
        Hud.HideReport();
        Phase = Act.Free;
        var cur = State.Scenario.Current;
        if (cur?.Start != null) Hud.Say(cur.Start);
        if (cur?.Id == "tezgah_kur") Capa.Select(Capa.Head.Insa);
    }
}
