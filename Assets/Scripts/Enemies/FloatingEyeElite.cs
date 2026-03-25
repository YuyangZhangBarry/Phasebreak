using System.Collections;
using UnityEngine;

/// <summary>
/// 精英怪专属技能：每隔一段时间开启 360 度旋转激光。
/// 开启时会利用 isEliteSweeping 锁住基础 AI 的移动和索敌。
/// </summary>
[RequireComponent(typeof(FloatingEyeMovement))]
[RequireComponent(typeof(FloatingEyeAttack))]
[RequireComponent(typeof(Rigidbody))]
public class FloatingEyeElite : MonoBehaviour
{
    [Header("Laser Setup")]
    public LineRenderer laserLine;
    public float laserRange = 40f;
    
    [Header("Elite Ability Settings")]
    public float attackInterval = 10f;      // 每10秒放一次大招
    public float warningDuration = 1.5f;    // 闪烁预警时间
    public float laserDuration = 2f;        // 激光扫射持续时间
    public float rotationSpeed = 180f;      // 旋转速度(度/秒)，180就是2秒转一圈
    
    [Header("Laser Damage")]
    public float laserDamagePerTick = 15f;  // 扫到一次的伤害
    public float damageTickRate = 0.5f;     // 每0.5秒最多受一次伤(防秒杀)

    private FloatingEyeMovement _movementScript;
    private FloatingEyeAttack _attackScript;
    private Rigidbody _rb;
    private MeshRenderer _meshRenderer;
    private Color _originalColor;

    private bool _isUsingEliteSkill = false;
    private float _skillTimer;
    private float _nextDamageTime;

    void Start()
    {
        _movementScript = GetComponent<FloatingEyeMovement>();
        _attackScript = GetComponent<FloatingEyeAttack>();
        _rb = GetComponent<Rigidbody>();
        
        // 尝试获取材质用于闪烁红光
        _meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (_meshRenderer != null) _originalColor = _meshRenderer.material.color;

        if (laserLine != null) laserLine.enabled = false;
        _skillTimer = attackInterval;
    }

    void Update()
    {
        // 技能倒计时
        _skillTimer -= Time.deltaTime;
        if (_skillTimer <= 0 && !_isUsingEliteSkill)
        {
            StartCoroutine(ExecuteEliteSweep());
        }
    }

    private IEnumerator ExecuteEliteSweep()
    {
        _isUsingEliteSkill = true;

        // 1. 【核心上锁】：通知基础 AI 停止乱动和开火
        _movementScript.isEliteSweeping = true;
        _attackScript.isEliteSweeping = true;

        // 2. 预警阶段：红光闪烁
        float elapsed = 0;
        bool isRed = false;
        while (elapsed < warningDuration)
        {
            elapsed += 0.2f;
            isRed = !isRed;
            if (_meshRenderer != null)
                _meshRenderer.material.color = isRed ? Color.red : _originalColor;
            yield return new WaitForSeconds(0.2f);
        }
        if (_meshRenderer != null) _meshRenderer.material.color = _originalColor;

        // 3. 扫射阶段：开启激光，利用 Rigidbody 旋转
        if (laserLine != null) laserLine.enabled = true;
        
        float attackElapsed = 0;
        WaitForFixedUpdate fixedUpdateWait = new WaitForFixedUpdate();

        while (attackElapsed < laserDuration)
        {
            attackElapsed += Time.fixedDeltaTime;

            // 物理平滑旋转
            Quaternion rotDelta = Quaternion.Euler(0, rotationSpeed * Time.fixedDeltaTime, 0);
            _rb.MoveRotation(_rb.rotation * rotDelta);

            UpdateLaserVisuals();
            CheckLaserDamage();

            yield return fixedUpdateWait;
        }

        // 4. 收尾阶段：关闭激光
        if (laserLine != null) laserLine.enabled = false;
        
        // 【核心解锁】：归还控制权，继续吐子弹！
        _movementScript.isEliteSweeping = false;
        _attackScript.isEliteSweeping = false;

        _skillTimer = attackInterval;
        _isUsingEliteSkill = false;
    }

    private void UpdateLaserVisuals()
    {
        if (laserLine == null) return;
        
        // 【修改】：使用 laserLine 自身的坐标，完美继承你在 Inspector 里设置的 Y=1 偏移量！
        Vector3 startPoint = laserLine.transform.position;
        Vector3 endPoint = startPoint + transform.forward * laserRange;
        
        laserLine.SetPosition(0, startPoint);
        laserLine.SetPosition(1, endPoint);
    }

    private void CheckLaserDamage()
    {
        if (Time.time < _nextDamageTime) return;

        Vector3 startPoint = laserLine.transform.position;
        // 定义光柱的粗细（半径），比如 1.0f。这样即使起点在 Y=2，也能往下覆盖到 Y=1 的位置
        float laserThickness = 1.0f; 

        // 【核心修改】：用 SphereCast 代替 Raycast
        RaycastHit[] hits = Physics.SphereCastAll(startPoint, laserThickness, transform.forward, laserRange);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player"))
            {
                Health pHealth = hit.collider.GetComponentInParent<Health>();
                if (pHealth != null && pHealth.IsAlive)
                {
                    pHealth.TakeDamage(laserDamagePerTick);
                    _nextDamageTime = Time.time + damageTickRate; 
                    break; 
                }
            }
        }
    }
}