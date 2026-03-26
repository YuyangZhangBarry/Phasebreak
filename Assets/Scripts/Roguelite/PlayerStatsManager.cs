using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent player stats + cross-scene progression for roguelite runs.
/// Owns upgrade effects and dimension switch unlock state.
/// On each scene load: optional spawn snap + restore cached HP + rebind scene UI to this Health.
/// </summary>
public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }

    [Header("Dimension Switch Unlock")]
    [Tooltip("If false, the player is locked in 2D and cannot switch to 3D.")]
    [SerializeField] private bool isDimensionSwitchUnlocked = false;

    public bool IsDimensionSwitchUnlocked => isDimensionSwitchUnlocked;

    [Header("Cross-scene spawn")]
    [Tooltip("After LoadScene, if an object in the loaded scene has this tag, the player is moved there (position + rotation).")]
    [SerializeField] private string spawnPointTag = "SpawnPoint";

    [Header("Spawn Y by dimension (match ModeSwitcher / Portal)")]
    [Tooltip("Player Y when spawning after entering a portal in 2D mode.")]
    [SerializeField] private float spawnPlaneY2D = 40f;

    [Tooltip("Ray start height (world Y) when spawning in 3D — same idea as ModeSwitcher landing ray.")]
    [SerializeField] private float spawnRaycastStartHeight = 50f;

    [SerializeField] private float spawnRaycastMaxDistance = 100f;

    [Tooltip("Feet offset above ground hit; match player capsule center vs height (ModeSwitcher uses 1).")]
    [SerializeField] private float spawnGroundCapsuleHalfHeight = 1f;

    [Tooltip("If 0, uses default raycast layers.")]
    [SerializeField] private LayerMask spawnGroundLayers = ~0;

    [Tooltip("If true, 3D ground rays skip hits on the player so chests/spawn at XZ don't land on the capsule.")]
    [SerializeField] private bool ignorePlayerColliderOnGroundRay = true;

    /// <summary>Set by <see cref="Portal"/> right before LoadScene; consumed once when applying spawn.</summary>
    private bool pendingSpawnDimensionFromPortal;
    private bool pendingSpawnWas2DMode;

    private Health playerHealth;
    private PlayerController playerController;
    private ModeSwitcher modeSwitcher;

    /// <summary>Run-wide HP mirror (updated from Health events) so LoadScene cannot silently desync UI/stats.</summary>
    private float runMaxHealth = 100f;
    private float runCurrentHealth = 100f;

    /// <summary>Roguelite card bonuses (additive on top of baseline + meta upgrades).</summary>
    private float rogueMoveBonus;
    private float rogueDamageBonus;
    private float rogueHpBonusFromCards;

    private void Awake()
    {
        // Singleton + persistence.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy duplicated Player root in newly loaded scenes.
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        playerHealth = GetComponent<Health>();
        playerController = GetComponent<PlayerController>();
        modeSwitcher = GetComponent<ModeSwitcher>();

        // Requirement: keep 2D→3D locked during Level 1. Unlock permanently after Level 1 is cleared.
        if (IsLevel1Scene())
            isDimensionSwitchUnlocked = false;

        SyncDimensionSwitchLock();
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private bool isDead = false;

    private void Start()
    {
        if (Instance != this)
            return;

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnPlayerHealthChanged;
            playerHealth.OnDie += OnPlayerDied;
            runMaxHealth = playerHealth.MaxHealth;
            runCurrentHealth = playerHealth.CurrentHealth;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this && playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnPlayerHealthChanged;
            playerHealth.OnDie -= OnPlayerDied;
        }
    }

    private void OnPlayerDied()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log("[PlayerStatsManager] Player died! Loading GameOver scene.");

        GameStatsManager.EnsureExists();
        if (GameStatsManager.Instance != null)
            GameStatsManager.Instance.RecordDeath();

        // Unsubscribe before destroying
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnPlayerHealthChanged;
            playerHealth.OnDie -= OnPlayerDied;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;

        Instance = null;

        // Load first: GameOver must be in Build Settings. Destroy after load — DestroyImmediate on this
        // object can prevent the rest of the callback from running, so LoadScene would never execute.
        SceneManager.LoadScene("GameOver");
        Destroy(gameObject);
    }

    private void OnPlayerHealthChanged(float current, float max)
    {
        runMaxHealth = max;
        runCurrentHealth = current;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this)
            return;

        StartCoroutine(CoAfterSceneLoaded());
    }

    private IEnumerator CoAfterSceneLoaded()
    {
        // Wait one frame so duplicate scene Player is destroyed and scene objects exist.
        yield return null;

        if (Instance != this || playerHealth == null)
            yield break;

        TryMoveToSpawnPoint();

        // Safety: after spawn placement, re-apply current mode's Y.
        // This fixes an initialization order issue where 2D mode might spawn at Y=0 until
        // the player toggles dimensions once.
        if (modeSwitcher != null && modeSwitcher.is2DMode)
        {
            Vector3 p = transform.position;
            p.y = modeSwitcher.PlaneY2D;
            transform.position = p;
        }

        MetaProgression.ApplyMetaToPlayer(
            playerController,
            playerHealth,
            ref runMaxHealth,
            ref runCurrentHealth,
            rogueMoveBonus,
            rogueDamageBonus,
            rogueHpBonusFromCards);

        playerHealth.ApplyRuntimeHealthState(runMaxHealth, runCurrentHealth);
        RebindPlayerUI();
        GameplayStatsHUD.EnsureAndBind(playerController);

        // If we just loaded Level 1, force-lock dimension switch until all enemies are dead.
        if (IsLevel1Scene())
            isDimensionSwitchUnlocked = false;

        // Keep ModeSwitcher in sync with persisted run flag after every load (e.g. L2+ still unlocked after L1).
        SyncDimensionSwitchLock();
    }

    /// <summary>
    /// Call from <see cref="Portal"/> immediately before <c>LoadScene</c> so spawn Y matches how the player entered the portal.
    /// </summary>
    public void RegisterPortalExitDimension(bool was2DMode)
    {
        pendingSpawnDimensionFromPortal = true;
        pendingSpawnWas2DMode = was2DMode;
    }

    /// <summary>
    /// World position for chests / props using the same 2D plane vs 3D ground rules as <see cref="Portal"/> and player spawn.
    /// Uses current <see cref="ModeSwitcher.is2DMode"/> (not portal-pending flags).
    /// </summary>
    /// <param name="referencePosition">Designer X/Z (and optional Y ignored for 2D/3D logic).</param>
    /// <param name="yOffsetOn2DPlane">Added on top of <see cref="spawnPlaneY2D"/> in 2D.</param>
    /// <param name="yOffsetAboveGroundHit">Added on top of ground ray hit in 3D (pivot above floor).</param>
    public Vector3 GetWorldPositionForGameplayObject(
        Vector3 referencePosition,
        float yOffsetOn2DPlane = 0f,
        float yOffsetAboveGroundHit = 0f)
    {
        bool use2D = modeSwitcher != null && modeSwitcher.is2DMode;
        return ComputeDimensionWorldPosition(referencePosition, use2D, yOffsetOn2DPlane, yOffsetAboveGroundHit);
    }

    /// <summary>Shared math for spawn point, chests, etc.</summary>
    private Vector3 ComputeDimensionWorldPosition(
        Vector3 referencePosition,
        bool use2DHeight,
        float yOffset2D,
        float yOffset3D)
    {
        if (use2DHeight)
            return new Vector3(referencePosition.x, spawnPlaneY2D + yOffset2D, referencePosition.z);

        Vector3 rayStart = new Vector3(referencePosition.x, spawnRaycastStartHeight, referencePosition.z);
        LayerMask mask = spawnGroundLayers.value == 0 ? (LayerMask)Physics.DefaultRaycastLayers : spawnGroundLayers;

        if (Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                spawnRaycastMaxDistance,
                mask,
                QueryTriggerInteraction.Ignore))
        {
            if (!ignorePlayerColliderOnGroundRay || !IsWorldPlacementRayHitIgnored(hit))
                return new Vector3(referencePosition.x, hit.point.y + yOffset3D, referencePosition.z);
        }

        // Single ray hit only the player (or we need to skip player): use all hits and pick first valid ground.
        if (ignorePlayerColliderOnGroundRay &&
            TryRaycastGroundSkippingPlayer(rayStart, mask, yOffset3D, referencePosition, out Vector3 fromAllHits))
        {
            return fromAllHits;
        }

        Debug.LogWarning(
            "[PlayerStatsManager] 3D placement: no ground hit; using Y = yOffset3D only. Check layers / colliders.",
            this);
        return new Vector3(referencePosition.x, yOffset3D, referencePosition.z);
    }

    private bool TryRaycastGroundSkippingPlayer(
        Vector3 rayStart,
        LayerMask mask,
        float yOffset3D,
        Vector3 referencePosition,
        out Vector3 worldPos)
    {
        worldPos = default;
        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            Vector3.down,
            spawnRaycastMaxDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (IsWorldPlacementRayHitIgnored(h))
                continue;

            worldPos = new Vector3(referencePosition.x, h.point.y + yOffset3D, referencePosition.z);
            return true;
        }

        return false;
    }

    private static bool IsWorldPlacementRayHitIgnored(RaycastHit hit)
    {
        if (hit.collider == null)
            return true;

        if (hit.collider.CompareTag("Player"))
            return true;

        return hit.collider.GetComponentInParent<PlayerStatsManager>() != null;
    }

    private void TryMoveToSpawnPoint()
    {
        if (string.IsNullOrEmpty(spawnPointTag))
            return;

        GameObject[] spawns;
        try
        {
            spawns = GameObject.FindGameObjectsWithTag(spawnPointTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning(
                $"[PlayerStatsManager] Tag '{spawnPointTag}' is not defined in Tag Manager. Add it and tag your spawn empty object.",
                this);
            return;
        }

        if (spawns == null || spawns.Length == 0)
            return;

        Transform spawn = spawns[0].transform;
        Vector3 spawnPos = spawn.position;
        Quaternion spawnRot = spawn.rotation;

        bool use2DHeight;
        if (pendingSpawnDimensionFromPortal)
        {
            use2DHeight = pendingSpawnWas2DMode;
            pendingSpawnDimensionFromPortal = false;
        }
        else
        {
            use2DHeight = modeSwitcher != null && modeSwitcher.is2DMode;
        }

        Vector3 finalPos;
        if (use2DHeight)
        {
            // Pivot at feet: spawn Y = 2D plane.
            finalPos = new Vector3(spawnPos.x, spawnPlaneY2D, spawnPos.z);
        }
        else
        {
            finalPos = ComputeDimensionWorldPosition(spawnPos, false, 0f, spawnGroundCapsuleHalfHeight);
        }

        transform.SetPositionAndRotation(finalPos, spawnRot);
    }

    private void RebindPlayerUI()
    {
        if (playerHealth == null)
            return;

        PlayerHUD[] huds = UnityEngine.Object.FindObjectsByType<PlayerHUD>(FindObjectsSortMode.None);
        foreach (PlayerHUD hud in huds)
        {
            if (hud != null)
                hud.BindToHealth(playerHealth);
        }

        DamageVignette[] vignettes = UnityEngine.Object.FindObjectsByType<DamageVignette>(FindObjectsSortMode.None);
        foreach (DamageVignette v in vignettes)
        {
            if (v != null)
                v.BindToHealth(playerHealth);
        }
    }

    private void SyncDimensionSwitchLock()
    {
        if (modeSwitcher != null)
            modeSwitcher.isDimensionSwitchUnlocked = isDimensionSwitchUnlocked;
    }

    private bool IsLevel1Scene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Level_01", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Apply a roguelite upgrade card effect to the player.
    /// </summary>
    public void ApplyUpgrade(UpgradeData data)
    {
        if (data == null)
            return;

        switch (data.upgradeType)
        {
            case UpgradeType.DamageUp:
                if (playerController == null || playerHealth == null)
                {
                    Debug.LogWarning("[PlayerStatsManager] Missing PlayerController or Health. DamageUp ignored.", this);
                    return;
                }
                rogueDamageBonus += data.value;
                MetaProgression.ApplyMetaToPlayer(
                    playerController,
                    playerHealth,
                    ref runMaxHealth,
                    ref runCurrentHealth,
                    rogueMoveBonus,
                    rogueDamageBonus,
                    rogueHpBonusFromCards);
                break;

            case UpgradeType.MaxHealthUp:
                if (playerHealth == null)
                {
                    Debug.LogWarning("[PlayerStatsManager] Missing Health. MaxHealthUp ignored.", this);
                    return;
                }
                rogueHpBonusFromCards += data.value;
                MetaProgression.ApplyMetaToPlayer(
                    playerController,
                    playerHealth,
                    ref runMaxHealth,
                    ref runCurrentHealth,
                    rogueMoveBonus,
                    rogueDamageBonus,
                    rogueHpBonusFromCards);
                break;

            case UpgradeType.MoveSpeedUp:
                if (playerController == null)
                {
                    Debug.LogWarning("[PlayerStatsManager] Missing PlayerController. MoveSpeedUp ignored.", this);
                    return;
                }
                rogueMoveBonus += data.value;
                MetaProgression.ApplyMetaToPlayer(
                    playerController,
                    playerHealth,
                    ref runMaxHealth,
                    ref runCurrentHealth,
                    rogueMoveBonus,
                    rogueDamageBonus,
                    rogueHpBonusFromCards);
                break;

            default:
                Debug.LogWarning($"[PlayerStatsManager] Unknown upgrade type: {data.upgradeType}", this);
                break;
        }
    }

    /// <summary>
    /// Force-lock the 2D -> 3D dimension switch permission (typically used in the tutorial on Level 1).
    /// </summary>
    public void UnlockDimensionSwitch()
    {
        if (isDimensionSwitchUnlocked)
            return;

        // Unlock the 2D -> 3D dimension switch for this run.
        isDimensionSwitchUnlocked = true;
        SyncDimensionSwitchLock(); // Sync to ModeSwitcher so it takes effect immediately.
    }
    public void LockDimensionSwitch()
    {
        if (!isDimensionSwitchUnlocked)
            return; 

        isDimensionSwitchUnlocked = false;
        SyncDimensionSwitchLock(); 
    }
}
