using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Tooltip("拖入子物体里的 Slider")]
    public Slider healthSlider;
    
    [Tooltip("怪物的 Health 组件。如果不填，会自动去父物体上找")]
    public Health targetHealth; 

    void Start()
    {
        // 1. 自动寻址：如果没手动拖拽，自动去父节点（僵尸根节点）找 Health 组件
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<Health>();
        }

        // 2. 绑定监听：订阅 Health 脚本的 OnHealthChanged 事件
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged += UpdateHealth;
            
            // 初始化时隐藏满血血条
            gameObject.SetActive(false); 
        }
        else
        {
            Debug.LogWarning($"[EnemyHealthBar] 找不到 Health 组件！请检查层级: {gameObject.name}");
        }
    }

    void OnDestroy()
    {
        // 3. 释放监听：怪物死亡或销毁时，必须取消订阅，否则会导致内存泄漏！
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged -= UpdateHealth;
        }
    }

    // 当 Health 脚本触发 OnHealthChanged 时，会自动调用这个方法
    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        // 只要不是满血，就显示血条
        if (currentHealth < maxHealth && currentHealth > 0)
        {
            gameObject.SetActive(true); 
        }

        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
        
        // 如果血量归零，可以再次隐藏血条（如果怪物尸体还要留在场上的话）
        if (currentHealth <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}