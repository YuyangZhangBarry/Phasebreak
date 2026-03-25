using System;
using UnityEngine;

/// <summary>
/// Player reward container:
/// - When player interacts (by trigger proximity), show 3 random upgrade cards.
/// - After opening, the chest disables itself to prevent multiple opens.
/// - Dimension sync: same rules as <see cref="Portal"/> — 2D = high plane; 3D = ray down to ground.
/// </summary>
public class Chest : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("If true, the chest opens automatically when the player enters the trigger.")]
    [SerializeField] private bool autoOpenOnTriggerEnter = true;

    [Tooltip("If true, disables the chest after it opens.")]
    [SerializeField] private bool disableAfterOpen = true;

    [Header("Dimension sync (match Portal / ModeSwitcher)")]
    [Tooltip("If true, chest Y follows 2D plane vs 3D ground when the player switches dimensions.")]
    [SerializeField] private bool syncPositionWithDimension = true;

    [Tooltip("Y height in 2D top-down mode (same as Portal planeY2D).")]
    [SerializeField] private float planeY2D = 40f;

    [Tooltip("Ray origin height above XZ when snapping to ground in 3D.")]
    [SerializeField] private float raycastStartHeight = 50f;

    [Tooltip("Max ray length downward.")]
    [SerializeField] private float raycastMaxDistance = 100f;

    [Tooltip("Y offset above ground hit (pivot above floor; tune so BoxCollider bottom sits on tile).")]
    [SerializeField] private float groundYOffset = 0.5f;

    [Tooltip("Layers that count as ground. If Nothing (0), uses default raycast layers.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    private Collider[] myColliders;
    private ModeSwitcher playerModeSwitcher;
    private bool lastWas2D = true;

    private bool opened = false;

    private void Awake()
    {
        myColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        if (!syncPositionWithDimension)
            return;

        ResolvePlayerModeSwitcher();
        if (playerModeSwitcher != null)
        {
            lastWas2D = playerModeSwitcher.is2DMode;
            ApplyDimensionPlacement();
        }
    }

    private void LateUpdate()
    {
        if (!syncPositionWithDimension || opened)
            return;

        if (playerModeSwitcher == null)
            ResolvePlayerModeSwitcher();

        if (playerModeSwitcher == null)
            return;

        bool now2D = playerModeSwitcher.is2DMode;
        if (now2D != lastWas2D)
        {
            ApplyDimensionPlacement();
            lastWas2D = now2D;
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

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        playerModeSwitcher = player.GetComponent<ModeSwitcher>();
        if (playerModeSwitcher == null)
            playerModeSwitcher = player.GetComponentInChildren<ModeSwitcher>();
    }

    private void ApplyDimensionPlacement()
    {
        Vector3 p = transform.position;

        if (playerModeSwitcher.is2DMode)
        {
            transform.position = new Vector3(p.x, planeY2D, p.z);
            return;
        }

        ApplyDimensionPlacement3D();
    }

    /// <summary>Same pattern as <see cref="Portal.ApplyDimensionPlacement3DOnly"/> + skip player / self.</summary>
    private void ApplyDimensionPlacement3D()
    {
        Vector3 p = transform.position;
        Vector3 rayStart = new Vector3(p.x, raycastStartHeight, p.z);

        LayerMask mask = groundLayers.value == 0 ? (LayerMask)Physics.DefaultRaycastLayers : groundLayers;

        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            Vector3.down,
            raycastMaxDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (IsRayHitIgnoredForGroundSnap(h))
                continue;

            transform.position = new Vector3(p.x, h.point.y + groundYOffset, p.z);
            return;
        }
    }

    private bool IsRayHitIgnoredForGroundSnap(RaycastHit h)
    {
        if (h.collider == null)
            return true;

        if (myColliders != null)
        {
            for (int i = 0; i < myColliders.Length; i++)
            {
                if (myColliders[i] != null && myColliders[i] == h.collider)
                    return true;
            }
        }

        if (h.collider.CompareTag("Player"))
            return true;

        return h.collider.GetComponentInParent<PlayerStatsManager>() != null;
    }

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!autoOpenOnTriggerEnter)
            return;

        if (opened)
            return;

        if (!other.CompareTag("Player"))
            return;

        opened = true;

        Debug.Log($"[Chest] Player entered chest trigger: '{name}'. Showing upgrade UI.", this);

        if (RogueliteUIManager.Instance != null)
            RogueliteUIManager.Instance.ShowRandomUpgrades();
        else
            Debug.LogWarning("[Chest] RogueliteUIManager.Instance is missing. Cannot show upgrades.", this);

        if (disableAfterOpen)
            gameObject.SetActive(false);
    }
}
