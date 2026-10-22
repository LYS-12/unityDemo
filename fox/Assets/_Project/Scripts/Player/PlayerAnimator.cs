using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int IsWalkingParameter = Animator.StringToHash("isWalking");
    [SerializeField] private Player player;
    private Animator animator;

    private void Awake() => animator = GetComponent<Animator>();
    private void Update()
    {
        if (player != null && !StoryCinematic.IsPlaying)
            animator.SetBool(IsWalkingParameter, player.IsWalking && !GameplayPause.IsPaused);
    }
}
