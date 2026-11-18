namespace Kabarma.Sim;

/// <summary>
/// Yapi bedelleri ve malzeme kurallari (GDD §7, ICERIK §10.1): sekil birimi x malzeme orani, yukseltme (yeni bedel
/// odenir, eskinin sokum iadesi doner), onarim (hasar oraninda %25), sokum (tapu icinde %100, disinda %50),
/// plan parcasina malzeme koyma.
/// </summary>
public static class StructureCosts
{
    /// <summary>
    /// Malzemenin bu sekil icin bedeli. Kesirler en buyuk kalanla dagitilir; toplam = yuvarla(birim x oranlar toplami),
    /// esitlikte ilk yazilan esya alir (Kerpic-Tas 3 birim: 2 Kerpic + 1 Tas).
    /// </summary>
    public static Dictionary<string, int> Cost(BuildFile b, string material, int units)
    {
        var res = new Dictionary<string, int>();
        var m = b.Material(material);
        if (m == null || units <= 0) return res;
        var rows = m.Cost.Select((kv, i) => (kv.Key, Exact: units * kv.Value, Order: i)).ToList();
        int target = (int)Math.Round(rows.Sum(r => r.Exact), MidpointRounding.AwayFromZero);
        foreach (var r in rows) res[r.Key] = (int)Math.Floor(r.Exact + 1e-4);
        int left = target - res.Values.Sum();
        foreach (var r in rows.OrderByDescending(r => r.Exact - Math.Floor(r.Exact + 1e-4)).ThenBy(r => r.Order))
        {
            if (left <= 0) break;
            res[r.Key]++;
            left--;
        }
        foreach (var k in res.Where(kv => kv.Value <= 0).Select(kv => kv.Key).ToList()) res.Remove(k);
        return res;
    }

    /// <summary>Sokum iadesi: tapu icinde RefundInClaim, disinda RefundOutClaim kati (asagi yuvarlanir).</summary>
    public static Dictionary<string, int> Refund(IReadOnlyDictionary<string, int> cost, bool inClaim, BuildFile b)
    {
        float rate = inClaim ? b.RefundInClaim : b.RefundOutClaim;
        var res = new Dictionary<string, int>();
        foreach (var kv in cost)
        {
            int n = (int)Math.Floor(kv.Value * rate + 1e-4);
            if (n > 0) res[kv.Key] = n;
        }
        return res;
    }

    /// <summary>Onarim bedeli: hasar oraninda bedelin RepairFraction kati, esya basina yukari yuvarlanir. Hasarsizsa bos.</summary>
    public static Dictionary<string, int> RepairCost(IReadOnlyDictionary<string, int> cost, float hp, float maxHp, BuildFile b)
    {
        var res = new Dictionary<string, int>();
        if (maxHp <= 0 || hp >= maxHp) return res;
        float damage = Math.Clamp(1f - hp / maxHp, 0f, 1f);
        foreach (var kv in cost)
        {
            int n = (int)Math.Ceiling(kv.Value * b.RepairFraction * damage - 1e-4);
            if (n > 0) res[kv.Key] = n;
        }
        return res;
    }

    /// <summary>Yukseltmede can: hasar orani korunur.</summary>
    public static float UpgradedHp(float hp, float oldMax, float newMax) => oldMax <= 0 ? newMax : Math.Clamp(hp / oldMax, 0f, 1f) * newMax;

    /// <summary>Yukseltmenin bedeli ve iadesi: bir ust malzeme yoksa null.</summary>
    public static (BuildMaterialDef To, Dictionary<string, int> Pay, Dictionary<string, int> Back)? Upgrade(BuildFile b, string material, int units, bool inClaim)
    {
        var next = b.Next(material);
        if (next == null) return null;
        return (next, Cost(b, next.Id, units), Refund(Cost(b, material, units), inClaim, b));
    }

    /// <summary>Plan parcasinin eksigi: bedel - konan.</summary>
    public static Dictionary<string, int> Missing(IReadOnlyDictionary<string, int> cost, IReadOnlyDictionary<string, int> delivered)
    {
        var res = new Dictionary<string, int>();
        foreach (var kv in cost)
        {
            int n = kv.Value - (delivered.TryGetValue(kv.Key, out int d) ? d : 0);
            if (n > 0) res[kv.Key] = n;
        }
        return res;
    }

    /// <summary>Cantadan plan parcasina eksik kadar malzeme koyar; konani dondurur.</summary>
    public static Dictionary<string, int> Deliver(IReadOnlyDictionary<string, int> cost, Dictionary<string, int> delivered, Inventory bag)
    {
        var moved = new Dictionary<string, int>();
        foreach (var (item, need) in Missing(cost, delivered))
        {
            int n = Math.Min(need, bag.Count(item));
            if (n <= 0) continue;
            bag.Remove(item, n);
            delivered[item] = (delivered.TryGetValue(item, out int d) ? d : 0) + n;
            moved[item] = n;
        }
        return moved;
    }
}
