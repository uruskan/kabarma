using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Sirt cantasi: yuva ve yigin sinirlari.</summary>
public class InventoryTests
{
    [Fact]
    public void Inventory_respects_slots_and_stacks()
    {
        var inv = new Inventory(2, 50);
        Assert.Equal(100, inv.Add("stone", 130));
        Assert.Equal(0, inv.Add("wood", 1));
        Assert.True(inv.Remove("stone", 60));
        Assert.Equal(40, inv.Count("stone"));
        Assert.False(inv.Remove("stone", 41));
    }
}
