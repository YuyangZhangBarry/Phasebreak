using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Roguelite level flow controller:
/// - Wait until all enemies are dead.
/// - Show "Level Complete" UI.
/// - When continued: spawn Chest in scene center.
/// </summary>
public class LevelManager : MonoBehaviour
{
    /// <summary>True while "Level Complete" + Continue are shown. PlayerController uses this to keep the cursor visible in 3D.</summary>
    public static bool IsLevelCompleteUiVisible { get; private set; }

    [Header("Next Scene")]
    [Tooltip("Next level scene name loaded after player picks an upgrade (via Portal).")]
    [SerializeField] private string nextSceneName = "";

    [Header("Chest Spawn")]
    [Tooltip("Chest prefab spawned after the level is completed.")]
    [SerializeField] private Chest chestPrefab;

    [Tooltip("Where to spawn the chest in the scene (default: center). X/Z are used; Y is recomputed from 2D/3D when PlayerStatsManager exists.")]
    [SerializeField] private Vector3 chestSpawnPosition = Vector3.zero;

    [Tooltip("Extra Y on the 2D plane height (e.g. chest pivot above the 40 plane).")]
    [SerializeField] private float chestYOffset2D = 0f;

    [Tooltip("Extra Y above 3D ground ray hit (e.g. half box height so the chest sits on the floor).")]
    [SerializeField] private float chestYOffset3D = 0f;

    [Header("Level Complete UI")]
    [Tooltip("TMP text that shows 'Level Complete'.")]
    [SerializeField] private TextMeshProUGUI levelCompleteText;

    [Tooltip("Button that player clicks to spawn chest.")]
    [SerializeField] private Button continueButton;

    [Header("Enemy Detection")]
    [Tooltip("If enabled, LevelManager auto-detects enemies by 'Health' components on objects in the Enemy layer.")]
    [SerializeField] private bool autoFindEnemies = true;

    [Tooltip("Layer name that contains enemy objects (must exist).")]
    [SerializeField] private string enemyLayerName = "Enemy";

    [Tooltip("If Continue / Level Complete text are missing in Inspector, try to find by GameObject name in the scene.")]
    [SerializeField] private bool autoWireMissingUiByName = true;

    private readonly HashSet<Health> aliveEnemies = new HashSet<Health>();
    private bool chestSpawned = false;

    private void Start()
    {
        if (autoWireMissingUiByName)
            TryAutoWireLevelCompleteUi();

        // Core logic: if this is Level 1, force the 2D tutorial mode.
        if (IsLevel1Scene())
        {
            EnforceLevel1TutorialState();
        }

        Debug.Log($"[LevelManager] Start. nextSceneName='{nextSceneName}', chestPrefab={(chestPrefab != null ? chestPrefab.gameObject.name : "null")}, continueButton={(continueButton != null ? continueButton.name : "null")}", this);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        if (levelCompleteText != null)
            levelCompleteText.gameObject.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (RogueliteUIManager.Instance != null)
            RogueliteUIManager.Instance.SetNextSceneName(nextSceneName);

        if (autoFindEnemies)
            RegisterEnemiesFromScene();
    }

    private void OnDestroy()
    {
        IsLevelCompleteUiVisible = false;
    }

    /// <summary>
    /// Force Level 1 player into 2D and temporarily lock dimension switching.
    /// </summary>
    private void EnforceLevel1TutorialState()
    {
        Debug.Log("[LevelManager] Level 1 tutorial mode enabled: lock 3D switching and force 2D mode.");

        // 1) Lock permissions: call the dedicated lock method.
        if (PlayerStatsManager.Instance != null)
        {
            PlayerStatsManager.Instance.LockDimensionSwitch();
        }

        // 2) Force newly spawned player into 2D dimension (Y=40).
        /*
        ModeSwitcher modeSwitcher = Object.FindFirstObjectByType<ModeSwitcher>();
        if (modeSwitcher != null)
        {
            modeSwitcher.Force2DMode(); 
        }
        */
    }

    private void TryAutoWireLevelCompleteUi()
    {
        if (continueButton == null)
        {
            GameObject go = GameObject.Find("ContinueButton");
            if (go != null)
                continueButton = go.GetComponent<Button>();
        }

        if (levelCompleteText == null)
        {
            GameObject go = GameObject.Find("LevelComplete");
            if (go != null)
                levelCompleteText = go.GetComponent<TextMeshProUGUI>();
        }
    }

    private void RegisterEnemiesFromScene()
    {
        int enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        if (enemyLayer < 0)
        {
            Debug.LogWarning($"[LevelManager] Enemy layer '{enemyLayerName}' not found. Enemy counting may fail.");
            return;
        }

        Health playerHealth = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.GetComponent<Health>() : null;

        Health[] allHealth = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (Health h in allHealth)
        {
            if (h == null) continue;
            if (playerHealth != null && h == playerHealth) continue;
            if (h.gameObject.layer != enemyLayer) continue;

            aliveEnemies.Add(h);
        }

        // Subscribe to deaths so we can trigger Level Complete.
        // Health.OnDie has no parameters, so we capture the current local enemyHealth.
        foreach (Health enemyHealth in aliveEnemies)
        {
            if (enemyHealth == null)
                continue;

            enemyHealth.OnDie += () => OnEnemyDied(enemyHealth);
        }

        Debug.Log($"[LevelManager] Registered {aliveEnemies.Count} enemies at start (layer='{enemyLayerName}').", this);
        if (aliveEnemies.Count == 0)
            Debug.LogWarning($"[LevelManager] Enemy count is 0. Check that enemies (e.g., Zombies) are on layer='{enemyLayerName}'.", this);

        if (aliveEnemies.Count == 0)
            ShowLevelComplete();
    }

    private void OnEnemyDied(Health enemyHealth)
    {
        // Robust cleanup:
        // - if enemyHealth is already destroyed, HashSet contains null entries; remove them too
        // - if multiple deaths happen, ensure we still reach count==0
        aliveEnemies.RemoveWhere(e => e == null || e == enemyHealth);

        if (enemyHealth != null)
        {
            int gold = GoldDropTable.GetGoldForKill(enemyHealth.gameObject);
            if (gold > 0)
                MetaProgression.AddGold(gold);
        }

        Debug.Log(
            $"[LevelManager] Enemy killed: enemyHealth={(enemyHealth != null ? enemyHealth.gameObject.name : "null")}, remaining enemies: {aliveEnemies.Count}",
            this);

        if (aliveEnemies.Count <= 0)
            ShowLevelComplete();
    }

    private void ShowLevelComplete()
    {
        IsLevelCompleteUiVisible = true;

        // Core change: removed PlayerStatsManager.Instance.UnlockDimensionSwitch() from here.
        // Unlocking must happen only after the player picks a card. This method is UI-only.

        Debug.Log($"[LevelManager] ShowLevelComplete called. levelCompleteText={(levelCompleteText != null ? levelCompleteText.name : "null")}, continueButton={(continueButton != null ? continueButton.name : "null")}", this);

        if (PlayerStatsManager.Instance != null)
        {
            PlayerController pc = PlayerStatsManager.Instance.GetComponent<PlayerController>();
            if (pc != null) pc.UnlockCursorForMenus();
        }
        else
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
            if (pc != null) pc.UnlockCursorForMenus();
        }

        if (levelCompleteText != null) levelCompleteText.gameObject.SetActive(true);
        if (continueButton != null) continueButton.gameObject.SetActive(true);
    }

    private bool IsLevel1Scene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Level_01", System.StringComparison.OrdinalIgnoreCase);
    }

    private void OnContinueClicked()
    {
        if (chestSpawned) return;

        chestSpawned = true;
        IsLevelCompleteUiVisible = false;

        if (continueButton != null) continueButton.gameObject.SetActive(false);
        if (levelCompleteText != null) levelCompleteText.gameObject.SetActive(false);

        Vector3 chestPos = ResolveChestSpawnWorldPosition();
        Debug.Log($"[LevelManager] Continue clicked. Spawning chest at {chestPos}.", this);
        SpawnChestAtCenter(chestPos);

        if (PlayerStatsManager.Instance != null)
        {
            PlayerController pc = PlayerStatsManager.Instance.GetComponent<PlayerController>();
            if (pc != null) pc.RefreshCursorForGameMode();
        }
        else
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
            if (pc != null) pc.RefreshCursorForGameMode();
        }
    }

    private Vector3 ResolveChestSpawnWorldPosition()
    {
        if (PlayerStatsManager.Instance != null)
        {
            return PlayerStatsManager.Instance.GetWorldPositionForGameplayObject(
                chestSpawnPosition,
                chestYOffset2D,
                chestYOffset3D);
        }
        return chestSpawnPosition;
    }

    private void SpawnChestAtCenter(Vector3 worldPosition)
    {
        if (chestPrefab == null) return;
        var chestInstance = Instantiate(chestPrefab, worldPosition, Quaternion.identity);
    }
}