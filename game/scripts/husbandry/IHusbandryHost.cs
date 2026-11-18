using Godot;

namespace Kabarma.Game;

/// <summary>
/// Evcil davranisin (HusbandryAgent) yaratik aktorune dokundugu yer (isteğe bagli). Uygulayan aktor kendi yurur ve
/// kliplerini oynatir; uygulamayan aktorun konumunu bilesen kendisi yazar (aktorun _Process'inden sonra).
/// Sozlesme yaratik2 akisina notla bildirildi (side-session-notes/hayvan-sozlesme.md).
/// </summary>
public interface IHusbandryHost
{
    /// <summary>husbandry.json tur kimligi.</summary>
    string SpeciesId { get; }
    /// <summary>driven = true: vahsi gezinmeyi birak, goal'e speed ile yuru. false: kendi davranisina don.</summary>
    void HusbandrySteer(Vector3 goal, float speed, bool driven);
    /// <summary>Can orani 0-1 (yirtici yolu, GDD 9.1: %25 alti tutulur).</summary>
    float HpFraction { get; }
}
