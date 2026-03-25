using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Generic health pool with optional post-hit invincibility. Fires events only — no UI references.
/// </summary>
[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [Header("Values")]
    [Tooltip("Maximum hit points.")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Starting current HP. If 0 or less at Awake, fills to Max Health.")]
    [SerializeField] private float currentHealth = 100f;

    [Header("Invincibility (I-frames)")]
    [Tooltip("Seconds of invulnerability after taking damage. 0 disables I-frames.")]
    [SerializeField] private float invincibilityDuration = 0f;

    [Header("Debug")]
    [Tooltip("Log to Console when TakeDamage successfully applies (useful to tell real hits from UI bugs). Disable on noisy enemies if needed.")]
    [SerializeField] private bool logDamageToConsole = true;

    private float invincibleUntil;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0f;
    public bool IsInvulnerable => Time.time < invincibleUntil;

    /// <summary>Fired as (currentHealth, maxHealth) after any change that updates HP.</summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>Fired with damage actually applied (after I-frame check). Not fired for 0 damage.</summary>
    public event Action<float> OnTookDamage;

    public event Action OnDie;

    [Serializable]
    public class FloatFloatUnityEvent : UnityEvent<float, float> { }

    [Serializable]
    public class FloatUnityEvent : UnityEvent<float> { }

    [Header("UnityEvents (optional)")]
    [Tooltip("Invoked as (currentHealth, maxHealth).")]
    public FloatFloatUnityEvent onHealthChangedUnity;

    [Tooltip("Invoked with applied damage amount.")]
    public FloatUnityEvent onTookDamageUnity;

    public UnityEvent onDieUnity;

    private void Awake()
    {
        if (maxHealth <= 0f)
            maxHealth = 100f;

        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        RaiseHealthChanged();
    }

    /// <summary>
    /// Apply damage. Respects I-frames unless ignoreInvincibility is true.
    /// </summary>
    public void TakeDamage(float damageAmount, bool ignoreInvincibility = false)
    {
        if (!IsAlive || damageAmount <= 0f)
            return;

        if (!ignoreInvincibility && IsInvulnerable)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);

        if (logDamageToConsole)
        {
            string role = CompareTag("Player") ? "Player took damage" : "Entity took damage";
            Debug.Log($"[Damage] {role}: '{gameObject.name}' −{damageAmount} HP → now {currentHealth}/{maxHealth}", this);
        }

        OnTookDamage?.Invoke(damageAmount);
        onTookDamageUnity?.Invoke(damageAmount);
        RaiseHealthChanged();

        if (invincibilityDuration > 0f && !ignoreInvincibility)
            invincibleUntil = Time.time + invincibilityDuration;

        if (currentHealth <= 0f)
            Die();
    }

    /// <summary>
    /// Heal up to max. Does not trigger OnTookDamage.
    /// </summary>
    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RaiseHealthChanged();
    }

    /// <summary>
    /// Changes Max HP and optionally keeps the current HP proportion.
    /// </summary>
    public void SetMaxHealth(float newMaxHealth, bool keepCurrentProportion = true)
    {
        newMaxHealth = Mathf.Max(0f, newMaxHealth);
        if (newMaxHealth <= 0f)
            newMaxHealth = 1f; // Prevent divide-by-zero / invalid max.

        float oldMax = maxHealth;
        float ratio = (oldMax > 0f && keepCurrentProportion) ? (currentHealth / oldMax) : 1f;

        maxHealth = newMaxHealth;
        currentHealth = Mathf.Clamp(newMaxHealth * ratio, 0f, maxHealth);

        RaiseHealthChanged();
    }

    /// <summary>
    /// Forces max/current HP (e.g. after LoadScene when serialized defaults might fight roguelite stats).
    /// Does not trigger OnTookDamage.
    /// </summary>
    public void ApplyRuntimeHealthState(float newMaxHealth, float newCurrentHealth)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = Mathf.Clamp(newCurrentHealth, 0f, maxHealth);
        RaiseHealthChanged();
    }

    /// <summary>
    /// Reserved for future use: reset I-frame window (e.g. dash iframes from another system).
    /// </summary>
    public void ClearInvincibility()
    {
        invincibleUntil = 0f;
    }

    /// <summary>
    /// Reserved for future use: force invulnerable until time.
    /// </summary>
    public void SetInvincibleUntil(float time)
    {
        invincibleUntil = Mathf.Max(invincibleUntil, time);
    }

    private void Die()
    {
        OnDie?.Invoke();
        onDieUnity?.Invoke();
    }

    private void RaiseHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        onHealthChangedUnity?.Invoke(currentHealth, maxHealth);
    }
}
