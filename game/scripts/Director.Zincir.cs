using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director (zincir M2): ambardan uretim (GDD §5), arastirma (GDD §12), Tarayici (GDD §4) ve Ocak basamak yukseltme (GDD §6).
/// Kurallar Sim'de (Research.cs, Crafting*.cs); burada dunyadan girdi ve geri bildirim.
/// </summary>
public partial class Director
{
    bool _zincirWired;
    float _coreHintT;

    /// <summary>Tapu icindeki Sandiklar (Ocak'tan tapu yaricapinda; Ocak yoksa hic).</summary>
    public IEnumerable<Inventory> ClaimChests()
    {
        if (Ocak == null) return Enumerable.Empty<Inventory>();
        float r = State.Research.ClaimRadius;
        var o = OcakPos;
        return Furniture.Where(f => IsInstanceValid(f) && f.Storage != null && new Vector2(f.GlobalPosition.X - o.X, f.GlobalPosition.Z - o.Z).Length() <= r).Select(f => f.Storage);
    }

    /// <summary>Istasyon tariflerinin deposu: canta, sonra tapudaki Sandiklar (GDD §5 ambardan uretim).</summary>
    public ItemPool StationStore() => State.StationStore(ClaimChests());

    /// <summary>Uretim panelinin sayacagi depo: istasyon tarifinde canta + Sandiklar, el tarifinde canta.</summary>
    public IItemStore StoreFor(RecipeDef r) => CraftRules.IsHand(r) && !Data.Research.HandFromStorage ? State.Backpack : StationStore();

    /// <summary>Arastirma Masasi yakinda mi.</summary>
    public bool NearResearchTable => NearStation(Data.Research.Table) != null;

    /// <summary>Tarif arastirmayla kilitli ve henuz arastirilmamis mi.</summary>
    public bool ResearchLocked(RecipeDef r) => Data.Research.NodeFor(r) is ResearchNodeDef n && !State.Research.IsDone(n.Id);

    /// <summary>
    /// Uretim panelinden: arastirmayla kilitli satir "uretilince" arastirilir (Masa yaninda, Veri yeterse). Satir arastirma
    /// satiri degilse false (olagan uretim surer).
    /// </summary>
    public bool TryResearch(RecipeDef r)
    {
        if (!ResearchLocked(r)) return false;
        var n = Data.Research.NodeFor(r)!;
        var block = State.Research.Check(n.Id, NearResearchTable);
        if (block != ResearchBlock.Ok)
        {
            Hud.Hint(ResearchBlockText(n, block), 1.8f);
            Sfx.Play("hit", null, -12f);
            return true;
        }
        State.Research.Research(n.Id, true);
        return true;
    }

    public string ResearchBlockText(ResearchNodeDef n, ResearchBlock b) => b switch
    {
        ResearchBlock.NeedsPrereq => "Önce araştır: " + string.Join(", ", n.Requires.Where(q => !State.Research.IsDone(q)).Select(ItemNames.Of)),
        ResearchBlock.NoTable => $"{ItemNames.Of(Data.Research.Table)} yanında araştırılır",
        ResearchBlock.NoVeri => $"Veri yetmiyor ({State.Research.Veri}/{Data.Research.CostOf(n)}): {{act:scan}} ile yeni türleri tara",
        ResearchBlock.Done => "Zaten araştırıldı",
        _ => "Araştırılamaz",
    };

    /// <summary>Kilitli satirin aciklamasi (Uretim paneli): arastirma, Ocak basamagi ya da ilk alinacak esya.</summary>
    public string LockText(RecipeDef r)
    {
        if (Data.Research.NodeFor(r) is ResearchNodeDef n)
        {
            var b = State.Research.Check(n.Id, NearResearchTable);
            return b == ResearchBlock.Ok || b == ResearchBlock.NoTable || b == ResearchBlock.NoVeri
                ? $"Araştırma: {Data.Research.CostOf(n)} Veri ({ItemNames.Of(Data.Research.Table)})"
                : ResearchBlockText(n, b);
        }
        int tier = ResearchFile.TierLock(r);
        if (tier > 0) return $"Ocak T{tier} gerekir (Ata Çekirdeği)";
        return $"Kilitli: ilk {ItemNames.Of(r.UnlockOn ?? "")} ile açılır";
    }

    void TickZincir(float dt)
    {
        if (!_zincirWired)
        {
            _zincirWired = true;
            State.Research.Researched += n =>
            {
                Hud.Big($"Araştırıldı: {ItemNames.Of(n.Id)}", Palette.Warning, 2.5f);
                Sfx.Play("pickup", null, 0f);
                GD.Print($"RESEARCHED {n.Id} veri={State.Research.Veri}");
            };
            State.Research.TierChanged += t => { Hud.Big($"Ocak T{t}: tapu {State.Research.ClaimRadius:0} m", Palette.Warning, 3f); GD.Print($"OCAK_TIER {t}"); };
        }
        if (!Hud.MenuOpen && Input.IsActionJustPressed("scan")) Scan();
        // Ocak basamagi: siradaki Cekirdek cantadaysa Ocak'in yaninda takilir (GDD §6).
        _coreHintT -= dt;
        if (Ocak != null && _coreHintT <= 0)
        {
            _coreHintT = 0.5f;
            var cores = Data.Research.TierCores;
            int next = State.Research.OcakTier + 1;
            if (next < cores.Count && !string.IsNullOrEmpty(cores[next]) && State.Backpack.Count(cores[next]) > 0)
            {
                if (OcakPos.DistanceTo(Player.GlobalPosition) < 3.5f) State.Research.TryUpgradeOcak(State.Backpack);
                else Hud.Hint($"{ItemNames.Of(cores[next])}: Ocak'ın yanına götür, basamak yükselsin", 1f);
            }
        }
    }

    /// <summary>
    /// Tarayici (GDD §4): nisan noktasina en yakin taranabilir tur (yaratik, bitki, maden). Ilk kez taranirsa Veri.
    /// Donen: kazanilan Veri (0: zaten taranmis, -1: hedef yok).
    /// </summary>
    public int Scan()
    {
        var sc = Data.Research.Scan;
        var aim = Player.AimPoint(out var hit);
        var me = Player.GlobalPosition;
        string kind = null, id = null;
        float best = sc.Pick;
        void Consider(string k, string i, Vector3 at)
        {
            if (at.DistanceTo(me) > sc.Range || State.Research.ScanEntry(k, i) == null) return;
            float d = at.DistanceTo(aim);
            if (d < best) { best = d; kind = k; id = i; }
        }
        // Isin bir yaratiga carptiysa dogrudan o.
        for (Node n = hit; n != null && n != this; n = n.GetParent())
            if (CreatureId(n) is string cid) { Consider("creature", cid, aim); best = -1; break; }
        if (best >= 0)
        {
            foreach (var c in FindChildren("*", "", true, false).OfType<Node3D>()) if (CreatureId(c) is string cid && c.Visible) Consider("creature", cid, c.GlobalPosition + Vector3.Up * 0.5f);
            foreach (var r in World.Rocks) if (IsInstanceValid(r)) Consider("mineral", r.Item, r.Center);
            foreach (var t in World.Tufts) if (t.Left > 0) Consider("plant", t.Item, t.Position);
            foreach (var p in World.TreePositions) Consider("plant", "tree", p + Vector3.Up * 1.2f);
            foreach (var v in FarmView.Views) if (v.Plot.Crop is string crop && crop.Length > 0) Consider("plant", crop, v.GlobalPosition);
        }
        if (id == null) { Hud.Hint("Tarayıcı: taranacak bir şeye bak (yaratık, bitki, maden)", 1.4f); Sfx.Play("hit", null, -16f); return -1; }
        int got = State.Research.Scan(kind, id);
        var name = State.Research.ScanEntry(kind, id)!.Name;
        if (got > 0)
        {
            Hud.Big($"Tarandı: {name}  +{got} Veri", Palette.CoopBlue.Lightened(0.3f), 2f);
            Sfx.Play("pickup", null, -4f);
        }
        else Hud.Hint($"{name} Ansiklopedi'de var ({State.Research.Veri} Veri)", 1.2f);
        GD.Print($"SCANNED {kind}:{id} +{Mathf.Max(0, got)} veri={State.Research.Veri}");
        return got;
    }

    /// <summary>Sandik menusunun ilk iki satiri (GDD §5 envanter duzeni): hizli yigma ve siralama.</summary>
    void AddChestTools(List<Hud.MenuItem> items, Furniture chest)
    {
        items.Add(new Hud.MenuItem("Aynılarını koy (hızlı yığ)", () => chest.Storage.Contents.Any(c => State.Backpack.Count(c.id) > 0), () =>
        {
            int n = State.Backpack.QuickStackTo(chest.Storage);
            Sfx.Play("place", null, -8f);
            GD.Print($"QUICKSTACK {n}");
            OpenChestMenu(chest);
        }));
        items.Add(new Hud.MenuItem("Çantayı ve Sandığı sırala", () => !State.Backpack.IsEmpty || !chest.Storage.IsEmpty, () =>
        {
            State.Backpack.Sort(SortOrder);
            chest.Storage.Sort(SortOrder);
            Sfx.Play("place", null, -14f);
            OpenChestMenu(chest);
        }));
    }

    /// <summary>Siralama: once ham kaynak, sonra ara urun, yemek, yerlestirilebilir (veri sirasina gore tur).</summary>
    int SortOrder(string id) => Data.Tool.IsPlaceable(id) ? 3 : Data.Tool.IsFood(id) ? 2 : Data.Tool.Intermediates.Contains(id) || Data.Tool.RecipeFor(id) != null ? 1 : 0;

    static string CreatureId(Node n) => n switch { Kabarma.Game.Gidak => "gidak", Zipzip => "zipzip", Kabarma.Game.Pofuduk => "pofuduk", _ => null };
}
