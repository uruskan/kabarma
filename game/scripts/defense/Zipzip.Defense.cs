using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Zipzip, savunma akisinin tuzak etkileri (GDD §16.4 M3). Yeni partial: Zipzip.cs'e dokunmaz. Tutma inis sersemlemesini
/// (TickLaunch, hareketsiz) kullanir; yavaslatma FreezeT'yi (Zipzip.cs: %60 hiz); firlatma Gidak tekmesinin yayini
/// (Zipzip.Launch.cs). Savas akisinin durum sozlesmesi (Status*) gelince bunlar onun Yavas/Tutuk durumlarina baglanir.
/// </summary>
public partial class Zipzip
{
    /// <summary>Yerinden kipirdamaz (Yapiskan Zemin, Cukur).</summary>
    public void DefenseHold(float seconds) => _landStunT = Mathf.Max(_landStunT, seconds);

    /// <summary>Yavaslatir (Diken Tarlasi).</summary>
    public void DefenseSlow(float seconds) => FreezeT = Mathf.Max(FreezeT, seconds);

    /// <summary>Firlatma Pedi: dir yonunde, pedin yayiyla ucar; hasar inince.</summary>
    public void DefenseLaunch(TrapDef pad, Vector3 dir, float damage)
    {
        var kick = new CreatureDef { KickDistance = pad.Distance, KickApex = pad.Apex, KickFlightSeconds = pad.FlightSeconds, KickSpinTurns = 1.5f, KickLandStun = 0.5f };
        Launch(kick, dir, damage);
    }
}
