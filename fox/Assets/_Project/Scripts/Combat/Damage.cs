using UnityEngine;

public enum CombatTeam { Player, Enemy }

public readonly struct DamageInfo
{
    public readonly int Amount;
    public readonly CombatTeam SourceTeam;
    public readonly GameObject Source;
    public DamageInfo(int amount, CombatTeam team, GameObject source)
    {
        Amount = amount;
        SourceTeam = team;
        Source = source;
    }
}

public interface IDamageable
{
    CombatTeam Team { get; }
    bool TryApplyDamage(DamageInfo damage);
}
