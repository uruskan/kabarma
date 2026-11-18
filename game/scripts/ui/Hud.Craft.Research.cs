using System.Linq;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Uretim paneli, zincir M2 eki (GDD §12 arastirma; ICERIK §11.1): "Arastirma" kategorisi arastirmayla kilitli satirlari
/// listeler; secili satirin dugmesi "ARASTIR · n VERI" olur (Arastirma Masasi yaninda, Veri yeterse yesil). Sol sutunda
/// Veri, Ocak basamagi ve ambardan uretim notu. Arastirma Masasi'nin E menusu paneli bu kategoride acar.
/// </summary>
public partial class Hud
{
    public const string ResearchCategory = "arastirma";

    /// <summary>Arastirma kategorisinin satiri: arastirmayla kilitli ve henuz arastirilmamis.</summary>
    bool ResearchRow(RecipeDef r) => _d.ResearchLocked(r);

    /// <summary>Arastirma Masasi (E): Uretim paneli Arastirma kategorisinde.</summary>
    public void OpenResearch()
    {
        OpenCraft();
        _craftCategory = ResearchCategory;
        _craftSel = 0; _craftScroll = 0;
    }

    /// <summary>Arastirma satirinin dugmesi: etiket ve basilabilir mi.</summary>
    (string label, bool can) ResearchButton(RecipeDef r)
    {
        var n = _d.Data.Research.NodeFor(r);
        if (n == null) return ("KİLİTLİ", false);
        var b = _d.State.Research.Check(n.Id, _d.NearResearchTable);
        int cost = _d.Data.Research.CostOf(n);
        return b switch
        {
            ResearchBlock.Ok => ($"ARAŞTIR · {cost} VERİ", true),
            ResearchBlock.NoVeri => ($"VERİ {_d.State.Research.Veri}/{cost}", false),
            ResearchBlock.NoTable => ("MASA GEREKİR", false),
            ResearchBlock.NeedsPrereq => ("ÖNCE ÖNCÜLÜ", false),
            _ => ("KİLİTLİ", false),
        };
    }

    /// <summary>Sol sutun: Veri, Ocak basamagi, tapudaki Sandik sayisi.</summary>
    void PaintVeri(float y)
    {
        if (y + 96 > CBottom) return;
        var rs = _d.State.Research;
        Rect(CLeftX, y, CLeftW, 92, T.Panel);
        Text($"VERİ  {rs.Veri}", CLeftX + 12, y + 8, 20, T.Ink, true, true);
        Text($"OCAK T{rs.OcakTier}  ·  TARANAN {rs.Scanned.Count}", CLeftX + 12, y + 36, 14, T.A(T.Ink, 0.8f), true, true);
        int chests = _d.ClaimChests().Count();
        Text(chests > 0 ? $"İstasyonlar {chests} Sandık'tan da alır" : "İstasyon: çanta + tapudaki Sandıklar", CLeftX + 12, y + 62, 14, T.A(T.Ink, 0.7f));
    }

    /// <summary>Istasyon kipinde: guc ve kuyrugun kalan suresi.</summary>
    void PaintStationExtras()
    {
        if (_craftStation == null) return;
        var q = _d.State.QueueFor(_craftStation);
        float y = CTop + 172;
        if (q.Watts > 0) Text($"GÜÇ {q.Watts} W  ·  {(q.PowerOk == null ? "ağ yok, güçsüz çalışır" : q.PowerOk() ? "var" : "yok")}", CLeftX, y, 14, T.A(T.Ink, 0.8f), true, true);
        if (q.Jobs.Count > 0) Text($"SIRA {q.Jobs.Count}/{q.MaxJobs} İŞ  ·  {q.SecondsLeft:0} SN", CLeftX, y + 24, 14, T.A(T.Ink, 0.8f), true, true);
    }
}
