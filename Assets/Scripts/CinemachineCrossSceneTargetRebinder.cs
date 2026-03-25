using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>
/// Fixes Cinemachine after <see cref="SceneManager.LoadScene"/> when the <b>Player</b> uses
/// <see cref="PlayerStatsManager"/> + <c>DontDestroyOnLoad</c>:
/// <list type="bullet">
/// <item>New scene <see cref="CinemachineCamera"/>s still point at the destroyed duplicate Player.</item>
/// <item><see cref="ModeSwitcher"/> on the persistent Player still references destroyed Level-1 vcams.</item>
/// </list>
/// Subscribes to <see cref="SceneManager.sceneLoaded"/> and reassigns TrackingTarget + ModeSwitcher / PlayerController refs.
/// 3D vcam uses the player's <c>Head</c> child when present (see <see cref="FindHeadTransform"/>).
/// </summary>
public static class CinemachineCrossSceneTargetRebinder
{
    const string VCam2DObjectName = "VCam_2D";
    const string VCam3DObjectName = "VCam_3D";
    const string HeadObjectName = "Head";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Run next frame so all scene objects finish enabling (safe for Cinemachine Brain).
        RebindRunner.Queue(Rebind);
    }

    private static void Rebind()
    {
        Transform player = ResolvePlayerTransform();
        if (player == null)
        {
            Debug.LogWarning("[CinemachineCrossSceneTargetRebinder] No Player found (tag / PlayerStatsManager / PlayerController). Camera follow not rebound.");
            return;
        }

        CinemachineCamera[] vcams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        if (vcams == null || vcams.Length == 0)
            return;

        Transform head = FindHeadTransform(player);

        foreach (CinemachineCamera vcam in vcams)
        {
            if (vcam == null) continue;

            CameraTarget t = vcam.Target;
            if (vcam.gameObject.name == VCam3DObjectName)
            {
                t.TrackingTarget = head != null ? head : player;
                if (head == null)
                {
                    Debug.LogWarning(
                        "[CinemachineCrossSceneTargetRebinder] No transform named 'Head' under Player — VCam_3D tracks player root. Add a child named Head.",
                        player);
                }
            }
            else
            {
                t.TrackingTarget = player;
            }

            vcam.Target = t;
        }

        ModeSwitcher modeSwitcher = player.GetComponent<ModeSwitcher>();
        if (modeSwitcher == null)
            modeSwitcher = player.GetComponentInChildren<ModeSwitcher>();

        if (modeSwitcher != null)
        {
            foreach (CinemachineCamera vcam in vcams)
            {
                if (vcam == null) continue;
                string n = vcam.gameObject.name;
                if (n == VCam2DObjectName)
                    modeSwitcher.vcam2D = vcam;
                else if (n == VCam3DObjectName)
                    modeSwitcher.vcam3D = vcam;
            }

            ApplyVcamPriorities(modeSwitcher);

            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null)
                pc = player.GetComponentInChildren<PlayerController>();
            if (pc != null && modeSwitcher.vcam3D != null)
                pc.vcam3DTransform = modeSwitcher.vcam3D.transform;
        }
    }

    private static Transform ResolvePlayerTransform()
    {
        if (PlayerStatsManager.Instance != null)
            return PlayerStatsManager.Instance.transform;

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            return tagged.transform;

        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
        return pc != null ? pc.transform : null;
    }

    /// <summary>Direct child named Head, else first descendant named Head (e.g. under rig).</summary>
    private static Transform FindHeadTransform(Transform playerRoot)
    {
        if (playerRoot == null)
            return null;

        Transform direct = playerRoot.Find(HeadObjectName);
        if (direct != null)
            return direct;

        foreach (Transform t in playerRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t != playerRoot && t.name == HeadObjectName)
                return t;
        }

        return null;
    }

    /// <summary>Mirrors <see cref="ModeSwitcher"/> priority rules without calling ToggleDimension.</summary>
    private static void ApplyVcamPriorities(ModeSwitcher ms)
    {
        if (ms.vcam2D == null || ms.vcam3D == null)
            return;

        if (ms.is2DMode)
        {
            ms.vcam2D.Priority = 20;
            ms.vcam3D.Priority = 10;
        }
        else
        {
            ms.vcam3D.Priority = 20;
            ms.vcam2D.Priority = 10;
        }
    }

    /// <summary>Runs an action on the next Unity player loop tick.</summary>
    private sealed class RebindRunner : MonoBehaviour
    {
        private System.Action _action;

        public static void Queue(System.Action action)
        {
            var go = new GameObject(nameof(RebindRunner));
            var r = go.AddComponent<RebindRunner>();
            r._action = action;
        }

        private void Start()
        {
            _action?.Invoke();
            Destroy(gameObject);
        }
    }
}
