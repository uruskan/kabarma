using Godot;

namespace Kabarma.Game;

/// <summary>SANAT-SES.md §2: Altin Cayir paleti, ortak notr palet, Maya paleti.</summary>
public static class Palette
{
    // Altin Cayir
    public static readonly Color Ground = new("E2B54A");
    public static readonly Color Plant = new("7FA650");
    public static readonly Color Accent = new("F26B3A");
    public static readonly Color Shadow = new("5A4A6B");
    public static readonly Color Fog = new("F3D9A4");
    public static readonly Color SkyNoon = new("8CC8E8");
    public static readonly Color SkySunset = new("F58A5B");

    // Ortak notr
    public static readonly Color OffWhite = new("EDE6D6");
    public static readonly Color CoopBlue = new("3E78B2");
    public static readonly Color Warning = new("F2B632");
    public static readonly Color DarkGrey = new("3B3F4A");
    public static readonly Color Wood = new("B07A4A");
    public static readonly Color HoldingRed = new("C8372D");

    // Maya
    public static readonly Color MayaTeal = new("5FF0C8");
    public static readonly Color MayaViolet = new("D45BFF");
    public static readonly Color NightDeep = new("1A2238");

    // Turetilmis tonlar (palet renklerinin karisimi; yeni renk degil)
    public static readonly Color Soil = Wood.Lerp(Shadow, 0.55f);
    public static readonly Color SoilWet = Soil.Lerp(Shadow, 0.5f);
    public static readonly Color TurnipBody = OffWhite.Lerp(Shadow, 0.08f);
    public static readonly Color TurnipTop = new Color("9B5BC4").Lerp(Shadow, 0.15f);
    public static readonly Color Rock = OffWhite.Lerp(Shadow, 0.55f);
    public static readonly Color RockDark = Rock.Lerp(Shadow, 0.4f);
}
