using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ModeSwitcher))] 
public class PlayerController : MonoBehaviour
{
    [Header("Animation")]
    private Animator animator; // 内部引用 Y Bot 的动画机

    [Header("Attack hit detection")]
    public float attackHeightOffset = 1.0f;

    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 5f;

    [Header("3D Ground & Jump")]
    [SerializeField] private float groundCheckOriginOffset = 0.1f;
    [SerializeField] private float groundCheckDistance = 0.22f;
    [SerializeField] private float jumpCooldown = 0.12f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("3D Gravity & Fall")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public float maxFallSpeed = 20f;

    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 5f;

    [Header("References")]
    public Transform vcam3DTransform;
    public Transform mainCameraTransform;

    [Header("Visual Layers")]
    public GameObject visual2D;
    public GameObject visual3D;
    public SpriteRenderer spriteRenderer2D;

    [Header("3D Camera Facing")]
    [SerializeField] private float rotation3DAlignDegreesPerSecond = 540f;

    [Header("2D Facing")]
    [SerializeField] private float facing2DDegreesPerSecond = 720f;

    [Header("Cursor")]
    [SerializeField] private bool confineCursorIn2D = true;

    [Header("2D Melee Attack")]
    public float attackRadius = 3.0f;
    public float attackAngle = 120f;
    public float meleeCooldown = 0.75f;
    public string enemyLayerName = "Enemy";
    public float meleeDamage = 10f;
    public float knockbackForce = 15f;

    [Header("3D Melee Settings")]
    public Vector3 melee3DBoxSize = new Vector3(2.2f, 2f, 3.3f);
    public float melee3DBoxDistance = 1.5f;
    public float melee3DBoxCenterYOffset = 0.5f;
    public float melee3DDamage = 20f;
    [Tooltip("强化攻击（如 EnhancedAttack）伤害，与 melee3DDamage 无关；默认 40，可在 Inspector 单独调。")]
    public float melee3DEnhancedDamage = 60f;
    public float melee3DCooldown = 0.75f;
    public float melee3DKnockbackForce = 18f;

    [Header("3D Combo Settings")]
    [Tooltip("每段攻击动画的最短持续时间（秒），应大致等于你最长的一段攻击动画长度。")]
    public float comboStepMinDuration = 0.65f;
    [Tooltip("连招窗口：上一段结束后多久内可以接出下一段。")]
    public float comboWindow = 1.2f;
    private int currentComboStep = 0;
    private float lastAttackTime = -10f;
    private float comboStepLockedUntil = -10f;
    private bool hasBufferedAttack;

    [Header("3D Melee — Execution")]
    [SerializeField] private string executionZoneTag = "ExecutionZone";
    [SerializeField] private bool enablePlungeStateExecute = true;
    [SerializeField] private float plungeSnapDuration = 0.1f;
    [SerializeField] private float plungeArmMinFallSpeed = -0.1f;
    [SerializeField] private float plungeExecuteMinFallSpeed = -1.0f;
    public float minPlungeSpeed = -3f;
    public float weakPointPlungeFailScratchDamage = 0f;

    [Header("Attack Movement")]
    [Tooltip("攻击期间移动速度的倍率（0.15 = 15%）")]
    [SerializeField] private float attackMoveSpeedMultiplier = 0.15f;
    [Tooltip("前冲在攻击触发后延迟多久开始（秒），对齐挥刀发力帧")]
    [SerializeField] private float lungeDelay = 0.12f;
    [Tooltip("前冲持续时间（秒），极短 = 瞬移手感")]
    [SerializeField] private float lungeDuration = 0.04f;
    [Tooltip("2D 攻击的前冲距离（米）")]
    [SerializeField] private float lunge2DDistance = 0.4f;
    [Tooltip("3D 每段连招的前冲距离（米），依次对应第 1、2、3 下（第 3 段若使用动画曲线驱动则仅作备用）")]
    [SerializeField] private float[] lunge3DDistances = new float[] { 0.4f, 0.2f, 0.9f };
    [Tooltip("3D：第三段 / 强化攻击动画里 Root 曲线写入的 Float 参数名（单位建议：米/秒，沿面朝方向）。")]
    [SerializeField] private string lungeSpeedAnimatorParam = "LungeSpeed";

    [Header("Perfect Parry — Dimension Strike")]
    [Tooltip("按 F 切维后的极短窗口：在此时间内受击则免疫并触发强化大招（使用真实时间）。")]
    public bool isParryWindowActive { get; private set; }
    public float parryWindowDuration = 0.35f;
    [Tooltip("完美闪避成功后子弹时间的 Time.timeScale（仅 EnhancedAttack 播放到下方比例前保持）。")]
    [SerializeField] private float perfectStrikeBulletTimeScale = 0.1f;
    [Tooltip("EnhancedAttack 当前片段 normalizedTime 达到该比例（0~1）时恢复为正常时间流速；之后动画仍以正常速度播完。")]
    [SerializeField] private float perfectStrikeRestoreNormalizedTime = 0.08f;
    [Tooltip("进入子弹时间后、触发 EnhancedAttack 前的短暂停顿（真实秒）；0 = 立刻触发。")]
    [SerializeField] private float perfectStrikeIntroDelayRealtime = 0f;
    [Tooltip("等待动画结束的最长真实时间（秒），防止 Animator 配置异常导致永远卡慢动作）。")]
    [SerializeField] private float perfectStrikeMaxRealtimeWait = 45f;
    [SerializeField] private string enhancedAttackTriggerParam = "EnhancedAttack";

    [Header("Weapon Visual")]
    public WeaponSwingVisual weaponSwingVisual;

    private Rigidbody rb;
    private ModeSwitcher modeSwitcher; 
    private GhostTrail ghostTrail;

    private Vector3 movementInput;
    private bool isGrounded;
    private float nextJumpAllowedTime = -100f;
    private bool isDashing = false;
    private float lastDashTime = -100f;
    private float nextMeleeAttackTime;
    private float next3DMeleeAttackTime;
    private bool perfectDimensionStrikeInProgress;
    private Coroutine openParryWindowCoroutine;
    private Health cachedPlayerHealth;
    private int enemyLayerMask;
    private bool jumpInputHeld;
    private float attackFacingEndTime;
    private float lungeStartTime = -10f;
    private float lungeEndTime = -10f;
    private Vector3 lungeVelocity;
    private float attackSlowUntil = -10f;
    private bool isPlunging;

    public bool IsPlunging => isPlunging;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        modeSwitcher = GetComponent<ModeSwitcher>(); 
        ghostTrail = GetComponentInChildren<GhostTrail>(); 
        
        bool startIn2D = modeSwitcher != null && modeSwitcher.is2DMode;
        SwitchAnimator(startIn2D);

        nextMeleeAttackTime = -Mathf.Infinity;
        next3DMeleeAttackTime = -Mathf.Infinity;
        enemyLayerMask = LayerMask.GetMask(enemyLayerName);

        if (weaponSwingVisual == null)
            weaponSwingVisual = GetComponentInChildren<WeaponSwingVisual>();

        if (mainCameraTransform == null && Camera.main != null)
            mainCameraTransform = Camera.main.transform;

        rb.linearDamping = 0f;

        if (modeSwitcher != null)
        {
            modeSwitcher.OnDimensionChanged += OnPlayerDimensionChanged;
            UpdateCursorState(!modeSwitcher.is2DMode);
            ApplyVisualLayer(modeSwitcher.is2DMode);
        }

        cachedPlayerHealth = GetComponent<Health>() ?? GetComponentInParent<Health>();
        if (cachedPlayerHealth != null)
            cachedPlayerHealth.CustomTryAbsorbDamage += TryPerfectParryAbsorbDamage;

        PauseMenuController.EnsureExists();
    }

    private void OnDestroy()
    {
        if (cachedPlayerHealth != null)
            cachedPlayerHealth.CustomTryAbsorbDamage -= TryPerfectParryAbsorbDamage;

        if (modeSwitcher != null)
            modeSwitcher.OnDimensionChanged -= OnPlayerDimensionChanged;
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;

        bool levelCompleteUi = LevelManager.IsLevelCompleteUiVisible;
        if (levelCompleteUi) UnlockCursorForMenus();

        if (Keyboard.current != null)
            jumpInputHeld = Keyboard.current.spaceKey.isPressed;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && !isDashing)
        {
            if (modeSwitcher.is2DMode) Dash();
            else TryJump3D();
        }

        if (isDashing) return;

        bool pointerOverUi = IsPointerOverUiThisFrame();
        bool cursorRecapturedThisFrame = false;
        
        if (modeSwitcher != null && !modeSwitcher.is2DMode && !levelCompleteUi && !pointerOverUi &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            UpdateCursorState(true);
            cursorRecapturedThisFrame = true;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1;
            if (Keyboard.current.sKey.isPressed) input.y -= 1;
            if (Keyboard.current.aKey.isPressed) input.x -= 1;
            if (Keyboard.current.dKey.isPressed) input.x += 1;
        }
        movementInput = new Vector3(input.x, 0, input.y).normalized;

        if (modeSwitcher != null && modeSwitcher.CurrentMode == ModeSwitcher.GameMode.Mode3D)
            Handle3DCameraFacing();

        if (modeSwitcher != null)
        {
            if (modeSwitcher.is2DMode)
                Handle2DFacing();
            else
                ; // 3D facing handled earlier via Handle3DCameraFacing

            if (!perfectDimensionStrikeInProgress)
            {
                bool wantsAttack = !levelCompleteUi && Mouse.current != null &&
                                   Mouse.current.leftButton.wasPressedThisFrame;
                if (!modeSwitcher.is2DMode)
                    wantsAttack = wantsAttack && !cursorRecapturedThisFrame && !pointerOverUi;

                bool ready = Time.time >= comboStepLockedUntil;

                if (ready && animator != null && currentComboStep > 0)
                {
                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                    if (!state.loop && state.normalizedTime < 0.85f)
                        ready = false;
                }

                if (wantsAttack && !ready)
                    hasBufferedAttack = true;

                if (Time.time - lastAttackTime > comboWindow && ready)
                {
                    currentComboStep = 0;
                    hasBufferedAttack = false;
                }

                bool fireNow = ready && (wantsAttack || hasBufferedAttack);

                if (fireNow)
                {
                    hasBufferedAttack = false;
                    currentComboStep = (currentComboStep % 3) + 1;
                    lastAttackTime = Time.time;
                    comboStepLockedUntil = Time.time + comboStepMinDuration;
                    attackSlowUntil = Time.time + comboStepMinDuration;

                    if (animator != null)
                    {
                        animator.SetInteger("ComboStep", currentComboStep);
                        animator.SetTrigger(modeSwitcher.is2DMode ? "Attack2D" : "Attack3D");
                    }

                    if (modeSwitcher.is2DMode)
                    {
                        if (weaponSwingVisual != null) weaponSwingVisual.PlaySwing();
                        if (currentComboStep == 3)
                            ApplyLungeForward(lunge3DDistances[Mathf.Clamp(2, 0, lunge3DDistances.Length - 1)]);
                        PerformMeleeAttack();
                    }
                    // 3D：第三段前冲在 MovePlayer 中由 Animator 的 LungeSpeed 曲线驱动
                }
            }
        }
    }

    void LateUpdate()
    {
        if (modeSwitcher != null && modeSwitcher.is2DMode)
        {
            if (visual2D != null)
                visual2D.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (spriteRenderer2D != null)
                spriteRenderer2D.flipX = transform.forward.x < 0f;
        }
    }

    void FixedUpdate()
    {
        UpdateGroundedState3D();
        if (rb != null && rb.isKinematic) return;
        if (modeSwitcher != null && !modeSwitcher.is2DMode && isGrounded && isPlunging)
            isPlunging = false;
        
        MovePlayer();
        Apply3DVerticalGravityModifiers();
    }

    private void UpdateGroundedState3D()
    {
        if (modeSwitcher == null || modeSwitcher.is2DMode)
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = TryGetGroundHit(out _);
        }

        if (animator != null && modeSwitcher != null && !modeSwitcher.is2DMode)
            animator.SetBool("IsGrounded", isGrounded);
    }

    private void MovePlayer()
    {
        if (rb != null && rb.isKinematic) return;
        if (isDashing) return;

        // 3D：第三段 / 大招 — 动画曲线 LungeSpeed（米/秒）× 面朝方向，不受 WASD 干扰（输入已在下方清零）
        if (ShouldApplyAnimatorLungeSpeed3D())
        {
            float lungeSpeed = animator.GetFloat(lungeSpeedAnimatorParam);
            Vector3 forwardXZ = transform.forward;
            forwardXZ.y = 0f;
            if (forwardXZ.sqrMagnitude > 1e-8f)
                forwardXZ.Normalize();
            else
                forwardXZ = Vector3.forward;

            Vector3 xzVel = forwardXZ * lungeSpeed;
            rb.linearVelocity = new Vector3(xzVel.x, rb.linearVelocity.y, xzVel.z);
            animator.SetFloat("Speed", 0f);
            return;
        }

        // 前冲阶段：2D 第三段等仍使用短时固定速度突进
        if (Time.time >= lungeStartTime && Time.time < lungeEndTime)
        {
            if (modeSwitcher.is2DMode)
                rb.linearVelocity = lungeVelocity;
            else
                rb.linearVelocity = new Vector3(lungeVelocity.x, rb.linearVelocity.y, lungeVelocity.z);
            if (animator != null && modeSwitcher != null && !modeSwitcher.is2DMode)
                animator.SetFloat("Speed", 0f);
            return;
        }

        bool movementAttackSlow = Time.time < attackSlowUntil;
        if (!movementAttackSlow && currentComboStep > 0 && animator != null)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            movementAttackSlow = !state.loop && state.normalizedTime < 0.9f;
        }

        Vector3 moveInputEffective = movementInput;
        if (IsMovementAndFacingLockedForMelee())
            moveInputEffective = Vector3.zero;

        float currentSpeed = movementAttackSlow ? moveSpeed * attackMoveSpeedMultiplier : moveSpeed;

        if (modeSwitcher.is2DMode) 
        {
            Vector3 targetVelocity = moveInputEffective * currentSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        }
        else
        {
            if (vcam3DTransform == null) return; 
            Vector3 camForward = vcam3DTransform.forward;
            Vector3 camRight = vcam3DTransform.right;
            camForward.y = 0; camRight.y = 0;
            camForward.Normalize(); camRight.Normalize();

            Vector3 moveDir = camForward * moveInputEffective.z + camRight * moveInputEffective.x;
            rb.linearVelocity = new Vector3(moveDir.x * currentSpeed, rb.linearVelocity.y, moveDir.z * currentSpeed);
        }

        if (animator != null && modeSwitcher != null && !modeSwitcher.is2DMode)
        {
            float speedParam = moveInputEffective.magnitude;
            if (movementAttackSlow && !IsMovementAndFacingLockedForMelee())
                speedParam *= attackMoveSpeedMultiplier;
            if (movementAttackSlow && IsMovementAndFacingLockedForMelee())
                speedParam = 0f;
            animator.SetFloat("Speed", speedParam);
        }
    }

    private void TryJump3D()
    {
        if (modeSwitcher == null || modeSwitcher.is2DMode) return;
        if (rb != null && rb.isKinematic) return;
        if (Time.time < nextJumpAllowedTime) return;
        if (!TryGetGroundHit(out _)) return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        isGrounded = false;
        nextJumpAllowedTime = Time.time + jumpCooldown;

        // 同步起跳信号给动画机
        if (animator != null && !modeSwitcher.is2DMode)
        {
            animator.SetTrigger("Jump");
            animator.SetBool("IsGrounded", false);
        }
    }

    // --- 下面是原本的辅助功能代码，保持不变 ---

    private bool TryGetGroundHit(out RaycastHit groundHit)
    {
        groundHit = default;
        Vector3 origin = transform.position + Vector3.up * groundCheckOriginOffset;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider != null && h.collider.transform.root != transform.root)
            {
                groundHit = h;
                return true;
            }
        }
        return false;
    }

    private void Apply3DVerticalGravityModifiers()
    {
        if (rb == null || rb.isKinematic || isDashing || modeSwitcher.is2DMode) return;
        Vector3 v = rb.linearVelocity;
        if (v.y < 0f) v += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        else if (v.y > 0f && !jumpInputHeld) v += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;
        rb.linearVelocity = v;
    }

    private void Dash()
    {
        if (Time.time >= lastDashTime + dashCooldown && movementInput.magnitude > 0.1f)
            StartCoroutine(PerformDashCoroutine());
    }

    private void Handle3DCameraFacing()
    {
        if (IsMovementAndFacingLockedForMelee())
            return;

        if (movementInput.sqrMagnitude < 0.01f) 
            return;

        if (vcam3DTransform == null) 
            return;

        // 3. 计算相对于摄像机的世界移动方向 (和 MovePlayer 里计算位移的逻辑一样)
        Vector3 camForward = vcam3DTransform.forward;
        Vector3 camRight = vcam3DTransform.right;
        camForward.y = 0; 
        camRight.y = 0;
        camForward.Normalize(); 
        camRight.Normalize();

        Vector3 moveDir = camForward * movementInput.z + camRight * movementInput.x;

        // 4. 让角色的身体平滑地旋转到这个移动方向
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotation3DAlignDegreesPerSecond * Time.deltaTime);
        }
    }

    private void Handle2DFacing()
    {
        if (IsMovementAndFacingLockedForMelee())
            return;

        Vector3 mouseDir = GetMouseWorldDirection();
        if (mouseDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(mouseDir, Vector3.up);
    }

    private Vector3 GetMouseWorldDirection()
    {
        if (Camera.main == null || Mouse.current == null) return transform.forward;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.000001f) return dir.normalized;
        }
        return transform.forward;
    }

    /// <summary>终结技突进：沿面朝方向，不依赖 WASD（第三段禁用移动时仍正确前冲）。</summary>
    private void ApplyLungeForward(float distance)
    {
        Vector3 dir = transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f)
            dir = Vector3.forward;
        dir.Normalize();
        lungeVelocity = dir * (distance / Mathf.Max(lungeDuration, 0.01f));
        lungeStartTime = Time.time + lungeDelay;
        lungeEndTime = lungeStartTime + lungeDuration;
    }

    /// <summary>3D：使用动画曲线 LungeSpeed 驱动位移的阶段（第三段终结技或完美反击强化演出）。</summary>
    private bool ShouldApplyAnimatorLungeSpeed3D()
    {
        if (animator == null || modeSwitcher == null || modeSwitcher.is2DMode)
            return false;
        if (perfectDimensionStrikeInProgress)
            return true;
        if (currentComboStep == 3 && IsThirdFinisherAttackLockActive())
            return true;
        return false;
    }

    /// <summary>禁 WASD、禁改朝向：第三段终结技演出，或完美反击 EnhancedAttack 整段协程期间。</summary>
    private bool IsMovementAndFacingLockedForMelee()
    {
        if (perfectDimensionStrikeInProgress)
            return true;
        return IsThirdFinisherAttackLockActive();
    }

    /// <summary>第三段攻击演出内：禁 WASD、禁改朝向（与 attackSlow / 当前攻击动画进度一致）。</summary>
    private bool IsThirdFinisherAttackLockActive()
    {
        if (currentComboStep != 3)
            return false;
        if (Time.time < attackSlowUntil)
            return true;
        if (animator != null)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.loop && state.normalizedTime < 0.9f)
                return true;
        }
        return false;
    }

    private Vector3 GetAttackBasePosition() { return transform.position + Vector3.up * attackHeightOffset; }

    private void PerformMeleeAttack()
    {
        if (enemyLayerMask == 0) return;
        Vector3 basePosition = GetAttackBasePosition();
        Collider[] hits = Physics.OverlapSphere(basePosition, attackRadius, enemyLayerMask);
        float halfAngle = attackAngle * 0.5f;
        Vector3 forward = transform.forward;
        var damaged = new HashSet<Health>();
        foreach (Collider hit in hits)
        {
            Vector3 toEnemy = hit.transform.position - basePosition; toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.000001f) continue;
            if (Vector3.Angle(forward, toEnemy) <= halfAngle)
            {
                Health health = hit.GetComponentInParent<Health>() ?? hit.GetComponent<Health>();
                if (health != null && damaged.Add(health))
                {
                    health.TakeDamage(meleeDamage);
                    Rigidbody enemyRb = hit.GetComponentInParent<Rigidbody>() ?? hit.GetComponent<Rigidbody>();
                    if (enemyRb != null) { enemyRb.linearVelocity = new Vector3(0f, enemyRb.linearVelocity.y, 0f); enemyRb.AddForce(toEnemy.normalized * knockbackForce, ForceMode.Impulse); }
                }
            }
        }
    }

    public void Perform3DMeleeAttack()
    {
        Perform3DMeleeAttackWithDamage(melee3DDamage);
    }

    /// <summary>强化攻击命中帧调用（与普通 3D 判定相同，伤害为 melee3DEnhancedDamage）。</summary>
    public void Perform3DMeleeEnhancedAttack()
    {
        Perform3DMeleeAttackWithDamage(melee3DEnhancedDamage);
    }

    private void Perform3DMeleeAttackWithDamage(float damage)
    {
        if (enemyLayerMask == 0) return;
        Vector3 basePosition = GetAttackBasePosition();
        Vector3 center = basePosition + transform.forward * melee3DBoxDistance + Vector3.up * melee3DBoxCenterYOffset;
        Collider[] hits = Physics.OverlapBox(center, melee3DBoxSize * 0.5f, transform.rotation, enemyLayerMask, QueryTriggerInteraction.Collide);
        var damagedHealth = new HashSet<Health>();
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("WeakPoint"))
            {
                WeakPointHitReceiver wp = hit.GetComponent<WeakPointHitReceiver>();
                if (wp != null && ((isPlunging && rb.linearVelocity.y < plungeExecuteMinFallSpeed) || (!isPlunging && rb.linearVelocity.y <= minPlungeSpeed)))
                {
                    if (wp.TryProcessWeakPointFrom3DMelee(this, rb, modeSwitcher)) { isPlunging = false; return; }
                }
                continue;
            }
            Health health = hit.GetComponentInParent<Health>() ?? hit.GetComponent<Health>();
            if (health != null && damagedHealth.Add(health))
            {
                health.TakeDamage(damage);
                Rigidbody enemyRb = hit.GetComponentInParent<Rigidbody>() ?? hit.GetComponent<Rigidbody>();
                if (enemyRb != null) { enemyRb.linearVelocity = new Vector3(0f, enemyRb.linearVelocity.y, 0f); enemyRb.AddForce((hit.transform.position - basePosition).normalized * melee3DKnockbackForce, ForceMode.Impulse); }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag(executionZoneTag) || modeSwitcher.is2DMode || !isPlunging || rb.linearVelocity.y >= plungeExecuteMinFallSpeed) return;
        Earthshaker boss = other.GetComponentInParent<Earthshaker>();
        if (boss == null || (boss.GetComponent<Health>() != null && !boss.GetComponent<Health>().IsAlive)) return;
        rb.linearVelocity = Vector3.zero;
        if (plungeSnapDuration > 0f) StartCoroutine(PlungeSnapAndExecuteRoutine(other.transform, boss));
        else { boss.ExecuteWeakPointInstantKill(); isPlunging = false; }
    }

    private IEnumerator PlungeSnapAndExecuteRoutine(Transform executionZone, Earthshaker boss)
    {
        rb.isKinematic = true; rb.useGravity = false;
        Vector3 start = transform.position; Vector3 end = executionZone.position;
        float t = 0f;
        while (t < plungeSnapDuration) { t += Time.deltaTime; transform.position = Vector3.Lerp(start, end, t / plungeSnapDuration); yield return null; }
        transform.position = end;
        if (boss != null) boss.ExecuteWeakPointInstantKill();
        isPlunging = false; rb.isKinematic = false; rb.useGravity = true;
    }

    private IEnumerator PerformDashCoroutine()
    {
        isDashing = true; lastDashTime = Time.time;
        if (ghostTrail != null) ghostTrail.StartTrail();
        rb.linearVelocity = new Vector3(movementInput.x * dashSpeed, 0f, movementInput.z * dashSpeed);
        yield return new WaitForSeconds(dashDuration);
        rb.linearVelocity = Vector3.zero;
        if (ghostTrail != null) ghostTrail.StopTrail();
        isDashing = false;
    }

    private void UpdateCursorState(bool is3D)
    {
        Cursor.lockState = is3D ? CursorLockMode.Locked : (confineCursorIn2D ? CursorLockMode.Confined : CursorLockMode.None);
        Cursor.visible = !is3D;
    }

    private void OnPlayerDimensionChanged(bool is2DMode)
    {
        UpdateCursorState(!is2DMode);
        ApplyVisualLayer(is2DMode);
        if (is2DMode) isPlunging = false;

        currentComboStep = 0;
        hasBufferedAttack = false;
        attackSlowUntil = -10f;
        comboStepLockedUntil = -10f;
        lungeEndTime = -10f;
        if (animator != null)
            animator.SetInteger("ComboStep", 0);

        SwitchAnimator(is2DMode);
    }

    private void SwitchAnimator(bool is2DMode)
    {
        GameObject target = is2DMode ? visual2D : visual3D;
        if (target != null)
            animator = target.GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void ApplyVisualLayer(bool is2DMode)
    {
        if (visual2D != null) visual2D.SetActive(is2DMode);
        if (visual3D != null) visual3D.SetActive(!is2DMode);
    }

    /// <summary>
    /// 由 <see cref="ModeSwitcher"/> 在即将执行普通切维前调用：开启短暂完美闪避判定窗口。
    /// </summary>
    public void BeginParryWindowFromDimensionSwitch()
    {
        if (openParryWindowCoroutine != null)
            StopCoroutine(openParryWindowCoroutine);
        openParryWindowCoroutine = StartCoroutine(OpenParryWindowRoutine());
    }

    private IEnumerator OpenParryWindowRoutine()
    {
        isParryWindowActive = true;
        yield return new WaitForSecondsRealtime(parryWindowDuration);
        isParryWindowActive = false;
        openParryWindowCoroutine = null;
    }

    /// <summary>
    /// 连接到 <see cref="Health.CustomTryAbsorbDamage"/>：窗口内受伤则免疫并触发强化连招演出（不再二次切维）。
    /// </summary>
    private bool TryPerfectParryAbsorbDamage(float _)
    {
        if (!isParryWindowActive)
            return false;

        if (openParryWindowCoroutine != null)
        {
            StopCoroutine(openParryWindowCoroutine);
            openParryWindowCoroutine = null;
        }
        isParryWindowActive = false;

        StartCoroutine(PerfectDimensionStrikeRoutine());
        return true;
    }

    /// <summary>打断普攻连招、前后摇与 Animator 攻击触发器。</summary>
    private void InterruptMeleeComboState()
    {
        currentComboStep = 0;
        hasBufferedAttack = false;
        attackSlowUntil = -10f;
        comboStepLockedUntil = -10f;
        lungeEndTime = -10f;
        if (animator != null)
        {
            animator.SetInteger("ComboStep", 0);
            animator.ResetTrigger("Attack2D");
            animator.ResetTrigger("Attack3D");
        }
    }

    private IEnumerator PerfectDimensionStrikeRoutine()
    {
        perfectDimensionStrikeInProgress = true;
        DimensionTimeScaleCoordinator.BeginPerfectParryStrikeOwnership();
        try
        {
            InterruptMeleeComboState();

            if (modeSwitcher != null)
                SwitchAnimator(modeSwitcher.is2DMode);

            Time.timeScale = perfectStrikeBulletTimeScale;
            Time.fixedDeltaTime = 0.02f * Mathf.Max(0.01f, Time.timeScale);

            if (perfectStrikeIntroDelayRealtime > 0f)
                yield return new WaitForSecondsRealtime(perfectStrikeIntroDelayRealtime);

            if (animator != null)
            {
                animator.SetInteger("ComboStep", 0);
                animator.SetTrigger(enhancedAttackTriggerParam);
            }

            if (animator == null)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                DimensionTimeScaleCoordinator.EndPerfectParryStrikeOwnership();
            }
            else
                yield return WaitForEnhancedAttackEarlyRestoreThenClipEnd();
        }
        finally
        {
            DimensionTimeScaleCoordinator.EndPerfectParryStrikeOwnership();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            perfectDimensionStrikeInProgress = false;
        }
    }

    /// <summary>
    /// 等待 EnhancedAttack：先保持子弹时间；片段 normalizedTime 达到 <see cref="perfectStrikeRestoreNormalizedTime"/> 时恢复全局时间并释放 Coordinator；
    /// 再继续等到该段结束：既支持「非 Loop 播到末尾」，也支持「切回 Idle 等 Loop 状态」（避免卡在 Idle 等到超时）。
    /// </summary>
    private IEnumerator WaitForEnhancedAttackEarlyRestoreThenClipEnd()
    {
        float restoreAt = Mathf.Clamp01(perfectStrikeRestoreNormalizedTime);
        float elapsed = 0f;
        bool restoredBulletTime = false;
        int enhancedAttackStateHash = 0;
        yield return null;
        yield return null;

        while (elapsed < perfectStrikeMaxRealtimeWait)
        {
            if (!animator.IsInTransition(0))
            {
                AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0);

                if (enhancedAttackStateHash == 0 && !s.loop && s.normalizedTime > 0.02f)
                    enhancedAttackStateHash = s.fullPathHash;

                if (!s.loop)
                {
                    if (!restoredBulletTime && s.normalizedTime >= restoreAt)
                    {
                        Time.timeScale = 1f;
                        Time.fixedDeltaTime = 0.02f;
                        DimensionTimeScaleCoordinator.EndPerfectParryStrikeOwnership();
                        restoredBulletTime = true;
                    }

                    if (enhancedAttackStateHash != 0 && s.fullPathHash == enhancedAttackStateHash && s.normalizedTime >= 0.98f)
                        yield break;
                }

                if (enhancedAttackStateHash != 0 && s.fullPathHash != enhancedAttackStateHash)
                    yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public void UnlockCursorForMenus()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RefreshCursorForGameMode()
    {
        if (modeSwitcher == null) return;
        UpdateCursorState(!modeSwitcher.is2DMode);
    }

    private static bool IsPointerOverUiThisFrame() { return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1); }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        Vector3 basePosition = GetAttackBasePosition();
        Handles.color = new Color(1f, 0f, 0f, 0.2f);
        Vector3 forward = transform.forward;
        Vector3 startingAngle = Quaternion.Euler(0, -attackAngle * 0.5f, 0) * forward;
        Handles.DrawSolidArc(basePosition, Vector3.up, startingAngle, attackAngle, attackRadius);
        Gizmos.color = Color.blue;
        Vector3 boxCenter = basePosition + transform.forward * melee3DBoxDistance + Vector3.up * melee3DBoxCenterYOffset;
        Matrix4x4 prev = Gizmos.matrix; Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, melee3DBoxSize); Gizmos.matrix = prev;
#endif
    }
}