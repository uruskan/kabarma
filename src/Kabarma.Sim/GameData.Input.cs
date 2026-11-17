using System.Text.Json;

namespace Kabarma.Sim;

// Girdi eslemesi: input.json (TEKNIK §11 insan giris yolu, SANAT-SES §8.3). Burada yalnizca duz veri; belirtecleri
// (tus ve dugme adlari) Godot katmani cozer (game/scripts/input/Controls.cs). Kurallar oyun motorundan bagimsiz kalir.

/// <summary>Bir eylemin baglamalari. kb: klavye-fare, pad: oyun kolu. Ilk belirtec glifte gosterilir.</summary>
public sealed class InputActionDef
{
    public string Name { get; set; } = "";
    public List<string> Kb { get; set; } = new();
    public List<string> Pad { get; set; } = new();
    /// <summary>Baglama yerine gosterilecek klavye glifi (ornegin bakis: "Fare").</summary>
    public string? KbGlyph { get; set; }
    /// <summary>Kolda bu eylemin dogrudan dugmesi yok; o eylemle yapilir (baslik tuslari → carki). Glif oradan gelir.</summary>
    public string? PadVia { get; set; }
}

/// <summary>Baglamasi olmayan, yalnizca glif tasiyan ipucu (ornegin menu satiri secmek: "1–9" ya da "✚ + Ⓐ").</summary>
public sealed class InputHintDef
{
    public string Name { get; set; } = "";
    public string Kb { get; set; } = "";
    public string Pad { get; set; } = "";
}

/// <summary>Sag cubuk ve fare bakisi (rad/sn; carka cubuk kazanci).</summary>
public sealed class InputLookDef
{
    public float PadYawSpeed { get; set; } = 3f;
    public float PadPitchSpeed { get; set; } = 2f;
    public float Curve { get; set; } = 1.5f;
    public float PitchMin { get; set; } = -1.2f;
    public float PitchMax { get; set; } = 0.75f;
    public float MouseSens { get; set; } = 0.0025f;
    public float WheelStickGain { get; set; } = 14f;
}

/// <summary>Yalnizca --test=input surucusunun ayarlari (oyuncuyu etkilemez).</summary>
public sealed class InputDriverDef
{
    public int HoldFrames { get; set; } = 3;
    public int GapFrames { get; set; } = 2;
    /// <summary>Tetik, kamera isinin hedefte tahmini iskalamasi bu mesafenin altina inince basilir (m).</summary>
    public float AimGateMeters { get; set; } = 0.25f;
    public float AimTimeoutSeconds { get; set; } = 1.5f;
    /// <summary>Fare: kare basina kapatilan aci hatasi orani.</summary>
    public float LookGain { get; set; } = 0.5f;
    /// <summary>Sag cubuk: kare basina kapatilan aci hatasi orani (bakis dt'sine bolunup cubuk degerine cevrilir).</summary>
    public float PadLookGain { get; set; } = 0.3f;
    public int WheelHoldFrames { get; set; } = 8;
    /// <summary>Gorus engeli: kamera isini hedeften bu kadar (m) once bir seye carpiyorsa...</summary>
    public float OccludeMeters { get; set; } = 2f;
    /// <summary>...ve bu kadar (oyun sn) surerse yana adim atilir...</summary>
    public float OccludeSeconds { get; set; } = 0.6f;
    /// <summary>...bu sure (oyun sn; her denemede yon degisir, sure artar).</summary>
    public float SidestepSeconds { get; set; } = 0.35f;
    public float StepTimeoutSeconds { get; set; } = 240f;
    /// <summary>Acilis denetimi: bu eylem gecici olarak probeKb / probePad'e atanir, basilir, varsayilana donulur.</summary>
    public string ProbeAction { get; set; } = "interact";
    public string ProbeKb { get; set; } = "F";
    public string ProbePad { get; set; } = "R3";
}

public sealed class InputFile
{
    public float Deadzone { get; set; } = 0.25f;
    public InputLookDef Look { get; set; } = new();
    public string KbFormat { get; set; } = "[{0}]";
    public string PadFormat { get; set; } = "{0}";
    public Dictionary<string, string> KbGlyphs { get; set; } = new();
    public Dictionary<string, string> PadGlyphs { get; set; } = new();
    public Dictionary<string, InputActionDef> Actions { get; set; } = new();
    public Dictionary<string, InputHintDef> Hints { get; set; } = new();
    public InputDriverDef Driver { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("input.json")]
    public InputFile Input { get; private set; } = new();

    /// <summary>Yalnizca girdi eslemesini okur (Main, oyun verisi yuklenmeden once InputMap'i kurar).</summary>
    public static InputFile LoadInput(Func<string, string> read) =>
        JsonSerializer.Deserialize<InputFile>(read("input.json"), Options) ?? throw new InvalidDataException("input.json okunamadi");
}
