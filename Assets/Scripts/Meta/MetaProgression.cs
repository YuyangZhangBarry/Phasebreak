using System;
using UnityEngine;

/// <summary>
/// Persistent gold and meta upgrades (power / speed / HP). Saved in PlayerPrefs.
/// </summary>
public static class MetaProgression
{
    public const int MaxUpgradeLevel = 10;
    public const int UpgradeCostGold = 100;

    private const string KeyGold = "MetaGold";
    private const string KeyPower = "MetaPowerLevel";
    private const string KeySpeed = "MetaSpeedLevel";
    private const string KeyHp = "MetaHpLevel";

    public static event Action<int> OnGoldChanged;
    public static event Action OnUpgradesChanged;

    public static int GetGold() => PlayerPrefs.GetInt(KeyGold, 0);

    public static void SetGold(int value)
    {
        value = Mathf.Max(0, value);
        PlayerPrefs.SetInt(KeyGold, value);
        PlayerPrefs.Save();
        OnGoldChanged?.Invoke(value);
    }

    public static void AddGold(int amount)
    {
        if (amount == 0)
            return;
        SetGold(GetGold() + amount);
    }

    public static int GetPowerLevel() => Mathf.Clamp(PlayerPrefs.GetInt(KeyPower, 0), 0, MaxUpgradeLevel);
    public static int GetSpeedLevel() => Mathf.Clamp(PlayerPrefs.GetInt(KeySpeed, 0), 0, MaxUpgradeLevel);
    public static int GetHpLevel() => Mathf.Clamp(PlayerPrefs.GetInt(KeyHp, 0), 0, MaxUpgradeLevel);

    private static void SetPowerLevel(int v) => PlayerPrefs.SetInt(KeyPower, v);
    private static void SetSpeedLevel(int v) => PlayerPrefs.SetInt(KeySpeed, v);
    private static void SetHpLevel(int v) => PlayerPrefs.SetInt(KeyHp, v);

    /// <summary>+1 damage per level to melee / 3D melee.</summary>
    public static float GetPowerBonus() => GetPowerLevel() * 1f;

    /// <summary>+0.3 move speed per level.</summary>
    public static float GetSpeedBonus() => GetSpeedLevel() * 0.3f;

    /// <summary>+5 max HP per level.</summary>
    public static float GetHpBonus() => GetHpLevel() * 5f;

    public static bool TryUpgradePower()
    {
        int lv = GetPowerLevel();
        if (lv >= MaxUpgradeLevel || GetGold() < UpgradeCostGold)
            return false;

        SetGold(GetGold() - UpgradeCostGold);
        SetPowerLevel(lv + 1);
        PlayerPrefs.Save();
        OnUpgradesChanged?.Invoke();
        OnGoldChanged?.Invoke(GetGold());
        return true;
    }

    public static bool TryUpgradeSpeed()
    {
        int lv = GetSpeedLevel();
        if (lv >= MaxUpgradeLevel || GetGold() < UpgradeCostGold)
            return false;

        SetGold(GetGold() - UpgradeCostGold);
        SetSpeedLevel(lv + 1);
        PlayerPrefs.Save();
        OnUpgradesChanged?.Invoke();
        OnGoldChanged?.Invoke(GetGold());
        return true;
    }

    public static bool TryUpgradeHp()
    {
        int lv = GetHpLevel();
        if (lv >= MaxUpgradeLevel || GetGold() < UpgradeCostGold)
            return false;

        SetGold(GetGold() - UpgradeCostGold);
        SetHpLevel(lv + 1);
        PlayerPrefs.Save();
        OnUpgradesChanged?.Invoke();
        OnGoldChanged?.Invoke(GetGold());
        return true;
    }

    /// <summary>
    /// Writes gold and all upgrade levels in one save (e.g. main menu upgrade screen commit).
    /// </summary>
    public static void CommitMeta(int gold, int powerLevel, int speedLevel, int hpLevel)
    {
        gold = Mathf.Max(0, gold);
        powerLevel = Mathf.Clamp(powerLevel, 0, MaxUpgradeLevel);
        speedLevel = Mathf.Clamp(speedLevel, 0, MaxUpgradeLevel);
        hpLevel = Mathf.Clamp(hpLevel, 0, MaxUpgradeLevel);

        PlayerPrefs.SetInt(KeyGold, gold);
        PlayerPrefs.SetInt(KeyPower, powerLevel);
        PlayerPrefs.SetInt(KeySpeed, speedLevel);
        PlayerPrefs.SetInt(KeyHp, hpLevel);
        PlayerPrefs.Save();

        OnGoldChanged?.Invoke(GetGold());
        OnUpgradesChanged?.Invoke();
    }

    /// <summary>
    /// Rebuilds combat stats from baseline + meta + roguelite run bonuses. Call after scene load.
    /// </summary>
    public static void ApplyMetaToPlayer(
        PlayerController pc,
        Health hp,
        ref float runMaxHealth,
        ref float runCurrentHealth,
        float rogueMoveBonus,
        float rogueDamageBonus,
        float rogueHpExtraFromCards)
    {
        if (pc == null || hp == null)
            return;

        const float BaseMove = 8f;
        const float BaseMelee = 10f;
        const float BaseMelee3D = 20f;
        const float BaseHp = 100f;

        pc.moveSpeed = BaseMove + GetSpeedBonus() + rogueMoveBonus;
        pc.meleeDamage = BaseMelee + GetPowerBonus() + rogueDamageBonus;
        pc.melee3DDamage = BaseMelee3D + GetPowerBonus() + rogueDamageBonus;

        float newMax = BaseHp + GetHpBonus() + rogueHpExtraFromCards;
        hp.SetMaxHealth(newMax, true);
        runMaxHealth = hp.MaxHealth;
        runCurrentHealth = hp.CurrentHealth;
    }
}
