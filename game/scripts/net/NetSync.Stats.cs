namespace Kabarma.Game;

/// <summary>Ag olcumu (TEKNIK 5.3 butcesi; --test=stress): tasimanin kablo bayti.</summary>
public partial class NetSync
{
    /// <summary>Bu surecin gonderdigi toplam bayt (ENet: protokol ek yuku dahil).</summary>
    public long BytesSent => _tx?.BytesSent ?? 0;
    public long BytesReceived => _tx?.BytesReceived ?? 0;
}
