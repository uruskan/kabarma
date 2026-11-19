using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// kusam (M4): ICERIK 7 #6-#12 yedi guc ve iki ulti (Zaman Bali, Cekim Kuyusu, Roket Ziplama, Alev Cemberi, Isinlan, Buz Duvari,
/// Gorunmez Pelerin; Yorunge Sulamasi, Zaman Durdu). Sayilar abilities.json, kural AbilityRules; burada dunyadaki karsiligi.
/// Dusman bulma: Zipzip.All + Kabarans + deneme hayaletleri (AbilitySystem.Shock ile ayni uc kaynak).
/// Bilinen sinir: Gorunmez Pelerin hedef secimini simdilik degistirmez (Sim: AbilityLoadout.CloakLeft, Cloaked); Zaman Bali
/// yalniz Zipzip'i yavaslatir (FreezeT), Kabaran'a hasar ve itme ile; Kadim Tas denemeleri henuz yok (Unlock cagrilirsa acilir).
/// </summary>
public partial class AbilitySystem
{
    public bool Cloaked => L.CloakLeft > 0;
    public bool TimeStopped => L.TimeStopLeft > 0;

    float _balT, _wellT, _ringT, _ringTick, _rainT, _rainTick, _rocketGrace;
    Vector3 _balPos, _wellPos, _ringPos, _rainPos;
    bool _rocketUp;
    float _markPrev;
    readonly List<(Node3D wall, float t)> _walls = new();
    readonly List<Node3D> _frozen = new();

    /// <summary>Etkin alanlardaki dusmanlar (Zipzip, Kabaran ve hayaletler), tekrarsiz.</summary>
    List<Node3D> Foes(Vector3 at, float radius)
    {
        var list = new List<Node3D>();
        foreach (var z in Zipzip.All.ToArray()) if (!z.Dead && AbilityRules.InRadius(at.X, at.Z, radius, z.GlobalPosition.X, z.GlobalPosition.Z)) list.Add(z);
        foreach (var k in Kabarans.Near(at, radius)) if (!list.Contains(k.Node)) list.Add(k.Node);
        foreach (var g in Ghosts.ToArray()) if (!g.Dead && AbilityRules.InRadius(at.X, at.Z, radius, g.GlobalPosition.X, g.GlobalPosition.Z)) list.Add(g);
        return list;
    }

    void HitFoe(Node3D n, float dmg, Vector3 from, float knock)
    {
        var dir = ((n.GlobalPosition - from) with { Y = 0 }).Normalized() * knock;
        switch (n)
        {
            case Zipzip z: z.Hit(dmg, false, dir, dir.Length(), false); break;
            case TrialGhost g: g.Hit(dmg, dir); break;
            default:
                var k = Kabarans.Live.FirstOrDefault(x => x.Node == n);
                if (k != null) Kabarans.Hit(k, dmg, from, knock);
                break;
        }
    }

    /// <summary>Yeni guclerin etkisi (Apply'in varsayilan kolu).</summary>
    void ApplyM4(AbilityDef def, Vector3 at, Vector3 aim, bool local)
    {
        switch (def.Id)
        {
            case "zaman_bali":
                _balT = def.Duration; _balPos = D.World.Ground(at + aim * 3f);
                Vfx.Ring(_balPos + Vector3.Up * 0.1f, def.Radius, def.Duration, new Color(1f, 0.8f, 0.3f), 0.25f);
                Sfx.Play("place", at, -4f);
                break;
            case "cekim_kuyusu":
                _wellT = def.Duration; _wellPos = D.World.Ground(at + aim * 8f);
                Vfx.Ring(_wellPos + Vector3.Up * 0.1f, def.Radius, def.Duration, Palette.MayaViolet, 0.3f);
                Sfx.Play("levelup", at, -6f);
                break;
            case "roket_ziplama":
                if (!local) break;
                var p = D.Player;
                p.Velocity = new Vector3(p.Velocity.X, Mathf.Sqrt(2f * 18f * def.Height), p.Velocity.Z); // oyuncu yercekimi 18
                _rocketUp = true; _rocketGrace = 0.25f;
                Fx.Burst(at + Vector3.Up * 0.2f, Palette.Accent, 24, 5f, 0.12f, 0.5f, true, 0f);
                break;
            case "alev_cemberi":
                _ringT = def.Duration; _ringPos = at; _ringTick = 0f;
                Vfx.Ring(at + Vector3.Up * 0.1f, def.Radius, def.Duration, Palette.Warning, 0.4f);
                Sfx.Play("hit", at, -4f);
                break;
            case "isinlan":
                L.MarkX = at.X; L.MarkZ = at.Z; // MarkLeft AbilityRules.Use'ta kuruldu; sure dolunca geri doner (TickM4)
                Vfx.Ring(at + Vector3.Up * 0.1f, 1.2f, def.Duration, Palette.MayaTeal, 0.2f);
                Sfx.Play("pickup", at, -4f);
                break;
            case "buz_duvari":
                SpawnWall(def, at, aim);
                break;
            case "gorunmez_pelerin":
                Sfx.Play("levelup", at, -8f);
                break;
            case "yorunge_sulamasi":
                _rainT = def.Duration; _rainTick = 0f; _rainPos = D.World.Ground(at + aim * def.Distance);
                Vfx.Ring(_rainPos + Vector3.Up * 0.1f, def.Radius, def.Duration, new Color("8CC8E8"), 0.5f);
                Hud().Big("YÖRÜNGE SULAMASI", new Color("8CC8E8"), 2f);
                break;
            case "zaman_durdu":
                FreezeAll();
                Hud().Big("ZAMAN DURDU", Palette.MayaTeal, 2f);
                break;
        }
    }

    void SpawnWall(AbilityDef def, Vector3 at, Vector3 aim)
    {
        var (a, b) = AbilityRules.WallEnds(def, at.X, at.Z, aim.X, aim.Z);
        var mid = D.World.Ground(new Vector3((a.x + b.x) * 0.5f, 0, (a.z + b.z) * 0.5f));
        var body = new StaticBody3D();
        var shape = new BoxShape3D { Size = new Vector3(def.Length, 3f, 0.8f) };
        body.AddChild(new CollisionShape3D { Shape = shape });
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = shape.Size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.65f, 0.88f, 1f, 0.7f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = new Color("8CC8E8"), EmissionEnergyMultiplier = 0.6f },
        };
        body.AddChild(mesh);
        D.AddChild(body);
        body.GlobalPosition = mid + Vector3.Up * 1.5f;
        body.LookAt(body.GlobalPosition + new Vector3(aim.X, 0, aim.Z), Vector3.Up); // duvarin uzun kenari nisana dik
        _walls.Add((body, def.Duration));
        Sfx.Play("place", at, -2f);
        Fx.Burst(mid + Vector3.Up, new Color("BFE6F5"), 30, 4f, 0.1f, 0.6f, true, 0f);
    }

    /// <summary>Zaman Durdu: butun dusmanlar sureyi donar (dugum islemi kapanir; sure bitince TickM4 acar).</summary>
    void FreezeAll()
    {
        foreach (var z in Zipzip.All.ToArray()) if (!z.Dead && z.ProcessMode != Node.ProcessModeEnum.Disabled) { z.ProcessMode = Node.ProcessModeEnum.Disabled; _frozen.Add(z); }
        foreach (var k in Kabarans.Live.ToArray()) if (k.Node.ProcessMode != Node.ProcessModeEnum.Disabled) { k.Node.ProcessMode = Node.ProcessModeEnum.Disabled; _frozen.Add(k.Node); }
        Sfx.Play("levelup", D.Player.GlobalPosition, -2f);
    }

    void TickM4(float dt)
    {
        var p = D.Player;
        if (_balT > 0)
        {
            _balT -= dt;
            var def = Def("zaman_bali");
            foreach (var n in Foes(_balPos, def.Radius))
            {
                if (n is Zipzip z) z.FreezeT = Mathf.Max(z.FreezeT, 0.4f); // yavas hareket (Zipzip dondurma kolu)
            }
        }
        if (_wellT > 0)
        {
            _wellT -= dt;
            var def = Def("cekim_kuyusu");
            foreach (var n in Foes(_wellPos, def.Radius))
            {
                var (x, z) = AbilityRules.PullToward(def, _wellPos.X, _wellPos.Z, n.GlobalPosition.X, n.GlobalPosition.Z, dt);
                n.GlobalPosition = D.World.Ground(new Vector3(x, 0, z)) with { Y = n.GlobalPosition.Y };
            }
        }
        if (_rocketUp)
        {
            _rocketGrace -= dt;
            if (_rocketGrace <= 0 && p.IsOnFloor())
            {
                _rocketUp = false;
                var def = Def("roket_ziplama");
                Fx.Burst(p.GlobalPosition + Vector3.Up * 0.3f, Palette.Warning, 40, def.Radius * 2f, 0.12f, 0.5f, true, 0f);
                foreach (var n in Foes(p.GlobalPosition, def.Radius)) HitFoe(n, def.Damage, p.GlobalPosition, def.Knock);
                Sfx.Play("hit", p.GlobalPosition, 0f);
            }
        }
        if (_ringT > 0)
        {
            _ringT -= dt; _ringTick -= dt;
            if (_ringTick <= 0)
            {
                _ringTick = 0.5f;
                var def = Def("alev_cemberi");
                _ringPos = p.GlobalPosition; // halka oyuncuyla gider
                foreach (var n in Foes(_ringPos, def.Radius))
                {
                    if (!AbilityRules.InRing(_ringPos.X, _ringPos.Z, def.Inner, def.Radius, n.GlobalPosition.X, n.GlobalPosition.Z)) continue;
                    HitFoe(n, def.Damage * 0.5f, _ringPos, 0f);
                    if (n is Zipzip z) z.BurnT = Mathf.Max(z.BurnT, def.Burn);
                }
                Fx.Burst(_ringPos + Vector3.Up * 0.2f, Palette.Warning, 10, def.Radius, 0.1f, 0.4f, true, 0f);
            }
        }
        bool markEnded = _markPrev > 0 && L.MarkLeft <= 0;
        _markPrev = L.MarkLeft;
        if (markEnded) // sure doldu: isarete don
        {
            var to = D.World.Ground(new Vector3(L.MarkX, 0, L.MarkZ));
            Fx.Burst(p.GlobalPosition + Vector3.Up, Palette.MayaTeal, 20, 4f, 0.1f, 0.4f, true, 0f);
            p.GlobalPosition = to + Vector3.Up * 0.2f; p.Velocity = Vector3.Zero;
            Fx.Burst(to + Vector3.Up, Palette.MayaTeal, 20, 4f, 0.1f, 0.4f, true, 0f);
            Sfx.Play("pickup", to, -2f);
        }
        for (int i = _walls.Count - 1; i >= 0; i--)
        {
            var (w, t) = _walls[i];
            t -= dt;
            if (!IsInstanceValid(w)) { _walls.RemoveAt(i); continue; }
            foreach (var n in Foes(w.GlobalPosition, 6f)) // duvarin icine giren dusman disari itilir
            {
                var local = w.ToLocal(n.GlobalPosition);
                if (Mathf.Abs(local.X) < Def("buz_duvari").Length * 0.5f + 0.3f && Mathf.Abs(local.Z) < 1.1f)
                    n.GlobalPosition += w.GlobalBasis.Z * Mathf.Sign(local.Z == 0 ? 1 : local.Z) * 6f * dt;
            }
            if (t <= 0) { Fx.Burst(w.GlobalPosition, new Color("BFE6F5"), 24, 4f, 0.1f, 0.5f, true, 0f); w.QueueFree(); _walls.RemoveAt(i); }
            else _walls[i] = (w, t);
        }
        if (_rainT > 0)
        {
            _rainT -= dt; _rainTick -= dt;
            if (_rainTick <= 0)
            {
                _rainTick = 0.5f;
                var def = Def("yorunge_sulamasi");
                float per = def.Damage / (def.Duration / 0.5f);
                foreach (var n in Foes(_rainPos, def.Radius)) HitFoe(n, per, _rainPos, def.Knock);
                foreach (var pl in D.State.Farm.Plots) // tarlayi sular
                {
                    var c = D.FarmView.CellCenter(pl.X, pl.Y);
                    if (AbilityRules.InRadius(_rainPos.X, _rainPos.Z, def.Radius, c.X, c.Z)) D.State.Farm.Water(pl.X, pl.Y);
                }
                Fx.Burst(_rainPos + Vector3.Up * 6f, new Color("8CC8E8"), 30, def.Radius, 0.1f, 0.8f, true, 3f);
            }
        }
        if (_frozen.Count > 0 && L.TimeStopLeft <= 0)
        {
            foreach (var n in _frozen) if (IsInstanceValid(n)) n.ProcessMode = Node.ProcessModeEnum.Inherit;
            _frozen.Clear();
        }
    }
}