using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

// Kayit icin Godot siniflarinin partial parcalari (TEKNIK 9.1): sahibi akisin dosyasina dokunmadan ozel alanlara
// erisen sessiz yukleyiciler. Olay yaymaz, ses ve efekt calmaz.

public partial class Rock
{
    internal float Wear => _wear;

    internal void LoadState(int stones, float wear)
    {
        Stones = Mathf.Clamp(stones, 0, _max);
        _wear = wear;
        if (_mesh != null) _mesh.Scale = Vector3.One * (0.45f + 0.55f * Stones / (float)Mathf.Max(1, _max));
    }
}

public partial class PenSystem
{
    /// <summary>Kayittan kurulan parcalardan agil halkalarini simdi hesaplar (normalde bir sonraki karede).</summary>
    internal void RebuildNow() { if (_dirty) Rebuild(); }

    /// <summary>
    /// Kayittan sonra yapi dugumleri yeniden kuruldu (BuildSystem.RebuildFromGraph Placed yaymaz): kapi kayitlari yeni
    /// dugumlerden kurulur, halkalar yeniden hesaplanir.
    /// </summary>
    internal void RelinkPieces()
    {
        _gates.Clear();
        var sys = _d.Build?.System;
        if (sys != null) foreach (var p in sys.Pieces) if (IsInstanceValid(p) && !p.IsQueuedForDeletion()) OnPlaced(p);
        _dirty = true;
        Rebuild();
    }

    internal IEnumerable<(BuildPiece piece, bool open)> GateStates => _gates.Where(kv => IsInstanceValid(kv.Key)).Select(kv => (kv.Key, kv.Value.Open));

    /// <summary>Bahce kapisini aninda acar/kapatir (kanat donusu ve carpisma; ses yok).</summary>
    internal void LoadGate(BuildPiece piece, bool open)
    {
        if (!_gates.TryGetValue(piece, out var g) || g.Door != null) return;
        g.Open = open;
        if (_wallOf.TryGetValue(g.Piece, out var w)) w.Open = open;
        float ang = Mathf.DegToRad(_def.GateOpenDegrees);
        foreach (var (leaf, sign) in g.Leaves) leaf.Rotation = new Vector3(leaf.Rotation.X, open ? ang * sign : 0f, leaf.Rotation.Z);
        foreach (var c in g.Cols) c.SetDeferred(CollisionShape3D.PropertyName.Disabled, open);
    }

    internal EggPickup LoadEgg(string product, Vector3 at)
    {
        var egg = new EggPickup { Product = product };
        _d.AddChild(egg);
        egg.GlobalPosition = at;
        _eggs.Add(egg);
        return egg;
    }
}

public partial class Gidak
{
    /// <summary>Kayittan: Coop.Animals'taki kural durumuna baglanir (Animal ozelligi yenisini kurmasin).</summary>
    internal void BindAnimal(PenAnimal a) => _animal = a;
}

public partial class Capa
{
    /// <summary>Basligi sessizce secer (Select ses ve ipucu verir).</summary>
    internal void LoadHead(Head h) => Current = h;
}

public partial class FarmView
{
    /// <summary>Kayittan sonra: tarla karelerinin gorunumunu Farm'dan bastan kurar (olaylar sessiz yuklemede yayilmaz).</summary>
    internal void Rebuild()
    {
        foreach (var v in _views.Values) if (IsInstanceValid(v)) { RemoveChild(v); v.QueueFree(); }
        _views.Clear();
        foreach (var p in Farm.Plots)
        {
            var v = new PlotView(p, this);
            _views[(p.X, p.Y)] = v;
            AddChild(v);
            v.GlobalPosition = CellCenter(p.X, p.Y);
            _world.MarkSoil(CellCenter(p.X, p.Y), 0.62f);
            if (p.Planted) v.OnPlanted();
            if (p.Planted && p.Swollen != null) v.OnSwelled();
        }
    }
}

public partial class SwollenCrop
{
    internal float PivotScale => _pivot?.Scale.X ?? 1f;
}

public static partial class Sfx
{
    /// <summary>Yeni oyun ya da yuklemeden once: eski sahnenin tween ve dugum basvurulari birakilir.</summary>
    internal static void ResetForNewGame()
    {
        if (_duck != null && GodotObject.IsInstanceValid(_duck)) _duck.Kill();
        _duck = null;
        Held.Clear(); Live.Clear();
        _beam = null; _beamId = null; _music = null; _d = null; _root = null;
    }
}

public partial class Hud
{
    /// <summary>Kayit yuklendi: toplanan esya akisinin cantayi yeniden ornekler (sessiz yukleme sahte '+N' gostermesin).</summary>
    internal void AfterLoad() => SnapshotBackpack();
}
