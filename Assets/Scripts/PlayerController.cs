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
    [Header("Attack hit detection")]
    [Tooltip("Height offset for the attack hit center (distance above the feet pivot).")]
    public float attackHeightOffset = 1.0f;

    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 5f;

    [Header("3D Ground & Jump")]
    [Tooltip("Ground check ray starts this far above the pivot (feet) so the origin is not inside the floor mesh/collider.")]
    [SerializeField] private float groundCheckOriginOffset = 0.1f;

    [Tooltip("Max distance cast downward from that origin to count as grounded / allow a jump.")]
    [SerializeField] private float groundCheckDistance = 0.22f;

    [Tooltip("Seconds after a successful jump before another jump is accepted (prevents air-chaining).")]
    [SerializeField] private float jumpCooldown = 0.12f;

    [Tooltip("Layers considered ground for ray checks. Default: Everything.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("3D Gravity & Fall (FixedUpdate)")]
    [Tooltip("Extra gravity scale while falling (vy < 0). Total ≈ Physics.gravity * fallMultiplier.")]
    public float fallMultiplier = 2.5f;

    [Tooltip("Extra gravity while rising if jump key is released (short hop). Only applies when vy > 0 and Space not held.")]
    public float lowJumpMultiplier = 2f;

    [Tooltip("Max downward speed magnitude (m/s); clamps rb.linearVelocity.y.")]
    public float maxFallSpeed = 20f;

    [Header("Dash Settings")]
    public float dashSpeed = 15f;      // dash speed
    public float dashDuration = 0.2f;  // dash duration
    public float dashCooldown = 5f;    // dash cooldown

    [Header("References")]
    public Transform vcam3DTransform;

    [Header("3D Camera Facing")]
    [Tooltip("Player body yaw aligns to this camera's forward (XZ only, no pitch). If empty, uses Camera.main at Start.")]
    public Transform mainCameraTransform;

    [Tooltip("Max degrees per second when rotating toward camera forward. Set 0 for instant snap.")]
    [SerializeField] private float rotation3DAlignDegreesPerSecond = 540f;

    [Header("2D Facing (方案 C)")]
    [Tooltip("2D 模式下身体朝向跟随移动方向的旋转速度（度/秒）。0 = 瞬间对齐。")]
    [SerializeField] private float facing2DDegreesPerSecond = 720f;

    [Header("Cursor")]
    [Tooltip("If true, 2D mode uses Confined so the cursor stays inside the game window. If false, uses None.")]
    [SerializeField] private bool confineCursorIn2D = true;

    [Header("2D Melee Attack")]
    [Tooltip("Melee attack radius (XZ plane) in 2D mode")]
    public float attackRadius = 3.0f;

    [Tooltip("Melee attack angle in degrees (e.g. 120 means 60° left/right from forward)")]
    public float attackAngle = 120f;

    [Tooltip("Cooldown seconds between melee attacks in 2D mode")]
    public float meleeCooldown = 0.75f;

    [Tooltip("Layer name for enemies (must exist in project layers)")]
    public string enemyLayerName = "Enemy";

    [Tooltip("Damage applied to each enemy hit by the 2D melee cone (requires Health on target).")]
    public float meleeDamage = 10f;

    [Tooltip("Melee knockback force.")]
    public float knockbackForce = 15f;

    [Header("3D Melee — Vertical Cleave (OverlapBox)")]
    [Tooltip("Full size of the attack box (width, height, depth) in world-aligned local space of the player.")]
    public Vector3 melee3DBoxSize = new Vector3(2.2f, 2f, 3.3f);

    [Tooltip("Distance from player position along forward to the center of the overlap box.")]
    public float melee3DBoxDistance = 1.5f;

    [Tooltip("Vertical offset added to box center (helps align with chest/weapon height).")]
    public float melee3DBoxCenterYOffset = 0.5f;

    [Tooltip("Damage for 3D vertical cleave.")]
    public float melee3DDamage = 20f;

    [Tooltip("Cooldown between 3D melee attacks (seconds).")]
    public float melee3DCooldown = 0.75f;

    [Tooltip("Knockback impulse strength for 3D hits (XZ + optional Y).")]
    public float melee3DKnockbackForce = 18f;

    [Header("3D Melee — Weak point / plunge execute")]
    [Tooltip("Tag on boss head trigger volume (large sphere). Forgiving execute when isPlunging.")]
    [SerializeField] private string executionZoneTag = "ExecutionZone";

    [Tooltip("While airborne in 3D, pressing attack sets plunge state so execute does not rely on velocity.y.")]
    [SerializeField] private bool enablePlungeStateExecute = true;

    [Tooltip("Seconds to Lerp player onto execution zone center (0 = snap off).")]
    [SerializeField] private float plungeSnapDuration = 0.1f;

    [Tooltip("Air attack only arms plunge when vertical speed is below this (must be falling).")]
    [SerializeField] private float plungeArmMinFallSpeed = -0.1f;

    [Tooltip("Execution requires at least this downward speed (used for ExecutionZone and weak-point execute).")]
    [SerializeField] private float plungeExecuteMinFallSpeed = -1.0f;

    [Tooltip("Max allowed vertical velocity (Rigidbody.linearVelocity.y) when NOT in plunge state. Ignored when isPlunging.")]
    public float minPlungeSpeed = -3f;

    [Tooltip("If > 0: when WeakPoint is hit but plunge speed is too low and not plunging, apply chip damage once. 0 = no effect.")]
    public float weakPointPlungeFailScratchDamage = 0f;

    [Header("Weapon Visual")]
    [Tooltip("Optional visual swing component on a weapon pivot")]
    public WeaponSwingVisual weaponSwingVisual;

    private Rigidbody rb;
    private ModeSwitcher modeSwitcher; 
    private GhostTrail ghostTrail;     // GhostTrail component used by PlayerController.

    private Vector3 movementInput;

    /// <summary>Updated in FixedUpdate for 3D; used for jump / debug. In 2D mode space is dash — not used for jump.</summary>
    private bool isGrounded;

    private float nextJumpAllowedTime = -100f;

    // State machine variables
    private bool isDashing = false;
    private float lastDashTime = -100f;

    private float nextMeleeAttackTime;
    private float next3DMeleeAttackTime;
    private int enemyLayerMask;

    /// <summary>Space held this frame (for low-jump gravity in FixedUpdate).</summary>
    private bool jumpInputHeld;

    /// <summary>2D: body faces mouse direction until this time (for attack facing snap).</summary>
    private float attackFacingEndTime;

    /// <summary>3D: set true when airborne and attack pressed — execute uses this instead of strict fall speed.</summary>
    private bool isPlunging;

    /// <summary>True while player is in plunge-attack state (3D).</summary>
    public bool IsPlunging => isPlunging;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        modeSwitcher = GetComponent<ModeSwitcher>(); 
        ghostTrail = GetComponentInChildren<GhostTrail>(); // Get GhostTrail component.

        nextMeleeAttackTime = -Mathf.Infinity;
        next3DMeleeAttackTime = -Mathf.Infinity;

        // Cache enemy layer mask for melee queries.
        enemyLayerMask = LayerMask.GetMask(enemyLayerName);
        if (enemyLayerMask == 0)
        {
            Debug.LogWarning($"PlayerController: enemy layer '{enemyLayerName}' not found. Melee attack will hit nothing.");
        }

        if (weaponSwingVisual == null)
            weaponSwingVisual = GetComponentInChildren<WeaponSwingVisual>();

        if (mainCameraTransform == null && Camera.main != null)
            mainCameraTransform = Camera.main.transform;

        // Unity 6: avoid drag fighting custom gravity / air control.
        rb.linearDamping = 0f;

        if (modeSwitcher != null)
        {
            modeSwitcher.OnDimensionChanged += OnPlayerDimensionChanged;
            UpdateCursorState(!modeSwitcher.is2DMode);
        }

        PauseMenuController.EnsureExists();
    }

    private void OnDestroy()
    {
        if (modeSwitcher != null)
            modeSwitcher.OnDimensionChanged -= OnPlayerDimensionChanged;
    }

    /// <summary>3D: lock + hide. 2D: free cursor inside window for top-down mouse aim.</summary>
    private void UpdateCursorState(bool is3D)
    {
        if (is3D)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = confineCursorIn2D ? CursorLockMode.Confined : CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnPlayerDimensionChanged(bool is2DMode)
    {
        UpdateCursorState(!is2DMode);
        if (is2DMode)
            isPlunging = false;
    }

    /// <summary>Unlock cursor so UI buttons (e.g. Level Complete Continue) work. Call <see cref="RefreshCursorForGameMode"/> when the menu closes.</summary>
    public void UnlockCursorForMenus()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Re-apply lock/confine from current dimension after closing a menu.</summary>
    public void RefreshCursorForGameMode()
    {
        if (modeSwitcher == null)
            return;

        UpdateCursorState(!modeSwitcher.is2DMode);
    }

    /// <summary>Lets UI buttons receive clicks instead of gameplay (3D melee / cursor recapture).</summary>
    private static bool IsPointerOverUiThisFrame()
    {
        if (EventSystem.current == null)
            return false;

        // Unity UI + Input System: mouse uses pointer id -1 (Mouse has no pointerId property).
        return EventSystem.current.IsPointerOverGameObject(-1);
    }

    void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        bool levelCompleteUi = LevelManager.IsLevelCompleteUiVisible;
        if (levelCompleteUi)
            UnlockCursorForMenus();

        if (Keyboard.current != null)
            jumpInputHeld = Keyboard.current.spaceKey.isPressed;

        // --- Space: 2D = Dash, 3D = Jump — not nested under WASD / movement ---
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            modeSwitcher != null &&
            !isDashing)
        {
            if (modeSwitcher.is2DMode)
                Dash();
            else
                TryJump3D();
        }

        // Priority 1: if dashing, lock out further inputs (override movement).
        if (isDashing) return;

        bool pointerOverUi = IsPointerOverUiThisFrame();

        // 3D: after unlocking with Esc, left click can recapture cursor this frame
        // (and should not steal UI clicks, e.g. Level Complete).
        bool cursorRecapturedThisFrame = false;
        if (modeSwitcher != null &&
            !modeSwitcher.is2DMode &&
            !levelCompleteUi &&
            !pointerOverUi &&
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            UpdateCursorState(true);
            cursorRecapturedThisFrame = true;
        }

        // Priority 2: capture movement input
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1;
            if (Keyboard.current.sKey.isPressed) input.y -= 1;
            if (Keyboard.current.aKey.isPressed) input.x -= 1;
            if (Keyboard.current.dKey.isPressed) input.x += 1;
        }
        movementInput = new Vector3(input.x, 0, input.y).normalized;

        // 3D mode: align player forward to Main Camera forward on XZ (no pitch on body).
        if (modeSwitcher != null && modeSwitcher.CurrentMode == ModeSwitcher.GameMode.Mode3D)
            Handle3DCameraFacing();

        if (modeSwitcher != null)
        {
            ModeSwitcher.GameMode currentMode = modeSwitcher.CurrentMode;

            // All rotation + attack logic must be inside this 2D check.
            if (currentMode == ModeSwitcher.GameMode.Mode2D)
            {
                Handle2DFacing();

                if (!levelCompleteUi &&
                    Mouse.current != null &&
                    Mouse.current.leftButton.wasPressedThisFrame &&
                    Time.time >= nextMeleeAttackTime)
                {
                    attackFacingEndTime = Time.time + meleeCooldown * 0.5f;
                    Vector3 mouseDir = GetMouseWorldDirection();
                    if (mouseDir.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(mouseDir, Vector3.up);

                    nextMeleeAttackTime = Time.time + meleeCooldown;
                    if (weaponSwingVisual != null)
                        weaponSwingVisual.PlaySwing();
                    PerformMeleeAttack();
                }
            }
            else if (currentMode == ModeSwitcher.GameMode.Mode3D)
            {
                if (!levelCompleteUi &&
                    !cursorRecapturedThisFrame &&
                    !pointerOverUi &&
                    Mouse.current != null &&
                    Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (Time.time >= next3DMeleeAttackTime)
                    {
                        next3DMeleeAttackTime = Time.time + melee3DCooldown;
                        if (weaponSwingVisual != null)
                            weaponSwingVisual.Play3DChop();
                        Perform3DMeleeAttack();
                    }

                    if (enablePlungeStateExecute && !isGrounded && rb != null && rb.linearVelocity.y < plungeArmMinFallSpeed)
                        isPlunging = true;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (LevelManager.IsLevelCompleteUiVisible)
            UnlockCursorForMenus();
    }

    void FixedUpdate()
    {
        UpdateGroundedState3D();
        // Plunge execution temporarily makes the rigidbody kinematic; while kinematic Unity disallows velocity edits.
        if (rb != null && rb.isKinematic)
            return;
        if (modeSwitcher != null && !modeSwitcher.is2DMode && isGrounded && isPlunging)
            isPlunging = false;
        MovePlayer();
        Apply3DVerticalGravityModifiers();
    }

    /// <summary>
    /// Feet-level pivot: ray starts slightly above feet so it is not inside the ground collider.
    /// Skips hits on this character so the capsule is not mistaken for ground.
    /// </summary>
    private void UpdateGroundedState3D()
    {
        if (modeSwitcher == null || modeSwitcher.is2DMode)
        {
            isGrounded = true;
            return;
        }

        isGrounded = TryGetGroundHit(out _);
    }

    /// <summary>True if a downward cast from above the feet hits non-self geometry within range.</summary>
    private bool TryGetGroundHit(out RaycastHit groundHit)
    {
        groundHit = default;
        Vector3 origin = transform.position + Vector3.up * groundCheckOriginOffset;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return false;

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

    /// <summary>
    /// Heavier fall + short-hop when jump released; terminal speed clamp. 3D only.
    /// </summary>
    private void Apply3DVerticalGravityModifiers()
    {
        if (rb != null && rb.isKinematic)
            return;
        if (isDashing)
            return;

        if (modeSwitcher == null || modeSwitcher.is2DMode)
            return;

        Vector3 v = rb.linearVelocity;
        float vy = v.y;

        if (vy < 0f)
        {
            v += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (vy > 0f && !jumpInputHeld)
        {
            v += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }

        vy = v.y;
        if (vy < -maxFallSpeed)
            v.y = -maxFallSpeed;

        rb.linearVelocity = v;
    }

    private void MovePlayer()
    {
        if (rb != null && rb.isKinematic)
            return;
        // During dash, regular movement logic gives up control
        if (isDashing) return; 

        if (modeSwitcher.is2DMode) 
        {
            Vector3 targetVelocity = movementInput * moveSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        }
        else
        {
            if (vcam3DTransform == null) return; 

            Vector3 camForward = vcam3DTransform.forward;
            Vector3 camRight = vcam3DTransform.right;
            
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = camForward * movementInput.z + camRight * movementInput.x;
            Vector3 targetVelocity = moveDir * moveSpeed;

            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
    }

    private void TryJump3D()
    {
        if (modeSwitcher == null || modeSwitcher.is2DMode)
            return;

        if (rb != null && rb.isKinematic)
            return;

        if (Time.time < nextJumpAllowedTime)
            return;

        if (!TryGetGroundHit(out _))
            return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        isGrounded = false;
        nextJumpAllowedTime = Time.time + jumpCooldown;
    }

    private void Dash()
    {
        // Check cooldown and require a movement direction for dash (no dash from standing still)
        if (Time.time >= lastDashTime + dashCooldown && movementInput.magnitude > 0.1f)
        {
            StartCoroutine(PerformDashCoroutine());
        }
    }

    /// <summary>
    /// 3D: match <see cref="transform.forward"/> to camera view direction projected on XZ (upright character).
    /// </summary>
    private void Handle3DCameraFacing()
    {
        if (mainCameraTransform == null)
        {
            if (Camera.main != null)
                mainCameraTransform = Camera.main.transform;
            else
                return;
        }

        Vector3 flatForward = mainCameraTransform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            return;

        flatForward.Normalize();
        Quaternion targetRot = Quaternion.LookRotation(flatForward, Vector3.up);

        if (rotation3DAlignDegreesPerSecond <= 0f)
            transform.rotation = targetRot;
        else
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotation3DAlignDegreesPerSecond * Time.deltaTime);
    }

    /// <summary>
    /// 方案 C：移动时身体朝向跟随 WASD 方向（平滑旋转）；攻击窗口期间身体瞬间朝向鼠标。
    /// </summary>
    private void Handle2DFacing()
    {
        if (Time.time < attackFacingEndTime)
        {
            Vector3 mouseDir = GetMouseWorldDirection();
            if (mouseDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(mouseDir, Vector3.up);
            return;
        }

        if (movementInput.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(movementInput.normalized, Vector3.up);
            if (facing2DDegreesPerSecond <= 0f)
                transform.rotation = targetRot;
            else
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, facing2DDegreesPerSecond * Time.deltaTime);
        }
    }

    /// <summary>
    /// 从玩家位置指向鼠标在世界 XZ 平面上投影点的归一化方向。
    /// </summary>
    private Vector3 GetMouseWorldDirection()
    {
        if (Camera.main == null || Mouse.current == null)
            return transform.forward;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.up, transform.position);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.000001f)
                return dir.normalized;
        }

        return transform.forward;
    }

    /// <summary>
    /// Root is at feet — all melee overlap centers and gizmos use this (feet + <see cref="attackHeightOffset"/>).
    /// </summary>
    private Vector3 GetAttackBasePosition()
    {
        return transform.position + Vector3.up * attackHeightOffset;
    }

    private void PerformMeleeAttack()
    {
        if (enemyLayerMask == 0)
            return;

        Vector3 basePosition = GetAttackBasePosition();

        // Query all enemies within radius (do not use physics colliders to simulate the cone).
        Collider[] hits = Physics.OverlapSphere(
            basePosition,
            attackRadius,
            enemyLayerMask
        );

        float halfAngle = attackAngle * 0.5f;
        Vector3 forward = transform.forward;

        var damaged = new HashSet<Health>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            Vector3 toEnemy = hit.transform.position - basePosition;
            toEnemy.y = 0f;

            if (toEnemy.sqrMagnitude < 0.000001f)
                continue;

            float angle = Vector3.Angle(forward, toEnemy);
            if (angle <= halfAngle)
            {
                // 1) Get Health component and apply damage
                Health health = hit.GetComponentInParent<Health>();
                if (health == null)
                    health = hit.GetComponent<Health>();

                if (health != null && damaged.Add(health))
                {
                    health.TakeDamage(meleeDamage);

                    // 2) Added: knockback logic
                    // Try to get Rigidbody from the enemy
                    Rigidbody enemyRb = hit.GetComponentInParent<Rigidbody>();
                    if (enemyRb == null) enemyRb = hit.GetComponent<Rigidbody>();

                    if (enemyRb != null)
                    {
                        // Compute knockback direction using XZ plane only
                        Vector3 knockbackDir = toEnemy.normalized;
                        knockbackDir.y = 0f;

                        // Feel trick: clear the enemy's current velocity before applying knockback.
                        // This keeps knockback distance consistent and avoids cancellation from enemy motion.
                        enemyRb.linearVelocity = new Vector3(0f, enemyRb.linearVelocity.y, 0f);
                        
                        // Apply an impulse burst (Impulse)
                        enemyRb.AddForce(knockbackDir * knockbackForce, ForceMode.Impulse);
                    }
                }

                Debug.Log($"Melee hit: {hit.transform.name}");
            }
        }
    }

    /// <summary>
    /// 3D vertical cleave: box overlap in front of the player, aligned with player rotation.
    /// </summary>
    private void Perform3DMeleeAttack()
    {
        if (enemyLayerMask == 0)
            return;

        Vector3 basePosition = GetAttackBasePosition();
        Vector3 halfExtents = melee3DBoxSize * 0.5f;
        Vector3 center = basePosition
            + transform.forward * melee3DBoxDistance
            + Vector3.up * melee3DBoxCenterYOffset;

        Quaternion orientation = transform.rotation;

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            orientation,
            enemyLayerMask,
            QueryTriggerInteraction.Collide);

        float verticalSpeed = rb.linearVelocity.y;

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;
            if (!hit.CompareTag("WeakPoint"))
                continue;

            WeakPointHitReceiver wp = hit.GetComponent<WeakPointHitReceiver>();
            if (wp == null)
                continue;

            bool allowPlungeExecute =
                (isPlunging && verticalSpeed < plungeExecuteMinFallSpeed) ||
                (!isPlunging && verticalSpeed <= minPlungeSpeed);
            if (!allowPlungeExecute)
            {
                if (weakPointPlungeFailScratchDamage > 0f)
                {
                    Health scratchTarget = hit.GetComponentInParent<Health>();
                    if (scratchTarget == null)
                        scratchTarget = hit.GetComponent<Health>();
                    if (scratchTarget != null && scratchTarget.IsAlive)
                        scratchTarget.TakeDamage(weakPointPlungeFailScratchDamage);
                }

                continue;
            }

            if (wp.TryProcessWeakPointFrom3DMelee(this, rb, modeSwitcher))
            {
                isPlunging = false;
                return;
            }
        }

        var damaged = new HashSet<Health>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            // Weak point only counts for plunge execute above — not normal cleave damage.
            if (hit.CompareTag("WeakPoint"))
                continue;

            Health health = hit.GetComponentInParent<Health>();
            if (health == null)
                health = hit.GetComponent<Health>();

            if (health == null || !damaged.Add(health))
                continue;

            health.TakeDamage(melee3DDamage);

            Vector3 toEnemy = hit.transform.position - basePosition;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.000001f)
                continue;

            Vector3 knockDir = toEnemy.normalized;

            Rigidbody enemyRb = hit.GetComponentInParent<Rigidbody>();
            if (enemyRb == null) enemyRb = hit.GetComponent<Rigidbody>();

            if (enemyRb != null)
            {
                enemyRb.linearVelocity = new Vector3(0f, enemyRb.linearVelocity.y, 0f);
                enemyRb.AddForce(knockDir * melee3DKnockbackForce, ForceMode.Impulse);
            }

            Debug.Log($"3D cleave hit: {hit.transform.name}");
        }
    }

    private Coroutine _plungeSnapCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || string.IsNullOrEmpty(executionZoneTag) || !other.CompareTag(executionZoneTag))
            return;
        if (modeSwitcher == null || modeSwitcher.is2DMode)
            return;
        if (!isPlunging)
            return;
        if (rb == null || rb.linearVelocity.y >= plungeExecuteMinFallSpeed)
            return;

        Earthshaker boss = other.GetComponentInParent<Earthshaker>();
        if (boss == null)
            return;

        Health bh = boss.GetComponent<Health>();
        if (bh != null && !bh.IsAlive)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (plungeSnapDuration > 0f)
        {
            if (_plungeSnapCoroutine != null)
                StopCoroutine(_plungeSnapCoroutine);
            _plungeSnapCoroutine = StartCoroutine(PlungeSnapAndExecuteRoutine(other.transform, boss));
        }
        else
        {
            boss.ExecuteWeakPointInstantKill();
            isPlunging = false;
        }
    }

    private IEnumerator PlungeSnapAndExecuteRoutine(Transform executionZone, Earthshaker boss)
    {
        bool wasKinematic = rb.isKinematic;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 start = transform.position;
        Vector3 end = executionZone.position;
        float dur = Mathf.Max(0.01f, plungeSnapDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t / dur));
            yield return null;
        }

        transform.position = end;

        if (boss != null)
        {
            Health h = boss.GetComponent<Health>();
            if (h == null || h.IsAlive)
                boss.ExecuteWeakPointInstantKill();
        }

        isPlunging = false;
        // Always restore to non-kinematic during gameplay so gravity can resume (prevents "floating in air").
        rb.isKinematic = false;
        rb.useGravity = true;
        // Keep constraints consistent with 3D mode.
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        _plungeSnapCoroutine = null;
    }

    private IEnumerator PerformDashCoroutine()
    {
        // Enter dash state
        isDashing = true;
        lastDashTime = Time.time;

        // Enable GhostTrail
        if (ghostTrail != null) ghostTrail.StartTrail();

        // Record dash direction and boost speed
        Vector3 dashDirection = movementInput;
        rb.linearVelocity = new Vector3(dashDirection.x * dashSpeed, 0f, dashDirection.z * dashSpeed);

        // Maintain dash speed for dashDuration
        yield return new WaitForSeconds(dashDuration);

        // End dash and quickly stop
        rb.linearVelocity = Vector3.zero;

        // Disable GhostTrail
        if (ghostTrail != null) ghostTrail.StopTrail();

        // Release lock
        isDashing = false;
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        Vector3 basePosition = GetAttackBasePosition();

        // 2D cone (red) — only meaningful for 2D mode visualization; still drawn for reference.
        Handles.color = new Color(1f, 0f, 0f, 0.2f);

        Vector3 forward = transform.forward;
        Vector3 startingAngle = Quaternion.Euler(0, -attackAngle * 0.5f, 0) * forward;

        Handles.DrawSolidArc(
            basePosition,
            Vector3.up,
            startingAngle,
            attackAngle,
            attackRadius
        );

        Handles.color = Color.red;
        Handles.DrawWireArc(basePosition, Vector3.up, startingAngle, attackAngle, attackRadius);

        Vector3 endingAngle = Quaternion.Euler(0, attackAngle * 0.5f, 0) * forward;
        Handles.DrawLine(basePosition, basePosition + startingAngle * attackRadius);
        Handles.DrawLine(basePosition, basePosition + endingAngle * attackRadius);

        // 3D vertical cleave box (blue wire cube), follows player facing.
        Gizmos.color = Color.blue;
        Matrix4x4 prev = Gizmos.matrix;
        Vector3 boxCenter = basePosition
            + transform.forward * melee3DBoxDistance
            + Vector3.up * melee3DBoxCenterYOffset;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, melee3DBoxSize);
        Gizmos.matrix = prev;
#endif
    }
}

