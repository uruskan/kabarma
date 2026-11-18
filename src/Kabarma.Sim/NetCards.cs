namespace Kabarma.Sim;

/// <summary>
/// Kabarma kartlari ortak oyunda (GDD 15, 22.1): her oyuncu kendi Oz'unu toplar, kendi seviyesini atlar ve kendi 3
/// kartindan secer; oyun durmaz ve ortak oyunda zaman yavaslamaz (yavaslama yalniz tek oyuncuda). Ekip kartlari
/// (Ciftlik sinifi) butun ekibi etkiler; ayni kart farkli oyunculardan yigilmaz, en yuksek rutbe gecerlidir.
/// </summary>
public static class NetCards
{
    /// <summary>Kart secimi sirasinda zaman olcegi: tek oyuncuda cards.json slowMoScale, ortak oyunda 1.</summary>
    public static float SelectionTimeScale(bool coop, float singleSlowMo) => coop ? 1f : singleSlowMo;

    /// <summary>Ekip kartlarinin gecerli rutbesi: oyuncularin rutbelerinin en buyugu (yigilmaz).</summary>
    public static Dictionary<string, int> TeamRanks(IEnumerable<IReadOnlyDictionary<string, int>> perPlayer, Func<string, bool> isTeamCard)
    {
        var r = new Dictionary<string, int>();
        foreach (var ranks in perPlayer)
            foreach (var (id, rank) in ranks)
                if (isTeamCard(id)) r[id] = Math.Max(r.GetValueOrDefault(id), rank);
        return r;
    }

    /// <summary>Kart secimi bolum yazimi: es basina alinan kartlar (Oyuncu listesinde ve gecenin sonunda Nisan icin).</summary>
    public static void Write(NetWriter w, IReadOnlyDictionary<string, int> ranks)
    {
        var l = ranks.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        w.U8((byte)l.Count);
        foreach (var (id, rank) in l) { w.Str(id); w.U8((byte)rank); }
    }

    public static Dictionary<string, int> Read(NetReader r)
    {
        int n = r.U8();
        var d = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++) d[r.Str()] = r.U8();
        return d;
    }
}
