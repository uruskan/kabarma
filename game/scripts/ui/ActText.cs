using System.Text.RegularExpressions;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Ekrandaki adim satirindaki {act:eylem} yer tutuculari (GDD 24, SANAT-SES 10.3: tus ekranda glifle, Suna
/// soylemez). Gercek glifi girdi akisi cizer; bu dosya duz yedek metni verir: eylemin su anki ilk tusu ([E], [1],
/// [Sol tik]). Tus degisirse metin de degisir.
/// </summary>
public static class ActText
{
    static readonly Regex Token = new(@"\{act:([a-z0-9_]+)\}", RegexOptions.Compiled);
    static readonly bool Headless = DisplayServer.GetName() == "headless";

    /// <summary>Metindeki butun {act:eylem} yer tutucularini duz tus adina cevirir.</summary>
    public static string Plain(string text) => string.IsNullOrEmpty(text) ? text : Token.Replace(text, m => "[" + Label(m.Groups[1].Value) + "]");

    /// <summary>Eylemin ilk klavye ya da fare baglantisinin adi; yoksa eylem adi.</summary>
    public static string Label(string action)
    {
        if (!InputMap.HasAction(action)) return action;
        foreach (var ev in InputMap.ActionGetEvents(action))
        {
            if (ev is InputEventKey k)
            {
                // Basliksiz kosuda klavye duzeni yok (KeyboardGetKeycodeFromPhysical her cagrida hata yazar): fiziksel tus adi.
                var code = k.PhysicalKeycode == Key.None ? k.Keycode
                    : Headless ? k.PhysicalKeycode : DisplayServer.KeyboardGetKeycodeFromPhysical(k.PhysicalKeycode);
                var s = OS.GetKeycodeString(code);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            if (ev is InputEventMouseButton mb)
                return mb.ButtonIndex switch
                {
                    MouseButton.Left => "Sol tık",
                    MouseButton.Right => "Sağ tık",
                    MouseButton.WheelUp or MouseButton.WheelDown => "Tekerlek",
                    _ => mb.ButtonIndex.ToString(),
                };
        }
        return action;
    }
}
