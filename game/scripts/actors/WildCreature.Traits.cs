using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// M4 yaratiklarinin (yaratik4, ICERIK §3.3-3.6) yabani ozellikleri, WildDef'in M4 alanlariyla surulur:
///  - Projectile: menzilli yirtici (Tukurgen asit, Kizgin alev, Kivrik zehir, Cizbiz sok, Vrak dil) vurusunu gorunur kilar;
///    hasar Wildlife.Report'ta (yakin vurusla ayni yol, AttackRange uzak).
///  - Burrow: Dehliz hucuma gecene kadar toprak altinda, ustunde tumsek.
///  - BellySlide: Paytak kosarken karninin ustune yatar.
///  - SplitInto: Hamurcuk olunce kucuk parcalara bolunur (kusak WildState.Gen'de).
/// Gorunus yer tutucudur (koddan govde, bodies.json); Tripo modelleri gelince ayni kalir.
/// </summary>
public partial class WildCreature
{
    /// <summary>Bolunen parcanin boy carpani (kusak basina 0.7).</summary>
    public float SizeMult { get; set; } = 1f;
    float _burrowY, _tilt, _sinceStrike = 99f;
    MeshInstance3D _mound;

    float TraitY(float dt, string clip, bool moving)
    {
        var w = Def.Wild;
        if (w == null) return 0f;
        _sinceStrike += dt;
        // Karin ustu kayma: kosarken govde one yatar (kus planinin kosu klibi altta oynar).
        if (w.BellySlide)
        {
            float want = moving && clip == "run" ? Mathf.DegToRad(68f) : 0f;
            _tilt = Mathf.Lerp(_tilt, want, Mathf.Min(1f, dt * 6f));
            _visual.Rotation = new Vector3(_tilt, 0, 0);
        }
        if (!w.Burrow) return 0f;
        // Pusu: hucum (kosu), vurus ya da yara alinca cikar; yoksa gomulur.
        bool up = clip == "run" || _sinceStrike < 2.5f || State.Hp < Def.Hp || State.Swollen;
        float h = (Def.Rig?.Height ?? 0.5f) * 1.15f;
        _burrowY = Mathf.Lerp(_burrowY, up ? 0f : -h, Mathf.Min(1f, dt * (up ? 6f : 1.5f)));
        if (_mound == null)
        {
            _mound = new MeshInstance3D
            {
                Name = "Mound",
                Mesh = new SphereMesh { Radius = 0.45f, Height = 0.35f, RadialSegments = 10, Rings = 5 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.3f, 0.2f), Roughness = 1f },
            };
            AddChild(_mound);
        }
        float k = Mathf.Clamp(-_burrowY / h, 0f, 1f);
        _mound.Visible = k > 0.2f;
        _mound.Scale = new Vector3(1f, 0.4f + 0.6f * k, 1f) * (Def.Rig?.Height ?? 0.5f) * 1.6f;
        return _burrowY;
    }

    void TraitEvent(WildEvent e)
    {
        if (e != WildEvent.Struck) return;
        _sinceStrike = 0f;
        var kind = Def.Wild?.Projectile;
        if (string.IsNullOrEmpty(kind) || GetParent() == null) return;
        var from = GlobalPosition + Vector3.Up * (Def.Rig?.Height ?? 0.6f) * _scale * 0.8f;
        var to = _w.PlayerPos + Vector3.Up * 0.9f;
        var color = kind switch
        {
            "acid" => new Color(0.6f, 0.95f, 0.2f),
            "fire" => new Color(1f, 0.45f, 0.1f),
            "poison" => new Color(0.6f, 0.25f, 0.85f),
            "shock" => new Color(0.55f, 0.85f, 1f),
            _ => new Color(0.95f, 0.45f, 0.55f), // dil
        };
        var mat = new StandardMaterial3D { AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 2f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var len = from.DistanceTo(to);
        MeshInstance3D shot;
        if (kind is "tongue" or "shock")
        {
            // Dil ve sok: kaynaktan hedefe uzanan cizgi, kisa surede soner.
            shot = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = kind == "tongue" ? 0.05f : 0.025f, BottomRadius = kind == "tongue" ? 0.07f : 0.025f, Height = len, RadialSegments = 6 }, MaterialOverride = mat };
            GetParent().AddChild(shot);
            shot.GlobalPosition = from.Lerp(to, 0.5f);
            if (len > 0.01f) shot.LookAt(to, Vector3.Up);
            shot.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);
            var tw = shot.CreateTween();
            tw.TweenProperty(shot, "scale", new Vector3(1, 0.05f, 1), 0.25f);
            tw.TweenCallback(Callable.From(shot.QueueFree));
        }
        else
        {
            // Asit, alev, zehir: yay cizen top, varinca patlar.
            shot = new MeshInstance3D { Mesh = new SphereMesh { Radius = kind == "fire" ? 0.22f : 0.14f, Height = kind == "fire" ? 0.44f : 0.28f, RadialSegments = 8, Rings = 4 }, MaterialOverride = mat };
            GetParent().AddChild(shot);
            shot.GlobalPosition = from;
            var tw = shot.CreateTween();
            float secs = Mathf.Clamp(len / 14f, 0.15f, 0.6f);
            tw.TweenMethod(Callable.From<float>(u =>
            {
                if (!IsInstanceValid(shot)) return;
                shot.GlobalPosition = from.Lerp(to, u) + Vector3.Up * Mathf.Sin(u * Mathf.Pi) * len * 0.25f;
            }), 0f, 1f, secs);
            tw.TweenCallback(Callable.From(() => { Fx.Burst(to, color, 10, 3f, 0.08f, 0.4f, true, -2f); shot.QueueFree(); }));
        }
    }

    /// <summary>Wildlife.Strike oldurunce (QueueFree'den once): bolunen tur parcalara ayrilir (ertelenmis; liste dolasilirken).</summary>
    public void OnDowned()
    {
        var w = Def.Wild;
        if (w == null || w.SplitInto <= 0 || State.Gen >= w.SplitGenerations || GetParent() is not Wildlife life) return;
        var at = GlobalPosition;
        int gen = State.Gen + 1;
        float hp = Def.Hp * Mathf.Pow(w.SplitHpMult, gen);
        float size = SizeMult * 0.7f;
        bool swollen = State.Swollen;
        Callable.From(() =>
        {
            for (int i = 0; i < w.SplitInto; i++)
            {
                float a = Mathf.Tau * i / w.SplitInto;
                foreach (var n in life.Spawn(Def, at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.7f))
                    if (n is WildCreature c)
                    {
                        c.State.Gen = gen; c.State.Hp = hp; c.SizeMult = size;
                        if (swollen) c.SnapSwell(true);
                        c.Startle();
                    }
            }
            Fx.Burst(at + Vector3.Up * 0.3f, Palette.MayaViolet, 12, 3f, 0.1f, 0.5f, true, -2f);
        }).CallDeferred();
    }
}
