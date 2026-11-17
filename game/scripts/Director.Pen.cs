using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: agil ve ilk gunun kucuk baglari. PenSystem kurulumu, E'nin agil yolu (Gidak'a bugday, kapi, Yemlik,
/// Folluk, yerdeki yumurta), istasyon olmayan yerlestirilebilirler, ilk gecenin dalga yonu (GDD 24: dalgalar agilin
/// bulundugu yonden girer) ve Suna'nin replikleri (kimlikle, Events.SunaSaid).
/// </summary>
public partial class Director
{
    public PenSystem Pens { get; private set; }

    void SetupPens()
    {
        Pens = new PenSystem(this);
        AddChild(Pens);
    }

    /// <summary>E: Gidak'a bugday ya da agil nesnesi. Isledigini dondurur (Director.Interact'in agil yolu).</summary>
    bool PenInteract()
    {
        if (Gidak.Visible && !Gidak.Swollen && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < 3f && !Gidak.Tame)
        {
            if (State.Backpack.Remove(Data.Pen.Animals[0].Food, 1)) Gidak.GiveWheat();
            else Hud.Hint($"{ItemNames.Of(Data.Pen.Animals[0].Food)} yok", 1f);
            return true;
        }
        if (Pens != null && Pens.Interact(Player.GlobalPosition)) return true;
        // Evcil Gidak yakinda: eski "bugday ver, hemen yumurtla" yoluna dusmesin.
        return Gidak.Visible && Gidak.Tame && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < 3f;
    }

    /// <summary>Istasyon olmayan yerlestirilebilir (Yemlik, Folluk, Ahsap Kapi; ICERIK 10.2).</summary>
    public void PlaceProp(PlaceableDef def, Vector3 at, float yaw) => Pens?.PlaceProp(def, at, yaw);

    /// <summary>Ilk gece (GDD 24): dalga agilin bulundugu yonden girer, aci veride (scenario.json waveSpreadDegrees).</summary>
    float WaveAngle(float random)
    {
        if (Pens == null || Pens.Pens.Count == 0 || !State.Scenario.Is("night")) return random; // yalniz ilk gece (ogretici)
        var c = Pens.Pens[0].Centroid;
        var f = FarmCenter();
        float a = Mathf.Atan2(c.Z - f.Z, c.X - f.X);
        float spread = Mathf.DegToRad(Data.Scenario.WaveSpreadDegrees);
        return a + (float)GD.RandRange(-spread, spread);
    }

    /// <summary>Suna'nin repligi (kimlikle): ekrana Turkce metin, seslendirme icin Events.SunaSaid.</summary>
    public void SayLine(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var text = Data.Scenario.Text(id);
        if (string.IsNullOrEmpty(text)) { GD.PushWarning($"Suna: '{id}' repligi yok"); return; }
        Hud.Say(text);
        Events.RaiseSunaSaid(id);
    }

    /// <summary>Kapak acilinca giris replikleri (Pod.Hatch.cs cagirir).</summary>
    public void SayIntro()
    {
        foreach (var id in Data.Scenario.Intro) SayLine(id);
    }
}
