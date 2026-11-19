using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ana Maya botu (--test=anamaya, M4 kabul; URETIM §6 M4 "hizlandirilmis bot, baslangictan Ana Maya'ya kopmadan"). Ilk gunun
/// zinciri ("end") bitince oyuncunun yolundan, 3x hizlandirilmis zamanla: Sis Korusu'nun Ata Tasi'ndan Isil Sazlik, Kizil Kanyon,
/// Ayaz Yaylasi'nin Ata Taslarina, oradan Gobek'te Ana Maya'nin Ata Tasi'na yuruyerek gider (altı biyom, ~3,2 km). Her yeni
/// biyomda o kademenin kaynak dugumunu gercek Kazma yoluyla kazar (Sis Korusu: Demir; Sazlik, Kanyon, Ayaz, Gobek: kademe
/// kaynagi). Esya verilmez, isinlama yok. Kopma = ayni yerde 15 sn ilerlememe ya da zeminin altina dusme (RESULT fail).
/// GEVSETMELER (raporda): (1) zaman 3x; (2) yaratiklarla dovusulmez: can 40 altina inince yenilenir (sayac yazilir);
/// (3) Ata dovusleri, T2-T6 uretim zinciri ve Kabarma gecesi bu botta YOK (kendi testleri: boss, ata4, zincir, kabarma).
/// </summary>
public partial class Bot
{
    static readonly (string biome, string name, Vector3 at, string[] mine)[] AnaMayaRoute =
    {
        ("sis_korusu", "koru", new Vector3(-212, 0, 219), new[] { "iron_ore" }),
        ("isil_sazlik", "sazlik", new Vector3(91, 0, 1241), new[] { "turba", "kukurt", "kaucuk_sutu", "balcik", "isil_yosun" }),
        ("kizil_kanyon", "kanyon", new Vector3(-725, 0, 1008), new[] { "kum", "kuvars", "titan_ore", "katran", "tuz" }),
        ("ayaz_yaylasi", "ayaz", new Vector3(-768, 0, 391), new[] { "buz", "ayaz_kristali" }),
        ("gobek", "gobek", new Vector3(-401, 0, 523), new[] { "ham_maya", "obsidyen", "isil_cevher" }),
    };

    int _amLeg;
    bool _amMined, _amStarted;
    string _amMinedItem = "";
    float _amProgT, _amDist, _amT0;
    Vector3 _amLast;
    float _amBest = float.MaxValue;
    int _amHeals;
    readonly List<string> _amLog = new();
    int _amBagBefore;

    void RegisterAnaMaya()
    {
        if (!Main.Args.ContainsKey("anamaya")) return;
        _steps["end"] = StepAnaMaya;
    }

    void AmFail(string why)
    {
        P.MoveOverride = Vector3.Zero;
        Engine.TimeScale = 1;
        GD.Print($"RESULT fail anamaya {why} leg={_amLeg} pos={P.GlobalPosition} log={string.Join(",", _amLog)} t={_t:0.0}");
        GetTree().Quit(1);
    }

    void StepAnaMaya()
    {
        if (_d.Hud.ReportOpen) { if (_stepT > 1.5f) { CloseReport(); _stepT = 0; } P.MoveOverride = Vector3.Zero; return; }
        if (!_amStarted) { _amStarted = true; Engine.TimeScale = 3; _amLast = P.GlobalPosition; _amT0 = _t; GD.Print("BOT anamaya basladi (3x zaman)"); }
        float dt = (float)GetProcessDeltaTime();
        var p = P.GlobalPosition;
        if (p.Y < _d.World.HeightAt(p.X, p.Z) - 6f) { AmFail("zeminin altina dustu"); return; }
        if (P.Down || P.Hp < 40f) { P.Hp = 100f; _amHeals++; }
        if (_amLeg >= AnaMayaRoute.Length)
        {
            P.MoveOverride = Vector3.Zero;
            Engine.TimeScale = 1;
            GD.Print($"RESULT ok anamaya biomes={AnaMayaRoute.Length + 1} metres={_amDist:0} mined={string.Join(",", _amLog)} heals={_amHeals} stations={_d.Stations.Count} t={_t - _amT0:0.0}");
            _done = true;
            GetTree().Quit(0);
            return;
        }
        var (biome, name, target, mine) = AnaMayaRoute[_amLeg];
        target.Y = _d.World.HeightAt(target.X, target.Z);
        _amDist += new Vector2(p.X - _amLast.X, p.Z - _amLast.Z).Length(); _amLast = p;
        // Yol: ilerleme izlenir (15 sn en iyi uzakliga 2 m yaklasmadiysa kopma).
        float d = new Vector2(target.X - p.X, target.Z - p.Z).Length();
        if (d < _amBest - 2f) { _amBest = d; _amProgT = 0; } else { _amProgT += dt; if (_amProgT > 15f * 3f) { AmFail($"kopma: {name} yolunda {d:0} m kala ilerlemedi"); return; } }

        if (!_amMined)
        {
            // Biyomun icine girince (agirlik >= 0,7) o kademenin dugumunu kazar; once ona yurur.
            if (_d.World.Map.Weight(biome, p.X, p.Z) >= 0.7f)
            {
                var rock = _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && mine.Contains(r.Item) && _d.World.Map.Weight(biome, r.GlobalPosition.X, r.GlobalPosition.Z) >= 0.5f)
                    .OrderBy(r => r.GlobalPosition.DistanceTo(p)).FirstOrDefault();
                if (rock != null && rock.GlobalPosition.DistanceTo(p) < 300f)
                {
                    Head(Capa.Head.Kazma);
                    Aim(rock.Center);
                    if (_amBagBefore == 0) _amBagBefore = 1 + _d.State.Backpack.Count(rock.Item);
                    if (MoveTo(rock.GlobalPosition, 3.6f)) Fire = true;
                    if (_d.State.Backpack.Count(rock.Item) >= _amBagBefore) { _amMined = true; _amMinedItem = rock.Item; _amLog.Add($"{name}:{rock.Item}"); _amBagBefore = 0; GD.Print($"BOT anamaya {name}: {rock.Item} kazildi"); }
                    return;
                }
            }
        }
        if (MoveTo(target, 6f))
        {
            if (!_amMined && mine.Length > 0) { AmFail($"{name}: kazilacak dugum bulunamadi ({string.Join("/", mine)})"); return; }
            GD.Print($"BOT anamaya {name} Ata Tasi'na vardi ({_amDist:0} m, t={_t - _amT0:0})");
            _amLeg++; _amMined = false; _amBest = float.MaxValue; _amProgT = 0;
            // Ana Maya'nin arenasi: Ata Tasi'nda dur (son bacak).
            if (_amLeg >= AnaMayaRoute.Length) _amLog.Add("ana_maya_arena");
        }
    }
}
