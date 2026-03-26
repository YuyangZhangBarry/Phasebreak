using System.Collections;
using UnityEngine;

/// <summary>
/// Elite enemy exclusive skill: periodically enables a 360-degree rotating laser.
/// When active, it uses <c>isEliteSweeping</c> to lock the base AI from moving/targeting.
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
    public float attackInterval = 10f;      // every 10 seconds
    public float warningDuration = 1.5f;    // warning flicker duration
    public float laserDuration = 2f;        // sweep laser duration
    public float rotationSpeed = 180f;      // rotation speed (deg/s); 180 = one rotation in 2 seconds
    
    [Header("Laser Damage")]
    public float laserDamagePerTick = 15f;  // damage per tick
    public float damageTickRate = 0.5f;     // max once per 0.5s (prevents instant kill)

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
        
        // Try to get material for red blinking
        _meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (_meshRenderer != null) _originalColor = _meshRenderer.material.color;

        if (laserLine != null) laserLine.enabled = false;
        _skillTimer = attackInterval;
    }

    void Update()
    {
        // Ability countdown
        _skillTimer -= Time.deltaTime;
        if (_skillTimer <= 0 && !_isUsingEliteSkill)
        {
            StartCoroutine(ExecuteEliteSweep());
        }
    }

    private IEnumerator ExecuteEliteSweep()
    {
        _isUsingEliteSkill = true;

        // 1) Core lock: notify base AI to stop moving and firing
        _movementScript.isEliteSweeping = true;
        _attackScript.isEliteSweeping = true;

        // 2) Warning phase: red flicker
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

        // 3) Sweep phase: enable laser and rotate using Rigidbody
        if (laserLine != null) laserLine.enabled = true;
        
        float attackElapsed = 0;
        WaitForFixedUpdate fixedUpdateWait = new WaitForFixedUpdate();

        while (attackElapsed < laserDuration)
        {
            attackElapsed += Time.fixedDeltaTime;

            // Smooth physical rotation
            Quaternion rotDelta = Quaternion.Euler(0, rotationSpeed * Time.fixedDeltaTime, 0);
            _rb.MoveRotation(_rb.rotation * rotDelta);

            UpdateLaserVisuals();
            CheckLaserDamage();

            yield return fixedUpdateWait;
        }

        // 4) End phase: disable laser
        if (laserLine != null) laserLine.enabled = false;
        
        // Core unlock: restore control and continue firing bullets
        _movementScript.isEliteSweeping = false;
        _attackScript.isEliteSweeping = false;

        _skillTimer = attackInterval;
        _isUsingEliteSkill = false;
    }

    private void UpdateLaserVisuals()
    {
        if (laserLine == null) return;
        
        // Use laserLine's own transform so the Inspector Y offset is preserved.
        Vector3 startPoint = laserLine.transform.position;
        Vector3 endPoint = startPoint + transform.forward * laserRange;
        
        laserLine.SetPosition(0, startPoint);
        laserLine.SetPosition(1, endPoint);
    }

    private void CheckLaserDamage()
    {
        if (Time.time < _nextDamageTime) return;

        Vector3 startPoint = laserLine.transform.position;
        // Define laser thickness (radius). For example, 1.0f ensures coverage downward even if the start point is at Y=2.
        float laserThickness = 1.0f; 

        // Core change: use SphereCastAll instead of Raycast
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