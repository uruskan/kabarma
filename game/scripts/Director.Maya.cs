using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director, Maya Kazani'nin agi (M4; GDD 14.4): "maya_vault" bolumu host'taki depoyu herkese yazar; "maya" istegi istemci
/// Ocak'in yanindayken host'tan Maya'yi kendi cantasina ister (host menzili dogrular). Tek oyuncuda _d.Net null.
/// </summary>
public partial class Director
{
    /// <summary>PowerSystem.BindNet'ten (InitNet'ten sonra) bir kez.</summary>
    public void BindMayaNet()
    {
        var net = Net;
        if (net == null) return;
        net.Register("maya_vault", false,
            (w, peer) => { w.VarU((uint)State.MayaStock.Stock); w.VarU((uint)State.MayaStock.TotalDeposited); },
            r => { State.MayaStock.Stock = (int)r.VarU(); State.MayaStock.TotalDeposited = (int)r.VarU(); });
        net.On("maya", (actor, r) =>
        {
            if (Ocak == null || !NetRules.InRange(actor, OcakPos.X, OcakPos.Y, OcakPos.Z, 6.5f)) return;
            int got = State.MayaStock.Withdraw(actor.Bag);
            GD.Print($"MAYA net withdraw {got} left={State.MayaStock.Stock}");
            if (got > 0) net.Flush();
        });
    }
}
