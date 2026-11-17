using System.Diagnostics;
using System.Globalization;
using System.Text;
using Kabarma.Sim;
using Xunit;
using Xunit.Abstractions;

namespace Kabarma.Sim.Tests;

/// <summary>
/// TEKNIK §1.2 adim 2: "1.000 oyun gunu 10 saniyenin altinda simule ediliyor". Godot'suz, yalnizca Kabarma.Sim.
/// Bir oyun gunu GDD §2: 30 dk (22 dk gunduz, 8 dk gece). TEKNIK §3: Sim saniyede 20 tik; yavas sistemler (tarla,
/// istasyon) saniyede 2 alt adim. Bugun Sim'de olan her sistem isler; takvim/gun dongusu Sim'de yok (gecehud), gun
/// burada dongunun kendisidir. Her gece tam bir Kucuk Kabarma (en kotu durum: oyunda her gece Kabarma yok).
/// Kategori "Bench": tools/test.ps1 -Only bench Release derlemesinde kosar; olagan "sim" adimi bunu atlar.
/// Sonuc KABARMA_BENCH_OUT ortam degiskenindeki dosyaya da yazilir.
/// </summary>
[Trait("Category", "Bench")]
public class SimBenchmarkTests
{
    readonly ITestOutputHelper _out;
    public SimBenchmarkTests(ITestOutputHelper output) { _out = output; }

    const int Days = 1000;
    const float DaySeconds = 22 * 60, NightSeconds = 8 * 60;
    const int FastHz = 20, SlowHz = 2;

    public sealed record Result(int Days, int Plots, double Seconds, long AllocatedBytes, long RetainedBytes, long PeakWorkingSet,
        int Gen0, int Gen1, int Gen2, long FastTicks, long SlowTicks, int Nights, long Kills, long Harvests, long Produced, int Credits);

    /// <summary>Bir Sim calismasi: tarla (side x side), istasyonlar, yemek, gece Kabarmasi, satis.</summary>
    public static Result Run(int days, int side)
    {
        var data = TestData.Load();
        var s = new GameState(data, 4417);
        var zip = data.Creature(data.Kabarma.FirstNight.Enemy);
        var food = data.Tool.Foods[0];
        var tez = s.Stations["tezgah"];
        var firin = s.Stations["tas_firin"];
        var pak = s.Stations["paketleme"];
        long produced = 0, harvests = 0, kills = 0, fast = 0, slow = 0;
        int nights = 0;
        foreach (var q in s.Stations.Values) q.Produced += (_, n) => produced += n;
        for (int x = 0; x < side; x++) for (int y = 0; y < side; y++) s.Farm.Till(x, y);

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long alloc0 = GC.GetTotalAllocatedBytes(true);
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        var sw = Stopwatch.StartNew();

        float fdt = 1f / FastHz, sdt = 1f / SlowHz;
        int slowEvery = FastHz / SlowHz;
        for (int day = 0; day < days; day++)
        {
            // Sabah: olgunlari bic, bos kareleri ek, sula; istasyonlara is ver.
            foreach (var p in s.Farm.Plots.ToList())
            {
                if (p.Planted && p.Stage == CropStage.Mature)
                {
                    var h = s.Farm.Harvest(p.X, p.Y, s.Rng);
                    if (h != null) { s.Backpack.Add("turnip", h.Value.yield); s.Backpack.Add("turnip_seed", h.Value.seeds); harvests++; }
                }
                if (!p.Planted) { s.Backpack.Add("turnip_seed", 1); s.Farm.Plant(p.X, p.Y, "turnip", s.Backpack); }
                s.Farm.Water(p.X, p.Y);
            }
            s.Backpack.Add("stone", 4);
            tez.Enqueue(tez.Recipes.First(), s.Backpack);
            firin.Enqueue(firin.Recipes.First(), s.Backpack);
            pak.Enqueue(pak.Recipes.First(), s.Backpack);
            if (s.Backpack.Count(food.Id) > 0 && s.Food.Eat(food)) s.Backpack.Remove(food.Id, 1);

            // Gunduz: 20 Hz hizli tik (yemek), 2 Hz yavas tik (tarla, istasyonlar). Ogleyin bir kez daha sulanir.
            int dayTicks = (int)(DaySeconds * FastHz);
            for (int t = 0; t < dayTicks; t++)
            {
                s.Food.Tick(fdt);
                fast++;
                if (t % slowEvery == 0)
                {
                    s.Farm.Tick(sdt, true);
                    foreach (var q in s.Stations.Values) q.Tick(sdt, s.Backpack);
                    slow++;
                }
                if (t == dayTicks / 2) foreach (var p in s.Farm.Plots) s.Farm.Water(p.X, p.Y);
            }

            // Aksam (cift gunler): olgunlar gece kabarmadan bicilir; tek gunler kabarir ve safakta satilir.
            if (day % 2 == 0)
                foreach (var p in s.Farm.Plots.ToList())
                {
                    var h = s.Farm.Harvest(p.X, p.Y, s.Rng);
                    if (h != null) { s.Backpack.Add("turnip", h.Value.yield); s.Backpack.Add("turnip_seed", h.Value.seeds); harvests++; }
                }

            // Gece: tam bir Kucuk Kabarma (alarm, kabarma, dalgalar, Oz, kartlar), sonra karanlik.
            var n = s.StartNight();
            nights++;
            n.SwellNow += () => s.Farm.SwellMature(n.Ks, 0);
            int nightTicks = (int)(NightSeconds * FastHz);
            for (int t = 0; t < nightTicks; t++)
            {
                s.Food.Tick(fdt);
                if (n.Phase != NightPhase.Dawn)
                {
                    n.Tick(fdt);
                    int due = n.SpawnDue();
                    for (int i = 0; i < due; i++) { n.OnKill(zip); kills++; }
                    if (n.Offer != null) n.Choose(0);
                    if (due == 0) n.WaveCleared();
                }
                fast++;
                if (t % slowEvery == 0)
                {
                    s.Farm.Tick(sdt, false);
                    foreach (var q in s.Stations.Values) q.Tick(sdt, s.Backpack);
                    slow++;
                }
            }
            // Safak: kabarik urunu sok, sat; fazlayi rampadan sat (canta dolmasin).
            foreach (var p in s.Farm.Plots.Where(p => p.Swollen != null && p.Planted).ToList())
            {
                var size = s.Farm.Uproot(p.X, p.Y);
                if (size != null) s.Sell("turnip", size);
            }
            s.SellItem("turnip", s.Backpack.Count("turnip"));
            s.SellItem("kargo_kasasi", s.Backpack.Count("kargo_kasasi"));
        }

        sw.Stop();
        long alloc = GC.GetTotalAllocatedBytes(true) - alloc0;
        int gg0 = GC.CollectionCount(0) - g0, gg1 = GC.CollectionCount(1) - g1, gg2 = GC.CollectionCount(2) - g2;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long retained = GC.GetTotalMemory(true);
        long peak = Process.GetCurrentProcess().PeakWorkingSet64;
        GC.KeepAlive(s);
        return new Result(days, side * side, sw.Elapsed.TotalSeconds, alloc, retained, peak, gg0, gg1, gg2, fast, slow, nights, kills, harvests, produced, s.Credits);
    }

    static string Format(Result r)
    {
        var c = CultureInfo.InvariantCulture;
        return string.Format(c, "days={0} plots={1} seconds={2:0.000} ms_per_day={3:0.000} allocated_mb={4:0.0} retained_kb={5:0} peak_ws_mb={6:0.0} gc0={7} gc1={8} gc2={9} fast_ticks={10} slow_ticks={11} nights={12} kills={13} harvests={14} produced={15} credits={16}",
            r.Days, r.Plots, r.Seconds, r.Seconds * 1000 / r.Days, r.AllocatedBytes / 1048576.0, r.RetainedBytes / 1024.0, r.PeakWorkingSet / 1048576.0,
            r.Gen0, r.Gen1, r.Gen2, r.FastTicks, r.SlowTicks, r.Nights, r.Kills, r.Harvests, r.Produced, r.Credits);
    }

    [Fact]
    public void Thousand_game_days_under_ten_seconds()
    {
#if DEBUG
        const string config = "Debug";
#else
        const string config = "Release";
#endif
        Run(5, 3); // isinma (JIT)
        var m1 = Run(Days, 3);     // M1 dilimi: 3x3 ilk tarla (PLAN 1.4)
        // Olcek kosusu istege bagli (uzun): KABARMA_BENCH_SCALE=10 → 10x10 tarla.
        int side = int.TryParse(Environment.GetEnvironmentVariable("KABARMA_BENCH_SCALE"), out var sv) ? sv : 0;
        var big = side > 3 ? Run(Days, side) : null;
        var sb = new StringBuilder();
        sb.AppendLine($"# Sim kiyaslamasi (TEKNIK §1.2 adim 2) {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"config={config} runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} os={System.Runtime.InteropServices.RuntimeInformation.OSDescription} cpus={Environment.ProcessorCount}");
        sb.AppendLine($"gun=30 dk (22 gunduz + 8 gece, GDD §2); hizli tik {FastHz} Hz (yemek, Kabarma gecesi), yavas tik {SlowHz} Hz (tarla, 3 istasyon); her gun bic-ek-sula x2, 3 tarif, 1 yemek; her gece tam Kucuk Kabarma (3 dalga, butun Zipzip'ler olur, kartlar secilir), safakta kabarik ve normal urun satisi.");
        sb.AppendLine("Sim'de olmayan (sayilmadi): takvim ve gun dongusu, hayvan ihtiyaclari, guc agi, ekonomi borsasi, yol bulma (M2+).");
        sb.AppendLine("M1 " + Format(m1));
        if (big != null) sb.AppendLine($"{side}x{side} " + Format(big));
        sb.AppendLine($"olcut: 1000 gun < 10 sn -> M1 {(m1.Seconds < 10 ? "GECTI" : "KALDI")}" + (big != null ? $", {side}x{side} {(big.Seconds < 10 ? "GECTI" : "KALDI")}" : ""));
        var text = sb.ToString();
        _out.WriteLine(text);
        var path = Environment.GetEnvironmentVariable("KABARMA_BENCH_OUT");
        if (!string.IsNullOrEmpty(path)) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); File.WriteAllText(path, text); }
        Assert.Equal(Days, m1.Nights);
        Assert.True(m1.Harvests > 0 && m1.Kills > 0 && m1.Produced > 0, "butun sistemler islemeli");
        Assert.True(m1.Seconds < 10, $"1000 gun {m1.Seconds:0.00} sn (sinir 10)");
    }
}
