using UnityEngine;

/// <summary>
/// Data-only ScriptableObject that describes one roguelite upgrade card.
/// </summary>
[CreateAssetMenu(menuName = "Roguelite/Upgrade Data", fileName = "UpgradeData")]
public class UpgradeData : ScriptableObject
{
    public string cardName;

    [TextArea]
    public string description;

    public Sprite icon;

    public UpgradeType upgradeType;

    [Tooltip("Upgrade amount/value (meaning depends on UpgradeType).")]
    public float value;
}

public enum UpgradeType
{
    DamageUp,
    MaxHealthUp,
    MoveSpeedUp
}

