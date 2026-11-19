using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Capa: cipler (ICERIK 6.3; GDD 4). Silahin takili cipleri atis basina CombatStats'a eklenir ve vurusta hasar/kritik/durum
/// duzeltmesi uygulanir. GECICI ARAYUZ: savas akisinin ortak hasar sozlesmesi (Combat*) main'e girince bu dosya onun
/// modifier listesine baglanir (Sim: Chip.Combat.cs). Is basliklarinin cipleri (Damar Goren, Nazik El...) kendi sistemlerince
/// ChipBook.Stat(baslik, stat) ile okunur.
/// </summary>
public partial class Capa
{
    /// <summary>Capa.Head -> cip baslik kimligi (chips.json).</summary>
    public static string ChipHeadId(Head h) => h switch
    {
        Head.Kazma => "kazma", Head.Bicme => "bicme", Head.Toprak => "toprak", Head.Sulama => "sulama", Head.Cekim => "cekim",
        Head.Tirpan => "tirpan_isini", Head.Civi => "civi_tabancasi", _ => Head4Id(h),
    };

    /// <summary>Atis basina istatistik: kart (gece) + baslik cipleri.</summary>
    CombatStats StatsFor(string head) => ChipCombat.Merge(_d.Night?.Stats, _d.State.Chips, head, _d.State.ChipRng);

    /// <summary>Silahin ates hizi carpani (cipler).</summary>
    float ChipFireRate(string head) => 1f + _d.State.Chips.Stat(head, "fire_rate");
}
