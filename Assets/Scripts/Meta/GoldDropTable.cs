using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gold per enemy kill by scene / type: L1 mobs 10, L2 50, L3 flying 30, other L3 10.
/// </summary>
public static class GoldDropTable
{
    public static int GetGoldForKill(GameObject enemy)
    {
        if (enemy == null)
            return 0;

        string scene = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(scene))
            return 10;

        if (scene.StartsWith("Level_02", StringComparison.OrdinalIgnoreCase))
            return 50;

        if (scene.StartsWith("Level_03", StringComparison.OrdinalIgnoreCase))
        {
            if (enemy.GetComponent<FloatingEyeMovement>() != null)
                return 30;
            return 10;
        }

        // Level_01, Level_04, etc.
        return 10;
    }
}
