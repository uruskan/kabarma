namespace Kabarma.Sim;

/// <summary>
/// Capa'nin kural durumu (GDD 4): hangi silah basliklari acik. Duz ve seriye dokulebilir (TEKNIK 9, kayda hazir).
/// Tirpan Isini bastan acik (ICERIK 6.2 #1, "B"); Civi Tabancasi tarifle acilir (#2): tarifin ilk Bakir Kulce'de
/// acilmasi ekonomi akisinda, o akis uretimde Unlock("civi_tabancasi") cagirir. Baslangic degeri weapons.json'da
/// (unlockedAtStart).
/// </summary>
public sealed class CapaState
{
    public HashSet<string> Unlocked { get; set; } = new();

    public static CapaState FromData(GameData data)
    {
        var s = new CapaState();
        foreach (var w in new[] { data.Tool.Weapon, data.Tool.Nailgun })
            if (w.UnlockedAtStart && !string.IsNullOrEmpty(w.Id)) s.Unlocked.Add(w.Id);
        return s;
    }

    public bool IsUnlocked(string weaponId) => Unlocked.Contains(weaponId);

    /// <returns>Yeni acildiysa true.</returns>
    public bool Unlock(string weaponId) => Unlocked.Add(weaponId);
}
