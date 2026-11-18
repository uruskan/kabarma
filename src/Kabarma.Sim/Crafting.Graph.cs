namespace Kabarma.Sim;

/// <summary>
/// Tarif grafi (TEKNIK §11, ICERIK §20): baslangictan bir Ocak basamagina kadar neyin elde edilebilecegini kapilarla
/// birlikte yurur. Bir tarif uretilir ancak: istasyonu kurulmus (istasyonun kendisi bir tarifin urunu), kilidi acik
/// (ilk alinan esya, "arastirma:x" ve agactaki onculleri + Arastirma Masasi, "ocak:n" ≤ basamak), butun girdileri ve
/// yakiti elde edilebilir. Ham kaynaklar research.json rawSources'tan (basamagiyla), baslangic esyalari tool.json'dan.
/// Veri sinirsiz sayilir (Veri kaynagi tarama ve is emirleri; bedeli denge simulasyonu sinar).
/// </summary>
public sealed class RecipeGraph
{
    public HashSet<string> Items { get; } = new();
    public HashSet<string> Stations { get; } = new();
    public HashSet<string> Tokens { get; } = new();
    public List<RecipeDef> Made { get; } = new();
    public List<RecipeDef> Unreached { get; } = new();

    public bool Has(string item) => Items.Contains(item);

    public static RecipeGraph Walk(GameData d, int ocakTier, IReadOnlyList<RecipeDef>? recipes = null)
    {
        var g = new RecipeGraph();
        var all = recipes ?? d.Tool.Recipes;
        var rs = d.Research;
        foreach (var id in d.Tool.StartItems.Keys) g.Items.Add(id);
        foreach (var s in rs.RawSources) if (s.Tier <= ocakTier) g.Items.Add(s.Id);
        for (int k = 1; k <= ocakTier; k++) g.Tokens.Add(ResearchFile.TierPrefix + k);
        var left = all.ToList();
        bool changed = true;
        while (changed)
        {
            changed = false;
            // Kurulu istasyonlar: urunu elde edilmis yerlestirilebilir istasyonlar.
            foreach (var p in d.Tool.Placeables) if (p.Station != null && g.Items.Contains(p.Id) && g.Stations.Add(p.Station)) changed = true;
            // Arastirma: masa kurulu ve oncul dugumler arastirilmis.
            if (g.Stations.Contains(rs.Table))
                foreach (var n in rs.Nodes)
                    if (!g.Tokens.Contains(ResearchState.Token(n.Id)) && n.Requires.All(q => g.Tokens.Contains(ResearchState.Token(q))))
                    { g.Tokens.Add(ResearchState.Token(n.Id)); changed = true; }
            for (int i = left.Count - 1; i >= 0; i--)
            {
                var r = left[i];
                if (!g.CanMake(d, r)) continue;
                g.Items.Add(r.Id);
                g.Made.Add(r);
                left.RemoveAt(i);
                changed = true;
            }
        }
        g.Unreached.AddRange(left);
        return g;
    }

    bool CanMake(GameData d, RecipeDef r)
    {
        if (!string.IsNullOrEmpty(r.Station) && !Stations.Contains(r.Station)) return false;
        // Guc isteyen istasyon (research.json watts) ancak bir ureteci (power.json kind generator) elde edilebiliyorsa calisir.
        if (!string.IsNullOrEmpty(r.Station) && d.Research.Watts.GetValueOrDefault(r.Station) > 0
            && !d.Power.Devices.Any(g => g.Kind == "generator" && Items.Contains(g.Id))) return false;
        if (r.UnlockOn != null && !Tokens.Contains(r.UnlockOn) && !Items.Contains(r.UnlockOn)) return false;
        if (r.In.Keys.Any(k => !Items.Contains(k))) return false;
        var st = string.IsNullOrEmpty(r.Station) ? null : d.Tool.Placeables.FirstOrDefault(p => p.Station == r.Station);
        if (st != null && st.FuelPerJob > 0 && st.Fuel.Count > 0 && !st.Fuel.Any(Items.Contains)) return false;
        return true;
    }
}
