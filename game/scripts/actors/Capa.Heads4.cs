using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// kusam (M4): Capa silah basliklari #6-#12 (ICERIK 6.2): Alev Puskurteci, Havan, Ark, Tohum Makinalisi, Ayaz Lazeri, Yercekimi,
/// Maya Topu. Sayilar weapons.json "weaponHeads", kural HeadRules (Sim). Dusman: Zipzip.All + Kabarans (Kabarans.Hit). Cipler
/// ve kartlar bu basliklara henuz baglanmadi (chips.json weaponHeads kimlikleri var: Capa.ChipHeadId). Efektler vfx.json head.<baslik>.*.
/// </summary>
public partial class Capa
{
    public static string Head4Id(Head h) => h switch
    {
        Head.Alev => "alev_puskurtecu", Head.Havan => "havan", Head.Ark => "ark", Head.Tohum => "tohum_makinalisi",
        Head.Ayaz => "ayaz_lazeri", Head.Yercekimi => "yercekimi", Head.MayaTopu => "maya_topu", _ => "",
    };

    public static bool IsHead4(Head h) => h >= Head.Alev;

    HeadDef Def4(Head h) => _t.Head4(Head4Id(h)) ?? new HeadDef { Id = Head4Id(h) };

    /// <summary>Yeni baslik cantada (tarifinin urunu esya) ya da acildiysa kullanilabilir.</summary>
    bool Head4Available(Head h) { var id = Head4Id(h); return _d.State.Capa.IsUnlocked(id) || _d.State.Backpack.Count(id) > 0; }

    float _hold4, _tick4, _ammoAcc;
    Node3D _grab4;

    /// <summary>Cephane: gece sinirsiz; gunduz cantadan. Kesirli harcama birikir.</summary>
    bool TakeAmmo4(HeadDef h, float amount)
    {
        var item = HeadRules.AmmoItem(_d.Data, h, _d.State.Farm.Selected);
        if (string.IsNullOrEmpty(item) || _d.NightActive || amount <= 0) return true;
        _ammoAcc += amount;
        int need = (int)_ammoAcc;
        if (need <= 0) return true;
        if (!_d.State.Backpack.Remove(item, need)) { _ammoAcc = 0; _d.Hud.Hint(Loc.T("capa.item_none", ItemNames.Of(item)), 1.2f); return false; }
        _ammoAcc -= need;
        return true;
    }

    List<Node3D> Foes4(Vector3 at, float radius)
    {
        var l = new List<Node3D>();
        foreach (var z in Zipzip.All.ToArray()) if (!z.Dead && z.GlobalPosition.DistanceTo(at) <= radius + 0.6f) l.Add(z);
        foreach (var k in Kabarans.Near(at, radius)) if (!l.Contains(k.Node)) l.Add(k.Node);
        return l;
    }

    void Hit4(Node3D n, float dmg, Vector3 from, float knock)
    {
        var dir = ((n.GlobalPosition - from) with { Y = 0 }).Normalized() * knock;
        if (n is Zipzip z) z.Hit(dmg, false, dir, dir.Length(), false);
        else { var k = Kabarans.Live.FirstOrDefault(x => x.Node == n); if (k != null) Kabarans.Hit(k, dmg, from, knock); }
    }

    static float SegDist(Vector3 a, Vector3 b, Vector3 p)
    {
        var ab = b - a; float t = ab.LengthSquared() < 0.0001f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return (a + ab * t).DistanceTo(p);
    }

    void Zap4(Vector3 a, Vector3 b, Color c, float life = 0.12f)
    {
        var d = b - a; float len = d.Length(); if (len < 0.05f) return;
        var mi = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = len, RadialSegments = 5, Rings = 1 }, TopLevel = true,
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = c } };
        _d.AddChild(mi);
        mi.GlobalPosition = a + d * 0.5f;
        var up = d / len; var side = Mathf.Abs(up.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up.Cross(up).Normalized();
        mi.GlobalBasis = new Basis(side, up, side.Cross(up));
        GetTree().CreateTimer(life).Timeout += () => { if (IsInstanceValid(mi)) mi.QueueFree(); };
    }

    /// <summary>Yeni basliklarin kare isi. Donus: isin gosterildi mi.</summary>
    bool TickHead4(Head head, float dt, bool fire, bool firePressed, Vector3 aim, Vector3 muzzle)
    {
        var def = Def4(head);
        var dir = (aim - muzzle).Normalized();
        var flat = (dir with { Y = 0 }).Normalized();
        _tick4 -= dt;
        bool beaming = false;
        switch (def.Kind)
        {
            case "cone": // Alev Puskurteci: kisa konide surekli ates, yanik birakir
                if (fire && _tick4 <= 0f && TakeAmmo4(def, def.AmmoPerSecond * def.FireInterval))
                {
                    _tick4 = def.FireInterval; _reacted = true; ShotsFired++;
                    foreach (var n in Foes4(muzzle + flat * def.Range * 0.5f, def.Range * 0.6f))
                    {
                        float r = n is Zipzip zz ? 0.6f * zz.Scale0 : 0.6f;
                        if (!HeadRules.InCone(muzzle.X, muzzle.Z, flat.X, flat.Z, def.ConeDegrees, def.Range, n.GlobalPosition.X, n.GlobalPosition.Z, r)) continue;
                        Hit4(n, def.Damage, muzzle, 0.5f);
                        if (n is Zipzip z) z.BurnT = Mathf.Max(z.BurnT, def.BurnSeconds);
                    }
                    Vfx.Head("alev", "muzzle", muzzle + flat * 1.2f, Palette.Warning, flat);
                    Fx.Burst(muzzle + flat * (def.Range * 0.5f), Palette.Warning, 5, 3f, 0.14f, 0.35f, true, 0.5f);
                }
                beaming = fire; break;
            case "mortar": // Havan: basili tut = menzil; birak = mermi
            case "orb":    // Maya Topu: basili tut = yuklen; birak = kure
                if (fire) { _hold4 += dt; beaming = true; var land = HoldTarget(def, muzzle, flat); Vfx.Ring(land, def.Splash > 0 ? def.Splash : 3f, 0.08f, def.Kind == "orb" ? Palette.MayaViolet : Palette.Warning, 0.1f); }
                else if (_hold4 > 0f)
                {
                    float held = _hold4; _hold4 = 0f;
                    if (_fireCd <= 0f && TakeAmmo4(def, def.AmmoPerShot)) { _fireCd = def.FireInterval; _reacted = true; ShotsFired++; Launch4(def, muzzle, HoldTarget(def, muzzle, flat, held)); }
                }
                break;
            case "chain": // Ark: ilk hedefe, oradan en yakin hedeflere sicrar
                if (fire && _tick4 <= 0f)
                {
                    _tick4 = def.FireInterval; _reacted = true; ShotsFired++;
                    var near = Foes4(muzzle + flat * def.Range * 0.5f, def.Range * 0.6f)
                        .Where(n => HeadRules.InCone(muzzle.X, muzzle.Z, flat.X, flat.Z, 40f, def.Range, n.GlobalPosition.X, n.GlobalPosition.Z, 0.6f))
                        .OrderBy(n => n.GlobalPosition.DistanceTo(muzzle)).ToList();
                    if (near.Count > 0)
                    {
                        var rest = Foes4(near[0].GlobalPosition, def.ChainRange * (def.ChainCount + 1)).Where(n => n != near[0]).ToList();
                        var order = HeadRules.ChainOrder(near[0].GlobalPosition.X, near[0].GlobalPosition.Z, rest.Select(n => (n.GlobalPosition.X, n.GlobalPosition.Z)).ToList(), def.ChainCount - 1, def.ChainRange);
                        var chain = new List<Node3D> { near[0] }; chain.AddRange(order.Select(i => rest[i]));
                        var prev = muzzle;
                        for (int i = 0; i < chain.Count; i++)
                        {
                            var p = chain[i].GlobalPosition + Vector3.Up * 0.8f;
                            Zap4(prev, p, new Color("BFE6F5"));
                            Hit4(chain[i], HeadRules.ChainDamage(def, i), prev, 1f);
                            Fx.Burst(p, new Color("BFE6F5"), 6, 3f, 0.08f, 0.25f, true, 0f);
                            prev = p;
                        }
                    }
                    else Zap4(muzzle, muzzle + flat * 3f, new Color("BFE6F5"), 0.06f);
                    Vfx.Head("ark", "muzzle", muzzle, new Color("BFE6F5"), flat);
                }
                break;
            case "seed": // Tohum Makinalisi: hizli, tohumun turu etkiyi belirler
                if (fire && _tick4 <= 0f && TakeAmmo4(def, def.AmmoPerShot))
                {
                    _tick4 = def.FireInterval; _reacted = true; ShotsFired++;
                    var fx = HeadRules.SeedEffect(def, _d.State.Farm.Selected);
                    var end = muzzle + dir * def.Range;
                    var hits = Foes4(muzzle + dir * def.Range * 0.5f, def.Range * 0.55f).Where(n => SegDist(muzzle, end, n.GlobalPosition + Vector3.Up * 0.6f) <= 0.9f)
                        .OrderBy(n => n.GlobalPosition.DistanceTo(muzzle)).Take(1 + fx.Pierce).ToList();
                    foreach (var n in hits)
                    {
                        Hit4(n, def.Damage * fx.DamageMult, muzzle, 1f);
                        if (n is Zipzip z) { if (fx.Burn > 0) z.BurnT = Mathf.Max(z.BurnT, fx.Burn); if (fx.Slow > 0) z.FreezeT = Mathf.Max(z.FreezeT, fx.SlowSeconds); }
                    }
                    Zap4(muzzle, hits.Count > 0 ? hits[^1].GlobalPosition + Vector3.Up * 0.6f : muzzle + dir * 12f, new Color("D9C089"), 0.05f);
                    Vfx.Head("tohum", "muzzle", muzzle, new Color("D9C089"), flat);
                }
                break;
            case "beam": // Ayaz Lazeri: surekli isin, dondurur
                if (fire)
                {
                    var end = muzzle + dir * def.Range;
                    ShowBeam(muzzle, end, new Color("BFE6F5"), 0.8f); beaming = true; _reacted = true;
                    if (_tick4 <= 0f)
                    {
                        _tick4 = def.FireInterval;
                        foreach (var n in Foes4(muzzle + dir * def.Range * 0.5f, def.Range * 0.55f).Where(n => SegDist(muzzle, end, n.GlobalPosition + Vector3.Up * 0.6f) <= 0.9f))
                        {
                            Hit4(n, def.Damage, muzzle, 0f);
                            if (n is Zipzip z) z.FreezeT = Mathf.Max(z.FreezeT, def.FreezeSeconds);
                        }
                    }
                }
                break;
            case "gravity": // Yercekimi: tut, tasi, birak = firlat (Cekim'in silah hali)
                if (_grab4 != null && !IsInstanceValid(_grab4)) _grab4 = null;
                if (fire && _grab4 == null && firePressed)
                {
                    var best = Foes4(aim, def.GrabRadius).Where(n => n.GlobalPosition.DistanceTo(muzzle) <= def.Range).OrderBy(n => n.GlobalPosition.DistanceTo(aim)).FirstOrDefault();
                    if (best != null) { _grab4 = best; _reacted = true; Sfx.Play("pickup", muzzle, -4f); }
                }
                if (_grab4 != null)
                {
                    if (fire)
                    {
                        var to = muzzle + dir * def.HoldDistance + Vector3.Up * 0.4f;
                        _grab4.GlobalPosition = _grab4.GlobalPosition.Lerp(to, Mathf.Clamp(dt * 12f, 0f, 1f));
                        ShowBeam(muzzle, _grab4.GlobalPosition, Palette.MayaViolet, 0.6f); beaming = true;
                    }
                    else
                    {
                        var node = _grab4; _grab4 = null;
                        var from = muzzle;
                        Hit4(node, def.Damage, from, def.ThrowSpeed * 0.4f);
                        if (node is Zipzip z) z.Push(flat, def.ThrowSpeed);
                        else if (IsInstanceValid(node)) node.GlobalPosition += flat * 6f;
                        FeelThrow4(muzzle, flat);
                    }
                }
                break;
        }
        return beaming;
    }

    void FeelThrow4(Vector3 at, Vector3 dir) { Sfx.Play("hit", at, -2f); Vfx.Head("yercekimi", "muzzle", at, Palette.MayaViolet, dir); }

    Vector3 HoldTarget(HeadDef def, Vector3 muzzle, Vector3 flat, float? held = null)
    {
        float dist = HeadRules.MortarDistance(def, held ?? _hold4);
        var p = muzzle + flat * dist;
        return _d.World.Ground(p);
    }

    /// <summary>Havan mermisi ve Maya Topu: kavisle ucar, inince alan hasari (Maya Topu hedefleri once sisirir).</summary>
    void Launch4(HeadDef def, Vector3 from, Vector3 to)
    {
        float flight = HeadRules.MortarFlight(def, from.DistanceTo(to));
        var shell = new MeshInstance3D { Mesh = new SphereMesh { Radius = def.Kind == "orb" ? 0.35f : 0.22f, Height = def.Kind == "orb" ? 0.7f : 0.44f, RadialSegments = 8, Rings = 4 }, TopLevel = true,
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = def.Kind == "orb" ? Palette.MayaViolet : new Color("3A3A3A") } };
        _d.AddChild(shell);
        shell.GlobalPosition = from;
        float arc = def.Kind == "orb" ? 1.5f : Mathf.Max(3f, from.DistanceTo(to) * 0.35f);
        var tw = shell.CreateTween();
        tw.TweenMethod(Callable.From<float>(t => { if (IsInstanceValid(shell)) shell.GlobalPosition = from.Lerp(to, t) + Vector3.Up * (arc * 4f * t * (1f - t)); }), 0f, 1f, flight);
        tw.TweenCallback(Callable.From(() => { if (IsInstanceValid(shell)) shell.QueueFree(); Impact4(def, to); }));
        Vfx.Head(Head4Id(def.Kind == "orb" ? Head.MayaTopu : Head.Havan) == "maya_topu" ? "maya_topu" : "havan", "muzzle", from, def.Kind == "orb" ? Palette.MayaViolet : Palette.Warning, ((to - from) with { Y = 0 }).Normalized());
        Sfx.Play("hit", from, -3f);
    }

    void Impact4(HeadDef def, Vector3 at)
    {
        var col = def.Kind == "orb" ? Palette.MayaViolet : Palette.Warning;
        Fx.Burst(at + Vector3.Up * 0.4f, col, 36, def.Splash * 1.6f, 0.14f, 0.5f, true, 0f);
        Vfx.Ring(at + Vector3.Up * 0.1f, def.Splash, 0.4f, col, 0.3f);
        Sfx.Play("hit", at, 0f);
        var foes = Foes4(at, def.Splash);
        if (def.Kind == "orb")
        {
            foreach (var n in foes) if (n is Node3D t) t.CreateTween().TweenProperty(t, "scale", t.Scale * 1.4f, def.SwellSeconds); // sisirir
            GetTree().CreateTimer(def.SwellSeconds).Timeout += () =>
            {
                foreach (var n in foes)
                {
                    if (!IsInstanceValid(n)) continue;
                    Hit4(n, def.Damage, at, 3f);
                    if (IsInstanceValid(n)) n.Scale /= 1.4f;
                    Fx.Burst(n.GlobalPosition + Vector3.Up * 0.6f, col, 14, 3f, 0.1f, 0.4f, true, 0f);
                }
            };
        }
        else foreach (var n in foes) Hit4(n, def.Damage, at, 6f);
    }
}