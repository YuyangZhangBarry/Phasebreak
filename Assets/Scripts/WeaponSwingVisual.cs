using System.Collections;
using UnityEngine;

/// <summary>
/// Plays local weapon motion: 2D = yaw sweep (horizontal fan); 3D = pitch chop (vertical cleave).
/// Attach this to the weapon <b>pivot</b> (empty parent), not the mesh, so rotation origin is correct.
/// </summary>
public class WeaponSwingVisual : MonoBehaviour
{
    [Header("Aim alignment")]
    [Tooltip("Constant local rotation applied before any swing. Use to fix mesh import axis vs. forward.")]
    [SerializeField] private Vector3 baseAimOffsetEuler = Vector3.zero;

    [Header("2D swing — local Y yaw (horizontal fan)")]
    [Tooltip("Idle pose (local euler, degrees). Usually (0,0,0) so weapon points at attack cone center.")]
    [SerializeField] private Vector3 restEuler = Vector3.zero;

    [Tooltip("Half of total slash arc in degrees (yaw). 60 = sweep from -60° to +60°.")]
    [SerializeField] private float halfConeAngleDegrees = 60f;

    [Header("2D timing")]
    [SerializeField] private float windupTime = 0.05f;
    [SerializeField] private float slashTime = 0.12f;
    [SerializeField] private float returnTime = 0.10f;

    [Header("3D chop — local X pitch (vertical cleave)")]
    [Tooltip("Start local pitch (degrees), e.g. raised / overhead.")]
    [SerializeField] private float chop3DPitchStartDegrees = -60f;

    [Tooltip("End local pitch (degrees), e.g. slammed forward/down.")]
    [SerializeField] private float chop3DPitchEndDegrees = 60f;

    [Tooltip("Total duration of the 3D chop (seconds). Keep short for snappy feel.")]
    [SerializeField] private float chop3DDuration = 0.15f;

    [Tooltip("Seconds to hide weapon mesh after chop (0 = skip).")]
    [SerializeField] private float chop3DHideDuration = 0.08f;

    [Tooltip("Optional: mesh renderers to briefly disable during hide. If empty, tries GetComponentsInChildren.")]
    [SerializeField] private Renderer[] hideRenderers;

    private Coroutine swingRoutine;
    private Coroutine chopRoutine;

    private Quaternion BaseAim => Quaternion.Euler(baseAimOffsetEuler);

    /// <summary>Center of the 2D swing cone.</summary>
    private Quaternion CenterRotation2D => BaseAim * Quaternion.Euler(restEuler);

    private void Awake()
    {
        transform.localRotation = CenterRotation2D;
    }

    /// <summary>2D horizontal fan swing (yaw).</summary>
    public void PlaySwing()
    {
        StopVisualRoutines();
        swingRoutine = StartCoroutine(SwingRoutine());
    }

    /// <summary>3D vertical cleave visual (pitch).</summary>
    public void Play3DChop()
    {
        StopVisualRoutines();
        chopRoutine = StartCoroutine(Chop3DRoutine());
    }

    private void StopVisualRoutines()
    {
        if (swingRoutine != null)
        {
            StopCoroutine(swingRoutine);
            swingRoutine = null;
        }

        if (chopRoutine != null)
        {
            StopCoroutine(chopRoutine);
            chopRoutine = null;
        }
    }

    private IEnumerator SwingRoutine()
    {
        Quaternion center = CenterRotation2D;
        Quaternion left = center * Quaternion.Euler(0f, -halfConeAngleDegrees, 0f);
        Quaternion right = center * Quaternion.Euler(0f, halfConeAngleDegrees, 0f);

        transform.localRotation = center;

        float w = Mathf.Max(0f, windupTime);
        if (w > 0.0001f)
            yield return RotateOverTime(center, left, w);
        else
            transform.localRotation = left;

        yield return RotateOverTime(transform.localRotation, right, Mathf.Max(0.0001f, slashTime));
        yield return RotateOverTime(right, center, Mathf.Max(0.0001f, returnTime));

        swingRoutine = null;
    }

    private IEnumerator Chop3DRoutine()
    {
        Quaternion center = CenterRotation2D;
        Quaternion raised = center * Quaternion.Euler(chop3DPitchStartDegrees, 0f, 0f);
        Quaternion slammed = center * Quaternion.Euler(chop3DPitchEndDegrees, 0f, 0f);

        transform.localRotation = raised;

        float dur = Mathf.Max(0.0001f, chop3DDuration);
        yield return RotateOverTime(raised, slammed, dur);

        if (chop3DHideDuration > 0.0001f)
        {
            SetWeaponHidden(true);
            yield return new WaitForSeconds(chop3DHideDuration);
            SetWeaponHidden(false);
        }

        transform.localRotation = center;
        chopRoutine = null;
    }

    private void SetWeaponHidden(bool hidden)
    {
        Renderer[] rs = hideRenderers;
        if (rs == null || rs.Length == 0)
            rs = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in rs)
        {
            if (r != null)
                r.enabled = !hidden;
        }
    }

    private IEnumerator RotateOverTime(Quaternion from, Quaternion to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            transform.localRotation = Quaternion.Slerp(from, to, eased);
            yield return null;
        }

        transform.localRotation = to;
    }
}
