using UnityEngine;

/// <summary>
/// Place on a child GameObject with Tag <c>WeakPoint</c> and a <b>Trigger</b> collider.
/// 3D melee overlap must use triggers — handled in <see cref="PlayerController"/>.
/// </summary>
public class WeakPointHitReceiver : MonoBehaviour
{
    [SerializeField] private Earthshaker boss;

    [Tooltip("Min downward speed (Y) to count as falling attack.")]
    [SerializeField] private float minFallSpeed = -0.4f;

    [Tooltip("If player feet are this many meters above boss root Y, counts as 'attack from above' without falling.")]
    [SerializeField] private float heightAboveBossForPlunge = 1.2f;

    [Tooltip("Execution requires downward speed lower than this (strict drop-kill gate).")]
    [SerializeField] private float executeMinFallSpeed = -1.0f;

    private void Awake()
    {
        if (boss == null)
            boss = GetComponentInParent<Earthshaker>();
    }

    /// <summary>PlayerController calls this after OverlapBox hits this collider with triggers enabled.</summary>
    public bool TryProcessWeakPointFrom3DMelee(PlayerController player, Rigidbody playerRb, ModeSwitcher modeSwitcher)
    {
        if (boss == null || player == null || playerRb == null || modeSwitcher == null)
            return false;

        if (modeSwitcher.is2DMode)
            return false;

        // Strict drop-kill: always require meaningful downward velocity.
        if (playerRb.linearVelocity.y >= executeMinFallSpeed)
            return false;

        // Even with enough downward velocity, keep original airborne context checks.
        if (!EvaluateAirborneCondition(player.transform, playerRb))
            return false;

        boss.ExecuteWeakPointInstantKill();
        return true;
    }

    private bool EvaluateAirborneCondition(Transform player, Rigidbody playerRb)
    {
        Transform root = boss != null ? boss.transform : transform.root;
        bool falling = playerRb.linearVelocity.y < minFallSpeed;

        float playerFeetY = player.position.y;
        Collider pc = player.GetComponent<Collider>();
        if (pc != null)
            playerFeetY = pc.bounds.min.y;

        bool highAbove = playerFeetY > root.position.y + heightAboveBossForPlunge;

        return falling || highAbove;
    }
}
