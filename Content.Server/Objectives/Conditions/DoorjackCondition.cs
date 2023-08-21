using Content.Server.Mind;
using Content.Server.Objectives.Interfaces;
using Content.Server.Roles;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Objectives.Conditions;

/// <summary>
/// Objective condition that requires the player to be a ninja and have doorjacked at least a random number of airlocks.
/// </summary>
[DataDefinition]
public sealed class DoorjackCondition : IObjectiveCondition
{
    private Mind.Mind? _mind;
    private int _target;

    public IObjectiveCondition GetAssigned(Mind.Mind mind)
    {
        // TODO: clamp to number of doors on station incase its somehow a shittle or something
        return new DoorjackCondition {
            _mind = mind,
            _target = IoCManager.Resolve<IRobustRandom>().Next(15, 40)
        };
    }

    public string Title => Loc.GetString("objective-condition-doorjack-title", ("count", _target));

    public string Description => Loc.GetString("objective-condition-doorjack-description", ("count", _target));

    public SpriteSpecifier Icon => new SpriteSpecifier.Rsi(new ResPath("Objects/Tools/emag.rsi"), "icon");

    public float Progress
    {
        get
        {
            // prevent divide-by-zero
            if (_target == 0)
                return 1f;

            var entMan = IoCManager.Resolve<IEntityManager>();
            var mindSystem = entMan.System<MindSystem>();
            if (!mindSystem.TryGetRole<NinjaRole>(_mind, out var role))
                return 0f;

            if (role.DoorsJacked >= _target)
                return 1f;

            return (float) role.DoorsJacked / (float) _target;
        }
    }

    public float Difficulty => 1.5f;

    public bool Equals(IObjectiveCondition? other)
    {
        return other is DoorjackCondition cond && Equals(_mind, cond._mind) && _target == cond._target;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is DoorjackCondition cond && cond.Equals(this);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_mind?.GetHashCode() ?? 0, _target);
    }
}
