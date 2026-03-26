using UnityEngine;

/// <summary>
/// Floating eye movement with dimension-aware Y bobbing and XZ retreat.
/// Upgraded: compatible with elite enemy ability lock (isEliteSweeping).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Health))]
public class FloatingEyeMovement : MonoBehaviour
{
    [Header("Target")]
    public Transform player;
    public bool isAttacking = false;

    [Header("Distance (XZ plane only)")]
    public float sniperDistance = 10f;

    [Header("Movement")]
    public float retreatSpeed = 1.5f;
    public float yawTurnSpeedDegrees = 180f;
    public float heightTransitionSpeed = 10f;

    [Header("Heights & bob")]
    public float height3D = 16f;
    public float height2D = 40f;
    public float bobAmplitude = 0.5f;
    public float bobFrequency = 1f;

    // Elite ability lock flag
    [HideInInspector] public bool isEliteSweeping = false;

    private Rigidbody _rb;
    private Health _health;
    private ModeSwitcher _playerModeSwitcher;
    private float _smoothedBaseY;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _health = GetComponent<Health>();
        _rb.useGravity = false;
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnDie += HandleDie;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnDie -= HandleDie;
    }

    private void HandleDie()
    {
        Destroy(gameObject);
    }

    private void Start()
    {
        _smoothedBaseY = transform.position.y;
        ResolvePlayer();
        ResolvePlayerModeSwitcher();
    }

    private void FixedUpdate()
    {
        if (_health != null && !_health.IsAlive)
            return;

        if (player == null)
            ResolvePlayer();
        if (_playerModeSwitcher == null)
            ResolvePlayerModeSwitcher();

        bool is2D = _playerModeSwitcher != null && _playerModeSwitcher.is2DMode;
        float targetBaseHeight = is2D ? height2D : height3D;

        float t = Mathf.Clamp01(heightTransitionSpeed * Time.fixedDeltaTime);
        _smoothedBaseY = Mathf.Lerp(_smoothedBaseY, targetBaseHeight, t);

        float bob = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        float finalY = _smoothedBaseY + bob;

        Vector3 horizontalVelocity = Vector3.zero;
        if (player != null)
        {
            Vector3 myXZ = new Vector3(_rb.position.x, 0f, _rb.position.z);
            Vector3 playerXZ = new Vector3(player.position.x, 0f, player.position.z);
            Vector3 delta = playerXZ - myXZ;
            float dist = delta.magnitude;

            // Core change: during elite ability, freeze XZ position (don't roam).
            if (isAttacking || isEliteSweeping)
            {
                horizontalVelocity = Vector3.zero;
            }
            else if (dist > sniperDistance)
            {
                horizontalVelocity = Vector3.zero;
            }
            else
            {
                if (dist > 0.0001f)
                {
                    Vector3 dir = delta / dist;
                    horizontalVelocity = -dir * retreatSpeed;
                }
                else
                {
                    horizontalVelocity = Vector3.zero;
                }
            }
        }

        Vector3 newPos = _rb.position + horizontalVelocity * Time.fixedDeltaTime;
        newPos.y = finalY;
        _rb.MovePosition(newPos);

        // Core change: during elite ability, disable smooth yaw and let the elite script force rotation.
        if (player != null && !isEliteSweeping)
        {
            Vector3 toPlayer = player.position - _rb.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                float angle = Quaternion.Angle(_rb.rotation, targetYaw);
                if (angle > 0.001f)
                {
                    float stepDegrees = yawTurnSpeedDegrees * Time.fixedDeltaTime;
                    float slerpT = Mathf.Clamp01(stepDegrees / angle);
                    Quaternion smoothed = Quaternion.Slerp(_rb.rotation, targetYaw, slerpT);
                    _rb.MoveRotation(smoothed);
                }
                else
                {
                    _rb.MoveRotation(targetYaw);
                }
            }
        }
    }

    private void ResolvePlayer()
    {
        if (player != null) return;

        if (PlayerStatsManager.Instance != null)
        {
            player = PlayerStatsManager.Instance.transform;
            return;
        }

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
    }

    private void ResolvePlayerModeSwitcher()
    {
        if (PlayerStatsManager.Instance != null)
        {
            ModeSwitcher ms = PlayerStatsManager.Instance.GetComponent<ModeSwitcher>();
            if (ms == null) ms = PlayerStatsManager.Instance.GetComponentInChildren<ModeSwitcher>();
            if (ms != null)
            {
                _playerModeSwitcher = ms;
                return;
            }
        }

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;

        _playerModeSwitcher = go.GetComponent<ModeSwitcher>();
        if (_playerModeSwitcher == null) _playerModeSwitcher = go.GetComponentInChildren<ModeSwitcher>();
    }
}