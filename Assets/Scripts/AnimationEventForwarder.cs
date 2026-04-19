using UnityEngine;

/// <summary>
/// 挂载在带有 Animator 的子物体（如 Y Bot）上。
/// 接收动画事件并转发给父物体上的 PlayerController。
/// </summary>
public class AnimationEventForwarder : MonoBehaviour
{
    private PlayerController playerController;
    private Animator animator;

    private void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
        animator = GetComponentInParent<Animator>();
        if (playerController == null)
            Debug.LogError($"[AnimationEventForwarder] 未在父层级找到 PlayerController: {gameObject.name}");
    }

    /// <summary>
    /// 与 Animator 里 <c>EnhancedAttack</c> 状态名一致（该状态与 Attack3 共用同一 FBX 片段时，事件仍是本函数名，据此分支强化伤害）。
    /// </summary>
    private static readonly int EnhancedAttackStateHash = Animator.StringToHash("EnhancedAttack");

    private bool IsPlayingEnhancedAttack()
    {
        if (animator == null)
            return false;
        const int layer = 0;
        AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(layer);
        if (cur.shortNameHash == EnhancedAttackStateHash)
            return true;
        if (animator.IsInTransition(layer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
            if (next.shortNameHash == EnhancedAttackStateHash)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 由 3D 攻击动画的 Animation Event 调用。
    /// 在每段攻击动画的"命中帧"处添加此事件。
    /// </summary>
    public void TriggerMeleeDamage()
    {
        if (playerController == null)
            return;
        if (IsPlayingEnhancedAttack())
            playerController.Perform3DMeleeEnhancedAttack();
        else
            playerController.Perform3DMeleeAttack();
    }

    /// <summary>
    /// 由强化攻击（EnhancedAttack）动画的命中帧调用；伤害见 PlayerController.melee3DEnhancedDamage。
    /// </summary>
    public void TriggerMeleeDamageEnhanced()
    {
        if (playerController != null)
            playerController.Perform3DMeleeEnhancedAttack();
    }
}
