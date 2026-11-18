using System.Linq;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Defter'in Islah notlari (tarim M2, GDD 8.3): her turun en iyi genleri, Tohum Kaydi'ndaki soy sayisi, caprazlama ve
/// Alaca sayaci, son olaylar (yeni en iyi, +2 sicrama, caprazlama, Alaca). Kaynak: State.Farm.Log ve Seeds.
/// </summary>
public partial class Hud
{
    void DefterIslah()
    {
        var f = _d.State.Farm;
        var log = f.Log;
        if (log.Best.Count == 0 && f.Seeds.Lots.Count == 0) return; // ilk hasattan once bos
        _defterPage.AddChild(DefterSection($"ISLAH NOTLARI · {log.Crosses} çaprazlama · {log.AlacaFinds} Alaca"));
        foreach (var c in _d.Data.Crops.Crops)
        {
            if (!log.Best.TryGetValue(c.Id, out var g)) continue;
            int lots = f.Seeds.Lots.Count(l => l.Crop == c.Id);
            _defterPage.AddChild(OrderRow(g.Sum >= 28, false, $"{c.Name}: en iyi soy {g} (toplam {g.Sum})", $"{f.Seeds.CountFor(c.Id)} kayıtlı · {lots} soy"));
        }
        foreach (var n in log.Notes.AsEnumerable().Reverse().Take(6))
        {
            string name = _d.Data.FindCrop(n.Crop)?.Name ?? n.Crop;
            string text = n.Kind switch
            {
                "best" => $"{name}: yeni en iyi soy {n.Genes}",
                "jump" => $"{name}: mutasyon sıçraması {n.Genes}",
                "cross" => $"{name}: çaprazlandı (Vızvız kovanı yakında)",
                "alaca" => $"{name}: Alaca {_d.Data.Crops.Genetics.AlacaOf(n.Alaca)?.Name}! Tohumu %50 Alaca verir",
                _ => $"{name}: {n.Kind}",
            };
            _defterPage.AddChild(UL(text, 15, n.Kind == "alaca" ? T.Star : T.Muted, n.Kind == "alaca"));
        }
    }
}
