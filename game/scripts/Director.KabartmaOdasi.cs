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
        var animal = NearestSwellAnimal(at, def.Radius);
        int maya = State.Backpack.Count(MayaVault.Item);
        var items = new List<Hud.MenuItem>
        {
            new($"Tarla karesi kabart ({def.MayaCost} Maya)", () => powered && plot != null && maya >= def.MayaCost, () => UseSwellRoomPlot(st, plot), MayaVault.Item),
            new($"Hayvan kabart ({def.MayaCost} Maya)", () => powered && animal != null && maya >= def.MayaCost, () => UseSwellRoomAnimal(st, animal), MayaVault.Item),
        };
        string foot = !powered ? "Güç yok: Kabartma Odası 800 W ister."
            : NightActive ? "Kabarma sürerken çalışmaz."
            : $"Çantada {maya} Maya  ·  {(plot != null ? "olgun kare var" : "yakında olgun kare yok")}  ·  {(animal != null ? "evcil hayvan var" : "yakında evcil hayvan yok")}";
        Hud.ShowMenu(st.Def.Name, items, foot + "  ·  {act:interact} / {act:ui_cancel}: kapat");
    }

    Node3D NearestSwellAnimal(Vector3 from, float radius)
    {
        Node3D best = null; float bd = float.MaxValue;
        void Consider(Node3D n)
        {
            if (n == null || !IsInstanceValid(n) || !n.Visible) return;
            float d = n.GlobalPosition.DistanceTo(from);
            if (d <= radius && d < bd) { best = n; bd = d; }
        }
        if (Wildlife != null) foreach (var c in Wildlife.Creatures) if (IsInstanceValid(c) && c.State.Tamed && !c.Dead) Consider(c);
        Consider(Gidak); Consider(Pofuduk);
        return best;
    }

    void UseSwellRoomPlot(Station st, Plot target)
    {
        if (NetClient) { Hud.Hint("Kabartma Odası ortak oyunda host'ta kullanılır", 2f); return; }
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

    void UseSwellRoomAnimal(Station st, Node3D animal)
    {
        if (NetClient) { Hud.Hint("Kabartma Odası ortak oyunda host'ta kullanılır", 2f); return; }
        bool powered = st.Queue.PowerOk == null || st.Queue.PowerOk();
        var r = SwellRoom.UseAnimal(State, State.Backpack, powered, animal != null && IsInstanceValid(animal));
        Hud.HideMenu();
        ReportSwellRoom(r, () =>
        {
            SwellAnimalVisual(animal, true);
            GD.Print($"SWELLROOM animal {animal.Name} maya={State.Backpack.Count(MayaVault.Item)}");
            GetTree().CreateTimer(Data.Tuzuk.SwellRoom.AnimalSeconds).Timeout += () => SwellAnimalVisual(animal, false);
            Fx.Burst(animal.GlobalPosition + Vector3.Up * 0.6f, Palette.MayaViolet, 24, 3.5f, 0.07f, 0.8f, true, -1f);
        });
    }

    static void SwellAnimalVisual(Node3D a, bool on)
    {
        if (a == null || !IsInstanceValid(a)) return;
        switch (a) { case WildCreature w: w.Swell(on); break; case Gidak g: g.Swell(on); break; case Pofuduk p: p.Swell(on); break; }
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
