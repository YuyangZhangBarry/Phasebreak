using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen red flash when the player takes damage. Requires a full-rect UI Image (see setup in comments).
/// </summary>
[RequireComponent(typeof(Image))]
public class DamageVignette : MonoBehaviour
{
    /*
     * === Editor setup ===
     * 1) Under a Screen Space - Overlay Canvas, create UI > Image named "DamageVignette".
     * 2) Anchor Stretch-Stretch, Left/Right/Top/Bottom = 0 so it covers the full screen.
     * 3) Color = red, Image alpha = 0. Raycast Target = OFF.
     * 4) Add this script. Assign Player Health (same object as PlayerController / ModeSwitcher).
     */

    [Header("References")]
    [Tooltip("Player Health component to listen for damage.")]
    [SerializeField] private Health playerHealth;

    [Header("Flash")]
    [Tooltip("Alpha immediately after taking damage.")]
    [SerializeField] [Range(0f, 1f)] private float peakAlpha = 0.3f;

    [Tooltip("Seconds to fade from current alpha down to 0.")]
    [SerializeField] private float fadeDuration = 0.2f;

    private Image image;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        image = GetComponent<Image>();
        Color c = image.color;
        c.a = 0f;
        image.color = c;
    }

    private void OnEnable()
    {
        SubscribeDamage();
    }

    private void OnDisable()
    {
        UnsubscribeDamage();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    private void SubscribeDamage()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnTookDamage -= HandleTookDamage;
        playerHealth.OnTookDamage += HandleTookDamage;
    }

    private void UnsubscribeDamage()
    {
        if (playerHealth != null)
            playerHealth.OnTookDamage -= HandleTookDamage;
    }

    /// <summary>
    /// Call after cross-scene load so vignette follows the persistent player's <see cref="Health"/>.
    /// </summary>
    public void BindToHealth(Health health)
    {
        if (isActiveAndEnabled)
            UnsubscribeDamage();

        playerHealth = health;

        if (isActiveAndEnabled)
            SubscribeDamage();
    }

    private void HandleTookDamage(float _)
    {
        Color c = image.color;
        c.a = peakAlpha;
        image.color = c;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float elapsed = 0f;
        Color c = image.color;
        float startA = c.a;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = fadeDuration > 0f ? elapsed / fadeDuration : 1f;
            c.a = Mathf.Lerp(startA, 0f, t);
            image.color = c;
            yield return null;
        }

        c.a = 0f;
        image.color = c;
        fadeRoutine = null;
    }
}
