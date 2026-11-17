using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Vurulan hedefte ezilip esneme (GDD 4, SANAT-SES 7 Vurus): hedefin olcegine sonumlu yay. Yalnizca gorsel dugumlere
/// uygulanir (fizik govdesi olceklenmez: Visual() ile govdesiz cocuk bulunur): kaya, tutam, tarla karesi, kabarik urun.
/// Zipzip kendi ezilmesini yapar. Baska kod ayni olcegi degistirirse (kaya kazildikca kuculur) yeni deger taban olur.
/// Olcek oyun zamaniyla isler: vurus duraklamasinda o da durur.
/// </summary>
public partial class Squash : Node
{
    const string NodeName = "Squash";
    Node3D _target;
    Vector3 _base, _last;
    float _amount, _seconds, _wobbles, _t;

    public static void Kick(Node3D target, float amount, float seconds, float wobbles)
    {
        if (target == null || !IsInstanceValid(target) || amount <= 0f || seconds <= 0f) return;
        target = Visual(target);
        if (target == null) return;
        var s = target.GetNodeOrNull<Squash>(NodeName);
        if (s == null)
        {
            s = new Squash { Name = NodeName, _target = target, _base = target.Scale, _last = target.Scale };
            target.AddChild(s);
        }
        s._amount = amount; s._seconds = seconds; s._wobbles = Mathf.Max(0.5f, wobbles); s._t = 0f;
    }

    /// <summary>Dugumun altinda fizik govdesi tasimayan ilk gorsel cocuk (kendisi govde tasimiyorsa kendisi).</summary>
    public static Node3D Visual(Node3D n)
    {
        if (n == null) return null;
        bool hasBody = n is CollisionObject3D || n.GetChildren().Any(c => c is CollisionObject3D);
        if (!hasBody) return n;
        return n.GetChildren().OfType<Node3D>().FirstOrDefault(c => c is not CollisionObject3D && !c.FindChildren("*", "CollisionObject3D", true, false).Any());
    }

    /// <summary>Esnemeyi keser, olcegi oldugu gibi birakir (ornegin satista olcegi baska bir ara deger surecekse).</summary>
    public static void Stop(Node3D target)
    {
        target = Visual(target); // Kick esnemeyi govdesiz gorsel cocuga koyar
        var s = target?.GetNodeOrNull<Squash>(NodeName);
        if (s == null) return;
        target.Scale = s._base;
        s.QueueFree();
        s.Name = NodeName + "_bitti";
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_target)) { QueueFree(); return; }
        if (!_target.Scale.IsEqualApprox(_last)) _base = _target.Scale; // baska kod olcegi degistirdi
        _t += (float)delta;
        float k = _t / _seconds;
        if (k >= 1f) { _target.Scale = _base; QueueFree(); Name = NodeName + "_bitti"; return; }
        float env = (1f - k) * (1f - k);
        float w = Mathf.Sin(k * Mathf.Pi * 2f * _wobbles) * _amount * env;
        // Once yassilir (y kisalir, yanlar genisler), sonra esner.
        _target.Scale = _last = new Vector3(_base.X * (1f + w), _base.Y * (1f - w), _base.Z * (1f + w));
    }

    /// <summary>
    /// Ornekli cizimdeki (MultiMesh) tek bir ornegi esnetir: agaclar World.Decor'da tek bir MultiMesh (dunya akisi).
    /// Ornek bulunamazsa sessizce gecer.
    /// </summary>
    public static void KickInstance(MultiMeshInstance3D mmi, Vector3 localOrigin, float amount, float seconds, float wobbles)
    {
        if (mmi?.Multimesh == null || amount <= 0f || seconds <= 0f) return;
        var mm = mmi.Multimesh;
        int best = -1; float bd = 0.25f;
        for (int i = 0; i < mm.InstanceCount; i++)
        {
            float d = mm.GetInstanceTransform(i).Origin.DistanceSquaredTo(localOrigin);
            if (d < bd) { bd = d; best = i; }
        }
        if (best < 0) return;
        string name = $"{NodeName}_{best}";
        var s = mmi.GetNodeOrNull<InstanceSquash>(name);
        if (s == null) { s = new InstanceSquash { Name = name, Mmi = mmi, Index = best, Base = mm.GetInstanceTransform(best) }; mmi.AddChild(s); }
        s.Restart(amount, seconds, Mathf.Max(0.5f, wobbles));
    }
}

/// <summary>Squash.KickInstance'in dugumu: MultiMesh'teki tek ornegin donusumunu esnetir, bitince geri koyar.</summary>
public partial class InstanceSquash : Node
{
    public MultiMeshInstance3D Mmi;
    public int Index;
    public Transform3D Base;
    float _a, _s, _w, _t;

    public void Restart(float a, float s, float w) { _a = a; _s = s; _w = w; _t = 0f; }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Mmi)) { QueueFree(); return; }
        _t += (float)delta;
        float k = _t / _s;
        if (k >= 1f) { Mmi.Multimesh.SetInstanceTransform(Index, Base); QueueFree(); Name = "bitti"; return; }
        float w = Mathf.Sin(k * Mathf.Pi * 2f * _w) * _a * (1f - k) * (1f - k);
        var b = Base.Basis.Scaled(new Vector3(1f + w, 1f - w, 1f + w));
        Mmi.Multimesh.SetInstanceTransform(Index, new Transform3D(b, Base.Origin));
    }
}
