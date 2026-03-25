using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Expanding cylinder trigger: only damages in a hollow ring band on XZ (not the full disk).
/// Each <see cref="Health"/> can only be damaged once per shockwave instance.
/// 2D/3D world Y is injected by the spawner via <see cref="InitializeHeights"/> — no raycasts.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Shockwave : MonoBehaviour
{
    [Header("Dimension height (spawner-injected)")]
    [Tooltip("If false, InitializeHeights does not subscribe to ModeSwitcher (manual FX only).")]
    [SerializeField] private bool syncHeightWithPlayerDimension = true;

    [SerializeField] private string modeSwitcherResolveTag = "Player";

    [Header("Damage")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private string playerTag = "Player";

    [Header("Hollow ring (XZ)")]
    [Tooltip("Damage only when horizontal distance from shockwave center is in [R - ringThickness, R]. " +
             "If dist < R - ringThickness, player is in the inner safe zone (skipped the edge).")]
    public float ringThickness = 1f;

    [Tooltip("Unity default Cylinder mesh has radius 0.5 in local space; world radius ≈ multiplier × lossyScale.x. Change if you use a custom mesh.")]
    [SerializeField] private float cylinderRadiusMultiplier = 0.5f;

    [Header("Expand")]
    [Tooltip("Per second multiplier on localScale (XZ). Y unchanged.")]
    [SerializeField] private float expandSpeed = 8f;

    [Tooltip("Stop expanding when max scale on XZ reached.")]
    [SerializeField] private float maxScaleXZ = 40f;

    [Tooltip("Extra clearance above wave top to count as 'jumped over' in 3D.")]
    [SerializeField] private float jumpOverClearance = 0.15f;

    /// <summary>Each player (Health) at most once per wave instance.</summary>
    private readonly HashSet<Health> damagedHealths = new HashSet<Health>();

    private Collider col;

    /// <summary>World Y for this FX when the game is in 3D (from spawner).</summary>
    private float trueY3D;

    /// <summary>World Y for this FX when the game is in 2D (from spawner).</summary>
    private float trueY2D;

    private bool heightsInitialized;

    private ModeSwitcher modeSwitcher;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnDestroy()
    {
        UnsubscribeDimension();
    }

    /// <summary>
    /// Call immediately after <see cref="Object.Instantiate"/> from the spawner (e.g. Earthshaker).
    /// </summary>
    /// <param name="groundY3D">Target pivot Y in 3D for this arena (e.g. boss platform floor + clearance).</param>
    /// <param name="skyY2D">Target pivot Y in 2D for this arena (e.g. combat plane + clearance).</param>
    /// <param name="isCurrently2D">Current <see cref="ModeSwitcher.is2DMode"/> at spawn time.</param>
    public void InitializeHeights(float groundY3D, float skyY2D, bool isCurrently2D)
    {
        trueY3D = groundY3D;
        trueY2D = skyY2D;
        heightsInitialized = true;

        if (syncHeightWithPlayerDimension)
        {
            ResolveModeSwitcher();
            SubscribeDimension();
        }

        float targetY = isCurrently2D ? trueY2D : trueY3D;
        Vector3 p = transform.position;
        p.y = targetY;
        transform.position = p;
    }

    private void SubscribeDimension()
    {
        UnsubscribeDimension();

        if (modeSwitcher == null)
            ResolveModeSwitcher();

        if (modeSwitcher != null)
            modeSwitcher.OnDimensionChanged += HandleDimensionChanged;
    }

    private void UnsubscribeDimension()
    {
        if (modeSwitcher != null)
            modeSwitcher.OnDimensionChanged -= HandleDimensionChanged;
    }

    private void HandleDimensionChanged(bool is2DMode)
    {
        if (!heightsInitialized || !syncHeightWithPlayerDimension)
            return;

        Vector3 p = transform.position;
        p.y = is2DMode ? trueY2D : trueY3D;
        transform.position = p;
    }

    private void ResolveModeSwitcher()
    {
        if (PlayerStatsManager.Instance != null)
        {
            ModeSwitcher ms = PlayerStatsManager.Instance.GetComponent<ModeSwitcher>();
            if (ms == null)
                ms = PlayerStatsManager.Instance.GetComponentInChildren<ModeSwitcher>();
            if (ms != null)
            {
                modeSwitcher = ms;
                return;
            }
        }

        GameObject go = GameObject.FindGameObjectWithTag(modeSwitcherResolveTag);
        if (go == null)
            return;

        modeSwitcher = go.GetComponent<ModeSwitcher>();
        if (modeSwitcher == null)
            modeSwitcher = go.GetComponentInChildren<ModeSwitcher>();
    }

    private void Update()
    {
        float s = transform.localScale.x + expandSpeed * Time.deltaTime;
        s = Mathf.Min(s, maxScaleXZ);
        float y = transform.localScale.y;
        transform.localScale = new Vector3(s, y, s);

        if (s >= maxScaleXZ - 0.01f)
            Destroy(gameObject, 0.05f);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        if (other == null || !other.CompareTag(playerTag))
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health == null)
            health = other.GetComponent<Health>();
        if (health == null || !health.IsAlive)
            return;

        if (damagedHealths.Contains(health))
            return;

        float currentRadius = GetCylinderOuterRadiusWorld();
        float innerR = Mathf.Max(0f, currentRadius - ringThickness);

        float dist = HorizontalDistanceFromCenter(other);

        // Inside the inner safe disk: no hit, do not lock out future ring contact.
        if (dist < innerR)
            return;

        // Outer bound (allow tiny epsilon for float / capsule straddle).
        if (dist > currentRadius + 0.05f)
            return;

        ModeSwitcher ms = other.GetComponentInParent<ModeSwitcher>();
        if (ms == null)
            ms = other.GetComponent<ModeSwitcher>();

        if (!PassesHeightCheck(other, ms))
            return;

        health.TakeDamage(damage);
        damagedHealths.Add(health);
    }

    /// <summary>Horizontal distance from this transform's XZ to the player's position (XZ).</summary>
    private float HorizontalDistanceFromCenter(Collider playerCol)
    {
        Vector3 delta = playerCol.transform.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private float GetCylinderOuterRadiusWorld()
    {
        float sx = transform.lossyScale.x;
        float sz = transform.lossyScale.z;
        float uniform = Mathf.Max(sx, sz);
        return cylinderRadiusMultiplier * uniform;
    }

    private bool PassesHeightCheck(Collider playerCol, ModeSwitcher ms)
    {
        if (ms != null && ms.is2DMode)
            return true;

        Bounds b = col.bounds;
        float playerLowY = playerCol.bounds.min.y;
        if (playerLowY > b.max.y + jumpOverClearance)
            return false;

        return true;
    }
}
