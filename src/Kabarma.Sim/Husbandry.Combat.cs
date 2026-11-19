namespace Kabarma.Sim;

// M3 (GDD 9.5): Kabarma'da hayvanlar. Savas Agili (bayrakli agil) ve Ahir; kabarik hayvanin savas govdesi ortak hasar
// kuraliyla (Combat.cs, oyuncu takimi); bayilma, yorgunluk, ahirin yikilmasi, Ahir Zili, Baglama Diregi. Godot katmani
// (game/scripts/husbandry/Husbandry.Combat*.cs) hedef bulur ve Hit'i cagirir. Sayilar husbandry.json "combat", "forms".

/// <summary>Savas Agili Bayragi: durdugu agil Savas Agili olur.</summary>
public sealed class WarFlag
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>Baglama Diregi: bir binek baglanir (kacmaz, Kabarma'da saklanir).</summary>
public sealed class TiePost
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public int AnimalId { get; set; }
}

public sealed partial class PenAnimal
{
    /// <summary>Kabarik savas govdesi (yalniz Kabarma'da savasirken; yoksa null).</summary>
    public CombatBody? War { get; set; }
    /// <summary>Cani bitti: gece boyu yerde, safakta agilinda uyanir.</summary>
    public bool Fainted { get; set; }
    /// <summary>Kalan yorgun safak sayisi (uretmez, savasmaz, binilmez).</summary>
    public int TiredDawns { get; set; }
    /// <summary>Kabarma'da saklaniyor (ahirda ya da direge bagli).</summary>
    public bool Hidden { get; set; }
    /// <summary>Gece ahiri yikildi ve kacisti: safakta mutluluk yariya iner.</summary>
    public bool Scattered { get; set; }
    /// <summary>Kabarma basladiginda agilinin merkezi: bayilan safakta buraya doner.</summary>
    public float HomeX { get; set; }
    public float HomeZ { get; set; }
    /// <summary>Eyerli (Eyerlik'te takildi).</summary>
    public bool Saddled { get; set; }
    /// <summary>Bagli oldugu Baglama Diregi (0: yok).</summary>
    public int PostId { get; set; }
    /// <summary>Binen aktor (ortak oyunda es kimligi; 0: yok). Kayda girmez: yuklemede herkes iner.</summary>
    public int Rider { get; set; }
    /// <summary>Zil cagirdi: agilina donuyor (CallX, CallZ).</summary>
    public bool Called { get; set; }
    public float CallX { get; set; }
    public float CallZ { get; set; }
}

public enum WarEventKind { Swelled, Hid, Fainted, Died, Woke, Scattered, Called, SwellEnded }

public readonly record struct WarEvent(WarEventKind Kind, int AnimalId);

public sealed partial class Ranch
{
    public List<WarFlag> Flags { get; set; } = new();
    public List<TiePost> Posts { get; set; } = new();
    /// <summary>Kabarma surerken (KabarmaBegin ile Dawn arasi).</summary>
    public bool AtWar { get; set; }

    MountRules? _mount;
    /// <summary>Binek kurallari (yol sayacinin kesri gecicidir, kayda girmez).</summary>
    public MountRules Mount => _mount ??= new MountRules(this);

    public WarFlag AddFlag(float x, float z) { var f = new WarFlag { Id = NextId++, X = x, Z = z }; Flags.Add(f); return f; }
    public TiePost AddPost(float x, float z) { var p = new TiePost { Id = NextId++, X = x, Z = z }; Posts.Add(p); return p; }

    AnimalCombatDef War => Rules.Def.Combat;

    /// <summary>Icinde Savas Agili Bayragi duran agil.</summary>
    public bool IsWarPen(PenShape pen) => Flags.Any(f => In(pen, f.X, f.Z));

    /// <summary>Bayilmis ya da yorgun: uretmez, savasmaz, binilmez.</summary>
    public bool Exhausted(PenAnimal a) => a.Fainted || a.TiredDawns > 0;

    /// <summary>Kabarma'da savasir mi: evcil, kayitli, yetiskin, Savasci, yorgun degil, Savas Agili'nda, formu dovusur.</summary>
    public bool Fights(PenAnimal a, IReadOnlyList<PenShape> pens)
    {
        if (!a.Tame || !a.Registered || a.HiveId != 0 || a.Role != "savasci" || Exhausted(a)) return false;
        if (!a.Adult(Rules.Def.Breeding.AdultDays) || Rules.Def.FormOf(a.Species) is not { Fights: true }) return false;
        return PenOf(a, pens) is { } pen && IsWarPen(pen);
    }

    /// <summary>Kabarik savas cani: turun cani (creatures.json) x B geni x form carpani.</summary>
    public float WarHp(PenAnimal a, float creatureHp) => creatureHp * Rules.HpMult(a) * (Rules.Def.FormOf(a.Species)?.HpMult ?? 1f);

    /// <summary>
    /// Kabarma basladi (GDD 9.5): Savas Agili'ndaki Savascilar kabarir ve savas govdesi alir; kayitli ve direge bagli
    /// digerleri saklanir. creatureHp: tur kimligi → creatures.json cani. Ilk gunun ogretici hayvani kendi yolunda kalir.
    /// </summary>
    public List<WarEvent> KabarmaBegin(IReadOnlyList<PenShape> pens, Func<string, float> creatureHp)
    {
        var ev = new List<WarEvent>();
        AtWar = true;
        foreach (var a in Animals)
        {
            if (!Rules.Known(a.Species) || a.Tutorial || !a.Tame) continue;
            var pen = a.HiveId == 0 ? PenOf(a, pens) : null;
            if (pen != null) { a.HomeX = pen.Centroid.X; a.HomeZ = pen.Centroid.Z; }
            else { a.HomeX = a.X; a.HomeZ = a.Z; }
            if (Fights(a, pens))
            {
                var f = Rules.Def.FormOf(a.Species)!;
                float hp = WarHp(a, creatureHp(a.Species));
                a.War = CombatBody.Of(hp, CombatTeam.Player, a.Species, f.Armor, Math.Max(1f, Rules.Species(a.Species).Area / 4f));
                ev.Add(new WarEvent(WarEventKind.Swelled, a.Id));
            }
            else if (a.Registered || a.PostId != 0)
            {
                a.Hidden = true;
                ev.Add(new WarEvent(WarEventKind.Hid, a.Id));
            }
        }
        return ev;
    }

    /// <summary>
    /// Kabarma suresince: ahiri yikilan (agil cokgeni kayboldu) saklanan hayvan kacisir (kaydi duser, guveni kacma
    /// kuralindaki gibi, safakta mutluluk yariya iner). Godot katmani duvar dustukce cagirir.
    /// </summary>
    public List<WarEvent> WarTick(IReadOnlyList<PenShape> pens)
    {
        var ev = new List<WarEvent>();
        if (!AtWar) return ev;
        foreach (var a in Animals)
        {
            if (!a.Hidden || !a.Registered || a.HiveId != 0 || a.Tutorial) continue;
            if (PenOf(a, pens) != null) continue;
            Escape(a);
            a.Hidden = false;
            a.Scattered = true;
            ev.Add(new WarEvent(WarEventKind.Scattered, a.Id));
        }
        return ev;
    }

    /// <summary>Kabarik hayvana isabet (ortak hasar kurali). Cani biterse bayilir (Sert Ay'da olur).</summary>
    public CombatResult Hit(PenAnimal a, CombatFile combat, in CombatHit hit, List<WarEvent>? ev = null, List<CombatArea>? areas = null)
    {
        if (a.War == null || a.Fainted) return default;
        var r = Combat.Apply(combat, a.War, hit, areas);
        if (r.Killed) Faint(a, ev);
        return r;
    }

    /// <summary>Durumlar (yanik, zehir) islenir; surekli hasarla bayilabilir.</summary>
    public void WarStatusTick(CombatFile combat, float dt, List<WarEvent>? ev = null, List<CombatArea>? areas = null)
    {
        foreach (var a in Animals.Where(a => a.War != null && !a.Fainted).ToList())
        {
            Combat.Tick(combat, a.War!, dt, areas);
            if (a.War!.Dead) Faint(a, ev);
        }
    }

    void Faint(PenAnimal a, List<WarEvent>? ev)
    {
        if (War.HardMoonDies)
        {
            Animals.Remove(a);
            ev?.Add(new WarEvent(WarEventKind.Died, a.Id));
            return;
        }
        a.Fainted = true;
        ev?.Add(new WarEvent(WarEventKind.Fainted, a.Id));
    }

    /// <summary>Safak (her safak, Dawn'dan): yorgunluk azalir; bayilan agilinda yorgun uyanir; dagilanin mutlulugu yariya.</summary>
    void DawnCombat()
    {
        foreach (var a in Animals)
        {
            if (a.TiredDawns > 0 && !a.Fainted) a.TiredDawns--;
            if (a.Fainted)
            {
                a.Fainted = false;
                a.TiredDawns = War.TiredDays;
                a.X = a.HomeX; a.Z = a.HomeZ;
            }
            if (a.Scattered)
            {
                a.Scattered = false;
                if (a.Happiness > 0) a.Happiness *= 0.5f;
            }
            a.War = null;
            a.Hidden = false;
        }
        AtWar = false;
    }

    /// <summary>
    /// Ahir Zili (x, z; agilin merkezi home): yaricaptaki evcil, kayitsiz hayvanlar (izleyen ya da kacmis) agila doner.
    /// Kacmis hayvanin guveni geri gelir (mutlulugu kacma esiginin ustundeyse). Cagrilanlar doner.
    /// </summary>
    public List<WarEvent> Bell(float x, float z, float homeX, float homeZ)
    {
        var ev = new List<WarEvent>();
        float r2 = War.BellRadius * War.BellRadius;
        foreach (var a in Animals)
        {
            if (a.Registered || a.HiveId != 0 || a.Tutorial || a.Rider != 0 || a.PostId != 0 || a.Bound) continue;
            if ((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z) > r2) continue;
            bool escaped = !a.Tame && a.Trust >= Rules.Def.Escape.TrustAfter && a.Happiness > Rules.Def.Escape.Below;
            if (!a.Tame && !escaped) continue;
            if (escaped) a.Trust = Math.Max(a.Trust, 1f);
            a.Called = true; a.CallX = homeX; a.CallZ = homeZ;
            ev.Add(new WarEvent(WarEventKind.Called, a.Id));
        }
        return ev;
    }
}
