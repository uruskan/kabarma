namespace Kabarma.Sim;

// Elde tutulan nesneler ve el soketleri (oyuncu akisi): grips.json.
// Kural: nesnenin dunya donusumu = el kemiginin son pozu x el soketi x nesne tutamaginin tersi. Nesne her durumda
// (bekleme, yurume, nisan, ragdoll, poz) eli izler; nisan almak eli surmek demektir, nesneyi tasimak degil.

/// <summary>
/// Bir kavrama cercevesi. Ayni anlam hem elde (soket, el kemiginin yerel uzayinda) hem nesnede (tutamak, nesnenin
/// ya da alt dugumunun yerel uzayinda) kullanilir: Pos yumrugun ortasi (kavranan cubugun gectigi nokta, metre);
/// Bar cubugun basparmaga dogru ekseni; Fingers parmak bogumlarinin baktigi yon (Bar'a dik olmasi gerekmez, dik
/// izdusumu alinir).
/// </summary>
public sealed class GripFrameDef
{
    public float[] Pos { get; set; } = { 0f, 0f, 0f };
    public float[] Bar { get; set; } = { 0f, 1f, 0f };
    public float[] Fingers { get; set; } = { 0f, 0f, 1f };
    /// <summary>Tutamak bir alt dugumdeyse onun adi (ornegin El Arabasi'nin "handle_r"); bossa nesnenin kokune gore.</summary>
    public string Node { get; set; } = "";
}

/// <summary>El soketi: hangi kemik, kemigin yerel uzayinda kavrama cercevesi.</summary>
public sealed class HandSocketDef
{
    public string Bone { get; set; } = "";
    public float[] Pos { get; set; } = { 0f, 0f, 0f };
    public float[] Bar { get; set; } = { 0f, 1f, 0f };
    public float[] Fingers { get; set; } = { 0f, 0f, 1f };
}

/// <summary>Nesnenin sirtta durdugu yer (uyurken, iki el baska bir seyi tutarken): kemigin yerel uzayinda.</summary>
public sealed class BackMountDef
{
    public string Bone { get; set; } = "Backpack";
    public float[] Pos { get; set; } = { 0f, 0f, 0f };
    /// <summary>Euler acilari (derece, Godot YXZ sirasi).</summary>
    public float[] RotDeg { get; set; } = { 0f, 0f, 0f };
}

/// <summary>Elde tutulan bir nesne. Right/Left: o elin tutamagi; Left yoksa nesne tek elle tutulur.</summary>
public sealed class HeldItemDef
{
    public string Model { get; set; } = "";
    /// <summary>Modelin nesne dugumundeki yeri ve donusu (nesne uzayi: namlu +Z, ust +Y).</summary>
    public float[] ModelPos { get; set; } = { 0f, 0f, 0f };
    public float[] ModelRotDeg { get; set; } = { 0f, 0f, 0f };
    /// <summary>Namlu ucu, nesne uzayinda (isinlar ve mermiler buradan cikar).</summary>
    public float[] Muzzle { get; set; } = { 0f, 0f, 0f };
    public GripFrameDef? Right { get; set; }
    public GripFrameDef? Left { get; set; }
    /// <summary>Nisanda yumruk omzun bu kadar onunde (m), nisan yonunde.</summary>
    public float AimReach { get; set; } = 0.45f;
    /// <summary>Nisanda yumruk omuz-nisan cizgisinin bu kadar altinda (m).</summary>
    public float AimDrop { get; set; } = 0.05f;
    /// <summary>Nisan disinda (bekleme, yurume) namlu govdenin onune, yataydan bu kadar asagi bakar (derece);
    /// kol animasyondan sallanir, yalniz bilek doner ("alcak hazir" tutus). Nisana gecerken namlu zaten ondedir.</summary>
    public float CarryPitchDeg { get; set; } = -35f;
    /// <summary>Alcak hazir tutusun agirligi (0: el animasyondaki yonunde kalir, 1: tam).</summary>
    public float CarryAlign { get; set; } = 1f;
    public BackMountDef? Back { get; set; }
}

public sealed class GripsFile
{
    /// <summary>"right" ve "left".</summary>
    public Dictionary<string, HandSocketDef> Hands { get; set; } = new();
    /// <summary>Kol IK'sinin acilip kapanma hizi (agirlik/sn).</summary>
    public float ArmBlend { get; set; } = 6f;
    /// <summary>Nesne kimligi -> tanim ("capa", "el_arabasi").</summary>
    public Dictionary<string, HeldItemDef> Items { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("grips.json")]
    public GripsFile Grips { get; private set; } = new();
}
