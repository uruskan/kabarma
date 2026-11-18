using Kabarma.Sim;
using Xunit;
using Xunit.Abstractions;

namespace Kabarma.Sim.Tests;

/// <summary>URETIM M3 kabul: onerilen (GECICI) donanimla ilk denemede gecme orani %60-80, KS 1-10.</summary>
public class WaveBalanceTests
{
    static readonly GameData D = TestData.Load();
    readonly ITestOutputHelper _out;
    public WaveBalanceTests(ITestOutputHelper o) => _out = o;

    [Fact]
    public void Pass_rates_ks_1_to_10()
    {
        var b = D.Kabarma.Full.Balance;
        var rates = new List<string>();
        for (int ks = 1; ks <= 10; ks++)
        {
            float r = WaveBalance.PassRate(D, b, ks);
            rates.Add($"KS{ks}={r:0.00}");
        }
        _out.WriteLine(string.Join(" ", rates));
        System.Console.WriteLine("BALANCE " + string.Join(" ", rates));
        Assert.All(rates, s => Assert.InRange(float.Parse(s[(s.IndexOf('=') + 1)..], System.Globalization.CultureInfo.InvariantCulture), b.PassMin, b.PassMax));
    }
}