using System.Collections;
using UnityEngine;

/// <summary>
/// Basic zombie: chases the player on the XZ plane. Pivot at feet: 2D Y = ModeSwitcher plane, 3D Y = 0.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Health))] // Requires a Health component.
public class Zombie : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("If null, finds a GameObject tagged Player at runtime")]
    [SerializeField] private Transform playerRoot;

    [Tooltip("If null, taken from playerRoot")]
    [SerializeField] private ModeSwitcher playerModeSwitcher;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.25f;

    [Tooltip("Stop horizontal movement when this close on the XZ plane")]
    [SerializeField] private float stopDistance = 0.4f;

    [Header("Contact damage (player)")]
    [Tooltip(
        "Max planar (XZ) distance between collider surfaces for damage. Center-to-center distance is often too small for overlapping capsules — this uses ClosestPoint. Default ~1.2 works for two ~1m-tall capsules.")]
    [SerializeField] private float contactDamageRange = 0.45f;

    [Tooltip("Damage applied to the player Health when in range.")]
    [SerializeField] private float contactDamage = 5f;

    [Tooltip("Seconds between damage ticks while the player stays inside the DamageHitbox trigger.")]
    public float damageTickRate = 0.5f;

    [Tooltip("Minimum seconds between damage ticks.")]
    [SerializeField] private float contactDamageCooldown = 0.75f;

    [Tooltip("Log when contact damage is applied (Console).")]
    [SerializeField] private bool logContactDamage = true;

    [Tooltip("One-time warning if player has no Health when in contact range.")]
    [SerializeField] private bool warnIfPlayerMissingHealth = true;

    [Header("Dimension sync")]
    [Tooltip("Fallback if ModeSwitcher missing.")]
    [SerializeField] private float planeY2D = 40f;

    [Tooltip("3D floor world Y when switching to 3D (pivot at feet).")]
    [SerializeField] private float planeY3D = 0f;

    private Rigidbody rb;
    private Health myHealth; // Health component on this zombie.
    
    private bool dimensionSyncReady;
    private bool lastPlayerWas2D = true;
    private float nextDamageTime;
    private bool warnedMissingPlayerHealth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints |= RigidbodyConstraints.FreezeRotation;
        
        myHealth = GetComponent<Health>(); // Get this zombie's Health component.
    }

    // Added: lifecycle event subscription
    private void OnEnable()
    {
        if (myHealth != null)
        {
            myHealth.OnDie += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (myHealth != null)
        {
            myHealth.OnDie -= HandleDeath;
        }
    }

    // Added: death handling logic
    private void HandleDeath()
    {
        Debug.Log($"[Zombie] '{gameObject.name}' died and was destroyed!");
        
        // If you add death effects or coin-drop logic later, put it here.
        // Instantiate(deathVFX, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    private IEnumerator Start()
    {
        yield return null;

        ResolvePlayerReferences();

        if (playerModeSwitcher == null)
        {
            Debug.LogWarning(
                $"{nameof(Zombie)} on '{name}': No player found. Set Tag 'Player' on the player, or assign Player Root + ModeSwitcher in Inspector.",
                this);
            yield break;
        }

        lastPlayerWas2D = playerModeSwitcher.is2DMode;
        ApplyDimensionPlacement();
        dimensionSyncReady = true;
    }

    /// <summary>
    /// Tag "Player" first; if missing, finds any <see cref="PlayerController"/> in the scene (beginner-friendly).
    /// </summary>
    private void ResolvePlayerReferences()
    {
        if (PlayerStatsManager.Instance != null)
        {
            Transform real = PlayerStatsManager.Instance.transform;
            if (real != null)
            {
                playerRoot = real;
                playerModeSwitcher = playerRoot.GetComponent<ModeSwitcher>();
                if (playerModeSwitcher == null)
                    playerModeSwitcher = playerRoot.GetComponentInChildren<ModeSwitcher>();
                return;
            }
        }

        if (playerRoot != null)
        {
            if (playerModeSwitcher == null)
                playerModeSwitcher = playerRoot.GetComponent<ModeSwitcher>();
            return;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            playerRoot = tagged.transform;
            playerModeSwitcher = playerRoot.GetComponent<ModeSwitcher>();
            return;
        }

        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            playerRoot = pc.transform;
            playerModeSwitcher = pc.GetComponent<ModeSwitcher>();
            Debug.LogWarning(
                $"{nameof(Zombie)} on '{name}': No GameObject with tag 'Player'. Using PlayerController on '{pc.name}'. Add Tag Player to avoid this.",
                this);
        }
    }

    private void LateUpdate()
    {
        // Persistent run player lives on DDOL root; rebind if scene had a duplicate or stale Inspector ref.
        if (PlayerStatsManager.Instance != null)
        {
            Transform real = PlayerStatsManager.Instance.transform;
            if (real != null && playerRoot != real)
            {
                playerRoot = real;
                playerModeSwitcher = playerRoot.GetComponent<ModeSwitcher>();
                if (playerModeSwitcher == null)
                    playerModeSwitcher = playerRoot.GetComponentInChildren<ModeSwitcher>();

                if (playerModeSwitcher != null)
                {
                    lastPlayerWas2D = playerModeSwitcher.is2DMode;
                    ApplyDimensionPlacement();
                    dimensionSyncReady = true;
                }
            }
        }

        if (!dimensionSyncReady || playerModeSwitcher == null)
            return;

        bool now2D = playerModeSwitcher.is2DMode;
        if (now2D != lastPlayerWas2D)
        {
            ApplyDimensionPlacement();
            lastPlayerWas2D = now2D;
        }
        else if (now2D)
        {
            float planeY = playerModeSwitcher.PlaneY2D;
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y - planeY) > 0.001f)
            {
                pos.y = planeY;
                transform.position = pos;
            }
        }
    }

    /// <summary>2D: Y = combat plane. 3D: Y = floor plane.</summary>
    private void ApplyDimensionPlacement()
    {
        Vector3 p = transform.position;
        float y2DWorld = playerModeSwitcher != null ? playerModeSwitcher.PlaneY2D : planeY2D;

        if (playerModeSwitcher.is2DMode)
        {
            transform.position = new Vector3(p.x, y2DWorld, p.z);
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.useGravity = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
        else
        {
            transform.position = new Vector3(p.x, planeY3D, p.z);
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
        }
    }

    private void FixedUpdate()
    {
        if (!dimensionSyncReady || playerRoot == null || playerModeSwitcher == null)
            return;

        Vector3 delta = playerRoot.position - transform.position;
        delta.y = 0f;

        float dist = delta.magnitude;

        if (dist <= stopDistance)
        {
            if (playerModeSwitcher.is2DMode)
                rb.linearVelocity = Vector3.zero;
            else
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Vector3 dir = delta / dist;

        if (playerModeSwitcher.is2DMode)
            rb.linearVelocity = new Vector3(dir.x * moveSpeed, 0f, dir.z * moveSpeed);
        else
        {
            float vy = rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(dir.x * moveSpeed, vy, dir.z * moveSpeed);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (Time.time < nextDamageTime)
            return;

        Health playerHealth = other.GetComponentInParent<Health>();
        if (playerHealth == null)
            playerHealth = other.GetComponent<Health>();

        if (playerHealth != null && playerHealth.IsAlive)
        {
            playerHealth.TakeDamage(contactDamage);
            nextDamageTime = Time.time + Mathf.Max(0.001f, damageTickRate);

            if (logContactDamage)
                Debug.Log($"[Zombie] '{name}' trigger tick hit player.", this);
        }
        else if (warnIfPlayerMissingHealth && !warnedMissingPlayerHealth)
        {
            warnedMissingPlayerHealth = true;
            Debug.LogWarning(
                $"{nameof(Zombie)} '{name}': player has no Health component on this trigger — add Health to the player root (or ensure it's on a parent).",
                this);
        }
    }
}