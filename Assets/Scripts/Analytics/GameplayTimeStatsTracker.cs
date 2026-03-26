using UnityEngine;

/// <summary>
/// Optional component: attach it to an object that already has <see cref="ModeSwitcher"/> (e.g. the player),
/// and report <see cref="Time.deltaTime"/> per-frame to <see cref="GameStatsManager"/> based on the current dimension.
/// Does not change dimension switching logic; it only collects statistics.
/// </summary>
[RequireComponent(typeof(ModeSwitcher))]
public class GameplayTimeStatsTracker : MonoBehaviour
{
    [Tooltip("If true, use time that is not affected by timeScale (paused time won't accumulate).")]
    [SerializeField] private bool useUnscaledDeltaTime;

    private ModeSwitcher _mode;

    private void Awake()
    {
        _mode = GetComponent<ModeSwitcher>();
    }

    private void Update()
    {
        if (_mode == null)
            return;

        float dt = useUnscaledDeltaTime ? Time.unscaledDeltaTime : Time.deltaTime;
        GameStatsManager.EnsureExists();
        if (GameStatsManager.Instance != null)
            GameStatsManager.Instance.AddTime(_mode.is2DMode, dt);
    }
}
