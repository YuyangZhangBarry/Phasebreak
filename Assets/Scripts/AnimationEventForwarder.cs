using UnityEngine;

/// <summary>
/// 挂载在带有 Animator 的子物体（如 Y Bot）上。
/// 接收动画事件并转发给父物体上的 PlayerController。
/// </summary>
public class AnimationEventForwarder : MonoBehaviour
{
    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
        if (playerController == null)
            Debug.LogError($"[AnimationEventForwarder] 未在父层级找到 PlayerController: {gameObject.name}");
    }

    /// <summary>
    /// 由 3D 攻击动画的 Animation Event 调用。
    /// 在每段攻击动画的"命中帧"处添加此事件。
    /// </summary>
    public void TriggerMeleeDamage()
    {
        if (playerController != null)
            playerController.Perform3DMeleeAttack();
    }
}
