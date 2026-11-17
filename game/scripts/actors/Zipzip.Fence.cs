using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Zipzip'in cit hucumu (GDD 14.3: hedefi ekin, yoldaki yapiya yuklenir; GDD 24: ilk gecede yapi cani %10'un
/// altina inmez). Cit dusmani durdurur: sicrama duvarda biter, inince parca hasar alir (PenSystem.DamageWall).
/// Itme (tekme, vurus) duvara takilmaz: Gidak cit ustunden tekmeleyebilir. PenSystem bunu Zipzip'in kendi
/// _Process'inden sonra cagirir; hareket ve gorunus Zipzip.cs'te (yaratik akisi).
/// </summary>
public partial class Zipzip
{
    float _fenceLastHopT;
    Vector3 _fencePrev;
    bool _fencePrevSet;
    PenWall _fenceWall;
    bool _fenceArmed;
    float _fenceCd;

    /// <summary>Bu Zipzip su an bir cite yukleniyor (sinama ve bot icin).</summary>
    public bool AtFence => _fenceWall != null && _fenceArmed;
    public int FenceCharges { get; private set; }

    internal void FenceTick(float dt, PenSystem ps)
    {
        if (Dead || Display) return;
        _fenceCd -= dt;
        // Yeni sicrama (zamanlayici yeniden kuruldu; citte duran Zipzip ayni noktadan yeniden sicrar):
        // yolu bir duvari kesiyorsa sicrama duvarin onunde biter. Bu karede yapilmis hareket de geri alinir.
        bool newHop = _hopT > 0 && (_fenceLastHopT <= 0 || _hopT > _fenceLastHopT);
        _fenceLastHopT = _hopT;
        // Yedek: itme olmadan bu karede bir duvari kestiyse duvarin onunde kalir (itme, tekme cit ustunden atabilir).
        if (_fencePrevSet && !newHop && _knock.LengthSquared() < 0.25f && ps.FirstHit(_fencePrev, GlobalPosition, Radius * 0.85f) is WallHit back)
        {
            GlobalPosition = _dir.World.Ground(new Vector3(back.Point.X, 0, back.Point.Z));
            _hopTo = GlobalPosition;
        }
        if (newHop)
        {
            _fenceArmed = false;
            if (ps.FirstHit(_hopFrom, _hopTo, Radius * 0.85f) is WallHit h)
            {
                _hopTo = _dir.World.Ground(new Vector3(h.Point.X, 0, h.Point.Z));
                GlobalPosition = _hopFrom;
                _fenceWall = h.Wall;
                _fenceArmed = true;
            }
        }
        _fencePrev = GlobalPosition;
        _fencePrevSet = true;
        // Indi: cite yuklendi.
        if (_fenceArmed && _hopT <= 0)
        {
            _fenceArmed = false;
            if (_fenceCd <= 0)
            {
                _fenceCd = ps.Def.FenceHitCooldown;
                var w = _fenceWall;
                var p = new P2(GlobalPosition.X, GlobalPosition.Z);
                var ab = w.B - w.A;
                float t = Mathf.Clamp(P2.Dot(p - w.A, ab) / Mathf.Max(1e-4f, P2.Dot(ab, ab)), 0f, 1f);
                var on = w.A + ab * t;
                var at = new Vector3(on.X, GlobalPosition.Y + 0.6f, on.Z);
                ps.DamageWall(w, Def.Damage * _damageMult, at);
                FenceCharges++;
                _squash = 1f;
                _flash = Mathf.Max(_flash, 0.3f);
            }
        }
    }
}
