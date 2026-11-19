namespace Kabarma.Sim;

// Arayuz tasarim sistemi (ui.json; SANAT-SES 8.1 "Gorsel dil: Rust tarzi arayuz", URETIM 6 M1 karari 20).
// Uzunluklar 1600 x 900 tasarim tuvalinde pikseldir; Godot katmani (UiTheme) ekran olcegiyle tam piksele yuvarlar.

public sealed class UiDesignDef
{
    public float Width { get; set; } = 1600;
    public float Height { get; set; } = 900;
}

/// <summary>Olcek: 1080p'ye dek 1, sonra yukseklikle buyur; kucuk pencerede sigacak kadar kuculur (UiScaler).</summary>
public sealed class UiScaleDef
{
    public float Min { get; set; } = 0.75f;
    public float Max { get; set; } = 4f;
    /// <summary>Olcek 1'in yuksekligi: bunun ustunde arayuz yukseklikle buyur.</summary>
    public float BaseHeight { get; set; } = 1080;
    /// <summary>Her panelin sigdigi en kucuk tuval (genislik, yukseklik): olcek bunu asmaz.</summary>
    public float FitWidth { get; set; } = 1440;
    public float FitHeight { get; set; } = 600;
    /// <summary>Ctrl + arti / eksi bir adimi; Ctrl + 0 sifirlar.</summary>
    public float Step { get; set; } = 1.1f;
    public float PreferenceMin { get; set; } = 0.6f;
    public float PreferenceMax { get; set; } = 2f;
    /// <summary>
    /// Olcek bu kesirlere yuvarlanir (20: yirmide bir); kucuk pencere oynamasi temayi bastan kurmaz. 20, 1280 x 720'nin
    /// tam sigma olcegini (0,8) tam verir.
    /// </summary>
    public float Steps { get; set; } = 20;
}

/// <summary>Yazi rampasi (tasarim pikseli). Govde Barlow, basliklar Barlow Condensed (kalin).</summary>
public sealed class UiTypeDef
{
    public float Section { get; set; } = 28;
    public float Title { get; set; } = 26;
    public float Body { get; set; } = 18;
    public float Small { get; set; } = 15;
    public float Caption { get; set; } = 13;
    public float Count { get; set; } = 16;
    public float Prompt { get; set; } = 24;
    public float Big { get; set; } = 64;
    /// <summary>Bu boydan buyuk "kalin" yazi dar kalin (Barlow Condensed Bold) olur; altinda Barlow SemiBold.</summary>
    public float CondensedFrom { get; set; } = 26;
}

/// <summary>Kemer (hotbar): alt ortada 8 yuva. Kalan yerler (Capa basligi, can, cephane) bundan hizalanir.</summary>
public sealed class UiBeltDef
{
    public float Y { get; set; } = 802;
    public float Size { get; set; } = 75;
    public float Pitch { get; set; } = 80;
    /// <summary>Secilen yuvanin esya adi kemerin ustunde bu kadar saniye kalir.</summary>
    public float NameSeconds { get; set; } = 1.6f;
    public float FlashSeconds { get; set; } = 0.25f;
}

/// <summary>Sag alt: can ve dayaniklilik cubuklari (alttan uste), cephane onlarin ustunde, toplanan esya en ustte.</summary>
public sealed class UiVitalsDef
{
    public float X { get; set; } = 1340;
    public float Bottom { get; set; } = 880;
    public float Width { get; set; } = 240;
    public float Height { get; set; } = 32;
    public float Pitch { get; set; } = 35;
    public float FillLeft { get; set; } = 30;
    public float FillWidth { get; set; } = 202;
    public float FillHeight { get; set; } = 25;
}

public sealed class UiFeedDef
{
    public float Seconds { get; set; } = 4f;
    public float FadeSeconds { get; set; } = 0.5f;
    public int Rows { get; set; } = 6;
}

/// <summary>Nisangahin yaninda etkilesim istemi (glif + fiil + nesne) ve onun altindaki kisa notlar.</summary>
public sealed class UiPromptDef
{
    public float Dx { get; set; } = 26;
    public float Dy { get; set; } = -14;
    public float HintY { get; set; } = 560;
    public float CrosshairDot { get; set; } = 4;
}

/// <summary>Ust orta: pusula serit, altinda Defter adimi; Kabarma panosu onlarin altinda.</summary>
public sealed class UiCompassDef
{
    public float Y { get; set; } = 8;
    public float Width { get; set; } = 560;
    public float Height { get; set; } = 26;
    public float StepY { get; set; } = 56;
    public float KabarmaY { get; set; } = 104;
    public float KabarmaWidth { get; set; } = 520;
}

public sealed class UiInventoryDef
{
    public float Slot { get; set; } = 75;
    public float Pitch { get; set; } = 80;
    /// <summary>Canta izgarasinin sutun secenekleri: yuva sayisini tam bolen ilki (20 -> 5 x 4, 24 -> 6 x 4).</summary>
    public List<int> BagColumns { get; set; } = new() { 6, 5 };
    /// <summary>Solda kusam yuvalari (M1'de kusam yok: bos ve kilitli).</summary>
    public int GearSlots { get; set; } = 6;
    public float Blur { get; set; } = 2.5f;
    public float Dim { get; set; } = 0.86f;
    /// <summary>Surukleme bu kadar tasarim pikseli kayinca baslar.</summary>
    public float DragStart { get; set; } = 5f;
}

/// <summary>Baslik carki (Rust'in insa carki gibi): krem halka, kizil-turuncu secim dilimi.</summary>
public sealed class UiWheelDef
{
    public float Inner { get; set; } = 200;
    public float Outer { get; set; } = 300;
    public float SelectInner { get; set; } = 197;
    public float SelectOuter { get; set; } = 318;
    public float IconRadius { get; set; } = 250;
    public float IconRadiusSelected { get; set; } = 257;
    public float IconSize { get; set; } = 29;
    public float CenterIconSize { get; set; } = 64;
    /// <summary>Fare ya da cubuk merkezden bu kesirden azsa secim degismez (takili baslik kalir).</summary>
    public float DeadZone { get; set; } = 0.35f;
    public float Blur { get; set; } = 1.6f;
    public float Dim { get; set; } = 1f;
}

/// <summary>Cark dilimi: baslik adi ve bir satir aciklama (Capa.Head sirasiyla).</summary>
public sealed class UiHeadDef
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class UiFile
{
    public UiDesignDef Design { get; set; } = new();
    public UiScaleDef Scale { get; set; } = new();
    /// <summary>Erisilebilirlik ve fotograf modu sayilari (M6 arayuz).</summary>
    public UiAccessDef Access { get; set; } = new();
    /// <summary>Ad -> sRGB onaltilik renk (RRGGBB ya da RRGGBBAA).</summary>
    public Dictionary<string, string> Palette { get; set; } = new();
    public UiTypeDef Type { get; set; } = new();
    public UiBeltDef Belt { get; set; } = new();
    public UiVitalsDef Vitals { get; set; } = new();
    public UiFeedDef Feed { get; set; } = new();
    public UiPromptDef Prompt { get; set; } = new();
    public UiCompassDef Compass { get; set; } = new();
    public UiInventoryDef Inventory { get; set; } = new();
    public UiWheelDef Wheel { get; set; } = new();
    public List<UiHeadDef> Heads { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("ui.json")]
    public UiFile Ui { get; private set; } = new();
}

/// <summary>Erisilebilirlik ayarlari ve fotograf modu (SANAT-SES §8.3, §8.2 "fotograf modu"; M6 arayuz).</summary>
public sealed class UiAccessDef
{
    /// <summary>Arayuz boyu secenekleri (UiScaler tercihi) ve adlari.</summary>
    public List<float> UiScales { get; set; } = new() { 0.8f, 1f, 1.25f, 1.5f, 2f };
    /// <summary>Altyazi boyu carpanlari (Suna kutusu).</summary>
    public List<float> SubtitleScales { get; set; } = new() { 0.85f, 1f, 1.3f, 1.7f };
    /// <summary>Renk korlugu ad listesi: ilki kapali; ardindan protanopi, deuteranopi, tritanopi (renk duzeltme matrisi).</summary>
    public List<string> ColorBlindModes { get; set; } = new() { "Kapalı", "Protanopi", "Döteranopi", "Tritanopi" };
    /// <summary>Renk duzeltme gucu (0..1).</summary>
    public float ColorBlindStrength { get; set; } = 1f;
    public float PhotoFovMin { get; set; } = 25f;
    public float PhotoFovMax { get; set; } = 100f;
    public float PhotoFovDefault { get; set; } = 60f;
    public float PhotoFovStep { get; set; } = 5f;
    /// <summary>Serbest kamera hizi (m/sn), Shift ile carpan, fare hassasiyeti (rad/piksel).</summary>
    public float PhotoSpeed { get; set; } = 6f;
    public float PhotoFastMult { get; set; } = 3f;
    public float PhotoLook { get; set; } = 0.0025f;
    /// <summary>Oyuncudan en uzak serbest kamera mesafesi (m).</summary>
    public float PhotoRange { get; set; } = 60f;
    /// <summary>Poz listesi: oyuncu klibi ya da kodla poz kimlikleri (player_anims.json).</summary>
    public List<string> PhotoPoses { get; set; } = new() { "idle", "wave", "cheer", "kneel", "sleep", "eat", "run", "jump" };
    /// <summary>Foto kaydi klasoru (user://).</summary>
    public string PhotoFolder { get; set; } = "photos";
}