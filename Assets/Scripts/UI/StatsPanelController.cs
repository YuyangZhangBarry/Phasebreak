using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stats data display panel: only opens/closes UI and refreshes TMP text.
/// Bind the main menu "Stats" button to <see cref="OpenPanel"/>, and the close button to <see cref="ClosePanel"/>.
/// </summary>
public class StatsPanelController : MonoBehaviour
{
    [Header("Panel Root (full-page Stats panel GameObject for show/hide)")]
    [SerializeField] private GameObject panelRoot;

    [Header("Texts (any layout; leave empty to skip the row)")]
    [SerializeField] private TextMeshProUGUI totalAttemptsText;

    [SerializeField] private TextMeshProUGUI time2DText;
    [SerializeField] private TextMeshProUGUI time3DText;

    [SerializeField] private TextMeshProUGUI upgradeDamageText;
    [SerializeField] private TextMeshProUGUI upgradeHealthText;
    [SerializeField] private TextMeshProUGUI upgradeSpeedText;

    [Header("Optional: click overlay to close")]
    [SerializeField] private Button optionalBackgroundCloseButton;

    private void Awake()
    {
        GameStatsManager.EnsureExists();

        if (optionalBackgroundCloseButton != null)
            optionalBackgroundCloseButton.onClick.AddListener(ClosePanel);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (optionalBackgroundCloseButton != null)
            optionalBackgroundCloseButton.onClick.RemoveListener(ClosePanel);
    }

    /// <summary>Open panel: fetch latest data, refresh text, and show the panel.</summary>
    public void OpenPanel()
    {
        GameStatsManager.EnsureExists();
        if (GameStatsManager.Instance == null)
            return;

        PlayerStatsData data = GameStatsManager.Instance.Snapshot;
        RefreshTexts(data);

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    /// <summary>Close panel.</summary>
    public void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void RefreshTexts(PlayerStatsData data)
    {
        if (data == null)
            data = PlayerStatsData.CreateDefault();

        SetIfAssigned(totalAttemptsText, $"Total Attempts: {data.totalAttempts}");
        SetIfAssigned(time2DText, $"Total Time in 2D: {FormatDurationChinese(data.timeSpent2D)}");
        SetIfAssigned(time3DText, $"Total Time in 3D: {FormatDurationChinese(data.timeSpent3D)}");
        SetIfAssigned(upgradeDamageText, $"Damage Upgrade Picks: {data.upgradeCountDamage}");
        SetIfAssigned(upgradeHealthText, $"Health Upgrade Picks: {data.upgradeCountHealth}");
        SetIfAssigned(upgradeSpeedText, $"Move Speed Upgrade Picks: {data.upgradeCountSpeed}");
    }

    private static void SetIfAssigned(TextMeshProUGUI tmp, string value)
    {
        if (tmp != null)
            tmp.text = value;
    }

    /// <summary>Format seconds as "X hours Y minutes Z seconds".</summary>
    public static string FormatDurationChinese(float totalSeconds)
    {
        if (totalSeconds < 0f)
            totalSeconds = 0f;

        int s = Mathf.FloorToInt(totalSeconds);
        int hours = s / 3600;
        int minutes = (s % 3600) / 60;
        int seconds = s % 60;
        return $"{hours} hours {minutes} minutes {seconds} seconds";
    }
}
