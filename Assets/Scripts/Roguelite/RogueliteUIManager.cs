using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
// SceneManagement kept for potential future scene-based UI logic.

/// <summary>
/// Manages the "choose 3 upgrades" UI (3-choice system).
/// On card click: applies upgrade, closes UI, and spawns the next-level Portal.
/// </summary>
public class RogueliteUIManager : MonoBehaviour
{
    public static RogueliteUIManager Instance { get; private set; }

    /// <summary>
    /// Per-scene: Level 1 tutorial ON — show F-key hint after card pick. Level 2+ — leave OFF.
    /// Does not remove any toast code; only gates whether it runs.
    /// </summary>
    [Header("Tutorial / per-level")]
    [Tooltip("If true, the dimension-unlock toast coroutine can run after picking a card (use on Level 1 only).")]
    public bool shouldShowUnlockPrompt = false;

    [Header("Upgrade Pool")]
    [Tooltip("All possible upgrade cards for this run/level.")]
    [SerializeField] private List<UpgradeData> upgradePool = new List<UpgradeData>();

    [Header("Card Slots")]
    [Tooltip("Exactly 3 card UI slots (choose-3 layout).")]
    [SerializeField] private UpgradeCardUI[] cardSlots = new UpgradeCardUI[3];

    [Header("Panel Root")]
    [Tooltip("Root panel GameObject to show/hide the upgrade selection UI.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Portal Spawn")]
    [Tooltip("Portal prefab spawned after the player picks an upgrade.")]
    [SerializeField] private Portal portalPrefab;

    [Tooltip("Where to spawn the next-level portal in the scene.")]
    [SerializeField] private Vector3 portalSpawnPosition = Vector3.zero;

    [Tooltip("If >= 0, overwrites Portal.GroundYOffset on the spawned instance (fixes stale prefab serialization). -1 = use prefab value.")]
    [SerializeField] private float portalGroundYOffsetOverride = -1f;

    [Header("Dimension unlock toast")]
    [Tooltip("Center-screen TMP message shown after picking a card. Duration = Dimension Unlock Toast Duration. Leave empty to skip.")]
    [SerializeField] private TextMeshProUGUI dimensionUnlockToastText;

    [Tooltip("Optional parent GameObject to toggle (e.g. panel with background). If empty, only the TMP object is toggled.")]
    [SerializeField] private GameObject dimensionUnlockToastRoot;

    [Tooltip("Message shown when dimension switch is unlocked.")]
    [SerializeField] private string dimensionUnlockMessage = "Dimension switch unlocked! Press F to switch.";

    [Tooltip("How long the toast stays visible (seconds).")]
    [SerializeField] private float dimensionUnlockToastDuration = 3f;

    [Header("Runtime auto-wire (optional)")]
    [Tooltip("If upgrade pool is empty in Inspector, load these ScriptableObjects at runtime (assign 3+ UpgradeData assets).")]
    [SerializeField] private List<UpgradeData> upgradePoolRuntimeFallback = new List<UpgradeData>();

    private string nextSceneName = "";

    private Portal spawnedPortal;
    private bool isPanelOpen = false;
    private Coroutine dimensionUnlockToastRoutine;

    private void Awake()
    {
        // Previous scene's manager may still be alive for one frame — destroy OLD, keep THIS scene's UI.
        if (Instance != null && Instance != this)
            Destroy(Instance.gameObject);

        Instance = this;

        TryAutoWireMissingReferences();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (dimensionUnlockToastText != null)
            dimensionUnlockToastText.gameObject.SetActive(false);
        if (dimensionUnlockToastRoot != null)
            dimensionUnlockToastRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Fills missing references when a duplicated scene forgot to assign RogueliteUIManager fields.
    /// </summary>
    private void TryAutoWireMissingReferences()
    {
        if (panelRoot == null)
        {
            GameObject panel = GameObject.Find("UpgradeSelectionPanel");
            if (panel != null)
                panelRoot = panel;
        }

        if (cardSlots == null || cardSlots.Length < 3)
            cardSlots = new UpgradeCardUI[3];

        bool needCards = false;
        for (int i = 0; i < 3; i++)
        {
            if (i >= cardSlots.Length || cardSlots[i] == null)
            {
                needCards = true;
                break;
            }
        }

        if (needCards)
        {
            if (panelRoot != null)
            {
                UpgradeCardUI[] found = panelRoot.GetComponentsInChildren<UpgradeCardUI>(true);
                for (int i = 0; i < Mathf.Min(3, found.Length); i++)
                    cardSlots[i] = found[i];
            }

            if (cardSlots[0] == null || cardSlots[1] == null || cardSlots[2] == null)
            {
                UpgradeCardUI[] all = UnityEngine.Object.FindObjectsByType<UpgradeCardUI>(FindObjectsSortMode.None);
                Array.Sort(all, (a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
                int j = 0;
                for (int i = 0; i < cardSlots.Length && j < all.Length; i++)
                {
                    if (cardSlots[i] == null)
                        cardSlots[i] = all[j++];
                }
            }
        }

        if (dimensionUnlockToastText == null)
        {
            GameObject toastGo = GameObject.Find("UnlockNotificationText");
            if (toastGo != null)
                dimensionUnlockToastText = toastGo.GetComponent<TextMeshProUGUI>();
        }

        if (upgradePool == null)
            upgradePool = new List<UpgradeData>();

        if (upgradePool.Count < 3 || upgradePool.Exists(x => x == null))
        {
            upgradePool.RemoveAll(x => x == null);
            if (upgradePoolRuntimeFallback != null && upgradePoolRuntimeFallback.Count >= 3)
            {
                upgradePool.Clear();
                foreach (UpgradeData d in upgradePoolRuntimeFallback)
                {
                    if (d != null)
                        upgradePool.Add(d);
                }
            }
        }

    }

    public void SetNextSceneName(string sceneName)
    {
        nextSceneName = sceneName;
    }

    /// <summary>
    /// Randomly pick 3 unique upgrades from the pool and show them.
    /// </summary>
    public void ShowRandomUpgrades()
    {
        if (isPanelOpen)
            return;

        TryAutoWireMissingReferences();

        Debug.Log($"[RogueliteUIManager] ShowRandomUpgrades called. upgradePoolCount={(upgradePool != null ? upgradePool.Count : -1)}, cardSlotsCount={(cardSlots != null ? cardSlots.Length : -1)}, panelRoot={(panelRoot != null ? panelRoot.name : "null")}.", this);

        // Ensure UI is visible even if panelRoot was assigned incorrectly.
        if (panelRoot != null)
            panelRoot.SetActive(true);
        else
        {
            for (int i = 0; i < cardSlots.Length; i++)
                if (cardSlots[i] != null)
                    cardSlots[i].gameObject.SetActive(true);
        }

        isPanelOpen = true;

        // Unlock cursor so player can click cards (fixes 3D mode cursor lock)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (upgradePool == null || upgradePool.Count < 3)
        {
            Debug.LogWarning("[RogueliteUIManager] Upgrade pool has fewer than 3 cards.", this);
            HidePanel();
            return;
        }

        // Choose 3 unique cards.
        List<UpgradeData> poolCopy = new List<UpgradeData>(upgradePool);
        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] == null)
            {
                Debug.LogWarning($"[RogueliteUIManager] cardSlots[{i}] is null. Check Inspector assignment.", this);
                continue;
            }

            if (cardSlots[i].gameObject != null)
                cardSlots[i].gameObject.SetActive(true);

            Debug.Log($"[RogueliteUIManager] Preparing slot {i}: cardSlotName='{cardSlots[i].gameObject.name}', activeSelf={cardSlots[i].gameObject.activeSelf}.", this);

            int index = UnityEngine.Random.Range(0, poolCopy.Count);
            UpgradeData picked = poolCopy[index];
            poolCopy.RemoveAt(index);

            cardSlots[i].Setup(picked, OnCardClicked);
        }
    }

    private void OnCardClicked(UpgradeData picked)
    {
        Debug.Log($"[RogueliteUIManager] Card clicked: '{(picked != null ? picked.cardName : "null")}'.", this);

        // Apply upgrade first.
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.ApplyUpgrade(picked);
        else
            Debug.LogWarning("[RogueliteUIManager] Missing PlayerStatsManager.Instance.", this);

        if (picked != null)
        {
            GameStatsManager.EnsureExists();
            if (GameStatsManager.Instance != null)
                GameStatsManager.Instance.RecordUpgrade(picked.upgradeType);
        }

        // Toast only the first time dimension switch becomes unlocked (e.g. first unlock in Level 2+).
        bool wasAlreadyUnlocked =
            PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.IsDimensionSwitchUnlocked;

        // Unlock when the player selects a card (between levels).
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.UnlockDimensionSwitch();

        // Close UI then spawn portal.
        HidePanel();

        // Re-lock cursor for gameplay (respects current dimension)
        if (PlayerStatsManager.Instance != null)
        {
            PlayerController pc = PlayerStatsManager.Instance.GetComponent<PlayerController>();
            if (pc != null)
                pc.RefreshCursorForGameMode();
        }

        SpawnPortalForNextScene();

        if (!wasAlreadyUnlocked)
            ShowDimensionUnlockToast();
    }

    /// <summary>
    /// Shows center toast for <see cref="dimensionUnlockToastDuration"/> seconds (dimension unlock + F key hint).
    /// </summary>
    private void ShowDimensionUnlockToast()
    {
        if (!shouldShowUnlockPrompt)
            return;

        if (dimensionUnlockToastText == null)
            return;

        if (dimensionUnlockToastRoutine != null)
            StopCoroutine(dimensionUnlockToastRoutine);

        dimensionUnlockToastRoutine = StartCoroutine(DimensionUnlockToastRoutine());
    }

    private IEnumerator DimensionUnlockToastRoutine()
    {
        if (dimensionUnlockToastRoot != null)
            dimensionUnlockToastRoot.SetActive(true);

        dimensionUnlockToastText.gameObject.SetActive(true);
        dimensionUnlockToastText.text = dimensionUnlockMessage;

        yield return new WaitForSeconds(Mathf.Max(0.01f, dimensionUnlockToastDuration));

        dimensionUnlockToastText.gameObject.SetActive(false);
        if (dimensionUnlockToastRoot != null)
            dimensionUnlockToastRoot.SetActive(false);

        dimensionUnlockToastRoutine = null;
    }

    private void HidePanel()
    {
        isPanelOpen = false;
        if (panelRoot != null)
            panelRoot.SetActive(false);

        // Extra safety: hide card slots in case panelRoot isn't the direct parent.
        for (int i = 0; i < cardSlots.Length; i++)
            if (cardSlots[i] != null)
                cardSlots[i].gameObject.SetActive(false);
    }

    private void SpawnPortalForNextScene()
    {
        if (portalPrefab == null)
        {
            Debug.LogWarning("[RogueliteUIManager] Portal prefab is not assigned.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.Log("[RogueliteUIManager] Final level: portal has no next scene — touching it shows victory.", this);
        }

        if (spawnedPortal != null)
            Destroy(spawnedPortal.gameObject);

        spawnedPortal = Instantiate(portalPrefab, portalSpawnPosition, Quaternion.identity);
        spawnedPortal.gameObject.SetActive(true);
        spawnedPortal.nextSceneName = nextSceneName;

        if (portalGroundYOffsetOverride >= 0f)
            spawnedPortal.GroundYOffset = portalGroundYOffsetOverride;

        Debug.Log($"[RogueliteUIManager] Portal instantiated at {portalSpawnPosition} for nextSceneName='{nextSceneName}'.", this);
    }
}

