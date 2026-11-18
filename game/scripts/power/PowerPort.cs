using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Guc isteyen istasyonun agdaki ucu (tuketici arayuzu; CLAUDE.md M2 "guc baglanma notu"). Watt degeri research.json
/// "watts" tablosunda (istasyon kimligi -> W); tabloda olmayan istasyon guc istemez. PowerSystem.SyncStations her
/// istasyona ucu kendisi acar ve StationQueue.PowerOk'u baglar; istasyon kodu degismez. Etiket: _d.Power?.PortOf(st)?.Status.
/// Istasyon disi bir tuketici (or. ileride bir makine dugumu) icin elle: Attach(owner, id), her kare Gate(calisiyor), cikista Detach().
/// Kablo Kablo esyasiyla istasyona elle cekilir (soket: istasyonun ustu). Oncelik ve durum: Kablo eldeyken istasyona E.
/// Durum Sim'de (GameState.Power, PowerNode); bu sinif yalniz kimligi tutar.
/// </summary>
public sealed class PowerPort
{
    readonly PowerSystem _sys;
    public int NodeId { get; }
    public float Watts { get; }

    internal PowerPort(PowerSystem sys, int nodeId, float watts) { _sys = sys; NodeId = nodeId; Watts = watts; }

    PowerNode Node => _sys.Net.Get(NodeId);

    /// <summary>Ag talebi karsiladi (son tik).</summary>
    public bool Powered => Node?.Powered ?? false;

    /// <summary>Kablosu var mi (yoksa etiket "kablo yok" der).</summary>
    public bool Connected => _sys.Net.UsedSockets(NodeId) > 0;

    /// <summary>Yetmeyen guc yuzunden kapandi (asiri yuk).</summary>
    public bool Shed => Node?.Shed ?? false;

    /// <summary>
    /// Her kare: istasyon calismak istiyor mu bildirir, calisabilir mi doner. Calismiyorken talep yok (true doner); calisirken
    /// ag besliyorsa true. Is basladigi ilk karede ag henuz hesaplanmamissa bir kare bekler.
    /// </summary>
    public bool Gate(bool working)
    {
        var n = Node;
        if (n == null) return true;
        n.Wants = working;
        return !working || n.Powered;
    }

    /// <summary>Istasyonun etiketi icin kisa durum: "Güç: 20 W ✓" / "Güç yok: kablo çek" / "Güç yetmiyor".</summary>
    public string Status => !Connected ? $"Güç yok ({Watts:0} W): Kablo çek" : Shed ? $"Güç yetmiyor ({Watts:0} W)" : Powered ? $"Güç: {Watts:0} W" : $"Güç: {Watts:0} W (beklemede)";

    /// <summary>Istasyon kalkinca: dugum ve kablolari silinir, kablolar cantaya doner.</summary>
    public void Detach() => _sys.Unregister(NodeId, refund: true);
}
