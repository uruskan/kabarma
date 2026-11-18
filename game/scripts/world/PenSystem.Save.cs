using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Agil yerlestirilebilirlerinin kaydi (TEKNIK 9.1, kayit sozlesmesi): Yemlik, Folluk, Ahsap Kapi, Suluk, Kasinma
/// Fircasi, Ari Kovani. Her biri bir EntityRecord ("pen_prop"): yer, donus ve Sim nesnesinin icerigi (Params).
/// Spawn sirasinda okunur: once var olanlar silinir, sonra kayittakiler kurulur. Hayvanlar Sim'in "pen.animals" ve
/// "ranch" bolumlerinde; aktorleri RanchSystem ilk karesinde baglar.
/// </summary>
public partial class PenSystem
{
    bool _saveBound;

    /// <summary>Director.Save defterine "pen.props" bolumunu baglar (bir kez).</summary>
    public void BindSave()
    {
        if (_saveBound) return;
        _saveBound = true;
        _d.Save.Add(SaveSection.Json("pen.props", 1, CaptureProps, ApplyProps, SaveOrder.Spawn));
    }

    static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    static float P(SortedDictionary<string, string> d, string k) => d.TryGetValue(k, out var s) ? float.Parse(s, CultureInfo.InvariantCulture) : 0f;
    static int I(SortedDictionary<string, string> d, string k) => d.TryGetValue(k, out var s) ? int.Parse(s, CultureInfo.InvariantCulture) : 0;

    List<EntityRecord> CaptureProps()
    {
        var list = new List<EntityRecord>();
        foreach (var p in _props.Where(IsInstanceValid))
        {
            var q = p.GlobalBasis.GetRotationQuaternion();
            var r = new EntityRecord
            {
                Kind = "pen_prop", Content = p.Def.Id,
                Pos = new[] { p.GlobalPosition.X, p.GlobalPosition.Y, p.GlobalPosition.Z },
                Rot = new[] { q.X, q.Y, q.Z, q.W },
            };
            if (p.Feeder != null) { r.Params["id"] = p.Feeder.Id.ToString(CultureInfo.InvariantCulture); r.Params["food"] = p.Feeder.Food; r.Params["amount"] = p.Feeder.Amount.ToString(CultureInfo.InvariantCulture); }
            if (p.Nest != null) { r.Params["id"] = p.Nest.Id.ToString(CultureInfo.InvariantCulture); r.Params["eggs"] = p.Nest.Eggs.ToString(CultureInfo.InvariantCulture); }
            if (p.Trough != null) { r.Params["id"] = p.Trough.Id.ToString(CultureInfo.InvariantCulture); r.Params["water"] = F(p.Trough.Water); }
            if (p.Brush != null) r.Params["id"] = p.Brush.Id.ToString(CultureInfo.InvariantCulture);
            if (p.Hive != null) { r.Params["id"] = p.Hive.Id.ToString(CultureInfo.InvariantCulture); r.Params["animal"] = p.Hive.AnimalId.ToString(CultureInfo.InvariantCulture); }
            if (p.Kasa != null) r.Params["open"] = p.DoorOpen ? "1" : "0";
            list.Add(r);
        }
        return list;
    }

    void ApplyProps(List<EntityRecord> list)
    {
        var ranch = _d.State.Ranch;
        foreach (var p in _props.Where(IsInstanceValid))
        {
            if (p.Feeder != null) ranch.Feeders.Remove(p.Feeder);
            if (p.Nest != null) ranch.Nests.Remove(p.Nest);
            if (p.Trough != null) ranch.Troughs.Remove(p.Trough);
            if (p.Brush != null) ranch.Brushes.Remove(p.Brush);
            if (p.Hive != null) ranch.Hives.Remove(p.Hive);
            p.QueueFree();
        }
        _props.Clear();
        foreach (var r in list ?? new())
        {
            var def = _d.Data.Tool.Placeables.FirstOrDefault(x => x.Id == r.Content);
            if (def == null || r.Pos.Length < 3) continue;
            var q = r.Rot.Length >= 4 ? new Quaternion(r.Rot[0], r.Rot[1], r.Rot[2], r.Rot[3]) : Quaternion.Identity;
            var prop = PlaceProp(def, new Vector3(r.Pos[0], r.Pos[1], r.Pos[2]), new Basis(q).GetEuler().Y);
            var d = r.Params;
            if (prop.Feeder != null) { prop.Feeder.Id = I(d, "id"); prop.Feeder.Food = d.GetValueOrDefault("food", prop.Feeder.Food); prop.Feeder.Amount = I(d, "amount"); }
            if (prop.Nest != null) { prop.Nest.Id = I(d, "id"); prop.Nest.Eggs = I(d, "eggs"); }
            if (prop.Trough != null) { prop.Trough.Id = I(d, "id"); prop.Trough.Water = P(d, "water"); }
            if (prop.Brush != null) prop.Brush.Id = I(d, "id");
            if (prop.Hive != null) { prop.Hive.Id = I(d, "id"); prop.Hive.AnimalId = I(d, "animal"); }
            if (prop.Kasa != null && d.GetValueOrDefault("open") == "1" && !prop.DoorOpen) prop.ToggleDoor();
        }
    }
}
