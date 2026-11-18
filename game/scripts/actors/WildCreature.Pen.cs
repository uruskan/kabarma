using Godot;

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
        float walk = Def.Speed * (Def.Wild?.WalkSpeedMult ?? 0.5f);
        clip = speed > walk * 1.3f ? "run" : speed > 0.01f ? "walk" : "idle";
    }
}
