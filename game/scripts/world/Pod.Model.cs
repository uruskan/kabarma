using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kapsulun parcali gorunusu (gorsel-model): data/kapsul.json'daki kapak-rampa, dort inis ayagi, Kargo Rampasi pisti ve
/// kargo roketi modelleri. Pod.cs'nin kurdugu dugumlere (_hatch mentesesi, _pad, _rocket) takilir; acilis ve rampa
/// animasyonlari degismeden calisir. Model dosyasi yoksa o parca eski gorunusunde kalir (Tripo uretimi bekleniyor:
/// progress/gorsel/model/uretim_istegi.md).
/// </summary>
public partial class Pod
{
    bool _modelApplied;
    static KapsulFile _kapsul;

    static KapsulFile Kapsul
    {
        get
        {
            if (_kapsul != null) return _kapsul;
            const string path = "res://data/kapsul.json";
            _kapsul = FileAccess.FileExists(path) ? GameData.ParseKapsul(FileAccess.GetFileAsString(path)) : new KapsulFile();
            return _kapsul;
        }
    }

    public override void _EnterTree()
    {
        if (_modelApplied) return;
        _modelApplied = true;
        var k = Kapsul;
        SwapVisual(_pad, k.Pad);
        SwapVisual(_rocket, k.Rocket);
        // Govdede kendi 'hatch' parcasi varsa (eski kapsul.glb) Pod._Ready onu menteseye takar; ayri kapak yalniz yeni govdeyle.
        bool bodyHasHatch = _golden?.FindChild("hatch", true, false) != null;
        if (!bodyHasHatch && Assets.Load(k.Hatch) is Node3D hatch)
        {
            foreach (var c in _hatch.GetChildren()) c.QueueFree();
            if (k.Hinge.Length == 3) _hatch.Position = new Vector3(k.Hinge[0], k.Hinge[1], k.Hinge[2]);
            Fit(hatch, k.HatchLength);
            // Model yatik: mentese kenari -X ucunda, orta x=z=0, taban y=0. Mentese kenarini kokune tasi, sonra dik cevir.
            var box = Bounds(hatch);
            var holder = new Node3D { Name = "hatch_model" };
            holder.AddChild(hatch);
            hatch.Position = new Vector3(-box.Position.X, 0, 0);
            if (k.HatchClosedRotation.Length == 3)
                holder.RotationDegrees = new Vector3(k.HatchClosedRotation[0], k.HatchClosedRotation[1], k.HatchClosedRotation[2]);
            _hatch.AddChild(holder);
        }
        if (_golden != null && Assets.Load(k.Leg) is Node3D legProto)
        {
            Fit(legProto, k.LegLength);
            foreach (var l in k.Legs.Where(l => l.Pos.Length == 3))
            {
                var leg = (Node3D)legProto.Duplicate();
                var mount = new Node3D { Name = "leg", Position = new Vector3(l.Pos[0], l.Pos[1], l.Pos[2]) };
                mount.RotationDegrees = new Vector3(0, l.Yaw, 0);
                leg.RotationDegrees = new Vector3(0, 0, -l.Tilt);
                mount.AddChild(leg);
                AddChild(mount);
            }
            legProto.Free();
        }
    }

    /// <summary>MeshKit gorselinin yerine model: dugumun kendisi (gorunurluk ve konum animasyonu) korunur.</summary>
    static void SwapVisual(Node3D slot, string path)
    {
        if (slot == null || string.IsNullOrEmpty(path)) return;
        var model = Assets.Load(path);
        if (model == null) return;
        if (slot is MeshInstance3D mi) mi.Mesh = null;
        foreach (var c in slot.GetChildren().OfType<MeshInstance3D>()) c.QueueFree();
        slot.AddChild(model);
    }

    /// <summary>Modelin en uzun boyunu metreye olcekler (0: dokunma).</summary>
    static void Fit(Node3D n, float length)
    {
        if (length <= 0) return;
        var b = Bounds(n);
        float longest = Mathf.Max(b.Size.X, Mathf.Max(b.Size.Y, b.Size.Z));
        if (longest > 0.001f) n.Scale = Vector3.One * (length / longest);
    }

    static Aabb Bounds(Node3D n)
    {
        var box = new Aabb();
        bool first = true;
        foreach (var mi in n.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var t = Transform3D.Identity;
            Node cur = mi;
            while (cur != null && cur != n) { if (cur is Node3D n3) t = n3.Transform * t; cur = cur.GetParent(); }
            var b = n.Transform * (t * mi.GetAabb());
            box = first ? b : box.Merge(b);
            first = false;
        }
        return box;
    }
}
