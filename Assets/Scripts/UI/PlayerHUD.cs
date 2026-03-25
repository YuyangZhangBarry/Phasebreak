using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player health HUD: portrait + slider + TMP text. Listens to Health only.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    /*
     * === Editor hierarchy (Screen Space - Overlay) ===
     * 1) UI > Canvas. Render Mode = Screen Space - Overlay. Optional: Canvas Scaler (Scale With Screen Size).
     * 2) Create empty "PlayerHUD" as child of Canvas. Anchor: bottom-left. Add this script to PlayerHUD.
     * 3) Add Horizontal Layout Group to PlayerHUD: Child Alignment = Middle Left, Spacing ~ 8, Child Force Expand Height = on.
     * 4) Child "Portrait": UI > Image. Set width = height (e.g. 64x64) for square placeholder. Assign to portraitImage.
     * 5) Child "HealthBlock": empty RectTransform with Horizontal Layout Group OR single row:
     *    - UI > Slider (width ~ 220, height ~ 24). Min/Max in Inspector are overridden at runtime to match Health.
     *    - As child of Slider's Fill Area (or overlay sibling above Fill): UI > Text - TextMeshPro. Anchor stretch,
     *      margins 0. Center text. Assign to healthText. Set font size readable (~14-18).
     * 6) Assign player Health (on Player root). Slider fill = current/max HP (same as text).
     */

    [Header("References")]
    [Tooltip("Player Health to display.")]
    [SerializeField] private Health playerHealth;

    [Tooltip("Wide horizontal slider for HP.")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("TMP overlay for numeric HP (e.g. 100/100).")]
    [SerializeField] private TextMeshProUGUI healthText;

    [Tooltip("Square placeholder for future portrait art.")]
    [SerializeField] private Image portraitImage;

    [Header("Formatting")]
    [Tooltip("TMP format: {0} current, {1} max.")]
    [SerializeField] private string healthTextFormat = "{0:0}/{1:0}";

    private void OnEnable()
    {
        SubscribeHealth();
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
    }

    private void SubscribeHealth()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth.OnHealthChanged += HandleHealthChanged;
        HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void UnsubscribeHealth()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    /// <summary>
    /// Call after cross-scene load so HUD follows the persistent player's <see cref="Health"/>.
    /// </summary>
    public void BindToHealth(Health health)
    {
        if (isActiveAndEnabled)
            UnsubscribeHealth();

        playerHealth = health;

        if (isActiveAndEnabled)
            SubscribeHealth();
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (healthSlider != null)
        {
            // Use absolute HP on the Slider (min=0, max=MaxHealth) so the bar stays full at cur==max even when
            // max changes (e.g. roguelite upgrades). Normalized 0..1 only works if Inspector Max is exactly 1;
            // Unity's default Slider Max is 100, which makes full HP look ~1% or wrong after max HP changes.
            healthSlider.minValue = 0f;
            healthSlider.maxValue = Mathf.Max(0.0001f, max);
            healthSlider.value = Mathf.Clamp(current, 0f, max);
        }

        if (healthText != null)
            healthText.text = string.Format(healthTextFormat, current, max);
    }
}
