using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Evcil hal (hayvan akisi, GDD 9): WildCreature'in TameSteer kancasi. HusbandryAgent (game/scripts/husbandry) yaratiga
/// bilesen olarak takilir ve IHusbandryHost ile surer: driven = true iken yabani beyin o kare calismaz, yaratik hedefe
/// yurur; false iken kendi yabani davranisina doner (evcillesmemis hayvan). State.Tamed'i bilesen tutar.
/// </summary>
public partial class WildCreature : IHusbandryHost
{
    Vector3 _hGoal;
    float _hSpeed;
    bool _hDriven;
    /// <summary>Agil duvarlari (RanchSystem atar; yabani yaratik bilmez).</summary>
    public PenSystem HusbandryPens;
    /// <summary>Duvar kirpma yaricapi (bilesenin Radius'u; kapi acikligindan gecebilsin).</summary>
    public float HusbandryRadius = 0.3f;

    public string SpeciesId => Def.Id;
    public float HpFraction => Def.Hp > 0 ? Mathf.Clamp(State.Hp / Def.Hp, 0f, 1f) : 1f;

    public void HusbandrySteer(Vector3 goal, float speed, bool driven)
    {
        _hGoal = goal; _hSpeed = speed; _hDriven = driven;
    }

    partial void TameSteer(float dt, ref Vector3 goal, ref float speed, ref string clip, ref bool handled)
    {
        if (!_hDriven) return;
        handled = true;
        goal = _hGoal with { Y = GlobalPosition.Y };
        speed = _hSpeed;
        // Agil duvari: bu karenin adimi gecilmez duvari keserse duvarin onunde durur (acik kapi gecilir).
        var pos = GlobalPosition;
        var d = goal - pos; d.Y = 0;
        if (d.Length() > 0.01f)
        {
            var next = pos + d.Normalized() * Mathf.Min(d.Length(), speed * dt + 0.05f);
            if (HusbandryPens?.FirstHit(pos, next, HusbandryRadius) is WallHit h)
                goal = new Vector3(h.Point.X, pos.Y, h.Point.Z);
        }
        float walk = Def.Speed * (Def.Wild?.WalkSpeedMult ?? 0.5f);
        clip = speed > walk * 1.3f ? "run" : speed > 0.01f ? "walk" : "idle";
    }
}
