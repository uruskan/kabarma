using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, hayvan akisi (M2; --test=hayvan): zincirin sonunda (end adimindan once) ikinci bir turu evcillestirir ve
/// urununu alir. Tur Pofuduk, yemi Salgam (ICERIK 3.1).
/// 1. Salgam: cantada yoksa tarladan Bicme ile hasat.
/// 2. Pofuduk'a Salgam ver (E) ve guveni doldur.
/// 3. Izleyen Pofuduk'u ilk gunun agilina acik kapidan sok; agil alani 18 m² (Gidak 4 + Pofuduk 9).
/// 4. Ikinci Yemlik'i uret, agila kur, Salgam koy.
/// 5. Sut cikinca Pofuduk'un yanina git, E ile al.
/// Hile yok: esya verilmez, adim atlanmaz, sure verideki urun araligi (husbandry.json).
/// </summary>
public partial class Bot
{
    bool _hayvanDone;
    int _hayvanStage, _hayvanGate;
    float _hayvanLog;

    /// <summary>Kurucunun sonunda: --test=hayvan ise "end" adimindan once hayvan isi.</summary>
    void RegisterHayvan()
    {
        if (!Main.Args.ContainsKey("hayvanbot") || !_steps.TryGetValue("end", out var end)) return;
        bool quitting = false;
        _steps["end"] = () =>
        {
            if (!_hayvanDone) { StepHayvan(); return; }
            end();
            if (Done && !quitting && Main.Args.ContainsKey("savashayvan")) { quitting = true; GetParent().AddChild(new SavasHayvanTest(_d)); return; } // M3: bilesen sinamasi (Husbandry.Combat.Test.cs) kendisi cikar
            if (Done && !quitting) { quitting = true; GetTree().CreateTimer(1.0).Timeout += () => GetTree().Quit(0); } // sinama bitti: sure dolmasini bekleme
        };
    }

    HusbandryAgent HayvanPofuduk => _d.Ranch?.Agents.FirstOrDefault(a => IsInstanceValid(a) && !a.Passive && a.Species.Id == "pofuduk" && a.Animal.AgeDays >= 1);

    void StepHayvan()
    {
        float dt = (float)GetProcessDeltaTime();
        _menuT -= dt;
        var ag = HayvanPofuduk;
        if (ag == null) { P.MoveOverride = Vector3.Zero; return; }
        var a = ag.Animal;
        var bag = _d.State.Backpack;
        _hayvanLog -= dt;
        if (_hayvanLog <= 0) { _hayvanLog = 5f; GD.Print($"BOT hayvan stage={_hayvanStage} trust={a.Trust:0.00} reg={a.Registered} turnip={bag.Count("turnip")} happy={a.Happiness:0} pending={a.Pending.Sum(x => x.Count)} timer={a.Timers.GetValueOrDefault("sut"):0}"); }
        // Acik kalan panel E'yi yutar: kapat.
        // (envanter / Uretim sayfasi HavePlaceable'in; ona dokunma)
        if (_d.Hud.StationOpen && !_d.Hud.ReportOpen && _hayvanShot == 0) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return; }
        int need = (a.Tame ? 0 : Mathf.CeilToInt((1f - a.Trust) * _d.State.Ranch.Rules.Species("pofuduk").FoodToTrust - 0.001f)) + (FeederFor("turnip") == null ? 2 : 0);
        if (bag.Count("turnip") < need) { HarvestTurnip(); return; }
        // Ikinci Yemlik'in malzemesini agila girmeden topla (oyuncu da disarida toplar; agilin icinden kapiya donus bota takiliyordu).
        if (!a.Registered && bag.Count("yemlik") == 0 && FeederFor("turnip") == null)
        {
            var pen0 = _d.Pens.Pens.FirstOrDefault();
            bool spare = pen0 != null && _d.Pens.PropsIn(pen0, "yemlik").Any(y => y.Feeder.Amount == 0);
            var rec0 = _d.Data.Tool.RecipeFor("yemlik");
            if (!spare && rec0 != null)
                foreach (var (item, n) in rec0.In)
                    if (bag.Count(item) < n) { if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return; } EnsureItem(item, n); return; }
        }

        // 2. Guven
        if (!a.Tame)
        {
            _hayvanStage = 2;
            Aim(ag.Host.GlobalPosition + Vector3.Up * 0.6f);
            if (MoveTo(ag.Host.GlobalPosition, 1.6f, false) && _menuT <= 0) { Use(); _menuT = 0.7f; }
            return;
        }
        // 3. Agila sok
        var gate = PenGate;
        if (gate == null) { P.MoveOverride = Vector3.Zero; return; }
        var (outside, inside) = GatePoints(gate);
        if (!a.Registered)
        {
            _hayvanStage = 3;
            if (!gate.Open)
            {
                Aim(gate.Piece.GlobalPosition + Vector3.Up);
                if (MoveRouted(outside, 0.5f) && _menuT <= 0) { Use(); _menuT = 0.8f; }
                return;
            }
            var pen = _d.Pens.Pens.FirstOrDefault();
            var deep = pen != null ? _d.World.Ground(new Vector3(pen.Centroid.X, 0, pen.Centroid.Z)) : inside;
            if (_hayvanGate == 0) { Aim(inside + Vector3.Up); if (MoveRouted(outside, 0.5f)) _hayvanGate = 1; return; }
            if (_hayvanGate == 1) { Aim(inside + Vector3.Up); if (MoveTo(inside, 0.5f, false)) _hayvanGate = 2; return; }
            Aim(ag.Host.GlobalPosition + Vector3.Up * 0.5f);
            MoveTo(deep + (deep - inside).Normalized() * 1.2f, 0.5f, false);
            return;
        }
        // 4. Salgamli Yemlik
        var penNow = _d.Pens.PenAt(ag.Host.GlobalPosition) ?? _d.Pens.Pens.FirstOrDefault();
        if (FeederFor("turnip") == null)
        {
            _hayvanStage = 4;
            if (penNow == null) return;
            if (_hayvanLog > 4.9f)
                GD.Print($"BOT hayvan s4 p={P.GlobalPosition} pof={ag.Host.GlobalPosition} pen={penNow.Area:0} c={penNow.Centroid} pens={string.Join("/", _d.Pens.Pens.Select(x => $"{x.Area:0}@{x.Centroid.X:0},{x.Centroid.Z:0}"))} yemlik={string.Join("/", _d.Pens.Props.Where(x => IsInstanceValid(x) && x.Feeder != null).Select(x => $"{x.Feeder.Food}:{x.Feeder.Amount}@{x.GlobalPosition.X:0},{x.GlobalPosition.Z:0}"))} bag_yemlik={bag.Count("yemlik")} wood={bag.Count("wood")} gate={gate.Open} vel={P.Velocity.Length():0.0}");
            float maxX = penNow.Polygon.Max(v => v.X), minZ = penNow.Polygon.Min(v => v.Z);
            var c = new Vector3(penNow.Centroid.X, 0, penNow.Centroid.Z);
            // Agilin icinden calisir (kapi acik): disarisi baska yapilarla kapali olabilir.
            if (_d.Pens.PenAt(P.GlobalPosition) == null && !gate.Open)
            {
                Aim(gate.Piece.GlobalPosition + Vector3.Up);
                if (MoveRouted(outside, 0.5f) && _menuT <= 0) { Use(); _menuT = 0.8f; }
                return;
            }
            // Bos Yemlik (ilk gunun Yemlik'i bugdayi bitirmis olabilir) varsa ona Salgam; yoksa ikinciyi kur.
            var empty = _d.Pens.PropsIn(penNow, "yemlik").FirstOrDefault(y => y.Feeder.Amount == 0);
            if (empty == null)
            {
                var at = _d.World.Ground(new Vector3(maxX - 0.8f, 0, minZ + 1.6f));
                var stand = _d.World.Ground(at + (c - at).Normalized() * 1.6f);
                // Malzeme eksikse once kapidan cik (agactan Odun: MoveTo duvari dolasmaz).
                var rec = _d.Data.Tool.RecipeFor("yemlik");
                bool mats = bag.Count("yemlik") > 0 || rec.In.All(x => bag.Count(x.Key) >= x.Value);
                if (!mats && _d.Pens.PenAt(P.GlobalPosition) != null) { Aim(outside + Vector3.Up); MoveRouted(outside + (outside - inside).Normalized() * 2f, 0.6f); return; }
                if (HavePlaceable("yemlik")) PlacePropAt("yemlik", at, stand);
                return;
            }
            var ey = empty.GlobalPosition with { Y = 0 };
            var standY = _d.World.Ground(ey + (c - ey).Normalized() * 1.2f);
            if (_hayvanLog > 4.9f) GD.Print($"BOT hayvan fill p={P.GlobalPosition} stand={standY} yemlik={empty.GlobalPosition} pen={penNow.Area:0} inpen={_d.Pens.PenAt(P.GlobalPosition) != null} gate={gate.Open} vel={P.Velocity.Length():0.0} menu={_d.Hud.MenuOpen}");
            Aim(empty.GlobalPosition + Vector3.Up * 0.4f);
            if (MoveRouted(standY, 0.3f) && _menuT <= 0) { Use(); _menuT = 0.8f; GD.Print($"BOT hayvan fill yemlik food={_d.Pens.FoodFor(empty)}"); }
            return;
        }
        // 5. Urun: Pofuduk'un yanina (agilin icine) git, E ile al
        if ((bag.Count("sut") > 0 || bag.Count("yun") > 0) && Main.Args.ContainsKey("hayvanshots") && _hayvanShot < 3) { HayvanShots(ag); return; }
        if (bag.Count("sut") > 0 || bag.Count("yun") > 0)
        {
            _hayvanDone = true;
            P.MoveOverride = Vector3.Zero;
            GD.Print($"HAYVAN ok species=pofuduk sut={bag.Count("sut")} yun={bag.Count("yun")} happiness={a.Happiness:0} genes={a.Genes} t={_t:0.0}");
            return;
        }
        _hayvanStage = 5;
        if (a.Pending.Count == 0)
        {
            // urun bekleniyor: agilin icinde, Pofuduk'un yakininda dur
            Aim(ag.Host.GlobalPosition + Vector3.Up * 0.5f);
            if (_d.Pens.PenAt(P.GlobalPosition) == null) { if (gate.Open) MoveRouted(inside, 0.5f); else if (MoveRouted(outside, 0.5f) && _menuT <= 0) { Use(); _menuT = 0.8f; } return; }
            MoveTo(ag.Host.GlobalPosition, 2.2f, false);
            return;
        }
        if (_d.Pens.PenAt(P.GlobalPosition) == null && !(MoveRouted(inside, 0.5f))) return;
        Aim(ag.Host.GlobalPosition + Vector3.Up * 0.5f);
        if (MoveTo(ag.Host.GlobalPosition, 1.3f, false) && _menuT <= 0) { Use(); _menuT = 0.6f; }
    }

    int _hayvanShot;
    float _hayvanShotT;

    /// <summary>--hayvanshots: kanit goruntuleri (progress/m2/hayvan/): kart, okşama sonrasi panel (agil listesi).</summary>
    void HayvanShots(HusbandryAgent ag)
    {
        _hayvanShotT += (float)GetProcessDeltaTime();
        Aim(ag.Host.GlobalPosition + Vector3.Up * 0.5f);
        if (_hayvanShot == 0)
        {
            _d.Ranch.Card.Pin = ag; // kart ikinci ture (Pofuduk) sabit
            if (!MoveTo(ag.Host.GlobalPosition, 2.0f, false) || _hayvanShotT < 1.5f) return;
            Shot("kart"); _d.Ranch.Card.Pin = null; _hayvanShot = 1; _hayvanShotT = 0; return;
        }
        if (_hayvanShot == 1)
        {
            if (_d.Hud.MenuOpen) { if (_hayvanShotT > 1.2f) { Shot("panel"); _hayvanShot = 2; _hayvanShotT = 0; } return; }
            if (MoveTo(ag.Host.GlobalPosition, 1.3f, false) && _menuT <= 0) { Use(); _menuT = 0.5f; _hayvanShotT = 0; }
            return;
        }
        if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return; }
        _hayvanShot = 3;
    }

    void Shot(string name)
    {
        var dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/m2/hayvan";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng($"{dir}/{name}.png");
        GD.Print($"SCREENSHOT {dir}/{name}.png");
    }

    PenProp FeederFor(string food) => _d.Pens?.Props.FirstOrDefault(p => IsInstanceValid(p) && p.Feeder != null && p.Feeder.Food == food && p.Feeder.Amount > 0);

    /// <summary>Salgam: tarladaki olgun bitkiyi Bicme ile hasat (StepHasat'in yolu).</summary>
    void HarvestTurnip()
    {
        _hayvanStage = 1;
        Head(Capa.Head.Bicme);
        var v = _d.FarmView.Views.Where(x => x.Plot.Planted && x.Plot.Stage == CropStage.Mature)
            .OrderBy(x => x.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (v == null) { if (_hayvanLog > 4.9f) GD.Print("BOT hayvan: olgun salgam yok"); P.MoveOverride = Vector3.Zero; return; }
        Aim(v.GlobalPosition + Vector3.Up * 0.3f);
        if (MoveTo(v.GlobalPosition, 3.2f, false)) Fire = true;
    }
}
