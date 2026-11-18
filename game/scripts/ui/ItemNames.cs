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
        ["sebze_corbasi"] = "Sebze Çorbası",
        ["kargo_kasasi"] = "Kargo Kasası",
        ["egg"] = "Yumurta",
        // M1 ortak kimlikleri (URETIM 6 M1 kapsami). Akislar bu kimlikleri kullanir; yeni kimlik satir eklenerek gelir.
        ["fiber"] = "Lif",
        ["clay"] = "Kil",
        ["copper_ore"] = "Bakır Cevheri",
        ["copper_ingot"] = "Bakır Külçe",
        ["civi_tabancasi"] = "Çivi Tabancası",
        ["el_arabasi"] = "El Arabası",
        ["kozde_salgam"] = "Közde Şalgam",
        ["haslanmis_yumurta"] = "Haşlanmış Yumurta",
        ["ahsap_kapi"] = "Ahşap Kapı",
        ["sandik"] = "Sandık",
        ["uyku_tulumu"] = "Uyku Tulumu",
        ["mesale"] = "Meşale",
        ["yemlik"] = "Yemlik",
        ["folluk"] = "Folluk",
        ["bakir_tel"] = "Bakır Tel", // guc akisi (ICERIK 12 M2)
        ["kablo"] = "Kablo",
        ["direk"] = "Direk",
        ["salter"] = "Şalter",
        ["ruzgar_gulu"] = "Rüzgâr Gülü",
        ["hayvan_carki"] = "Hayvan Çarkı",
        ["aku_mk1"] = "Akü Mk1",
        ["elektrik_lambasi"] = "Elektrik Lambası",
        ["dugme"] = "Düğme",
        ["basinc_plakasi"] = "Basınç Plakası",
        ["ocakbasi"] = "Ocakbaşı",
        ["kargo_rampasi"] = "Kargo Rampası",
        // Karar 23 (kemer akisi): kemer aletleri ve uretilen yerlestirilebilirler.
        ["capa"] = "Çapa",
        ["insa_plani"] = "İnşa Planı",
        ["ocak"] = "Ocak",
        ["tezgah"] = "Tezgâh",
        ["tas_firin"] = "Taş Fırın",
        ["paketleme"] = "Paketleme Tezgâhı",
    };

    public static string Of(string id) => Names.TryGetValue(id, out var n) ? n : id;
}
