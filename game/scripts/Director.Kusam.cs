using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// kusam (M4): zirh takimi, kusam modulleri (ICERIK 16). Canta icerigi giyilendir (ArmorRules): en yuksek zirhli takim, o takimin
/// yuvasi kadar modul. Uygulananlar: zirh puani (Player.Hurt), Kalkan Jeneratoru (20'lik kalkan, 6 sn hasar almazsa dolar).
/// Henuz baglanmayan modul etkileri: filtre/serin/termal/maya direnci (iklim akisi), planor, cift ziplama, parasut, amortisor, yuzgec,
/// genis canta, kemer uzatmasi, aku paketi, hizli sarj, miknatis, far, hayvan dudugu, tamir kiti. Capa Mk (cip yuvasi) cip akisinda
/// Ocak basamagi; Mk esyalari ve tarifleri hazir (ArmorRules.Mk).
/// </summary>
public partial class Director
{
    float _armorT;

    void ApplyArmor(float dt)
    {
        var a = Data.Armor;
        Player.ShieldIdle += dt;
        _armorT -= dt;
        if (_armorT <= 0f)
        {
            _armorT = 0.5f;
            int Count(string id) => State.Backpack.Count(id);
            Player.ArmorPoints = ArmorRules.ArmorPoints(a, Count);
            Player.ShieldMax = ArmorRules.Effect(a, Count, "shield");
            if (Player.Shield > Player.ShieldMax) Player.Shield = Player.ShieldMax;
        }
        if (Player.Shield < Player.ShieldMax && Player.ShieldIdle > 6f) Player.Shield = Mathf.Min(Player.ShieldMax, Player.Shield + 10f * dt);
    }
}