using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kabaran sozlesmesi, Godot tarafi (kabarma3). Sahnedeki her gece dusmani (tek varlik ya da suru) bunu tasir.
/// Savunma (kule/tuzak), savas-hayvan ve gucler dusmani bu arayuzle bulur ve vurur; aktorun sinifina bakmaz.
/// Vurus GECICI: savas akisinin hasar/durum sozlesmesi (Combat*) gelince yalniz <see cref="Kabarans.Hit"/> degisir.
/// </summary>
public interface IKabaran
{
    /// <summary>Duz durum (Sim): tur, elit, can, hedef.</summary>
    KabaranState State { get; }
    Node3D Node { get; }
    Vector3 Center { get; }
    float Radius { get; }
    bool Dead { get; }
    bool Flying { get; }
    /// <summary>Hasar uygular (zirh sonrasi). attacker: oyuncu kimligi (0 yerel), oldurme ona yazilir.</summary>
    void ApplyHit(float damage, Vector3 from, float knock, int attacker);
}

/// <summary>Director'un gece dusmani kaydi: sorgu ve tek vurus girisi.</summary>
public static class Kabarans
{
    static readonly List<IKabaran> _all = new();
    public static IReadOnlyList<IKabaran> All => _all;

    public static void Add(IKabaran k) { if (!_all.Contains(k)) _all.Add(k); }
    public static void Remove(IKabaran k) => _all.Remove(k);
    public static void Clear() => _all.Clear();

    /// <summary>Sahnedeki canli Kabaranlar (Godot dugumu gecerli, olmemis).</summary>
    public static IEnumerable<IKabaran> Live => _all.Where(k => GodotObject.IsInstanceValid(k.Node) && !k.Dead);

    /// <summary>Bir noktanin yaricapi icindeki canli Kabaranlar (yakindan uzaga).</summary>
    public static List<IKabaran> Near(Vector3 at, float radius) =>
        Live.Where(k => k.Center.DistanceTo(at) <= radius + k.Radius).OrderBy(k => k.Center.DistanceSquaredTo(at)).ToList();

    /// <summary>
    /// Tek vurus girisi (GECICI arayuz: savas sozlesmesi gelince burasi Combat'a gecer). Zirh GDD 16.1 ile duser.
    /// environment: cevreyle oldurme (tuzak, firlatma pedi, arac) Gosteri'yi artirir (GDD 14.5).
    /// </summary>
    public static void Hit(IKabaran k, float damage, Vector3 from, float knock = 0f, int attacker = 0, bool environment = false, KabarmaNight night = null)
    {
        if (k == null || k.Dead || !GodotObject.IsInstanceValid(k.Node)) return;
        k.ApplyHit(Waves.AfterArmor(damage, k.State.Armor), from, knock, attacker);
        if (k.Dead && environment) night?.ReportGosteri(GosteriEvent.EnvironmentKill);
        if (k.Dead && k.Flying) night?.ReportGosteri(GosteriEvent.AirKill);
    }
}
