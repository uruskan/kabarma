namespace Kabarma.Game;

/// <summary>
/// Esya kimliklerinin arayuz adlari (M6 dil): metinler game/locale/items*.loc dosyalarinda ("item.&lt;kimlik&gt;"), secili dilde gelir.
/// Tabloda olmayan kimlik kendi kimligiyle doner (sinama bunu yakalar: ItemNamesTests).
/// </summary>
public static class ItemNames
{
    public static string Of(string id) => Loc.Has("item." + id) ? Loc.T("item." + id) : id;
}