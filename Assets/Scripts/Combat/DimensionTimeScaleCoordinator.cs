using UnityEngine;

/// <summary>
/// 全局 <see cref="Time.timeScale"/> 写入仲裁：<see cref="PlayerController"/> 的完美反击子弹时间与
/// <see cref="DimensionShiftDirector"/> 的切维 Hitstop 不能同时改同一变量。
/// 当完美反击声明占用时，Director 只播果汁（后处理 / 形变 / 粒子），不改时间缩放。
/// </summary>
public static class DimensionTimeScaleCoordinator
{
    /// <summary>为 true 时，DimensionShiftDirector 不得修改 Time.timeScale / Time.fixedDeltaTime。</summary>
    public static bool PerfectParryStrikeOwnsGlobalTimeScale { get; private set; }

    public static void BeginPerfectParryStrikeOwnership()
    {
        PerfectParryStrikeOwnsGlobalTimeScale = true;
    }

    public static void EndPerfectParryStrikeOwnership()
    {
        PerfectParryStrikeOwnsGlobalTimeScale = false;
    }
}
