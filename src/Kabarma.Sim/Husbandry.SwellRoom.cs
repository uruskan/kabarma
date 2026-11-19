namespace Kabarma.Sim;

/// <summary>
/// Kabartma Odasi'nin hayvan kabartmasi (gece4, GDD 14.10): evcil hayvan Kabarma disinda da kabarik savas govdesi alir (can,
/// zirh ve vurus kabarik hayvanin kurallariyla, Ranch.Hit) ve sure (2 dk) dolunca soner. Kabarma gecesi gelirse gece kurali
/// devralir. Sayac duz ve gecicidir (kayda girmez: yuklemede kabarik hayvan sonmus sayilir).
/// </summary>
public sealed partial class Ranch
{
    readonly Dictionary<int, float> _daySwell = new();

    /// <summary>Gunduz kabartmasi suren hayvan var mi (Godot savas dongusunu acik tutar).</summary>
    public bool AnyDaySwell => _daySwell.Count > 0;

    public bool IsDaySwollen(PenAnimal a) => _daySwell.ContainsKey(a.Id);

    /// <summary>
    /// Hayvani <paramref name="seconds"/> sn kabartir: evcil, bilinen tur, formu var, bayilmamis/yorgun degil, zaten kabarik degil.
    /// Can = turun cani x B geni x form carpani (WarHp). Donen: kabardi mi.
    /// </summary>
    public bool SwellForSeconds(PenAnimal a, Func<string, float> creatureHp, float seconds, IReadOnlyList<PenShape> pens)
    {
        if (!a.Tame || !Rules.Known(a.Species) || a.Tutorial || Exhausted(a) || a.War != null || a.Hidden) return false;
        var f = Rules.Def.FormOf(a.Species);
        if (f == null) return false;
        var pen = PenOf(a, pens);
        if (pen != null) { a.HomeX = pen.Centroid.X; a.HomeZ = pen.Centroid.Z; }
        else { a.HomeX = a.X; a.HomeZ = a.Z; }
        a.War = CombatBody.Of(WarHp(a, creatureHp(a.Species)), CombatTeam.Player, a.Species, f.Armor, Math.Max(1f, Rules.Species(a.Species).Area / 4f));
        _daySwell[a.Id] = Math.Max(1f, seconds);
        return true;
    }

    /// <summary>Gunduz kabartmasi suresini ilerletir; bitenin savas govdesi duser (Kabarma suruyorsa gece kurali tutar). Olaylar: SwellEnded.</summary>
    public List<WarEvent> DaySwellTick(float dt)
    {
        var ev = new List<WarEvent>();
        if (_daySwell.Count == 0) return ev;
        foreach (var id in _daySwell.Keys.ToList())
        {
            float left = _daySwell[id] - dt;
            var a = Animals.FirstOrDefault(x => x.Id == id);
            if (a == null || a.Fainted) { _daySwell.Remove(id); continue; }
            if (left > 0f) { _daySwell[id] = left; continue; }
            _daySwell.Remove(id);
            if (!AtWar) a.War = null;
            ev.Add(new WarEvent(WarEventKind.SwellEnded, id));
        }
        return ev;
    }
}