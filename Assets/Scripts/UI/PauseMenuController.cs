using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ESC: pause with Continue / Main Menu. Only active during Level_* scenes.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    public static bool IsPaused { get; private set; }

    private Canvas _canvas;
    private GameObject _panel;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("PauseMenuController");
        go.AddComponent<PauseMenuController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildUi();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsGameplayLevel(scene.name))
        {
            if (IsPaused)
                ResumeWithoutNotifyPlayer();
            if (_panel != null)
                _panel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsGameplayLevel(SceneManager.GetActiveScene().name))
            return;

        if (VictoryMenuController.IsShowing)
            return;

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        TogglePause();
    }

    private static bool IsGameplayLevel(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;
        if (sceneName.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (sceneName.IndexOf("GameOver", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        return sceneName.StartsWith("Level_", System.StringComparison.OrdinalIgnoreCase);
    }

    private void BuildUi()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        GameObject canvasGo = new GameObject("PauseMenuCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5600;
        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform crt = canvasGo.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        _panel = new GameObject("PausePanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        RectTransform prt = _panel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        Image dim = _panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        GameObject card = new GameObject("Card");
        card.transform.SetParent(_panel.transform, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(480f, 280f);
        cardRt.anchoredPosition = Vector2.zero;
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color32(28, 34, 56, 245);

        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(card.transform, false);
        RectTransform trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -28f);
        trt.sizeDelta = new Vector2(440f, 48f);
        TextMeshProUGUI title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "Paused";
        title.fontSize = 36;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        ApplyTmpFont(title);

        CreateButton(card.transform, "ContinueButton", "Continue", 0f, ResumeGame);
        CreateButton(card.transform, "MainMenuButton", "Main menu", -72f, GoToMainMenu);

        _panel.SetActive(false);
    }

    private void CreateButton(Transform parent, string name, string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 52f);
        rt.anchoredPosition = new Vector2(0f, y);

        Image img = go.AddComponent<Image>();
        img.color = new Color32(61, 90, 128, 255);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        RectTransform trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        ApplyTmpFont(tmp);
    }

    private static void ApplyTmpFont(TextMeshProUGUI tmp)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        if (font != null)
            tmp.font = font;
    }

    private void TogglePause()
    {
        if (IsPaused)
            ResumeGame();
        else
        {
            IsPaused = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_panel != null)
                _panel.SetActive(true);
        }
    }

    private void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (_panel != null)
            _panel.SetActive(false);

        PlayerController pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (pc != null)
            pc.RefreshCursorForGameMode();
    }

    private void ResumeWithoutNotifyPlayer()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (_panel != null)
            _panel.SetActive(false);
    }

    private void GoToMainMenu()
    {
        ResumeWithoutNotifyPlayer();
        MenuNavigationHelper.LoadMainMenuAndCleanupRun();
    }
}
