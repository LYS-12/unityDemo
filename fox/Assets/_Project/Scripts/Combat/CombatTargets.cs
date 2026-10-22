using System.Collections.Generic;
using UnityEngine;

public interface ICombatTarget : IDamageable
{
    Transform TargetTransform
    {
        get;
    }
    bool CanBeTargeted
    {
        get;
    }
}

public static class CombatTargets
{
    private static readonly HashSet<ICombatTarget> targets = new HashSet<ICombatTarget>();
    public static System.Collections.Generic.IReadOnlyCollection<ICombatTarget> Registered => targets;
    public static void Register(ICombatTarget target) => targets.Add(target);
    public static void Unregister(ICombatTarget target) => targets.Remove(target);
    public static ICombatTarget Nearest(Vector3 origin, float range)
    {
        ICombatTarget closest = null;
        float squared = range * range;
        foreach (ICombatTarget target in targets)
        {
            if (target is Object obj && obj == null || !target.CanBeTargeted)
                continue;
            float distance = (target.TargetTransform.position - origin).sqrMagnitude;
            if (distance < squared)
            {
                squared = distance;
                closest = target;
            }
        }
        return closest;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => targets.Clear();
}
