using UnityEngine;

public class PlayerHealth : MonoBehaviour, ICombatTarget, IHealthSource
{
    public string DisplayName => "小幽";
    public int CurrentHealth => RunSession.Current?.CurrentHealth ?? 0;
    public int MaximumHealth => RunSession.Current?.MaxHealth ?? 5;
    public Transform TargetTransform => transform;
    public bool CanBeTargeted => CurrentHealth > 0 && isActiveAndEnabled;
    public event System.Action HealthChanged;
    public CombatTeam Team => CombatTeam.Player;
    private RunSession session;
    private void OnEnable()
    {
        session = RunSession.Current;
        if (session != null)
            session.Changed += Refresh;
        CombatTargets.Register(this);
    }
    private void Refresh() => HealthChanged?.Invoke();
    private void OnDisable()
    {
        if (session != null)
            session.Changed -= Refresh;
        CombatTargets.Unregister(this);
    }
    public bool TryApplyDamage(DamageInfo damage)
    {
        if (damage.SourceTeam == Team || GameplayPause.IsPaused || SceneLoader.IsLoading)
            return false;
        if (RunSession.Current == null)
            return false;
        bool shield = RunSession.Current.ShieldCharges > 0;
        bool applied = RunSession.Current.TryTakeDamage(damage.Amount);
        if (applied)
        {
            PartyAudio.Play(shield ? PartySound.Shield : PartySound.Hurt, transform.position);
            FoxEscortQuest.Current?.CancelRescue();
        }
        return applied;
    }
}
