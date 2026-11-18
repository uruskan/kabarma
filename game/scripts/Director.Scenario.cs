using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: ilk gunun adim tepkileri (GDD 24 sirasi; PLAN 1.4): adim sayaclari, adim baslangici, Suna'nin
/// replikleri (kimlikle, Director.Pen.cs SayLine), rapor kapanisi, hasat sayaci, sinama kisayolu.
/// Yeni adim: scenario.json'a adim, WireScenario'ya bir sayac satiri (gerekirse bir OnStepStart satiri), Bot.Xxx.cs'e bir isleyici.
/// </summary>
public partial class Director
{
    readonly HashSet<(int, int)> _wateredPlots = new();
    readonly HashSet<string> _madeThisStep = new();
    readonly HashSet<string> _placedIds = new();

    /// <summary>
    /// Oyun olaylarini (Events) senaryo adim sayaclarina baglar: adim basina bir satir.
    /// Kurgu dosyasindaki adim kimligi ile buradaki kimlik ayni olmali; yanlis adimin olayi Scenario tarafindan yok sayilir.
    /// </summary>
    void WireScenario()
    {
        var sc = State.Scenario;
        foreach (var (s, miss) in sc.Skipped) GD.Print($"SCENARIO skip {s.Id}: missing {string.Join(",", miss)}");
        // Siki kip (--strict-scenario): atlanan adim basarisizliktir (birlestirme sonrasi dogrulama).
        if (Main.Args.ContainsKey("strict-scenario") && sc.Skipped.Count > 0)
        {
            GD.Print($"RESULT fail scenario_skipped={SkippedIds} (--strict-scenario)");
            GetTree().Quit(1);
        }
        // 1-9: ilk 10 dakika
        Events.Gathered += (item, n) => { if (item == "stone") sc.Report("mine", n); };                                    // 1 mine (Tas)
        Events.Gathered += (item, _) => { if (item == "wood") sc.SetProgress("odun", State.Backpack.Count("wood")); };     // 2 odun
        Events.StationPlaced += id => { if (id == "ocak") sc.Report("ocak"); };                                            // 3 ocak
        Events.StationPlaced += id => { if (id == "tezgah") sc.Report("tezgah"); };                                        // 4 tezgah
        State.Farm.Watered += p => { if (p.Planted) { _wateredPlots.Add((p.X, p.Y)); sc.SetProgress("tarla", _wateredPlots.Count); } }; // 5 tarla
        Events.PenChanged += cells => sc.SetProgress("agil", cells);                                                       // 6 agil
        Events.AnimalPenned += species => { if (species == "gidak") sc.Report("gidak"); };                                 // 7 gidak
        Events.Dawn += () => sc.Report("night");                                                                           // 8 night (Kucuk Kabarma)
        Events.CropShipped += (_, _, _) => sc.Report("sell");                                                              // 9 sell (Kargo)
        // 10-16: ilk 10 dakikadan sonra
        Events.Harvested += _ => sc.SetProgress("hasat", State.Backpack.Count("turnip"));                                  // 10 hasat
        Events.Ate += _ => sc.Report("yemek");                                                                             // 11 yemek
        Events.Gathered += (item, _) => { if (item is "copper_ore" or "clay") sc.SetProgress("bakir_kil", OreAndClay()); }; // 12 bakir_kil
        Events.Produced += (item, _) => { if (item == "copper_ingot") sc.Report("firin"); };                               // 13 firin
        Events.Produced += (item, _) => { if (item is "civi_tabancasi" or "civi") { _madeThisStep.Add(item); sc.SetProgress("civi", _madeThisStep.Count); } }; // 14 civi
        Events.Sold += (item, _, _) => { if (item == "kargo_kasasi") sc.Report("kasa"); };                                // 15 kasa
        Events.PiecePlaced += _ => sc.SetProgress("barinak", ShelterParts());                                              // 16 barinak
        Events.StationPlaced += id => { _placedIds.Add(id); sc.SetProgress("barinak", ShelterParts()); };                  // 16 barinak
        Events.Delivered += (_, n) => { if (n > 0) sc.Report("katalog"); };                                                // 17 katalog (ekonomi2)

        // GDD 24: ilk gece ekin evresine bakmadan butun ekinleri kabartir (Kabarma ani gecenin icinde gelir).
        Events.NightStarted += () => { if (sc.Is("night") && Data.Scenario.FirstNightSwellsAll) GD.Print($"FIRSTNIGHT ripen={FirstDayRules.RipenForFirstNight(State.Farm)}"); };

        // Adim baslarken: dogru basligi tak, sahneyi hazirla.
        OnStepStart("odun", () => Capa.Select(Capa.Head.Bicme));
        OnStepStart("tarla", () => Capa.Select(Capa.Head.Toprak));
        OnStepStart("gidak", ShowGidak);
        OnStepStart("night", BeginDusk);
        OnStepStart("sell", () => Capa.Select(Capa.Head.Cekim));
        OnStepStart("hasat", () => Capa.Select(Capa.Head.Bicme));
        OnStepStart("bakir_kil", () => Capa.Select(Capa.Head.Kazma));
        OnStepStart("civi", () => _madeThisStep.Clear());
    }

    /// <summary>Yuklemede atlanan adimlar, virgulle ("-" yoksa). Bot sonuc satiri ve siki kip kullanir.</summary>
    public string SkippedIds => State.Scenario.Skipped.Count == 0 ? "-" : string.Join(",", State.Scenario.Skipped.Select(x => x.step.Id));

    int OreAndClay() => (State.Backpack.Count("copper_ore") > 0 ? 1 : 0) + (State.Backpack.Count("clay") > 0 ? 1 : 0);

    /// <summary>Barinak (GDD 24 adim 16): temel, duvar, kapi kasasi, Ahsap Kapi kasada, cati, Uyku Tulumu.</summary>
    int ShelterParts()
    {
        var pieces = Build?.System?.Pieces ?? (IReadOnlyList<BuildPiece>)System.Array.Empty<BuildPiece>();
        int n = 0;
        if (pieces.Any(p => p.Def.Kind == "foundation")) n++;
        if (pieces.Any(p => p.Def.Kind == "wall" && p.Def.Id != "kapi_kasasi")) n++;
        if (pieces.Any(p => p.Def.Id == "kapi_kasasi")) n++;
        if (Pens != null && Pens.Props.Any(p => IsInstanceValid(p) && p.Def.Id == "ahsap_kapi" && p.Kasa != null)) n++;
        if (pieces.Any(p => p.Def.Kind == "roof")) n++;
        if (_placedIds.Contains("uyku_tulumu")) n++;
        return n;
    }

    /// <summary>GDD 24 adim 7 / PLAN 1.4 6:00: "Bir Gidak geciyor." Agilin kapisinin onunden biraz uzakta belirir.</summary>
    void ShowGidak()
    {
        var at = GidakHome + new Vector3(-6, 0, 4);
        var gate = Pens?.Walls.FirstOrDefault(w => w.Gate);
        var pen = Pens?.Pens.FirstOrDefault();
        if (gate != null && pen != null)
        {
            var mid = new Vector3(gate.Mid.X, 0, gate.Mid.Z);
            var c = pen.Centroid;
            var outward = (mid - new Vector3(c.X, 0, c.Z)).Normalized();
            at = mid + outward * 7f + outward.Cross(Vector3.Up) * 2f;
        }
        Gidak.GlobalPosition = World.Ground(at);
        Gidak.Visible = true;
        GD.Print($"GIDAK appears at {Gidak.GlobalPosition}");
    }

    /// <summary>Adim <paramref name="id"/> basladiginda (adim degisiminde, Suna satirlarindan sonra) calisir.</summary>
    static void OnStepStart(string id, System.Action react) => Events.StepChanged += (_, to) => { if (to == id) react(); };

    void OnStepChanged(StepDef done, StepDef next)
    {
        SayLine(done.Done);
        // Kargo bitince Hasat Raporu acilir, Defter acilir (PLAN 1.4, 10:00); sonraki adimin repligi rapor kapaninca.
        if (done.Id == "sell") SayLine(Data.Scenario.NextOrderLine);
        else if (next?.Start != null) SayLine(next.Start);
        GD.Print($"STEP {done.Id} -> {next?.Id ?? "end"} t={Time.GetTicksMsec() / 1000f:0.0}");
        Events.RaiseStepChanged(done.Id, next?.Id ?? "end"); // OnStepStart tepkileri burada calisir
        if (done.Id == "sell") { Phase = Act.Report; _actT = 0; }
        if (next == null) GD.Print("CHAIN done");
        CallDeferred(nameof(CatchUp));
    }

    /// <summary>Yeni adim basladiginda onceden yapilmis isi say (ornegin onceden sulanan kare, kurulmus agil).</summary>
    void CatchUp()
    {
        var sc = State.Scenario;
        foreach (var p in State.Farm.Plots.Where(p => p.Planted && p.Moisture > 0)) _wateredPlots.Add((p.X, p.Y));
        sc.SetProgress("tarla", _wateredPlots.Count);
        sc.SetProgress("odun", State.Backpack.Count("wood"));
        if (Pens != null) sc.SetProgress("agil", Pens.Pens.Where(x => x.HasGate).Select(x => x.Cells(Data.Pen.CellArea)).DefaultIfEmpty(0).Max());
        if (Gidak.Penned) sc.Report("gidak");
        sc.SetProgress("bakir_kil", OreAndClay());
        sc.SetProgress("barinak", ShelterParts());
    }

    /// <summary>Sinama kisayolu: gunduz adimlarini kurallar uzerinden hizla yapar, gece basinda birakir.</summary>
    void SkipToNight()
    {
        var sc = State.Scenario;
        for (int i = 0; i < 10; i++) { State.Backpack.Add("stone", 1); sc.Report("mine"); }
        State.Backpack.Add("wood", 25); sc.SetProgress("odun", 25);
        PlaceOcak(World.Ground(new Vector3(0, 0, 0)));
        sc.Report("tezgah");
        var f = State.Farm;
        for (int x = 3; x < 6; x++) for (int y = -1; y < 2; y++) { f.Till(x, y); f.Plant(x, y, "turnip", State.Backpack); f.Water(x, y); }
        while (f.MatureCount < 9) f.Tick(1f, true);
        sc.SetProgress("agil", 2);
        sc.Report("gidak");
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
        if (cur?.Start != null) SayLine(cur.Start);
        if (cur?.Id == "hasat") Capa.Select(Capa.Head.Bicme);
    }
}
