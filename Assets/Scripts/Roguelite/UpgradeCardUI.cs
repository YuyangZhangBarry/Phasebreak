using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One selectable upgrade card UI.
/// Pure UI component: shows visuals from UpgradeData and forwards click events.
/// </summary>
public class UpgradeCardUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Button component for the clickable card.")]
    [SerializeField] private Button cardButton;

    [Tooltip("Icon Image shown on the card.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Card title text (TMP).")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Card description text (TMP).")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    private UpgradeData currentData;

    private void Awake()
    {
        if (cardButton == null)
            cardButton = GetComponent<Button>();

        // Be robust: Button might be on a child, not on the same root object.
        if (cardButton == null)
            cardButton = GetComponentInChildren<Button>(true);

        if (cardButton == null)
            Debug.LogWarning($"[UpgradeCardUI] No Button found under '{name}'. Clicks will not work.", this);
        else
            Debug.Log($"[UpgradeCardUI] Awake found Button='{cardButton.gameObject.name}' on '{name}'.", this);
    }

    public void Setup(UpgradeData data, Action<UpgradeData> onClicked)
    {
        currentData = data;

        if (titleText != null)
            titleText.text = data != null ? data.cardName : string.Empty;

        if (descriptionText != null)
            descriptionText.text = data != null ? data.description : string.Empty;

        if (iconImage != null)
        {
            iconImage.sprite = data != null ? data.icon : null;
            iconImage.enabled = data != null && data.icon != null;
        }

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => onClicked?.Invoke(currentData));

            Debug.Log(
                $"[UpgradeCardUI] Setup on '{name}' with data='{(data != null ? data.cardName : "null")}'. Listener added to Button='{cardButton.gameObject.name}'.",
                this);
        }
        else
        {
            Debug.LogWarning($"[UpgradeCardUI] Setup skipped button listener because cardButton is null on '{name}'.", this);
        }
    }
}

