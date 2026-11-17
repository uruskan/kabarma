using System.Collections.Generic;

namespace Kabarma.Game;

/// <summary>Esya kimliklerinin Turkce adlari (arayuz). Veri dosyasinda adi olanlar oradan, kalanlar burada.</summary>
public static class ItemNames
{
    static readonly Dictionary<string, string> Names = new()
    {
        ["stone"] = "Taş",
        ["wood"] = "Odun",
        ["turnip"] = "Şalgam",
        ["turnip_seed"] = "Şalgam Tohumu",
        ["wheat"] = "Buğday",
        ["civi"] = "Çivi",
        ["salgam_corbasi"] = "Şalgam Çorbası",
        ["salgam_kasasi"] = "Şalgam Kasası",
        ["egg"] = "Yumurta",
    };

    public static string Of(string id) => Names.TryGetValue(id, out var n) ? n : id;
}
