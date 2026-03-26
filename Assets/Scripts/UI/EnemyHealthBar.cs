using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Tooltip("Assign the Slider from a child object.")]
    public Slider healthSlider;
    
    [Tooltip("Monster Health component. If left empty, it will auto-find on the parent.")]
    public Health targetHealth; 

    void Start()
    {
        // 1) Auto-find: if not manually assigned, find Health on the parent (enemy root).
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<Health>();
        }

        // 2) Bind listener: subscribe to Health.OnHealthChanged
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged += UpdateHealth;
            
            // Hide full health bar at start.
            gameObject.SetActive(false); 
        }
        else
        {
            Debug.LogWarning($"[EnemyHealthBar] Could not find Health component. Please check hierarchy: {gameObject.name}");
        }
    }

    void OnDestroy()
    {
        // 3) Unsubscribe: required on enemy death/destruction to avoid memory leaks.
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged -= UpdateHealth;
        }
    }

    // Called automatically when Health triggers OnHealthChanged.
    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        // Show health bar when not full health.
        if (currentHealth < maxHealth && currentHealth > 0)
        {
            gameObject.SetActive(true); 
        }

        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
        
        // If health reaches zero, hide the bar again (if the corpse remains in the scene).
        if (currentHealth <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}