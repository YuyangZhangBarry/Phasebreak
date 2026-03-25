using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Next level portal: loads scene on player touch.
/// Dimension sync: mirrors player ModeSwitcher — 2D = fixed high plane; 3D = ray down to ground.
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("Portal Target")]
    [Tooltip("Scene name to load when the player touches this portal.")]
    public string nextSceneName = "";

    [Tooltip("If enabled, portal triggers only once per spawn.")]
    [SerializeField] private bool triggerOnce = true;

    [Header("Dimension sync (match player ModeSwitcher)")]
    [Tooltip("Y height when the game is in 2D top-down mode.")]
    [SerializeField] private float planeY2D = 40f;

    [Tooltip("Ray origin height above XZ when projecting to ground in 3D mode.")]
    [SerializeField] private float raycastStartHeight = 50f;

    [Tooltip("Max ray length downward.")]
    [SerializeField] private float raycastMaxDistance = 100f;

    [Tooltip("Y offset above ground hit point. E.g. portal total height 2 with pivot at center → use 1 so the base sits on the ground.")]
    [SerializeField] private float groundYOffset = 1.6f;

    /// <summary>Lets spawners (e.g. RogueliteUIManager) override prefab value if the asset was missing serialized fields.</summary>
    public float GroundYOffset
    {
        get => groundYOffset;
        set => groundYOffset = value;
    }

    [Tooltip("Optional: only these layers count as ground. If 0, uses all layers.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    private Collider myCollider;
    private ModeSwitcher playerModeSwitcher;
    private bool lastWas2D = true;

    private bool triggered = false;

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        ResolvePlayerModeSwitcher();
        if (playerModeSwitcher != null)
        {
            lastWas2D = playerModeSwitcher.is2DMode;
            ApplyDimensionPlacement();
        }
    }

    private void LateUpdate()
    {
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

        ApplyDimensionPlacement3DOnly();
    }

    private void ApplyDimensionPlacement3DOnly()
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

        // Prefer closest hit along the ray that is not this portal's collider.
        float bestDist = float.MaxValue;
        RaycastHit best = default;
        bool found = false;

        foreach (RaycastHit h in hits)
        {
            if (h.collider != null && myCollider != null && h.collider == myCollider)
                continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }

        if (found)
            transform.position = new Vector3(p.x, best.point.y + groundYOffset, p.z);
    }

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (triggerOnce && triggered)
            return;

        triggered = true;

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            VictoryMenuController.ShowVictory();
            return;
        }

        // So next scene spawn Y matches 2D plane (40) vs 3D ground raycast on PlayerStatsManager.
        ModeSwitcher ms = other.GetComponentInParent<ModeSwitcher>();
        bool enteredAs2D = ms == null || ms.is2DMode;
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.RegisterPortalExitDimension(enteredAs2D);

        SceneManager.LoadScene(nextSceneName);
    }
}
