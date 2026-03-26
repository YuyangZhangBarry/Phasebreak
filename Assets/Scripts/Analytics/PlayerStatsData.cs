using System;
using UnityEngine;

/// <summary>
/// Pure data class: a JSON-persisted snapshot of player statistics.
/// Note: <see cref="JsonUtility"/> does not support Dictionary, so upgrade pick counts are stored as
/// three independent int fields, corresponding to <see cref="UpgradeType"/> in this project.
/// </summary>
[Serializable]
public class PlayerStatsData
{
    /// <summary>Total deaths / attempts (incremented once per player death).</summary>
    public int totalAttempts;

    /// <summary>Total seconds spent in 2D mode.</summary>
    public float timeSpent2D;

    /// <summary>Total seconds spent in 3D mode.</summary>
    public float timeSpent3D;

    /// <summary>How many times damage upgrades (<see cref="UpgradeType.DamageUp"/>) were picked.</summary>
    public int upgradeCountDamage;

    /// <summary>How many times health upgrades (<see cref="UpgradeType.MaxHealthUp"/>) were picked.</summary>
    public int upgradeCountHealth;

    /// <summary>How many times move speed upgrades (<see cref="UpgradeType.MoveSpeedUp"/>) were picked.</summary>
    public int upgradeCountSpeed;

    /// <summary>Create default data where all values are 0.</summary>
    public static PlayerStatsData CreateDefault()
    {
        return new PlayerStatsData();
    }

    /// <summary>Deep copy to avoid external code mutating manager internal state.</summary>
    public PlayerStatsData Clone()
    {
        return new PlayerStatsData
        {
            totalAttempts = totalAttempts,
            timeSpent2D = timeSpent2D,
            timeSpent3D = timeSpent3D,
            upgradeCountDamage = upgradeCountDamage,
            upgradeCountHealth = upgradeCountHealth,
            upgradeCountSpeed = upgradeCountSpeed
        };
    }
}
