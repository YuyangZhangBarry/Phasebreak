using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using Unity.Cinemachine;

/// <summary>
/// Fixes Cinemachine after SceneManager.LoadScene when the Player uses DontDestroyOnLoad.
/// </summary>
public static class CinemachineCrossSceneTargetRebinder
{
    const string VCam2DObjectName = "VCam_2D";
    const string VCam3DObjectName = "VCam_3D";
    const string CameraTargetName = "CameraTarget"; 

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindRunner.Queue(Rebind);
    }

    private static void Rebind()
    {
        Transform player = ResolvePlayerTransform();
        if (player == null) return;

        CinemachineCamera[] vcams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        if (vcams == null || vcams.Length == 0) return;

        Transform camTarget = null;
        GameObject targetObj = GameObject.Find(CameraTargetName);
        
        if (targetObj != null)
        {
            camTarget = targetObj.transform;
            PositionConstraint constraint = targetObj.GetComponent<PositionConstraint>();
            if (constraint != null)
            {
                if (constraint.sourceCount > 0) 
                    constraint.RemoveSource(0);

                ConstraintSource source = new ConstraintSource();
                source.sourceTransform = player;
                source.weight = 1f;
                constraint.AddSource(source);
                
                // 【绝杀补丁】：强行清零 Prefab 带来的记忆偏移量！
                constraint.translationOffset = Vector3.zero; 
                constraint.constraintActive = true;
            }
        }

        ModeSwitcher modeSwitcher = player.GetComponent<ModeSwitcher>();
        if (modeSwitcher == null)
            modeSwitcher = player.GetComponentInChildren<ModeSwitcher>();

        foreach (CinemachineCamera vcam in vcams)
        {
            if (vcam == null) continue;

            CameraTarget t = vcam.Target;
            string n = vcam.gameObject.name;
            
            if (n == VCam3DObjectName)
            {
                t.TrackingTarget = camTarget != null ? camTarget : player;
                if (modeSwitcher != null) modeSwitcher.vcam3D = vcam;
            }
            else if (n == VCam2DObjectName)
            {
                t.TrackingTarget = player;
                if (modeSwitcher != null) modeSwitcher.vcam2D = vcam;
            }

            vcam.Target = t;
        }

        if (modeSwitcher != null)
        {
            // 分配优先级，并通知代码拿到新摄像机
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

    // 【绝杀补丁】：即使某个摄像机没找到，也不会让代码崩溃，确保优先级顺利发放
    private static void ApplyVcamPriorities(ModeSwitcher ms)
    {
        if (ms.is2DMode)
        {
            if (ms.vcam2D != null) ms.vcam2D.Priority = 20;
            if (ms.vcam3D != null) ms.vcam3D.Priority = 10;
        }
        else
        {
            if (ms.vcam3D != null) ms.vcam3D.Priority = 20;
            if (ms.vcam2D != null) ms.vcam2D.Priority = 10;
        }
    }

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