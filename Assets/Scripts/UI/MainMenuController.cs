using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds main menu UI at runtime: PhaseArena title, spaced buttons, upgrade panel with draft + save.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string firstLevelSceneName = "Level_01";

    private GameObject _upgradePanel;
    private GameObject _controlsPanel;
    private TextMeshProUGUI _panelGoldText;

    private int _draftGold;
    private int _draftPower;
    private int _draftSpeed;
    private int _draftHp;

    private readonly AbilityRowRefs _rowPower = new AbilityRowRefs();
    private readonly AbilityRowRefs _rowSpeed = new AbilityRowRefs();
    private readonly AbilityRowRefs _rowHp = new AbilityRowRefs();

    private sealed class AbilityRowRefs
    {
        public TextMeshProUGUI Label;
        public readonly Image[] Segments = new Image[MetaProgression.MaxUpgradeLevel];
        public Button UpBtn;
    }

    private static readonly Color32 BgDark = new Color32(18, 22, 38, 255);
    private static readonly Color32 PanelCard = new Color32(28, 34, 56, 250);
    private static readonly Color32 BtnPrimary = new Color32(61, 90, 128, 255);
    private static readonly Color32 BtnPrimaryHover = new Color32(80, 120, 168, 255);
    private static readonly Color32 BtnSave = new Color32(46, 125, 90, 255);
    private static readonly Color32 BtnSaveHover = new Color32(58, 155, 112, 255);
    private static readonly Color32 SegLit = new Color32(78, 205, 196, 255);
    private static readonly Color32 SegDim = new Color32(42, 48, 64, 255);

    private Canvas _rootCanvas;
    private RectTransform _canvasRootRt;
    private bool _upgradePanelBuilt;
    private bool _controlsPanelBuilt;

    private void Awake()
    {
        BuildMainMenuUi();
    }

    private IEnumerator Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (!_upgradePanelBuilt)
            BuildUpgradePanelUi();
        if (!_controlsPanelBuilt)
            BuildControlsPanelUi();
    }

    public void StartGame()
    {
        if (string.IsNullOrEmpty(firstLevelSceneName))
            return;
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void OpenUpgrades()
    {
        if (!_upgradePanelBuilt)
        {
            Canvas.ForceUpdateCanvases();
            BuildUpgradePanelUi();
        }

        _draftGold = MetaProgression.GetGold();
        _draftPower = MetaProgression.GetPowerLevel();
        _draftSpeed = MetaProgression.GetSpeedLevel();
        _draftHp = MetaProgression.GetHpLevel();
        RefreshDraftUi();
        if (_controlsPanel != null)
            _controlsPanel.SetActive(false);
        if (_upgradePanel != null)
            _upgradePanel.SetActive(true);
    }

    public void OpenControls()
    {
        if (!_controlsPanelBuilt)
        {
            Canvas.ForceUpdateCanvases();
            BuildControlsPanelUi();
        }

        if (_upgradePanel != null)
            _upgradePanel.SetActive(false);
        if (_controlsPanel != null)
            _controlsPanel.SetActive(true);
    }

    public void CloseControls()
    {
        if (_controlsPanel != null)
            _controlsPanel.SetActive(false);
    }

    /// <summary>Back / cancel: closes without writing upgrades or gold.</summary>
    public void CloseUpgrades()
    {
        if (_upgradePanel != null)
            _upgradePanel.SetActive(false);
    }

    public void SaveUpgrades()
    {
        MetaProgression.CommitMeta(_draftGold, _draftPower, _draftSpeed, _draftHp);
        if (_upgradePanel != null)
            _upgradePanel.SetActive(false);
    }

    private void DraftUpgradePower()
    {
        if (!TrySpendDraftUpgrade(ref _draftPower))
            return;
        RefreshDraftUi();
    }

    private void DraftUpgradeSpeed()
    {
        if (!TrySpendDraftUpgrade(ref _draftSpeed))
            return;
        RefreshDraftUi();
    }

    private void DraftUpgradeHp()
    {
        if (!TrySpendDraftUpgrade(ref _draftHp))
            return;
        RefreshDraftUi();
    }

    private bool TrySpendDraftUpgrade(ref int level)
    {
        if (level >= MetaProgression.MaxUpgradeLevel)
            return false;
        if (MetaProgression.UpgradeCostGold > 0 && _draftGold < MetaProgression.UpgradeCostGold)
            return false;

        if (MetaProgression.UpgradeCostGold > 0)
            _draftGold -= MetaProgression.UpgradeCostGold;
        level++;
        return true;
    }

    private void RefreshDraftUi()
    {
        if (_panelGoldText != null)
            _panelGoldText.text =
                $"Gold: <b>{_draftGold}</b>  <size=80%><color=#8899aa>({MetaProgression.UpgradeCostGold} per level)</color></size>";

        RefreshRow(_rowPower, _draftPower, "Power", "+1 damage per level");
        RefreshRow(_rowSpeed, _draftSpeed, "Speed", "+0.3 move speed per level");
        RefreshRow(_rowHp, _draftHp, "HP", "+5 max HP per level");
    }

    private void RefreshRow(AbilityRowRefs row, int level, string title, string effectLine)
    {
        if (row.Label != null)
        {
            row.Label.text =
                $"<b>{title}</b>\n<size=78%><color=#aab0c0>{effectLine}</color></size>";
        }

        for (int i = 0; i < row.Segments.Length; i++)
        {
            if (row.Segments[i] != null)
                row.Segments[i].color = i < level ? SegLit : SegDim;
        }

        bool canUp = level < MetaProgression.MaxUpgradeLevel &&
                     (MetaProgression.UpgradeCostGold <= 0 || _draftGold >= MetaProgression.UpgradeCostGold);
        if (row.UpBtn != null)
            row.UpBtn.interactable = canUp;
    }

    private void BuildMainMenuUi()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        GameObject canvasGo = new GameObject("Canvas");
        _rootCanvas = canvasGo.AddComponent<Canvas>();
        _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _rootCanvas.sortingOrder = 100;
        canvasGo.AddComponent<GraphicRaycaster>();
        // No CanvasScaler: Scale With Screen Size shrinks the whole menu in small Game views / <1080p.
        // Overlay canvas uses screen pixels; layout stays readable at native resolution.

        _canvasRootRt = canvasGo.GetComponent<RectTransform>();
        StretchFull(_canvasRootRt);

        Image bg = CreatePanel(canvasGo.transform, "Background");
        StretchFull(bg.rectTransform);
        bg.color = BgDark;

        GameObject menuCol = new GameObject("MenuColumn");
        menuCol.transform.SetParent(bg.transform, false);
        RectTransform colRt = menuCol.AddComponent<RectTransform>();
        colRt.anchorMin = colRt.anchorMax = new Vector2(0.5f, 0.5f);
        colRt.pivot = new Vector2(0.5f, 0.5f);
        float menuW = Mathf.Clamp(Screen.width * 0.88f, 520f, 920f);
        colRt.sizeDelta = new Vector2(menuW, 700f);
        colRt.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup colV = menuCol.AddComponent<VerticalLayoutGroup>();
        colV.spacing = 36f;
        colV.padding = new RectOffset(48, 48, 28, 28);
        colV.childAlignment = TextAnchor.MiddleCenter;
        colV.childControlWidth = true;
        colV.childControlHeight = true;
        colV.childForceExpandWidth = true;
        colV.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateTmp(menuCol.transform, "Title", "PhaseArena", 78, TextAlignmentOptions.Center, true);
        title.color = Color.white;
        LayoutElement leTitle = title.gameObject.AddComponent<LayoutElement>();
        leTitle.preferredHeight = 120f;
        leTitle.minHeight = 96f;

        CreateSpacer(menuCol.transform, 16f);

        Button startBtn = CreateStyledButton(menuCol.transform, "StartGameButton", "Start game", StartGame, BtnPrimary, BtnPrimaryHover);
        startBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        CreateSpacer(menuCol.transform, 12f);

        Button upgradeBtn = CreateStyledButton(menuCol.transform, "UpgradeButton", "Upgrade", OpenUpgrades, BtnPrimary, BtnPrimaryHover);
        upgradeBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        CreateSpacer(menuCol.transform, 12f);

        Button controlsBtn = CreateStyledButton(menuCol.transform, "ControlsButton", "Controls", OpenControls, BtnPrimary, BtnPrimaryHover);
        controlsBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
    }

    private void BuildControlsPanelUi()
    {
        if (_controlsPanelBuilt || _canvasRootRt == null)
            return;

        Transform bg = _canvasRootRt.GetChild(0);
        _controlsPanel = new GameObject("ControlsPanel");
        _controlsPanel.transform.SetParent(bg, false);
        RectTransform panelRt = _controlsPanel.AddComponent<RectTransform>();
        StretchFull(panelRt);
        Image dim = _controlsPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        GameObject card = new GameObject("ControlsCard");
        card.transform.SetParent(_controlsPanel.transform, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(Mathf.Min(720f, Screen.width * 0.9f), 420f);
        cardRt.anchoredPosition = Vector2.zero;
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = PanelCard;
        cardImg.raycastTarget = true;

        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectDistance = new Vector2(2f, -2f);
        cardOutline.effectColor = new Color(0.2f, 0.25f, 0.35f, 0.9f);

        VerticalLayoutGroup cardV = card.AddComponent<VerticalLayoutGroup>();
        cardV.spacing = 18f;
        cardV.padding = new RectOffset(32, 32, 28, 28);
        cardV.childAlignment = TextAnchor.UpperCenter;
        cardV.childControlWidth = true;
        cardV.childControlHeight = true;
        cardV.childForceExpandWidth = true;
        cardV.childForceExpandHeight = false;

        TextMeshProUGUI cardTitle = CreateTmp(card.transform, "ControlsTitle", "Controls", 38, TextAlignmentOptions.Center, true);
        cardTitle.color = new Color(0.95f, 0.97f, 1f);
        cardTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

        TextMeshProUGUI body = CreateTmp(card.transform, "ControlsBody", "", 24, TextAlignmentOptions.Center, true);
        body.text =
            "<b>WASD</b> — Move\n\n" +
            "<b>Space</b> — Jump in <color=#7ec8e3>3D</color> / Dash in <color=#c4a86e>2D</color>";
        body.color = new Color(0.92f, 0.93f, 0.96f);
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Overflow;
        body.fontStyle = FontStyles.Normal;
        body.gameObject.AddComponent<LayoutElement>().preferredHeight = 180f;

        Button backBtn = CreateStyledButton(card.transform, "CloseControls", "Back", CloseControls, BtnPrimary, BtnPrimaryHover);
        backBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

        _controlsPanel.SetActive(false);
        _controlsPanelBuilt = true;
    }

    private void BuildUpgradePanelUi()
    {
        if (_upgradePanelBuilt || _canvasRootRt == null)
            return;

        GetCanvasViewSize(out float viewW, out float viewH);
        ComputeUpgradeSegLayout(viewW, viewH, out float segSize, out float segGap, out float labelW, out float arrowBtnSize, out float cardHeight, out float cardSideMargin);

        Transform bg = _canvasRootRt.GetChild(0);
        _upgradePanel = new GameObject("UpgradePanel");
        _upgradePanel.transform.SetParent(bg, false);
        RectTransform upRt = _upgradePanel.AddComponent<RectTransform>();
        StretchFull(upRt);
        Image dim = _upgradePanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        GameObject card = new GameObject("UpgradeCard");
        card.transform.SetParent(_upgradePanel.transform, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0f, 0.5f);
        cardRt.anchorMax = new Vector2(1f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.offsetMin = new Vector2(cardSideMargin, -cardHeight * 0.5f);
        cardRt.offsetMax = new Vector2(-cardSideMargin, cardHeight * 0.5f);
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = PanelCard;
        cardImg.raycastTarget = true;

        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectDistance = new Vector2(2f, -2f);
        cardOutline.effectColor = new Color(0.2f, 0.25f, 0.35f, 0.9f);

        VerticalLayoutGroup cardV = card.AddComponent<VerticalLayoutGroup>();
        cardV.spacing = 16f;
        cardV.padding = new RectOffset(28, 28, 22, 22);
        cardV.childAlignment = TextAnchor.UpperCenter;
        cardV.childControlWidth = true;
        cardV.childControlHeight = true;
        cardV.childForceExpandWidth = true;
        cardV.childForceExpandHeight = false;

        TextMeshProUGUI cardTitle = CreateTmp(card.transform, "CardTitle", "Upgrades", 38, TextAlignmentOptions.Center, true);
        cardTitle.color = new Color(0.95f, 0.97f, 1f);
        cardTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

        _panelGoldText = CreateTmp(card.transform, "PanelGold", "Gold: 0", 28, TextAlignmentOptions.Center, true);
        _panelGoldText.color = new Color(1f, 0.92f, 0.45f);
        _panelGoldText.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

        BuildAbilityRow(card.transform, _rowPower, "RowPower", DraftUpgradePower, segSize, segGap, labelW, arrowBtnSize);
        BuildAbilityRow(card.transform, _rowSpeed, "RowSpeed", DraftUpgradeSpeed, segSize, segGap, labelW, arrowBtnSize);
        BuildAbilityRow(card.transform, _rowHp, "RowHp", DraftUpgradeHp, segSize, segGap, labelW, arrowBtnSize);

        CreateSpacer(card.transform, 8f);

        GameObject btnRow = new GameObject("SaveBackRow");
        btnRow.transform.SetParent(card.transform, false);
        HorizontalLayoutGroup btnRowH = btnRow.AddComponent<HorizontalLayoutGroup>();
        btnRowH.spacing = 16f;
        btnRowH.childAlignment = TextAnchor.MiddleCenter;
        btnRowH.childControlWidth = true;
        btnRowH.childControlHeight = true;
        btnRowH.childForceExpandWidth = true;
        btnRowH.childForceExpandHeight = false;
        btnRow.AddComponent<LayoutElement>().preferredHeight = 60f;

        Button saveBtn = CreateStyledButton(btnRow.transform, "SaveUpgrades", "Save", SaveUpgrades, BtnSave, BtnSaveHover);
        saveBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

        Button backBtn = CreateStyledButton(btnRow.transform, "CancelUpgrades", "Back", CloseUpgrades, BtnPrimary, BtnPrimaryHover);
        backBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

        _upgradePanel.SetActive(false);
        _upgradePanelBuilt = true;
    }

    /// <summary>Root canvas size in pixels (overlay, no CanvasScaler).</summary>
    private void GetCanvasViewSize(out float viewW, out float viewH)
    {
        if (_canvasRootRt != null && _canvasRootRt.rect.width > 2f && _canvasRootRt.rect.height > 2f)
        {
            viewW = _canvasRootRt.rect.width;
            viewH = _canvasRootRt.rect.height;
            return;
        }

        Rect safe = Screen.safeArea;
        viewW = Mathf.Max(safe.width, 100f);
        viewH = Mathf.Max(safe.height, 100f);
    }

    /// <summary>
    /// Sizes blocks from canvas width; shrinks until card fits vertically (no 980px clamp).
    /// </summary>
    private static void ComputeUpgradeSegLayout(
        float viewW,
        float viewH,
        out float segSize,
        out float segGap,
        out float labelWidth,
        out float arrowBtnSize,
        out float cardHeight,
        out float cardSideMargin)
    {
        viewW = Mathf.Max(viewW, 120f);
        viewH = Mathf.Max(viewH, 200f);

        cardSideMargin = Mathf.Clamp(viewW * 0.022f, 20f, 56f);

        const float cardPaddingH = 28f;
        const float rowPadH = 4f;
        const float hBetweenBlocks = 12f;
        // padding 22*2 + title + gold + spacer + save/back row + 6 * layout spacing 16
        const float chrome = 44f + 52f + 42f + 8f + 60f + 6f * 16f;

        Rect safe = Screen.safeArea;
        float maxCardH = viewH * 0.88f - 24f;
        if (safe.height > 10f)
            maxCardH = Mathf.Min(maxCardH, safe.height * 0.9f - 20f);
        maxCardH = Mathf.Min(maxCardH, viewH * 0.92f);
        if (maxCardH > viewH * 0.9f)
            maxCardH = viewH * 0.9f;

        labelWidth = Mathf.Clamp(viewW * 0.1f, 120f, 200f);
        segGap = Mathf.Clamp(viewW * 0.006f, 5f, 14f);

        float innerCardW = viewW - 2f * cardSideMargin;
        float contentW = innerCardW - 2f * cardPaddingH;
        float rowInnerW = contentW - 2f * rowPadH;

        arrowBtnSize = Mathf.Clamp(viewW * 0.088f, 48f, 96f);

        float stripW = rowInnerW - labelWidth - arrowBtnSize - 2f * hBetweenBlocks;
        stripW = Mathf.Max(stripW, 120f);

        segSize = (stripW - 9f * segGap) / 10f;
        segSize = Mathf.Clamp(segSize, 32f, 200f);

        float rowH = Mathf.Max(segSize, arrowBtnSize) + 10f;
        cardHeight = chrome + 3f * rowH;

        int guard = 0;
        while (cardHeight > maxCardH && segSize > 20f && guard++ < 120)
        {
            segSize -= 1f;
            arrowBtnSize = Mathf.Max(36f, arrowBtnSize - 0.4f);
            rowH = Mathf.Max(segSize, arrowBtnSize) + 6f;
            cardHeight = chrome + 3f * rowH;
        }

        guard = 0;
        while (cardHeight > maxCardH && segSize > 14f && guard++ < 80)
        {
            segSize -= 0.75f;
            arrowBtnSize = Mathf.Max(32f, arrowBtnSize - 0.5f);
            rowH = Mathf.Max(segSize, arrowBtnSize) + 4f;
            cardHeight = chrome + 3f * rowH;
        }
    }

    private void BuildAbilityRow(
        Transform parent,
        AbilityRowRefs row,
        string rowName,
        UnityEngine.Events.UnityAction onUpgrade,
        float segSize,
        float segGap,
        float labelWidth,
        float arrowBtnSize)
    {
        GameObject rowGo = new GameObject(rowName);
        rowGo.transform.SetParent(parent, false);

        HorizontalLayoutGroup h = rowGo.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        h.padding = new RectOffset(0, 0, 6, 6);

        float rowH = Mathf.Max(segSize, arrowBtnSize) + 12f;
        LayoutElement rowLe = rowGo.AddComponent<LayoutElement>();
        rowLe.preferredHeight = rowH;
        rowLe.minHeight = rowH;
        rowLe.flexibleWidth = 1f;

        row.Label = CreateTmp(rowGo.transform, "Label", "Power", 23, TextAlignmentOptions.Left, false);
        row.Label.alignment = TextAlignmentOptions.MidlineLeft;
        row.Label.enableWordWrapping = true;
        row.Label.overflowMode = TextOverflowModes.Overflow;
        RectTransform labelRt = row.Label.rectTransform;
        labelRt.sizeDelta = new Vector2(labelWidth, rowH);
        LayoutElement labelLe = row.Label.gameObject.AddComponent<LayoutElement>();
        labelLe.preferredWidth = labelWidth;
        labelLe.flexibleWidth = 0f;
        labelLe.flexibleHeight = 0f;

        GameObject segStrip = new GameObject("Segments");
        segStrip.transform.SetParent(rowGo.transform, false);
        HorizontalLayoutGroup segH = segStrip.AddComponent<HorizontalLayoutGroup>();
        float stripH = Mathf.Max(segSize, arrowBtnSize);
        segH.spacing = segGap;
        segH.padding = new RectOffset(0, 0, 0, 0);
        segH.childAlignment = TextAnchor.MiddleCenter;
        segH.childControlWidth = true;
        segH.childControlHeight = true;
        segH.childForceExpandWidth = true;
        segH.childForceExpandHeight = false;
        LayoutElement segStripLe = segStrip.AddComponent<LayoutElement>();
        segStripLe.preferredWidth = 0f;
        segStripLe.flexibleWidth = 1f;
        segStripLe.minWidth = 100f;
        segStripLe.preferredHeight = stripH;
        segStripLe.flexibleHeight = 0f;

        for (int i = 0; i < row.Segments.Length; i++)
        {
            GameObject sg = new GameObject($"Seg{i}");
            sg.transform.SetParent(segStrip.transform, false);
            RectTransform srt = sg.AddComponent<RectTransform>();
            srt.sizeDelta = Vector2.zero;
            Image img = sg.AddComponent<Image>();
            img.color = SegDim;
            LayoutElement sle = sg.AddComponent<LayoutElement>();
            sle.preferredWidth = 0f;
            sle.flexibleWidth = 1f;
            sle.minWidth = 6f;
            sle.preferredHeight = stripH;
            sle.flexibleHeight = 0f;
            row.Segments[i] = img;
        }

        row.UpBtn = CreateArrowButton(rowGo.transform, $"{rowName}Up", onUpgrade, arrowBtnSize);
        LayoutElement upLe = row.UpBtn.gameObject.AddComponent<LayoutElement>();
        upLe.preferredWidth = arrowBtnSize;
        upLe.flexibleWidth = 0f;
        upLe.preferredHeight = arrowBtnSize;
        upLe.flexibleHeight = 0f;
    }

    private Button CreateArrowButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick, float btnSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(btnSize, btnSize);
        Image img = go.AddComponent<Image>();
        img.color = BtnPrimary;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = BtnPrimary;
        colors.highlightedColor = BtnPrimaryHover;
        colors.pressedColor = new Color32(45, 70, 100, 255);
        colors.selectedColor = BtnPrimary;
        colors.disabledColor = new Color32(55, 60, 72, 200);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        RectTransform trt = textGo.AddComponent<RectTransform>();
        StretchFull(trt);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "→";
        tmp.fontSize = Mathf.Max(24f, btnSize * 0.42f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        ApplyFont(tmp);
        return btn;
    }

    private static LayoutElement CreateSpacer(Transform parent, float height)
    {
        GameObject go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return le;
    }

    private static Image CreatePanel(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        return go.AddComponent<Image>();
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateTmp(
        Transform parent,
        string name,
        string text,
        float size,
        TextAlignmentOptions align,
        bool layoutDriven)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        if (!layoutDriven)
        {
            rt.sizeDelta = new Vector2(900f, 100f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 0f);
        }

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        ApplyFont(tmp);
        tmp.outlineWidth = 0.18f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        return tmp;
    }

    private Button CreateStyledButton(
        Transform parent,
        string name,
        string label,
        UnityEngine.Events.UnityAction onClick,
        Color32 normal,
        Color32 highlighted)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(480f, 58f);
        Image img = go.AddComponent<Image>();
        img.color = normal;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = new Color32(
            (byte)Mathf.Clamp(normal.r * 3 / 4, 0, 255),
            (byte)Mathf.Clamp(normal.g * 3 / 4, 0, 255),
            (byte)Mathf.Clamp(normal.b * 3 / 4, 0, 255),
            normal.a);
        colors.selectedColor = normal;
        colors.disabledColor = new Color32(80, 80, 90, 180);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        RectTransform trt = textGo.AddComponent<RectTransform>();
        StretchFull(trt);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        ApplyFont(tmp);
        return btn;
    }

    private static void ApplyFont(TextMeshProUGUI tmp)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        if (font != null)
            tmp.font = font;
    }
}
