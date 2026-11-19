using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kabartma Odasi (T6, 800 W; GDD 14.10, ICERIK 11): gunduz tek bir olgun tarla karesini ya da evcil hayvani 50 Maya'ya kabartir.
/// Kural Sim'de (SwellRoom); burada yalniz menu, hedef secimi ve gorunum. Hayvan yalniz gorunum olarak kabarir (savas hayvani
/// kurali savas-hayvan akisinda); sure sonunda soner.
/// </summary>
public partial class Director
{
    /// <summary>Kabartma Odasi'nin E menusu: yaricaptaki en yakin olgun kare ve en yakin evcil hayvan.</summary>
    public void OpenSwellRoomMenu(Station st)
    {
        var def = Data.Tuzuk.SwellRoom;
        bool powered = st.Queue.PowerOk == null || st.Queue.PowerOk();
        var at = st.GlobalPosition;
        var plot = SwellRoom.NearestMature(State, at.X, at.Z);
        var animal = Ranch?.NearestSwellable(at, def.Radius);
        int maya = State.Backpack.Count(MayaVault.Item);
        var items = new List<Hud.MenuItem>
        {
            new(Loc.T("dir.swell_plot", def.MayaCost), () => powered && plot != null && maya >= def.MayaCost, () => UseSwellRoomPlot(st, plot), MayaVault.Item),
            new(Loc.T("dir.swell_animal", def.MayaCost, (def.AnimalSeconds / 60f).ToString("0", System.Globalization.CultureInfo.InvariantCulture)), () => powered && animal != null && maya >= def.MayaCost, () => UseSwellRoomAnimal(st, animal), MayaVault.Item),
        };
        string foot = !powered ? Loc.T("dir.swell_nopower_dot")
            : NightActive ? Loc.T("dir.swell_kabarma_dot")
            : Loc.T("dir.swell_status", maya, plot != null ? Loc.T("dir.swell_plot_yes") : Loc.T("dir.swell_plot_no"), animal != null ? Loc.T("dir.swell_animal_yes", animal.Species) : Loc.T("dir.swell_animal_no"));
        Hud.ShowMenu(st.Def.Name, items, foot + "  ·  " + Loc.T("dir.menu_close"));
    }

    void UseSwellRoomPlot(Station st, Plot target)
    {
        // ortak oyun: istemcide istek host'a gider (NetSync.SwellRoom.cs doğrular, aktörün çantasından öder)
        if (Net?.Forward("kabartma", w => { w.U8(0); w.F32(target.X + 0.5f); w.F32(target.Y + 0.5f); }) == true) { Hud.HideMenu(); return; }
        bool powered = st.Queue.PowerOk == null || st.Queue.PowerOk();
        var r = SwellRoom.UsePlot(State, State.Backpack, powered, target);
        Hud.HideMenu();
        ReportSwellRoom(r, () =>
        {
            GD.Print($"SWELLROOM plot {target.X},{target.Y} maya={State.Backpack.Count(MayaVault.Item)}");
            Fx.Burst(new Vector3(target.X + 0.5f, World.HeightAt(target.X + 0.5f, target.Y + 0.5f) + 0.5f, target.Y + 0.5f), Palette.MayaViolet, 24, 3.5f, 0.07f, 0.8f, true, -1f);
            Sfx.Play("levelup", st.GlobalPosition, -2f);
        });
    }

    void UseSwellRoomAnimal(Station st, PenAnimal animal)
    {
        if (animal == null) return;
        if (Net?.Forward("kabartma", w => { w.U8(1); w.VarU((uint)animal.Id); }) == true) { Hud.HideMenu(); return; }
        bool powered = st.Queue.PowerOk == null || st.Queue.PowerOk();
        var def = Data.Tuzuk.SwellRoom;
        var r = SwellRoom.UseAnimal(State, State.Backpack, powered, true);
        Hud.HideMenu();
        if (r == SwellRoomResult.Ok && !Ranch.SwellAnimal(animal, def.AnimalSeconds))
        {
            State.Backpack.Add(MayaVault.Item, def.MayaCost); // hayvan artik uygun degil: bedel iade
            r = SwellRoomResult.NoTarget;
        }
        ReportSwellRoom(r, () =>
        {
            GD.Print($"SWELLROOM animal {animal.Species} id={animal.Id} maya={State.Backpack.Count(MayaVault.Item)}");
            Sfx.Play("levelup", st.GlobalPosition, -2f);
        });
    }

    void ReportSwellRoom(SwellRoomResult r, System.Action ok)
    {
        switch (r)
        {
            case SwellRoomResult.Ok: ok(); Hud.Hint(Loc.T("dir.hint_swelled"), 1.5f); break;
            case SwellRoomResult.NoPower: Hud.Hint(Loc.T("dir.swell_nopower"), 2f); break;
            case SwellRoomResult.KabarmaActive: Hud.Hint(Loc.T("dir.swell_kabarma"), 2f); break;
            case SwellRoomResult.NoMaya: Hud.Hint(Loc.T("dir.hint_need_maya", Data.Tuzuk.SwellRoom.MayaCost), 2f); break;
            default: Hud.Hint(Loc.T("dir.hint_no_target"), 2f); break;
        }
    }
}
