using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Director: senaryo adimlarina tepkiler (adim degisimi, rapor kapanisi, hasat sayaci, sinama kisayolu).</summary>
public partial class Director
{
    void OnStepChanged(StepDef done, StepDef next)
    {
        Hud.Say(done.Done);
        if (next?.Start != null && done.Id != "sell") Hud.Say(next.Start); // rapor kapaninca okunur
        GD.Print($"STEP {done.Id} -> {next?.Id ?? "end"} t={Time.GetTicksMsec() / 1000f:0.0}");
        switch (next?.Id)
        {
            case "ocak": Capa.Select(Capa.Head.Insa); break;
            case "till": Capa.Select(Capa.Head.Toprak); break;
            case "water": Capa.Select(Capa.Head.Sulama); break;
            case "grow":
                Gidak.Visible = true;
                Gidak.GlobalPosition = World.Ground(GidakHome + new Vector3(-6, 0, 4));
                break;
            case "night": BeginDusk(); break;
            case "sell": Capa.Select(Capa.Head.Cekim); break;
        }
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
        State.Scenario.SetProgress("hasat", State.Backpack.Count("turnip"));
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
