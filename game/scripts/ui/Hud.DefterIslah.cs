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
        _defterPage.AddChild(DefterSection(Loc.T("defter.islah_title", log.Crosses, log.AlacaFinds)));
        foreach (var c in _d.Data.Crops.Crops)
        {
            if (!log.Best.TryGetValue(c.Id, out var g)) continue;
            int lots = f.Seeds.Lots.Count(l => l.Crop == c.Id);
            _defterPage.AddChild(OrderRow(g.Sum >= 28, false, Loc.T("defter.islah_best", c.Name, g, g.Sum), Loc.T("defter.islah_seeds", f.Seeds.CountFor(c.Id), lots)));
        }
        foreach (var n in log.Notes.AsEnumerable().Reverse().Take(6))
        {
            string name = _d.Data.FindCrop(n.Crop)?.Name ?? n.Crop;
            string text = n.Kind switch
            {
                "best" => Loc.T("defter.islah_note_best", name, n.Genes),
                "jump" => Loc.T("defter.islah_note_jump", name, n.Genes),
                "cross" => Loc.T("defter.islah_note_cross", name),
                "alaca" => Loc.T("defter.islah_note_alaca", name, _d.Data.Crops.Genetics.AlacaOf(n.Alaca)?.Name ?? ""),
                _ => $"{name}: {n.Kind}",
            };
            _defterPage.AddChild(UL(text, 15, n.Kind == "alaca" ? T.Star : T.Muted, n.Kind == "alaca"));
        }
    }
}
