using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Top-left: speed & power (damage). Top-right: total gold (meta). HP stays on PlayerHUD.
/// </summary>
public class GameplayStatsHUD : MonoBehaviour
{
    private static GameplayStatsHUD _instance;

    [SerializeField] private Canvas canvas;
    private TextMeshProUGUI _speedText;
    private TextMeshProUGUI _powerText;
    private TextMeshProUGUI _goldText;
    private PlayerController _player;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        MetaProgression.OnGoldChanged -= HandleGoldChanged;
        if (_instance == this)
            _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (canvas != null)
            canvas.enabled = !IsMainMenuScene(scene.name);
    }

    private static bool IsMainMenuScene(string name)
    {
        return !string.IsNullOrEmpty(name) && name.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Creates HUD once and binds to the persistent player.</summary>
    public static void EnsureAndBind(PlayerController player)
    {
        if (player == null)
            return;

        if (_instance == null)
        {
            GameObject root = new GameObject("GameplayStatsHUDRoot");
            var hud = root.AddComponent<GameplayStatsHUD>();
            hud.BuildUi();
        }

        _instance.Bind(player);
    }

    private void BuildUi()
    {
        GameObject root = gameObject;

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        _speedText = CreateTmp("SpeedText", false, -40f, TextAlignmentOptions.TopLeft);
        _powerText = CreateTmp("PowerText", false, -90f, TextAlignmentOptions.TopLeft);
        _goldText = CreateTmp("GoldText", true, -40f, TextAlignmentOptions.TopRight);

        MetaProgression.OnGoldChanged += HandleGoldChanged;
        HandleGoldChanged(MetaProgression.GetGold());
    }

    private TextMeshProUGUI CreateTmp(string name, bool topRight, float yFromTop, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        if (topRight)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, yFromTop);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, yFromTop);
        }

        rt.sizeDelta = new Vector2(520f, 44f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 26;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.color = Color.white;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        if (font == null && TMP_Settings.defaultFontAsset != null)
            font = TMP_Settings.defaultFontAsset;
        if (font != null)
            tmp.font = font;

        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        return tmp;
    }

    private void Bind(PlayerController player)
    {
        _player = player;
    }

    private void HandleGoldChanged(int totalGold)
    {
        if (_goldText != null)
            _goldText.text = $"Gold: {totalGold}";
    }

    private void Update()
    {
        if (_player == null)
            return;

        if (_speedText != null)
            _speedText.text = $"Speed: {_player.moveSpeed:0.#}";

        if (_powerText != null)
            _powerText.text = $"Power: {_player.meleeDamage:0.#}";
    }
}
