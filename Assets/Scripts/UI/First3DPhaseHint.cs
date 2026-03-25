using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// First time entering 3D: brief on-screen hint to press F for phase switch (2D/3D). No weapon-switch text.
/// </summary>
public class First3DPhaseHint : MonoBehaviour
{
    private const string PrefsKeyGlobal = "Hint_3DIntro_v5";

    [SerializeField] private string phaseHintText = "Press F to switch phase (2D / 3D)";

    [SerializeField] private Color phaseHintColor = Color.white;

    [SerializeField] private float totalVisibleDuration = 5f;

    [SerializeField] private float phaseBlinkHz = 2.5f;

    [Tooltip("If true, ignores PlayerPrefs so you can test the hint every run.")]
    [SerializeField] private bool ignorePlayerPrefsForTesting;

    private ModeSwitcher _modeSwitcher;
    private Canvas _canvas;
    private TextMeshProUGUI _phaseText;
    private Coroutine _hintRoutine;
    private bool _globalIntroScheduledOrShown;

    private void Awake()
    {
        _modeSwitcher = GetComponent<ModeSwitcher>();
    }

    private void Start()
    {
        StartCoroutine(CoDeferredHintIfSpawnedIn3D());
    }

    private IEnumerator CoDeferredHintIfSpawnedIn3D()
    {
        yield return null;
        yield return null;

        if (_modeSwitcher == null || _modeSwitcher.is2DMode)
            yield break;

        RunIntroChain();
    }

    private void OnEnable()
    {
        if (_modeSwitcher == null)
            return;

        _modeSwitcher.OnDimensionShiftComplete += OnDimensionShiftJuiceEnded;
    }

    private void OnDisable()
    {
        if (_modeSwitcher != null)
            _modeSwitcher.OnDimensionShiftComplete -= OnDimensionShiftJuiceEnded;

        if (_hintRoutine != null)
        {
            StopCoroutine(_hintRoutine);
            _hintRoutine = null;
        }
    }

    private void OnDimensionShiftJuiceEnded()
    {
        if (_modeSwitcher == null || _modeSwitcher.is2DMode)
            return;

        RunIntroChain();
    }

    private void RunIntroChain()
    {
        TryShowCombinedIntro();
    }

    private bool TryShowCombinedIntro()
    {
        if (!ignorePlayerPrefsForTesting && PlayerPrefs.GetInt(PrefsKeyGlobal, 0) != 0)
            return false;

        if (_globalIntroScheduledOrShown)
            return false;

        EnsureUi();
        if (_phaseText == null || _canvas == null)
            return false;

        _globalIntroScheduledOrShown = true;

        if (!ignorePlayerPrefsForTesting)
        {
            PlayerPrefs.SetInt(PrefsKeyGlobal, 1);
            PlayerPrefs.Save();
        }

        if (_hintRoutine != null)
            StopCoroutine(_hintRoutine);

        _hintRoutine = StartCoroutine(PhaseHintRoutine());
        return true;
    }

    private IEnumerator PhaseHintRoutine()
    {
        _phaseText.gameObject.SetActive(true);
        _phaseText.text = phaseHintText;
        _phaseText.color = phaseHintColor;
        _canvas.sortingOrder = 32000;
        _canvas.gameObject.SetActive(true);

        float t = 0f;
        while (t < totalVisibleDuration)
        {
            t += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(t * phaseBlinkHz * Mathf.PI * 2f);
            float a = Mathf.Lerp(0.35f, 1f, 0.5f + 0.5f * pulse);
            Color c = phaseHintColor;
            c.a = a;
            _phaseText.color = c;
            yield return null;
        }

        _canvas.gameObject.SetActive(false);
        _hintRoutine = null;
    }

    private void EnsureUi()
    {
        if (_canvas != null)
            return;

        GameObject root = new GameObject("First3DIntroHintCanvas");
        root.transform.SetParent(transform, false);

        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32000;

        _phaseText = CreateHintLine(root.transform, "PhaseHint", 0f);

        ApplyFontAndOutline(_phaseText);

        _phaseText.fontSize = 38;

        root.SetActive(false);
    }

    private static TextMeshProUGUI CreateHintLine(Transform parent, string name, float yOffset)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(1400f, 140f);
        return go.AddComponent<TextMeshProUGUI>();
    }

    private static void ApplyFontAndOutline(TextMeshProUGUI tmp)
    {
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        if (font == null && TMP_Settings.defaultFontAsset != null)
            font = TMP_Settings.defaultFontAsset;
        if (font != null)
            tmp.font = font;

        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.8f);
    }
}
