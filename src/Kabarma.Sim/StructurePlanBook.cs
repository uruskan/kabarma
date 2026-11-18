namespace Kabarma.Sim;

/// <summary>Plan Defteri'ndeki bir parca: ilk temele gore yer (cevrilmis) ve yon.</summary>
public sealed class BlueprintPiece
{
    public string Shape { get; set; } = "";
    public string Material { get; set; } = "wood";
    public float[] Pos { get; set; } = new float[3];
    public float Yaw { get; set; }
}

/// <summary>Kaydedilmis yapi plani (GDD §7 Plan Defteri). Parcalar kurulma sirasiyla (destek once).</summary>
public sealed class Blueprint
{
    public string Name { get; set; } = "";
    public List<BlueprintPiece> Pieces { get; set; } = new();

    /// <summary>Malzemeye gore toplam bedel (sekil birimi units(shape) ile).</summary>
    public Dictionary<string, int> TotalCost(BuildFile b, Func<string, int> units)
    {
        var tot = new Dictionary<string, int>();
        foreach (var p in Pieces)
            foreach (var (k, n) in StructureCosts.Cost(b, p.Material, units(p.Shape)))
                tot[k] = (tot.TryGetValue(k, out int o) ? o : 0) + n;
        return tot;
    }
}

/// <summary>Plan Defteri: 12 yuva (build.json planBook.slots). Duz ve serilestirilebilir.</summary>
public sealed class PlanBook
{
    public List<Blueprint?> Slots { get; set; } = new();

    public void EnsureSlots(int n)
    {
        while (Slots.Count < n) Slots.Add(null);
    }

    /// <summary>Godot ekseni: Basis(Y, a) * (x, 0, z).</summary>
    public static (float X, float Z) Rotate(float x, float z, float a)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a);
        return (x * c + z * s, -x * s + z * c);
    }

    /// <summary>
    /// fromId'nin bagli yapisini (kurulu ve plan, en fazla maxPieces) plana cevirir. Dayanak: bilesendeki ilk araziye
    /// oturan parca (yoksa fromId). Sira: plan parcalari kurulu sayildiginda aciklik, sonra yukseklik.
    /// </summary>
    public static Blueprint? Capture(StructureGraph g, int fromId, int maxPieces, string name)
    {
        var start = g.Get(fromId);
        if (start == null) return null;
        var comp = new List<StructurePiece>();
        var seen = new HashSet<int> { fromId };
        var q = new Queue<StructurePiece>();
        q.Enqueue(start);
        while (q.Count > 0 && comp.Count < maxPieces)
        {
            var p = q.Dequeue();
            comp.Add(p);
            foreach (var n in p.SupportedBy.Concat(p.Supports).OrderBy(x => x))
                if (seen.Add(n) && g.Get(n) is { } np) q.Enqueue(np);
        }
        var anchor = comp.Where(p => p.Grounded).OrderBy(p => p.Id).FirstOrDefault() ?? start;
        var spans = g.SolveSpans(comp, _ => true, null).Spans;
        int SpanOf(StructurePiece p) => spans.TryGetValue(p.Id, out int s) ? s : StructurePiece.Unsupported;
        var bp = new Blueprint { Name = name };
        foreach (var p in comp.OrderBy(p => p == anchor ? 0 : 1).ThenBy(SpanOf).ThenBy(p => p.CenterY).ThenBy(p => p.Id))
        {
            var (x, z) = Rotate(p.Pos[0] - anchor.Pos[0], p.Pos[2] - anchor.Pos[2], -anchor.Yaw);
            bp.Pieces.Add(new BlueprintPiece
            {
                Shape = p.Shape,
                Material = p.Material,
                Pos = new[] { x, p.Pos[1] - anchor.Pos[1], z },
                Yaw = Wrap(p.Yaw - anchor.Yaw),
            });
        }
        return bp;
    }

    /// <summary>Plan parcasinin dunya yeri: dayanak 'at' (dunya) ve 'yaw' ile.</summary>
    public static (float[] Pos, float Yaw) ToWorld(BlueprintPiece p, float[] at, float yaw)
    {
        var (x, z) = Rotate(p.Pos[0], p.Pos[2], yaw);
        return (new[] { at[0] + x, at[1] + p.Pos[1], at[2] + z }, Wrap(p.Yaw + yaw));
    }

    static float Wrap(float a)
    {
        float t = MathF.PI * 2f;
        a %= t;
        return a < 0 ? a + t : a;
    }

    public bool Save(int slot, Blueprint bp)
    {
        if (slot < 0 || slot >= Slots.Count) return false;
        Slots[slot] = bp;
        return true;
    }

    public Blueprint? Get(int slot) => slot >= 0 && slot < Slots.Count ? Slots[slot] : null;
}

/// <summary>Yapi durumu: parca grafi ve Plan Defteri (kayit bolumu buradan okunur; TEKNIK §9).</summary>
public sealed class StructureState
{
    public StructureGraph Graph { get; set; } = new();
    public PlanBook PlanBook { get; set; } = new();

    /// <summary>Yeni oyun ya da kayittan sonra: veriyi bagla, turetilmis degerleri kur, yuvalari tamamla.</summary>
    public void Bind(BuildFile data)
    {
        Graph.Bind(data);
        PlanBook.EnsureSlots(data.PlanBook.Slots);
    }
}

/// <summary>GameState parcasi: yapi (GDD §7).</summary>
public sealed partial class GameState
{
    StructureState? _build;

    /// <summary>Ilk eriside veriden baglanir. Kayit yukleyen yeni durumu atar ve Bind cagirir.</summary>
    public StructureState Build
    {
        get
        {
            if (_build == null) { _build = new StructureState(); _build.Bind(Data.Build); }
            return _build;
        }
        set { _build = value; _build.Bind(Data.Build); }
    }
}
