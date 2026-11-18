namespace Kabarma.Sim;

/// <summary>
/// Ekip kartlari (Ciftlik sinifi ve ekip asilari, GDD 15): butun ekibi etkiler, ayni kart farkli oyunculardan
/// yigilmaz, en yuksek rutbe gecerlidir. Kuleler, hayvanlar, Kazan, yapilar ve korkuluklar degeri buradan okur
/// (degistirici adlari cards.json stat'lari: towerFireRate, animalDamage, kazanHp, structureRegen, wallThorns,
/// scarecrowsFight, workAnimalsFight, towerBackup, boyBonus, alacaMult, animalCopies, towerDouble, towerRegen, ozGain).
/// </summary>
public static class CardTeam
{
    public static Dictionary<string, int> Ranks(GameData d, IEnumerable<IReadOnlyDictionary<string, int>> perPlayer) =>
        NetCards.TeamRanks(perPlayer, id => d.Cards.Find(id)?.Team == true);

    /// <summary>Ekibin bir degistiricisi: her ekip kartinin en yuksek rutbesiyle (Nisan'siz; Nisan oyuncunundur).</summary>
    public static float Mod(GameData d, IReadOnlyDictionary<string, int> teamRanks, string stat)
    {
        float v = 0;
        foreach (var (id, rank) in teamRanks)
        {
            var c = d.Cards.Find(id);
            if (c == null || !c.Team || rank <= 0) continue;
            foreach (var e in c.AllEffects())
                if (e.Stat == stat) v += e.Once ? e.PerRank : e.PerRank * rank;
        }
        return v;
    }

    /// <summary>Tek oyunculu kolaylik: bir elin ekip degistiricisi.</summary>
    public static float Mod(GameData d, CardHand hand, string stat) =>
        Mod(d, Ranks(d, new[] { hand.Ranks }), stat);
}
