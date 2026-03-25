using System;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 2D / 3D dimension toggle. Pivot at feet: 2D locks Y to <see cref="planeY2D"/>.
/// Switching to 3D raycasts down (<see cref="environmentLayer"/>) to land on platforms; fallback <see cref="planeY3D"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ModeSwitcher : MonoBehaviour
{
    public enum GameMode
    {
        Mode2D,
        Mode3D
    }

    [Header("Camera Setup")]
    public CinemachineCamera vcam3D;
    public CinemachineCamera vcam2D;

    [Header("World heights (pivot at feet)")]
    [Tooltip("2D combat plane world Y — character pivot Y is set to this value.")]
    [SerializeField] private float planeY2D = 40f;

    [Tooltip("Fallback world Y when switching to 3D if the down-ray hits nothing (pivot / feet).")]
    [SerializeField] private float planeY3D = 0f;

    [Header("2D → 3D landing (raycast)")]
    [Tooltip("Only these layers count as ground/platforms for the landing ray. Exclude Player / Enemy.")]
    public LayerMask environmentLayer = ~0;

    [Tooltip("Ray starts this far above the pivot along Y so the origin is not inside a collider.")]
    [SerializeField] private float landingRayOriginYOffset = 0.15f;

    [Tooltip("Max downward ray length from the start height (e.g. from Y≈40 down to floor).")]
    [SerializeField] private float landingRayMaxDistance = 200f;

    /// <summary>World Y of the 2D combat floor (same as pivot Y).</summary>
    public float PlaneY2D => planeY2D;

    /// <summary>Fired after dimension changes. Argument is <see cref="is2DMode"/> (true = 2D).</summary>
    public event Action<bool> OnDimensionChanged;

    /// <summary>Fired after <see cref="DimensionShiftJuice"/> ends (time scale restored). Use for first-time UI.</summary>
    public event Action OnDimensionShiftComplete;

    public bool is2DMode { get; private set; }
    public GameMode CurrentMode { get; private set; }

    [Header("Dimension Switch Lock")]
    [Tooltip("When false, switching from 2D -> 3D is blocked (2D is still the default).")]
    public bool isDimensionSwitchUnlocked = false;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        is2DMode = true;
        CurrentMode = GameMode.Mode2D;
        if (vcam2D != null)
            vcam2D.Priority = 20;
        if (vcam3D != null)
            vcam3D.Priority = 10;

        SnapPlayerY(planeY2D);
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.useGravity = false;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (is2DMode && !isDimensionSwitchUnlocked)
                return;

            ToggleDimension();
        }
    }

    private void LateUpdate()
    {
        // 2D mode: keep pivot at feet locked to the 2D plane.
        // This prevents slow drift caused by physics penetration resolution or initialization order.
        if (!is2DMode || rb == null)
            return;

        Vector3 p = transform.position;
        if (Mathf.Abs(p.y - planeY2D) > 0.001f)
        {
            p.y = planeY2D;
            transform.position = p;
        }

        // Ensure no residual Y velocity.
        if (rb.linearVelocity.y != 0f)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    }

    private void ToggleDimension()
    {
        is2DMode = !is2DMode;

        StartCoroutine(DimensionShiftJuice());

        if (is2DMode)
        {
            CurrentMode = GameMode.Mode2D;
            if (vcam2D != null)
                vcam2D.Priority = 20;
            if (vcam3D != null)
                vcam3D.Priority = 10;

            SnapPlayerY(planeY2D);
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.useGravity = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
        else
        {
            CurrentMode = GameMode.Mode3D;
            Apply3DLandingFrom2DPosition();

            if (vcam3D != null)
            {
                Vector3 lookDir = vcam3D.transform.forward;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }

            if (vcam3D != null)
                vcam3D.Priority = 20;
            if (vcam2D != null)
                vcam2D.Priority = 10;

            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
        }

        OnDimensionChanged?.Invoke(is2DMode);
    }

    /// <summary>
    /// From current XZ (2D top-down position over a platform), ray down through environment layers and place feet on the hit surface.
    /// </summary>
    private void Apply3DLandingFrom2DPosition()
    {
        Vector3 p = transform.position;
        float startY = p.y + landingRayOriginYOffset;
        Vector3 origin = new Vector3(p.x, startY, p.z);

        if (environmentLayer.value != 0 &&
            Physics.Raycast(origin, Vector3.down, out RaycastHit hit, landingRayMaxDistance, environmentLayer, QueryTriggerInteraction.Ignore))
        {
            p.y = hit.point.y;
        }
        else
        {
            p.y = planeY3D;
        }

        transform.position = p;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void SnapPlayerY(float worldY)
    {
        Vector3 p = transform.position;
        p.y = worldY;
        transform.position = p;
    }

    private IEnumerator DimensionShiftJuice()
    {
        Time.timeScale = 0.1f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(0.15f);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        OnDimensionShiftComplete?.Invoke();
    }
}
