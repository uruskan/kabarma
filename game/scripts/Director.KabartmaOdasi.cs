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
            new($"Tarla karesi kabart ({def.MayaCost} Maya)", () => powered && plot != null && maya >= def.MayaCost, () => UseSwellRoomPlot(st, plot), MayaVault.Item),
            new($"Hayvan kabart ({def.MayaCost} Maya, {def.AnimalSeconds / 60f:0} dk)", () => powered && animal != null && maya >= def.MayaCost, () => UseSwellRoomAnimal(st, animal), MayaVault.Item),
        };
        string foot = !powered ? "Güç yok: Kabartma Odası 800 W ister."
            : NightActive ? "Kabarma sürerken çalışmaz."
            : $"Çantada {maya} Maya  ·  {(plot != null ? "olgun kare var" : "yakında olgun kare yok")}  ·  {(animal != null ? $"hayvan: {animal.Species}" : "yakında evcil hayvan yok")}";
        Hud.ShowMenu(st.Def.Name, items, foot + "  ·  {act:interact} / {act:ui_cancel}: kapat");
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
            case SwellRoomResult.Ok: ok(); Hud.Hint("Kabarttı", 1.5f); break;
            case SwellRoomResult.NoPower: Hud.Hint("Güç yok: Kabartma Odası 800 W ister", 2f); break;
            case SwellRoomResult.KabarmaActive: Hud.Hint("Kabarma sürerken çalışmaz", 2f); break;
            case SwellRoomResult.NoMaya: Hud.Hint($"{Data.Tuzuk.SwellRoom.MayaCost} Maya gerekir", 2f); break;
            default: Hud.Hint("Yakında uygun hedef yok", 2f); break;
        }
    }
}
