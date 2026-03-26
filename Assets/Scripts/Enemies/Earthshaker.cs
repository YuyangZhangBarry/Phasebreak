using System.Collections;
using UnityEngine;

/// <summary>
/// Fixed elite: faces player on Y, periodic slam + shockwave. Weak point on child with <see cref="WeakPointHitReceiver"/>.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
public class Earthshaker : MonoBehaviour
{
    [Header("Dimension sync (pivot at feet)")]
    [Tooltip("2D plane when no player ModeSwitcher.")]
    public float planeY2D = 40f;

    [Tooltip("If false, boss Y is never changed by mode.")]
    [SerializeField] private bool syncHeightWithPlayerDimension = true;

    /// <summary>Designer 3D placement Y; restored when returning to 3D.</summary>
    private float startY3D;

    private ModeSwitcher playerModeSwitcher;
    private bool lastPlayerWas2D = true;

    [Header("Targeting")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("XZ distance to start facing player.")]
    [SerializeField] private float aggroRadius = 10f;

    [Tooltip("Max yaw degrees per second when tracking the player.")]
    [SerializeField] private float turnSpeedDegrees = 90f;

    [Header("Attack cadence")]
    [Tooltip("Seconds between the start of one slam cycle and the next (includes windup).")]
    [SerializeField] private float attackCycleInterval = 10f;

    [Tooltip("Windup before slam: no turning, red flash.")]
    [SerializeField] private float windupDuration = 1.5f;

    [Header("Slam — melee")]
    [SerializeField] private float slamRadius = 3f;
    [SerializeField] private float slamDamageToPlayer = 20f;

    [Header("Slam — shockwave prefab")]
    [SerializeField] private Shockwave shockwavePrefab;
    [Tooltip("Added on top of spawn point (XZ usually; Y for extra lift).")]
    [SerializeField] private Vector3 shockwaveSpawnOffset = Vector3.zero;

    [Tooltip("Lift above collider bottom to reduce Z-fighting with floor.")]
    [SerializeField] private float shockwaveGroundClearance = 0.05f;

    [Header("Windup visual (optional)")]
    [SerializeField] private Renderer[] tintRenderers;
    [SerializeField] private Color windupTint = Color.red;
    private Color[] originalColors;

    private Health health;
    private Rigidbody rb;
    private Transform player;

    private bool windupActive;
    private Coroutine attackRoutine;

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        health.OnDie += HandleDie;

        if (tintRenderers != null && tintRenderers.Length > 0)
        {
            originalColors = new Color[tintRenderers.Length];
            for (int i = 0; i < tintRenderers.Length; i++)
            {
                if (tintRenderers[i] != null)
                    originalColors[i] = tintRenderers[i].sharedMaterial != null
                        ? tintRenderers[i].sharedMaterial.color
                        : Color.white;
            }
        }

        startY3D = transform.position.y;
    }

    private static bool IsFiniteFloat(float v) => !(float.IsNaN(v) || float.IsInfinity(v));

    private void Start()
    {
        if (syncHeightWithPlayerDimension)
        {
            ResolvePlayerModeSwitcher();
            if (playerModeSwitcher != null)
            {
                lastPlayerWas2D = playerModeSwitcher.is2DMode;
                ApplyDimensionHeight();
            }
        }

        attackRoutine = StartCoroutine(AttackLoop());
    }

    private void LateUpdate()
    {
        if (!syncHeightWithPlayerDimension || !health.IsAlive)
            return;

        if (playerModeSwitcher == null)
            ResolvePlayerModeSwitcher();

        if (playerModeSwitcher == null)
            return;

        bool now2D = playerModeSwitcher.is2DMode;
        if (now2D != lastPlayerWas2D)
        {
            ApplyDimensionHeight();
            lastPlayerWas2D = now2D;
        }
        else if (now2D)
        {
            float planeY = playerModeSwitcher != null ? playerModeSwitcher.PlaneY2D : planeY2D;
            if (!IsFiniteFloat(planeY))
                planeY = planeY2D;

            Vector3 p = transform.position;
            if (!IsFiniteFloat(p.x) || !IsFiniteFloat(p.y) || !IsFiniteFloat(p.z))
                return;

            if (Mathf.Abs(p.y - planeY) > 0.001f)
            {
                p.y = planeY;
                transform.position = p;
            }
        }
    }

    private void ResolvePlayerModeSwitcher()
    {
        if (PlayerStatsManager.Instance != null)
        {
            ModeSwitcher ms = PlayerStatsManager.Instance.GetComponent<ModeSwitcher>();
            if (ms == null)
                ms = PlayerStatsManager.Instance.GetComponentInChildren<ModeSwitcher>();
            if (ms != null)
            {
                playerModeSwitcher = ms;
                return;
            }
        }

        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null)
            return;

        playerModeSwitcher = go.GetComponent<ModeSwitcher>();
        if (playerModeSwitcher == null)
            playerModeSwitcher = go.GetComponentInChildren<ModeSwitcher>();
    }

    /// <summary>2D: Y = combat plane. 3D: restore saved <see cref="startY3D"/>.</summary>
    private void ApplyDimensionHeight()
    {
        Vector3 p = transform.position;

        if (!IsFiniteFloat(p.x) || !IsFiniteFloat(p.y) || !IsFiniteFloat(p.z))
            return;

        if (playerModeSwitcher.is2DMode)
        {
            float py = playerModeSwitcher != null ? playerModeSwitcher.PlaneY2D : planeY2D;
            if (!IsFiniteFloat(py))
                py = planeY2D;
            p.y = py;
        }
        else if (IsFiniteFloat(startY3D))
        {
            p.y = startY3D;
        }

        if (IsFiniteFloat(p.x) && IsFiniteFloat(p.y) && IsFiniteFloat(p.z))
            transform.position = p;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDie -= HandleDie;
    }

    private void HandleDie()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!health.IsAlive)
            return;

        ResolvePlayer();
        if (player == null || windupActive)
            return;

        float distSq = HorizontalDistSq(transform.position, player.position);
        if (distSq > aggroRadius * aggroRadius)
            return;

        FacePlayerYaw();
    }

    private static float HorizontalDistSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null)
            player = go.transform;
    }

    private void FacePlayerYaw()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            target,
            turnSpeedDegrees * Time.deltaTime);
    }

    private IEnumerator AttackLoop()
    {
        var wait = new WaitForSeconds(0.1f);
        while (health.IsAlive)
        {
            ResolvePlayer();
            if (player == null || HorizontalDistSq(transform.position, player.position) > aggroRadius * aggroRadius)
            {
                yield return wait;
                continue;
            }

            windupActive = true;
            yield return WindupVisualRoutine();
            PerformSlam();
            windupActive = false;

            float waitAfter = Mathf.Max(0.1f, attackCycleInterval - windupDuration);
            yield return new WaitForSeconds(waitAfter);
        }
    }

    private IEnumerator WindupVisualRoutine()
    {
        SetWindupTint(true);
        yield return new WaitForSeconds(windupDuration);
        SetWindupTint(false);
    }

    private void SetWindupTint(bool on)
    {
        if (tintRenderers == null || tintRenderers.Length == 0)
            return;

        for (int i = 0; i < tintRenderers.Length; i++)
        {
            if (tintRenderers[i] == null)
                continue;

            Material m = tintRenderers[i].material;
            if (m.HasProperty("_Color"))
            {
                m.color = on ? windupTint : originalColors[i];
            }
            else if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", on ? windupTint : originalColors[i]);
            }
        }
    }

    private void PerformSlam()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            slamRadius,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (Collider c in hits)
        {
            if (c == null || !c.CompareTag(playerTag))
                continue;

            Health ph = c.GetComponentInParent<Health>();
            if (ph == null)
                ph = c.GetComponent<Health>();
            if (ph != null && ph.IsAlive)
                ph.TakeDamage(slamDamageToPlayer);
        }

        if (shockwavePrefab != null)
        {
            if (playerModeSwitcher == null)
                ResolvePlayerModeSwitcher();

            float floorY = transform.position.y + shockwaveGroundClearance;

            Vector3 spawnPos = new Vector3(
                transform.position.x + shockwaveSpawnOffset.x,
                floorY + shockwaveSpawnOffset.y,
                transform.position.z + shockwaveSpawnOffset.z);

            float planeY = playerModeSwitcher != null ? playerModeSwitcher.PlaneY2D : planeY2D;
            bool is2D = playerModeSwitcher != null && playerModeSwitcher.is2DMode;
            float y3D = startY3D + shockwaveGroundClearance;
            float y2D = planeY + shockwaveGroundClearance;

            Shockwave wave = Instantiate(shockwavePrefab, spawnPos, shockwavePrefab.transform.rotation);
            wave.gameObject.SetActive(true);
            wave.InitializeHeights(y3D, y2D, is2D);
        }
    }

    /// <summary>Called by <see cref="WeakPointHitReceiver"/> after valid aerial melee.</summary>
    public void ExecuteWeakPointInstantKill()
    {
        if (!health.IsAlive)
            return;

        health.TakeDamage(health.MaxHealth + 1000f, true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
    }
#endif
}
