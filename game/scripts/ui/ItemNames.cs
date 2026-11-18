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
        ["ocakbasi"] = "Ocakbaşı",
        ["kargo_rampasi"] = "Kargo Rampası",
        // boss akisi (M2, GDD §18): Ata odulleri ve Ata Tasi.
        ["cayir_cekirdegi"] = "Çayır Çekirdeği", ["koru_cekirdegi"] = "Koru Çekirdeği", ["ata_yunu"] = "Ata Yünü", ["dede_boynuzu"] = "Dede Boynuzu", ["ata_tasi"] = "Ata Taşı",
        // Karar 23 (kemer akisi): kemer aletleri ve uretilen yerlestirilebilirler.
        ["capa"] = "Çapa",
        ["insa_plani"] = "İnşa Planı",
        ["ocak"] = "Ocak",
        ["tezgah"] = "Tezgâh",
        ["tas_firin"] = "Taş Fırın",
        ["paketleme"] = "Paketleme Tezgâhı",
        // M2 dunya2: Koru kaynaklari ve Enkaz (ICERIK 2.1, 17), mancinik (ICERIK 10.2).
        ["hard_wood"] = "Sert Odun",
        ["resin"] = "Reçine",
        ["iron_ore"] = "Demir Cevheri",
        ["saltpeter"] = "Güherçile",
        ["scrap"] = "Hurda",
        ["mancinik"] = "Mancınık",
    };

    public static string Of(string id) => Names.TryGetValue(id, out var n) ? n : id;
}
