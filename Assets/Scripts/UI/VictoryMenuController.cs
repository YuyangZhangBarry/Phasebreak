using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shown when the final-level portal is touched (no next scene). Congratulations + Main Menu.
/// </summary>
public class VictoryMenuController : MonoBehaviour
{
    public static VictoryMenuController Instance { get; private set; }

    public static bool IsShowing { get; private set; }

    private GameObject _panel;

    public static void ShowVictory()
    {
        if (IsShowing)
            return;

        GameObject go = new GameObject("VictoryMenuController");
        DontDestroyOnLoad(go);
        go.AddComponent<VictoryMenuController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        IsShowing = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildUi();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsShowing = false;
        }
    }

    private void BuildUi()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        GameObject canvasGo = new GameObject("VictoryCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5700;
        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform crt = canvasGo.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        _panel = new GameObject("VictoryPanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        RectTransform prt = _panel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        Image dim = _panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        dim.raycastTarget = true;

        GameObject card = new GameObject("Card");
        card.transform.SetParent(_panel.transform, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(560f, 320f);
        cardRt.anchoredPosition = Vector2.zero;
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color32(28, 34, 56, 250);

        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(card.transform, false);
        RectTransform trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -32f);
        trt.sizeDelta = new Vector2(500f, 120f);
        TextMeshProUGUI title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "Congratulations!\nYou cleared all levels.";
        title.fontSize = 32;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        title.enableWordWrapping = true;
        ApplyTmpFont(title);

        GameObject btnGo = new GameObject("MainMenuButton");
        btnGo.transform.SetParent(card.transform, false);
        RectTransform brt = btnGo.AddComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(360f, 56f);
        brt.anchoredPosition = new Vector2(0f, -48f);
        Image bImg = btnGo.AddComponent<Image>();
        bImg.color = new Color32(46, 125, 90, 255);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = bImg;
        btn.onClick.AddListener(OnMainMenuClicked);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform t2 = textGo.AddComponent<RectTransform>();
        t2.anchorMin = Vector2.zero;
        t2.anchorMax = Vector2.one;
        t2.offsetMin = Vector2.zero;
        t2.offsetMax = Vector2.zero;
        TextMeshProUGUI btmp = textGo.AddComponent<TextMeshProUGUI>();
        btmp.text = "Main menu";
        btmp.fontSize = 28;
        btmp.fontStyle = FontStyles.Bold;
        btmp.alignment = TextAlignmentOptions.Center;
        btmp.color = Color.white;
        ApplyTmpFont(btmp);
    }

    private static void ApplyTmpFont(TextMeshProUGUI tmp)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        if (font != null)
            tmp.font = font;
    }

    private void OnMainMenuClicked()
    {
        IsShowing = false;
        Time.timeScale = 1f;
        Destroy(gameObject);
        MenuNavigationHelper.LoadMainMenuAndCleanupRun();
    }
}
