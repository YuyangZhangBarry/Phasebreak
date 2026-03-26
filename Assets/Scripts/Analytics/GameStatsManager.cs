using System;
using System.IO;
using UnityEngine;

/// <summary>
/// In-game statistics singleton: persists across scenes and stores <see cref="PlayerStatsData"/>
/// as JSON under <see cref="Application.persistentDataPath"/> (easy to replace with a remote backend later).
/// </summary>
[DefaultExecutionOrder(-50)]
public class GameStatsManager : MonoBehaviour
{
    public static GameStatsManager Instance { get; private set; }

    /// <summary>Disk file name (under persistentDataPath).</summary>
    private const string FileName = "player_game_stats.json";

    [Tooltip("If true, save to disk immediately after each change (small data; recommended).")]
    [SerializeField] private bool saveImmediatelyOnChange = true;

    /// <summary>In-memory statistics data (do not edit directly from outside; use this class API).</summary>
    private PlayerStatsData _data;

    /// <summary>Read-only access to a cloned snapshot.</summary>
    public PlayerStatsData Snapshot => _data != null ? _data.Clone() : PlayerStatsData.CreateDefault();

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadOrCreate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveToDisk();
    }

    private void OnApplicationQuit()
    {
        SaveToDisk();
    }

    /// <summary>
    /// If the manager doesn't exist in the current scene yet, this will dynamically create a hidden
    /// GameObject and attach this component to it.
    /// </summary>
    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject(nameof(GameStatsManager));
        go.AddComponent<GameStatsManager>();
    }

    /// <summary>Call when player dies: increments total attempts by +1.</summary>
    public void RecordDeath()
    {
        EnsureData();
        _data.totalAttempts++;
        OnDataChanged();
    }

    /// <summary>
    /// Accumulate play time by dimension (seconds). Usually called every frame by
    /// <see cref="GameplayTimeStatsTracker"/> with deltaTime.
    /// </summary>
    /// <param name="is2D">true = 2D mode, false = 3D mode.</param>
    /// <param name="deltaTime">Seconds to accumulate this frame (typically Time.deltaTime or Time.unscaledDeltaTime).</param>
    public void AddTime(bool is2D, float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        EnsureData();
        if (is2D)
            _data.timeSpent2D += deltaTime;
        else
            _data.timeSpent3D += deltaTime;

        OnDataChanged();
    }

    /// <summary>Call when a roguelite upgrade card is picked: increment count by upgrade type.</summary>
    public void RecordUpgrade(UpgradeType type)
    {
        EnsureData();
        switch (type)
        {
            case UpgradeType.DamageUp:
                _data.upgradeCountDamage++;
                break;
            case UpgradeType.MaxHealthUp:
                _data.upgradeCountHealth++;
                break;
            case UpgradeType.MoveSpeedUp:
                _data.upgradeCountSpeed++;
                break;
            default:
                return;
        }

        OnDataChanged();
    }

    private void EnsureData()
    {
        if (_data == null)
            _data = PlayerStatsData.CreateDefault();
    }

    private void OnDataChanged()
    {
        if (saveImmediatelyOnChange)
            SaveToDisk();
    }

    /// <summary>Load from disk; if missing or corrupted, use default data.</summary>
    public void LoadOrCreate()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    PlayerStatsData parsed = JsonUtility.FromJson<PlayerStatsData>(json);
                    _data = parsed ?? PlayerStatsData.CreateDefault();
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameStatsManager] Failed to load stats; using default data. {e.Message}");
        }

        _data = PlayerStatsData.CreateDefault();
        SaveToDisk();
    }

    /// <summary>Write current data to JSON file.</summary>
    public void SaveToDisk()
    {
        EnsureData();
        try
        {
            string json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameStatsManager] Failed to save stats: {e.Message}");
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor/Debug: clear stats and save to disk.</summary>
    [ContextMenu("Debug/Clear Stats And Save")]
    private void DebugClearStats()
    {
        _data = PlayerStatsData.CreateDefault();
        SaveToDisk();
    }
#endif
}
